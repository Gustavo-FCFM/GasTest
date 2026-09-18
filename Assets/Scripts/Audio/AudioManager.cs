using System.Collections.Generic;
using UnityEngine;

// ============================================================
// AudioManager
//
// El único que reproduce efectos de sonido. Se crea solo al arrancar el juego (no hay
// nada que poner en la escena) y sobrevive a todo.
//
// CÓMO FUNCIONA EL AUDIO EN ESTE JUEGO, en dos líneas:
//
//  · Cada cliente tiene UN oído: el AudioListener de SU cámara (la del jugador, o la
//    de la sala cuando no tiene personaje). Los otros jugadores no son oídos: son
//    fuentes 3D en el mundo, y se los escucha desde la cámara propia, con distancia.
//  · Los sonidos NO viajan por la red: viajan los EVENTOS que ya viajaban (la
//    animación de una habilidad, su VFX, un efecto aplicado, un aviso de partida), y
//    cada cliente pone el sonido al recibirlos. Cero RPCs nuevos.
//
// Acá hay un pool de AudioSources: Play() toma uno libre, lo lleva a la posición, le
// pone el clip y lo suelta. PlayUI() es lo mismo en 2D. Todo se multiplica por el
// volumen de Ajustes (GameSettings.SfxVolume).
//
// Además se cuelga de los avisos de la partida (MercenariesGameMode.OnAnnouncement)
// para los sonidos de inicio, Objetivo, fin, etc., y lleva el loop de ambiente.
// ============================================================
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Tooltip("Cuántos sonidos pueden sonar a la vez. Si se acaban, el más viejo se corta.")]
    public int PoolSize = 24;

    private readonly List<AudioSource> _pool = new List<AudioSource>();
    private int _next;

    private AudioSource _ambient;
    private MusicPlayer _music;

    // Cuenta atrás de la preparación: último segundo en el que sonó el beep.
    private int _lastCountdownSecond = -1;

    // =========================================================
    // ARRANQUE
    // =========================================================

    // Se crea solo en cuanto carga la primera escena. Sin esto habría que acordarse de
    // ponerlo en cada escena, y el día que falte el juego queda mudo sin avisar.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < PoolSize; i++) _pool.Add(CreateSource("Sfx_" + i));

        _ambient = CreateSource("Ambient");
        _ambient.loop         = true;
        _ambient.spatialBlend = 0f;

        _music = gameObject.AddComponent<MusicPlayer>();

        MercenariesGameMode.OnAnnouncement += HandleAnnouncement;
        GameSettings.OnChanged += ApplyVolumes;

        StartAmbient();
    }

    private void OnDestroy()
    {
        MercenariesGameMode.OnAnnouncement -= HandleAnnouncement;
        GameSettings.OnChanged -= ApplyVolumes;
        if (Instance == this) Instance = null;
    }

    private AudioSource CreateSource(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.rolloffMode  = AudioRolloffMode.Linear;
        src.minDistance  = 2f;
        src.dopplerLevel = 0f;
        return src;
    }

    // =========================================================
    // REPRODUCIR
    // =========================================================

    // Sonido 3D en un punto del mundo. Un cue vacío no hace nada.
    public static void Play(SfxCue cue, Vector3 position, float volumeScale = 1f)
    {
        if (Instance == null || cue == null || cue.IsEmpty) return;
        Instance.PlayInternal(cue, position, true, volumeScale);
    }

    // Sonido 2D (UI, avisos): suena igual en toda la pantalla.
    public static void PlayUI(SfxCue cue, float volumeScale = 1f)
    {
        if (Instance == null || cue == null || cue.IsEmpty) return;
        Instance.PlayInternal(cue, Vector3.zero, false, volumeScale);
    }

    // Atajos para la UI: un clic, un confirmar. Vacíos = silencio, como todo.
    public static void Click()   { AudioLibrary lib = AudioLibrary.Instance; if (lib != null) PlayUI(lib.UiClick); }
    public static void Confirm() { AudioLibrary lib = AudioLibrary.Instance; if (lib != null) PlayUI(lib.UiConfirm); }

    private void PlayInternal(SfxCue cue, Vector3 position, bool spatial, float volumeScale)
    {
        AudioClip clip = cue.Pick();
        if (clip == null) return;

        AudioSource src = TakeSource();
        src.transform.position = position;
        src.spatialBlend = spatial ? 1f : 0f;
        src.maxDistance  = Mathf.Max(cue.MaxDistance, 3f);
        src.pitch        = cue.RandomPitch();
        src.volume       = cue.Volume * volumeScale * GameSettings.SfxVolume;
        src.clip         = clip;
        src.Play();
    }

    // El siguiente libre; si están todos ocupados, el más viejo (round-robin).
    private AudioSource TakeSource()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            int idx = (_next + i) % _pool.Count;
            if (!_pool[idx].isPlaying)
            {
                _next = (idx + 1) % _pool.Count;
                return _pool[idx];
            }
        }

        AudioSource oldest = _pool[_next];
        _next = (_next + 1) % _pool.Count;
        oldest.Stop();
        return oldest;
    }

    // =========================================================
    // AMBIENTE Y VOLÚMENES
    // =========================================================

    private void StartAmbient()
    {
        AudioLibrary lib = AudioLibrary.Instance;
        if (lib == null || lib.AmbientLoop == null) return;

        _ambient.clip = lib.AmbientLoop;
        ApplyVolumes();
        _ambient.Play();
    }

    private void ApplyVolumes()
    {
        AudioLibrary lib = AudioLibrary.Instance;
        if (lib != null && _ambient != null) _ambient.volume = lib.AmbientVolume * GameSettings.SfxVolume;
    }

    // =========================================================
    // PARTIDA
    // =========================================================

    private void HandleAnnouncement(EMatchAnnouncement type, int team, int extra)
    {
        AudioLibrary lib = AudioLibrary.Instance;
        if (lib == null) return;

        switch (type)
        {
            case EMatchAnnouncement.MatchStarted:       PlayUI(lib.MatchStarted);       break;
            case EMatchAnnouncement.ObjectiveSpawned:   PlayUI(lib.ObjectiveSpawned);   break;
            case EMatchAnnouncement.ObjectiveTaken:     PlayUI(lib.ObjectiveTaken);     break;
            case EMatchAnnouncement.ObjectiveDropped:   PlayUI(lib.ObjectiveDropped);   break;
            case EMatchAnnouncement.ObjectiveDelivered: PlayUI(lib.ObjectiveDelivered); break;
            case EMatchAnnouncement.TeamWiped:          PlayUI(lib.TeamWiped);          break;
            case EMatchAnnouncement.TeamLevelUp:        PlayUI(lib.TeamLevelUp);        break;

            case EMatchAnnouncement.MatchEnded:
            {
                // Victoria si ganó MI equipo; derrota si ganó otro o hubo empate. Un
                // espectador (equipo 0) escucha la de victoria, que es la neutra.
                int mine = MercUIFactory.LocalTeam();
                bool won = team == 0 || mine == 0 || team == mine;
                PlayUI(won ? lib.Victory : lib.Defeat);
                break;
            }
        }
    }

    // Beep por segundo en los últimos segundos de la preparación (solo con el reloj
    // corriendo: con la sala abierta el reloj está congelado y no hay cuenta atrás).
    private void Update()
    {
        AudioLibrary lib = AudioLibrary.Instance;
        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (lib == null || gm == null || gm.State != EMatchState.Warmup) { _lastCountdownSecond = -1; return; }

        LobbyManager lobby = LobbyManager.Instance;
        if (lobby != null && !lobby.MatchStarted) { _lastCountdownSecond = -1; return; }

        int second = Mathf.CeilToInt(gm.PhaseTimeRemaining);
        if (second <= 0 || second > lib.CountdownFrom || second == _lastCountdownSecond) return;

        _lastCountdownSecond = second;
        PlayUI(lib.CountdownTick);
    }
}
