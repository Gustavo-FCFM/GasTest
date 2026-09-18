using UnityEngine;

// ============================================================
// AudioLibrary
//
// Los sonidos que NO pertenecen a una habilidad ni a un efecto: pasos, golpes
// recibidos, muerte, los avisos de la partida, la UI y la música. Un solo asset en
// Resources/AudioLibrary, y todo el código lo encuentra solo.
//
// Los sonidos de cada HABILIDAD (lanzar, impacto) van en el propio GA_*, y el de cada
// EFECTO (quemadura, stun, escudo) en su GE_*: al lado del VFX, mismo lugar, mismo
// gesto. Acá va lo que es de todos.
//
// Todos los cues pueden quedar vacíos: vacío = silencio, sin quejas. Se llena de a poco.
//
// Lo crea `Mercenarios ▸ Crear la biblioteca de audio` (o Create ▸ GAS ▸ Audio ▸
// Biblioteca de audio, guardándolo en Assets/Resources con el nombre AudioLibrary).
// ============================================================
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "GAS/Audio/Biblioteca de audio")]
public class AudioLibrary : ScriptableObject
{
    public const string ResourceName = "AudioLibrary";

    [Header("Personaje")]
    [Tooltip("Pasos. Tres o cuatro variaciones sobre tierra/piedra.")]
    public SfxCue Footsteps;
    [Tooltip("Metros recorridos entre paso y paso, corriendo. 1.6 ≈ el ritmo del clip de correr.")]
    public float FootstepDistance = 1.6f;
    public SfxCue Jump;
    public SfxCue Land;

    [Header("Daño")]
    [Tooltip("Recibir daño: golpe en carne, quejido. Suena en el personaje golpeado.")]
    public SfxCue Hurt;
    [Tooltip("Daño mínimo (fracción de la vida máxima) para que suene y se vea la reacción. " +
             "Evita que una quemadura de 3 por segundo suene y sacuda al personaje sin parar.")]
    [Range(0f, 0.2f)]
    public float HurtMinFraction = 0.02f;
    [Tooltip("Segundos entre dos reacciones de daño en el mismo personaje.")]
    public float HurtCooldown = 0.45f;
    public SfxCue Death;
    public SfxCue LevelUp;

    [Header("Partida")]
    public SfxCue MatchStarted;
    public SfxCue ObjectiveSpawned;
    public SfxCue ObjectiveTaken;
    public SfxCue ObjectiveDropped;
    public SfxCue ObjectiveDelivered;
    public SfxCue TeamWiped;
    public SfxCue TeamLevelUp;
    public SfxCue Victory;
    public SfxCue Defeat;
    [Tooltip("Un beep por segundo en los últimos segundos de la preparación.")]
    public SfxCue CountdownTick;
    public int CountdownFrom = 5;

    [Header("UI")]
    public SfxCue UiClick;
    public SfxCue UiConfirm;
    public SfxCue UiError;

    [Header("Ambiente")]
    [Tooltip("Loop de fondo de la arena (viento). Es 2D y suena siempre, bajito.")]
    public AudioClip AmbientLoop;
    [Range(0f, 1f)] public float AmbientVolume = 0.35f;

    [Header("Música")]
    [Tooltip("Menú principal y sala de espera.")]
    public AudioClip MenuMusic;
    [Tooltip("Durante la partida.")]
    public AudioClip BattleMusic;
    [Tooltip("Segundos del fundido entre pistas.")]
    public float MusicCrossfade = 1.5f;

    // =========================================================
    // ACCESO
    // =========================================================

    private static AudioLibrary _instance;
    private static bool _warned;

    // Null si el asset no existe todavía: todo el que lo use tiene que tolerarlo.
    public static AudioLibrary Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = Resources.Load<AudioLibrary>(ResourceName);
            if (_instance == null && !_warned)
            {
                _warned = true;
                Debug.Log("[Audio] No hay Resources/AudioLibrary.asset: el juego corre mudo. Crealo con " +
                          "'Mercenarios ▸ Crear la biblioteca de audio' y arrastrale clips.");
            }
            return _instance;
        }
    }
}
