using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ============================================================
// UI_OnScreenKeyboard
//
// El teclado en pantalla, para escribir con el control (26 de octubre de 2026, etapa 2 del
// showcase): la dirección del host en la pantalla de conexión y tu nombre en la sala.
//
// SE ABRE SOLO: con el control, A sobre cualquier campo de texto de los menús lo abre con lo
// que ya tenía escrito. Con el mouse los campos andan como siempre (clic y se escribe con el
// teclado de verdad).
//
// CON EL CONTROL:
//   · El stick recorre las teclas y A aprieta.
//   · X borra, Y pone un espacio.
//   · Start (o la tecla Listo) acepta: lo escrito pasa al campo.
//   · B cancela: el campo queda como estaba.
// Mayús es para UNA letra; al abrirlo vacío arranca prendida (para la inicial del nombre).
//
// LOS CAMPOS YA NO SE "PRENDEN" AL ELEGIRLOS: TextMeshPro, por defecto, se pone a escribir
// apenas el control pasa por encima, y A sobre el campo además lo enviaba (en la conexión eso
// era Conectar). Ahora se escribe solo con un clic o con este teclado.
//
// VIVE EN EL PREFAB DE MENÚS (Menus_UI ▸ Keyboard): el velo, el panel, el título, lo escrito,
// las teclas y la ayuda están armados ahí y se acomodan a mano. Cada tecla lleva su
// UI_KeyboardKey: se pueden mover, copiar o borrar, y el teclado las encuentra solo.
// ============================================================
[DefaultExecutionOrder(-100)]   // antes que el EventSystem: el A que abre el teclado no llega al campo
public class UI_OnScreenKeyboard : MonoBehaviour, IMenuScreen
{
    [Header("Piezas")]
    [Tooltip("El canvas del teclado: se prende al abrir y se apaga al cerrar.")]
    [Label("Canvas del teclado")]
    public GameObject KeyboardCanvas;

    [Tooltip("Qué se está escribiendo (\"Tu nombre\", \"Dirección del host\").")]
    [Label("Título")]
    public TMP_Text TitleText;

    [Tooltip("Lo escrito hasta ahora, con el cursor al final.")]
    [Label("Texto escrito")]
    public TMP_Text PreviewText;

    [Tooltip("Dónde están las teclas (cada una con su UI_KeyboardKey).")]
    [Label("Teclas")]
    public Transform KeysRoot;

    [Tooltip("La tecla elegida al abrir el teclado.")]
    [Label("Tecla inicial")]
    public Button FirstKey;

    [Header("Colores")]
    [Tooltip("El color de la tecla Mayús mientras está prendida (en lugar del \"Normal Color\" de su botón; ver ButtonTint).")]
    [Label("Mayús prendida")]
    public Color ShiftOnColor = new Color(0.95f, 0.65f, 0.20f);

    public static UI_OnScreenKeyboard Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    // El título de cada campo (lo anotan las pantallas que los tienen).
    private static readonly Dictionary<TMP_InputField, string> _titles = new Dictionary<TMP_InputField, string>();

    private UI_KeyboardKey[] _keys = new UI_KeyboardKey[0];
    private readonly Dictionary<UI_KeyboardKey, Color> _shiftColors = new Dictionary<UI_KeyboardKey, Color>();

    private TMP_InputField _target;
    private string _text = "";
    private bool   _shift;
    private int    _openedFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _titles.Clear();
        IsOpen = false;
    }

    // El título que se muestra al escribir en ese campo (una clave de Loc).
    public static void SetTitle(TMP_InputField field, string locKey)
    {
        if (field != null) _titles[field] = locKey;
    }

    // =========================================================
    // CICLO
    // =========================================================

    private void Awake()
    {
        Instance = this;
        if (KeyboardCanvas != null) KeyboardCanvas.SetActive(false);

        Transform root = KeysRoot != null ? KeysRoot : transform;
        _keys = root.GetComponentsInChildren<UI_KeyboardKey>(true);
        foreach (UI_KeyboardKey key in _keys)
        {
            if (key.Button == null) continue;
            UI_KeyboardKey k = key;
            key.Button.onClick.AddListener(AudioManager.Click);
            key.Button.onClick.AddListener(() => Press(k));
            if (key.Kind == EKeyboardKey.Shift) _shiftColors[key] = key.Button.colors.normalColor;
        }

        // Ver la cabecera: elegir un campo ya no lo prende.
        foreach (TMP_InputField field in FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            field.shouldActivateOnSelect = false;
    }

    private void OnDisable()
    {
        if (IsOpen) Close(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (IsOpen)
        {
            // El campo desapareció (se cortó la conexión con la sala abierta): no hay a dónde escribir.
            if (_target == null || !_target.isActiveAndEnabled) { Close(false); return; }

            if (MenuInput.Backspace != null && MenuInput.Backspace.WasPressedThisFrame()) Erase();
            if (MenuInput.Space != null && MenuInput.Space.WasPressedThisFrame()) Type(" ");
            return;
        }

        TryOpenFromPad();
    }

    // A del control sobre un campo de texto. Corre antes que el EventSystem: el teclado se abre
    // y se lleva la selección, así que ese A no le llega al campo (le llega a la primera tecla,
    // que lo ignora: ver Press).
    private void TryOpenFromPad()
    {
        InputAction submit = MenuInput.Submit;
        if (submit == null || !submit.WasPressedThisFrame()) return;
        if (!(submit.activeControl?.device is Gamepad)) return;

        EventSystem es = EventSystem.current;
        GameObject sel = es != null ? es.currentSelectedGameObject : null;
        TMP_InputField field = sel != null ? sel.GetComponent<TMP_InputField>() : null;
        if (field == null || !field.IsInteractable() || field.isFocused) return;

        Open(field);
    }

    // =========================================================
    // ABRIR / CERRAR
    // =========================================================

    public void Open(TMP_InputField field)
    {
        if (IsOpen || field == null) return;

        _target = field;
        _text = field.text ?? "";
        _shift = _text.Length == 0;
        _openedFrame = Time.frameCount;

        if (TitleText != null)
            TitleText.text = _titles.TryGetValue(field, out string key) ? Loc.T(key) : "";

        if (KeyboardCanvas != null) KeyboardCanvas.SetActive(true);
        IsOpen = true;
        Redraw();

        UICursor.Request(this);
        MenuInput.Push(this);
    }

    // accept: lo escrito pasa al campo (y le avisa como si se hubiera terminado de escribir en
    // él: la sala cambia el nombre ahí).
    private void Close(bool accept)
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (KeyboardCanvas != null) KeyboardCanvas.SetActive(false);

        TMP_InputField target = _target;
        _target = null;

        MenuInput.Pop(this);   // la selección vuelve al campo
        UICursor.Release(this);

        if (accept && target != null)
        {
            target.text = _text;
            target.onEndEdit.Invoke(_text);
        }
    }

    // =========================================================
    // TECLAS
    // =========================================================

    private void Press(UI_KeyboardKey key)
    {
        if (!IsOpen || Time.frameCount == _openedFrame) return;   // el A que abrió el teclado no escribe

        switch (key.Kind)
        {
            case EKeyboardKey.Character: Type(key.Character); break;
            case EKeyboardKey.Shift:     _shift = !_shift; Redraw(); break;
            case EKeyboardKey.Space:     Type(" "); break;
            case EKeyboardKey.Backspace: Erase(); break;
            case EKeyboardKey.Done:      Close(true); break;
            case EKeyboardKey.Cancel:    Close(false); break;
        }
    }

    private void Type(string s)
    {
        if (string.IsNullOrEmpty(s)) return;
        if (_shift) s = s.ToUpperInvariant();

        int limit = _target != null ? _target.characterLimit : 0;
        if (limit > 0 && _text.Length + s.Length > limit) return;

        _text += s;
        _shift = false;
        Redraw();
    }

    private void Erase()
    {
        if (_text.Length > 0) _text = _text.Substring(0, _text.Length - 1);
        Redraw();
    }

    private void Redraw()
    {
        // noparse: lo escrito se muestra tal cual, aunque tenga algo que parezca una etiqueta.
        if (PreviewText != null) PreviewText.text = "<noparse>" + _text + "</noparse><alpha=#88>_";

        foreach (UI_KeyboardKey key in _keys)
        {
            if (key.Kind == EKeyboardKey.Character && key.Label != null && key.Character != null)
                key.Label.text = _shift ? key.Character.ToUpperInvariant() : key.Character;
            else if (key.Kind == EKeyboardKey.Shift && _shiftColors.TryGetValue(key, out Color off))
                ButtonTint.Paint(key.Button.targetGraphic, _shift ? ShiftOnColor : off);
        }
    }

    // =========================================================
    // IMenuScreen (capa 2: encima de todo, también de la pausa)
    // =========================================================

    public int MenuLayer => 2;

    public GameObject DefaultSelection
    {
        get
        {
            if (FirstKey != null) return FirstKey.gameObject;
            foreach (UI_KeyboardKey key in _keys)
                if (key.Button != null) return key.Button.gameObject;
            return null;
        }
    }

    public Transform SelectionRoot => KeyboardCanvas != null ? KeyboardCanvas.transform : transform;
    public void OnBack()  => Close(false);
    public void OnPause() => Close(true);
}
