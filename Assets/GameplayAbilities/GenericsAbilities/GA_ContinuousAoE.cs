using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_ContinuousAoE
//
// Zona de efecto que dura un tiempo (TotalDuration) y le aplica la
// lista de efectos de la habilidad a todo lo que esté dentro de su radio,
// a intervalos regulares (TickInterval). Puede quedarse fija en el
// punto de activación o seguir al dueño (FollowOwner). Pensada para
// auras, charcos de veneno, tornados, etc.
//
// DÓNDE SE DESPLIEGA (DeployMode): sobre el propio dueño, o en la ZONA APUNTADA con
// la retícula (mantener el botón → marcador en el suelo → soltar, ver
// IGroundTargetAbility). Con retícula el área siempre queda FIJA en el punto elegido
// (FollowOwner se ignora): ahí es donde cae, no puede seguirte.
//
// A QUIÉN AFECTA lo dice cada entrada de la lista de efectos: una zona puede dañar
// enemigos y curar aliados a la vez (Luz del amanecer), o silenciar a todos y curar
// solo a los propios (Zona de verdad). El VFX de la zona es una entrada "En el impacto"
// con "Calzar con el área" (mide lo mismo que el radio) y "Lo sigue" (si la zona sigue
// al dueño, el VFX también). Con Destroy Time en 0 dura lo que la zona.
//
// Para un golpe de área de UNA sola aplicación (sin ticks ni duración) usá
// GA_InstantAoE — este es para zonas que persisten.
// ============================================================
[CreateAssetMenu(fileName = "GA_ContinuousAoE", menuName = "GAS/Generics/Continuous AoE")]
public class GA_ContinuousAoE : GameplayAbility, IGroundTargetAbility
{
    // A quién afectaba el área en el formato viejo (ahora lo dice cada entrada de efectos).
    public enum EAoETarget { Enemies, Allies, All }

    // Dónde nace el área. AtOwner es el valor 0 = el comportamiento de siempre, así
    // que los assets ya configurados no cambian.
    public enum EAoEDeploy { AtOwner, AtReticle }

    // Cómo se traduce el viejo "Targets" de un área a la entrada de efectos.
    public static EEffectTarget ToEffectTarget(EAoETarget targets)
    {
        switch (targets)
        {
            case EAoETarget.Allies: return EEffectTarget.Allies;
            case EAoETarget.All:    return EEffectTarget.Everyone;
            default:                return EEffectTarget.Enemies;
        }
    }

    [Section(AbilitySection.Shape)]
    [Tooltip("Radio del área, en metros.")]
    public float Radius        = 4f;

    [Tooltip("AtOwner: el área nace sobre el dueño (comportamiento clásico). AtReticle: se apunta " +
             "con el marcador en el suelo (mantener → apuntar → soltar) y queda fija ahí.")]
    public EAoEDeploy DeployMode = EAoEDeploy.AtOwner;

    [ShowIf(nameof(DeployMode), EAoEDeploy.AtReticle)]
    [Tooltip("Alcance máximo al que se puede desplegar la zona.")]
    public float MaxRange = 15f;

    // Si el área se mueve con el dueño o queda fija donde se activó. Se ignora con
    // DeployMode = AtReticle (una zona apuntada siempre queda fija donde cae).
    [ShowIf(nameof(DeployMode), EAoEDeploy.AtOwner)]
    [Tooltip("La zona se mueve con el dueño (un aura). Apagado = queda fija donde nació.")]
    public bool FollowOwner = true;

    // IGroundTargetAbility: el marcador del suelo usa estos valores para la vista
    // previa. PlayerController solo muestra el marcador si DeployMode es AtReticle
    // (ver UsesGroundTarget).
    public float MaxTargetRange => MaxRange;
    public float TargetRadius   => Radius;

    // True si esta configuración se apunta con la retícula.
    public bool UsesGroundTarget => DeployMode == EAoEDeploy.AtReticle;

    [Section(AbilitySection.Timing)]
    [Tooltip("Espera antes de que el área empiece a existir, tras activar la habilidad.")]
    public float StartDelay = 0.5f;

    [Tooltip("Cuánto dura la zona, en segundos.")]
    public float TotalDuration = 5f;

    // Cada cuánto se vuelve a revisar quién está dentro del área y se le
    // reaplican los efectos.
    [Tooltip("Cada cuántos segundos se aplican los efectos a los que están adentro.")]
    public float TickInterval  = 0.5f;

    public override float VisualAreaRadius => Radius;
    protected override float VisualDefaultLifetime => TotalDuration;
    public override bool SupportsVisualTiming(EVisualWhen when) => true;

    // El VFX "en el impacto" con "Lo sigue" se pega al dueño solo si la zona lo sigue.
    protected override Transform VisualImpactParent(AbilitySystemComponent owner)
        => ShouldFollowOwner && owner != null ? owner.transform : null;

    // ¿El área sigue al dueño? Solo puede seguirlo si nace sobre él: una zona
    // apuntada con la retícula queda siempre fija donde cayó.
    private bool ShouldFollowOwner => FollowOwner && DeployMode == EAoEDeploy.AtOwner && !_hasCenterOverride;

    // Despliegue pedido por OTRA habilidad (la bandera del Salto heroico): nace en ese
    // punto, fija, y sin animación propia (la del lanzador ya la puso quien la llama).
    [System.NonSerialized] private bool    _hasCenterOverride;
    [System.NonSerialized] private Vector3 _centerOverride;

    public void ActivateAt(Vector3 center)
    {
        _hasCenterOverride = true;
        _centerOverride    = center;
        Activate();
    }

    // Solo la ZONA, fija en 'center', sin el ciclo de vida de una habilidad: ni costo, ni
    // cooldown, ni animación, ni EndAbility. Para las que van dejando zonas mientras duran
    // (el Aliento del dragón rojo suelta una por tick): EndAbility le avisaría al dueño
    // que terminó su ataque en plena canalización. Se puede llamar varias veces seguidas
    // sobre la misma instancia: cada zona corre su propia corutina. Server-side.
    public void DeployZoneAt(Vector3 center)
    {
        if (!IsServer || OwnerASC == null) return;

        _hasCenterOverride = true;   // fija donde cae: no sigue al dueño
        _centerOverride    = center;
        OwnerASC.StartAbilityCoroutine(DeployedZone(center));
    }

    private IEnumerator DeployedZone(Vector3 center)
    {
        if (StartDelay > 0f) yield return new WaitForSeconds(StartDelay);
        yield return AreaRoutine(center);
    }

    // Valida, cobra costo/cooldown y arranca la secuencia del área.
    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();

            // El centro se resuelve ACÁ, al activar: con la retícula, el punto de mira
            // es el del instante del casteo (si lo leyéramos después del StartDelay, el
            // jugador ya podría estar apuntando a otro lado).
            Vector3 center = _hasCenterOverride ? _centerOverride : ResolveCenter(pc);

            // La animacion normal es un disparo suelto. Una subclase puede reemplazarla
            // por algo sostenido (ver GA_Whirlwind) devolviendo true en el hook.
            if (!_hasCenterOverride && !TryPlayCustomAnimation(pc) && pc != null) pc.PlayAnimation(this);

            OnAreaStarted();
            OwnerASC.StartAbilityCoroutine(AoESequence(center));
        }
    }

    // Punto donde nace el área: el dueño, o la zona apuntada (recortada a MaxRange
    // para que coincida con la vista previa del marcador).
    private Vector3 ResolveCenter(PlayerController pc)
    {
        Vector3 origin = OwnerASC.transform.position;
        if (DeployMode == EAoEDeploy.AtOwner) return origin;

        Vector3 center = pc != null ? pc.GetAimPoint(MaxRange)
                                    : origin + OwnerASC.transform.forward * MaxRange;

        Vector3 toZone = center - origin;
        if (toZone.magnitude > MaxRange) center = origin + toZone.normalized * MaxRange;
        return center;
    }

    // Espera StartDelay (ajustado por velocidad de ataque), libera al jugador,
    // y deja el área corriendo en segundo plano hasta que termina.
    private IEnumerator AoESequence(Vector3 center)
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

        // try/finally: si la corutina se corta a mitad (muerte, respawn, cambio de
        // clase), Unity dispone el iterador y el finally corre igual. Una subclase que
        // deje al jugador en un estado especial durante el area (bloqueado, girando)
        // depende de eso para no dejarlo trabado si la habilidad se interrumpe.
        try
        {
            yield return OwnerASC.StartCoroutine(AreaRoutine(center));
        }
        finally
        {
            OnAreaFinished();
        }
    }

    // =========================================================
    // HOOKS PARA SUBCLASES
    //
    // El area en si (radio, ticks, VFX, seguir al dueno) es igual para todas: un aura
    // de jefe, una zona de hielo y el molinete del barbaro comparten toda esa maquina.
    // Lo que cambia es que ALGUNAS ademas le hacen algo al LANZADOR mientras dura.
    //
    // Estos tres hooks son ese punto de extension, y estan vacios a proposito: una
    // habilidad de area normal no tiene por que saber que existe el molinete.
    // =========================================================

    // Reemplaza la animacion de disparo por una propia. Devolver true = ya se encargo
    // la subclase y la base no dispara la suya.
    protected virtual bool TryPlayCustomAnimation(PlayerController pc) => false;

    // Justo despues de lanzar el area. Para lo que dure TODA el area.
    protected virtual void OnAreaStarted() { }

    // Al terminar el area — o al interrumpirse, gracias al finally de arriba. Todo lo
    // que se haya prendido en OnAreaStarted se apaga aca.
    protected virtual void OnAreaFinished() { }

    // Reproduce el VFX del área, y cada TickInterval revisa quién está
    // dentro del radio para aplicarle la lista de efectos, durante TotalDuration segundos.
    private IEnumerator AreaRoutine(Vector3 spawnPoint)
    {
        float timeElapsed = 0f;
        bool firstEnemy = true;

        // Un tick de 0 (o negativo) NO es "cada frame": con WaitForSeconds(0) el bucle
        // avanza un frame por vuelta y timeElapsed nunca crece, así que el área aplicaba
        // sus efectos cada frame PARA SIEMPRE. Fue lo que hizo que el jefe matara de un
        // golpe a un Berserker de 260 de vida, y lo que inundaba la red hasta partir los
        // paquetes (el "unhandled PacketId of 0" de FishNet). Se interpreta como UNA sola
        // aplicación al empezar, y el área queda el resto de TotalDuration solo de adorno.
        float tick = TickInterval;
        if (tick <= 0f)
        {
            Debug.LogWarning($"[{name}] TickInterval en {TickInterval}: se aplica una sola vez. " +
                             "Si querés un golpe único de área usá GA_InstantAoE.");
            tick = Mathf.Max(TotalDuration, 0.05f);
        }

        // Instantiate() acá solo se vería en el proceso servidor —
        // BroadcastImpactVFX lo reproduce en todos los peers (cada uno con su propia
        // copia, que se autodestruye sola tras TotalDuration en vez de que la sigamos
        // con una referencia acá).
        BroadcastImpactVFX(spawnPoint);

        while (timeElapsed < TotalDuration)
        {
            Vector3 center = ShouldFollowOwner ? OwnerASC.transform.position : spawnPoint;
            Collider[] hits = Physics.OverlapSphere(center, Radius, TargetLayer);

            foreach (var hit in hits)
            {
                AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
                if (targetASC == null) continue;

                // Cada entrada de la lista dice a quién va: la Zona de verdad silencia a
                // los tres equipos y cura solo a los suyos; la Luz del amanecer quema a los
                // enemigos y cura a los aliados que estén adentro.
                bool enemy = IsEnemy(targetASC);
                if (!ApplyHitEffects(targetASC, firstHit: enemy && firstEnemy)) continue;

                BroadcastHitVFX(targetASC, withSound: false);

                if (!enemy) continue;
                firstEnemy = false;

                OnTargetHit(targetASC);
                if (OwnerASC.CompareTag("Player")) ChargeUltimate();
            }

            yield return new WaitForSeconds(tick);
            timeElapsed += tick;
        }
    }

    // Gancho para que una habilidad concreta reaccione a cada enemigo alcanzado, en
    // cada tick (ej: los Cañones del Pirata, que además le apuestan a quien golpean).
    protected virtual void OnTargetHit(AbilitySystemComponent target) { }

    // Vista previa del área en el Editor. Con AtReticle además dibuja el alcance
    // máximo (dónde se puede llegar a poner la zona) y la zona en ese tope.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        if (DeployMode == EAoEDeploy.AtReticle)
        {
            // Alcance al que se puede desplegar.
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(origin.position, MaxRange);

            // La zona, dibujada en el tope del alcance hacia adelante.
            Vector3 preview = origin.position + origin.forward * MaxRange;
            Gizmos.color = new Color(0.2f, 0.9f, 0.5f, 0.35f);
            Gizmos.DrawSphere(preview, Radius);
            return;
        }

        Gizmos.color = new Color(0.2f, 0.9f, 0.5f, 0.35f);
        Gizmos.DrawSphere(origin.position, Radius);
    }

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
    {
        if (origin == null) return;

        Vector3 center = origin.position;
        if (DeployMode == EAoEDeploy.AtReticle)
        {
            AbilityHandles.Distance(this, "Max Range", origin.position, origin.forward, ref MaxRange,
                                    AbilityHandles.DistanceColor);
            center = origin.position + origin.forward * MaxRange;
        }
        AbilityHandles.Radius(this, "Radius", center, ref Radius, AbilityHandles.RadiusColor, origin.right);
    }
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    // Se llamaba "Objetivos".
    [UnityEngine.Serialization.FormerlySerializedAs("Objetivos")]
    [SerializeField, HideInInspector] private EAoETarget Targets = EAoETarget.Enemies;
    [SerializeField, HideInInspector] private List<GameplayEffect> EffectsToApply;
    [SerializeField, HideInInspector] private List<GameplayEffect> AllyEffects;
    [SerializeField, HideInInspector] private GameObject VisualPrefab;
    [SerializeField, HideInInspector] private float VisualScaleMultiplier = 2f;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        UpgradeEffects(EffectsToApply, EEffectWhen.OnHit, ToEffectTarget(Targets), ref changed);
        UpgradeEffects(AllyEffects, EEffectWhen.OnHit, EEffectTarget.Allies, ref changed);
        UpgradeVisual(ref VisualPrefab, new AbilityVisual
        {
            When = EVisualWhen.OnImpact, MatchAreaSize = true, Attach = true,
            AreaSizeMultiplier = VisualScaleMultiplier,
        }, ref changed);
    }
}
