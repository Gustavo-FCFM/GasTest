using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_HeroicInterception  (Intercepción heroica — movimiento del Paladín)
//
// El jugador elige al ALIADO que tiene más centrado en la mira dentro de un
// alcance medio, salta a ponerse DELANTE de él —del lado hacia el que el aliado
// está mirando, y encarando para ese mismo lado— y les da un buff de resistencia
// a los dos. Tiene 2 cargas.
//
// Es la contracara del Golpe mortal del Pícaro (GA_Blink): aquel busca ENEMIGOS y
// aparece a su ESPALDA para apuñalarlos; este busca ALIADOS y aparece a su FRENTE
// para comerse el golpe por ellos. Se dejó como habilidad propia y no como una
// variante genérica de GA_Blink porque, más allá del teletransporte, casi todo lo
// demás es distinto (afiliación, orientación, buff a dos objetivos, cargas).
//
// RED: como toda GameplayAbility, Activate() corre en el SERVIDOR (autoridad del
// buff y de la elección de objetivo). El movimiento del CharacterController, en
// cambio, tiene que ejecutarlo el proceso DUEÑO porque el transform es
// client-authoritative — se delega en NetworkASC.ServerTeleportOwnerTo, igual que
// hacen el blink del Pícaro y el respawn.
//
// CARGAS: mismo esquema que GA_Dash. El estado vive solo en el servidor, y el
// bloqueo del dueño se hace con el TAG del cooldown (que sí se sincroniza), así
// que el CooldownEffect se aplica únicamente al gastar la ÚLTIMA carga. El
// cooldown de la habilidad ES, además, lo que tarda en volver cada carga.
// ============================================================
[CreateAssetMenu(fileName = "GA_HeroicInterception", menuName = "GAS/Specific Abilities/Paladin/Heroic Interception")]
public class GA_HeroicInterception : GameplayAbility
{
    [Header("Selección de Aliado")]
    [Tooltip("Alcance máximo para buscar al aliado al que interceptar.")]
    public float MaxRange = 14f;

    [Tooltip("Ángulo máximo (grados) entre la mira y el aliado para que cuente como objetivo. " +
             "Más alto = más fácil de enganchar, pero más fácil también de agarrar al que no querías.")]
    public float SelectionAngle = 30f;

    [Tooltip("Si se puede interceptar a un aliado MUERTO. Normalmente no: saltar a cubrir un " +
             "cadáver desperdicia la carga.")]
    public bool AllowDeadTargets = false;

    [Header("Salto")]
    [Tooltip("A qué distancia por DELANTE del aliado (según hacia dónde mira él) aterriza el Paladín.")]
    public float FrontDistance = 2.5f;

    [Tooltip("Si el Paladín queda mirando hacia donde miraba el aliado (o sea, dándole la espalda " +
             "y encarando la amenaza). Desactivado = queda mirando al aliado.")]
    public bool FaceAwayFromAlly = true;

    [Header("Efectos")]
    [Tooltip("Buff que se aplica al PROPIO Paladín al interceptar (la resistencia al daño).")]
    public List<GameplayEffect> SelfEffects;
    // Al ALIADO interceptado se le aplica la lista TargetEffects de GameplayAbility
    // (el mismo buff, normalmente): así el campo es el mismo que en el resto de las
    // habilidades que tocan aliados y no inventamos uno nuevo.

    [Header("Visuales")]
    public GameObject ImpactVFX;

    // =========================================================
    // ACTIVACIÓN
    // =========================================================

    public override void Activate()
    {
        if (!IsServer) return;

        // Gate estándar: incluye el tag de cooldown (que el dueño también ve por
        // NetTags, así su predicción coincide con el servidor) y las cargas.
        if (!CanActivate()) return;

        AbilitySystemComponent ally = FindAlly();

        // Sin aliado a la vista no se gasta nada: ni carga, ni cooldown, ni costo.
        if (ally == null)
        {
            EndAbility();
            return;
        }

        // Recién acá se cobra: gasta la carga, el costo, aplica el cooldown solo si
        // era la última, y reproduce la VisualsSequence (todo en la clase base). Va
        // DESPUÉS de encontrar aliado a propósito — sin aliado no se gasta nada.
        CommitAbility();

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        // Punto de aterrizaje: delante del aliado, del lado al que MIRA él (no al que
        // mira el Paladín). Se conserva su altura para no aparecer flotando ni
        // hundido en el piso.
        Vector3 allyForward = ally.transform.forward;
        allyForward.y = 0f;
        if (allyForward.sqrMagnitude < 0.0001f) allyForward = Vector3.forward;
        allyForward.Normalize();

        Vector3 landing = ally.transform.position + allyForward * FrontDistance;
        landing.y = ally.transform.position.y;

        Vector3 faceDir = FaceAwayFromAlly ? allyForward : -allyForward;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();

        // El teletransporte lo ejecuta el proceso dueño (CC client-authoritative).
        if (netAsc != null) netAsc.ServerTeleportOwnerTo(landing, faceDir);
        else if (pc != null) pc.TeleportTo(landing, faceDir); // fallback sin red

        if (pc != null) pc.PlayAnimation(this);

        // Buffs: al aliado interceptado y a uno mismo.
        ApplyEffectsTo(TargetEffects, ally);
        ApplyEffectsTo(SelfEffects, OwnerASC);

        Vector3 vfxPos = landing + Vector3.up;
        if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, vfxPos);
        else PlayImpactVFX(vfxPos);

        EndAbility();
    }

    // =========================================================
    // SELECCIÓN DE OBJETIVO
    // =========================================================

    // Elige al aliado más alineado con la mira, dentro de MaxRange y SelectionAngle.
    // En el servidor, GetAimPoint() usa el NetworkAimPoint que el dueño mandó junto
    // con el input (ver ServerActivateAbility).
    //
    // Se excluye a uno mismo a propósito: interceptarte a vos mismo no significa nada
    // (IsAllyOf con includeSelf daría true y el Paladín podría "saltar" a su propio
    // frente, gastando una carga por nada).
    private AbilitySystemComponent FindAlly()
        => FindBestTargetInAim(MaxRange, SelectionAngle, ETargetAffiliation.Allies,
                               includeSelf: false, allowDead: AllowDeadTargets);

    // =========================================================
    // VISUALES Y GIZMOS
    // =========================================================

    public override void PlayImpactVFX(Vector3 position)
    {
        if (ImpactVFX == null) return;
        GameObject vfx = Instantiate(ImpactVFX, position, Quaternion.identity);
        Destroy(vfx, 2.0f);
    }

    // Vista previa del alcance de selección en el Editor.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Gizmos.color = new Color(0.95f, 0.85f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(origin.position, MaxRange);
    }
}
