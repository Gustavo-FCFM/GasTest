using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
// UI_PlayerHUD
//
// HUD principal del jugador: barra de vida/escudo/maná, los slots
// de habilidades, y la notificación de nivel máximo. En multijugador
// hay un PlayerController por jugador en la escena, así que
// InitializeHUD() ignora cualquier llamada que no venga del dueño
// local (IsOwner) para no engancharse al personaje de otro jugador.
// ============================================================
public class UI_PlayerHUD : MonoBehaviour
{
    [Header("Target")]
    public PlayerController player;
    private AbilitySystemComponent asc;

    // Los cooldowns reales viven en NetworkAbilitySystemComponent — el ASC
    // local del cliente nunca tiene ActiveEffects poblado salvo en el
    // host, así que los slots de habilidad necesitan esto para mostrar el
    // cooldown correcto en cualquier cliente.
    private NetworkAbilitySystemComponent netAsc;

    [Header("Panel Izquierdo (Estado)")]
    public Image ClassIconImage;
    public Slider HealthSlider;
    public Slider ShieldSlider;
    public TextMeshProUGUI HealthText;

    [Header("Barra de Maná (Opcional)")]
    public GameObject ManaBarContainer; // Se oculta entero si la clase no usa maná
    public Slider ManaSlider;
    public TextMeshProUGUI ManaText;

    [Header("Barra de Energía (Opcional)")]
    [Tooltip("Recurso de las mecánicas de BLOQUEO (el escudo del Paladín/Guerrero). Igual que la " +
             "de maná, el contenedor entero se oculta en las clases que no la usan — o sea, en las " +
             "que su AttributeSetDefinition no define MaxEnergy.")]
    public GameObject EnergyBarContainer;
    public Slider EnergySlider;
    public TextMeshProUGUI EnergyText;

    [Header("Panel Derecho (Habilidades)")]
    public UI_AbilitySlot slotShift;
    public UI_AbilitySlot slotQ;
    public UI_AbilitySlot slotE;
    public UI_AbilitySlot slotLMB;
    public UI_AbilitySlot slotRMB;

    [Header("Panel Central (Ultimate)")]
    public UI_UltimateSlot UltimateSlot;

    [Header("Barra de Efectos (Buffs/Debuffs)")]
    // Contenedor de iconos de buffs/debuffs del jugador local. Su TargetASC
    // NO se puede setear en el prefab (el jugador se spawnea en runtime), así
    // que lo enganchamos acá en InitializeHUD. Arrastrá acá el UI_EffectContainer
    // del HUD (el que muestra los efectos propios, no el de los enemigos).
    public UI_EffectContainer EffectContainer;

    [Header("Notificaciones")]
    public GameObject LevelUpNotification;

    [Tooltip("Se prende cuando el Crítico mejorado (Asesino) está disponible. Solo aparece si el " +
             "jugador tiene esa pasiva, así que en las demás clases queda siempre oculto. Opcional.")]
    public GameObject FirstStrikeReadyIndicator;

    // =========================================================
    // INICIALIZACIÓN
    // =========================================================

    // Conecta el HUD a un jugador: solo acepta la llamada si ese jugador
    // es el dueño local (evita que el HUD de un cliente se enganche al
    // personaje de otro jugador). La llama PlayerController al spawnear
    // o al cambiar de clase.
    public void InitializeHUD(PlayerController ownerPlayer)
    {
        if (ownerPlayer != null && !ownerPlayer.IsOwner) return;

        player = ownerPlayer;

        if (player != null)
        {
            if (asc != null) asc.OnMaxLevelReached -= ShowLevelUpNotification;
            asc    = player.GetComponent<AbilitySystemComponent>();
            netAsc = player.GetComponent<NetworkAbilitySystemComponent>();

            if (asc != null)
                asc.OnMaxLevelReached += ShowLevelUpNotification;

            // Enganchar la barra de buffs/debuffs al jugador local. Sin esto su
            // TargetASC queda null y UI_EffectContainer.Update() corta al toque,
            // por eso los efectos se aplicaban en el ASC pero no se veían.
            if (EffectContainer != null && asc != null)
                EffectContainer.SetTargetASC(asc);

            if (ClassIconImage != null)
            {
                if (player.CharacterIcon != null)
                {
                    ClassIconImage.sprite  = player.CharacterIcon;
                    ClassIconImage.enabled = true;
                }
                else
                {
                    ClassIconImage.enabled = false;
                }
            }

            InitializeAbilitySlots();
            CheckIfManaExists();
            CheckIfEnergyExists();

            if (LevelUpNotification != null) LevelUpNotification.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (asc != null) asc.OnMaxLevelReached -= ShowLevelUpNotification;
    }

    // =========================================================
    // UNITY
    // =========================================================

    // Refresca vida y maná cada frame mientras haya un ASC asignado.
    void Update()
    {
        if (asc == null) return;
        UpdateHealthUI();
        UpdateManaUI();
        UpdateEnergyUI();
        UpdateFirstStrikeIndicator();
    }

    // Prende/apaga el indicador de "Crítico mejorado listo". El estado real vive
    // en el servidor, así que lo leemos del NetworkASC (sincronizado) cuando hay
    // red, y del ASC local cuando no. En clases sin la pasiva siempre es false, así
    // que el indicador queda oculto solo.
    void UpdateFirstStrikeIndicator()
    {
        if (FirstStrikeReadyIndicator == null) return;

        bool ready = netAsc != null ? netAsc.NetFirstStrikeReady : asc.IsFirstStrikeReady;
        if (FirstStrikeReadyIndicator.activeSelf != ready)
            FirstStrikeReadyIndicator.SetActive(ready);
    }

    // =========================================================
    // VIDA Y MANÁ
    // =========================================================

    // Actualiza la barra de vida, el escudo (si tiene) y el texto numérico.
    void UpdateHealthUI()
    {
        float currentHealth = asc.GetAttributeValue(EAttributeType.Health);
        float maxHealth     = asc.GetAttributeValue(EAttributeType.MaxHealth);
        float currentShield = asc.GetAttributeValue(EAttributeType.Shield);

        if (maxHealth <= 0) maxHealth = 1;

        if (HealthSlider) HealthSlider.value = currentHealth / maxHealth;

        if (ShieldSlider)
        {
            ShieldSlider.value = currentShield / maxHealth;
            ShieldSlider.gameObject.SetActive(currentShield > 0);
        }

        if (HealthText)
        {
            HealthText.text = currentShield > 0
                ? $"{currentHealth:F0} (+{currentShield:F0}) / {maxHealth:F0}"
                : $"{currentHealth:F0} / {maxHealth:F0}";
        }
    }

    // Actualiza la barra de maná, si el panel está activo.
    void UpdateManaUI()
    {
        if (ManaBarContainer != null && !ManaBarContainer.activeSelf) return;

        float currentMana = asc.GetAttributeValue(EAttributeType.Mana);
        float maxMana     = asc.GetAttributeValue(EAttributeType.MaxMana);
        if (maxMana <= 0) maxMana = 1;

        if (ManaSlider) ManaSlider.value = currentMana / maxMana;
        if (ManaText)   ManaText.text    = $"{currentMana:F0} / {maxMana:F0}";
    }

    // Oculta el panel de maná entero si la clase actual no tiene MaxMana
    // (ej: clases que no gastan maná).
    void CheckIfManaExists()
    {
        if (ManaBarContainer == null || asc == null) return;
        bool hasMana = asc.GetAttributeValue(EAttributeType.MaxMana) > 0;
        ManaBarContainer.SetActive(hasMana);
    }

    // =========================================================
    // ENERGÍA (recurso de bloqueo)
    // =========================================================

    // Actualiza la barra de energía, si el panel está activo. La energía la
    // consume el escudo al frenar daño y se regenera sola con el tiempo (un GE
    // pasivo con Period en la clase — ver CharacterClassDefinition.PassiveEffects).
    void UpdateEnergyUI()
    {
        if (EnergyBarContainer != null && !EnergyBarContainer.activeSelf) return;

        float currentEnergy = asc.GetAttributeValue(EAttributeType.Energy);
        float maxEnergy     = asc.GetAttributeValue(EAttributeType.MaxEnergy);
        if (maxEnergy <= 0) maxEnergy = 1;

        if (EnergySlider) EnergySlider.value = currentEnergy / maxEnergy;
        if (EnergyText)   EnergyText.text    = $"{currentEnergy:F0} / {maxEnergy:F0}";
    }

    // Oculta el panel de energía entero si la clase actual no tiene MaxEnergy
    // (o sea, en todas las que no usan mecánicas de bloqueo).
    void CheckIfEnergyExists()
    {
        if (EnergyBarContainer == null || asc == null) return;
        bool hasEnergy = asc.GetAttributeValue(EAttributeType.MaxEnergy) > 0;
        EnergyBarContainer.SetActive(hasEnergy);
    }

    // =========================================================
    // SLOTS DE HABILIDADES
    // =========================================================

    // Conecta cada slot visual a la habilidad correspondiente del
    // jugador actual (o lo oculta si ese slot no tiene habilidad asignada).
    void InitializeAbilitySlots()
    {
        if (player == null) return;

        SetupSlot(slotShift, player.MovementAbility,      EAbilityInput.Movement);
        SetupSlot(slotQ,     player.AbilityQ,             EAbilityInput.Action1);
        SetupSlot(slotE,     player.AbilityE,             EAbilityInput.Action2);
        SetupSlot(slotLMB,   player.PrimaryAttackAbility, EAbilityInput.PrimaryAttack);
        SetupSlot(slotRMB,   player.AimAbility,           EAbilityInput.SecondaryAttack);

        if (UltimateSlot != null)
            UltimateSlot.Setup(player.AbilityR, asc, netAsc, EAbilityInput.Action3);
    }

    // Configura o esconde un único slot de habilidad.
    void SetupSlot(UI_AbilitySlot slot, GameplayAbility ability, EAbilityInput slotInput)
    {
        if (slot == null) return;
        if (ability != null)
        {
            slot.Setup(ability, asc, netAsc, slotInput);
            slot.gameObject.SetActive(true);
        }
        else
        {
            slot.gameObject.SetActive(false);
        }
    }

    // =========================================================
    // NOTIFICACIONES
    // =========================================================

    // Se dispara al llegar a nivel máximo (OnMaxLevelReached).
    void ShowLevelUpNotification()
    {
        if (LevelUpNotification != null) LevelUpNotification.SetActive(true);
    }

    // La llama el sistema de selección de subclase al elegir una opción.
    public void HideLevelUpNotification()
    {
        if (LevelUpNotification != null) LevelUpNotification.SetActive(false);
    }
}
