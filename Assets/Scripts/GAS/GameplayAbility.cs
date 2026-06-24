using UnityEngine;
using System.Collections.Generic;

public abstract class GameplayAbility : ScriptableObject
{
    [Header("Configuración General")]
    public string AbilityName = "New Ability";
    public Sprite AbilityIcon;

    [Header("Costes")]
    public GameplayEffect CostEffect;

    [Header("Cooldown")]
    public GameplayEffect CooldownEffect;

    [Header("Cooldown Dinámico")]
    public bool UseAttackSpeedAsCooldown = false;

    [Header("Ultimate Charge")]
    public float UltimateChargeAmount = 0f;

    [Header("Bloqueos")]
    public List<EGameplayTag> ActivationBlockedTags;

    [Header("Animación")]
    public string AnimationTriggerName = "AttackTrigger";

    [Tooltip("1=Melee, 2=Proyectil, 3=Salto, 4=Extra")]
    public int AnimationID = 1;

    [Header("Configuración de Área (Opcional)")]
    public float AbilityRadius = 3f;
    public LayerMask TargetLayer;

    public enum EHitboxShape { Sphere, Box, Cone }

    [Header("Forma de la Hitbox (Gizmos)")]
    public EHitboxShape HitboxShape  = EHitboxShape.Sphere;
    public float        HitboxOffsetZ      = 1.5f;
    public Vector3      HitboxHalfExtents  = new Vector3(1f, 1f, 1f);

    [Range(0f, 360f)]
    public float ConeAngle = 90f;

    [System.Serializable]
    public struct AbilityVisual
    {
        public GameObject   VFXPrefab;
        public float        Delay;
        public Vector3      Offset;
        public Vector3      RotationOffset;
        public Vector3      Scale;
        public bool         AttachToOwner;
        public float        DestroyTime;
        public EGameplayTag EndWithTag;
    }

    [Header("Visuales de Habilidad (Automáticos)")]
    public List<AbilityVisual> VisualsSequence;

    protected AbilitySystemComponent OwnerASC;

    // =========================================================
    // IsServer — ARQUITECTURA CORRECTA
    //
    // AbilitySystemComponent hereda de MonoBehaviour.
    // NetworkAbilitySystemComponent hereda de NetworkBehaviour
    // y vive en el MISMO GameObject como componente separado.
    // Para saber si estamos en el servidor, buscamos ese componente
    // en el mismo GameObject y preguntamos su IsServerInitialized.
    // =========================================================
    protected bool IsServer
    {
        get
        {
            if (OwnerASC == null) return true;
            // Buscar el componente de red en el mismo GameObject
            NetworkAbilitySystemComponent netASC =
                OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
            // Si no existe (singleplayer / NPC sin red), siempre somos "servidor"
            if (netASC == null) return true;
            return netASC.IsServerInitialized;
        }
    }

    // =========================================================
    // INICIALIZACIÓN
    // =========================================================

    public void Initialize(AbilitySystemComponent asc)
    {
        OwnerASC = asc;
    }

    // =========================================================
    // CAN ACTIVATE
    // =========================================================

    public virtual bool CanActivate()
    {
        if (OwnerASC == null) return false;
        if (OwnerASC.HasTag(EGameplayTag.State_Dead)) return false;

        if (ActivationBlockedTags != null)
            foreach (EGameplayTag tag in ActivationBlockedTags)
                if (OwnerASC.HasTag(tag)) return false;

        if (CooldownEffect != null && CooldownEffect.GrantedTags.Count > 0)
            if (OwnerASC.HasTag(CooldownEffect.GrantedTags[0])) return false;

        if (CostEffect != null && !OwnerASC.CanAffordGameplayEffect(CostEffect)) return false;

        return true;
    }

    // =========================================================
    // ACTIVATE — en cada habilidad concreta agrega:
    //   if (!IsServer) return;
    // =========================================================

    public abstract void Activate();

    // =========================================================
    // COMMIT ABILITY
    // =========================================================

    protected void CommitAbility()
    {
        if (OwnerASC == null) return;

        if (CostEffect != null)
            OwnerASC.ApplyGameplayEffect(CostEffect, this);

        if (CooldownEffect != null)
        {
            float finalCooldown = -1f;
            if (UseAttackSpeedAsCooldown)
            {
                float spd = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
                if (spd > 0) finalCooldown = spd;
            }
            OwnerASC.ApplyGameplayEffect(CooldownEffect, this, finalCooldown);
        }

        if (VisualsSequence != null && VisualsSequence.Count > 0)
            OwnerASC.StartAbilityCoroutine(PlayVisualsSequence());
    }

    // =========================================================
    // VFX
    // =========================================================

    private System.Collections.IEnumerator PlayVisualsSequence()
    {
        float mult = 1f;
        float spd  = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (spd > 0) mult = 1f / spd;

        foreach (var v in VisualsSequence)
        {
            if (v.VFXPrefab == null) continue;

            if (v.Delay > 0)
                yield return new WaitForSeconds(v.Delay / mult);

            Vector3    pos = OwnerASC.transform.position + OwnerASC.transform.TransformDirection(v.Offset);
            Quaternion rot = OwnerASC.transform.rotation * Quaternion.Euler(v.RotationOffset);
            GameObject vfx = v.AttachToOwner
                ? Instantiate(v.VFXPrefab, pos, rot, OwnerASC.transform)
                : Instantiate(v.VFXPrefab, pos, rot);

            vfx.transform.localScale = (v.Scale != Vector3.zero) ? v.Scale : Vector3.one;

            if (v.EndWithTag != EGameplayTag.None)
                OwnerASC.StartAbilityCoroutine(DestroyVfxWhenTagRemoved(vfx, v.EndWithTag));
            else if (v.DestroyTime > 0)
                Destroy(vfx, v.DestroyTime);
        }
    }

    private System.Collections.IEnumerator DestroyVfxWhenTagRemoved(GameObject vfx, EGameplayTag tag)
    {
        yield return null;
        while (OwnerASC != null && OwnerASC.HasTag(tag) && vfx != null)
            yield return null;
        if (vfx != null) Destroy(vfx);
    }

    // =========================================================
    // END ABILITY
    // =========================================================

    public virtual void EndAbility()
    {
        PlayerController pc = OwnerASC?.GetComponent<PlayerController>();
        if (pc != null) pc.FinishAttack();
    }

    // =========================================================
    // ULTIMATE CHARGE
    // =========================================================

    protected void ChargeUltimate()
    {
        if (UltimateChargeAmount > 0 && OwnerASC != null)
            OwnerASC.ReduceCooldownByTag(EGameplayTag.Ability_Cooldown_Ultimate, UltimateChargeAmount);
    }

    // =========================================================
    // AFILIACIÓN
    // =========================================================

    protected bool IsEnemy(AbilitySystemComponent target)
        => OwnerASC != null && OwnerASC.IsEnemyOf(target);

    protected bool IsAlly(AbilitySystemComponent target, bool includeSelf = true)
        => OwnerASC != null && OwnerASC.IsAllyOf(target, includeSelf);
}