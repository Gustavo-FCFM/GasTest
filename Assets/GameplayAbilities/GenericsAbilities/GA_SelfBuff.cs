using UnityEngine;

// ============================================================
// GA_SelfBuff
//
// Habilidad de un solo efecto: se aplica a sí mismo un
// GameplayEffect con duración (buff) y reproduce una partícula
// pegada al dueño mientras dura. Pensada para habilidades tipo
// "Grito de guerra" o "Postura defensiva".
// ============================================================
[CreateAssetMenu(fileName = "GA_SelfBuff", menuName = "GAS/Generics/Self Buff")]
public class GA_SelfBuff : GameplayAbility
{
    [Header("Buff Settings")]
    // Efecto que se aplica al propio dueño al activar.
    public GameplayEffect BuffEffect;

    [Header("Visuales")]
    // Partícula que acompaña al buff mientras está activo.
    public GameObject ParticlePrefab;
    // Desplazamiento local de la partícula respecto al dueño.
    public Vector3    ParticleOffset;

    // Valida, aplica el buff al dueño, reproduce la partícula en todos
    // los peers y cobra costo/cooldown.
    public override void Activate()
    {
        if (!IsServer) return;   // ← NUEVO
        if (!CanActivate()) return;

        CommitAbility();

        if (BuffEffect != null)
            OwnerASC.ApplyGameplayEffect(BuffEffect, OwnerASC);
        else
            Debug.LogWarning("GA_SelfBuff activado sin un BuffEffect asignado.");

        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);

            // Instantiate() acá solo se vería en el proceso servidor —
            // ServerPlayAbilityVFX lo reproduce en todos los peers (cada uno
            // con su propia copia de OwnerASC.transform para el parenting).
            NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
            if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, OwnerASC.transform.position);
            else PlayImpactVFX(OwnerASC.transform.position);
        }

        EndAbility();
    }

    // Instancia ParticlePrefab como hijo del dueño y la destruye cuando
    // termina el buff (o a los 2s si BuffEffect no tiene duración).
    public override void PlayImpactVFX(Vector3 position)
    {
        if (ParticlePrefab == null || OwnerASC == null) return;

        GameObject vfxInstance = Instantiate(ParticlePrefab,
            OwnerASC.transform.position + ParticleOffset,
            Quaternion.identity,
            OwnerASC.transform);

        float destroyTime = (BuffEffect != null && BuffEffect.Duration > 0) ? BuffEffect.Duration : 2f;
        Destroy(vfxInstance, destroyTime);
    }
}
