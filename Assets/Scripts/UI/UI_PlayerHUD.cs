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
    public Slider ShieldSlider;
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
    
    [Header("Notificaciones")]
    public GameObject LevelUpNotification;

    // --- SE LLAMA DESDE EL PLAYERCONTROLLER ---
    public void InitializeHUD(PlayerController ownerPlayer)
    {
        player = ownerPlayer; 

        if (player != null)
        {
            if (asc != null) asc.OnMaxLevelReached -= ShowLevelUpNotification;
            asc = player.GetComponent<AbilitySystemComponent>();

            if (asc != null)
            {
                asc.OnMaxLevelReached += ShowLevelUpNotification;
            }

            if (ClassIconImage != null)
            {
                if (player.CharacterIcon != null)
                {
                    ClassIconImage.sprite = player.CharacterIcon;
                    ClassIconImage.enabled = true;
                }
                else
                {
                    ClassIconImage.enabled = false;
                }
            }

            InitializeAbilitySlots();
            CheckIfManaExists();

            if (LevelUpNotification != null) LevelUpNotification.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (asc != null) asc.OnMaxLevelReached -= ShowLevelUpNotification;
    }

    void Update()
    {
        if (asc == null) return; // Si no hay jugador asignado, la interfaz se queda quieta

        UpdateHealthUI();
        UpdateManaUI(); 
    }

    void ShowLevelUpNotification()
    {
        if (LevelUpNotification != null) LevelUpNotification.SetActive(true);
    }
    
    public void HideLevelUpNotification()
    {
        if (LevelUpNotification != null) LevelUpNotification.SetActive(false);
    }

    void UpdateHealthUI()
    {
        float currentHealth = asc.GetAttributeValue(EAttributeType.Health);
        float maxHealth = asc.GetAttributeValue(EAttributeType.MaxHealth);
        float currentShield = asc.GetAttributeValue(EAttributeType.Shield);
        
        if (maxHealth <= 0) maxHealth = 1;

        if (HealthSlider) HealthSlider.value = currentHealth / maxHealth;
        
        // --- EL ARREGLO DEL ESCUDO ---
        if (ShieldSlider) 
        {
            ShieldSlider.value = currentShield / maxHealth;
            // Apaga la barra completamente si el escudo es 0 o menor
            ShieldSlider.gameObject.SetActive(currentShield > 0); 
        }

        if (HealthText) 
        {
            if (currentShield > 0)
            {
                HealthText.text = $"{currentHealth:F0} (+{currentShield:F0}) / {maxHealth:F0}";
            }
            else
            {
                HealthText.text = $"{currentHealth:F0} / {maxHealth:F0}";
            }
        }
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
        if (player == null) return;
        
        SetupSlot(slotShift, player.MovementAbility);
        SetupSlot(slotQ, player.AbilityQ);
        SetupSlot(slotE, player.AbilityE);
        SetupSlot(slotLMB, player.PrimaryAttackAbility);
        SetupSlot(slotRMB, player.AimAbility);
        
        if (UltimateSlot != null)
        {
            UltimateSlot.Setup(player.AbilityR, asc);
        }
    }

    void SetupSlot(UI_AbilitySlot slot, GameplayAbility ability)
    {
        if (slot == null) return;
        if (ability != null)
        {
            slot.Setup(ability, asc);
            slot.gameObject.SetActive(true);
        }
        else
        {
            slot.gameObject.SetActive(false);
        }
    }

    void CheckIfManaExists()
    {
        if (ManaBarContainer == null || asc == null) return;
        bool hasMana = asc.GetAttributeValue(EAttributeType.MaxMana) > 0;
        ManaBarContainer.SetActive(hasMana);
    }
}