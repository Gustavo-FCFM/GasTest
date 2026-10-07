using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GA_LeapAttack
//
// Habilidad de salto con impacto en área al aterrizar (slam). El
// movimiento del salto en sí lo ejecuta el CLIENTE DUEÑO (es el
// único que mueve su propio CharacterController); esta clase solo
// arranca ese movimiento a través de NetworkAbilitySystemComponent
// y resuelve el daño/VFX del aterrizaje, siempre con autoridad de
// servidor. Ver comentarios en NetworkAbilitySystemComponent
// (sección SALTO) para el flujo completo.
// ============================================================
[CreateAssetMenu(fileName = "GA_LeapAttack", menuName = "GAS/Generics/Leap Attack")]
public class GA_LeapAttack : GameplayAbility, ILeapAbility
{
    [Section(AbilitySection.Movement)]
    [Tooltip("Impulso vertical inicial del salto.")]
    public float JumpVelocity = 15f;
    [Tooltip("Impulso hacia adelante (hacia donde mira la cámara del dueño).")]
    public float ForwardForce = 5f;

    [Section(AbilitySection.Shape)]
    [Tooltip("Radio del golpe al aterrizar, en metros. A los alcanzados les aplica la lista de " +
             "efectos; el VFX del aterrizaje es una entrada 'En el impacto' (con 'Calzar con el " +
             "área' mide lo mismo que este radio).")]
    public float AbilityRadius = 3f;

    [Section(AbilitySection.Animation)]
    [Tooltip("OPCIONAL: el DESPEGUE. Se reproduce una vez y encadena al bucle.\n\n" +
             "Va separado del bucle a propósito: si el despegue vive dentro del clip que se " +
             "loopea, el salto entero se repite una y otra vez mientras el personaje está en el " +
             "aire.\n\nVacío = se entra directo al bucle.")]
    public AnimationClip AirStartAnimation;

    [Tooltip("Clip que se reproduce EN BUCLE mientras el personaje está en el aire.\n\n" +
             "Existe porque el vuelo no dura lo que dura un clip: con la ranura de acción " +
             "normal la animación terminaba a mitad del salto y el personaje caía en pose de " +
             "locomoción. Acá el bucle se sostiene todo el vuelo y lo corta el aterrizaje.\n\n" +
             "VACÍO = comportamiento de siempre (el AnimationClip de arriba, una sola vez).")]
    public AnimationClip AirLoopAnimation;

    [Tooltip("OPCIONAL: remate que se reproduce UNA vez al tocar el suelo (el hachazo).\n\n" +
             "Vacío = del bucle se vuelve directo a la locomoción, sin remate.")]
    public AnimationClip AirLandAnimation;

    // ILeapAbility: PlayerController lee los clips por acá (ver ApplyLeapAnimation).
    public AnimationClip AirStartClip => AirStartAnimation;
    public AnimationClip AirLoopClip  => AirLoopAnimation;
    public AnimationClip AirLandClip => AirLandAnimation;

    // Valida, cobra costo/cooldown, reproduce la animación y le pide a
    // NetworkAbilitySystemComponent que ejecute el salto en el cliente
    // dueño (ver ServerStartLeap).
    public override void Activate()
    {
        if (!IsServer) return;   // ← NUEVO
        if (!CanActivate()) return;

        CommitAbility();

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        if (pc != null)
            pc.PlayAnimation(this);

        // El salto en sí (mover el CharacterController) tiene que resolverse
        // en el proceso DUEÑO del jugador, no acá — Activate() corre en el
        // servidor, que para el host es el mismo proceso que el dueño, pero
        // para cualquier otro jugador es un proceso distinto. ServerStartLeap
        // manda un TargetRpc a la conexión dueña para que ejecute el salto
        // en su propia copia.
        if (netAsc != null)
            netAsc.ServerStartLeap(this, JumpVelocity, ForwardForce);
        else
            Debug.LogWarning("[GA_LeapAttack] No hay NetworkAbilitySystemComponent — el salto no se va a ejecutar en ningún lado.");

        EndAbility();
    }

    // Resuelve el impacto del aterrizaje: reproduce el VFX en todos los
    // peers y aplica daño/CC a los enemigos dentro de AbilityRadius. Lo
    // llama NetworkAbilitySystemComponent.ServerResolveLeapImpact() —
    // siempre en el servidor — cuando el dueño (el único que simula su
    // propio CharacterController) le avisa que aterrizó.
    public void ExecuteImpactCheck()
    {
        Vector3 impactCenter = OwnerASC.transform.position;

        // El VFX de impacto es puramente cosmético, pero Instantiate() acá
        // (que corre en el servidor) solo lo crea en ESTE proceso — igual
        // que pasaba con el arma del proyectil, un cliente remoto nunca lo
        // ve. BroadcastImpactVFX lo reproduce localmente en el servidor Y
        // le avisa a los demás peers que hagan lo mismo con su propia copia.
        BroadcastImpactVFX(impactCenter);

        Collider[] hitColliders = Physics.OverlapSphere(impactCenter, AbilityRadius, TargetLayer);
        HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();

        foreach (var hitCollider in hitColliders)
        {
            AbilitySystemComponent targetASC = hitCollider.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC == null || ReferenceEquals(targetASC, OwnerASC)) continue; // ignora el propio collider del que saltó

            if (IsEnemy(targetASC) && !enemiesHit.Contains(targetASC))
            {
                ApplyHitEffects(targetASC, firstHit: enemiesHit.Count == 0);
                ChargeUltimate();
                BroadcastHitVFX(targetASC, withSound: false);
                enemiesHit.Add(targetASC);
            }
        }
    }

    // El área del aterrizaje: con "Calzar con el área", el VFX mide lo mismo que el golpe.
    public override float VisualAreaRadius => AbilityRadius;
    public override bool SupportsVisualTiming(EVisualWhen when) => true;

    // El impacto real se resuelve alrededor de OwnerASC.transform.position al
    // ATERRIZAR (ver ExecuteImpactCheck arriba) — no sabemos de antemano dónde
    // va a caer, así que dibujamos la esfera centrada en la posición ACTUAL
    // del jugador en el Editor, con el mismo AbilityRadius que usa de verdad.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.25f);
        Gizmos.DrawSphere(origin.position, AbilityRadius);
        Gizmos.color = new Color(1f, 0.25f, 0.1f, 1f);
        Gizmos.DrawWireSphere(origin.position, AbilityRadius);
    }

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
    {
        if (origin == null) return;
        AbilityHandles.Radius(this, "Ability Radius", origin.position, ref AbilityRadius,
                              AbilityHandles.RadiusColor, origin.right);
    }
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GameplayEffect DamageEffect;
    [SerializeField, HideInInspector] private GameplayEffect CrowdControlEffect;
    [SerializeField, HideInInspector] private List<GameplayEffect> AdditionalEffects;
    [SerializeField, HideInInspector] private GameObject ImpactVFX;
    [SerializeField, HideInInspector] private float ImpactVFXScaleMultiplier = 2f;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        UpgradeEffect(ref DamageEffect, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeEffect(ref CrowdControlEffect, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeEffects(AdditionalEffects, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeVisual(ref ImpactVFX, new AbilityVisual
        {
            When = EVisualWhen.OnImpact, MatchAreaSize = true,
            AreaSizeMultiplier = ImpactVFXScaleMultiplier, DestroyTime = 2f,
        }, ref changed);
    }
}
