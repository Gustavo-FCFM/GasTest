using UnityEngine;

// ============================================================
// MusicPlayer
//
// La música. Dos AudioSources 2D que se cruzan en fundido, y una sola regla: con
// partida en curso suena BattleMusic, el resto del tiempo (menú principal, sala de
// espera, fin de partida) suena MenuMusic. Las pistas salen de AudioLibrary; el
// volumen, de Ajustes (GameSettings.MusicVolume).
//
// Lo crea AudioManager; no hay que ponerlo en ninguna escena.
//
// Cuando llegue la música de batalla de tu amigo: la arrastrás a BattleMusic en la
// biblioteca y listo.
// ============================================================
public class MusicPlayer : MonoBehaviour
{
    private AudioSource _a;
    private AudioSource _b;
    private AudioSource _active;   // la que suena (o está subiendo)

    private AudioClip _wanted;
    private float _fadeTime;
    private float _pollTimer;

    private void Awake()
    {
        _a = CreateSource("Music_A");
        _b = CreateSource("Music_B");
        _active = _a;

        GameSettings.OnChanged += RefreshVolume;
    }

    private void OnDestroy()
    {
        GameSettings.OnChanged -= RefreshVolume;
    }

    private AudioSource CreateSource(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake  = false;
        src.loop         = true;
        src.spatialBlend = 0f;
        src.volume       = 0f;
        return src;
    }

    private void Update()
    {
        // Qué pista toca, revisado dos veces por segundo (alcanza y sobra).
        _pollTimer += Time.unscaledDeltaTime;
        if (_pollTimer >= 0.5f)
        {
            _pollTimer = 0f;
            AudioClip wanted = ResolveWantedTrack();
            if (wanted != _wanted) StartCrossfade(wanted);
        }

        TickFade();
    }

    // Batalla mientras la partida está en curso; menú en cualquier otro momento.
    private AudioClip ResolveWantedTrack()
    {
        AudioLibrary lib = AudioLibrary.Instance;
        if (lib == null) return null;

        MercenariesGameMode gm = MercenariesGameMode.Instance;
        LobbyManager lobby = LobbyManager.Instance;

        bool inMatch = gm != null && gm.State == EMatchState.Playing &&
                       (lobby == null || lobby.MatchStarted);

        return inMatch ? lib.BattleMusic : lib.MenuMusic;
    }

    private void StartCrossfade(AudioClip clip)
    {
        _wanted = clip;

        AudioLibrary lib = AudioLibrary.Instance;
        _fadeTime = lib != null ? Mathf.Max(0.05f, lib.MusicCrossfade) : 1f;

        // La otra fuente arranca la pista nueva desde cero; la vieja baja hasta callarse.
        AudioSource incoming = _active == _a ? _b : _a;
        incoming.clip = clip;
        if (clip != null) incoming.Play();
        _active = incoming;
    }

    private void TickFade()
    {
        float target = GameSettings.MusicVolume;
        float step   = Time.unscaledDeltaTime / _fadeTime * Mathf.Max(target, 0.01f);
        if (_fadeTime <= 0f) step = 1f;

        Fade(_a, _a == _active ? target : 0f, step);
        Fade(_b, _b == _active ? target : 0f, step);
    }

    private static void Fade(AudioSource src, float target, float step)
    {
        if (src.clip == null) { src.volume = 0f; return; }

        src.volume = Mathf.MoveTowards(src.volume, target, step);
        if (target <= 0f && src.volume <= 0f && src.isPlaying) src.Stop();
    }

    // Cambió el volumen en Ajustes: la fuente activa lo sigue en el próximo fundido
    // (TickFade ya apunta a GameSettings.MusicVolume), esto solo evita el retraso.
    private void RefreshVolume()
    {
        if (_active != null && _active.clip != null && _active.volume > 0f)
            _active.volume = GameSettings.MusicVolume;
    }
}
