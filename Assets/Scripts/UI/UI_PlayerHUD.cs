using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_PlayerHUD : MonoBehaviour
{
    [Header("Target")]
    public PlayerController player;
    private AbilitySystemComponent asc;

    [Header("Panel Izquierdo (Estado)")]
    public Image ClassIconImage;
    public Slider HealthSlider;
    public TextMeshProUGUI HealthText;

    [Header("Barra de Maná (Opcional)")]
    public GameObject ManaBarContainer; 
    public Slider ManaSlider;
    public TextMeshProUGUI ManaText;

    [Header("Panel Derecho (Habilidades)")]
    public UI_AbilitySlot slotShift;
    public UI_AbilitySlot slotQ;
    public UI_AbilitySlot slotE;
    public UI_AbilitySlot slotLMB;
    public UI_AbilitySlot slotRMB;

    [Header("Panel Central (Ultimate)")]
    public UI_UltimateSlot UltimateSlot;

    void Start()
    {
        InitializeHUD();
    }

    public void InitializeHUD()
    {
        // 1. Buscar Player
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.GetComponent<PlayerController>();
        }

        if (player != null)
        {
            asc = player.GetComponent<AbilitySystemComponent>();

            // 2. Configurar Icono
            if (player.CharacterIcon != null && ClassIconImage != null)
            {
                ClassIconImage.sprite = player.CharacterIcon;
                ClassIconImage.enabled = true;
            }

            // 3. Configurar Slots (Aquí está la magia de ocultar)
            InitializeAbilitySlots();
            
            // 4. Lógica de Maná
            CheckIfManaExists(); 
        }
    }

    void Update()
    {
        if (asc == null) return;
        UpdateHealthUI();
        UpdateManaUI(); 
    }

    void CheckIfManaExists()
    {
        if (ManaBarContainer == null) return;
        float maxMana = asc.GetAttributeValue(EAttributeType.MaxMana);
        ManaBarContainer.SetActive(maxMana > 0);
    }

    void UpdateHealthUI()
    {
        float currentHealth = asc.GetAttributeValue(EAttributeType.Health);
        float maxHealth = asc.GetAttributeValue(EAttributeType.MaxHealth);
        if (maxHealth <= 0) maxHealth = 1;

        if (HealthSlider) HealthSlider.value = currentHealth / maxHealth;
        if (HealthText) HealthText.text = $"{currentHealth:F0} / {maxHealth:F0}";
    }

    void UpdateManaUI()
    {
        if (ManaBarContainer != null && !ManaBarContainer.activeSelf) return;

        float currentMana = asc.GetAttributeValue(EAttributeType.Mana);
        float maxMana = asc.GetAttributeValue(EAttributeType.MaxMana);
        if (maxMana <= 0) maxMana = 1;

        if (ManaSlider) ManaSlider.value = currentMana / maxMana;
        if (ManaText) ManaText.text = $"{currentMana:F0} / {maxMana:F0}";
    }

    void InitializeAbilitySlots()
    {
        SetupSlot(slotShift, player.MovementAbility);
        SetupSlot(slotQ, player.AbilityQ);
        SetupSlot(slotE, player.AbilityE);
        SetupSlot(slotLMB, player.PrimaryAttackAbility);
        SetupSlot(slotRMB, player.AimAbility);
        if (UltimateSlot != null)
        {
            // El script UI_UltimateSlot se encarga de activarse/desactivarse solo en su Setup
            UltimateSlot.Setup(player.AbilityR, asc);
        }
    }

    void SetupSlot(UI_AbilitySlot slot, GameplayAbility ability)
    {
        if (slot == null) return;
        if (ability != null)
        {
            // Si hay habilidad: ENCENDEMOS
            slot.gameObject.SetActive(true);
            slot.Setup(ability, asc);
        }
        else
        {
            // Si NO hay habilidad: APAGAMOS
            slot.gameObject.SetActive(false);
        }
    }
}