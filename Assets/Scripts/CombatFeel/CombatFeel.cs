using System;
using UnityEngine;

// ============================================================
// CombatFeel
//
// El panel de control de la sensación de cada golpe (6 de noviembre de 2026, pedido de Gustavo).
// Es UN asset, Assets/Resources/CombatFeel: arriba una casilla por efecto y abajo sus números.
// Se puede tocar con el juego andando: se aplica al instante y, por ser un asset, queda guardado
// al salir de Play.
//
// HAY TRES FORMAS DE SACAR UN EFECTO:
//   1. Para todos: su casilla en este asset.
//   2. Para un jugador: Ajustes ▸ Combate (GameSettings). Se apaga si falta cualquiera de las dos.
//   3. Para siempre, borrándolo (primero el componente del prefab, después el archivo):
//      · Parón de impacto: el componente CombatHitStop y CombatHitStop.cs. Lo demás queda inerte
//        y compila igual: el servidor avisa por CombatFeel.ImpactStop y ya nadie escucha.
//      · Sacudida de cámara: el componente CombatCameraShake y CombatCameraShake.cs.
//      · Bordes de colores: el objeto DamageVignette y UI_DamageVignette.cs.
//      · Rachas de bajas: el componente UI_KillStreak y UI_KillStreak.cs.
//      · Daño fantasma: los objetos HealthGhost (Nameplate y Player Camera) y UI_HealthGhost.cs.
//      · Habilidad no disponible: el componente UI_AbilityDenied (Player Camera ▸ UI) y su .cs.
//      · Números con más vida: LivelyPop en UI_DamageNumbers, y dejar la línea de antes.
//      · Destello al matar: la línea de ShowKill en NetworkASC.ObserversHandleDeath.
//      El parón, la sacudida, los bordes y las rachas viven en Match_UI ▸ CombatFeel: apagar ese
//      objeto los apaga juntos.
//
// Los efectos de esta carpeta no los llama nadie por su nombre: miran el juego (la vida del
// jugador local, una barra) o escuchan los eventos de acá abajo. Por eso se pueden borrar sueltos.
// ============================================================
[CreateAssetMenu(fileName = "CombatFeel", menuName = "Mercenarios/Sensación de los golpes")]
public class CombatFeel : ScriptableObject
{
    [Header("Encendidos (para todos los jugadores)")]
    [Tooltip("Al conectar un golpe cuerpo a cuerpo, la animación del que ataca se congela un instante.")]
    [Label("Parón de impacto")]
    public bool HitStop = true;

    [Tooltip("La cámara tiembla al recibir un golpe fuerte y al lanzar la definitiva.")]
    [Label("Sacudida de cámara")]
    public bool CameraShake = true;

    [Tooltip("Los bordes de la pantalla se tiñen de rojo al recibir daño.")]
    [Label("Bordes rojos al recibir daño")]
    public bool DamageVignette = true;

    [Tooltip("Los bordes también se tiñen de verde al curarte y de celeste si un escudo frena el golpe entero.")]
    [Label("Bordes verdes y celestes")]
    public bool ColoredEdges = true;

    [Tooltip("En las barras de vida, lo que sacó el golpe queda un momento de otro color antes de vaciarse.")]
    [Label("Daño fantasma")]
    public bool HealthGhost = true;

    [Tooltip("Si aprietas una habilidad que no se puede usar, su ícono tiembla y parpadea en rojo.")]
    [Label("Habilidad no disponible")]
    public bool AbilityDenied = true;

    [Tooltip("Varias bajas seguidas sacan un aviso en tu pantalla: ¡DOBLE BAJA!, ¡TRIPLE BAJA!…")]
    [Label("Rachas de bajas")]
    public bool KillStreak = true;

    [Tooltip("Los números crecen de golpe, se pasan de tamaño y se reacomodan. Los críticos, con más impulso.")]
    [Label("Números con más vida")]
    public bool NumberPop = true;

    [Tooltip("El que muere da un destello más largo y más grueso que el de recibir daño.")]
    [Label("Destello al matar")]
    public bool KillFlash = true;

    [Header("Parón de impacto")]
    [Tooltip("Cuánto se congela, como fracción de su tiempo entre ataques: un hacha lenta pesa más " +
             "que una daga. 0.085 = 0.1 s con un ataque cada 1.2 s.")]
    [Label("Parón: fracción del tiempo entre ataques (0 a 1)")]
    [Range(0f, 1f)] public float HitStopFraction = 0.085f;

    [Label("Parón: lo mínimo (s)")]
    public float HitStopMinSeconds = 0.05f;

    [Label("Parón: lo máximo (s)")]
    public float HitStopMaxSeconds = 0.1f;

    [Header("Sacudida de cámara")]
    [Tooltip("Cuánto gira la cámara con la sacudida más fuerte. Solo gira: no se mete en las paredes.")]
    [Label("Sacudida: giro máximo (°)")]
    public float ShakeMaxAngle = 2.5f;

    [Tooltip("Cuánto tarda en calmarse una sacudida de fuerza 1.")]
    [Label("Sacudida: duración (s)")]
    public float ShakeSeconds = 0.45f;

    [Label("Sacudida: temblores por segundo")]
    public float ShakeFrequency = 18f;

    [Tooltip("Un golpe que saca menos que esto no sacude.")]
    [Label("Sacudida: golpe fuerte desde (% de la vida máxima)")]
    public float ShakeMinHitPercent = 12f;

    [Tooltip("Un golpe que saca esto o más sacude con la fuerza más alta.")]
    [Label("Sacudida: golpe más fuerte (% de la vida máxima)")]
    public float ShakeFullHitPercent = 40f;

    [Tooltip("La fuerza del golpe más flojo que sacude. La fuerza se eleva al cuadrado: 0.5 mueve 1/4 del giro máximo.")]
    [Label("Sacudida: fuerza del golpe flojo (0 a 1)")]
    [Range(0f, 1f)] public float ShakeWeakStrength = 0.35f;

    [Label("Sacudida: fuerza del golpe más fuerte (0 a 1)")]
    [Range(0f, 1f)] public float ShakeStrongStrength = 0.7f;

    [Label("Sacudida: fuerza de la definitiva (0 a 1)")]
    [Range(0f, 1f)] public float ShakeUltimateStrength = 0.85f;

    [Header("Bordes de la pantalla")]
    [Tooltip("Un golpe que saca menos que esto no se ve: una quemadura chica no tiene que parpadear.")]
    [Label("Bordes: se ven desde (% de la vida máxima)")]
    public float VignetteMinHitPercent = 2f;

    [Tooltip("Un golpe que saca esto o más se ve con toda la fuerza (el color de la Image en el prefab).")]
    [Label("Bordes: toda la fuerza con (% de la vida máxima)")]
    public float VignetteFullHitPercent = 25f;

    [Tooltip("Qué tan fuerte se ve el golpe más flojo, comparado con el color de la Image.")]
    [Label("Bordes: fuerza del golpe flojo (0 a 1)")]
    [Range(0f, 1f)] public float VignetteWeakStrength = 0.35f;

    [Label("Bordes: se apagan en (s)")]
    public float VignetteFadeSeconds = 0.45f;

    [Tooltip("El verde a toda fuerza, opacidad incluida.")]
    [Label("Bordes: color de la curación")]
    public Color EdgeHealColor = new Color(0.1f, 1f, 0.2f, 0.7f);

    [Tooltip("El celeste a toda fuerza, opacidad incluida.")]
    [Label("Bordes: color del escudo")]
    public Color EdgeShieldColor = new Color(0.1f, 0.7f, 1f, 0.8f);

    [Tooltip("Una curación más chica que esto no se ve: las de a poquito no tienen que parpadear.")]
    [Label("Bordes: curación desde (% de la vida máxima)")]
    public float EdgeHealMinPercent = 5f;

    [Header("Daño fantasma")]
    [Tooltip("Cuánto se queda quieto el tramo antes de empezar a vaciarse. Otro golpe vuelve a contar.")]
    [Label("Fantasma: espera (s)")]
    public float GhostDelaySeconds = 0.35f;

    [Tooltip("1 = vacía una barra entera en un segundo.")]
    [Label("Fantasma: se vacía (barras por s)")]
    public float GhostShrinkPerSecond = 0.6f;

    [Header("Habilidad no disponible")]
    [Label("No disponible: duración (s)")]
    public float DeniedSeconds = 0.3f;

    [Label("No disponible: temblores")]
    public float DeniedShakes = 3f;

    [Label("No disponible: temblor (px)")]
    public float DeniedShakePixels = 6f;

    [Label("No disponible: color")]
    public Color DeniedColor = new Color(1f, 0.1f, 0.1f);

    [Header("Rachas de bajas")]
    [Tooltip("Cuánto puede pasar entre una baja y la siguiente para que sigan siendo racha.")]
    [Label("Rachas: tiempo entre bajas (s)")]
    public float KillStreakSeconds = 8f;

    [Label("Rachas: color")]
    public Color KillStreakColor = new Color(1f, 0.6f, 0f);

    [Label("Rachas: tamaño de letra")]
    public float KillStreakFontSize = 52f;

    [Header("Números con más vida")]
    [Tooltip("Cuánto tarda en crecer, pasarse y reacomodarse.")]
    [Label("Números: duración del salto (s)")]
    public float NumberPopSeconds = 0.22f;

    [Tooltip("De qué tamaño nace, comparado con el normal.")]
    [Label("Números: tamaño al nacer (×)")]
    public float NumberStartScale = 0.5f;

    [Tooltip("Multiplica cuánto se pasa cada tipo de número (el crítico el que más, el daño en el tiempo casi nada).")]
    [Label("Números: fuerza del salto (×)")]
    public float NumberPopStrength = 1.5f;

    [Tooltip("Cuánto más sube un crítico que un número normal.")]
    [Label("Números: subida del crítico (×)")]
    public float CriticalRise = 1.6f;

    [Header("Destello al matar")]
    [Label("Destello: color")]
    public Color KillFlashColor = new Color(1f, 0.8f, 0.05f);

    [Label("Destello: duración (s)")]
    public float KillFlashSeconds = 0.45f;

    [Tooltip("El grosor del contorno al aparecer. Se va encogiendo hasta 0.")]
    [Label("Destello: grosor (m)")]
    public float KillFlashWidth = 0.08f;

    // =========================================================
    // ACCESO
    // =========================================================

    private const string ResourceName = "CombatFeel";

    private static CombatFeel _config;

    // El asset. Sin él (si alguien lo borró) valen los números de acá arriba, todo prendido.
    public static CombatFeel Config
    {
        get
        {
            if (_config != null) return _config;
            _config = Resources.Load<CombatFeel>(ResourceName);
            if (_config == null) _config = CreateInstance<CombatFeel>();
            return _config;
        }
    }

    // ¿Se ve en esta pantalla? Hace falta el asset Y el ajuste del jugador.
    public static bool HitStopOn        => Config.HitStop        && GameSettings.HitStop;
    public static bool CameraShakeOn    => Config.CameraShake    && GameSettings.CameraShake;
    public static bool DamageVignetteOn => Config.DamageVignette && GameSettings.DamageVignette;
    public static bool NumberPopOn      => Config.NumberPop      && GameSettings.NumberPop;
    public static bool KillFlashOn      => Config.KillFlash      && GameSettings.KillFlash;
    public static bool HealthGhostOn    => Config.HealthGhost    && GameSettings.HealthGhost;
    public static bool AbilityDeniedOn  => Config.AbilityDenied  && GameSettings.AbilityDenied;
    public static bool KillStreakOn     => Config.KillStreak     && GameSettings.KillStreak;

    // Cuánto se congela el que ataca, según su tiempo entre ataques.
    public float HitStopSecondsFor(float attackInterval)
        => Mathf.Clamp(attackInterval * HitStopFraction, HitStopMinSeconds, HitStopMaxSeconds);

    // =========================================================
    // EVENTOS (los escuchan los efectos: quien los dispara no sabe quién los usa)
    // =========================================================

    // Un golpe cuerpo a cuerpo conectó: ese personaje se congela esos segundos. En cada pantalla.
    public static event Action<Component, float> ImpactStop;
    public static void RaiseImpactStop(Component attacker, float seconds) => ImpactStop?.Invoke(attacker, seconds);

    // El jugador local lanzó su definitiva.
    public static event Action UltimateCast;
    public static void RaiseUltimateCast() => UltimateCast?.Invoke();

    // Al jugador local le salió un número: qué fue, si lo frenó entero un escudo, y cuánto, en %
    // de su vida máxima.
    public static event Action<ECombatNumber, bool, float> LocalCombatNumber;
    public static void RaiseLocalCombatNumber(ECombatNumber kind, bool shielded, float percent)
        => LocalCombatNumber?.Invoke(kind, shielded, percent);

    // El jugador local apretó algo que no se puede usar.
    public static event Action<EAbilityInput> AbilityRefused;
    public static void RaiseAbilityDenied(EAbilityInput slot) => AbilityRefused?.Invoke(slot);

    // Cayó un personaje y fue baja de otro (su ObjectId). En cada pantalla.
    public static event Action<int> CharacterKilled;
    public static void RaiseCharacterKilled(int killerObjectId) => CharacterKilled?.Invoke(killerObjectId);

    // Sin recarga de dominio al darle Play, lo estático sobreviviría de la vez anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _config      = null;
        ImpactStop   = null;
        UltimateCast = null;
        LocalCombatNumber = null;
        AbilityRefused    = null;
        CharacterKilled   = null;
    }
}
