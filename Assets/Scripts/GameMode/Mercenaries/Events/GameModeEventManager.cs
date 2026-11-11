using FishNet.Object;
using UnityEngine;

// ============================================================
// GameModeEventManager
//
// El cronograma de la partida (11 de noviembre de 2026, pedido de Gustavo): a qué segundo se abren
// las rejas, cuándo aparece el primer Objetivo y cuándo empiezan los santuarios. Cada cosa es un
// evento (MatchEvent) hijo de este objeto, con su "Empieza a los (s)".
//
// PARA UN MAPA NUEVO: arrastrar el prefab Prefabs/GameMode/MatchEvents a la escena y acomodar los
// segundos de cada evento. Los santuarios además necesitan zonas (el prefab Prefabs/GameMode/Shrine)
// donde se quiera que aparezcan; el resto no necesita nada más.
//
// SIN ESTE OBJETO EN LA ESCENA todo sigue como antes: las rejas se abren al arrancar y el Objetivo
// aparece a los "Primer Objetivo a los (s)" del MercenariesGameMode.
//
// Es un objeto de red de la escena, aparte del NetworkGameManager, para poder copiarlo entero de un
// mapa a otro. El reloj es el de MercenariesGameMode: el servidor mira la fase y los segundos jugados
// y va prendiendo cada evento. Lo que tengan que ver las pantallas lo sincroniza cada evento.
// ============================================================
public class GameModeEventManager : NetworkBehaviour
{
    // Hay uno por escena (o ninguno).
    public static GameModeEventManager Instance { get; private set; }

    private MatchEvent[] _events = new MatchEvent[0];
    private EMatchState _lastState = EMatchState.Warmup;

    private void Awake()
    {
        Instance = this;
        _events = GetComponentsInChildren<MatchEvent>(true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // El evento de ese tipo, si la escena lo tiene.
    public T Get<T>() where T : MatchEvent
    {
        foreach (MatchEvent ev in _events)
            if (ev is T found) return found;
        return null;
    }

    // A qué segundo de la partida empieza el evento de ese tipo. Sirve en cualquier pantalla: los
    // segundos están en el prefab, no viajan por la red. False si la escena no tiene ese evento.
    public static bool TryGetStartAt<T>(out float seconds) where T : MatchEvent
    {
        T ev = Instance != null ? Instance.Get<T>() : null;
        seconds = ev != null ? ev.StartAt : 0f;
        return ev != null;
    }

    // Sesión nueva en el mismo Play (Desconectar → Iniciar Host): nada de la partida anterior.
    public override void OnStartServer()
    {
        base.OnStartServer();
        _lastState = EMatchState.Warmup;
        foreach (MatchEvent ev in _events) ev.ServerReset();
    }

    private void Update()
    {
        if (!IsServerInitialized) return;

        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (gm == null) return;

        EMatchState state = gm.State;
        if (state != _lastState)
        {
            if (_lastState == EMatchState.Playing)
                foreach (MatchEvent ev in _events) if (ev.HasBegun) ev.ServerEnd();

            if (state == EMatchState.Playing)
                foreach (MatchEvent ev in _events) ev.ServerReset();

            _lastState = state;
        }

        if (state != EMatchState.Playing) return;

        float elapsed = gm.MatchElapsedSeconds;
        foreach (MatchEvent ev in _events)
        {
            if (!ev.HasBegun && elapsed >= ev.StartAt) ev.ServerBegin();
            if (ev.HasBegun) ev.ServerTick(Time.deltaTime);
        }
    }
}
