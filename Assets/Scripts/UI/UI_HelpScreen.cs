using System.Text;
using TMPro;
using UnityEngine;

// ============================================================
// UI_HelpScreen
//
// La ventana de ayuda: cubre toda la pantalla. Arriba, las reglas del modo y la clase equipada
// con sus estadísticas; abajo, una tarjeta por habilidad con qué hace y cuánto daña. Con una
// subclase, la tarjeta de la R detalla la definitiva. Se abre con F1 o el stick derecho
// apretado, o desde la pausa.
//
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ HelpScreen), desde el 4 de noviembre de 2026. Lee
// de la clase del jugador local y no toca nada. La partida sigue mientras está abierta, así
// que recibir daño la cierra. Abierta desde la pausa, volver vuelve a la pausa.
// ============================================================
public class UI_HelpScreen : MonoBehaviour, IMenuScreen
{
    // El orden de las tarjetas, de izquierda a derecha y de arriba abajo.
    private static readonly EAbilityInput[] CardSlots =
    {
        EAbilityInput.PrimaryAttack, EAbilityInput.SecondaryAttack, EAbilityInput.Movement,
        EAbilityInput.Action1, EAbilityInput.Action2,   EAbilityInput.Action3,
    };

    [Header("Piezas")]
    [Tooltip("Toda la ventana: se esconde entera cuando no se usa.")]
    [Label("Panel")]
    public RectTransform Panel;

    [Tooltip("Las reglas del modo: tres, en orden.")]
    [Label("Reglas")]
    public TMP_Text[] Rules = new TMP_Text[3];

    [Tooltip("La parte de la clase. Se apaga para el espectador, que no tiene una.")]
    [Label("Bloque de la clase")]
    public GameObject ClassBlock;

    [Label("Ícono de la clase")]
    public UnityEngine.UI.Image ClassIcon;

    [Label("Nombre de la clase")]
    public TMP_Text ClassName;

    [Label("Rol")]
    public TMP_Text ClassRole;

    [Label("Descripción de la clase")]
    public TMP_Text ClassDescription;

    [Tooltip("Las estadísticas, en dos columnas de texto.")]
    [Label("Estadísticas")]
    public TMP_Text[] Stats = new TMP_Text[2];

    [Tooltip("Una por botón, en orden: clic izquierdo, clic derecho, Shift, Q, E y R.")]
    [Label("Tarjetas")]
    public UI_HelpAbilityCard[] Cards = new UI_HelpAbilityCard[6];

    [Header("Reglas")]
    [Tooltip("Se cierra sola si te pegan: la partida sigue detrás y no se ve qué pasa.")]
    [Label("Cerrar al recibir daño")]
    public bool CloseOnDamage = true;

    private static UI_HelpScreen _instance;
    private bool _open;
    private float _lastHealth;
    private System.Action _onBack;

    public static UI_HelpScreen Get()
    {
        if (_instance == null) _instance = FindFirstObjectByType<UI_HelpScreen>(FindObjectsInactive.Include);
        return _instance;
    }

    private void Awake()
    {
        _instance = this;
        if (Panel != null) Panel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_open) Close();
    }

    private void Update()
    {
        if (_open && (!MatchRunning() || TookDamage())) { Close(); return; }

        if (MenuInput.Help == null || !MenuInput.Help.WasPressedThisFrame()) return;

        if (_open) Back();
        else if (!MenuInput.HasOverlay) Open();
    }

    // La vida bajó desde el último cuadro, sin contar el recorte al máximo cuando se acaba un
    // bono de vida (como en UI_ClassMenu).
    private bool TookDamage()
    {
        AbilitySystemComponent asc = LocalAsc();
        if (asc == null) return false;

        float health = asc.GetAttributeValue(EAttributeType.Health);
        bool clampedToMax = health >= asc.GetAttributeValue(EAttributeType.MaxHealth) - 0.01f;
        bool damaged = health < _lastHealth - 0.01f && !clampedToMax;
        _lastHealth = health;
        return damaged && CloseOnDamage;
    }

    private static AbilitySystemComponent LocalAsc()
    {
        PlayerController player = PlayerController.LocalPlayer;
        return player != null ? player.GetComponent<AbilitySystemComponent>() : null;
    }

    private static bool MatchRunning()
    {
        if (MercenariesGameMode.Instance == null) return false;

        LobbyManager lobby = LobbyManager.Instance;
        return lobby == null || lobby.MatchStarted;
    }

    // onBack: qué hacer al volver con el botón (la pausa se reabre así). Si la cierra un golpe,
    // no corre: se vuelve directo al juego.
    public void Open(System.Action onBack = null)
    {
        if (_open || Panel == null || !MatchRunning()) return;

        _open = true;
        _onBack = onBack;
        AbilitySystemComponent asc = LocalAsc();
        _lastHealth = asc != null ? asc.GetAttributeValue(EAttributeType.Health) : 0f;
        Refresh();
        Panel.gameObject.SetActive(true);
        UICursor.Request(this);
        MenuInput.Push(this);
    }

    public void Close()
    {
        if (!_open) return;

        _open = false;
        if (Panel != null) Panel.gameObject.SetActive(false);
        MenuInput.Pop(this);
        UICursor.Release(this);
    }

    private void Back()
    {
        System.Action onBack = _onBack;
        Close();
        onBack?.Invoke();
    }

    private void Refresh()
    {
        MercenariesGameMode gm = MercenariesGameMode.Instance;
        string[] rules =
        {
            Loc.T("help.rule1"),
            Loc.T("help.rule2"),
            Loc.T("help.rule3", ("points", gm != null ? gm.PointsToWin : 0)),
        };
        for (int i = 0; i < Rules.Length && i < rules.Length; i++)
            if (Rules[i] != null) Rules[i].text = rules[i];

        PlayerController player = PlayerController.LocalPlayer;
        CharacterClassDefinition def = player != null ? player.CurrentClassDef : null;

        if (ClassBlock != null) ClassBlock.SetActive(def != null);
        foreach (UI_HelpAbilityCard card in Cards)
            if (card != null) card.gameObject.SetActive(def != null);
        if (def == null) return;

        AbilitySystemComponent asc = player.GetComponent<AbilitySystemComponent>();

        if (ClassIcon != null)
        {
            ClassIcon.sprite  = def.ClassIcon;
            ClassIcon.enabled = def.ClassIcon != null;
        }
        if (ClassName != null) ClassName.text = def.DisplayName;
        if (ClassRole != null) ClassRole.text = RoleName(def.DisplayRole);
        if (ClassDescription != null) ClassDescription.text = def.DisplayDescription;

        ShowStats(asc);

        for (int i = 0; i < Cards.Length && i < CardSlots.Length; i++)
            if (Cards[i] != null) Cards[i].Show(InputGlyphs.For(CardSlots[i]), AbilityIn(def, CardSlots[i]), asc);
    }

    private static GameplayAbility AbilityIn(CharacterClassDefinition def, EAbilityInput slot)
    {
        if (def.Abilities == null) return null;

        foreach (CharacterClassDefinition.AbilityAssignment assignment in def.Abilities)
            if (assignment.InputSlot == slot) return assignment.Ability;
        return null;
    }

    private static string RoleName(EClassRole role)
    {
        switch (role)
        {
            case EClassRole.Tank:    return Loc.T("help.role.tank");
            case EClassRole.Damage:  return Loc.T("help.role.damage");
            case EClassRole.Support: return Loc.T("help.role.support");
            default:                 return "";
        }
    }

    private void ShowStats(AbilitySystemComponent asc)
    {
        var left  = new StringBuilder();
        var right = new StringBuilder();

        AddStat(left, "help.stat.health", asc, EAttributeType.MaxHealth);
        AddStat(left, "help.stat.attack", asc, EAttributeType.Attack);
        AddStat(left, "help.stat.magic",  asc, EAttributeType.MagicDamage);
        AddStat(left, "help.stat.armor",  asc, EAttributeType.Def);

        AddStat(right, "help.stat.speed",        asc, EAttributeType.MovSpeed);
        AddStat(right, "help.stat.attack_speed", asc, EAttributeType.AtkSpeed, "0.##");
        AddStat(right, "help.stat.mana",         asc, EAttributeType.MaxMana);
        AddStat(right, "help.stat.energy",       asc, EAttributeType.MaxEnergy);

        if (Stats.Length > 0 && Stats[0] != null) Stats[0].text = left.ToString();
        if (Stats.Length > 1 && Stats[1] != null) Stats[1].text = right.ToString();
    }

    // Lo que esta clase no usa (el maná de un Guerrero) no se muestra.
    private static void AddStat(StringBuilder text, string key, AbilitySystemComponent asc, EAttributeType attribute,
                                string format = "0.#")
    {
        float value = asc != null ? asc.GetAttributeValue(attribute) : 0f;
        if (value <= 0f) return;
        text.AppendLine(Loc.T(key, ("value", value.ToString(format))));
    }

    // =========================================================
    // IMenuScreen
    // =========================================================

    public int MenuLayer => 1;
    public GameObject DefaultSelection => null;
    public Transform SelectionRoot => Panel;
    public void OnBack()  => Back();
    public void OnPause() => Back();
}
