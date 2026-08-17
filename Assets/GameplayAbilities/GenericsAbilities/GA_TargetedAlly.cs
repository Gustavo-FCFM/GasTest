using UnityEngine;

// ============================================================
// GA_TargetedAlly  (genérico — "elegí un aliado y aplicale esto")
//
// Apunta al aliado que el jugador tiene más centrado en la mira dentro de un
// alcance, y le aplica los efectos de la habilidad. Si no hay nadie a la vista
// puede caer sobre UNO MISMO, que es el patrón que se repite en todo el juego:
//
//   · Imposición de manos (Paladín/Devoción): cura fuerte a un aliado cercano;
//     sin aliado seleccionado, se cura él.
//   · Protección divina (Paladín/Devoción, ult): vuelve inmune a un aliado;
//     sin aliado seleccionado, a él mismo.
//   · La curación apuntada del Clérigo.
//
// QUÉ APLICA: la lista AllyEffects de GameplayAbility — la misma que usan el resto
// de las habilidades que tocan aliados, así que no hay un campo nuevo que aprender.
// Podés poner tantos GE como quieras (curación, escudo, un buff con tag, los tres).
//
// Para una curación "del total de la vida máxima del lanzador", el GE va con un
// Modifier sobre Health con UseAttributeScaling, SourceAttribute = MaxHealth y
// coeficiente 1: el escalado siempre mira los stats de QUIEN lanza, no del objetivo.
//
// NO gasta nada si no hay a quién aplicárselo: sin objetivo válido (y con el
// autocasteo apagado) la habilidad sale sin cobrar costo, cooldown ni carga.
// ============================================================
[CreateAssetMenu(fileName = "GA_TargetedAlly", menuName = "GAS/Generics/Targeted Ally")]
public class GA_TargetedAlly : GameplayAbility
{
    [Header("Selección")]
    [Tooltip("Alcance máximo para buscar al aliado.")]
    public float MaxRange = 12f;

    [Tooltip("Ángulo máximo (grados) entre la mira y el aliado para que cuente como objetivo. " +
             "Más alto = más fácil de enganchar, pero más fácil también de agarrar al que no querías.")]
    public float SelectionAngle = 30f;

    [Tooltip("Si no hay ningún aliado en la mira, ¿se lo aplica a SÍ MISMO? Es el " +
             "'si no hay aliado seleccionado, se selecciona a sí mismo' del diseño. " +
             "Apagado = sin objetivo la habilidad no se lanza y no gasta nada.")]
    public bool SelfIfNoTarget = true;

    [Tooltip("Si se puede elegir a un aliado MUERTO. Solo tiene sentido en habilidades que " +
             "revivan; para una curación normal dejalo apagado o vas a desperdiciar el uso.")]
    public bool AllowDeadTargets = false;

    [Header("Visuales")]
    [Tooltip("VFX que aparece sobre el objetivo alcanzado.")]
    public GameObject ImpactVFX;

    // =========================================================
    // ACTIVACIÓN
    // =========================================================

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        AbilitySystemComponent target = ResolveTarget();

        // Sin objetivo no se gasta nada: ni costo, ni cooldown, ni carga. Igual hay que
        // liberar el estado "atacando" del dueño, que ya lo puso su predicción local.
        if (target == null)
        {
            EndAbility();
            return;
        }

        CommitAbility();

        if (AllyEffects == null || AllyEffects.Count == 0)
            Debug.LogWarning($"[{AbilityName}] no tiene ningún AllyEffect configurado: " +
                             $"selecciona objetivo pero no le aplica nada.");

        ApplyEffectsTo(AllyEffects, target);

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        if (pc != null)
        {
            pc.RotateToAim();
            pc.PlayAnimation(this);
        }

        Vector3 vfxPos = target.transform.position + Vector3.up;
        if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, vfxPos);
        else PlayImpactVFX(vfxPos);

        EndAbility();
    }

    // El aliado apuntado, o uno mismo si no hay ninguno y el autocasteo está activo.
    //
    // La búsqueda excluye al lanzador a propósito, aunque después pueda caer sobre él:
    // si se incluyera, apuntar al vacío te elegiría a vos mismo por ser el más
    // "centrado", y nunca alcanzarías al aliado que tenés un poco al costado.
    private AbilitySystemComponent ResolveTarget()
    {
        AbilitySystemComponent ally = FindBestTargetInAim(
            MaxRange, SelectionAngle, ETargetAffiliation.Allies,
            includeSelf: false, allowDead: AllowDeadTargets);

        if (ally != null) return ally;
        return SelfIfNoTarget ? OwnerASC : null;
    }

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
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.9f);
        Gizmos.DrawWireSphere(origin.position, MaxRange);
    }
}
