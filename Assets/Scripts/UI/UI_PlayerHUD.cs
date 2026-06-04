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


    public void InitializeHUD(PlayerController ownerPlayer) // <-- AHORA PIDE EL JUGADOR
    {
        player = ownerPlayer; // Asignación directa en lugar de buscar por Tag

        if (player != null)
        {
            if (asc != null) asc.OnMaxLevelReached -= ShowLevelUpNotification;
            asc = player.GetComponent<AbilitySystemComponent>();

            if (asc != null)
            {
                asc.OnMaxLevelReached += ShowLevelUpNotification;
            }

            // 2. Configurar Icono
            if (player.CharacterIcon != null && ClassIconImage != null)
            {
                ClassIconImage.sprite = player.CharacterIcon;
                ClassIconImage.enabled = true;
            }

            // 3. Configurar Slots 
            InitializeAbilitySlots();
            
            // 4. Lógica de Maná
            CheckIfManaExists();
            if (LevelUpNotification != null) LevelUpNotification.SetActive(false);
        }
    }

    void Update()
    {
        if (asc == null) return;
        UpdateHealthUI();
        UpdateManaUI(); 
    }
    private void ShowLevelUpNotification()
    {
        // 1. Verificamos que tengamos un jugador y una clase equipada
        if (player != null && player.CurrentClassDef != null)
        {
            // 2. Verificamos si esta clase específica tiene evoluciones configuradas
            if (player.CurrentClassDef.AvailableSubclasses != null && player.CurrentClassDef.AvailableSubclasses.Count > 0)
            {
                if (LevelUpNotification != null) LevelUpNotification.SetActive(true);
            }
            else
            {
                Debug.Log("Nivel Máximo alcanzado, pero no hay más evoluciones para esta clase.");
            }
        }
    }

    public void HideLevelUpNotification()
    {
        if (LevelUpNotification != null) LevelUpNotification.SetActive(false);
    }

    void OnDestroy()
    {
        // Limpieza de memoria vital en Unity
        if (asc != null) asc.OnMaxLevelReached -= ShowLevelUpNotification;
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
        float currentShield = asc.GetAttributeValue(EAttributeType.Shield); // Obtenemos el escudo
        
        if (maxHealth <= 0) maxHealth = 1;

        // 1. Actualizar barra de vida normal
        if (HealthSlider) 
        {
            HealthSlider.value = currentHealth / maxHealth;
        }

        // 2. Actualizar la NUEVA barra de escudo
        if (ShieldSlider)
        {
            // Ocultamos el slider de escudo si no hay escudo
            ShieldSlider.gameObject.SetActive(currentShield > 0);
            
            // Llenamos el escudo en proporción a la vida máxima
            ShieldSlider.value = currentShield / maxHealth;
        }

        // 3. Actualizar el texto
        if (HealthText) 
        {
            // Si hay escudo, lo mostramos en el texto entre corchetes
            if (currentShield > 0)
            {
                HealthText.text = $"{currentHealth:F0} [+{(int)currentShield}] / {maxHealth:F0}";
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