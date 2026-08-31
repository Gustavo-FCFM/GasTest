using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_ContinuousAoE
//
// Zona de efecto que dura un tiempo (TotalDuration) y le aplica una
// lista de GameplayEffect a todo lo que esté dentro de su radio,
// a intervalos regulares (TickInterval). Puede quedarse fija en el
// punto de activación o seguir al dueño (FollowOwner). Pensada para
// auras, charcos de veneno, tornados, etc.
//
// DÓNDE SE DESPLIEGA (DeployMode): sobre el propio dueño, o en la ZONA APUNTADA con
// la retícula (mantener el botón → marcador en el suelo → soltar, ver
// IGroundTargetAbility). Con retícula el área siempre queda FIJA en el punto elegido
// (FollowOwner se ignora): ahí es donde cae, no puede seguirte.
//
// Para un golpe de área de UNA sola aplicación (sin ticks ni duración) usá
// GA_InstantAoE — este es para zonas que persisten.
// ============================================================
[CreateAssetMenu(fileName = "GA_ContinuousAoE", menuName = "GAS/Generics/Continuous AoE")]
public class GA_ContinuousAoE : GameplayAbility, IGroundTargetAbility, IChanneledAbility
{
    // A quién afecta el área.
    public enum EAoETarget { Enemies, Allies, All }

    // Dónde nace el área. AtOwner es el valor 0 = el comportamiento de siempre, así
    // que los assets ya configurados no cambian.
    public enum EAoEDeploy { AtOwner, AtReticle }

    [Header("Targets")]
    // FormerlySerializedAs: este campo se llamaba "Objetivos". Unity serializa los
    // campos por NOMBRE, así que sin esto los assets ya configurados perderían su
    // valor en silencio al renombrarlo.
    [UnityEngine.Serialization.FormerlySerializedAs("Objetivos")]
    public EAoETarget Targets = EAoETarget.Enemies;

    [Header("Configuración de Área")]
    public float Radius        = 4f;
    public float TotalDuration = 5f;
    // Cada cuánto se vuelve a revisar quién está dentro del área y se le
    // reaplican los efectos.
    public float TickInterval  = 0.5f;

    [Header("Despliegue")]
    [Tooltip("AtOwner: el área nace sobre el dueño (comportamiento clásico). AtReticle: se apunta " +
             "con el marcador en el suelo (mantener → apuntar → soltar) y queda fija ahí.")]
    public EAoEDeploy DeployMode = EAoEDeploy.AtOwner;

    [Tooltip("Solo con AtReticle: alcance máximo al que se puede desplegar la zona.")]
    public float MaxRange = 15f;

    // IGroundTargetAbility: el marcador del suelo usa estos valores para la vista
    // previa. PlayerController solo muestra el marcador si DeployMode es AtReticle
    // (ver UsesGroundTarget).
    public float MaxTargetRange => MaxRange;
    public float TargetRadius   => Radius;

    // True si esta configuración se apunta con la retícula.
    public bool UsesGroundTarget => DeployMode == EAoEDeploy.AtReticle;

    [Header("Comportamiento")]
    // Si el área se mueve con el dueño o queda fija donde se activó. Se ignora con
    // DeployMode = AtReticle (una zona apuntada siempre queda fija donde cae).
    public bool FollowOwner = true;

    [Header("Efectos Visuales")]
    public GameObject VisualPrefab;

    [Header("Canalizado — animación sostenida y giro")]
    [Tooltip("Clip que se reproduce EN BUCLE mientras dura el área. Es lo que convierte el " +
             "molinete en un molinete: sin esto la habilidad dispara un mandoble suelto que " +
             "termina en un segundo, y el personaje se queda quieto los otros nueve mientras el " +
             "daño sigue ticando.\n\n" +
             "VACÍO = comportamiento de siempre (el AnimationClip de arriba, una sola vez).\n\n" +
             "Reusa las ranuras del MANTENIDO en el Animator, así que no hay que crear estados " +
             "nuevos: los del escudo sirven tal cual.")]
    public AnimationClip ChannelLoopAnimation;

    [Tooltip("OPCIONAL: arranque, una sola vez, antes de entrar al bucle.")]
    public AnimationClip ChannelStartAnimation;

    [Tooltip("OPCIONAL: remate al terminar el área.")]
    public AnimationClip ChannelEndAnimation;

    [Tooltip("Grados por segundo que gira el MODELO sobre su eje mientras dura el área — el " +
             "molinete tipo Garen. 0 = sin giro.\n\n" +
             "Gira el modelo y NO la raíz a propósito: la raíz la maneja la mira y el " +
             "movimiento WASD se calcula relativo a la cámara. Como el área es un radio sin " +
             "dirección, girar el modelo no cambia a quién le pega.\n\n" +
             "360 = una vuelta por segundo. Para un molinete rápido, 500-700.")]
    public float ModelSpinSpeed = 0f;

    [Tooltip("Mientras dure el área, el personaje NO puede usar ninguna otra habilidad — salvo " +
             "las que tengan marcado 'Usable While Channeling' en su propio asset.\n\n" +
             "Así se arma la lista de excepciones desde el lado de las POCAS que sí (Frenzy y el " +
             "salto, para el molinete) en vez de enumerar todas las que no. Una habilidad nueva " +
             "nace bloqueada, que es lo seguro.\n\n" +
             "El bloqueo dura lo mismo que el área (TotalDuration), no lo que tarde el jugador " +
             "en quedar libre.")]
    public bool BlockOtherAbilities = false;

    // IChanneledAbility: la capa de red lee los clips y el giro por acá.
    public AnimationClip ChannelStartClip => ChannelStartAnimation;
    public AnimationClip ChannelLoopClip  => ChannelLoopAnimation;
    public AnimationClip ChannelEndClip   => ChannelEndAnimation;
    public float         SpinSpeed        => ModelSpinSpeed;
    // Multiplica Radius para el tamaño del VFX (no afecta el área real).
    public float VisualScaleMultiplier = 2.0f;

    [Header("Lista de Efectos")]
    // Todos los GameplayEffect que se le aplican a cada objetivo válido
    // en cada tick.
    public List<GameplayEffect> EffectsToApply;

    [Header("Sincronización")]
    // Espera antes de que el área empiece a existir, tras activar la habilidad.
    public float StartDelay = 0.5f;

    // ¿El área sigue al dueño? Solo puede seguirlo si nace sobre él: una zona
    // apuntada con la retícula queda siempre fija donde cayó.
    private bool ShouldFollowOwner => FollowOwner && DeployMode == EAoEDeploy.AtOwner;

    // Valida, cobra costo/cooldown y arranca la secuencia del área.
    public override void Activate()
    {
        if (!IsServer) return;   // ← NUEVO
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();

            // El centro se resuelve ACÁ, al activar: con la retícula, el punto de mira
            // es el del instante del casteo (si lo leyéramos después del StartDelay, el
            // jugador ya podría estar apuntando a otro lado).
            Vector3 center = ResolveCenter(pc);

            // Con clip de bucle la animacion la maneja el canalizado (sostenida + giro) y no
            // el disparo suelto de siempre. Va por la capa de red porque Activate() corre en
            // el SERVIDOR: sin el RPC, el dueno remoto no veria nada.
            var netAscAnim = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
            if (ChannelLoopAnimation != null && netAscAnim != null)
                netAscAnim.ServerPlayChannelAnimation(this, true);
            else if (pc != null) pc.PlayAnimation(this);

            // El bloqueo se marca con un TAG y no con un flag interno: asi lo ve
            // CanActivate de cualquier habilidad sin que ninguna tenga que conocer a esta.
            if (BlockOtherAbilities) OwnerASC.AddTag(EGameplayTag.Status_Channeling);
            OwnerASC.StartAbilityCoroutine(AoESequence(center));
        }
    }

    // Punto donde nace el área: el dueño, o la zona apuntada (recortada a MaxRange
    // para que coincida con la vista previa del marcador).
    private Vector3 ResolveCenter(PlayerController pc)
    {
        Vector3 origin = OwnerASC.transform.position;
        if (DeployMode == EAoEDeploy.AtOwner) return origin;

        Vector3 center = pc != null ? pc.GetAimPoint(MaxRange)
                                    : origin + OwnerASC.transform.forward * MaxRange;

        Vector3 toZone = center - origin;
        if (toZone.magnitude > MaxRange) center = origin + toZone.normalized * MaxRange;
        return center;
    }

    // Espera StartDelay (ajustado por velocidad de ataque), libera al jugador,
    // y deja el área corriendo en segundo plano hasta que termina.
    private IEnumerator AoESequence(Vector3 center)
    {
        float speedMultiplier = 1f;
        float atkSpeedStat = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (atkSpeedStat > 0) speedMultiplier = 1f / atkSpeedStat;

        if (StartDelay > 0)
            yield return new WaitForSeconds(StartDelay / speedMultiplier);

        // Liberamos al jugador (EndAbility → isAttacking=false) APENAS arranca
        // el área, no al final. Este AoE es una zona persistente que dura
        // TotalDuration y puede seguir al dueño — no un canalizado. Antes se
        // llamaba EndAbility recién al terminar el área, así que el jugador
        // quedaba sin poder actuar toda la duración (ej. los 10s del Whirlwind),
        // o trabado para siempre si la corutina se interrumpía antes.
        EndAbility();

        // try/finally: si la corutina se corta a mitad (muerte, respawn, cambio de
        // clase), Unity dispone el iterador y el finally corre igual. Sin eso, el tag
        // de canalizado quedaria pegado y el jugador no podria usar NINGUNA habilidad
        // por el resto de la partida.
        try
        {
            yield return OwnerASC.StartCoroutine(AreaRoutine(center));
        }
        finally
        {
            // El canalizado se corta cuando termina el AREA, no cuando se libera al
            // jugador (EndAbility, mas arriba): el area sigue viva TotalDuration y el
            // giro tiene que acompanarla hasta el final.
            if (ChannelLoopAnimation != null)
            {
                var netAscStop = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
                if (netAscStop != null) netAscStop.ServerPlayChannelAnimation(this, false);
            }

            if (BlockOtherAbilities) OwnerASC.RemoveTag(EGameplayTag.Status_Channeling);
        }
    }

    // Reproduce el VFX del área, y cada TickInterval revisa quién está
    // dentro del radio (según Objetivos) para aplicarle EffectsToApply,
    // durante TotalDuration segundos.
    private IEnumerator AreaRoutine(Vector3 spawnPoint)
    {
        float timeElapsed = 0f;

        // Instantiate() acá solo se vería en el proceso servidor —
        // ServerPlayAbilityVFX lo reproduce en todos los peers (cada uno
        // con su propia copia, que se autodestruye sola tras TotalDuration
        // en vez de que la sigamos con una referencia acá).
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, spawnPoint);
        else PlayImpactVFX(spawnPoint);

        while (timeElapsed < TotalDuration)
        {
            Vector3 center = ShouldFollowOwner ? OwnerASC.transform.position : spawnPoint;
            Collider[] hits = Physics.OverlapSphere(center, Radius, TargetLayer);

            foreach (var hit in hits)
            {
                AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
                if (targetASC == null) continue;

                bool isValidTarget = false;
                if      (Targets == EAoETarget.Enemies && IsEnemy(targetASC)) isValidTarget = true;
                else if (Targets == EAoETarget.Allies  && IsAlly(targetASC))  isValidTarget = true;
                else if (Targets == EAoETarget.All)                           isValidTarget = true;

                if (isValidTarget)
                {
                    if (EffectsToApply != null)
                        foreach (var effect in EffectsToApply)
                            if (effect != null) targetASC.ApplyGameplayEffect(effect, OwnerASC);

                    OnTargetHit(targetASC);

                    if (OwnerASC.CompareTag("Player")) ChargeUltimate();
                }
            }

            yield return new WaitForSeconds(TickInterval);
            timeElapsed += TickInterval;
        }
    }

    // Gancho para que una habilidad concreta reaccione a cada objetivo alcanzado, en
    // cada tick (ej: los Cañones del Pirata, que además le apuestan a quien golpean).
    protected virtual void OnTargetHit(AbilitySystemComponent target) { }

    // Vista previa del área en el Editor. Con AtReticle además dibuja el alcance
    // máximo (dónde se puede llegar a poner la zona) y la zona en ese tope.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        if (DeployMode == EAoEDeploy.AtReticle)
        {
            // Alcance al que se puede desplegar.
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(origin.position, MaxRange);

            // La zona, dibujada en el tope del alcance hacia adelante.
            Vector3 preview = origin.position + origin.forward * MaxRange;
            Gizmos.color = new Color(0.2f, 0.9f, 0.5f, 0.35f);
            Gizmos.DrawSphere(preview, Radius);
            return;
        }

        Gizmos.color = new Color(0.2f, 0.9f, 0.5f, 0.35f);
        Gizmos.DrawSphere(origin.position, Radius);
    }

    // Instancia VisualPrefab en el punto de origen (parentado al dueño si
    // FollowOwner) y lo destruye solo tras TotalDuration segundos.
    public override void PlayImpactVFX(Vector3 position)
    {
        if (VisualPrefab == null || OwnerASC == null) return;

        GameObject vfxInstance = Instantiate(VisualPrefab, position, Quaternion.identity);
        if (ShouldFollowOwner) vfxInstance.transform.SetParent(OwnerASC.transform);

        // Si el prefab tiene VFX_AreaVisual, el círculo calza EXACTO con Radius y se
        // desvanece al terminar; si no, cae al multiplicador a ojo de siempre.
        VFX_AreaVisual.Configure(vfxInstance, Radius, VisualScaleMultiplier, TotalDuration);
    }
}
