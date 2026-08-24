using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

// ============================================================
// MercObjective
//
// El "Objetivo" del modo Mercenarios (la Bolsa de oro; por ahora una caja). Es lo
// único que hay que llevarse: quien lo deja en el punto de entrega de SU base le da
// un punto a su equipo, y con dos puntos se gana la partida.
//
// Reglas que implementa:
//   · Se levanta con solo acercarse (no hay tecla de recoger).
//   · Quien lo carga se mueve más lento y NO puede usar su definitiva: ese botón pasa
//     a SOLTAR el Objetivo (ver PlayerController.HandleAbilityInput, que consulta el
//     tag Status_Carrying_Objective).
//   · Si el portador muere, el Objetivo se le cae ahí mismo.
//   · Recién puede volver a levantarse pasados unos segundos de haber caído — así el
//     que lo suelta no lo vuelve a agarrar en el mismo frame.
//
// RED: el servidor manda la verdad y los clientes solo dibujan.
//   · Cuando está en el piso, la posición viaja en un SyncVar (se mueve muy poco).
//   · Cuando lo llevan, viaja el ObjectId del portador y cada cliente lo pega a ese
//     personaje LOCALMENTE. Esto es a propósito: seguir al portador en la máquina de
//     cada uno se ve perfectamente suave (usa la posición ya interpolada de ese
//     jugador), mientras que sincronizar la posición de la bolsa frame a frame se
//     vería a los tirones y gastaría red al pedo.
// ============================================================
public class MercObjective : NetworkBehaviour
{
    // Instancia viva en este proceso (hay como mucho una). La usa el marcador del HUD.
    public static MercObjective Instance { get; private set; }

    [Header("Recolección")]
    [Tooltip("A qué distancia se levanta el Objetivo con solo acercarse.")]
    public float PickupRadius = 2.2f;

    [Tooltip("Segundos que queda bloqueado tras caer al piso, antes de poder levantarse otra vez.")]
    public float PickupLockSeconds = 3f;

    [Tooltip("Capa de los personajes (en este proyecto, 'Character' = 7).")]
    public LayerMask CharacterLayer = 1 << 7;

    [Header("Cómo lo carga el portador")]
    [Tooltip("Dónde se ve la bolsa respecto del portador (espacio local del personaje).")]
    public Vector3 CarryOffset = new Vector3(0f, 2.1f, -0.35f);

    [Tooltip("Cuánta velocidad de movimiento pierde el que lo lleva. 0.25 = un 25% más lento.")]
    [Range(0f, 0.9f)] public float CarrySlowPercent = 0.25f;

    [Tooltip("Opcional: efecto propio para el portador. Si lo dejás vacío se arma uno en código " +
             "con la ralentización de arriba y el tag Status_Carrying_Objective.")]
    public GameplayEffect CarryEffect;

    [Header("Presentación")]
    [Tooltip("Altura del flotado cuando está en el piso.")]
    public float BobHeight = 0.25f;
    public float BobSpeed  = 2f;
    public float SpinSpeed = 45f;

    // =========================================================
    // ESTADO SINCRONIZADO
    // =========================================================

    // ObjectId del NetworkObject que lo lleva. -1 = está en el piso.
    private readonly SyncVar<int> _netCarrierId = new SyncVar<int>(-1);

    // Equipo del portador (0 = nadie). Se sincroniza aparte para que el HUD pueda
    // pintar el marcador del color del equipo sin tener que resolver el personaje.
    private readonly SyncVar<int> _netCarrierTeam = new SyncVar<int>(0);

    // Dónde está apoyado cuando nadie lo lleva.
    private readonly SyncVar<Vector3> _netGroundPos = new SyncVar<Vector3>(Vector3.zero);

    // =========================================================
    // ESTADO SOLO-SERVIDOR
    // =========================================================

    private MercenariesGameMode _gm;
    private AbilitySystemComponent _carrier;
    private float _pickupUnlockTime;
    private float _tickTimer;
    private Vector3 _lastCarrierPos;
    private GameplayEffect _runtimeCarryEffect;

    // Resolución del portador en el cliente (cacheada por ObjectId).
    private Transform _carrierTransformCache;
    private int _carrierTransformCacheId = -1;

    // =========================================================
    // LECTURA PÚBLICA
    // =========================================================

    public bool IsCarried      => _netCarrierId.Value >= 0;
    public int  CarrierTeam    => _netCarrierTeam.Value;

    // Posición para el marcador del HUD: la del portador si lo llevan, si no la del piso.
    public Vector3 WorldPosition
    {
        get
        {
            Transform carrier = ResolveCarrierTransform();
            return carrier != null ? carrier.position + Vector3.up * CarryOffset.y : _netGroundPos.Value;
        }
    }

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_runtimeCarryEffect != null) Destroy(_runtimeCarryEffect);
    }

    // La llama el game mode justo después de spawnearlo.
    [Server]
    public void ServerInitialize(MercenariesGameMode gm, Vector3 position)
    {
        _gm = gm;
        ServerPlaceOnGround(position, lockSeconds: 0f);
    }

    private void Update()
    {
        if (IsServerInitialized) ServerTick();
        UpdateVisual();
    }

    // =========================================================
    // SERVIDOR
    // =========================================================

    [Server]
    private void ServerTick()
    {
        _tickTimer += Time.deltaTime;
        if (_tickTimer < 0.15f) return;
        _tickTimer = 0f;

        if (_gm == null) _gm = MercenariesGameMode.Instance;

        if (_carrier != null) TickCarried();
        else                  TickOnGround();
    }

    [Server]
    private void TickCarried()
    {
        // El portador dejó de existir o cayó: la bolsa se le cae donde estaba.
        if (_carrier == null || _carrier.HasTag(EGameplayTag.State_Dead))
        {
            // Si el portador se DESCONECTÓ, su transform ya no existe — por eso vamos
            // anotando su última posición conocida en cada tick: sin eso, la bolsa
            // reaparecería en el último lugar donde había tocado el piso, que puede
            // estar en la otra punta del mapa.
            Vector3 dropAt = _carrier != null ? _carrier.transform.position : _lastCarrierPos;
            int team = _netCarrierTeam.Value;
            ServerReleaseCarrier();
            ServerPlaceOnGround(dropAt, PickupLockSeconds);
            if (_gm != null) _gm.ServerNotifyObjectiveDropped(team);
            return;
        }

        _lastCarrierPos = _carrier.transform.position;

        // ¿Llegó a la entrega de SU equipo?
        int carrierTeam = _carrier.TeamID;
        MercTeamBase teamBase = _gm != null ? _gm.GetBase(carrierTeam) : null;
        if (teamBase != null && teamBase.IsInDeliveryZone(_carrier.transform.position))
        {
            ServerReleaseCarrier();
            _gm.ServerScoreObjective(carrierTeam);
        }
    }

    [Server]
    private void TickOnGround()
    {
        if (Time.time < _pickupUnlockTime) return;
        if (_gm != null && _gm.State != EMatchState.Playing) return;

        Collider[] hits = Physics.OverlapSphere(_netGroundPos.Value, PickupRadius,
                                                CharacterLayer, QueryTriggerInteraction.Collide);
        foreach (Collider col in hits)
        {
            if (col == null) continue;

            AbilitySystemComponent asc = col.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null) continue;
            if (asc.GetComponent<PlayerController>() == null) continue;   // los NPCs no lo levantan (todavía)
            if (asc.HasTag(EGameplayTag.State_Dead)) continue;
            if (!MercenariesGameMode.IsValidTeam(asc.TeamID)) continue;

            ServerAssignCarrier(asc);
            return;
        }
    }

    // Deja el Objetivo apoyado en el piso en 'position' (pegado al suelo con un
    // raycast, para que no quede flotando ni enterrado si lo soltaron en una rampa).
    [Server]
    private void ServerPlaceOnGround(Vector3 position, float lockSeconds)
    {
        Vector3 grounded = position;
        if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 8f,
                            ~0, QueryTriggerInteraction.Ignore))
            grounded = hit.point;

        grounded.y += 0.5f;

        _netGroundPos.Value = grounded;
        _pickupUnlockTime   = Time.time + lockSeconds;
        transform.position  = grounded;
    }

    [Server]
    private void ServerAssignCarrier(AbilitySystemComponent asc)
    {
        NetworkObject nob = asc.GetComponent<NetworkObject>();
        if (nob == null) return;

        _carrier = asc;
        _lastCarrierPos       = asc.transform.position;
        _netCarrierId.Value   = nob.ObjectId;
        _netCarrierTeam.Value = asc.TeamID;

        asc.ApplyGameplayEffect(ResolveCarryEffect(), asc);

        if (_gm != null) _gm.ServerNotifyObjectiveTaken(asc.TeamID);
    }

    // Saca el Objetivo de las manos de quien lo tenga (sin decidir dónde cae: eso lo
    // hace quien llama, que es el que sabe si fue una entrega o una caída).
    [Server]
    public void ServerReleaseCarrier()
    {
        if (_carrier != null)
            _carrier.RemoveEffectsByDefinition(ResolveCarryEffect());

        _carrier = null;
        _netCarrierId.Value   = -1;
        _netCarrierTeam.Value = 0;
    }

    // El botón de la definitiva con el Objetivo encima lo SUELTA. Va por
    // RequireOwnership=false porque el jugador no es dueño de la bolsa; el 'conn' lo
    // completa FishNet, así que se puede verificar que quien pide soltarla es
    // justamente quien la está cargando (nadie puede tirarle la bolsa a otro).
    [ServerRpc(RequireOwnership = false)]
    public void ServerRequestDrop(NetworkConnection conn = null)
    {
        if (_carrier == null || conn == null) return;

        NetworkObject carrierNob = _carrier.GetComponent<NetworkObject>();
        if (carrierNob == null || carrierNob.Owner != conn) return;

        Vector3 dropAt = _carrier.transform.position + _carrier.transform.forward * 1.2f;
        int team = _netCarrierTeam.Value;

        ServerReleaseCarrier();
        ServerPlaceOnGround(dropAt, PickupLockSeconds);

        if (_gm == null) _gm = MercenariesGameMode.Instance;
        if (_gm != null) _gm.ServerNotifyObjectiveDropped(team);
    }

    // Atajo para el dueño: el HUD/PlayerController llama a esto sin tener que buscar
    // la instancia ni saber si existe.
    public static void RequestDropFromOwner()
    {
        if (Instance != null && Instance.IsSpawned) Instance.ServerRequestDrop();
    }

    // Efecto que lleva el portador: ralentización + el tag que apaga la definitiva.
    // Si no se asignó uno a mano, se arma uno en código (Hidden para que no ensucie la
    // barra de buffs ni pida estar en el GameplayEffectRegistry).
    private GameplayEffect ResolveCarryEffect()
    {
        if (CarryEffect != null) return CarryEffect;
        if (_runtimeCarryEffect != null) return _runtimeCarryEffect;

        _runtimeCarryEffect = ScriptableObject.CreateInstance<GameplayEffect>();
        _runtimeCarryEffect.name           = "GE_CargandoObjetivo(runtime)";
        _runtimeCarryEffect.Duration       = 99999f;
        _runtimeCarryEffect.Period         = 0f;
        _runtimeCarryEffect.StackingPolicy = GameplayEffect.EStackingType.Refresh;
        _runtimeCarryEffect.EffectType     = GameplayEffect.EEffectType.Hidden;
        _runtimeCarryEffect.GrantedTags    = new System.Collections.Generic.List<EGameplayTag>
        {
            EGameplayTag.Status_Carrying_Objective
        };
        _runtimeCarryEffect.Modifiers = new System.Collections.Generic.List<Modifier>
        {
            new Modifier
            {
                Attribute = EAttributeType.MovSpeed,
                Type      = Modifier.EModificationType.Multiply,
                Magnitude = Mathf.Clamp01(1f - CarrySlowPercent),
            }
        };
        return _runtimeCarryEffect;
    }

    // =========================================================
    // PRESENTACIÓN (corre en todos los peers)
    // =========================================================

    private void UpdateVisual()
    {
        Transform carrier = ResolveCarrierTransform();

        if (carrier != null)
        {
            transform.position = carrier.position
                               + carrier.right   * CarryOffset.x
                               + Vector3.up      * CarryOffset.y
                               + carrier.forward * CarryOffset.z;
            transform.rotation = Quaternion.Euler(0f, carrier.eulerAngles.y, 0f);
            return;
        }

        Vector3 basePos = _netGroundPos.Value;
        basePos.y += Mathf.Sin(Time.time * BobSpeed) * BobHeight;
        transform.position = basePos;
        transform.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);
    }

    // Resuelve el transform del portador a partir del ObjectId sincronizado. Se cachea
    // porque se consulta cada frame.
    private Transform ResolveCarrierTransform()
    {
        int id = _netCarrierId.Value;
        if (id < 0) { _carrierTransformCacheId = -1; _carrierTransformCache = null; return null; }

        if (_carrierTransformCacheId == id && _carrierTransformCache != null)
            return _carrierTransformCache;

        NetworkObject nob = FindSpawned(id);
        if (nob == null) return null;

        _carrierTransformCacheId = id;
        _carrierTransformCache   = nob.transform;
        return _carrierTransformCache;
    }

    // Busca un NetworkObject por su ObjectId en la tabla de objetos spawneados. Hay que
    // mirar la del servidor o la del cliente según dónde corramos: en el host las dos
    // sirven, pero en un cliente puro la del servidor ni siquiera existe.
    private NetworkObject FindSpawned(int objectId)
    {
        if (IsServerInitialized && ServerManager != null &&
            ServerManager.Objects.Spawned.TryGetValue(objectId, out NetworkObject sNob))
            return sNob;

        if (ClientManager != null &&
            ClientManager.Objects.Spawned.TryGetValue(objectId, out NetworkObject cNob))
            return cNob;

        return null;
    }
}
