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
public class GA_ExactCopy : GameplayAbility
{
    [Header("Copia")]
    [Tooltip("Velocidad de caminado de la copia. 0 = usar la velocidad del jugador (MovSpeed).")]
    public float MoveSpeedOverride = 0f;
    [Tooltip("Alcance máximo hacia donde puede caminar la copia (recorta el punto de mira si está muy lejos). 0 = sin recorte.")]
    public float MaxRange = 0f;

    public override void Activate()
    {
        if (!IsServer) return;

        // Gate estándar: incluye el tag de cooldown que ve el dueño, y las cargas.
        if (!CanActivate()) return;

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

        // Gasta la carga, cobra el costo, aplica el cooldown solo si era la última y
        // reproduce la VisualsSequence. Todo eso vive en la clase base.
        CommitAbility();

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

        if (pc != null) pc.PlayAnimation(this);

        EndAbility();
    }
}
