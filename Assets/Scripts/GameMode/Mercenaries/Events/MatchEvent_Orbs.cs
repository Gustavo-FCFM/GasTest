using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

// ============================================================
// MatchEvent_Orbs
//
// Los ORBES DE DEFINITIVA de los sarcófagos (11 de noviembre de 2026, pedido de Gustavo; la idea son
// los de Valorant: una bolita negra y blanca).
//   · A "Empieza a los (s)" y después cada "Cada (s)", aparecen orbes en sarcófagos al azar (OrbSpot)
//     hasta tener "Activos como máximo" a la vez. Un orbe que nadie toma se queda.
//   · Para tomarlo hay que CANALIZAR: quedarse vivo a "Se toma a (m)" del orbe por "Canalizar (s)". Sale
//     la barra de canalizar de siempre (UI_CastBar). Recibir daño, morir o alejarse la corta; el orbe
//     sigue ahí y cualquiera puede empezar de nuevo. Lo canaliza uno a la vez: el que llegó primero.
//   · Al terminar da "Carga (0 a 1)" de la definitiva (PlayerController.ServerAddUltimateCharge). Con
//     la definitiva ya llena no se puede tomar (queda para otro). Una clase base, sin definitiva, guarda
//     la carga para su subclase.
// Los bots lo toman si pasan por al lado (no lo buscan).
//
// Todo lo decide el servidor. Se sincroniza en qué lugares hay orbe (índices de OrbSpot.All); la barra
// le llega al que canaliza por TargetRpc, y el sonido de tomarlo, a todos por ObserversRpc.
// ============================================================
public class MatchEvent_Orbs : MatchEvent
{
    public static MatchEvent_Orbs Instance { get; private set; }

    [Header("Aparecer")]
    [Tooltip("Cada cuánto aparecen orbes nuevos (hasta completar el máximo).")]
    [Label("Cada (s)")]
    public float SecondsBetween = 60f;

    [Tooltip("Cuántos orbes puede haber a la vez en el mapa.")]
    [Label("Activos como máximo")]
    public int MaxActive = 3;

    [Header("Tomarlo")]
    [Tooltip("A qué distancia del orbe tiene que estar el pecho del jugador para canalizarlo.")]
    [Label("Se toma a (m)")]
    public float TakeRadius = 2.5f;

    [Tooltip("Cuánto hay que quedarse canalizando para tomarlo.")]
    [Label("Canalizar (s)")]
    public float ChannelSeconds = 1.5f;

    [Tooltip("Qué parte de la definitiva entera da. 0.25 = un cuarto.")]
    [Range(0f, 1f)]
    [Label("Carga (0 a 1)")]
    public float UltimateCharge = 0.25f;

    [Header("Cómo se ve")]
    [Tooltip("El modelo del orbe (Prefabs/GameMode/UltOrb). Cada pantalla lo pone sobre el sarcófago mientras está activo.")]
    [Label("Orbe")]
    public GameObject OrbVisual;

    [Tooltip("El sonido al tomarlo, en el lugar del orbe.")]
    [Label("Sonido al tomarlo")]
    public SfxCue TakeSound = new SfxCue { MaxDistance = 30f };

    // =========================================================
    // ESTADO SINCRONIZADO (lo escribe solo el servidor)
    // =========================================================

    private readonly SyncList<int> _active = new SyncList<int>();   // índices de OrbSpot.All con orbe

    public bool IsActive(int spotIndex) => spotIndex >= 0 && _active.Contains(spotIndex);

    public int ActiveCount => _active.Count;

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    protected override void Reset()
    {
        base.Reset();
        StartAt = 45f;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // Sesión nueva en el mismo Play: nada de la partida anterior.
        ServerClearAll();
    }

    // =========================================================
    // SERVIDOR
    // =========================================================

    // Quien está canalizando un orbe.
    private class Channel
    {
        public PlayerController Player;
        public AbilitySystemComponent Asc;
        public float Elapsed;
        public bool Hit;
        public Action<AbilitySystemComponent> OnHit;
    }

    private readonly Dictionary<int, Channel> _channels = new Dictionary<int, Channel>();
    private readonly List<int> _spots = new List<int>();
    private float _waveTimer;
    private bool  _warnedNoSpots;

    protected override void OnEventReset() => ServerClearAll();

    protected override void OnEventEnd() => ServerClearAll();

    protected override void OnEventBegin()
    {
        ServerSpawnWave();
        _waveTimer = Mathf.Max(1f, SecondsBetween);
    }

    protected override void OnEventTick(float deltaTime)
    {
        _waveTimer -= deltaTime;
        if (_waveTimer <= 0f)
        {
            _waveTimer = Mathf.Max(1f, SecondsBetween);
            ServerSpawnWave();
        }

        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (gm == null) return;

        // Una copia: tomar un orbe lo saca de la lista.
        _spots.Clear();
        _spots.AddRange(_active);
        foreach (int spot in _spots) ServerTickOrb(gm, spot, deltaTime);
    }

    // Completa hasta el máximo, en lugares al azar que no tengan orbe.
    [Server]
    private void ServerSpawnWave()
    {
        IReadOnlyList<OrbSpot> all = OrbSpot.All;
        if (all.Count == 0)
        {
            if (!_warnedNoSpots)
                Debug.LogWarning("[Orbes] El evento está pero el mapa no tiene ningún lugar para orbes (OrbSpot).");
            _warnedNoSpots = true;
            return;
        }

        int spawned = 0;
        int guard = 0;
        while (_active.Count < Mathf.Min(MaxActive, all.Count) && guard++ < 200)
        {
            int index = UnityEngine.Random.Range(0, all.Count);
            if (_active.Contains(index)) continue;
            _active.Add(index);
            spawned++;
        }

        if (spawned > 0)
        {
            MercenariesGameMode gm = MercenariesGameMode.Instance;
            if (gm != null) gm.Announce(EMatchAnnouncement.OrbsSpawned, 0, spawned);
            Debug.Log($"[Orbes] Aparecieron {spawned} (hay {_active.Count}).");
        }
    }

    [Server]
    private void ServerTickOrb(MercenariesGameMode gm, int spot, float deltaTime)
    {
        OrbSpot place = OrbSpot.ByIndex(spot);
        if (place == null) { ServerRemoveOrb(spot); return; }
        Vector3 point = place.OrbPoint;

        if (_channels.TryGetValue(spot, out Channel ch))
        {
            // ¿Sigue valiendo? Vivo, cerca, sin golpes y con lugar para la carga.
            bool valid = ch.Player != null && ch.Asc != null && !ch.Hit &&
                         !ch.Asc.HasTag(EGameplayTag.State_Dead) &&
                         InRange(ch.Player, point) && ch.Player.ServerCanGainUltimateCharge();
            if (!valid)
            {
                ServerStopChannel(spot, interrupted: true);
                return;
            }

            ch.Elapsed += deltaTime;
            if (ch.Elapsed >= ChannelSeconds) ServerTake(spot, ch, point);
            return;
        }

        // Nadie canalizando: empieza el más cercano que pueda.
        PlayerController best = null;
        float bestSq = float.MaxValue;
        foreach (PlayerController pc in gm.Players)
        {
            if (pc == null || IsChanneling(pc)) continue;
            AbilitySystemComponent asc = pc.GetComponent<AbilitySystemComponent>();
            if (asc == null || !MercenariesGameMode.IsValidTeam(asc.TeamID) || asc.HasTag(EGameplayTag.State_Dead)) continue;
            if (!InRange(pc, point) || !pc.ServerCanGainUltimateCharge()) continue;

            float d = (Chest(pc) - point).sqrMagnitude;
            if (d < bestSq) { bestSq = d; best = pc; }
        }
        if (best != null) ServerStartChannel(spot, best);
    }

    private static Vector3 Chest(PlayerController pc) => pc.transform.position + Vector3.up;

    private bool InRange(PlayerController pc, Vector3 point) =>
        (Chest(pc) - point).sqrMagnitude <= TakeRadius * TakeRadius;

    private bool IsChanneling(PlayerController pc)
    {
        foreach (Channel c in _channels.Values) if (c.Player == pc) return true;
        return false;
    }

    [Server]
    private void ServerStartChannel(int spot, PlayerController pc)
    {
        var ch = new Channel { Player = pc, Asc = pc.GetComponent<AbilitySystemComponent>() };
        ch.OnHit = attacker => ch.Hit = true;
        ch.Asc.OnTookDamage += ch.OnHit;
        _channels[spot] = ch;

        if (pc.Owner != null && pc.Owner.IsValid) TargetCastBar(pc.Owner, true, ChannelSeconds, false);
    }

    [Server]
    private void ServerStopChannel(int spot, bool interrupted)
    {
        if (!_channels.TryGetValue(spot, out Channel ch)) return;
        _channels.Remove(spot);

        if (ch.Asc != null) ch.Asc.OnTookDamage -= ch.OnHit;
        if (ch.Player != null && ch.Player.Owner != null && ch.Player.Owner.IsValid)
            TargetCastBar(ch.Player.Owner, false, 0f, interrupted);
    }

    [Server]
    private void ServerTake(int spot, Channel ch, Vector3 point)
    {
        ServerStopChannel(spot, interrupted: false);
        ServerRemoveOrb(spot);

        ch.Player.ServerAddUltimateCharge(UltimateCharge);
        ObserversTaken(point);
        Debug.Log($"[Orbes] {ch.Player.name} (equipo {ch.Asc.TeamID}) tomó un orbe: +{UltimateCharge:P0} de definitiva.");
    }

    [Server]
    private void ServerRemoveOrb(int spot)
    {
        ServerStopChannel(spot, interrupted: true);
        _active.Remove(spot);
    }

    [Server]
    private void ServerClearAll()
    {
        foreach (int spot in new List<int>(_channels.Keys)) ServerStopChannel(spot, interrupted: false);
        _channels.Clear();
        _active.Clear();
        _waveTimer = 0f;
    }

    // La barra de canalizar del que lo toma (la misma de las habilidades).
    [TargetRpc]
    private void TargetCastBar(NetworkConnection conn, bool show, float seconds, bool interrupted)
    {
        if (show) UI_CastBar.Show(Loc.T("orb.channel"), seconds, false);
        else UI_CastBar.Hide(interrupted);
    }

    [ObserversRpc]
    private void ObserversTaken(Vector3 point)
    {
        AudioManager.Play(TakeSound, point);
    }
}
