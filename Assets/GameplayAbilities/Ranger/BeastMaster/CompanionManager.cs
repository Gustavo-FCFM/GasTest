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

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _net;

    private int _active = -1;                 // servidor
    private Entity_Companion _current;        // servidor
    private int _petHitDepth;
    private float _spawnDelayUntil;
    private readonly HashSet<int> _broken = new HashSet<int>();   // prefabs que no sirven (ver SpawnCompanion)

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
            if (_asc != null && _active >= 0 && _active < Companions.Count && Companions[_active].ActiveEffect != null)
                _asc.RemoveEffectsByDefinition(Companions[_active].ActiveEffect);
        }
    }

    private void Update()
    {
        if (!IsServer || _asc == null || Companions.Count == 0) return;

        if (_active < 0) _active = Mathf.Clamp(StartIndex, 0, Companions.Count - 1);
        EnsureActiveMark();

        if (_asc.HasTag(EGameplayTag.State_Dead)) { DespawnCurrent(); return; }

        if (_current == null && Time.time >= _spawnDelayUntil && !IsOnCooldown(_active) && !_broken.Contains(_active))
            SpawnCompanion(_active);
    }

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
        GameObject prefab = Companions[index].Prefab;
        if (prefab == null || !InstanceFinder.IsServerStarted) return;

        Transform owner = _asc.transform;
        Vector3 spot = owner.position + Quaternion.Euler(0f, owner.eulerAngles.y, 0f) * new Vector3(1.5f, 0f, -1.5f);
        if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 4f, NavMesh.AllAreas)) spot = hit.position;

        GameObject obj = Instantiate(prefab, spot, owner.rotation);
        NetworkObject nob = obj.GetComponent<NetworkObject>();
        if (nob == null)
        {
            Debug.LogWarning($"[Compañero] '{prefab.name}' no tiene NetworkObject.");
            Destroy(obj);
            return;
        }
        InstanceFinder.ServerManager.Spawn(nob);

        _current = obj.GetComponent<Entity_Companion>();
        if (_current == null)
        {
            // Un prefab sin Entity_Companion no se reconocería nunca y se sacaría otro en cada
            // frame (pasó con el primer Búho): se saca y no se vuelve a intentar.
            Debug.LogError($"[Compañero] '{prefab.name}' no tiene Entity_Companion: no se puede usar.");
            InstanceFinder.ServerManager.Despawn(nob);
            _broken.Add(index);
            return;
        }
        _current.Initialize(_asc, this);
    }

    private void DespawnCurrent()
    {
        if (_current == null) return;
        NetworkObject nob = _current.GetComponent<NetworkObject>();
        if (nob != null && nob.IsSpawned) InstanceFinder.ServerManager.Despawn(nob);
        else if (_current != null) Destroy(_current.gameObject);
        _current = null;
    }

    // Lo avisa el compañero al morir: el dueño recibe su espera para volver.
    public void OnCompanionDied(Entity_Companion companion)
    {
        if (companion != _current) return;
        _current = null;
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
        if (!IsServer || _petHitDepth > 0 || _current == null || victim == null) return;
        _current.Command(victim);
    }

    private void HandleTookDamage(AbilitySystemComponent attacker)
    {
        if (!IsServer || _current == null || attacker == null) return;
        _current.Defend(attacker);
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

    // El compañero vivo de este dueño en ESTA pantalla (null si no hay). Lo busca entre los
    // que hay en la escena por su dueño sincronizado.
    private Entity_Companion _cachedCompanion;
    private float _nextSearch;
    public Entity_Companion FindCompanionHere()
    {
        if (_current != null) return _current;   // en el servidor ya se sabe
        if (_cachedCompanion != null && _cachedCompanion.OwnerObject == OwnerObject) return _cachedCompanion;
        if (Time.time < _nextSearch) return null;
        _nextSearch = Time.time + 0.5f;

        NetworkObject me = OwnerObject;
        foreach (Entity_Companion c in FindObjectsByType<Entity_Companion>(FindObjectsSortMode.None))
            if (c.OwnerObject != null && c.OwnerObject == me) { _cachedCompanion = c; return c; }
        return null;
    }

    private NetworkObject OwnerObject => _asc != null ? _asc.GetComponent<NetworkObject>() : null;

    public string DisplayName(int index)
        => index >= 0 && index < Companions.Count ? Loc.Pick(Companions[index].Name, Companions[index].NameEs) : "";

    private static EGameplayTag FirstTag(GameplayEffect effect)
        => effect != null && effect.GrantedTags != null && effect.GrantedTags.Count > 0 ? effect.GrantedTags[0] : EGameplayTag.None;
}
