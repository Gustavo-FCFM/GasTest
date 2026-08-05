using UnityEngine;

// ============================================================
// GA_ImmortalWrath
//
// Ultimate de resurrección: solo se puede activar estando MUERTO
// (sobreescribe CanActivate). Revive con 1 de vida, se aplica un
// buff de inmortalidad, y hace una explosión de daño en área
// alrededor del punto de reaparición. La dispara PlayerController
// automáticamente al morir, si está disponible.
// ============================================================
[CreateAssetMenu(fileName = "GA_ImmortalWrath", menuName = "GAS/Specific Abilities/Immortal/Immortal Wrath")]
public class GA_ImmortalWrath : GameplayAbility
{
    [Header("Configuración Immortal Wrath")]
    // FormerlySerializedAs: este campo se llamaba "InmortalBuffEffect". Unity
    // serializa los campos por NOMBRE, así que sin esto el asset ya configurado
    // perdería la referencia en silencio al renombrarlo.
    [UnityEngine.Serialization.FormerlySerializedAs("InmortalBuffEffect")]
    public GameplayEffect ImmortalBuffEffect;
    public GameplayEffect ExplosionDamageEffect;
    // Radio de la explosión de daño alrededor del punto de reaparición.
    public float AbilityRadius = 3f;

    // Solo se puede activar si el personaje está muerto (y no en
    // cooldown) — al revés de cualquier otra habilidad.
    public override bool CanActivate()
    {
        if (OwnerASC == null) return false;

        if (!OwnerASC.HasTag(EGameplayTag.State_Dead)) return false;

        if (CooldownEffect != null && CooldownEffect.GrantedTags.Count > 0)
            if (OwnerASC.HasTag(CooldownEffect.GrantedTags[0])) return false;

        return true;
    }

    // Revive con 1 de vida, aplica el buff de inmortalidad, y hace
    // explotar de daño a los enemigos dentro de AbilityRadius.
    public override void Activate()
    {
        // NOTA: Esta habilidad se activa al morir, que ocurre en el servidor.
        // No necesita el guard if (!IsServer) porque HandlePlayerDeath ya
        // corre desde el servidor en PlayerController.
        // Aun así lo dejamos por consistencia.
        if (!IsServer) return;

        CommitAbility();

        if (OwnerASC != null)
        {
            OwnerASC.Revive();
            OwnerASC.SetCurrentAttributeValue(EAttributeType.Health, 1f);

            if (ImmortalBuffEffect != null)
                OwnerASC.ApplyGameplayEffect(ImmortalBuffEffect, OwnerASC);

            Collider[] hits = Physics.OverlapSphere(OwnerASC.transform.position, AbilityRadius, TargetLayer);
            foreach (Collider hit in hits)
            {
                AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
                if (targetASC != null && IsEnemy(targetASC))
                    if (ExplosionDamageEffect != null)
                        targetASC.ApplyGameplayEffect(ExplosionDamageEffect, OwnerASC);
            }

            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) pc.PlayAnimation(this);
        }

        EndAbility();
    }
}
