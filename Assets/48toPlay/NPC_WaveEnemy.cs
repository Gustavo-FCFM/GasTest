using UnityEngine;

[RequireComponent(typeof(AbilitySystemComponent))]
public class NPC_WaveEnemy : MonoBehaviour
{
    private AbilitySystemComponent ASC;
    private GameMode_Survival gameMode;

    [Header("Configuración Base")]
    public float ExperienceOnDeath = 50f;

    [Header("Dificultad Progresiva (Suma por Ronda)")]
    public float HealthPerRound = 15f; 
    public float DamagePerRound = 2f;
    public float SpeedPerRound = 0.15f;

    void Awake()
    {
        ASC = GetComponent<AbilitySystemComponent>();
        gameMode = FindFirstObjectByType<GameMode_Survival>();
    }

    void Start()
    {
        if (ASC != null)
        {
            ASC.OnDeath += HandleDeath;
        }
    }

    void OnDestroy()
    {
        if (ASC != null) ASC.OnDeath -= HandleDeath;
    }

    public void InitializeEnemy(int difficultyLevel)
    {
        if (ASC == null) return;

        // 1. Calcular cuántas mejoras tocan
        int scalingFactor = Mathf.Max(0, difficultyLevel - 1);

        if (scalingFactor > 0)
        {
            float addedHealth = scalingFactor * HealthPerRound;
            float addedDamage = scalingFactor * DamagePerRound;
            float addedSpeed = scalingFactor * SpeedPerRound;

            // 2. Aplicar usando UpgradeAttribute (Modifica la BASE, es permanente)
            
            // Vida (UpgradeAttribute ya se encarga de curarlo al máximo también)
            ASC.UpgradeAttribute(EAttributeType.MaxHealth, addedHealth);
            
            // Daño
            ASC.UpgradeAttribute(EAttributeType.Atq, addedDamage);

            // Velocidad (Con límite de seguridad para que no rompa la física)
            float currentSpeed = ASC.GetAttributeValue(EAttributeType.MovSpeed);
            if (currentSpeed + addedSpeed > 8f) 
            {
                addedSpeed = Mathf.Max(0, 8f - currentSpeed); // Solo subimos lo que falte para 8
            }
            ASC.UpgradeAttribute(EAttributeType.MovSpeed, addedSpeed);
        }
    }

    private void HandleDeath()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            var playerASC = player.GetComponent<AbilitySystemComponent>();
            if (playerASC) playerASC.GainExperience(ExperienceOnDeath);
        }

        if (gameMode != null)
        {
            gameMode.OnEnemyKilled(this);
        }

        Destroy(gameObject); 
    }
}