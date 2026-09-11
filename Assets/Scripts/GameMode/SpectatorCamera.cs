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
// ============================================================
public class SpectatorCamera : MonoBehaviour
{
    [Header("Vuelo libre")]
    public float MoveSpeed       = 16f;
    [Tooltip("Cuánto multiplica la velocidad el Shift.")]
    public float FastMultiplier  = 3f;
    public float LookSensitivity = 2.4f;

    [Header("Dónde arranca")]
    [Tooltip("Altura sobre el centro de la arena. Pensada para ver el centro entero de " +
             "una, más o menos desde donde mira la cámara de la sala.")]
    public float StartHeight   = 26f;
    [Tooltip("Cuánto se aleja del centro hacia atrás para tener perspectiva.")]
    public float StartDistance = 40f;

    [Header("Seguir a un jugador")]
    [Tooltip("Dónde se pone la cámara respecto del jugador que sigue: atrás y arriba, " +
             "como una cámara de hombro.")]
    public Vector3 FollowOffset = new Vector3(0f, 2.4f, -4.8f);
    [Tooltip("Qué tan rápido persigue. Bajo = más suave, y en una grabación se nota.")]
    public float FollowLerp = 8f;

    [Header("Teclas")]
    public KeyCode FreeCamKey        = KeyCode.F;
    public KeyCode TogglePlayerUIKey = KeyCode.H;
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

    // --- UI, dibujada por código como el resto del HUD del modo ---
    private Canvas          _canvas;
    private RectTransform   _panel;
    private TextMeshProUGUI _nameText;
    private TextMeshProUGUI _healthText;
    private Image           _healthFill;
    private TextMeshProUGUI _hintText;

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
        if (_canvas != null) _canvas.gameObject.SetActive(false);

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
        if (_canvas != null) { _canvas.gameObject.SetActive(true); return; }

        _canvas = MercUIFactory.CreateCanvas("Canvas_Spectator", 60);

        _panel = MercUIFactory.CreateRect(_canvas.transform, "Observado",
                                          new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                          new Vector2(0.5f, 0f), new Vector2(0f, 90f),
                                          new Vector2(460f, 74f));

        Image bg = _panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.07f, 0.10f, 0.72f);

        _nameText = MercUIFactory.CreateText(_panel, "Nombre", "", 26f, Color.white,
                                             TextAlignmentOptions.Center,
                                             new Vector2(0f, 18f), new Vector2(440f, 32f),
                                             new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f));

        RectTransform barBack = MercUIFactory.CreateRect(_panel, "BarraFondo",
                                                         new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                                         new Vector2(0.5f, 0.5f), new Vector2(0f, -14f),
                                                         new Vector2(400f, 16f));
        Image backImg = barBack.gameObject.AddComponent<Image>();
        backImg.color = new Color(0.15f, 0.16f, 0.20f, 1f);

        RectTransform fill = MercUIFactory.CreateRect(barBack, "BarraRelleno",
                                                      new Vector2(0f, 0f), new Vector2(1f, 1f),
                                                      new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        _healthFill = fill.gameObject.AddComponent<Image>();
        _healthFill.sprite    = MercUIFactory.WhiteSprite;
        _healthFill.type      = Image.Type.Filled;
        _healthFill.fillMethod = Image.FillMethod.Horizontal;
        _healthFill.color     = new Color(0.45f, 0.85f, 0.45f, 1f);

        _healthText = MercUIFactory.CreateText(_panel, "Vida", "", 16f, Color.white,
                                               TextAlignmentOptions.Center,
                                               new Vector2(0f, -14f), new Vector2(400f, 20f),
                                               new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                               new Vector2(0.5f, 0.5f));

        _hintText = MercUIFactory.CreateText(_canvas.transform, "Ayuda", "", 15f,
                                             new Color(0.85f, 0.87f, 0.92f, 0.75f),
                                             TextAlignmentOptions.Right,
                                             new Vector2(-24f, 24f), new Vector2(560f, 24f),
                                             new Vector2(1f, 0f), new Vector2(1f, 0f),
                                             new Vector2(1f, 0f));
    }

    private void RefreshUI()
    {
        if (_hintText != null)
        {
            _hintText.text = _following != null
                ? $"Clic: cambiar de jugador · {FreeCamKey}: cámara libre · {TogglePlayerUIKey}: datos · {ScoreboardKey}: marcador"
                : $"WASD + Espacio/Ctrl · Shift: rápido · Clic: seguir a un jugador · {ScoreboardKey}: marcador";
        }

        bool showPanel = _showPlayerUI && _following != null;
        if (_panel != null && _panel.gameObject.activeSelf != showPanel)
            _panel.gameObject.SetActive(showPanel);

        if (!showPanel) return;

        AbilitySystemComponent asc = _following.GetComponent<AbilitySystemComponent>();
        if (asc == null) return;

        int team = ResolveTeam(_following);
        if (_nameText != null)
        {
            _nameText.text  = ResolveName(_following);
            _nameText.color = MercUIFactory.TeamColor(team);
        }

        float hp  = asc.GetAttributeValue(EAttributeType.Health);
        float max = asc.GetAttributeValue(EAttributeType.MaxHealth);
        if (max <= 0f) max = 1f;

        if (_healthFill != null) _healthFill.fillAmount = Mathf.Clamp01(hp / max);
        if (_healthText != null) _healthText.text = $"{Mathf.CeilToInt(hp)} / {Mathf.CeilToInt(max)}";
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
