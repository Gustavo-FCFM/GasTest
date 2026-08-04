using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_InstantAoE
//
// Golpe de área de UNA sola aplicación: revisa quién está dentro del radio y le
// aplica la lista de GameplayEffect una vez. Es la versión instantánea de
// GA_ContinuousAoE (misma configuración: área, efectos, dónde se despliega), sin
// duración ni ticks. Pensada para explosiones, ondas expansivas, bombas, etc.
//
// DÓNDE SE DESPLIEGA (DeployMode): sobre el propio dueño, o en la ZONA APUNTADA con
// la retícula (mantener el botón → marcador en el suelo → soltar, ver
// IGroundTargetAbility).
//
// StartDelay sirve para sincronizar el impacto con la animación (el golpe cae recién
// después de ese tiempo). El punto de la zona se resuelve al ACTIVAR, así que apuntar
// a otro lado durante el delay no cambia dónde cae.
//
// Si en cambio querés una zona que PERSISTA aplicando efectos cada tanto (charco de
// veneno, aura, la zona de los Cañones del Pirata), usá GA_ContinuousAoE.
// ============================================================
[CreateAssetMenu(fileName = "GA_InstantAoE", menuName = "GAS/Generics/Instant AoE")]
public class GA_InstantAoE : GameplayAbility, IGroundTargetAbility
{
    [Header("Targets")]
    [Tooltip("A quién afecta el área.")]
    public GA_ContinuousAoE.EAoETarget Targets = GA_ContinuousAoE.EAoETarget.Enemies;

    [Header("Configuración de Área")]
    public float Radius = 4f;

    [Header("Despliegue")]
    [Tooltip("AtOwner: el área estalla sobre el dueño. AtReticle: se apunta con el marcador " +
             "en el suelo (mantener → apuntar → soltar) y cae ahí.")]
    public GA_ContinuousAoE.EAoEDeploy DeployMode = GA_ContinuousAoE.EAoEDeploy.AtOwner;

    [Tooltip("Solo con AtReticle: alcance máximo al que se puede lanzar la zona.")]
    public float MaxRange = 15f;

    // IGroundTargetAbility: valores del marcador del suelo (solo se muestra si
    // DeployMode es AtReticle).
    public float MaxTargetRange   => MaxRange;
    public float TargetRadius     => Radius;
    public bool  UsesGroundTarget => DeployMode == GA_ContinuousAoE.EAoEDeploy.AtReticle;

    [Header("Lista de Efectos")]
    [Tooltip("Todos los GameplayEffect que se aplican UNA vez a cada objetivo válido.")]
    public List<GameplayEffect> EffectsToApply;

    [Header("Efectos Visuales")]
    public GameObject VisualPrefab;
    [Tooltip("Multiplica Radius para el tamaño del VFX (no afecta el área real).")]
    public float VisualScaleMultiplier = 2.0f;
    [Tooltip("Segundos hasta que se destruye el VFX del impacto.")]
    public float VisualLifetime = 2f;

    [Header("Sincronización")]
    [Tooltip("Espera antes de que el golpe caiga (para acompañar la animación). Se ajusta " +
             "por la velocidad de ataque, igual que en las demás habilidades.")]
    public float StartDelay = 0f;

    // Valida, cobra costo/cooldown y programa el impacto.
    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC == null) return;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();

        // El centro se resuelve ACÁ, al activar (ver nota de cabecera sobre StartDelay).
        Vector3 center = ResolveCenter(pc);

        if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);

        if (StartDelay > 0f) OwnerASC.StartAbilityCoroutine(ImpactRoutine(center));
        else                 { Detonate(center); EndAbility(); }
    }

    // Punto donde cae el área: el dueño, o la zona apuntada (recortada a MaxRange
    // para que coincida con la vista previa del marcador).
    private Vector3 ResolveCenter(PlayerController pc)
    {
        Vector3 origin = OwnerASC.transform.position;
        if (DeployMode == GA_ContinuousAoE.EAoEDeploy.AtOwner) return origin;

        Vector3 center = pc != null ? pc.GetAimPoint(MaxRange)
                                    : origin + OwnerASC.transform.forward * MaxRange;

        Vector3 toZone = center - origin;
        if (toZone.magnitude > MaxRange) center = origin + toZone.normalized * MaxRange;
        return center;
    }

    // Espera StartDelay (ajustado por velocidad de ataque) y detona.
    private IEnumerator ImpactRoutine(Vector3 center)
    {
        float speedMultiplier = 1f;
        float atkSpeedStat = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (atkSpeedStat > 0) speedMultiplier = 1f / atkSpeedStat;

        yield return new WaitForSeconds(StartDelay / speedMultiplier);

        Detonate(center);
        EndAbility();
    }

    // Aplica los efectos UNA vez a cada objetivo válido dentro del radio y reproduce
    // el VFX de impacto en todos los peers.
    private void Detonate(Vector3 center)
    {
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, center);
        else PlayImpactVFX(center);

        Collider[] hits = Physics.OverlapSphere(center, Radius, TargetLayer);
        var seen = new HashSet<AbilitySystemComponent>();

        foreach (var hit in hits)
        {
            AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
            // Un mismo personaje puede tener varios colliders: sin este filtro le
            // aplicaríamos los efectos más de una vez (y esto es de UNA sola aplicación).
            if (targetASC == null || !seen.Add(targetASC)) continue;

            bool isValidTarget =
                (Targets == GA_ContinuousAoE.EAoETarget.Enemies && IsEnemy(targetASC)) ||
                (Targets == GA_ContinuousAoE.EAoETarget.Allies  && IsAlly(targetASC))  ||
                 Targets == GA_ContinuousAoE.EAoETarget.All;

            if (!isValidTarget) continue;

            if (EffectsToApply != null)
                foreach (var effect in EffectsToApply)
                    if (effect != null) targetASC.ApplyGameplayEffect(effect, OwnerASC);

            OnTargetHit(targetASC);

            if (OwnerASC.CompareTag("Player")) ChargeUltimate();
        }
    }

    // Gancho para que una habilidad concreta reaccione a cada objetivo alcanzado
    // (ej: los Cañones del Pirata, que además le apuestan a quien golpean).
    protected virtual void OnTargetHit(AbilitySystemComponent target) { }

    // Instancia el VFX escalado al radio. La llama cada peer con su propia copia
    // (ver NetworkAbilitySystemComponent.ServerPlayAbilityVFX).
    public override void PlayImpactVFX(Vector3 position)
    {
        if (VisualPrefab == null) return;

        GameObject vfx = Instantiate(VisualPrefab, position, Quaternion.identity);
        float finalScale = Radius * VisualScaleMultiplier;
        vfx.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
        Destroy(vfx, VisualLifetime);
    }

    // Vista previa del área en el Editor.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Vector3 center = DeployMode == GA_ContinuousAoE.EAoEDeploy.AtReticle
            ? origin.position + origin.forward * MaxRange
            : origin.position;

        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.35f);
        Gizmos.DrawSphere(center, Radius);
    }
}
