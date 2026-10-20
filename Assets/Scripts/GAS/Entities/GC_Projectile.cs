using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;

// ============================================================
// GC_Projectile
//
// Proyectil físico en red: vuela con un Rigidbody (simulado en el
// servidor, cinemático en los clientes) y, al chocar, resuelve daño
// con autoridad de servidor. El modelo visual real (arma del que
// disparó) y el VFX de impacto se resuelven por separado en cada
// peer a partir de quién disparó — ver comentarios en cada sección.
// ============================================================
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class GC_Projectile : NetworkBehaviour
{
    private AbilitySystemComponent sourceASC; // Quién disparó (solo poblado en el servidor)
    // Cuánto vive un proyectil si la habilidad no dice otra cosa (su "Tiempo de vuelo" en 0).
    public const float DefaultLifeTime = 5f;
    private float lifeTime = DefaultLifeTime;
    private float ultChargeAmount = 0f;
    // Instancia de GA_ProjectileShoot que disparó. Lo que le hace el proyectil a quien
    // toca es SU lista de efectos (ApplyHitEffects: daño a enemigos, curación a aliados
    // si la trae, lo de primer golpe), y sus VFX de impacto se replican desde ella.
    private GameplayAbility sourceAbility;

    // El daño principal de la habilidad (con el que la barrera mide cuánto frenó). Devuelto
    // por un parry, su copia congelada (ver Reflect).
    private GameplayEffect PrimaryDamage => Frozen(sourceAbility != null ? sourceAbility.PrimaryDamageEffect : null);

    // Enemigos ya golpeados, para no repetir daño en el mismo frame si el
    // proyectil atraviesa varios colliders del mismo objetivo.
    private HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();

    // Lo mismo del lado aliado: un proyectil que cura y atraviesa no debe curar
    // varias veces al mismo aliado mientras lo recorre.
    private HashSet<AbilitySystemComponent> alliesHit = new HashSet<AbilitySystemComponent>();

    // Quién disparó, sincronizado a TODOS los peers. sourceASC (arriba) solo
    // se llena en el servidor porque Initialize() solo corre ahí — pero el
    // swap de visuales (cubo -> arma real) es puramente cosmético y tiene
    // que verse igual en cualquier cliente, no solo en el servidor. Un
    // NetworkObject sí lo sabe serializar FishNet de forma nativa (a
    // diferencia de un ScriptableObject o un GameObject local), así que cada
    // peer resuelve SU PROPIA copia local del arma del dueño a partir de esto.
    private readonly SyncVar<NetworkObject> _shooterNob = new SyncVar<NetworkObject>();
    private bool _weaponVisualsApplied = false;

    // Los Renderer que trae el prefab (su modelo por defecto). Se anotan en Awake para
    // poder apagarlos y volver a prenderlos sin tocar el arma clonada.
    private Renderer[] _defaultRenderers;

    [Header("Modelo")]
    [Tooltip("Lo que se ve volar es una copia del ARMA de la mano del que lo tiró (el hacha, la " +
             "daga, el kunai). Apagado = se ve el modelo propio de este prefab: la flecha del " +
             "Explorador, que no es el arco que tiene en la mano.")]
    [Label("Usar el arma del que lo tira")]
    public bool UseShooterWeapon = true;

    [Tooltip("La punta sigue la curva del vuelo: con gravedad, sube mirando arriba y cae de " +
             "punta (la flecha). Apagado = conserva el giro con el que salió (el hacha que gira).")]
    [Label("Apunta hacia donde vuela")]
    public bool AlignToVelocity;

    private Rigidbody _body;

    [Header("Choque")]
    [Tooltip("La caja que choca con paredes y escudos toma el tamaño del ARMA que se ve volar (el " +
             "hacha, la daga). Con la caja grande de antes, un tiro que pasaba cerca de una pared " +
             "'pegaba' en ella aunque el arma no la tocara. Solo con 'Usar el arma del que lo tira' " +
             "y un BoxCollider; con el modelo propio (la flecha) vale la caja del prefab.")]
    [Label("Caja al tamaño del arma")]
    public bool FitColliderToWeapon = true;

    [Tooltip("Lo más fina que puede quedar la caja en cada lado.")]
    [Label("Caja: grosor mínimo (m)")]
    public float MinColliderSize = 0.1f;

    [Tooltip("Con qué radio le pega a los PERSONAJES, aparte de la caja: puede ser más generoso " +
             "que el arma para que no cueste acertar (la caja solo decide contra paredes y " +
             "escudos). 0 = la misma caja.")]
    [Label("Radio contra personajes (m; 0 = la caja)")]
    public float CharacterHitRadius = 0.5f;

    [Header("Blur de giro (opcional)")]
    [Tooltip("Material translucido del efecto (el paquete Simple Spin Blur trae uno listo: " +
             "'Spin Blur Material').\n\nVACIO = sin blur; el proyectil vuela como siempre.\n\n" +
             "Con material puesto, cada malla del ARMA CLONADA recibe SimpleSpinBlur, que " +
             "dibuja copias fantasma entre los frames de rotacion. Por eso el efecto sale solo " +
             "cuando el proyectil gira de verdad (hacha con AddSpin) y no en un tiro recto.")]
    [Label("Blur: material")]
    public Material SpinBlurMaterial;

    [Range(1, 128)]
    [Tooltip("Cuantos frames de retraso cubre el blur. Mas alto = estela de giro mas larga.")]
    [Label("Blur: frames de estela")]
    public int SpinBlurShutterSpeed = 4;

    [Range(1, 50)]
    [Tooltip("Copias fantasma dibujadas entre cada par de rotaciones. Mas alto = mas suave y " +
             "mas caro.")]
    [Label("Blur: copias")]
    public int SpinBlurSamples = 4;

    [Range(-0.1f, 0.1f)]
    [Tooltip("Opacidad de las copias fantasma. El rango es chico a proposito: es el del paquete.")]
    [Label("Blur: opacidad (0 a 1)")]
    public float SpinBlurAlpha = 0f;

    [Tooltip("Por debajo de esta velocidad angular no se dibuja nada. Es lo que hace que el " +
             "blur aparezca solo mientras el arma gira y desaparezca sola si se frena.")]
    [Label("Blur: giro mínimo")]
    public float SpinBlurAngularCutoff = 5f;

    [Range(1, 8)]
    [Tooltip("A cuantas mallas del arma se les monta el blur, empezando por las MAS GRANDES.\n\n" +
             "Un arma esta hecha de varias piezas (el hacha tiene siete: hojas, mango y adornos) " +
             "y el blur en un adorno chiquito no se distingue, pero cuesta igual. Con 2 alcanza " +
             "para las hojas, que es lo unico que se nota girando.\n\n" +
             "El criterio es el VOLUMEN de la malla, no el nombre: sigue funcionando aunque " +
             "renombres las piezas.")]
    [Label("Blur: máximo de mallas")]
    public int SpinBlurMaxMeshes = 2;

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    // Vuela derecho (sin gravedad) 'seconds' y después cae. Lo pide GA_ProjectileShoot con su
    // Vuelo recto, en el servidor, justo después de darle la velocidad.
    public void HoldGravity(float seconds)
    {
        if (_body == null) _body = GetComponent<Rigidbody>();
        if (_body == null || !_body.useGravity) return;
        _body.useGravity = false;
        StartCoroutine(RestoreGravity(seconds));
    }

    private IEnumerator RestoreGravity(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (_body != null && !_body.isKinematic) _body.useGravity = true;
    }

    // Solo en el servidor: el choque (ver CHOQUE POR BARRIDO) y, la flecha, mirar hacia donde
    // va (el NetworkTransform lo lleva a todos).
    private void FixedUpdate()
    {
        if (!IsServerInitialized) return;

        Sweep();

        if (!AlignToVelocity) return;
        if (_body == null) _body = GetComponent<Rigidbody>();
        if (_body == null || _body.isKinematic) return;

        Vector3 v = _body.linearVelocity;
        if (v.sqrMagnitude > 0.25f) _body.MoveRotation(Quaternion.LookRotation(v));
    }

    // Apaga el placeholder visual (el cubo del prefab) apenas el objeto
    // existe.
    private void Awake()
    {
        // Se anotan los renderers PROPIOS del prefab antes de tocar nada: son los que
        // se apagan y se vuelven a prender. Guardarlos evita que, más adelante, un
        // "prender todo" también encienda el arma clonada y se vean las dos cosas.
        _defaultRenderers = GetComponentsInChildren<Renderer>(true);

        // Con modelo propio (la flecha) no hay nada que reemplazar: se ve tal cual viene.
        if (!UseShooterWeapon)
        {
            _weaponVisualsApplied = true;
            return;
        }

        // Apagamos el modelo propio ACÁ y no en OnStartClient(). Awake() corre síncrono
        // como parte del propio Instantiate(), antes de que el objeto se dibuje por
        // primera vez. OnStartClient() en cambio es un callback de FishNet que puede
        // disparar uno o más frames después — en el servidor/host el objeto ya existe
        // (y se renderiza) desde el Instantiate(), así que si esperáramos a
        // OnStartClient() para ocultarlo, el host llegaría a ver el modelo por defecto
        // durante esos frames antes del swap al arma.
        SetDefaultVisuals(false);

        // ...pero apagarlo es una APUESTA a que va a venir un arma a reemplazarlo, y esa
        // apuesta solo la gana un jugador con arma equipada. Un NPC no tiene
        // PlayerController ni arma, así que su proyectil quedaba apagado para siempre:
        // volaba INVISIBLE, hacía daño, explotaba, y no se veía nada.
        //
        // Este temporizador es la red de seguridad: si en un par de frames nadie aplicó
        // un arma, se vuelve a mostrar el modelo del prefab. Cubre al NPC, al jugador
        // desarmado y a cualquier proyectil que se spawnee sin dueño.
        Invoke(nameof(ShowDefaultVisualsIfNoSwap), 0.2f);
    }

    // En los clientes, apaga la física local (el servidor es quien
    // simula el vuelo real) y se suscribe/aplica el swap de arma según
    // quién disparó.
    public override void OnStartClient()
    {
        base.OnStartClient();

        // Este proyectil no tiene dueño (se spawnea sin conexión asociada),
        // así que el servidor es quien simula su física — la velocidad se le
        // pone en Initialize()/SpawnProjectile() del lado servidor. Si el
        // Rigidbody sigue no-kinemático en los clientes, cae por gravedad
        // local y pelea contra el NetworkTransform (mismo problema que
        // tuvimos con el jugador). En clientes lo apagamos y dejamos que
        // NetworkTransform mueva el objeto.
        if (!IsServerInitialized)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity  = false;
            }
        }

        _shooterNob.OnChange += OnShooterChanged;
        if (_shooterNob.Value != null) TryApplyShooterWeaponVisuals(_shooterNob.Value);
    }

    // La llama GA_ProjectileShoot (siempre en el servidor) justo después
    // de spawnear el proyectil, con todos los datos que necesita para
    // resolver el impacto y publicar quién disparó.
    // Nota: la velocidad NO se pasa acá — la setea GA_ProjectileShoot.SpawnProjectile
    // directo sobre el Rigidbody. Antes había un parámetro 'speed' que no se usaba.
    // Los efectos ya no se pasan sueltos: los lee de 'ability' al chocar. 'lifeTimeSeconds'
    // es opcional: sin él vive 5 segundos.
    public void Initialize(AbilitySystemComponent source, float ultCharge, GameplayAbility ability, float lifeTimeSeconds = 0f)
    {
        sourceASC            = source;
        ultChargeAmount      = ultCharge;
        sourceAbility        = ability;

        // Alcance del proyectil expresado como tiempo de vuelo: con velocidad
        // constante, "vive 2s" es "llega hasta 2s × velocidad". 0 = dejar el default.
        if (lifeTimeSeconds > 0f) lifeTime = lifeTimeSeconds;

        // Publicamos quién disparó para que cada cliente pueda resolver su
        // propia arma local (ver comentario en _shooterNob más arriba).
        if (source != null)
            _shooterNob.Value = source.GetComponent<NetworkObject>();

        // Solo el servidor decide cuándo se destruye/despawnea el proyectil.
        if (IsServerInitialized)
        {
            // El arma también en el servidor, YA: la caja que choca toma su tamaño (ver
            // FitColliderToModel). En el host ya venía por el SyncVar; en un servidor sin
            // pantalla no llegaría nunca.
            if (_shooterNob.Value != null) TryApplyShooterWeaponVisuals(_shooterNob.Value);

            StartSweep(source);
            StartCoroutine(DespawnAfterLifetime());
        }
    }

    // Despawnea el proyectil solo tras lifeTime segundos, si nadie lo
    // destruyó antes por un impacto.
    private IEnumerator DespawnAfterLifetime()
    {
        yield return new WaitForSeconds(lifeTime);
        DespawnSelf();
    }

    // =========================================================
    // CHOQUE POR BARRIDO — autoridad de servidor (19 de octubre de 2026)
    //
    // Antes el choque era OnTriggerEnter: el collider del proyectil tenía que TOCAR algo en
    // un paso de física. Eso tenía dos problemas:
    //   · A velocidad alta salta lo que hay en el medio: a 100 m/s (el Haz guía) avanza 2 m
    //     por paso, y una pared o un escudo más finos que eso se los podía saltar.
    //   · La caja tenía que ser grande (1 m) para acertarle a la gente, y con esa caja un
    //     tiro que pasaba cerca de una pared "pegaba" en ella (pedido de Gustavo).
    // Ahora, en cada paso, el servidor BARRE el tramo que recorrió el proyectil: su forma
    // (la caja, del tamaño del arma; ver FitColliderToModel) contra paredes, escudos y
    // gente, y además una esfera de "Radio contra personajes" solo contra personajes. Lo
    // que encuentra lo procesa en ORDEN de distancia, así un escudo frena antes que el que
    // está detrás. Cada collider cuenta una vez (como un OnTriggerEnter). El primer tramo
    // sale del cuerpo del que lo tira: tirar pegado a una pared pega en la pared, no la cruza.
    // =========================================================

    private Collider _shape;
    private int      _mask;
    private Vector3  _lastCenter;
    private bool     _sweeping;
    private bool     _gone;          // ya chocó con algo que lo despawneó
    private int      _trajectory;    // sube en cada Reflect: lo del tramo viejo ya no cuenta
    private readonly HashSet<Collider> _touched = new HashSet<Collider>();
    private readonly HashSet<AbilitySystemComponent> _impactShown = new HashSet<AbilitySystemComponent>();

    private static readonly RaycastHit[] s_hits     = new RaycastHit[32];
    private static readonly Collider[]   s_overlaps = new Collider[32];
    private static readonly List<KeyValuePair<float, Collider>> s_found = new List<KeyValuePair<float, Collider>>();

    // Desde el cuerpo del que lo tira (a la altura a la que sale) hasta donde aparece.
    private void StartSweep(AbilitySystemComponent shooter)
    {
        _shape = GetComponent<Collider>();
        // Las mismas capas con las que chocaba antes (la matriz de colisiones de Physics).
        _mask  = CollisionMask(gameObject.layer);
        Vector3 center = ShapeCenter();
        _lastCenter = shooter != null
            ? new Vector3(shooter.transform.position.x, center.y, shooter.transform.position.z)
            : center;
        _sweeping = true;
    }

    private Vector3 ShapeCenter()
    {
        if (_shape is BoxCollider box)       return transform.TransformPoint(box.center);
        if (_shape is SphereCollider sphere) return transform.TransformPoint(sphere.center);
        return transform.position;
    }

    private void Sweep()
    {
        if (!_sweeping || _gone || _shape == null) return;

        Vector3 from = _lastCenter;
        Vector3 to   = ShapeCenter();
        _lastCenter  = to;

        Vector3 delta = to - from;
        float   dist  = delta.magnitude;
        Vector3 dir   = dist > 0.0001f ? delta / dist : transform.forward;

        int mask = _mask;

        s_found.Clear();
        CollectShape(from, dir, dist, mask);
        if (CharacterHitRadius > 0f) CollectSphere(from, dir, dist, CharacterHitRadius, mask, charactersOnly: true);
        s_found.Sort((a, b) => a.Key.CompareTo(b.Key));

        int trajectory = _trajectory;
        foreach (KeyValuePair<float, Collider> found in s_found)
        {
            if (_gone || trajectory != _trajectory) break;   // chocó, o un parry lo dio vuelta
            if (found.Value == null || !_touched.Add(found.Value)) continue;
            HandleHit(found.Value, from + dir * found.Key);
        }
    }

    // La forma del proyectil (su caja o su esfera), contra todo: paredes, escudos y gente.
    private void CollectShape(Vector3 from, Vector3 dir, float dist, int mask)
    {
        Vector3 scale = transform.lossyScale;
        if (_shape is BoxCollider box)
        {
            Vector3 half = new Vector3(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y),
                                       Mathf.Abs(box.size.z * scale.z)) * 0.5f;
            int n = Physics.OverlapBoxNonAlloc(from, half, s_overlaps, transform.rotation, mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++) AddFound(0f, s_overlaps[i], false);
            if (dist <= 0f) return;
            n = Physics.BoxCastNonAlloc(from, half, dir, s_hits, transform.rotation, dist, mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++) AddFound(s_hits[i].distance, s_hits[i].collider, false);
            return;
        }

        float radius = _shape is SphereCollider sphere
            ? sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))
            : Mathf.Min(_shape.bounds.extents.x, _shape.bounds.extents.y, _shape.bounds.extents.z);
        CollectSphere(from, dir, dist, radius, mask, charactersOnly: false);
    }

    private void CollectSphere(Vector3 from, Vector3 dir, float dist, float radius, int mask, bool charactersOnly)
    {
        QueryTriggerInteraction triggers = charactersOnly ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide;
        int n = Physics.OverlapSphereNonAlloc(from, radius, s_overlaps, mask, triggers);
        for (int i = 0; i < n; i++) AddFound(0f, s_overlaps[i], charactersOnly);
        if (dist <= 0f) return;
        n = Physics.SphereCastNonAlloc(from, radius, dir, s_hits, dist, mask, triggers);
        for (int i = 0; i < n; i++) AddFound(s_hits[i].distance, s_hits[i].collider, charactersOnly);
    }

    // Las capas que chocan con 'layer' según la matriz de Project Settings ▸ Physics.
    private static int CollisionMask(int layer)
    {
        int mask = 0;
        for (int i = 0; i < 32; i++)
            if (!Physics.GetIgnoreLayerCollision(layer, i)) mask |= 1 << i;
        return mask;
    }

    private void AddFound(float distance, Collider col, bool charactersOnly)
    {
        if (col == null || col.transform.IsChildOf(transform)) return;   // el propio proyectil
        if (charactersOnly && col.GetComponentInParent<AbilitySystemComponent>() == null) return;
        s_found.Add(new KeyValuePair<float, Collider>(distance, col));
    }

    // =========================================================
    // IMPACTO — autoridad de servidor
    // =========================================================

    // Lo que pasa al encontrar algo en el barrido ('point': dónde estaba el proyectil al
    // tocarlo): el VFX de impacto en todos los peers y, si es un personaje enemigo, sus
    // efectos (un aliado se atraviesa); si es un obstáculo sólido, se despawnea.
    private void HandleHit(Collider other, Vector3 point)
    {
        // Se ignora TODO lo que cuelgue del lanzador, no solo el GameObject exacto de su
        // ASC: el arma que lleva en la mano tiene colliders propios y cuelga del hueso
        // del rig, así que un proyectil que sale cerca del cuerpo chocaba contra su
        // propia arma y se despawneaba al instante, sin llegar a volar.
        if (sourceASC != null && other.transform.root == sourceASC.transform.root) return;

        // BARRERA (escudo del Paladín y compañía). Va ANTES del descarte de triggers
        // de abajo a propósito: la barrera ES un trigger (para no empujar a nadie ni
        // pelearse con el CharacterController de su dueño), así que si la dejáramos
        // caer en ese return el proyectil la atravesaría como si no existiera.
        //
        // Solo para una barrera LEVANTADA y ENEMIGA: los proyectiles de tu propio
        // equipo la cruzan sin frenarse, como en cualquier hero shooter.
        Entity_ShieldBarrier barrier = other.GetComponent<Entity_ShieldBarrier>();
        if (barrier != null)
        {
            if (!barrier.IsRaised || !barrier.IsHostile(sourceASC)) return;

            // El dueño ESQUIVA proyectiles (el bloqueo del Shinobi): su barrera no los para,
            // lo atraviesan a él también (ver abajo) y siguen de largo.
            if (barrier.Owner != null && barrier.Owner.HasTag(EGameplayTag.Status_DodgeProjectiles)) return;

            // PARRY QUE DEVUELVE (Guardián): el proyectil no se para, cambia de dueño y sale
            // hacia donde apunta el dueño de la barrera. Una sola vez por proyectil.
            if (barrier.ReflectsProjectilesNow && !_reflected)
            {
                barrier.NotifyProjectileBlocked(PrimaryDamage, sourceASC, point);
                // Sale desde donde tocó el escudo: el barrido lo encuentra un paso tarde, y
                // para entonces el proyectil ya podía estar del otro lado.
                transform.position += point - ShapeCenter();
                Reflect(barrier.Owner, barrier.AimDirection);
                return;
            }

            // Le pasamos el efecto de daño (no un número) para que la barrera pueda
            // reportar cuánto daño evitó de verdad — ver NotifyProjectileBlocked.
            barrier.NotifyProjectileBlocked(PrimaryDamage, sourceASC, point);
            PlayImpactVFXEverywhere(point);
            DespawnSelf();
            return;
        }

        if (other.isTrigger) return;

        AbilitySystemComponent targetASC = other.GetComponentInParent<AbilitySystemComponent>();

        // ESQUIVA (Status_DodgeProjectiles, el bloqueo del Shinobi): un enemigo que esquiva no
        // recibe nada; el proyectil lo atraviesa como si no estuviera, sin impacto ni VFX.
        if (targetASC != null && sourceASC != null && targetASC.HasTag(EGameplayTag.Status_DodgeProjectiles) &&
            sourceASC.IsEnemyOf(targetASC))
            return;

        // 1. VFX de impacto, sea lo que sea lo que golpeamos (a un personaje, una vez aunque
        // tenga varios colliders).
        if (targetASC == null || _impactShown.Add(targetASC)) PlayImpactVFXEverywhere(point);

        // 2. ¿Es un personaje?
        if (targetASC != null && sourceAbility != null && sourceASC != null)
        {
            // SISTEMA DE EQUIPOS (AFILIACIÓN LÓGICA): neutral (equipo 0) cuenta como enemigo.
            bool isEnemy = sourceASC.IsEnemyOf(targetASC);

            // A) Es enemigo y no lo hemos golpeado antes
            if (isEnemy)
            {
                if (enemiesHit.Add(targetASC))
                {
                    // La lista de efectos de la habilidad, en su orden. El PRIMER enemigo de
                    // este proyectil recibe además los de "al primer golpe" (el aturdido del
                    // Clérigo del Orden). Devuelto por un parry: solo lo que va a los enemigos,
                    // con el daño que traía (sus copias congeladas, ver Reflect).
                    bool first = enemiesHit.Count == 1 && !_reflected;
                    sourceAbility.ApplyHitEffects(targetASC, first, sourceASC, _reflected ? _frozen : null,
                                                  onlyHostile: _reflected);

                    if (ultChargeAmount > 0)
                        sourceASC.ReduceCooldownByTag(EGameplayTag.Ability_Cooldown_Ultimate, ultChargeAmount);

                    // Los VFX "al golpear" sobre él (el sonido ya va con el impacto de arriba).
                    if (!_reflected) sourceAbility.BroadcastHitVFX(targetASC, withSound: false);
                }

                // "Se deshace en el primer enemigo" (GA_ProjectileShoot.StopAtFirstEnemy: los
                // Misiles arcanos del Mago): le pegó a uno y se va. Sin la casilla, lo atraviesa
                // y sigue (cada enemigo, una vez), como siempre.
                if (sourceAbility is GA_ProjectileShoot shot && shot.StopAtFirstEnemy)
                {
                    DespawnSelf();
                    return;
                }
            }
            // B) Es aliado
            else
            {
                // Nunca le hacemos daño, pero SÍ le aplicamos lo que la lista trae para
                // los aliados (Castigo divino del Paladín: la estela daña enemigos y cura
                // aliados a su paso). Sin nada para ellos, esto no hace nada, que es el
                // comportamiento de siempre. Devuelto por un parry, tampoco: curaría a los
                // del Guardián con la magia del Paladín.
                //
                // El proyectil lo atraviesa en los dos casos: no lo destruimos acá.
                if (!_reflected && sourceAbility.HasAllyHitEffects && alliesHit.Add(targetASC))
                {
                    sourceAbility.ApplyHitEffects(targetASC, false, sourceASC);
                    sourceAbility.BroadcastHitVFX(targetASC, withSound: false);
                }
            }
        }
        else
        {
            // 3. ¿Es una pared / entorno sólido?
            // Si llegamos aquí, no tiene ASC y no es trigger.
            DespawnSelf();
        }
    }

    // =========================================================
    // DEVUELTO POR UN PARRY (escudo del Guardián)
    // =========================================================

    // Las copias INSTANTÁNEAS se usan solo al chocar: se van con el proyectil. Las que
    // duran (la quemadura del Clérigo de la Luz) no: un efecto activo en alguien todavía
    // puede apuntar a ellas. Son chicas y se liberan al cambiar de escena.
    private void OnDestroy()
    {
        foreach (GameplayEffect copy in _frozen.Values)
            if (copy != null && copy.Duration <= 0f) Destroy(copy);
    }

    // True si un parry ya lo devolvió: no se vuelve a devolver, y lo del primer impacto
    // (el aturdido del Orden) era de su lanzador original, no viaja con él.
    private bool _reflected;

    // EL DAÑO ES EL QUE TRAÍA. Al devolverlo, cada efecto de la habilidad que escala con un
    // stat del que lo tiró se cambia por una copia CONGELADA con esos stats (original →
    // copia; ver GameplayEffect.CreateFrozenCopy). Con el efecto original, el daño se
    // recalcularía con los stats del Guardián: un rayo mágico devuelto no haría nada, y la
    // quemadura del Clérigo de la Luz tampoco. La copia conserva además el tipo: un daño
    // mágico devuelto sigue ignorando la armadura. Lo que no escala va tal cual (la
    // ralentización del hacha, las heridas).
    private readonly Dictionary<GameplayEffect, GameplayEffect> _frozen = new Dictionary<GameplayEffect, GameplayEffect>();

    private GameplayEffect Frozen(GameplayEffect effect)
        => effect != null && _frozen.TryGetValue(effect, out GameplayEffect copy) ? copy : effect;

    // Congela lo que el proyectil devuelto puede aplicar: lo de "al golpear" que va a los
    // enemigos (o a todos). Con los stats de 'shooter', el que lo tiró.
    private void FreezeEffects(AbilitySystemComponent shooter)
    {
        if (sourceAbility == null || shooter == null) return;

        List<GameplayEffect> hostile = new List<GameplayEffect>();
        sourceAbility.GetEffects(EEffectWhen.OnHit, EEffectTarget.Enemies, hostile);
        sourceAbility.GetEffects(EEffectWhen.OnHit, EEffectTarget.Everyone, hostile);

        foreach (GameplayEffect effect in hostile)
            if (effect.ScalesWithSource && !_frozen.ContainsKey(effect))
                _frozen[effect] = effect.CreateFrozenCopy(shooter);
    }

    // Cambia de dueño y de dirección, en el servidor (el vuelo lo simula él y el
    // NetworkTransform lo lleva a los demás). Conserva la velocidad y sus efectos: ahora
    // dañan al equipo del lanzador original, con el daño que traían (ver _frozen). Lo que
    // cura a aliados (la estela del Castigo divino) se descarta: curaría a los del
    // Guardián con la magia del Paladín.
    public void Reflect(AbilitySystemComponent newSource, Vector3 direction)
    {
        if (!IsServerInitialized || newSource == null) return;

        FreezeEffects(sourceASC);   // ANTES de cambiar de dueño: con los stats del que lo tiró

        _reflected      = true;
        sourceASC       = newSource;
        ultChargeAmount = 0f;
        enemiesHit.Clear();
        alliesHit.Clear();

        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : -transform.forward;

        Rigidbody rb = GetComponent<Rigidbody>();
        float speed = rb != null ? Mathf.Max(rb.linearVelocity.magnitude, 10f) : 10f;

        // Un poco hacia afuera, para que no siga adentro de la barrera que lo devolvió.
        transform.SetPositionAndRotation(transform.position + dir * 0.5f, Quaternion.LookRotation(dir));
        if (rb != null) rb.linearVelocity = dir * speed;

        // El barrido arranca de nuevo desde acá, sin recordar lo del tramo de ida: a la
        // vuelta puede pegarle al que lo tiró (o a uno de los suyos que ya atravesó).
        _trajectory++;
        _touched.Clear();
        _impactShown.Clear();
        _lastCenter = ShapeCenter();
    }

    // Reproduce el VFX de impacto en TODOS los peers. Un Instantiate() local acá
    // solo existiría en el proceso servidor (que es donde corre el choque), así
    // que reusamos el mismo mecanismo que las demás habilidades: el servidor lo
    // reproduce y le avisa a cada cliente que haga lo mismo con SU PROPIA copia de
    // esta habilidad, sin sincronizar el GameObject del VFX en sí.
    private void PlayImpactVFXEverywhere(Vector3 position)
    {
        if (sourceAbility == null || sourceASC == null || !sourceAbility.HasImpactFeedback) return;

        NetworkAbilitySystemComponent shooterNetAsc = sourceASC.GetComponent<NetworkAbilitySystemComponent>();
        if (shooterNetAsc != null)
            shooterNetAsc.ServerPlayAbilityVFX(sourceAbility, position);
        else
            sourceAbility.PlayImpactVFX(position); // fallback sin red
    }

    // Despawnea el proyectil en red (avisa a todos los clientes que
    // borren su copia) — un Destroy() normal solo lo sacaría del servidor.
    private void DespawnSelf()
    {
        _gone = true;
        if (IsServerInitialized && IsSpawned)
            ServerManager.Despawn(gameObject);
    }

    // =========================================================
    // VISUALES — el modelo del arma y el placeholder por defecto
    // =========================================================

    // Cuando llega (o cambia) el dueño sincronizado, intenta aplicar la
    // visual de su arma.
    private void OnShooterChanged(NetworkObject prev, NetworkObject next, bool asServer)
    {
        if (next != null) TryApplyShooterWeaponVisuals(next);
    }

    // Resuelve el PlayerController del dueño EN ESTE PROCESO y le pide su
    // arma actual (currentMainWeapon) — cada peer instancia esa arma
    // localmente como parte de EquipCharacterClass, así que no hace falta
    // sincronizar la malla en sí, solo QUIÉN es el dueño.
    private void TryApplyShooterWeaponVisuals(NetworkObject shooterNob)
    {
        if (_weaponVisualsApplied) return; // idempotente: OnChange + el chequeo en OnStartClient pueden pisarse

        PlayerController pc = shooterNob != null ? shooterNob.GetComponent<PlayerController>() : null;
        GameObject weapon   = pc != null ? pc.GetCurrentMainWeapon() : null;

        if (weapon == null)
        {
            // Quien disparó no tiene arma que clonar: un NPC (los magos fantasma, por
            // ejemplo) o un jugador sin arma equipada. En ese caso el proyectil se
            // muestra tal como viene el prefab — que para eso trae su propio modelo.
            SetDefaultVisuals(true);
            _weaponVisualsApplied = true;   // ya está resuelto: no hay que seguir intentando
            return;
        }

        OverrideVisuals(weapon);
        _weaponVisualsApplied = true;
    }

    // Red de seguridad del temporizador de Awake: si nadie resolvió los visuales, el
    // proyectil se muestra con su propio modelo en vez de quedar invisible.
    private void ShowDefaultVisualsIfNoSwap()
    {
        if (_weaponVisualsApplied) return;

        SetDefaultVisuals(true);
        _weaponVisualsApplied = true;
    }

    // Prende o apaga los Renderer PROPIOS del prefab (los que había al crearse), sin
    // importar si están en la raíz o en un hijo. No toca el arma clonada.
    private void SetDefaultVisuals(bool visible)
    {
        if (_defaultRenderers == null) return;

        foreach (Renderer r in _defaultRenderers)
            if (r != null) r.enabled = visible;
    }

    // Clona el modelo del arma real como hijo visual del proyectil,
    // ajusta su posición/rotación, y le quita los colliders (para que no
    // interfieran con el collider principal del proyectil).
    public void OverrideVisuals(GameObject weaponModel)
    {
        if (weaponModel == null) return;

        // El modelo propio del prefab ya se apagó en Awake — acá solo clonamos el arma
        // real. Si nunca llegamos hasta acá (un NPC, que no tiene arma), ese modelo se
        // vuelve a prender: ver TryApplyShooterWeaponVisuals.
        SetDefaultVisuals(false);

        GameObject weaponClone = Instantiate(weaponModel, transform);

        // El arma del jugador puede estar OCULTA justo ahora: las habilidades de
        // lanzamiento la apagan al soltar (HideWeaponWhileFlying), y este clon se arma
        // en CADA peer cuando le llega el SyncVar del tirador — que puede ser uno o
        // varios frames despues. Clonar un arma ya apagada dejaba el proyectil
        // invisible: no se veia ni el arma en mano ni el hacha volando.
        //
        // El clon siempre nace visible: su estado no tiene por que heredar el del
        // original, que a esta altura ya no representa nada.
        foreach (Renderer r in weaponClone.GetComponentsInChildren<Renderer>(true))
            if (r != null) r.enabled = true;

        weaponClone.transform.localPosition = Vector3.zero;
        weaponClone.transform.localRotation = Quaternion.Euler(0f,90f,0f); // O la rotación que necesites para que apunte bien

        var colliders = weaponClone.GetComponentsInChildren<Collider>();
        foreach (var col in colliders) Destroy(col);

        // La estela del arma NO se clona. Un TrailRenderer guarda sus puntos en espacio
        // de MUNDO, así que el clon nace con los puntos que la estela tenía en la mano
        // del jugador: mientras el proyectil vuela, esa estela dibuja una línea desde la
        // mano hasta el arma, y al impactar esa línea es lo último que queda en pantalla
        // — se ve como si el arma "volviera" desde el punto de impacto hasta la mano.
        //
        // Además la estela del arma la prenden y apagan los AnimationEvents del clip
        // (EnableTrail/DisableTrail), que solo existen en el dueño: el clon nunca
        // recibiría el apagado y seguiría emitiendo todo el vuelo.
        //
        // Si un proyectil necesita estela propia, va en SU prefab, no en la del arma.
        foreach (var trail in weaponClone.GetComponentsInChildren<TrailRenderer>(true))
            Destroy(trail);

        ApplySpinBlur(weaponClone);
        FitColliderToModel(weaponClone);
    }

    // La caja del proyectil, del tamaño del arma que se ve volar (pedido de Gustavo, 19 de
    // octubre de 2026): con la caja de 1 m de antes, tirar cerca de una pared pegaba en la
    // pared. Se mide con las mallas (no con los Renderer, que pueden estar apagados) en el
    // espacio del proyectil, así gira con él (el hacha que da vueltas). Corre en todos los
    // peers; el que choca es el servidor (ver Sweep). Acertarle a la gente no se achica: eso
    // lo decide aparte el "Radio contra personajes".
    private void FitColliderToModel(GameObject model)
    {
        if (!FitColliderToWeapon || !(GetComponent<Collider>() is BoxCollider box)) return;

        Matrix4x4 toLocal = transform.worldToLocalMatrix;
        Bounds bounds = default;
        bool any = false;

        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = null;
            if (r is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
            else if (r is MeshRenderer)
            {
                MeshFilter filter = r.GetComponent<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
            }
            if (mesh == null) continue;   // partículas, estelas: no son el arma

            Matrix4x4 m = toLocal * r.transform.localToWorldMatrix;
            Vector3 c = mesh.bounds.center, e = mesh.bounds.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x,
                                                 (i & 2) == 0 ? -e.y : e.y,
                                                 (i & 4) == 0 ? -e.z : e.z);
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                else bounds.Encapsulate(p);
            }
        }
        if (!any) return;

        float min = Mathf.Max(0.01f, MinColliderSize);
        box.center = bounds.center;
        box.size   = new Vector3(Mathf.Max(min, bounds.size.x), Mathf.Max(min, bounds.size.y),
                                 Mathf.Max(min, bounds.size.z));
    }

    // Le monta el blur de giro (paquete Simple Spin Blur) a cada malla del arma
    // clonada. El efecto dibuja copias fantasma del mesh entre los frames de rotacion,
    // asi que aparece SOLO cuando el proyectil gira de verdad: un hacha lanzada con
    // AddSpin lo muestra, un proyectil recto que no rota no dibuja nada gracias al
    // corte por velocidad angular.
    //
    // Sin material asignado no se hace nada y el proyectil vuela como siempre.
    private void ApplySpinBlur(GameObject weaponClone)
    {
        if (SpinBlurMaterial == null) return;

        // Solo las mallas mas GRANDES reciben el blur — en un arma, las hojas. El mango
        // y los adornos son piezas chicas cuyo blur no se distingue y que multiplican el
        // costo: el hacha sola tiene siete mallas.
        //
        // Se ordena por VOLUMEN de bounds y NO por nombre a proposito. Un filtro por
        // "blade" moriria en la primera espada que se llame distinto, y en este proyecto
        // los assets se renombran seguido.
        List<MeshFilter> meshes = new List<MeshFilter>();
        foreach (MeshFilter mf in weaponClone.GetComponentsInChildren<MeshFilter>(true))
        {
            // El Start() del paquete hace GetComponent<MeshFilter>().mesh sin chequear:
            // una malla vacia lo tira abajo.
            if (mf.sharedMesh != null) meshes.Add(mf);
        }

        meshes.Sort((a, b) => BoundsVolume(b.sharedMesh).CompareTo(BoundsVolume(a.sharedMesh)));

        int count = Mathf.Min(SpinBlurMaxMeshes, meshes.Count);
        for (int i = 0; i < count; i++)
        {
            SimpleSpinBlur blur = meshes[i].gameObject.AddComponent<SimpleSpinBlur>();
            blur.SSB_Material = SpinBlurMaterial;
            blur.shutterSpeed = SpinBlurShutterSpeed;
            blur.Samples      = SpinBlurSamples;
            blur.alphaOffset  = SpinBlurAlpha;

            // advancedSettings se arma a mano a proposito: AddComponent no pasa por el
            // deserializador de Unity, asi que el campo llega en NULL — y el Start() del
            // paquete lo desreferencia sin chequear. Sin esto, excepcion en cada tiro.
            blur.advancedSettings = new AdvancedSettings
            {
                AngularVelocityCutoff = SpinBlurAngularCutoff,
                enableGPUInstancing   = true,
                subMaterialIndex      = 0,
                unitLocalScale        = false
            };
        }
    }

    // Volumen de la caja envolvente de una malla. Sirve para separar las piezas con
    // presencia visual (hojas, cabezas de maza) de los adornos, sin depender de como
    // se llamen los objetos.
    private static float BoundsVolume(Mesh mesh)
    {
        Vector3 size = mesh.bounds.size;
        return size.x * size.y * size.z;
    }
}
