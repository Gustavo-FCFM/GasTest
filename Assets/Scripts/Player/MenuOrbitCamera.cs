using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ============================================================
// MenuOrbitCamera
//
// La cámara de la sala (Camara_Lobby) girando despacio sobre la meseta. Es el fondo del
// menú principal: la arena de verdad, con su luz y sus materiales, sin grabar ningún
// video ni levantar ninguna partida. Cuesta lo que cuesta dibujar el mapa vacío.
//
// GIRA mientras NO hay partida (menú principal y sala de espera). Cuando arranca la
// partida deja de moverla: si sos jugador, PlayerController apaga esta cámara y prende
// la suya; si sos espectador, SpectatorCamera toma esta misma cámara y la vuela. Al
// terminar la partida y volver a la sala, vuelve a girar sola.
//
// EL BLUR es un Depth of Field de URP metido por código (un Volume global que se
// prende solo mientras el menú principal está a la vista). Con el menú cerrado se apaga
// y la sala se ve nítida. Si el renderer de URP no tiene post-procesado, simplemente no
// hay blur — el resto funciona igual.
//
// Va en el mismo GameObject que la cámara de la sala. Lo instala
// `Mercenarios ▸ Instalar el menú principal en la arena`.
// ============================================================
[RequireComponent(typeof(Camera))]
public class MenuOrbitCamera : MonoBehaviour
{
    [Header("Órbita")]
    [Tooltip("Altura sobre el centro de la arena.")]
    public float Height = 22f;

    [Tooltip("Distancia horizontal al centro. Chica = encima de la meseta; grande = desde afuera.")]
    public float Radius = 18f;

    [Tooltip("Grados por segundo. 3 = una vuelta cada dos minutos.")]
    public float DegreesPerSecond = 3f;

    [Tooltip("Hacia dónde mira, relativo al centro de la arena (en metros). Un poco por " +
             "encima del piso queda más natural que mirar al punto exacto.")]
    public Vector3 LookAtOffset = new Vector3(0f, 1f, 0f);

    [Header("Blur (Depth of Field de URP)")]
    public bool  BlurEnabled = true;
    [Tooltip("Cuanto más chica la distancia de foco y más grande la focal, más blur.")]
    public float BlurFocusDistance = 0.5f;
    public float BlurFocalLength   = 120f;
    public float BlurAperture      = 1.4f;

    private Camera _camera;
    private float  _angle;
    private Volume _volume;
    private bool   _postProcessWasOn;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void OnDisable()
    {
        SetBlur(false);
    }

    private void LateUpdate()
    {
        // El menú sobre la arena pide el blur; la sala, no.
        SetBlur(UI_MainMenu.IsShowing);

        if (!ShouldOrbit()) return;

        Vector3 center = ResolveArenaCenter();

        _angle += DegreesPerSecond * Time.unscaledDeltaTime;
        if (_angle > 360f) _angle -= 360f;

        float rad = _angle * Mathf.Deg2Rad;
        Vector3 pos = center + new Vector3(Mathf.Sin(rad) * Radius, Height, Mathf.Cos(rad) * Radius);

        transform.position = pos;
        transform.rotation = Quaternion.LookRotation((center + LookAtOffset - pos).normalized, Vector3.up);
    }

    // Gira mientras no haya partida en curso. Con partida, esta cámara o está apagada (un
    // jugador) o la maneja SpectatorCamera (un espectador): no hay que pelearle.
    private bool ShouldOrbit()
    {
        LobbyManager lobby = LobbyManager.Instance;
        return lobby == null || !lobby.MatchStarted;
    }

    private static Vector3 ResolveArenaCenter()
    {
        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (gm != null && gm.ObjectiveSpawnPoint != null) return gm.ObjectiveSpawnPoint.position;

        MercObjective obj = MercObjective.Instance;
        return obj != null ? obj.WorldPosition : Vector3.zero;
    }

    // =========================================================
    // BLUR
    // =========================================================

    private void SetBlur(bool on)
    {
        if (!BlurEnabled) on = false;

        if (on && _volume == null) BuildVolume();
        if (_volume == null) return;
        if (_volume.enabled == on) return;

        _volume.enabled = on;

        // El post-procesado de la cámara se prende SOLO mientras hay blur, y se deja como
        // estaba al apagarlo: la sala no tiene por qué pagar un pase de post-proceso.
        UniversalAdditionalCameraData data = _camera.GetUniversalAdditionalCameraData();
        if (data == null) return;

        if (on)
        {
            _postProcessWasOn = data.renderPostProcessing;
            data.renderPostProcessing = true;
        }
        else
        {
            data.renderPostProcessing = _postProcessWasOn;
        }
    }

    // Un Volume global con un solo override: Depth of Field en modo Bokeh, enfocado a
    // medio metro. Todo lo que está más lejos —o sea, el mapa entero— queda fuera de foco.
    private void BuildVolume()
    {
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "MenuBlurProfile";

        DepthOfField dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(BlurFocusDistance);
        dof.focalLength.Override(BlurFocalLength);
        dof.aperture.Override(BlurAperture);

        // Capa 0 (Default): es la que mira el Volume Layer Mask de la cámara si nadie lo
        // cambió. En su propio hijo, para no tocar la capa de la cámara.
        GameObject go = new GameObject("MenuBlurVolume");
        go.transform.SetParent(transform, false);
        go.layer = 0;

        _volume = go.AddComponent<Volume>();
        _volume.isGlobal      = true;
        _volume.priority      = 100f;
        _volume.sharedProfile = profile;
        _volume.enabled       = false;
    }
}
