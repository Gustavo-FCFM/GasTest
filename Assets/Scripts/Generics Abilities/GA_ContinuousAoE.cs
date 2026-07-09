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
// ============================================================
[CreateAssetMenu(fileName = "GA_ContinuousAoE", menuName = "GAS/Generics/Continuous AoE")]
public class GA_ContinuousAoE : GameplayAbility
{
    // A quién afecta el área.
    public enum EAoETarget { Enemigos, Aliados, Todos }

    [Header("Objetivos Afectados")]
    public EAoETarget Objetivos = EAoETarget.Enemigos;

    [Header("Configuración de Área")]
    public float Radius        = 4f;
    public float TotalDuration = 5f;
    // Cada cuánto se vuelve a revisar quién está dentro del área y se le
    // reaplican los efectos.
    public float TickInterval  = 0.5f;

    [Header("Comportamiento")]
    // Si el área se mueve con el dueño o queda fija donde se activó.
    public bool FollowOwner = true;

    [Header("Efectos Visuales")]
    public GameObject VisualPrefab;
    // Multiplica Radius para el tamaño del VFX (no afecta el área real).
    public float VisualScaleMultiplier = 2.0f;

    [Header("Lista de Efectos")]
    // Todos los GameplayEffect que se le aplican a cada objetivo válido
    // en cada tick.
    public List<GameplayEffect> EffectsToApply;

    [Header("Sincronización")]
    // Espera antes de que el área empiece a existir, tras activar la habilidad.
    public float StartDelay = 0.5f;

    // Valida, cobra costo/cooldown y arranca la secuencia del área.
    public override void Activate()
    {
        if (!IsServer) return;   // ← NUEVO
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);
            OwnerASC.StartAbilityCoroutine(AoESequence());
        }
    }

    // Espera StartDelay (ajustado por velocidad de ataque), libera al jugador,
    // y deja el área corriendo en segundo plano hasta que termina.
    private IEnumerator AoESequence()
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

        yield return OwnerASC.StartCoroutine(AreaRoutine());
    }

    // Reproduce el VFX del área, y cada TickInterval revisa quién está
    // dentro del radio (según Objetivos) para aplicarle EffectsToApply,
    // durante TotalDuration segundos.
    private IEnumerator AreaRoutine()
    {
        float     timeElapsed = 0f;
        Vector3   spawnPoint  = OwnerASC.transform.position;

        // Instantiate() acá solo se vería en el proceso servidor —
        // ServerPlayAbilityVFX lo reproduce en todos los peers (cada uno
        // con su propia copia, que se autodestruye sola tras TotalDuration
        // en vez de que la sigamos con una referencia acá).
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, spawnPoint);
        else PlayImpactVFX(spawnPoint);

        while (timeElapsed < TotalDuration)
        {
            Vector3 center = FollowOwner ? OwnerASC.transform.position : spawnPoint;
            Collider[] hits = Physics.OverlapSphere(center, Radius, TargetLayer);

            foreach (var hit in hits)
            {
                AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
                if (targetASC == null) continue;

                bool isValidTarget = false;
                if      (Objetivos == EAoETarget.Enemigos && IsEnemy(targetASC)) isValidTarget = true;
                else if (Objetivos == EAoETarget.Aliados  && IsAlly(targetASC))  isValidTarget = true;
                else if (Objetivos == EAoETarget.Todos)                          isValidTarget = true;

                if (isValidTarget)
                {
                    if (EffectsToApply != null)
                        foreach (var effect in EffectsToApply)
                            if (effect != null) targetASC.ApplyGameplayEffect(effect, OwnerASC);

                    if (OwnerASC.CompareTag("Player")) ChargeUltimate();
                }
            }

            yield return new WaitForSeconds(TickInterval);
            timeElapsed += TickInterval;
        }
    }

    // Instancia VisualPrefab en el punto de origen (parentado al dueño si
    // FollowOwner) y lo destruye solo tras TotalDuration segundos.
    public override void PlayImpactVFX(Vector3 position)
    {
        if (VisualPrefab == null || OwnerASC == null) return;

        GameObject vfxInstance = Instantiate(VisualPrefab, position, Quaternion.identity);
        float finalScale = Radius * VisualScaleMultiplier;
        vfxInstance.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
        if (FollowOwner) vfxInstance.transform.SetParent(OwnerASC.transform);

        Destroy(vfxInstance, TotalDuration);
    }
}
