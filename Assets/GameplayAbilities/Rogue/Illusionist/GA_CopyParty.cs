using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GA_CopyParty  (Fiesta de copias — ult del Ilusionista)
//
// Por CADA jugador elegible (uno mismo + aliados vivos en rango) genera CopiesPerPlayer
// copias, dispersándose en direcciones parejas ALREDEDOR de ese jugador. Cada copia nace
// en su jugador y usa su arma/anim (la clase de ESE jugador; ver Entity_PlayerCopy, que
// resuelve la clase por índice). Con 2 jugadores y 4 c/u = 8 copias, 3 jugadores = 12.
//
// Estas copias van al pool "Fiesta" del PlayerCopyManager: SIN límite (no cuentan ni
// eliminan a las de Copia exacta), pero se limpian igual si el Ilusionista muere.
//
// Al ser golpeadas, se comportan igual que cualquier copia (ciegan + hieren al enemigo
// que las golpea y explotan). Los aliados no pueden golpearlas (mismo equipo).
// ============================================================
[CreateAssetMenu(fileName = "GA_CopyParty", menuName = "GAS/Specific Abilities/Illusionist/Copy Party")]
public class GA_CopyParty : GameplayAbility
{
    [Header("Fiesta de copias")]
    [Tooltip("Cuántas copias genera CADA jugador elegible (uno mismo + aliados en rango).")]
    public int CopiesPerPlayer = 4;
    [Tooltip("Radio para encontrar aliados (y a vos mismo) a copiar.")]
    public float Range = 15f;
    [Tooltip("Qué tan lejos camina cada copia al dispersarse desde su jugador.")]
    public float DisperseDistance = 8f;
    [Tooltip("Velocidad de caminado de las copias. 0 = usar la MovSpeed del jugador copiado.")]
    public float MoveSpeedOverride = 0f;
    [Tooltip("Capa de personajes para buscar a los jugadores (Character).")]
    public LayerMask CharacterLayer;

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        PlayerCopyManager manager = OwnerASC.GetComponentInChildren<PlayerCopyManager>();
        if (manager == null)
        {
            Debug.LogWarning("[GA_CopyParty] No hay PlayerCopyManager en el dueño — ¿falta en el PassiveBehaviorsPrefab?");
            EndAbility();
            return;
        }

        // Costo + cooldown + visuales del ult.
        CommitAbility();

        // Jugadores elegibles: uno mismo + aliados vivos en rango. El Ilusionista va
        // primero (garantizado, por si el OverlapSphere no pilla su propio collider).
        List<AbilitySystemComponent> sources = new List<AbilitySystemComponent>();
        var seen = new HashSet<AbilitySystemComponent>();

        if (OwnerASC.GetComponent<PlayerController>() != null && seen.Add(OwnerASC))
            sources.Add(OwnerASC);

        Collider[] cols = Physics.OverlapSphere(OwnerASC.transform.position, Range, CharacterLayer);
        foreach (var col in cols)
        {
            AbilitySystemComponent asc = col.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || !seen.Add(asc)) continue;
            if (!OwnerASC.IsAllyOf(asc)) continue;                  // aliados (uno mismo ya está)
            if (asc.HasTag(EGameplayTag.State_Dead)) continue;
            if (asc.GetComponent<PlayerController>() == null) continue;
            sources.Add(asc);
        }

        if (sources.Count == 0) { EndAbility(); return; } // no debería pasar (siempre estás vos)

        // Por cada jugador elegible, genera CopiesPerPlayer copias dispersándose parejo
        // en 360° ALREDEDOR de ese jugador (con su propia arma/anim y velocidad).
        int perPlayer = Mathf.Max(1, CopiesPerPlayer);
        foreach (AbilitySystemComponent src in sources)
        {
            PlayerController srcPc = src.GetComponent<PlayerController>();

            int   classIdx = srcPc.VisualClassIndex;
            float speed    = MoveSpeedOverride > 0f ? MoveSpeedOverride
                                                    : src.GetAttributeValue(EAttributeType.MovSpeed);
            Vector3 spawnPos = src.transform.position;

            for (int i = 0; i < perPlayer; i++)
            {
                Vector3 dir    = DisperseDirection(i, perPlayer);
                Vector3 target = spawnPos + dir * DisperseDistance;
                manager.SpawnPartyCopy(spawnPos, target, speed, classIdx);
            }
        }

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);

        EndAbility();
    }

    // Direcciones horizontales repartidas parejo en 360° (i de count): con count=4 son
    // 0°, 90°, 180°, 270°. Así se abren en abanico en direcciones distintas.
    private static Vector3 DisperseDirection(int i, int count)
    {
        float rad = (360f / Mathf.Max(1, count)) * i * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
    }
}
