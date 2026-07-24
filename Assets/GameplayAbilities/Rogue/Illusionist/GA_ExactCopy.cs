using UnityEngine;
using System.Collections;

// ============================================================
// GA_ExactCopy  (Copia exacta — Ilusionista)
//
// Invoca una copia visual del jugador que camina desde su posición hasta el punto
// al que apunta al presionar la tecla, con la animación y velocidad del jugador.
// La copia es un señuelo: al ser golpeada, ciega + hiere al enemigo que la golpeó
// y explota (ver Entity_PlayerCopy). El límite (máx. 4, FIFO) y la limpieza por
// muerte los maneja PlayerCopyManager, que vive en el prefab del Ilusionista.
//
// CARGAS Y RED: 2 cargas, con el MISMO patrón que GA_Dash — el estado de cargas
// vive solo en el servidor y el bloqueo se hace con el TAG de cooldown (que sí se
// sincroniza al dueño), aplicándolo solo al agotar la ÚLTIMA carga. Un solo valor
// (ResolveCooldownDuration) define el cooldown y la recarga por carga.
// ============================================================
[CreateAssetMenu(fileName = "GA_ExactCopy", menuName = "GAS/Specific Abilities/Illusionist/Exact Copy")]
public class GA_ExactCopy : GameplayAbility, IChargedAbility
{
    public int MaxChargeCount => MaxCharges;

    [Header("Cargas")]
    [Tooltip("Cantidad de cargas. Cada carga tarda en volver lo que dure el cooldown de la habilidad.")]
    public int MaxCharges = 2;

    [Header("Copia")]
    [Tooltip("Velocidad de caminado de la copia. 0 = usar la velocidad del jugador (MovSpeed).")]
    public float MoveSpeedOverride = 0f;
    [Tooltip("Alcance máximo hacia donde puede caminar la copia (recorta el punto de mira si está muy lejos). 0 = sin recorte.")]
    public float MaxRange = 0f;

    // Cargas disponibles en el SERVIDOR. -1 = sin inicializar (se toma como lleno).
    [System.NonSerialized] private int  _charges = -1;
    [System.NonSerialized] private bool _recharging;

    public override void Activate()
    {
        if (!IsServer) return;
        if (_charges < 0) _charges = MaxCharges;

        // Gate estándar (incluye el tag de cooldown que ve el dueño).
        if (!CanActivate()) return;
        if (_charges <= 0) { EndAbility(); return; }

        PlayerCopyManager manager = OwnerASC.GetComponentInChildren<PlayerCopyManager>();
        if (manager == null)
        {
            // La clase no trae el comportamiento de copias (no debería pasar en el
            // Ilusionista). Liberamos al dueño para no dejarlo trabado.
            Debug.LogWarning("[GA_ExactCopy] No hay PlayerCopyManager en el dueño — ¿falta en el PassiveBehaviorsPrefab?");
            EndAbility();
            return;
        }

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        PlayerController pc = OwnerASC.GetComponent<PlayerController>();

        _charges--;
        ReportCharges();

        // Costo siempre; cooldown solo al agotar la última carga (como GA_Dash).
        if (CostEffect != null) OwnerASC.ApplyGameplayEffect(CostEffect, this);
        if (_charges <= 0 && CooldownEffect != null)
            OwnerASC.ApplyGameplayEffect(CooldownEffect, this, ResolveCooldownDuration());

        if (VisualsSequence != null && VisualsSequence.Count > 0)
        {
            if (netAsc != null) netAsc.ServerPlayAbilityVisualsSequence(this);
            else OwnerASC.StartAbilityCoroutine(PlayVisualsSequence());
        }

        // Origen = jugador; objetivo = a donde apunta al presionar.
        Vector3 spawnPos = OwnerASC.transform.position;
        Vector3 target   = pc != null ? pc.GetAimPoint() : spawnPos + OwnerASC.transform.forward * 10f;

        // Recorte de alcance opcional (sobre el plano horizontal).
        if (MaxRange > 0f)
        {
            Vector3 flat = target - spawnPos; flat.y = 0f;
            if (flat.magnitude > MaxRange)
                target = spawnPos + flat.normalized * MaxRange + Vector3.up * (target.y - spawnPos.y);
        }

        float speed = MoveSpeedOverride > 0f
            ? MoveSpeedOverride
            : OwnerASC.GetAttributeValue(EAttributeType.MovSpeed);

        // La Copia exacta copia al PROPIO Ilusionista → su índice de clase.
        int sourceClassIndex = pc != null ? pc.VisualClassIndex : -1;
        manager.SpawnCopy(spawnPos, target, speed, sourceClassIndex);

        if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);

        StartRecharge();
        EndAbility();
    }

    // --- Cargas (mismo patrón que GA_Dash, sin reembolso por muerte) ---

    private void ReportCharges()
    {
        if (OwnerASC == null) return;
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) netAsc.ServerReportCharges(this, _charges);
    }

    private void StartRecharge()
    {
        if (_recharging || OwnerASC == null) return;
        OwnerASC.StartAbilityCoroutine(RechargeRoutine());
    }

    // Devuelve 1 carga cada "cooldown" hasta llenar MaxCharges. Al recuperar una
    // carga limpia el tag de cooldown para que quede disponible al instante.
    private IEnumerator RechargeRoutine()
    {
        _recharging = true;
        while (_charges < MaxCharges)
        {
            float cd = ResolveCooldownDuration();
            if (cd <= 0f) cd = 1f;

            yield return new WaitForSeconds(cd);

            if (_charges < MaxCharges)
            {
                _charges++;
                ReportCharges();
                ClearCooldownTag();
            }
        }
        _recharging = false;
    }

    private void ClearCooldownTag()
    {
        if (OwnerASC != null && CooldownEffect != null && CooldownEffect.GrantedTags.Count > 0)
            OwnerASC.ReduceCooldownByTag(CooldownEffect.GrantedTags[0], 99999f);
    }
}
