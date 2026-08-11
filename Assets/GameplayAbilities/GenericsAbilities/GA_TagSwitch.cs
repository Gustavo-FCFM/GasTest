using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GA_TagSwitch  (genérico — "tu próximo ataque cambia")
//
// Una habilidad ENVOLTORIO: no hace nada por sí misma, sino que ejecuta OTRA
// según los tags que tenga el dueño en ese momento. Es la pieza que faltaba para
// las mecánicas de "carga tu próximo golpe": el GA que otorga el tag (un
// GA_SelfBuff cualquiera) solo pone el estado — quien tiene que CAMBIAR es el
// ataque principal, y eso se resuelve acá.
//
// Caso del Paladín (Castigo divino): el LMB es este switch.
//   · Sin tag                 → el cono de martillo de siempre.
//   · Con Status_DivineSmite  → un GA_ComboSequence que lanza el cono Y la estela
//                               de luz a la vez (dos pasos con DelayAfter = 0), y
//                               consume el tag.
// Lo van a reusar el ult de Conquista ("todos sus ataques aturden") y el de
// Venganza ("su ataque principal hace un corte avanzando").
//
// COSTO Y COOLDOWN son del SWITCH, no de las variantes: el ritmo del ataque
// principal tiene que ser el mismo se dispare la variante que se dispare. A las
// variantes se les limpia el suyo al clonarlas, por el mismo motivo que a los
// pasos de un combo (si el asset trae su propio CooldownEffect —porque también
// sirve como ataque suelto— su CanActivate ve el tag que el switch acaba de
// aplicar y no se ejecuta, en silencio).
// ============================================================
[CreateAssetMenu(fileName = "GA_TagSwitch", menuName = "GAS/Generics/Tag Switch")]
public class GA_TagSwitch : GameplayAbility
{
    // Una variante: qué tag la activa y qué habilidad se ejecuta en su lugar.
    [System.Serializable]
    public struct TagVariant
    {
        [Tooltip("Tag que el dueño tiene que tener para que se dispare esta variante.")]
        public EGameplayTag RequiredTag;

        [Tooltip("Habilidad a ejecutar cuando el tag está presente.")]
        public GameplayAbility Ability;

        [Tooltip("Si al usarla se consume el tag (o sea, es de UN solo uso: 'tu PRÓXIMO ataque'). " +
                 "Desactivado = la variante sigue activa mientras dure el efecto que da el tag " +
                 "(el caso de un ult que cambia tus ataques por un rato).")]
        public bool ConsumeTag;
    }

    [Header("Variante por Defecto")]
    [Tooltip("La que se ejecuta cuando no aplica ninguna variante (el ataque normal).")]
    public GameplayAbility DefaultAbility;

    [Header("Variantes por Tag")]
    [Tooltip("Se evalúan EN ORDEN y gana la primera cuyo tag esté presente. Poné arriba las " +
             "más específicas o las que deban tener prioridad si se solapan dos estados.")]
    public List<TagVariant> Variants;

    // Instancias clonadas de cada variante (una por habilidad, cacheada). Se crean
    // la primera vez que hacen falta y se reusan: las habilidades guardan estado de
    // runtime (cargas, cooldowns internos), así que clonarlas en cada activación lo
    // perdería. NonSerialized: estado por instancia otorgada.
    [System.NonSerialized] private Dictionary<GameplayAbility, GameplayAbility> _instances;

    // Qué variante se ejecutó en la última activación. La necesita
    // ResolveAnimationSource DESPUÉS de Activate(): para entonces el tag ya se
    // consumió, así que volver a resolverlo por tag daría la variante equivocada.
    [System.NonSerialized] private GameplayAbility _lastResolved;

    // =========================================================
    // ACTIVACIÓN
    // =========================================================

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        // Se resuelve ANTES de cobrar: si no hay ninguna variante ni habilidad por
        // defecto configurada, no gastamos cooldown por nada.
        GameplayAbility template = ResolveTemplate(out bool consumeTag, out EGameplayTag usedTag);
        if (template == null)
        {
            Debug.LogWarning($"[{AbilityName}] no tiene DefaultAbility ni ninguna variante aplicable.");
            EndAbility();
            return;
        }

        CommitAbility();

        if (consumeTag) ConsumeTagFromOwner(usedTag);

        GameplayAbility instance = GetOrCreateInstance(template);
        _lastResolved = instance;

        // La variante hereda el ritmo del switch: si el ataque principal se acelera
        // con la velocidad de ataque, la versión cargada tiene que acelerarse igual.
        instance.AnimationSpeedOverride = AnimationSpeedOverride;

        instance.Activate();

        // El fin de la habilidad lo maneja la VARIANTE (su propio EndAbility, que
        // llega cuando termina su secuencia). Llamarlo también acá liberaría
        // isAttacking antes de tiempo y dejaría al jugador atacar encima del swing.
    }

    // Elige qué habilidad ejecutar: la primera variante cuyo tag tenga el dueño, o
    // la habilidad por defecto.
    private GameplayAbility ResolveTemplate(out bool consumeTag, out EGameplayTag usedTag)
    {
        consumeTag = false;
        usedTag    = EGameplayTag.None;

        if (OwnerASC != null && Variants != null)
        {
            foreach (var variant in Variants)
            {
                if (variant.Ability == null || variant.RequiredTag == EGameplayTag.None) continue;
                if (!OwnerASC.HasTag(variant.RequiredTag)) continue;

                consumeTag = variant.ConsumeTag;
                usedTag    = variant.RequiredTag;
                return variant.Ability;
            }
        }

        return DefaultAbility;
    }

    // Consume el tag de un solo uso. Primero se retira el EFECTO que lo otorga (que
    // es como llegan estos estados: un GE con duración de un GA_SelfBuff); si aun así
    // el tag sigue puesto, se lo quita a mano — hay mecánicas que lo otorgan sueltas,
    // igual que hace el crítico asegurado.
    private void ConsumeTagFromOwner(EGameplayTag tag)
    {
        if (OwnerASC == null || tag == EGameplayTag.None) return;

        OwnerASC.RemoveEffectsWithTag(tag);
        if (OwnerASC.HasTag(tag)) OwnerASC.RemoveTag(tag);
    }

    // Clon propio de una variante, cacheado por template. Se le limpian costo y
    // cooldown porque el ciclo de vida es del switch (ver cabecera).
    private GameplayAbility GetOrCreateInstance(GameplayAbility template)
    {
        _instances ??= new Dictionary<GameplayAbility, GameplayAbility>();

        if (_instances.TryGetValue(template, out GameplayAbility cached) && cached != null)
            return cached;

        GameplayAbility instance = Instantiate(template);
        instance.Initialize(OwnerASC);
        instance.SourceTemplate = template;   // para resolver su índice en GameplayAbilityRegistry
        instance.CooldownEffect = null;
        instance.CostEffect     = null;

        _instances[template] = instance;
        return instance;
    }

    // =========================================================
    // ANIMACIÓN
    // =========================================================

    // La animación es la de la variante que se ejecuta, no la de este envoltorio
    // (que no tiene clip propio).
    //
    // Dos momentos distintos la consultan y por eso hay dos caminos:
    //   · El SERVIDOR, después de Activate(): ahí el tag ya se consumió, así que
    //     resolver por tag daría la variante equivocada — se usa la que se guardó.
    //   · El DUEÑO REMOTO, al predecir: su Activate() nunca corrió (es server-only),
    //     así que _lastResolved está vacío y se resuelve por tag en vivo. Funciona
    //     porque los tags se sincronizan.
    public override GameplayAbility ResolveAnimationSource()
    {
        if (_lastResolved != null) return _lastResolved.ResolveAnimationSource();

        GameplayAbility template = ResolveTemplate(out _, out _);
        return template != null ? template.ResolveAnimationSource() : this;
    }

    // Un paso de combo de la variante (si la variante es un combo). Deja que la
    // réplica de pasos funcione igual a través del switch.
    public override AnimationClip GetStepAnimationClip(int sequenceIndex, int stepIndex)
    {
        GameplayAbility source = ResolveAnimationSource();
        return source != null && source != this ? source.GetStepAnimationClip(sequenceIndex, stepIndex) : null;
    }

    // Dibuja el área de la variante por defecto, para poder ajustar el ataque
    // principal desde el jugador seleccionado como con cualquier otra habilidad.
    public override void DrawGizmos(Transform origin)
    {
        DefaultAbility?.DrawGizmos(origin);
    }
}
