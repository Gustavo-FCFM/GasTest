using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

// ============================================================
// Entity_Summon  (la base de todo lo que se invoca — 19 de octubre de 2026)
//
// Tótems, trampas, compañeros, copias, cuchillas: todos seguían la misma receta, pero cada uno
// la tenía escrita por su cuenta (el límite "la más vieja se va" estaba tres veces, "quién es
// enemigo" se decidía de tres formas, y qué pasaba si el dueño moría dependía de cada uno). Acá
// está UNA vez:
//
//   · Se crea con Entity_Summon.Spawn (en el servidor): la instancia, la spawnea en red, le
//     dice quién la invocó y aplica el límite por dueño.
//   · Su equipo es el del dueño y viaja en un SyncVar: cada pantalla lo sabe.
//   · "Dura" segundos (0 = hasta que la quiten); "Máximo por dueño" (la más vieja se va).
//   · Si el dueño muere: se queda o desaparece. Si se va del juego o cambia de clase,
//     desaparece (no tiene a quién darle lo que haga).
//   · Si tiene su propio AbilitySystemComponent: se le puede pegar (con su vida a 0 desaparece)
//     y puede ser AUTORA de efectos (el empuje que sale desde ella).
//   · La ven todos, o solo su equipo (las trampas).
//   · Una regla de enemigo/aliado (IsEnemy / IsAlly), la misma que la del ASC.
//
// Lo propio de cada una va en una subclase (Entity_Totem, Entity_Trap...) sobreescribiendo
// OnSummoned, OnExpired, OnOwnerGone, OnOwnDeath y OnZoneEnded. Para lo que solo aplica algo
// a los que estén cerca cada tanto (un aura, un muro de fuego), alcanza con este componente
// tal cual + una Summon_Zone, sin código.
// ============================================================
public class Entity_Summon : NetworkBehaviour
{
    public enum EOwnerDeath
    {
        [InspectorName("Se queda")]   Stays,
        [InspectorName("Desaparece")] Disappears,
    }

    public enum EVisibleTo
    {
        [InspectorName("Todos")]          Everyone,
        [InspectorName("Solo su equipo")] OwnTeam,
    }

    [Header("Invocación")]
    [Tooltip("Cuánto dura. 0 = hasta que la quiten (el límite, el dueño, su vida).")]
    [Label("Dura (s; 0 = hasta que la quiten)")]
    public float LifeSeconds = 0f;

    [Tooltip("Cuántas puede tener un mismo dueño a la vez: al pasarse, se va la más vieja. Quien la " +
             "invoca puede pedir otro número (la rueda de trampas, 6). 0 = sin límite.")]
    [Label("Máximo por dueño (0 = sin límite)")]
    public int MaxPerOwner = 0;

    [Tooltip("Las que comparten límite. Vacío = todas las de este tipo (todos los tótems juntos, todas " +
             "las trampas juntas).")]
    [Label("Grupo del límite (vacío = las de su tipo)")]
    public string LimitGroup = "";

    [Tooltip("Apagado = no cuenta para el límite ni saca a ninguna (las bombas de la definitiva del " +
             "Trampero no sacan a sus trampas).")]
    [Label("Cuenta para el límite")]
    public bool CountsTowardLimit = true;

    [Tooltip("Qué pasa si el dueño muere. (Si se va del juego o cambia de clase, desaparece siempre.)")]
    [Label("Si el dueño muere")]
    public EOwnerDeath IfOwnerDies = EOwnerDeath.Stays;

    [Tooltip("Quién ve el modelo. 'Solo su equipo' = invisible para los enemigos (las trampas).")]
    [Label("La ven")]
    public EVisibleTo VisibleTo = EVisibleTo.Everyone;

    [Tooltip("El modelo que se esconde (para los enemigos, o cuando la subclase lo pide). Vacío = todo " +
             "lo que cuelga de la invocación.")]
    [Label("Modelo")]
    public Transform Visual;

    [Tooltip("Si las zonas (Summon_Zone) de otras invocaciones la alcanzan. Apagado en los tótems: no " +
             "se dan el aura entre ellos.")]
    [Label("Recibe zonas")]
    public bool ReceivesZones = true;

    // --- todas las pantallas ---
    private readonly SyncVar<int>  _team       = new SyncVar<int>();
    private readonly SyncVar<bool> _revealed   = new SyncVar<bool>();   // la ven todos (el aviso de la trampa)
    private readonly SyncVar<bool> _hidden     = new SyncVar<bool>();   // no la ve nadie (ya explotó)
    private readonly SyncVar<bool> _zoneActive = new SyncVar<bool>();   // su Summon_Zone está prendida
    private readonly SyncVar<NetworkObject> _summonerNob = new SyncVar<NetworkObject>();   // el dueño, en todas las pantallas

    // El dueño (su NetworkObject), en TODAS las pantallas: el HUD del dueño encuentra así a su
    // compañero. Summoner (abajo) es el ASC y solo existe en el servidor.
    public NetworkObject SummonerObject => _summonerNob.Value;

    private Renderer[] _renderers;
    private bool       _lastVisible = true;

    // El equipo del dueño (0 = neutral). En todas las pantallas.
    public int  Team       => _team.Value;
    public bool ZoneActive => _zoneActive.Value;

    // Su propio ASC, si lo tiene (vida, autor de efectos). En todas las pantallas.
    public AbilitySystemComponent OwnASC { get; private set; }

    // --- servidor ---
    // Quién la invocó (no confundir con Owner, la conexión de red de FishNet). Solo en el servidor.
    public AbilitySystemComponent Summoner { get; private set; }

    private bool  _setUp, _despawning, _ownerDied, _ownDied;
    private float _expireAt = float.MaxValue;
    private CharacterClassDefinition _ownerClass;
    private (AbilitySystemComponent, string) _limitKey;
    private bool  _inLimit;

    // Las que cuentan para el límite, por dueño y grupo, en orden (la primera es la más vieja).
    private static readonly Dictionary<(AbilitySystemComponent, string), List<Entity_Summon>> s_byOwner =
        new Dictionary<(AbilitySystemComponent, string), List<Entity_Summon>>();

    protected virtual void Awake()
    {
        OwnASC = GetComponent<AbilitySystemComponent>();
        Transform root = Visual != null ? Visual : transform;
        _renderers = root.GetComponentsInChildren<Renderer>(true);
    }

    // =========================================================
    // CREAR (servidor)
    // =========================================================

    // Instancia el prefab, lo spawnea en red y lo prepara. maxPerOwner: el límite que pide quien
    // la invoca (-1 = el del prefab). countsTowardLimit: false para las que no cuentan (los
    // tótems de la definitiva, las bombas). Devuelve null si el prefab no es una invocación.
    public static Entity_Summon Spawn(GameObject prefab, AbilitySystemComponent owner, Vector3 position,
                                      Quaternion rotation, int maxPerOwner = -1, bool countsTowardLimit = true)
    {
        if (prefab == null) return null;
        if (prefab.GetComponent<Entity_Summon>() == null)
        {
            Debug.LogWarning($"[Entity_Summon] '{prefab.name}' no tiene un Entity_Summon (o una subclase): no se invoca.");
            return null;
        }

        GameObject obj = Instantiate(prefab, position, rotation);
        NetworkObject nob = obj.GetComponent<NetworkObject>();
        if (nob != null) InstanceFinder.ServerManager.Spawn(nob);
        else Debug.LogWarning($"[Entity_Summon] '{prefab.name}' no tiene NetworkObject: solo existe en el servidor.");

        Entity_Summon summon = obj.GetComponent<Entity_Summon>();
        summon.Setup(owner, maxPerOwner >= 0 ? maxPerOwner : summon.MaxPerOwner, countsTowardLimit);
        return summon;
    }

    private void Setup(AbilitySystemComponent owner, int maxPerOwner, bool countsTowardLimit)
    {
        Summoner    = owner;
        _team.Value = owner != null ? owner.TeamID : 0;
        _summonerNob.Value = owner != null ? owner.GetComponent<NetworkObject>() : null;
        _expireAt   = LifeSeconds > 0f ? Time.time + LifeSeconds : float.MaxValue;

        if (OwnASC != null)
        {
            // Con NetworkASC el equipo llega a todos: sin eso, en los clientes sería neutral (su
            // barra de vida no sabría si pintarse de aliado o de enemigo).
            NetworkAbilitySystemComponent net = GetComponent<NetworkAbilitySystemComponent>();
            if (net != null) net.AssignTeam(_team.Value);
            else             OwnASC.TeamID = _team.Value;
            OwnASC.OnDeath += HandleOwnDeath;

            // Su vida, a todas las pantallas desde ya. El ASC la carga en Awake, cuando todavía
            // no está en red, y el NetworkASC solo manda los CAMBIOS: sin esto la barra de vida
            // del tótem se veía vacía hasta que le pegaban.
            if (net != null) net.SyncAllAttributesToNet();
        }

        if (owner != null)
        {
            _ownerClass = owner.CurrentClass;
            owner.OnDeath += HandleOwnerDeath;
            if (countsTowardLimit && CountsTowardLimit) JoinLimit(owner, maxPerOwner);
        }

        _setUp = true;
        OnSummoned();

        foreach (Summon_Zone zone in GetComponents<Summon_Zone>())
            if (zone.StartActive) zone.Begin();
    }

    private void JoinLimit(AbilitySystemComponent owner, int max)
    {
        _limitKey = (owner, string.IsNullOrEmpty(LimitGroup) ? GetType().Name : LimitGroup);
        if (!s_byOwner.TryGetValue(_limitKey, out List<Entity_Summon> list))
            s_byOwner[_limitKey] = list = new List<Entity_Summon>();

        list.RemoveAll(s => s == null || s._despawning);
        list.Add(this);
        _inLimit = true;

        while (max > 0 && list.Count > max)
        {
            Entity_Summon oldest = list[0];
            list.RemoveAt(0);
            if (oldest != null) oldest.ServerDespawn();
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();

        if (_inLimit && s_byOwner.TryGetValue(_limitKey, out List<Entity_Summon> list))
        {
            list.Remove(this);
            if (list.Count == 0) s_byOwner.Remove(_limitKey);
        }
        if (Summoner != null) Summoner.OnDeath  -= HandleOwnerDeath;
        if (OwnASC != null) OwnASC.OnDeath -= HandleOwnDeath;
    }

    // =========================================================
    // CADA FRAME
    // =========================================================

    protected virtual void Update()
    {
        UpdateVisibility();
        if (!IsServerInitialized || !_setUp || _despawning) return;

        // Las muertes llegan en medio del daño: se resuelven acá, fuera de ese camino.
        if (_ownDied) { _ownDied = false; OnOwnDeath(); return; }

        // Sin dueño (se fue, se desatascó) o con otra clase: no tiene a quién darle lo que haga.
        if (Summoner == null || Summoner.CurrentClass != _ownerClass) { OnOwnerGone(); return; }
        if (_ownerDied) { _ownerDied = false; if (IfOwnerDies == EOwnerDeath.Disappears) { OnOwnerGone(); return; } }

        if (Time.time >= _expireAt)
        {
            _expireAt = float.MaxValue;
            OnExpired();
        }
    }

    private void HandleOwnerDeath() => _ownerDied = true;
    private void HandleOwnDeath()   => _ownDied   = true;

    // =========================================================
    // LO QUE CADA SUBCLASE PUEDE CAMBIAR (todo en el servidor)
    // =========================================================

    // Recién creada y preparada (ya tiene dueño y equipo).
    protected virtual void OnSummoned() { }

    // Se cumplió "Dura". Por defecto desaparece; la bomba explota.
    protected virtual void OnExpired() => ServerDespawn();

    // El dueño murió (con "Desaparece"), se fue o cambió de clase.
    protected virtual void OnOwnerGone() => ServerDespawn();

    // Su propia vida llegó a 0 (solo si tiene ASC).
    protected virtual void OnOwnDeath() => ServerDespawn();

    // Su zona se terminó ("Dura" de la zona). Por defecto nada: la zona de espinas sí se va.
    protected virtual void OnZoneEnded() { }

    // =========================================================
    // DÓNDE APOYARLA (19 de octubre de 2026: un tótem apareció arriba del techo)
    //
    // Antes cada habilidad buscaba el piso tirando un rayo hacia abajo desde ARRIBA del punto
    // (10 m el tótem, 3 m la trampa): con un techo más bajo que eso, el rayo empezaba encima
    // del techo y la invocación quedaba ahí arriba.
    // =========================================================

    // Sin personajes (7) ni Ignore Raycast (2: las paredes invisibles).
    protected const int GroundMask = ~((1 << 7) | (1 << 2));

    // El piso donde va algo apuntado a 'aimed', mirando desde 'viewFrom' (los ojos del que la
    // pone). Se busca hacia abajo desde un poco ANTES del punto, del lado desde donde se miró:
    // ese tramo está libre (por ahí pasó la mira), así que nunca se arranca arriba de un techo.
    // Apuntando al techo, cae al piso de abajo; apuntando a una pared, al piso frente a ella.
    public static Vector3 GroundUnderAim(Vector3 aimed, Vector3 viewFrom)
    {
        Vector3 back  = viewFrom - aimed;
        Vector3 start = aimed + (back.sqrMagnitude > 0.0001f ? back.normalized * 0.3f : Vector3.up * 0.3f);
        return Physics.Raycast(start, Vector3.down, out RaycastHit hit, 50f, GroundMask, QueryTriggerInteraction.Ignore)
            ? hit.point : aimed;
    }

    // El piso cerca de 'spot' a la altura de un piso conocido ('floorY': las bombas alrededor
    // de su centro, los tótems de la Furia alrededor del Chamán). Se busca desde 1.5 m arriba de
    // ese piso, no desde el cielo: un techo o una cornisa no lo atrapan.
    public static Vector3 GroundNear(Vector3 spot, float floorY)
    {
        Vector3 start = new Vector3(spot.x, floorY + 1.5f, spot.z);
        return Physics.Raycast(start, Vector3.down, out RaycastHit hit, 10f, GroundMask, QueryTriggerInteraction.Ignore)
            ? hit.point : new Vector3(spot.x, floorY, spot.z);
    }

    // =========================================================
    // HERRAMIENTAS PARA LAS SUBCLASES
    // =========================================================

    // Está viva en el servidor (preparada y sin irse): la lógica propia de cada subclase corre
    // solo con esto en true.
    protected bool IsLiveOnServer => IsServerInitialized && _setUp && !_despawning;

    // Misma regla que el ASC: neutral (equipo 0) cuenta como enemigo de todos; el dueño nunca.
    public bool IsEnemy(AbilitySystemComponent other)
    {
        if (other == null || other == OwnASC || other == Summoner) return false;
        if (Team == 0 || other.TeamID == 0) return true;
        return other.TeamID != Team;
    }

    // Mismo equipo (y nadie neutral). El dueño cuenta.
    public bool IsAlly(AbilitySystemComponent other)
        => other != null && other != OwnASC && Team != 0 && other.TeamID == Team;

    [Server]
    public void ServerDespawn()
    {
        if (_despawning) return;
        _despawning = true;
        if (IsSpawned) ServerManager.Despawn(gameObject);
        else Destroy(gameObject);
    }

    // La ven todos aunque sea "Solo su equipo" (el aviso de la trampa de frío).
    [Server] protected void SetRevealed(bool revealed) => _revealed.Value = revealed;

    // No la ve nadie (la trampa ya explotó: queda solo el VFX).
    [Server] protected void SetModelHidden(bool hidden) => _hidden.Value = hidden;

    // La prende y apaga su Summon_Zone (ver ahí).
    [Server] public void SetZoneFlag(bool active) => _zoneActive.Value = active;

    [Server]
    public void NotifyZoneEnded()
    {
        _zoneActive.Value = false;
        OnZoneEnded();
    }

    // =========================================================
    // LO QUE VE CADA PANTALLA
    // =========================================================

    private void UpdateVisibility()
    {
        bool visible;
        if (_hidden.Value)                                         visible = false;
        else if (_revealed.Value || VisibleTo == EVisibleTo.Everyone) visible = true;
        else visible = !IsClientInitialized || _team.Value == 0 || MercUIFactory.LocalTeam() == _team.Value;

        if (visible == _lastVisible) return;
        _lastVisible = visible;
        foreach (Renderer r in _renderers) if (r != null) r.enabled = visible;
    }
}
