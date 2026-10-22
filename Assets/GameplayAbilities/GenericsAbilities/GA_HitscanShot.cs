using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GA_HitscanShot
//
// Disparo HITSCAN ("escaneo instantáneo"), el modelo clásico de las armas de fuego
// en un FPS: no se crea ninguna bala que viaje: se traza un rayo desde el arma hacia
// donde apunta la retícula y el impacto se resuelve en el MISMO frame. Si algo está
// en la línea, le pega ya (a diferencia de GC_Projectile, que sí viaja y se puede
// esquivar). Para el jugador se siente instantáneo y preciso.
//
// El rayo se corta en la primera PARED (WallLayer) y le pega al primer enemigo que
// haya antes de eso. BulletRadius > 0 usa un SphereCast en vez de una línea fina:
// una bala "gorda" perdona un poco la puntería (lo que en shooters se llama ayuda de
// apuntado); 0 sería una línea perfecta.
//
// RETROCESO (opcional): además del disparo, empuja al tirador en la dirección
// CONTRARIA al tiro, con el mismo sistema de impulso del dash. Como usa la dirección
// 3D completa de la mira, disparar hacia ABAJO te impulsa hacia ARRIBA (el clásico
// "rocket jump"), y disparar al frente te tira hacia atrás. Con RecoilSpeed = 0 no
// hay retroceso y queda un disparo normal. Al terminar el empujón no frena en seco:
// conserva una fracción configurable de la velocidad (RecoilExitSpeedPercent), igual
// que el dash — es lo que hace que el rocket jump describa un arco.
//
// RED: la resolución del tiro es server-authoritative (Activate es server-only) y usa
// el punto de mira que el dueño ya sincroniza (GetAimPoint → NetworkAimPoint). El
// empujón lo ejecuta el proceso DUEÑO vía ServerStartDash, porque el
// CharacterController es client-authoritative.
// ============================================================
[CreateAssetMenu(fileName = "GA_HitscanShot", menuName = "GAS/Generics/Hitscan Shot")]
public class GA_HitscanShot : GameplayAbility
{
    [Section(AbilitySection.Shape)]
    [Tooltip("Alcance máximo del disparo.")]
    [Label("Alcance máximo (m)")]
    public float MaxRange = 50f;

    [Tooltip("Grosor de la bala. 0 = línea fina (puntería exacta); > 0 perdona un poco la puntería.")]
    [Label("Grosor de la bala (m)")]
    public float BulletRadius = 0.15f;

    [Tooltip("Altura del cañón sobre el pivote del personaje (desde dónde sale el rayo).")]
    [Label("Altura del cañón (m)")]
    public float MuzzleHeight = 1.4f;

    [Tooltip("Distancia mínima a la que se considera que la retícula y el disparo se " +
             "juntan. La retícula sale de la cámara y el tiro del arma: apuntando a algo " +
             "muy cerca, esas dos líneas se abren y el disparo sale visiblemente torcido. " +
             "Con esto se apunta a un punto del MISMO rayo pero a esta distancia.")]
    [Label("Convergencia mínima (m)")]
    public float MinConvergeDistance = 4f;

    [Tooltip("Capas que FRENAN el disparo (paredes/entorno).")]
    [Label("Capas que frenan")]
    public LayerMask WallLayer;

    [Section("Retroceso")]
    [Tooltip("Velocidad del empujón hacia atrás (contrario al tiro: disparar al piso te eleva). " +
             "0 = sin retroceso.")]
    [Label("Retroceso: velocidad (m/s)")]
    public float RecoilSpeed = 0f;

    [ShowIf(nameof(RecoilSpeed), ShowIfAttribute.Positive)]
    [Tooltip("Cuánto dura el empujón (después cae por gravedad).")]
    [Label("Retroceso: duración (s)")]
    public float RecoilDuration = 0.25f;

    [Tooltip("Qué fracción de la velocidad del retroceso se CONSERVA cuando termina el empujón, " +
             "en vez de frenar en seco. 0 = frenada seca. 0.35 = seguís volando hacia atrás un " +
             "rato y se va disipando. Se conserva también la parte VERTICAL, así que un rocket " +
             "jump mantiene el arco en vez de caer a plomo apenas termina el impulso.")]
    [Range(0f, 1f)]
    [ShowIf(nameof(RecoilSpeed), ShowIfAttribute.Positive)]
    [Label("Retroceso: inercia al terminar (0 a 1)")]
    public float RecoilExitSpeedPercent = 0.35f;

    [ShowIf(nameof(RecoilSpeed), ShowIfAttribute.Positive)]
    [Tooltip("Qué tan rápido se disipa esa inercia, en 1/segundos. Más alto = se va antes. " +
             "Mientras dura, el jugador conserva el control normal: puede girar, frenar y saltar.")]
    [Label("Retroceso: disipación (más = frena antes)")]
    public float RecoilExitDamping = 3f;

    [ShowIf(nameof(RecoilSpeed), ShowIfAttribute.Positive)]
    [Tooltip("Capa de JUGADORES a atravesar durante el empujón (igual que en el dash).")]
    [Label("Atraviesa capas")]
    public LayerMask ExcludePlayerLayer;

    // El impacto del tiro (en la pared o en el enemigo) es un punto: acepta VFX de impacto.
    public override bool SupportsVisualTiming(EVisualWhen when) => true;

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC == null) return;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        // Origen del rayo: a la altura del arma, no a los pies.
        Vector3 origin = OwnerASC.transform.position + Vector3.up * MuzzleHeight;

        // Dirección 3D hacia la retícula (incluye el ángulo vertical: es lo que
        // permite el impulso hacia arriba al disparar al piso).
        //
        // Sale del PlayerController y no de (mira - origen) a secas: la retícula sale de
        // la cámara y el tiro del arma, y de cerca esas dos líneas se abren lo bastante
        // como para que el disparo se vea torcido. Ver GetLaunchDirection.
        Vector3 dir = pc != null ? pc.GetLaunchDirection(origin, MinConvergeDistance)
                                 : OwnerASC.transform.forward;

        ResolveShot(origin, dir, pc, netAsc);

        // Los ecos (la Proyección arcana): el mismo tiro desde cada aliado proyectado.
        FireEchoes<GA_HitscanShot>((echo, from) => echo.ResolveEchoShot(from, pc, netAsc));

        if (pc != null) pc.PlayAnimation(this);

        // Retroceso: impulso en la dirección OPUESTA al disparo. faceVelocity=false
        // para que el cuerpo siga mirando a donde disparó mientras sale despedido.
        bool recoiled = false;
        if (RecoilSpeed > 0f)
        {
            Vector3 recoilVelocity = -dir * RecoilSpeed;
            if (netAsc != null)
            {
                netAsc.ServerStartDash(recoilVelocity, RecoilDuration, ExcludePlayerLayer.value, false,
                                       RecoilExitSpeedPercent, RecoilExitDamping);
                recoiled = true;
            }
            else if (pc != null)
            {
                pc.ApplyDashVelocity(recoilVelocity, false); // fallback sin red
            }
        }

        // Con retroceso en red NO llamamos EndAbility: el fin del impulso (restaurar
        // colisión y liberar "atacando") lo maneja el dueño en DashRoutine, igual que
        // en GA_Dash. Sin retroceso, cerramos acá.
        if (!recoiled) EndAbility();
    }

    // La copia de un eco: el mismo tiro desde el pecho de 'from' hacia donde apunta el dueño.
    // Corre en la instancia del eco (IsEcho, con su DamageScale).
    private void ResolveEchoShot(AbilitySystemComponent from, PlayerController pc,
                                 NetworkAbilitySystemComponent netAsc)
    {
        if (from == null) return;

        Vector3 origin = from.transform.position + Vector3.up * MuzzleHeight;
        Vector3 aim    = pc != null ? pc.GetAimPoint() : origin + OwnerASC.transform.forward * MaxRange;
        Vector3 dir    = aim - origin;
        if (dir.sqrMagnitude < 0.01f) dir = OwnerASC.transform.forward;

        ResolveShot(origin, dir.normalized, pc, netAsc);
    }

    // Traza el rayo: lo corta en la primera pared y le aplica los efectos al primer
    // enemigo que haya antes de ese punto.
    private void ResolveShot(Vector3 origin, Vector3 dir, PlayerController pc,
                             NetworkAbilitySystemComponent netAsc)
    {
        // 1) ¿Hasta dónde llega? La pared más cercana recorta el alcance.
        float distance = MaxRange;
        Vector3 impactPoint = origin + dir * MaxRange;

        if (Physics.SphereCast(origin, Mathf.Max(0.01f, BulletRadius), dir, out RaycastHit wallHit,
                               MaxRange, WallLayer, QueryTriggerInteraction.Ignore))
        {
            distance    = wallHit.distance;
            impactPoint = wallHit.point;
        }

        // 1b) Algo propio de la habilidad que también lo frena antes (el tornado de la Tormenta
        // eléctrica frena la Descarga del Mago).
        float stop = StopDistance(origin, dir, distance);
        if (stop < distance)
        {
            distance    = Mathf.Max(0f, stop);
            impactPoint = origin + dir * distance;
        }

        // 2) Primer enemigo dentro de ese tramo. SphereCastAll no viene ordenado, así
        // que buscamos el de menor distancia a mano.
        RaycastHit[] hits = Physics.SphereCastAll(origin, Mathf.Max(0.01f, BulletRadius), dir,
                                                  distance, TargetLayer, QueryTriggerInteraction.Collide);

        AbilitySystemComponent victim = null;
        float bestDist = float.MaxValue;

        foreach (RaycastHit h in hits)
        {
            AbilitySystemComponent asc = h.collider.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || ReferenceEquals(asc, OwnerASC) || !IsEnemy(asc)) continue;
            if (asc.HasTag(EGameplayTag.State_Dead)) continue;

            // distance 0 = el collider ya solapaba el origen; lo tomamos como el más cercano.
            float d = h.distance <= 0f ? 0f : h.distance;
            if (d < bestDist)
            {
                bestDist    = d;
                victim      = asc;
                impactPoint = h.distance <= 0f ? asc.transform.position + Vector3.up : h.point;
            }
        }

        if (victim != null)
        {
            ApplyHitEffects(victim, firstHit: true);
            ChargeUltimate();
            // El sonido ya va con el impacto de abajo: acá solo los VFX de golpe.
            BroadcastHitVFX(victim, withSound: false);
        }

        // VFX del impacto (en la pared o en el enemigo) para todos los peers.
        BroadcastImpactVFX(impactPoint);

        OnShotResolved(origin, impactPoint, victim);
    }

    // Después de resolver el tiro: de dónde salió, dónde terminó (la pared, el enemigo o el
    // alcance máximo) y a quién le pegó (null = a nadie). La Descarga eléctrica del Mago dibuja
    // ahí su rayo y rebota.
    protected virtual void OnShotResolved(Vector3 origin, Vector3 impactPoint, AbilitySystemComponent victim) { }

    // Hasta dónde llega el tiro antes de que lo frene algo PROPIO de la habilidad, además de las
    // paredes (ya recortado a 'maxDistance'). Los enemigos que haya más allá no reciben el tiro.
    // La Descarga del Mago se frena en el tornado de una Tormenta eléctrica aliada.
    protected virtual float StopDistance(Vector3 origin, Vector3 dir, float maxDistance) => maxDistance;

    // Vista previa del disparo en el Editor.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Vector3 p0 = origin.position + Vector3.up * MuzzleHeight;
        Vector3 p1 = p0 + origin.forward * MaxRange;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawLine(p0, p1);
        if (BulletRadius > 0f) Gizmos.DrawWireSphere(p1, BulletRadius);
    }

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
    {
        if (origin == null) return;

        AbilityHandles.Height(this, "Muzzle Height", origin.position, ref MuzzleHeight, AbilityHandles.OffsetColor);
        Vector3 p0 = origin.position + Vector3.up * MuzzleHeight;
        AbilityHandles.Distance(this, "Max Range", p0, origin.forward, ref MaxRange, AbilityHandles.DistanceColor);
    }
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GameplayEffect DamageEffect;
    [SerializeField, HideInInspector] private List<GameplayEffect> AdditionalEffects;
    [SerializeField, HideInInspector] private GameObject HitVFX;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        UpgradeEffect(ref DamageEffect, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeEffects(AdditionalEffects, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        // Salía donde pegaba el tiro (pared o enemigo): es un VFX "en el impacto".
        UpgradeVisual(ref HitVFX, new AbilityVisual { When = EVisualWhen.OnImpact, DestroyTime = 1.5f }, ref changed);
    }
}
