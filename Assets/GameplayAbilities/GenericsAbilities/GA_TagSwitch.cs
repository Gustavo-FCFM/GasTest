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
public class GA_TagSwitch : GameplayAbility, IGroundTargetAbility, IWallTargetAbility
{
    // Si la variante que se dispararía AHORA se apunta en el piso (el Aliento de dragón rojo
    // del Maestro elemental: una zona en la retícula), el switch también: mantener → apuntar
    // → soltar. Con una variante que no se apunta, nada cambia.
    public float MaxTargetRange   => ResolveTemplate(out _, out _) is IGroundTargetAbility g ? g.MaxTargetRange : 0f;
    public float TargetRadius     => ResolveTemplate(out _, out _) is IGroundTargetAbility g ? g.TargetRadius : 0f;
    public bool  UsesGroundTarget => ResolveTemplate(out _, out _) is IGroundTargetAbility g && g.UsesGroundTarget;

    // Y si su marcador es un muro (el Muro de fuego de la Q del Mago), también.
    public bool  UsesWallMarker   => ResolveTemplate(out _, out _) is IWallTargetAbility w && w.UsesWallMarker;
    public float WallWidth        => ResolveTemplate(out _, out _) is IWallTargetAbility w ? w.WallWidth : 1f;
    public float WallDepth        => ResolveTemplate(out _, out _) is IWallTargetAbility w ? w.WallDepth : 1f;
    public int   WallSegments     => ResolveTemplate(out _, out _) is IWallTargetAbility w ? w.WallSegments : 1;
    public float WallRotationStep => ResolveTemplate(out _, out _) is IWallTargetAbility w ? w.WallRotationStep : 0f;

    // Una variante: qué tag la activa y qué habilidad se ejecuta en su lugar.
    [System.Serializable]
    public struct TagVariant
    {
        [Tooltip("Tag que el dueño tiene que tener para que se dispare esta variante.")]
        [Label("Tag necesario")]
        public EGameplayTag RequiredTag;

        [Tooltip("Habilidad a ejecutar cuando el tag está presente.")]
        [Label("Habilidad de la variante")]
        public GameplayAbility Ability;

        [Tooltip("Si al usarla se consume el tag (o sea, es de UN solo uso: 'tu PRÓXIMO ataque'). " +
                 "Desactivado = la variante sigue activa mientras dure el efecto que da el tag " +
                 "(el caso de un ult que cambia tus ataques por un rato).")]
        [Label("Gasta el tag")]
        public bool ConsumeTag;

        [Tooltip("Solo con ShowVariantIcon: el ícono del botón mientras esta variante sea la que " +
                 "se dispararía. Vacío = el ícono de su habilidad.")]
        [Label("Ícono de la variante")]
        public Sprite Icon;

        [Tooltip("OPCIONAL: con este tag la variante también se dispara, y SIN gastar nada (la " +
                 "Paz mental del Maestro elemental: mientras dura, todo sale con Ki y el Ki no se " +
                 "gasta). None = solo con RequiredTag.")]
        [Label("También activa con tag")]
        public EGameplayTag AlsoActiveWithTag;
    }

    // ¿Esta variante se dispararía ahora? 'free' = por AlsoActiveWithTag: no gasta el tag.
    private bool IsVariantActive(TagVariant variant, out bool free)
    {
        free = variant.AlsoActiveWithTag != EGameplayTag.None && OwnerASC.HasTag(variant.AlsoActiveWithTag);
        return free || OwnerASC.HasTag(variant.RequiredTag);
    }

    [Section(AbilitySection.General)]
    [Tooltip("Si el botón muestra el ícono de la variante que se dispararía AHORA, en vez del " +
             "propio. Lo usan las posturas del Guerrero (Q y Shift) para que se vea en qué " +
             "postura está. Apagado = el ícono de siempre (el Castigo divino del Paladín).")]
    [Label("Mostrar ícono de la variante")]
    public bool ShowVariantIcon = false;

    [ShowIf(nameof(ShowVariantIcon))]
    [Tooltip("El ícono cuando no aplica ninguna variante. Vacío = el de DefaultAbility.")]
    [Label("Ícono por defecto")]
    public Sprite DefaultIcon;

    [Section("Variantes")]
    [Tooltip("La que se ejecuta cuando no aplica ninguna variante (el ataque normal).")]
    [Label("Habilidad por defecto")]
    public GameplayAbility DefaultAbility;

    [Tooltip("Se evalúan EN ORDEN y gana la primera cuyo tag esté presente. Poné arriba las " +
             "más específicas o las que deban tener prioridad si se solapan dos estados.")]
    [Label("Variantes")]
    public List<TagVariant> Variants;

    [Tooltip("Cada variante cobra SU costo (el maná de cada hechizo del Mago) y canaliza con SU " +
             "sección Canalizar (la Bola de fuego, 0.5 s; los misiles, nada). El ritmo (el cooldown) " +
             "sigue siendo del switch. Apagado = lo de siempre: cobra el switch y la variante sale gratis.")]
    [Label("Las variantes cobran y canalizan lo suyo")]
    public bool VariantsPayOwnCost = false;

    [ShowIf(nameof(VariantsPayOwnCost))]
    [Tooltip("Si no alcanza para el costo de una variante, sale la habilidad por defecto y se le " +
             "quita al dueño el tag de esa variante (el Mago sin maná vuelve a los misiles). También " +
             "apenas la lanza, si ya no le alcanza para otra.")]
    [Label("Sin para pagarla: vuelve a la de por defecto")]
    public bool FallBackWhenUnaffordable = true;

    // ¿Alcanza para pagar esta variante? Solo cuenta si cada una cobra lo suyo y se vuelve a la
    // de por defecto sin con qué pagarla. Corre en el dueño y en el servidor (el maná viaja).
    private bool CanPay(GameplayAbility template)
        => !VariantsPayOwnCost || !FallBackWhenUnaffordable || template == null || OwnerASC == null ||
           template.CostEffect == null || OwnerASC.CanAffordGameplayEffect(template.CostEffect);

    // Sin con qué pagar la variante elegida: se le quita su tag (el hechizo del Mago vuelve a los
    // misiles, y la Q a recargar maná). Servidor.
    protected void DropUnpayableVariants()
    {
        if (!VariantsPayOwnCost || !FallBackWhenUnaffordable || OwnerASC == null || Variants == null) return;
        foreach (var variant in Variants)
            if (variant.Ability != null && variant.RequiredTag != EGameplayTag.None &&
                OwnerASC.HasTag(variant.RequiredTag) && !CanPay(variant.Ability))
                OwnerASC.RemoveEffectsWithTag(variant.RequiredTag);
    }

    // Envoltorio: busca y golpea la variante que se ejecuta, no esta.
    public override bool UsesTargetLayer => false;
    public override bool UsesHitEffects  => false;

    // Instancias clonadas de cada variante (una por habilidad, cacheada). Se crean
    // la primera vez que hacen falta y se reusan: las habilidades guardan estado de
    // runtime (cargas, cooldowns internos), así que clonarlas en cada activación lo
    // perdería. NonSerialized: estado por instancia otorgada.
    [System.NonSerialized] private Dictionary<GameplayAbility, GameplayAbility> _instances;

    // Qué variante se ejecutó en la última activación. La necesita
    // ResolveAnimationSource DESPUÉS de Activate(): para entonces el tag ya se
    // consumió, así que volver a resolverlo por tag daría la variante equivocada.
    [System.NonSerialized] protected GameplayAbility _lastResolved;

    // =========================================================
    // ACTIVACIÓN
    // =========================================================

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        DropUnpayableVariants();

        // Se resuelve ANTES de cobrar: si no hay ninguna variante ni habilidad por
        // defecto configurada, no gastamos cooldown por nada.
        GameplayAbility template = ResolveTemplate(out bool consumeTag, out EGameplayTag usedTag);
        if (template == null)
        {
            Debug.LogWarning($"[{AbilityName}] no tiene DefaultAbility ni ninguna variante aplicable.");
            EndAbility();
            return;
        }

        // Una repetición de su continuo: sale con la variante que empezó la canalización, y gratis
        // después de la primera (ver ActivateAsCastTick). Se mira ANTES de cobrar el switch.
        bool tick     = IsCastTick;
        bool tickPaid = tick && CastTickPaid;
        if (tick && CastSource != null && CastSource != this) template = CastSource;

        CommitAbility();

        if (consumeTag && !tickPaid) ConsumeTagFromOwner(usedTag);

        GameplayAbility instance = GetOrCreateInstance(template);
        _lastResolved = instance;

        // La variante hereda el ritmo del switch: si el ataque principal se acelera
        // con la velocidad de ataque, la versión cargada tiene que acelerarse igual.
        instance.AnimationSpeedOverride = AnimationSpeedOverride;

        // Y hereda si se puede CORTAR. El slot del ataque principal marca como
        // interrumpible al SWITCH (ver EquipCharacterClass), pero quien pega es la
        // variante clonada, que nacía sin la marca: su golpe no miraba el contador de
        // cancelación y pegaba aunque otra habilidad lo hubiera cortado. Con el Paladín
        // eso era clic izquierdo + clic derecho: el martillo pegaba igual y el escudo
        // quedaba arriba — golpear y bloquear a la vez. El cooldown ya pagado no se
        // devuelve: cancelar tiene ese costo, como en cualquier otro básico.
        instance.IsInterruptible = IsInterruptible;

        if (tick) instance.ActivateAsCastTick(tickPaid);
        else      instance.Activate();

        // Ya cobró: si no le alcanza para otra, vuelve ya a la de por defecto (el botón cambia).
        DropUnpayableVariants();

        // El fin de la habilidad lo maneja la VARIANTE (su propio EndAbility, que
        // llega cuando termina su secuencia). Llamarlo también acá liberaría
        // isAttacking antes de tiempo y dejaría al jugador atacar encima del swing.
    }

    // Si alguna de sus habilidades se mueve por el dueño (la Patada voladora del Monje), el
    // switch también: un bot no la propone.
    public override bool MovesThroughOwner
    {
        get
        {
            if (DefaultAbility != null && DefaultAbility.MovesThroughOwner) return true;
            if (Variants != null)
                foreach (var variant in Variants)
                    if (variant.Ability != null && variant.Ability.MovesThroughOwner) return true;
            return false;
        }
    }

    // Si la variante que corrió es un combo, él manda la animación de cada paso: la del
    // switch pisaría la del primer golpe en las demás pantallas (ver NetworkASC).
    public override bool BroadcastsOwnAnimation => _lastResolved != null && _lastResolved.BroadcastsOwnAnimation;

    // El ícono de la variante que se dispararía ahora (ver ShowVariantIcon). Corre en el
    // dueño: los tags le llegan sincronizados, así que el botón cambia solo.
    public override Sprite CurrentIcon
    {
        get
        {
            if (!ShowVariantIcon || OwnerASC == null) return AbilityIcon;

            if (Variants != null)
                foreach (var variant in Variants)
                {
                    if (variant.Ability == null || variant.RequiredTag == EGameplayTag.None) continue;
                    if (!IsVariantActive(variant, out _) || !CanPay(variant.Ability)) continue;
                    return variant.Icon != null ? variant.Icon
                         : variant.Ability.AbilityIcon != null ? variant.Ability.AbilityIcon : AbilityIcon;
                }

            if (DefaultIcon != null) return DefaultIcon;
            return DefaultAbility != null && DefaultAbility.AbilityIcon != null ? DefaultAbility.AbilityIcon : AbilityIcon;
        }
    }

    // Con el escudo arriba, decide la variante que se dispararía AHORA (o el propio switch,
    // si lo marcaron a él). Corre en el dueño y en el servidor: los dos ven los mismos tags.
    public override bool CanUseWhileHolding
    {
        get
        {
            if (UsableWhileHolding) return true;
            GameplayAbility template = ResolveTemplate(out _, out _);
            return template != null && template.UsableWhileHolding;
        }
    }

    // Elige qué habilidad ejecutar: la primera variante cuyo tag tenga el dueño, o
    // la habilidad por defecto.
    protected GameplayAbility ResolveTemplate(out bool consumeTag, out EGameplayTag usedTag)
    {
        consumeTag = false;
        usedTag    = EGameplayTag.None;

        if (OwnerASC != null && Variants != null)
        {
            foreach (var variant in Variants)
            {
                if (variant.Ability == null || variant.RequiredTag == EGameplayTag.None) continue;
                if (!IsVariantActive(variant, out bool free)) continue;
                if (!CanPay(variant.Ability)) continue;   // sin con qué pagarla: la que sigue (o la de por defecto)

                consumeTag = variant.ConsumeTag && !free;
                usedTag    = variant.RequiredTag;
                return variant.Ability;
            }
        }

        return DefaultAbility;
    }

    // La habilidad que saldría AHORA (la variante, o la de por defecto). Corre en el dueño y en el
    // servidor. La usa el cerebro de los bots para juzgar la Q del Mago por lo que es en este momento.
    public GameplayAbility CurrentVariant => ResolveTemplate(out _, out _);

    // Consume el tag de un solo uso. Primero se retira el EFECTO que lo otorga (que
    // es como llegan estos estados: un GE con duración de un GA_SelfBuff); si aun así
    // el tag sigue puesto, se lo quita a mano — hay mecánicas que lo otorgan sueltas,
    // igual que hace el crítico asegurado.
    protected void ConsumeTagFromOwner(EGameplayTag tag)
    {
        if (OwnerASC == null || tag == EGameplayTag.None) return;

        // UNA sola acumulación por uso. Si el estado se puede acumular (ej. las 2
        // cargas del Castigo divino), gastar una deja la otra viva: el conteo del tag
        // baja de 2 a 1 y el jugador sigue cargado para el próximo golpe. Antes se
        // borraban TODAS de una y la segunda carga se perdía sin usarse.
        if (OwnerASC.RemoveOneEffectWithTag(tag)) return;

        // No vino de ningún efecto: es un tag suelto (como el crítico asegurado),
        // así que se lo quita a mano.
        if (OwnerASC.HasTag(tag)) OwnerASC.RemoveTag(tag);
    }

    // Si a las variantes se les limpia costo y cooldown (lo normal: el ciclo de vida es
    // del switch, ver cabecera). GA_HoldTagSwitch lo apaga: ahí cada variante paga lo
    // suyo, porque el escudo y el ataque cargado no comparten cooldown.
    protected virtual bool StripVariantCooldowns => true;

    // Clon propio de una variante, cacheado por template. Se le limpian costo y
    // cooldown porque el ciclo de vida es del switch (ver cabecera).
    protected GameplayAbility GetOrCreateInstance(GameplayAbility template)
    {
        _instances ??= new Dictionary<GameplayAbility, GameplayAbility>();

        if (_instances.TryGetValue(template, out GameplayAbility cached) && cached != null)
            return cached;

        GameplayAbility instance = Instantiate(template);
        instance.Initialize(OwnerASC);
        instance.SourceTemplate = template;   // para resolver su índice en GameplayAbilityRegistry
        instance.ReportEndAs    = this;       // su fin se avisa con el nombre del switch (ver EndAbility)
        if (StripVariantCooldowns)
        {
            instance.CooldownEffect = null;
            if (!VariantsPayOwnCost) instance.CostEffect = null;   // si cobra lo suyo, el costo se queda
            instance.DisableCharges();
        }

        _instances[template] = instance;
        return instance;
    }

    // =========================================================
    // CANALIZAR CON LA VARIANTE ("Las variantes cobran y canalizan lo suyo")
    // =========================================================

    // Canaliza con los datos de la que va a salir: la Bola de fuego 0.5 s, la Flecha helada 1 s,
    // los misiles nada. Corre en el dueño y en el servidor: los dos ven los mismos tags y el
    // mismo maná.
    public override GameplayAbility CastSettingsSource
        => VariantsPayOwnCost && OwnerASC != null ? (ResolveTemplate(out _, out _) ?? this) : this;

    // Cortada a mitad de canalizar cuenta como lanzada: cobra también lo de la variante que iba
    // a salir (el maná del hechizo y, en un switch de mantener, su ritmo).
    protected override void PayCutCast()
    {
        if (!VariantsPayOwnCost || OwnerASC == null) return;
        GameplayAbility template = ResolveTemplate(out _, out _);
        if (template != null) GetOrCreateInstance(template).PayAsCast();
        DropUnpayableVariants();
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
        if (DefaultAbility != null) DefaultAbility.DrawGizmos(origin);
    }

#if UNITY_EDITOR
    // Las manijas ajustan el asset de la variante por defecto (es la forma que se ve).
    public override void DrawSceneHandles(Transform origin)
    {
        if (DefaultAbility != null && DefaultAbility != this) DefaultAbility.DrawSceneHandles(origin);
    }
#endif
}
