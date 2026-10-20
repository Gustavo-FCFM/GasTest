using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;

// ============================================================
// CompanionManager  (Maestro de bestias — 14 de octubre de 2026)
//
// El que maneja los compañeros de UN Explorador Maestro de bestias:
//
//   · Cuál está activo (Lobo de entrada). Lo cambia la habilidad Cambio de compañero
//     (GA_SwitchCompanion, la rueda). El elegido se marca con un GE oculto en el dueño
//     (Status_CompanionWolf/Bear/Owl): así cada pantalla sabe cuál es sin RPC.
//   · Lo hace aparecer junto al dueño y lo saca si el dueño muere o cambia de clase.
//   · Cada compañero que MUERE tiene su propia espera para volver (un GE en el dueño, que
//     sale en su barra de efectos con el tiempo). Mientras, el Explorador puede sacar otro.
//   · Le pasa las órdenes: el Explorador pegó a alguien (OnDealtDamage) → Command; le
//     pegaron al Explorador (OnTookDamage) → Defend. Los golpes del propio compañero
//     (que cuentan como del dueño) se ignoran: BeginPetHit/EndPetHit.
//   · IRA PRIMAL (la definitiva, 15 de octubre de 2026): mientras el dueño tiene
//     Status_PrimalFury (GE_PrimalFury, 10 s), salen los otros dos compañeros junto al elegido
//     (y el elegido también, si estaba muerto esperando). Son EXTRAS: salen con la vida llena,
//     si mueren no les empieza ninguna espera, y al terminar desaparecen; queda el elegido con
//     la vida que tenía. Las órdenes y la defensa van a todos. Mientras dura no se puede
//     cambiar de compañero. Lo que cada uno hace distinto (atacar más rápido, presencia doble)
//     lo resuelve Entity_Companion mirando el mismo tag.
//   · Lo que se ve en el HUD lo lee UI_CompanionPanel (en el prefab de la cámara): el manager
//     no sabe nada de la UI.
//
// Vive en el PassiveBehaviorsPrefab del Maestro de bestias (hijo del jugador): corre en
// todos los peers, pero aparecer, sacar y dar órdenes es solo del servidor.
// ============================================================
public class CompanionManager : MonoBehaviour
{
    [System.Serializable]
    public struct CompanionOption
    {
        [Label("Nombre (inglés)")]
        public string Name;
        [Label("Nombre (español)")]
        public string NameEs;
        [Label("Descripción (inglés)")]
        public string Description;
        [Label("Descripción (español)")]
        public string DescriptionEs;
        [Label("Ícono")]
        public Sprite Icon;

        [Tooltip("El prefab del compañero (Entity_Companion + NetworkObject).")]
        [Label("Prefab")]
        public GameObject Prefab;

        [Tooltip("GE oculto y largo que marca en el dueño que este es el elegido (su primer tag).")]
        [Label("Efecto de elegido")]
        public GameplayEffect ActiveEffect;

        [Tooltip("GE que recibe el dueño cuando este compañero muere: mientras dure su primer tag, " +
                 "no puede volver. Sale en la barra de efectos con el tiempo. Vacío = vuelve enseguida.")]
        [Label("Espera para volver (GE)")]
        public GameplayEffect RespawnCooldown;
    }

    [Tooltip("Las opciones de la rueda, en orden.")]
    [Label("Compañeros")]
    public List<CompanionOption> Companions = new List<CompanionOption>();

    [Tooltip("El que sale al elegir la subclase.")]
    [Label("Compañero inicial")]
    public int StartIndex = 0;

    [Tooltip("Un compañero que se GUARDA herido (al cambiarlo por otro) vuelve con la vida que tenía, " +
             "y mientras está guardado se cura de a poco: este % de su vida máxima por segundo. " +
             "0 = no se cura guardado. Uno que MUERE vuelve con la vida llena tras su espera.")]
    [Label("Curación guardado (% de vida por s)")]
    public float StoredRegenPercentPerSecond = 2f;

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _net;

    private int _active = -1;                 // servidor
    private Entity_Companion _current;        // servidor
    private int _petHitDepth;
    private float _spawnDelayUntil;
    private readonly HashSet<int> _broken = new HashSet<int>();   // prefabs que no sirven (ver SpawnOne)

    // Ira primal: los compañeros que salieron de más, por índice. Solo servidor.
    private readonly Dictionary<int, Entity_Companion> _extras = new Dictionary<int, Entity_Companion>();
    private bool _furyActive;

    // La vida guardada de cada compañero (0 a 1), por índice. Solo servidor. Sin entrada = llena.
    private readonly Dictionary<int, float> _storedHealth = new Dictionary<int, float>();

    private bool IsServer => _net == null || _net.IsServerInitialized;

    private void Awake()
    {
        _asc = GetComponentInParent<AbilitySystemComponent>();
        _net = _asc != null ? _asc.GetComponent<NetworkAbilitySystemComponent>() : null;
    }

    private void OnEnable()
    {
        if (_asc == null) return;
        _asc.OnDealtDamage += HandleDealtDamage;
        _asc.OnTookDamage  += HandleTookDamage;
        _spawnDelayUntil = Time.time + 0.5f;   // que el cambio de clase termine de armarse
    }

    private void OnDisable()
    {
        if (_asc != null)
        {
            _asc.OnDealtDamage -= HandleDealtDamage;
            _asc.OnTookDamage  -= HandleTookDamage;
        }

        // Cambio de clase o destrucción: sin compañeros huérfanos.
        if (IsServer)
        {
            DespawnCurrent();
            EndFury();
            if (_asc != null && _active >= 0 && _active < Companions.Count && Companions[_active].ActiveEffect != null)
                _asc.RemoveEffectsByDefinition(Companions[_active].ActiveEffect);
        }
    }

    private void Update()
    {
        if (!IsServer || _asc == null || Companions.Count == 0) return;

        if (_active < 0) _active = Mathf.Clamp(StartIndex, 0, Companions.Count - 1);
        EnsureActiveMark();

        TickStoredRegen();

        if (_asc.HasTag(EGameplayTag.State_Dead)) { DespawnCurrent(); EndFury(); return; }

        if (_current == null && Time.time >= _spawnDelayUntil && !IsOnCooldown(_active) && !_broken.Contains(_active))
            SpawnCompanion(_active);

        bool fury = _asc.HasTag(EGameplayTag.Status_PrimalFury);
        if (fury && !_furyActive)      StartFury();
        else if (!fury && _furyActive) EndFury();
    }

    // =========================================================
    // IRA PRIMAL
    // =========================================================

    // Salen los que faltan: los otros dos, y el elegido si estaba muerto esperando.
    private void StartFury()
    {
        _furyActive = true;
        int slot = 0;
        for (int i = 0; i < Companions.Count; i++)
        {
            if (i == _active && (_current != null || !IsOnCooldown(i))) continue;   // el elegido ya está (o sale solo)
            if (_broken.Contains(i)) continue;
            Vector3 offset = slot % 2 == 0 ? new Vector3(-1.5f, 0f, -1.5f) : new Vector3(0f, 0f, -2.8f);
            slot++;
            Entity_Companion extra = SpawnOne(i, offset, 1f);
            if (extra != null) _extras[i] = extra;
        }
    }

    // Se van los extras (sin guardar su vida ni empezarles espera).
    private void EndFury()
    {
        _furyActive = false;
        foreach (Entity_Companion extra in _extras.Values)
            if (extra != null) extra.ServerDespawn();
        _extras.Clear();
    }

    // Los guardados heridos (los que no están afuera) se curan de a poco.
    private readonly List<int> _healedKeys = new List<int>();
    private void TickStoredRegen()
    {
        if (_storedHealth.Count == 0 || StoredRegenPercentPerSecond <= 0f) return;
        _healedKeys.Clear();
        _healedKeys.AddRange(_storedHealth.Keys);
        foreach (int i in _healedKeys)
        {
            if (i == _active && _current != null) continue;   // el que está afuera no se cura así
            float f = _storedHealth[i] + StoredRegenPercentPerSecond / 100f * Time.deltaTime;
            if (f >= 1f) _storedHealth.Remove(i);
            else         _storedHealth[i] = f;
        }
    }

    // La vida guardada de un compañero (0 a 1). 1 = llena (o nunca se guardó herido).
    public float StoredHealth(int index) => _storedHealth.TryGetValue(index, out float f) ? f : 1f;

    // El GE de "elegido" se pierde con la muerte o un cambio de clase: se vuelve a poner.
    private void EnsureActiveMark()
    {
        GameplayEffect mark = Companions[_active].ActiveEffect;
        EGameplayTag tag = FirstTag(mark);
        if (mark != null && tag != EGameplayTag.None && !_asc.HasTag(tag)) _asc.ApplyGameplayEffect(mark, _asc);
    }

    // =========================================================
    // CAMBIAR DE COMPAÑERO (lo pide GA_SwitchCompanion, en el servidor)
    // =========================================================

    // true = cambió (la habilidad cobra su cooldown); false = no hacía falta o no se puede.
    public bool ServerSwitch(int index)
    {
        if (!IsServer || index < 0 || index >= Companions.Count) return false;
        if (_furyActive) return false;   // durante la Ira primal no se cambia (también lo bloquea el tag en la habilidad)
        if (index == _active && _current != null) return false;
        if (IsOnCooldown(index)) return false;

        DespawnCurrent();   // el que se va vivo no queda en espera
        if (_active >= 0 && Companions[_active].ActiveEffect != null)
            _asc.RemoveEffectsByDefinition(Companions[_active].ActiveEffect);

        _active = index;
        EnsureActiveMark();
        _spawnDelayUntil = Time.time;
        return true;
    }

    private void SpawnCompanion(int index)
    {
        _current = SpawnOne(index, new Vector3(1.5f, 0f, -1.5f), StoredHealth(index));
        if (_current != null) _storedHealth.Remove(index);   // ya está afuera: su vida es la del compañero
    }

    // Hace aparecer al compañero 'index' junto al dueño (offset en su espacio, girado por su yaw)
    // con esa vida (0 a 1). null si no se pudo.
    private Entity_Companion SpawnOne(int index, Vector3 offset, float health)
    {
        GameObject prefab = Companions[index].Prefab;
        if (prefab == null || !InstanceFinder.IsServerStarted) return null;

        Transform owner = _asc.transform;
        Vector3 spot = owner.position + Quaternion.Euler(0f, owner.eulerAngles.y, 0f) * offset;
        if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 4f, NavMesh.AllAreas)) spot = hit.position;

        // La invocación (Entity_Summon): en red y del equipo del Explorador. Los compañeros no
        // tienen límite de la base: cuál está afuera lo decide este manager.
        Entity_Summon summon = Entity_Summon.Spawn(prefab, _asc, spot, owner.rotation, countsTowardLimit: false);
        if (!(summon is Entity_Companion companion))
        {
            // Un prefab sin Entity_Companion no se reconocería nunca y se sacaría otro en cada
            // frame (pasó con el primer Búho): se saca y no se vuelve a intentar.
            Debug.LogError($"[Compañero] '{prefab.name}' no tiene Entity_Companion: no se puede usar.");
            if (summon != null) summon.ServerDespawn();
            _broken.Add(index);
            return null;
        }
        companion.Begin(this, health, index);
        return companion;
    }

    // Lo saca de la escena VIVO (cambio de compañero, muerte del dueño): guarda su vida.
    private void DespawnCurrent()
    {
        if (_current == null) return;
        if (_active >= 0)
        {
            float f = _current.HealthFraction01;
            if (f < 0.999f) _storedHealth[_active] = Mathf.Max(0.01f, f);
            else            _storedHealth.Remove(_active);
        }
        _current.ServerDespawn();
        _current = null;
    }

    // Lo avisa el compañero al morir: el dueño recibe su espera para volver.
    public void OnCompanionDied(Entity_Companion companion)
    {
        // Un extra de la Ira primal: se va y listo (no le empieza ninguna espera).
        foreach (var pair in _extras)
            if (pair.Value == companion) { _extras.Remove(pair.Key); return; }

        if (companion != _current) return;
        _current = null;
        _storedHealth.Remove(_active);   // muerto vuelve lleno, tras su espera
        GameplayEffect cooldown = Companions[_active].RespawnCooldown;
        if (cooldown != null && _asc != null) _asc.ApplyGameplayEffect(cooldown, _asc);
    }

    // =========================================================
    // ÓRDENES
    // =========================================================

    public void BeginPetHit() => _petHitDepth++;
    public void EndPetHit()   => _petHitDepth = Mathf.Max(0, _petHitDepth - 1);

    private void HandleDealtDamage(AbilitySystemComponent victim, float damage)
    {
        if (!IsServer || _petHitDepth > 0 || victim == null) return;
        if (_current != null) _current.Command(victim);
        foreach (Entity_Companion extra in _extras.Values) if (extra != null) extra.Command(victim);
    }

    private void HandleTookDamage(AbilitySystemComponent attacker)
    {
        if (!IsServer || attacker == null) return;
        if (_current != null) _current.Defend(attacker);
        foreach (Entity_Companion extra in _extras.Values) if (extra != null) extra.Defend(attacker);
    }

    // =========================================================
    // LECTURA (en cualquier pantalla: el HUD y la rueda del dueño)
    // =========================================================

    // El elegido, leído de los tags sincronizados. -1 si todavía no hay.
    public int ActiveIndex
    {
        get
        {
            if (_asc == null) return -1;
            for (int i = 0; i < Companions.Count; i++)
            {
                EGameplayTag tag = FirstTag(Companions[i].ActiveEffect);
                if (tag != EGameplayTag.None && _asc.HasTag(tag)) return i;
            }
            return -1;
        }
    }

    // ¿Está la Ira primal? (los tres afuera). Lee los tags sincronizados: sirve en cualquier pantalla.
    public bool FuryActive => _asc != null && _asc.HasTag(EGameplayTag.Status_PrimalFury);

    public bool IsOnCooldown(int index)
    {
        if (_asc == null || index < 0 || index >= Companions.Count) return false;
        EGameplayTag tag = FirstTag(Companions[index].RespawnCooldown);
        return tag != EGameplayTag.None && _asc.HasTag(tag);
    }

    // Segundos que le faltan para poder volver (0 si puede). Lee lo sincronizado: sirve en
    // la pantalla del dueño.
    public float RespawnRemaining(int index)
    {
        if (!IsOnCooldown(index)) return 0f;
        GameplayEffect cd = Companions[index].RespawnCooldown;
        GameplayEffectRegistry registry = GameplayEffectRegistry.Instance;
        if (_net == null || registry == null) return 0f;
        int effectIndex = registry.GetIndex(cd);
        return effectIndex >= 0 && _net.TryGetNetActiveEffect(effectIndex, out float remaining, out _) ? remaining : 0f;
    }

    // El compañero ELEGIDO vivo de este dueño en ESTA pantalla (null si no hay). Lo busca entre
    // los que hay en la escena por su dueño y su opción sincronizados (con la Ira primal hay tres).
    private Entity_Companion _cachedCompanion;
    private float _nextSearch;
    public Entity_Companion FindCompanionHere()
    {
        if (_current != null) return _current;   // en el servidor ya se sabe
        int active = ActiveIndex;
        if (_cachedCompanion != null && _cachedCompanion.OwnerObject == OwnerObject && _cachedCompanion.OptionIndex == active)
            return _cachedCompanion;
        if (Time.time < _nextSearch) return null;
        _nextSearch = Time.time + 0.5f;

        NetworkObject me = OwnerObject;
        foreach (Entity_Companion c in FindObjectsByType<Entity_Companion>(FindObjectsSortMode.None))
            if (c.OwnerObject != null && c.OwnerObject == me && c.OptionIndex == active) { _cachedCompanion = c; return c; }
        return null;
    }

    private NetworkObject OwnerObject => _asc != null ? _asc.GetComponent<NetworkObject>() : null;

    public string DisplayName(int index)
        => index >= 0 && index < Companions.Count ? Loc.Pick(Companions[index].Name, Companions[index].NameEs) : "";

    private static EGameplayTag FirstTag(GameplayEffect effect)
        => effect != null && effect.GrantedTags != null && effect.GrantedTags.Count > 0 ? effect.GrantedTags[0] : EGameplayTag.None;
}
