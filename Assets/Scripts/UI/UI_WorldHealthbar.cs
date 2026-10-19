using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// ============================================================
// UI_WorldHealthbar
//
// "Nameplate" WORLD-SPACE sobre la cabeza de cada personaje: barra de vida
// coloreada por equipo, sus buffs/debuffs, y (para enemigos) el aviso de Crítico
// mejorado. Reemplazó al viejo HealthBarNPC de la game jam, que ya no existe.
//
// LA BARRA ES UN PREFAB (desde el 19 de octubre de 2026): Prefabs/UI/Nameplate, con su
// UI_Nameplate (las piezas y cómo se ve). Cada personaje lleva una instancia a la altura de
// su cabeza y este componente, en su raíz, la encuentra sola. Cambiar cómo se ven las vidas
// es cambiar ese prefab: vale para todos.
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
// ============================================================
[RequireComponent(typeof(AbilitySystemComponent))]
public class UI_WorldHealthbar : MonoBehaviour
{
    [Tooltip("La barra (una instancia del prefab Prefabs/UI/Nameplate, hija de este personaje). " +
             "Vacío = la busca sola entre los hijos.")]
    [Label("Barra (prefab Nameplate)")]
    public UI_Nameplate Nameplate;

    // =========================================================
    // DATOS VIEJOS: la barra armada a mano en cada prefab, antes del prefab Nameplate. Si un
    // prefab todavía no tiene su Nameplate, se usan estos (y con ellos la herramienta encuentra
    // la barra vieja para reemplazarla). Con Nameplate, se llenan solos desde él.
    // =========================================================
    [HideInInspector] public Transform BarRoot;
    [HideInInspector] public Image HealthFill;
    [HideInInspector] public Slider HealthSlider;
    [HideInInspector] public UI_EffectContainer EffectContainer;
    [HideInInspector] public GameObject FirstStrikeMarker;
    [HideInInspector] public TMPro.TMP_Text NameText;
    [HideInInspector] public Color AllyColor  = new Color(0.2f, 0.9f, 0.2f, 1f);
    [HideInInspector] public Color EnemyColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    [HideInInspector] public LayerMask ObstacleLayer;

    // De quién se MUESTRAN los datos (vida, nombre, buffs, clase): el propio personaje, o el
    // que imita (ver Mirror). _ownAsc es siempre el propio: con él se decide si se esconde
    // (invisible para los enemigos).
    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;
    private PlayerController               _pc;
    private AbilitySystemComponent        _ownAsc;

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

    // El ícono de la clase (una pieza del prefab Nameplate) y el sprite que muestra ahora.
    private Image  _classIcon;
    private Sprite _shownClassSprite;

    private void Awake()
    {
        _asc    = GetComponent<AbilitySystemComponent>();
        _netAsc = GetComponent<NetworkAbilitySystemComponent>();
        _pc     = GetComponent<PlayerController>();
        _ownAsc = _asc;

        // Las piezas salen del prefab Nameplate.
        if (Nameplate == null) Nameplate = GetComponentInChildren<UI_Nameplate>(true);
        if (Nameplate != null)
        {
            BarRoot           = Nameplate.transform;
            HealthFill        = Nameplate.HealthFill;
            HealthSlider      = Nameplate.HealthSlider;
            EffectContainer   = Nameplate.EffectContainer;
            FirstStrikeMarker = Nameplate.FirstStrikeMarker;
            NameText          = Nameplate.NameText;
            AllyColor         = Nameplate.AllyColor;
            EnemyColor        = Nameplate.EnemyColor;
            ObstacleLayer     = Nameplate.ObstacleLayer;
            _classIcon        = Nameplate.ShowClassIcon ? Nameplate.ClassIcon : null;
            if (Nameplate.ClassIcon != null) Nameplate.ClassIcon.enabled = false;   // hasta que haya clase
        }

        // Sin nombre hasta que llegue (un NPC no tiene): que no se vea el texto de ejemplo del prefab.
        if (NameText != null) NameText.text = "";

        if (EffectContainer != null) EffectContainer.SetTargetASC(_asc);

        if (_xrayMaterial == null)
        {
            Shader s = Shader.Find("UI/AlwaysOnTop");
            if (s != null) _xrayMaterial = new Material(s);
        }

        RefreshGraphics();
    }

    // Muestra los datos de OTRO personaje: su vida, su nombre, sus buffs y su ícono de clase.
    // La copia del Ilusionista lo usa con el jugador que copia (19 de octubre de 2026, pedido
    // de Gustavo): un señuelo sin barra, o con una barra distinta, se reconocía de lejos. La
    // barra sigue siendo de ESTE objeto (aparece sobre él y se tapa con sus paredes).
    public void Mirror(GameObject source)
    {
        if (source == null) return;
        AbilitySystemComponent asc = source.GetComponent<AbilitySystemComponent>();
        if (asc == null) return;

        _asc    = asc;
        _netAsc = source.GetComponent<NetworkAbilitySystemComponent>();
        _pc     = source.GetComponent<PlayerController>();

        if (EffectContainer != null) EffectContainer.SetTargetASC(_asc);
        _shownClassSprite = null;
        RefreshGraphics();
        _lastWasAlly = null;
    }

    // La clase puede cambiar en cualquier momento (cambio de clase, evolución): se mira
    // cada frame, pero el sprite solo se reescribe si cambió. Un NPC no tiene clase: apagado.
    private void UpdateClassIcon()
    {
        if (_classIcon == null) return;

        Sprite sprite = _pc != null && _pc.CurrentClassDef != null ? _pc.CurrentClassDef.ClassIcon : null;
        if (sprite == _shownClassSprite && _classIcon.enabled == (sprite != null)) return;

        _shownClassSprite  = sprite;
        _classIcon.sprite  = sprite;
        _classIcon.enabled = sprite != null;
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

        // Uno mismo: sin nameplate (para eso está el HUD). Una copia que te imita sí lo muestra.
        PlayerController local = PlayerController.LocalPlayer;
        if (local != null && local.gameObject == gameObject) { SetVisible(false); return; }

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
        if (isEnemy && _ownAsc != null && _ownAsc.IsHiddenFromEnemies)
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
        UpdateClassIcon();
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
