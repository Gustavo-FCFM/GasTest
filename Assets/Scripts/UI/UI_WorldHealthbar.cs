using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// ============================================================
// UI_WorldHealthbar
//
// "Nameplate" WORLD-SPACE sobre la cabeza de cada personaje: barra de vida
// coloreada por equipo, sus buffs/debuffs, y (para enemigos) el aviso de Crítico
// mejorado. Reemplaza al viejo HealthBarNPC, que solo hacía vida + billboard.
//
// REGLAS DE VISIBILIDAD (según la relación con el jugador LOCAL):
//  - Aliado (mismo TeamID) → barra VERDE, se ve a TRAVÉS de las paredes.
//  - Enemigo               → barra ROJA, se OCULTA tras las paredes.
//  - Uno mismo             → oculto (ya tenés el HUD).
//
// El canvas es World Space (escala con la distancia, se ve natural). Como un
// canvas world-space normal se TAPA con la geometría, el X-ray de los aliados se
// logra con un material ZTest Always (shader "UI/AlwaysOnTop") que se aplica a sus
// gráficos; a los enemigos se los oculta con un raycast contra el entorno.
//
// EN RED: no necesita nada propio. Cada cliente corre esto sobre TODAS las copias
// de personajes que ve y las evalúa con SU cámara y SU jugador local, así el mismo
// personaje sale rojo/oculto para el enemigo y verde/X-ray para el aliado. La vida
// y los efectos salen de lo ya sincronizado (NetworkASC). Sirve también para NPCs
// (sin NetworkASC lee el ASC local directo).
//
// SETUP (prefab del Player): este componente va en la RAÍZ (junto al ASC), y se le
// asignan las referencias del canvas world-space hijo (el que ya tenías con el
// HealthBarNPC — reemplazá ese script por este).
// ============================================================
[RequireComponent(typeof(AbilitySystemComponent))]
public class UI_WorldHealthbar : MonoBehaviour
{
    [Header("Canvas del Nameplate (hijo, World Space)")]
    [Tooltip("Raíz del nameplate: se orienta a la cámara (billboard) y se prende/apaga.")]
    public Transform BarRoot;

    [Header("UI")]
    [Tooltip("Barra de vida (Image Type: Filled). Se usa su fillAmount y su color.")]
    public Image HealthFill;
    [Tooltip("Alternativa/complemento: un Slider de vida. Opcional.")]
    public Slider HealthSlider;
    [Tooltip("Muestra los buffs/debuffs del personaje. Se le setea el TargetASC solo. Opcional.")]
    public UI_EffectContainer EffectContainer;
    [Tooltip("Aviso de Crítico mejorado disponible contra ESTE enemigo. Solo en enemigos. Opcional.")]
    public GameObject FirstStrikeMarker;
    [Tooltip("Nombre del jugador (el que eligió al entrar). Se pinta del color de su equipo. Opcional.")]
    public TMPro.TMP_Text NameText;

    [Header("Colores por Equipo")]
    public Color AllyColor  = new Color(0.2f, 0.9f, 0.2f, 1f);
    public Color EnemyColor = new Color(0.9f, 0.2f, 0.2f, 1f);

    [Header("Oclusión")]
    [Tooltip("Capas del entorno que TAPAN la barra de un enemigo (paredes/piso). Los aliados no se ocultan.")]
    public LayerMask ObstacleLayer;

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;
    private PlayerController               _pc;

    // Material ZTest Always para el X-ray de aliados (se crea en runtime desde el
    // shader). Los gráficos del nameplate alternan entre este (aliado) y su
    // material original (enemigo).
    private static Material _xrayMaterial;
    private Graphic[] _graphics;
    private int _lastEffectChildCount = -1;
    private bool? _lastWasAlly; // para no re-aplicar material si no cambió

    // Cachés de LateUpdate (ver la nota ahí): la cámara y el ASC del jugador local no
    // cambian entre frames, y esto corre una vez por personaje en pantalla.
    private Camera                 _cam;
    private PlayerController              _cachedLocal;
    private AbilitySystemComponent        _cachedLocalASC;
    private NetworkAbilitySystemComponent _cachedLocalNet;

    private void Awake()
    {
        _asc    = GetComponent<AbilitySystemComponent>();
        _netAsc = GetComponent<NetworkAbilitySystemComponent>();
        _pc     = GetComponent<PlayerController>();

        if (EffectContainer != null) EffectContainer.SetTargetASC(_asc);

        if (_xrayMaterial == null)
        {
            Shader s = Shader.Find("UI/AlwaysOnTop");
            if (s != null) _xrayMaterial = new Material(s);
        }

        RefreshGraphics();
    }

    // LateUpdate: después de que la cámara se movió, para que el billboard no
    // quede un frame atrás.
    private void LateUpdate()
    {
        // Cacheados: esto corre CADA frame y hay un nameplate por personaje, así que
        // con una partida llena se multiplica por 9. Camera.main hace una búsqueda por
        // tag y GetComponent recorre los componentes del jugador — ninguno de los dos
        // cambia entre frames, así que se resuelven una sola vez.
        // Se re-resuelve también si la cámara quedó DESACTIVADA, no solo si murió. Al
        // spawnear, PlayerController apaga la cámara del lobby (Camera.main.SetActive
        // false) — y un componente apagado NO es == null, así que con el chequeo de
        // null a secas los nameplates se quedaban orientándose hacia la cámara del
        // lobby para siempre: se veían como carteles fijos, bien desde un ángulo y mal
        // desde cualquier otro. Mismo criterio que PlayerController.MainCamera.
        if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
        Camera cam = _cam;
        if (cam == null || BarRoot == null) { SetVisible(false); return; }

        // Uno mismo: sin nameplate (para eso está el HUD).
        PlayerController local = PlayerController.LocalPlayer;
        if (local != null && _pc == local) { SetVisible(false); return; }

        // Relación con el jugador local. Sin jugador local no hay a quién comparar.
        // El caché se rehace si cambia el jugador local (respawn, cambio de clase).
        if (local != _cachedLocal)
        {
            _cachedLocal    = local;
            _cachedLocalASC = local != null ? local.GetComponent<AbilitySystemComponent>() : null;
            _cachedLocalNet = local != null ? local.GetComponent<NetworkAbilitySystemComponent>() : null;
        }
        AbilitySystemComponent localASC = _cachedLocalASC;
        bool isEnemy = localASC == null || localASC.IsEnemyOf(_asc);

        // Invisibilidad: si el modelo no se ve para los enemigos, su nameplate tampoco
        // debe verse — una barra de vida flotando delata la posición exacta y hacía
        // inútil la invisibilidad. Los ALIADOS sí lo siguen viendo, y además le notan
        // el buff en su barra de efectos.
        if (isEnemy && _asc != null && _asc.HasTag(EGameplayTag.Status_Invisible))
        {
            SetVisible(false);
            return;
        }

        // Oclusión: solo los enemigos se tapan con el entorno.
        if (isEnemy && Physics.Linecast(cam.transform.position, BarRoot.position, ObstacleLayer, QueryTriggerInteraction.Ignore))
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        // Billboard: encarar la cámara (plano de pantalla, sin inclinarse).
        BarRoot.forward = cam.transform.forward;

        ApplyXray(!isEnemy);
        UpdateHealth(isEnemy);
        UpdateFirstStrikeMarker(localASC, isEnemy);
    }

    // Actualiza el relleno de vida (de los datos sincronizados) y el color por equipo.
    private void UpdateHealth(bool isEnemy)
    {
        float health = _netAsc != null ? _netAsc.NetHealth    : _asc.GetAttributeValue(EAttributeType.Health);
        float max    = _netAsc != null ? _netAsc.NetMaxHealth : _asc.GetAttributeValue(EAttributeType.MaxHealth);
        float fill   = max > 0f ? Mathf.Clamp01(health / max) : 0f;

        Color color = isEnemy ? EnemyColor : AllyColor;

        if (HealthFill != null)   { HealthFill.fillAmount = fill; HealthFill.color = color; }
        if (HealthSlider != null) HealthSlider.value = fill;

        // Nombre del jugador, del mismo color que su barra (verde aliado / rojo
        // enemigo). Solo se reescribe si cambió: setear text en TMP fuerza un
        // remesh, y esto corre cada frame.
        if (NameText != null && _netAsc != null)
        {
            string playerName = _netAsc.PlayerName;
            if (NameText.text != playerName) NameText.text = playerName;
            NameText.color = color;
        }
    }

    // El marcador solo aplica a ENEMIGOS: se prende si el jugador local puede
    // clavarle un Crítico mejorado a ESTE enemigo. El gate global sale del
    // NetworkASC del local (sincronizado, sirve en cliente remoto); la frescura
    // por-objetivo solo es exacta en el host (ver IsFirstStrikeReadyAgainst).
    private void UpdateFirstStrikeMarker(AbilitySystemComponent localASC, bool isEnemy)
    {
        if (FirstStrikeMarker == null) return;

        bool show = false;
        if (isEnemy && localASC != null)
        {
            // Cacheado junto al ASC local (ver LateUpdate): también corría cada frame.
            bool globalReady = _cachedLocalNet != null ? _cachedLocalNet.NetFirstStrikeReady
                                                       : localASC.IsFirstStrikeReady;
            show = globalReady && localASC.IsFirstStrikeReadyAgainst(_asc);
        }

        if (FirstStrikeMarker.activeSelf != show) FirstStrikeMarker.SetActive(show);
    }

    // Aplica (o quita) el material X-ray a todos los gráficos del nameplate según
    // sea aliado. Setear Graphic.material al MISMO valor no hace nada (el setter
    // corta si no cambió), así que es barato llamarlo seguido. Re-recolectamos los
    // gráficos cuando el contenedor de efectos crea/destruye iconos.
    private void ApplyXray(bool isAlly)
    {
        if (_xrayMaterial == null) return; // shader no disponible → aliados sin X-ray (se comportan como enemigos)

        int effectChildren = EffectContainer != null ? EffectContainer.transform.childCount : 0;
        if (effectChildren != _lastEffectChildCount)
        {
            RefreshGraphics();
            _lastEffectChildCount = effectChildren;
            _lastWasAlly = null; // forzar re-aplicar sobre los gráficos nuevos
        }

        if (_lastWasAlly == isAlly) return;
        _lastWasAlly = isAlly;

        Material mat = isAlly ? _xrayMaterial : null; // null = material UI por defecto
        foreach (Graphic g in _graphics)
            if (g != null) g.material = mat;
    }

    private void RefreshGraphics()
    {
        _graphics = BarRoot != null
            ? BarRoot.GetComponentsInChildren<Graphic>(true)
            : new Graphic[0];
    }

    // Prende/apaga todo el nameplate de una.
    private void SetVisible(bool visible)
    {
        if (BarRoot != null && BarRoot.gameObject.activeSelf != visible)
            BarRoot.gameObject.SetActive(visible);
    }
}
