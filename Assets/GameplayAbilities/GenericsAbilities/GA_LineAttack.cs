using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_LineAttack
//
// Ataque cuerpo a cuerpo en forma de caja rectangular frente al
// dueño (Length x Width x Height): detecta a quien caiga dentro de esa caja y
// le aplica la lista de efectos de la habilidad. Pensada para ataques de arma
// larga/lanza que pegan en línea recta en vez de en cono.
// ============================================================
[CreateAssetMenu(fileName = "GA_LineAttack", menuName = "GAS/Generics/Line Attack")]
public class GA_LineAttack : GameplayAbility
{
    [Section(AbilitySection.Shape)]
    [Tooltip("Largo de la caja de detección, hacia adelante desde el punto de salida.")]
    public float Length = 5f;

    [Tooltip("Ancho de la caja de detección.")]
    public float Width  = 2f;

    [Tooltip("Alto TOTAL de la caja de detección.\n\n" +
             "OJO CON EL CENTRADO: la caja se centra en el punto de salida, que por defecto es el " +
             "pivote del dueño, a los PIES. Con 2 va de un metro BAJO el suelo a un metro sobre los " +
             "pies — o sea que la mitad se desperdicia enterrada y apenas llega a la cintura de " +
             "quien tenés enfrente.\n\n" +
             "Para cubrir un cuerpo entero de pie querés 4 o más (o subir la Y del Origin Offset). " +
             "Bajalo para un barrido rasante que pase por debajo de los que saltan.")]
    public float Height = 2f;

    [Tooltip("Desde DÓNDE sale la caja, en espacio local del dueño (X = derecha, Y = arriba, " +
             "Z = adelante).\n\n" +
             "El pivote está a los PIES, así que la caja nace a ras del suelo y la mitad de su " +
             "alto queda enterrada. Subir la Y a la altura del pecho (1.2-1.5) es lo que hace " +
             "que la estocada golpee donde se la ve.\n\n" +
             "En CERO (el default) sale del pivote, como se comportó siempre.")]
    public Vector3 OriginOffset = Vector3.zero;

    [Tooltip("La estocada se inclina hacia donde apunta la CÁMARA (arriba o abajo), en vez de " +
             "salir siempre horizontal. La caja de detección se inclina con ella.\n\n" +
             "El giro horizontal NO cambia: lo sigue dando el cuerpo, que acompaña la mira en " +
             "vivo. Lo único que se toma de la mira es la INCLINACIÓN.")]
    public bool UseVerticalAim = false;

    // Valida, rota al dueño hacia el punto de mira, cobra costo/cooldown
    // y arranca la secuencia de ataque.
    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.RotateToAim();
                pc.PlayAnimation(this);
            }
            OwnerASC.StartAbilityCoroutine(AttackSequence());
        }
    }

    // Resuelve el golpe en el/los frames de impacto que marca el clip, espera el
    // remate de la animación, y termina.
    private IEnumerator AttackSequence()
    {
        float speedMultiplier = 1f;
        float atkSpeedStat = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (atkSpeedStat > 0) speedMultiplier = 1f / atkSpeedStat;

        // El timing lo maneja GameplayAbility: si el clip tiene varios eventos de
        // impacto, esto se convierte solo en una estocada de varios tiempos.
        yield return HitTimingRoutine(PerformDamage);

        // Interrumpido por otra habilidad (ver GameplayAbility.IsInterruptible): ni golpe
        // ni EndAbility — el fin lo manda la habilidad que lo cortó.
        if (WasCancelled) yield break;

        yield return new WaitForSeconds(0.5f / speedMultiplier);

        EndAbility();
    }

    // Arma la caja (Length x Width) frente al dueño y le aplica los efectos a todos los
    // que caigan dentro, reproduciendo el VFX de golpe en todos los peers.
    //
    // targetsHit lo provee HitTimingRoutine y se COMPARTE entre los frames de impacto
    // del mismo golpe: así no se le pega dos veces al mismo objetivo.
    private void PerformDamage(HashSet<AbilitySystemComponent> targetsHit)
    {
        Vector3 origin    = OwnerASC.transform.TransformPoint(OriginOffset);
        Vector3 direction = ResolveAttackDirection(UseVerticalAim);
        float   length    = Length * MeleeReach;   // el Avatar del Guardián pega más lejos
        Vector3 center    = origin + (direction * (length / 2));
        Vector3 halfExtents = new Vector3(Width / 2f, Height / 2f, length / 2f);

        // La caja se orienta según la DIRECCIÓN del golpe y no según la rotación del
        // cuerpo: son lo mismo mientras la estocada sea horizontal, pero cuando se
        // apunta hacia arriba o hacia abajo la caja tiene que inclinarse con ella. Si
        // se quedara con la rotación del cuerpo, la estocada apuntaría a un lado y
        // seguiría golpeando al frente.
        Quaternion boxRotation = Quaternion.LookRotation(direction, Vector3.up);

        Collider[] hits = Physics.OverlapBox(center, halfExtents, boxRotation, TargetLayer);

        // En el orden en que la estocada los alcanza: el "primer golpe" es el de adelante.
        System.Array.Sort(hits, (a, b) =>
            Vector3.Dot(a.transform.position - origin, direction)
            .CompareTo(Vector3.Dot(b.transform.position - origin, direction)));

        foreach (var hit in hits)
        {
            AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC == null || targetsHit.Contains(targetASC)) continue;

            // Enemigos siempre; aliados solo si la lista trae algo para ellos.
            if (!ApplyHitEffects(targetASC, firstHit: targetsHit.Count == 0)) continue;

            if (IsEnemy(targetASC)) ChargeUltimate();

            targetsHit.Add(targetASC);

            // Instantiate() acá solo se vería en el proceso servidor —
            // BroadcastHitVFX lo reproduce en todos los peers.
            BroadcastHitVFX(targetASC);
        }
    }

    // Dibuja el mismo box que usa PerformDamage() (OverlapBox), con la
    // misma posición/rotación/tamaño reales.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Vector3 start       = origin.TransformPoint(OriginOffset);
        Vector3 center      = start + origin.forward * (Length / 2f);
        Vector3 halfExtents = new Vector3(Width / 2f, Height / 2f, Length / 2f);

        Matrix4x4 prevMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, origin.rotation, Vector3.one);

        Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.25f);
        Gizmos.DrawCube(Vector3.zero, halfExtents * 2f);
        Gizmos.color = new Color(0.1f, 0.6f, 1f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);

        Gizmos.matrix = prevMatrix;
    }

#if UNITY_EDITOR
    // La caja se arrastra cara por cara: la de adelante cambia el largo, las de los costados
    // el ancho y las de arriba/abajo el alto. Mover la cara de atrás o la de arriba también
    // corre el punto de salida (Origin Offset), porque la caja nace ahí.
    public override void DrawSceneHandles(Transform origin)
    {
        if (origin == null) return;

        Vector3 start  = origin.TransformPoint(OriginOffset);
        Vector3 center = start + origin.forward * (Length / 2f);
        Vector3 size   = new Vector3(Width, Height, Length);

        if (AbilityHandles.Box(this, "Line Box", center, origin.rotation, ref size, out Vector3 newCenter,
                               AbilityHandles.BoxColor))
        {
            Width  = size.x;
            Height = size.y;
            Length = size.z;

            Vector3 newStart = newCenter - origin.forward * (Length / 2f);
            OriginOffset = origin.InverseTransformPoint(newStart);
        }

        AbilityHandles.Label(start + origin.forward * Length + Vector3.up * (Height * 0.5f + 0.2f),
                             $"Length {Length:0.##} m · Width {Width:0.##} · Height {Height:0.##}",
                             AbilityHandles.BoxColor);
    }
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GameplayEffect DamageEffect;
    [SerializeField, HideInInspector] private List<GameplayEffect> AdditionalEffects;
    [UnityEngine.Serialization.FormerlySerializedAs("AllyEffects")]
    [SerializeField, HideInInspector] private List<GameplayEffect> TargetEffects;
    [SerializeField, HideInInspector] private GameObject HitVFX;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        UpgradeEffect(ref DamageEffect, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeEffects(AdditionalEffects, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeEffects(TargetEffects, EEffectWhen.OnHit, EEffectTarget.Allies, ref changed);
        UpgradeVisual(ref HitVFX, new AbilityVisual { When = EVisualWhen.OnHit, Offset = Vector3.up, DestroyTime = 2f },
                      ref changed);
    }
}
