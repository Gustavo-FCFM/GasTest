using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// ============================================================
// MercNightSetup
//
// La NOCHE del Cementerio (12 de noviembre de 2026, pedido de Gustavo). Gustavo ya puso el cielo nocturno y
// la luz direccional como luna; esto pone el resto en la ESCENA ABIERTA:
//   · Luz ambiente en degradado (Gradient), azul oscura: la del cielo a intensidad 1 dejaba todo como de día
//     nublado. Lo que se ve, se ve por la luna y las antorchas.
//   · Niebla nocturna azul oscura (exponencial al cuadrado): lo lejano se pierde en lo oscuro.
//   · Menos reflejos del cielo (las piedras no brillan como de día).
// No toca la luna ni el cielo, y NO guarda la escena: se guarda a mano (Ctrl+S), junto con lo demás.
// El evento del jefe oscurece todo un poco más mientras vive (MatchEvent_Boss, "Oscurece mientras vive").
// ============================================================
public static class MercNightSetup
{
    public static readonly Color AmbientSky     = new Color(0.16f, 0.18f, 0.30f);
    public static readonly Color AmbientEquator = new Color(0.08f, 0.09f, 0.15f);
    public static readonly Color AmbientGround  = new Color(0.04f, 0.04f, 0.06f);
    public static readonly Color FogColor       = new Color(0.03f, 0.04f, 0.08f);
    public const float FogDensity = 0.012f;

    [MenuItem("Mercenarios/Mapa/Poner la noche en la escena abierta")]
    public static void RunMenu() => Debug.Log(Apply());

    public static string Apply()
    {
        RenderSettings.ambientMode         = AmbientMode.Trilight;   // el "Gradient" del panel de Lighting
        RenderSettings.ambientSkyColor     = AmbientSky;
        RenderSettings.ambientEquatorColor = AmbientEquator;
        RenderSettings.ambientGroundColor  = AmbientGround;
        RenderSettings.ambientIntensity    = 1f;

        RenderSettings.fog        = true;
        RenderSettings.fogMode    = FogMode.ExponentialSquared;
        RenderSettings.fogColor   = FogColor;
        RenderSettings.fogDensity = FogDensity;

        RenderSettings.reflectionIntensity = 0.4f;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return "[Noche] Luz ambiente azul oscura, niebla nocturna y menos reflejos en la escena abierta. " +
               "Falta guardarla (Ctrl+S).";
    }
}
