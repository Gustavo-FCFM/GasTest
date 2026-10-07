using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GA_Target  (genérico — "elegí un objetivo y aplicale esto")
//
// Apunta al personaje que el jugador tiene más centrado en la mira dentro de un
// alcance, y le aplica los efectos de la habilidad. Sirve para las dos afiliaciones
// (ver el campo Targets), que es el patrón que se repite en todo el juego:
//
//   · Imposición de manos (Paladín/Devoción): cura fuerte a un aliado cercano;
//     sin aliado seleccionado, se cura él.
//   · Protección divina (Paladín/Devoción, ult): vuelve inmune a un aliado.
//   · Presencia conquistadora (Paladín/Conquista): aturde a un enemigo lejano.
//   · La curación apuntada del Clérigo y su "Canalizar divinidad: Aturdir".
//
// QUÉ APLICA: las entradas "Al golpear" de su lista de efectos — lo que recibe el elegido, apunte a
// un aliado o a un enemigo. Podés poner tantos GE como quieras (curación, escudo,
// un debuff con tag, los tres).
//
// Para una curación "del total de la vida máxima del lanzador", el GE va con un
// Modifier sobre Health con UseAttributeScaling, SourceAttribute = MaxHealth y
// coeficiente 1: el escalado siempre mira los stats de QUIEN lanza, no del objetivo.
//
// NO gasta nada si no hay a quién aplicárselo: sin objetivo válido (y con el
// autocasteo apagado) la habilidad sale sin cobrar costo, cooldown ni carga.
// ============================================================
[CreateAssetMenu(fileName = "GA_Target", menuName = "GAS/Generics/Target")]
public class GA_Target : TargetImpactAbility
{
    // A quién apunta la habilidad. Allies es el valor 0 = el comportamiento original,
    // así que los assets ya configurados no cambian al agregar esto.
    public enum ETargetSide { Allies, Enemies }

    [Section(AbilitySection.Targeting)]
    [Tooltip("A quién apunta. Allies: curaciones, escudos y protecciones. Enemies: marcas, " +
             "aturdimientos y debuffs de objetivo único (Enemigo jurado, Presencia conquistadora, " +
             "el 'Canalizar divinidad: Aturdir' del Clérigo).\n\n" +
             "Con Enemies, el autocasteo sobre uno mismo se ignora aunque esté marcado: no tiene " +
             "sentido aplicarte a vos mismo un debuff porque no encontraste a nadie.")]
    public ETargetSide Targets = ETargetSide.Allies;

    [Tooltip("Alcance máximo para buscar al objetivo.")]
    public float MaxRange = 12f;

    [Tooltip("Ángulo máximo (grados) entre la mira y el aliado para que cuente como objetivo. " +
             "Más alto = más fácil de enganchar, pero más fácil también de agarrar al que no querías.")]
    public float SelectionAngle = 30f;

    [ShowIf(nameof(Targets), ETargetSide.Allies)]
    [Tooltip("Si no hay ningún aliado en la mira, ¿se lo aplica a SÍ MISMO? Es el " +
             "'si no hay aliado seleccionado, se selecciona a sí mismo' del diseño. " +
             "Apagado = sin objetivo la habilidad no se lanza y no gasta nada.")]
    public bool SelfIfNoTarget = true;

    [Tooltip("Si se puede elegir a un aliado MUERTO. Solo tiene sentido en habilidades que " +
             "revivan; para una curación normal dejalo apagado o vas a desperdiciar el uso.")]
    public bool AllowDeadTargets = false;

    // LOS EFECTOS van en la lista de la habilidad, "Al golpear" con el bando que se apunta
    // (Aliados o Enemigos): curación, escudo, inmunidad, un debuff con tag... los que
    // quieras. Para una curación "del total de la vida máxima del lanzador", el GE va con
    // un Modifier sobre Health con UseAttributeScaling, SourceAttribute = MaxHealth y
    // coeficiente 1: el escalado siempre mira los stats de QUIEN lanza.

    // =========================================================
    // ACTIVACIÓN
    // =========================================================

    // Igual que en GA_SwornEnemy: sin objetivo válido la habilidad no se activa, así el
    // dueño no gasta la animación ni el turno apuntando al aire. Con autocasteo hacia
    // aliados siempre hay objetivo (uno mismo), así que ahí ni buscamos.
    public override bool CanActivate()
    {
        if (!base.CanActivate()) return false;

        if (SelfIfNoTarget && Targets == ETargetSide.Allies) return true;

        return ResolveTarget() != null;
    }

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

        if (!HasEffects(EEffectWhen.OnHit))
            Debug.LogWarning($"[{AbilityName}] no tiene ningún efecto 'Al golpear': " +
                             $"selecciona objetivo pero no le aplica nada.");

        ApplyHitEffects(target, firstHit: true);

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.RotateToAim();
            pc.PlayAnimation(this);
        }

        PlayImpactVFXOnTarget(target);

        EndAbility();
    }

    // El aliado apuntado, o uno mismo si no hay ninguno y el autocasteo está activo.
    //
    // La búsqueda excluye al lanzador a propósito, aunque después pueda caer sobre él:
    // si se incluyera, apuntar al vacío te elegiría a vos mismo por ser el más
    // "centrado", y nunca alcanzarías al aliado que tenés un poco al costado.
    // Sin objetivo, el dueño no anticipa la animación: el servidor va a descartar la
    // activación igual y el gesto quedaría en el vacío.
    public override bool CanPredictActivation() => ResolveTarget() != null;

    private AbilitySystemComponent ResolveTarget()
    {
        ETargetAffiliation side = Targets == ETargetSide.Enemies
            ? ETargetAffiliation.Enemies
            : ETargetAffiliation.Allies;

        AbilitySystemComponent target = FindBestTargetInAim(
            MaxRange, SelectionAngle, side,
            includeSelf: false, allowDead: AllowDeadTargets);

        if (target != null) return target;

        // El autocasteo solo tiene sentido apuntando a aliados: si la habilidad
        // aplica un debuff y no encontró enemigo, lo correcto es no hacer nada.
        if (Targets == ETargetSide.Enemies) return null;

        return SelfIfNoTarget ? OwnerASC : null;
    }

    // =========================================================
    // VISUALES Y GIZMOS
    // =========================================================

    // Vista previa del alcance de selección en el Editor.
    public override void DrawGizmos(Transform origin)
        => DrawSelectionGizmo(origin, MaxRange, SelectionAngle, new Color(0.4f, 1f, 0.6f, 0.9f));

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
        => AbilityHandles.Selection(this, origin, ref MaxRange, ref SelectionAngle);
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    // Se llamó "AllyEffects" y vivía en GameplayAbility.
    [UnityEngine.Serialization.FormerlySerializedAs("AllyEffects")]
    [SerializeField, HideInInspector] private List<GameplayEffect> TargetEffects;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        // Los recibía el elegido, del bando que apunta la habilidad.
        EEffectTarget side = Targets == ETargetSide.Enemies ? EEffectTarget.Enemies : EEffectTarget.Allies;
        UpgradeEffects(TargetEffects, EEffectWhen.OnHit, side, ref changed);
    }
}
