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
    private float lifeTime = 5f;
    private float ultChargeAmount = 0f;
    // Instancia de GA_ProjectileShoot que disparó. Lo que le hace el proyectil a quien
    // toca es SU lista de efectos (ApplyHitEffects: daño a enemigos, curación a aliados
    // si la trae, lo de primer golpe), y sus VFX de impacto se replican desde ella.
    private GameplayAbility sourceAbility;

    // El daño principal, pisado por el que traía un proyectil devuelto por un parry (ver
    // Reflect). null = el de la habilidad.
    private GameplayEffect PrimaryDamage => _carriedDamage != null ? _carriedDamage
                                          : sourceAbility != null ? sourceAbility.PrimaryDamageEffect : null;

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

    [Header("Blur de giro (opcional)")]
    [Tooltip("Material translucido del efecto (el paquete Simple Spin Blur trae uno listo: " +
             "'Spin Blur Material').\n\nVACIO = sin blur; el proyectil vuela como siempre.\n\n" +
             "Con material puesto, cada malla del ARMA CLONADA recibe SimpleSpinBlur, que " +
             "dibuja copias fantasma entre los frames de rotacion. Por eso el efecto sale solo " +
             "cuando el proyectil gira de verdad (hacha con AddSpin) y no en un tiro recto.")]
    public Material SpinBlurMaterial;

    [Range(1, 128)]
    [Tooltip("Cuantos frames de retraso cubre el blur. Mas alto = estela de giro mas larga.")]
    public int SpinBlurShutterSpeed = 4;

    [Range(1, 50)]
    [Tooltip("Copias fantasma dibujadas entre cada par de rotaciones. Mas alto = mas suave y " +
             "mas caro.")]
    public int SpinBlurSamples = 4;

    [Range(-0.1f, 0.1f)]
    [Tooltip("Opacidad de las copias fantasma. El rango es chico a proposito: es el del paquete.")]
    public float SpinBlurAlpha = 0f;

    [Tooltip("Por debajo de esta velocidad angular no se dibuja nada. Es lo que hace que el " +
             "blur aparezca solo mientras el arma gira y desaparezca sola si se frena.")]
    public float SpinBlurAngularCutoff = 5f;

    [Range(1, 8)]
    [Tooltip("A cuantas mallas del arma se les monta el blur, empezando por las MAS GRANDES.\n\n" +
             "Un arma esta hecha de varias piezas (el hacha tiene siete: hojas, mango y adornos) " +
             "y el blur en un adorno chiquito no se distingue, pero cuesta igual. Con 2 alcanza " +
             "para las hojas, que es lo unico que se nota girando.\n\n" +
             "El criterio es el VOLUMEN de la malla, no el nombre: sigue funcionando aunque " +
             "renombres las piezas.")]
    public int SpinBlurMaxMeshes = 2;

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    // Apaga el placeholder visual (el cubo del prefab) apenas el objeto
    // existe.
    private void Awake()
    {
        // Se anotan los renderers PROPIOS del prefab antes de tocar nada: son los que
        // se apagan y se vuelven a prender. Guardarlos evita que, más adelante, un
        // "prender todo" también encienda el arma clonada y se vean las dos cosas.
        _defaultRenderers = GetComponentsInChildren<Renderer>(true);

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
            StartCoroutine(DespawnAfterLifetime());
    }

    // Despawnea el proyectil solo tras lifeTime segundos, si nadie lo
    // destruyó antes por un impacto.
    private IEnumerator DespawnAfterLifetime()
    {
        yield return new WaitForSeconds(lifeTime);
        DespawnSelf();
    }

    // =========================================================
    // IMPACTO — autoridad de servidor
    // =========================================================

    // Al chocar: reproduce el VFX de impacto en todos los peers y, si es
    // un personaje enemigo, le aplica daño (o lo atraviesa si es aliado);
    // si es un obstáculo sólido, despawnea el proyectil. Usa OnTriggerEnter
    // porque el collider está marcado "Is Trigger".
    private void OnTriggerEnter(Collider other)
    {
        // TODA la lógica de daño/impacto es autoridad del servidor. Sin este
        // guard, la copia del proyectil en un cliente remoto también corre
        // esto — con sourceASC/damageEffect en null porque Initialize() solo
        // se llamó del lado servidor — y explota con NullReferenceException.
        if (!IsServerInitialized) return;

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

            // PARRY QUE DEVUELVE (Guardián): el proyectil no se para, cambia de dueño y sale
            // hacia donde apunta el dueño de la barrera. Una sola vez por proyectil.
            if (barrier.ReflectsProjectilesNow && !_reflected)
            {
                float carried = barrier.NotifyProjectileBlocked(PrimaryDamage, sourceASC, transform.position);
                Reflect(barrier.Owner, barrier.AimDirection, carried);
                return;
            }

            // Le pasamos el efecto de daño (no un número) para que la barrera pueda
            // reportar cuánto daño evitó de verdad — ver NotifyProjectileBlocked.
            barrier.NotifyProjectileBlocked(PrimaryDamage, sourceASC, transform.position);
            PlayImpactVFXEverywhere();
            DespawnSelf();
            return;
        }

        if (other.isTrigger) return;

        AbilitySystemComponent targetASC = other.GetComponentInParent<AbilitySystemComponent>();

        // 1. VFX de impacto, sea lo que sea lo que golpeamos.
        PlayImpactVFXEverywhere();

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
                    // Clérigo del Orden). Devuelto por un parry: solo lo que daña, con el
                    // daño que traía (ver Reflect).
                    bool first = enemiesHit.Count == 1 && !_reflected;
                    sourceAbility.ApplyHitEffects(targetASC, first, sourceASC, _carriedDamage, onlyHostile: _reflected);

                    if (ultChargeAmount > 0)
                        sourceASC.ReduceCooldownByTag(EGameplayTag.Ability_Cooldown_Ultimate, ultChargeAmount);

                    // Los VFX "al golpear" sobre él (el sonido ya va con el impacto de arriba).
                    if (!_reflected) sourceAbility.BroadcastHitVFX(targetASC, withSound: false);
                }

                // NOTA: Si quieres que el proyectil se DESTRUYA al golpear a un enemigo
                // en lugar de atravesarlo, descomenta la siguiente línea:
                // DespawnSelf();
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

    private void OnDestroy()
    {
        if (_carriedDamage != null) Destroy(_carriedDamage);
    }

    // True si un parry ya lo devolvió: no se vuelve a devolver, y lo del primer impacto
    // (el aturdido del Orden) era de su lanzador original, no viaja con él.
    private bool _reflected;

    // Cambia de dueño y de dirección, en el servidor (el vuelo lo simula él y el
    // NetworkTransform lo lleva a los demás). Conserva la velocidad y sus efectos extra:
    // ahora dañan al equipo del lanzador original. Lo que cura a aliados (la estela del
    // Castigo divino) se descarta: curaría a los del Guardián con la magia del Paladín.
    //
    // EL DAÑO ES EL QUE TRAÍA ('carriedDamage', calculado con los stats del que lo tiró):
    // con el efecto original, el daño se recalcularía con los stats del nuevo dueño, y un
    // rayo mágico devuelto por un Guardián (sin daño mágico) haría 1.
    private GameplayEffect _carriedDamage;

    public void Reflect(AbilitySystemComponent newSource, Vector3 direction, float carriedDamage)
    {
        if (!IsServerInitialized || newSource == null) return;

        _reflected      = true;
        sourceASC       = newSource;
        ultChargeAmount = 0f;
        enemiesHit.Clear();
        alliesHit.Clear();

        if (carriedDamage > 0f)
        {
            _carriedDamage = ScriptableObject.CreateInstance<GameplayEffect>();
            _carriedDamage.name       = "GE_ReflectedProjectile(runtime)";
            _carriedDamage.Duration   = 0f;
            _carriedDamage.EffectType = GameplayEffect.EEffectType.Hidden;
            _carriedDamage.Modifiers  = new List<Modifier>
            {
                new Modifier { Attribute = EAttributeType.Health, Type = Modifier.EModificationType.Add,
                               Magnitude = -carriedDamage },
            };
        }

        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : -transform.forward;

        Rigidbody rb = GetComponent<Rigidbody>();
        float speed = rb != null ? Mathf.Max(rb.linearVelocity.magnitude, 10f) : 10f;

        // Un poco hacia afuera, para que no siga adentro de la barrera que lo devolvió.
        transform.SetPositionAndRotation(transform.position + dir * 0.5f, Quaternion.LookRotation(dir));
        if (rb != null) rb.linearVelocity = dir * speed;
    }

    // Reproduce el VFX de impacto en TODOS los peers. Un Instantiate() local acá
    // solo existiría en el proceso servidor (que es donde corre OnTriggerEnter), así
    // que reusamos el mismo mecanismo que las demás habilidades: el servidor lo
    // reproduce y le avisa a cada cliente que haga lo mismo con SU PROPIA copia de
    // esta habilidad, sin sincronizar el GameObject del VFX en sí.
    private void PlayImpactVFXEverywhere()
    {
        if (sourceAbility == null || sourceASC == null || !sourceAbility.HasImpactFeedback) return;

        NetworkAbilitySystemComponent shooterNetAsc = sourceASC.GetComponent<NetworkAbilitySystemComponent>();
        if (shooterNetAsc != null)
            shooterNetAsc.ServerPlayAbilityVFX(sourceAbility, transform.position);
        else
            sourceAbility.PlayImpactVFX(transform.position); // fallback sin red
    }

    // Despawnea el proyectil en red (avisa a todos los clientes que
    // borren su copia) — un Destroy() normal solo lo sacaría del servidor.
    private void DespawnSelf()
    {
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
