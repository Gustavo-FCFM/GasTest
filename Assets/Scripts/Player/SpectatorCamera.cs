using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// SpectatorCamera
//
// La cámara de quien entra a la sala como ESPECTADOR. Sirve para mirar una partida sin
// jugarla: para enseñar el juego, para grabar, y para mirar peleas desde afuera cuando
// algo se ve raro.
//
// Se enciende sola: lee la sala (LobbyManager) y, si tu fila dice Spectator y la partida
// ya arrancó, toma la cámara de la escena y se pone a volar. No hay nada que cablear ni
// que elegir en un menú, y funciona igual para el host y para cualquier cliente — cada
// uno mira su propia copia.
//
// CÓMO SE USA:
//   WASD           moverse · Espacio sube · Ctrl baja · Shift para ir rápido
//   Clic izq/der   pasar al siguiente/anterior jugador (lo sigue por detrás)
//   F              volver a la cámara libre
//   H              mostrar u ocultar el nombre y la vida de quien mirás
//   M              mostrar u ocultar el marcador
//
// POR QUÉ NO ES UN NetworkBehaviour: no manda ni recibe nada. Un espectador no existe
// para el servidor más allá de su fila en la sala — todo lo que hace es mirar objetos
// que ya están sincronizados.
//
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ Spectator), desde el 14 de octubre de 2026: su
// canvas (el panel de quien mirás, con nombre y vida, y la ayuda de teclas abajo a la
// derecha) está armado ahí y se acomoda a mano. Este script mueve la cámara y escribe los textos.
// ============================================================
public class SpectatorCamera : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("El canvas del espectador: se prende solo al empezar a mirar.")]
    [Label("Canvas del espectador")]
    public Canvas UiCanvas;

    [Tooltip("El panel de abajo con el nombre y la vida de quien seguís.")]
    [Label("Panel del observado")]
    public RectTransform PlayerPanel;

    [Label("Nombre")]
    public TMP_Text NameText;

    [Tooltip("El relleno de la barra de vida (Image Filled horizontal).")]
    [Label("Relleno de la vida")]
    public Image HealthFill;

    [Label("Texto de la vida")]
    public TMP_Text HealthText;

    [Tooltip("La ayuda de teclas, abajo a la derecha.")]
    [Label("Ayuda")]
    public TMP_Text HintText;

    [Header("Vuelo libre")]
    [Tooltip("Metros por segundo.")]
    [Label("Velocidad (m/s)")]
    public float MoveSpeed       = 16f;
    [Tooltip("Cuánto multiplica la velocidad el Shift.")]
    [Label("Rápido (×)")]
    public float FastMultiplier  = 3f;
    [Label("Sensibilidad")]
    public float LookSensitivity = 2.4f;

    [Header("Dónde arranca")]
    [Tooltip("Altura sobre el centro de la arena. Pensada para ver el centro entero de " +
             "una, más o menos desde donde mira la cámara de la sala.")]
    [Label("Altura inicial (m)")]
    public float StartHeight   = 26f;
    [Tooltip("Cuánto se aleja del centro hacia atrás para tener perspectiva.")]
    [Label("Distancia inicial (m)")]
    public float StartDistance = 40f;

    [Header("Seguir a un jugador")]
    [Tooltip("Dónde se pone la cámara respecto del jugador que sigue: atrás y arriba, " +
             "como una cámara de hombro.")]
    [Label("Posición al seguir (m)")]
    public Vector3 FollowOffset = new Vector3(0f, 2.4f, -4.8f);
    [Tooltip("Qué tan rápido persigue. Bajo = más suave, y en una grabación se nota.")]
    [Label("Suavizado al seguir")]
    public float FollowLerp = 8f;

    [Header("Teclas")]
    [Label("Tecla: cámara libre")]
    public KeyCode FreeCamKey        = KeyCode.F;
    [Label("Tecla: UI del jugador")]
    public KeyCode TogglePlayerUIKey = KeyCode.H;
    [Label("Tecla: marcador")]
    public KeyCode ScoreboardKey     = KeyCode.M;

    // --- estado ---
    private Camera        _camera;
    private bool          _active;
    private float         _yaw;
    private float         _pitch;

    private PlayerController _following;
    private int              _followIndex = -1;

    private bool _showPlayerUI  = true;
    private bool _showScoreboard = true;

    private void Awake()
    {
        if (UiCanvas != null) UiCanvas.gameObject.SetActive(false);
    }

    private readonly List<PlayerController> _players = new List<PlayerController>();

    // =========================================================
    // CICLO
    // =========================================================

    private void Update()
    {
        bool shouldBeActive = ResolveIsSpectating();

        if (shouldBeActive != _active)
        {
            _active = shouldBeActive;
            if (_active) Enter();
            else         Exit();
        }

        if (!_active || _camera == null) return;

        ReadInput();
        MoveCamera();
        RefreshUI();
    }

    // Espectador = tu fila en la sala lo dice, y la partida ya arrancó. Antes de que
    // arranque estás mirando el panel de la sala, no el mapa.
    private bool ResolveIsSpectating()
    {
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby == null || !lobby.MatchStarted) return false;
        if (!lobby.TryGetLocalEntry(out LobbyEntry me)) return false;

        return me.Spectator || me.Team <= 0;
    }

    private void Enter()
    {
        _camera = ResolveCamera();
        if (_camera == null)
        {
            Debug.LogWarning("[Espectador] No hay ninguna cámara activa en la escena para usar.");
            _active = false;
            return;
        }

        MoveToOverview();
        BuildUI();

        // El cursor va TRABADO: esto se mira con el mouse, como jugando. Se pide por
        // UICursor y no a mano — con ESC abrís el recuadro de red y ahí sí lo necesitás
        // suelto, y quien manda sobre eso es UICursor.
        UICursor.Apply();

        Debug.Log("[Espectador] Cámara libre. WASD para moverte, clic para seguir a un jugador, " +
                  $"{FreeCamKey} para volver a libre.");
    }

    private void Exit()
    {
        _following = null;
        if (UiCanvas != null) UiCanvas.gameObject.SetActive(false);

        // Si el espectador apagó el marcador, se lo devolvemos al salir: la bandera es
        // global y se la llevaría puesta a la próxima partida.
        UI_MercenariesHUD.ScoreboardHiddenByViewer = false;
    }

    // La cámara de la escena (la del lobby). Un espectador nunca spawnea personaje, así
    // que esa cámara sigue viva y es la que tiene que volar.
    private Camera ResolveCamera()
    {
        if (Camera.main != null) return Camera.main;

        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (cam.isActiveAndEnabled) return cam;

        return null;
    }

    // Arranca mirando el centro de la arena desde arriba y desde atrás: la idea es que lo
    // primero que veas sea la meseta entera, no un pedazo de pared.
    private void MoveToOverview()
    {
        Vector3 center = ResolveArenaCenter();
        Vector3 start  = center + new Vector3(0f, StartHeight, -StartDistance);

        _camera.transform.position = start;
        _camera.transform.rotation = Quaternion.LookRotation((center - start).normalized);

        Vector3 e = _camera.transform.eulerAngles;
        _pitch = NormalizePitch(e.x);
        _yaw   = e.y;
    }

    private Vector3 ResolveArenaCenter()
    {
        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (gm != null && gm.ObjectiveSpawnPoint != null) return gm.ObjectiveSpawnPoint.position;

        MercObjective obj = MercObjective.Instance;
        return obj != null ? obj.WorldPosition : Vector3.zero;
    }

    private static float NormalizePitch(float x) => x > 180f ? x - 360f : x;

    // =========================================================
    // INPUT
    // =========================================================

    private void ReadInput()
    {
        // Con un menú abierto (el recuadro de red con ESC) el mouse es del menú: mover la
        // cámara mientras intentás apretar Desconectar es exactamente lo que molesta.
        bool menuOpen = UICursor.Free;

        if (!menuOpen)
        {
            if (Input.GetMouseButtonDown(0)) CycleTarget(+1);
            if (Input.GetMouseButtonDown(1)) CycleTarget(-1);
        }

        if (Input.GetKeyDown(FreeCamKey)) _following = null;

        if (Input.GetKeyDown(TogglePlayerUIKey)) _showPlayerUI = !_showPlayerUI;

        if (Input.GetKeyDown(ScoreboardKey))
        {
            _showScoreboard = !_showScoreboard;
            UI_MercenariesHUD.ScoreboardHiddenByViewer = !_showScoreboard;
        }

        if (menuOpen || _following != null) return;

        // Misma sensibilidad e inversión que la cámara del jugador (panel de Ajustes).
        float sens  = LookSensitivity * GameSettings.MouseSensitivity;
        float ySign = GameSettings.InvertY ? -1f : 1f;
        _yaw   += Input.GetAxisRaw("Mouse X") * sens;
        _pitch -= Input.GetAxisRaw("Mouse Y") * sens * ySign;
        _pitch  = Mathf.Clamp(_pitch, -89f, 89f);
    }

    // Pasa al siguiente (o anterior) jugador vivo. La lista se rearma en cada salto: los
    // jugadores entran, mueren y respawnean todo el tiempo, y guardarla se desincroniza.
    private void CycleTarget(int step)
    {
        RefreshPlayerList();
        if (_players.Count == 0) { _following = null; return; }

        // Desde cámara libre, el primer clic engancha al primero de la lista.
        if (_following == null) _followIndex = step > 0 ? -1 : 0;

        _followIndex = ((_followIndex + step) % _players.Count + _players.Count) % _players.Count;
        _following   = _players[_followIndex];
    }

    private void RefreshPlayerList()
    {
        PlayerController current = _following;

        _players.Clear();
        foreach (PlayerController pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (pc != null) _players.Add(pc);

        // Se ordenan por equipo y nombre para que el recorrido sea SIEMPRE el mismo: sin
        // esto el orden lo decide Unity y dos clics seguidos podían volver al mismo.
        _players.Sort((a, b) =>
        {
            int ta = ResolveTeam(a), tb = ResolveTeam(b);
            if (ta != tb) return ta.CompareTo(tb);
            return string.CompareOrdinal(ResolveName(a), ResolveName(b));
        });

        _followIndex = current != null ? _players.IndexOf(current) : -1;
    }

    // =========================================================
    // MOVIMIENTO
    // =========================================================

    private void MoveCamera()
    {
        if (_following != null)
        {
            FollowPlayer();
            return;
        }

        Transform t = _camera.transform;
        t.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        Vector3 dir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) dir += t.forward;
        if (Input.GetKey(KeyCode.S)) dir -= t.forward;
        if (Input.GetKey(KeyCode.D)) dir += t.right;
        if (Input.GetKey(KeyCode.A)) dir -= t.right;

        // Subir y bajar en el eje del MUNDO, no en el de la cámara: mirando al piso,
        // "arriba" tiene que seguir siendo arriba.
        if (Input.GetKey(KeyCode.Space))       dir += Vector3.up;
        if (Input.GetKey(KeyCode.LeftControl)) dir -= Vector3.up;

        if (dir.sqrMagnitude < 0.001f) return;

        float speed = MoveSpeed * (Input.GetKey(KeyCode.LeftShift) ? FastMultiplier : 1f);
        t.position += dir.normalized * speed * Time.deltaTime;
    }

    // Cámara de hombro sobre el jugador seguido. Va suavizada porque el objetivo puede
    // dashear o teletransportarse, y sin suavizado eso es un salto seco en la grabación.
    private void FollowPlayer()
    {
        if (_following == null) return;

        Transform target = _following.transform;
        Vector3 wanted   = target.position + target.rotation * FollowOffset;

        Transform t = _camera.transform;
        t.position  = Vector3.Lerp(t.position, wanted, FollowLerp * Time.deltaTime);

        Vector3 lookAt = target.position + Vector3.up * 1.6f;
        t.rotation = Quaternion.Slerp(t.rotation,
                                      Quaternion.LookRotation((lookAt - t.position).normalized),
                                      FollowLerp * Time.deltaTime);

        // Al cortar el seguimiento con F, la cámara libre tiene que arrancar mirando lo
        // mismo: se copia la orientación actual en todo momento.
        Vector3 e = t.eulerAngles;
        _pitch = NormalizePitch(e.x);
        _yaw   = e.y;
    }

    // =========================================================
    // UI
    // =========================================================

    private void BuildUI()
    {
        if (UiCanvas != null) UiCanvas.gameObject.SetActive(true);
    }

    private void RefreshUI()
    {
        if (HintText != null)
        {
            HintText.text = _following != null
                ? Loc.T("spectator.hint_following", ("free", FreeCamKey), ("info", TogglePlayerUIKey), ("scoreboard", ScoreboardKey))
                : Loc.T("spectator.hint_free", ("scoreboard", ScoreboardKey));
        }

        bool showPanel = _showPlayerUI && _following != null;
        if (PlayerPanel != null && PlayerPanel.gameObject.activeSelf != showPanel)
            PlayerPanel.gameObject.SetActive(showPanel);

        if (!showPanel) return;

        AbilitySystemComponent asc = _following.GetComponent<AbilitySystemComponent>();
        if (asc == null) return;

        int team = ResolveTeam(_following);
        if (NameText != null)
        {
            NameText.text  = ResolveName(_following);
            NameText.color = MercUIFactory.TeamColor(team);
        }

        float hp  = asc.GetAttributeValue(EAttributeType.Health);
        float max = asc.GetAttributeValue(EAttributeType.MaxHealth);
        if (max <= 0f) max = 1f;

        if (HealthFill != null) HealthFill.fillAmount = Mathf.Clamp01(hp / max);
        if (HealthText != null) HealthText.text = $"{Mathf.CeilToInt(hp)} / {Mathf.CeilToInt(max)}";
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static int ResolveTeam(PlayerController pc)
    {
        NetworkAbilitySystemComponent net = pc.GetComponent<NetworkAbilitySystemComponent>();
        if (net != null) return net.NetTeamID;

        AbilitySystemComponent asc = pc.GetComponent<AbilitySystemComponent>();
        return asc != null ? asc.TeamID : 0;
    }

    private static string ResolveName(PlayerController pc)
    {
        NetworkAbilitySystemComponent net = pc.GetComponent<NetworkAbilitySystemComponent>();
        string n = net != null ? net.PlayerName : null;

        return string.IsNullOrWhiteSpace(n) ? pc.name : n;
    }
}
