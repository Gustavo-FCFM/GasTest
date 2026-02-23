using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Necesario para filtrar listas fácilmente (Where/ToList)

public class GameMode_Survival : MonoBehaviour
{
    
    [System.Serializable]
    public struct EnemySpawnConfig
    {
        public string Name; // Solo para que se vea bonito en el editor
        public GameObject Prefab;
        public int MinRoundToSpawn; // ¿Desde qué ronda aparece? (ej: 1 para Fantasma, 3 para Mago)
    }

    public List<EnemySpawnConfig> EnemyTypes; // La lista que configurarás en el Inspector
    
    public Transform[] SpawnPoints; 
    public PlayerController Player;

    [Header("Progresión")]
    public CharacterClassDefinition BarbarianClass;
    public UI_RoguelikeMenu UpgradeMenu;

    [Header("Dificultad de Oleadas")]
    [Tooltip("Cuántos enemigos aparecen en la Ronda 1.")]
    public int BaseEnemies = 3; 
    
    [Tooltip("Cuántos enemigos EXTRA se suman por cada ronda nueva.")]
    public int EnemiesPerRound = 2;

    [Header("Habilidades Desbloqueables")]
    public GameplayAbility Skill_Q_Buff;      
    public GameplayAbility Skill_E_Combo;     
    public GameplayAbility Skill_Shift_Dash;  
    public GameplayAbility Skill_R_Ulti;      

    [Header("Configuración Roguelike")]
    public List<EAttributeType> AllowedAttributes = new List<EAttributeType>() 
    { 
        EAttributeType.MaxHealth, 
        EAttributeType.Atq, 
        EAttributeType.AtkSpeed, 
        EAttributeType.MovSpeed,
        EAttributeType.Def,
        EAttributeType.CritChance,
        EAttributeType.LifeSteal
    };

    private struct UpgradeOption
    {
        public EAttributeType Attribute;
        public float Amount;
        public string Description;
    }
    
    private List<UpgradeOption> currentRoundOptions = new List<UpgradeOption>();

    // Estado del Juego
    private int currentRound = 1;
    private int enemiesAlive = 0;
    private bool roundInProgress = false;

    IEnumerator Start()
    {
        yield return null; 
        InitializePlayer();
        StartCoroutine(StartRoundRoutine());
    }

    private void InitializePlayer()
    {
        Player.EquipCharacterClass(BarbarianClass);
        AbilitySystemComponent asc = Player.GetComponent<AbilitySystemComponent>();
        
        // Limpiamos por si acaso, pero EquipCharacterClass ya lo hace.
        asc.ClearGrantedAbilities(); 

        // Otorgamos TODAS las habilidades configuradas en la clase base
        foreach (var assignment in BarbarianClass.Abilities)
        {
            GameplayAbility instance = asc.GrantAbility(assignment.Ability);
            
            // Asignamos cada instancia a su espacio correspondiente en el PlayerController
            switch (assignment.InputSlot)
            {
                case EAbilityInput.PrimaryAttack:   Player.PrimaryAttackAbility = instance; break;
                case EAbilityInput.SecondaryAttack: Player.AimAbility = instance; break;
                case EAbilityInput.Action1:         Player.AbilityQ = instance; break;
                case EAbilityInput.Action2:         Player.AbilityE = instance; break;
                case EAbilityInput.Action3:         Player.AbilityR = instance; break;
                case EAbilityInput.Movement:        Player.MovementAbility = instance; break;
            }
        }

        // Ya no necesitamos poner en null las habilidades extra porque ya se otorgaron.
        FindFirstObjectByType<UI_PlayerHUD>().InitializeHUD();
    }

    public void OnEnemyKilled(NPC_WaveEnemy enemy)
    {
        enemiesAlive--;
        if (enemiesAlive <= 0 && roundInProgress)
        {
            EndRound();
        }
    }

    private void EndRound()
    {
        roundInProgress = false;
        Debug.Log("¡Ronda Terminada!");
        GenerateRandomOptions();
        
        List<string> descriptions = new List<string>();
        foreach (var opt in currentRoundOptions) descriptions.Add(opt.Description);

        CheckLevelUnlocks();
        currentRound++;
        UpgradeMenu.Show(currentRound, descriptions);
    }

    private void GenerateRandomOptions() {
        currentRoundOptions.Clear();
        for (int i = 0; i < 3; i++) {
            EAttributeType randomAttr = AllowedAttributes[Random.Range(0, AllowedAttributes.Count)];
            float randomValue = GetRandomValueForAttribute(randomAttr);
            string desc = FormatDescription(randomAttr, randomValue);
            currentRoundOptions.Add(new UpgradeOption { Attribute = randomAttr, Amount = randomValue, Description = desc });
        }
    }
    private float GetRandomValueForAttribute(EAttributeType type) {
        switch (type) {
            case EAttributeType.MaxHealth:  return Random.Range(30, 151);
            case EAttributeType.Atq:        return Random.Range(2, 12);
            case EAttributeType.Def:        return Random.Range(1, 4);
            case EAttributeType.MovSpeed:   return Random.Range(0.2f, 0.8f);
            case EAttributeType.LifeSteal:  return Random.Range(0.08f, 0.1f);
            case EAttributeType.CritChance: return Random.Range(5, 11);
            case EAttributeType.AtkSpeed:   return Random.Range(-0.2f, -0.02f); 
            default: return 1;
        }
    }
    private string FormatDescription(EAttributeType type, float value)
    {
        string sign = value > 0 ? "+" : ""; 
        
        switch (type)
        {
            case EAttributeType.MaxHealth: return $"Vitalidad\n{sign}{value:F0} Vida Máx";
            case EAttributeType.Atq:       return $"Fuerza Bruta\n{sign}{value:F0} Daño";
            case EAttributeType.Def:       return $"Piel de Hierro\n{sign}{value:F0} Defensa";
            case EAttributeType.AtkSpeed:  return $"Frenesí\n{value:F2}s Intervalo de Ataque"; 
            case EAttributeType.MovSpeed:  return $"Pies Ligeros\n{sign}{value:F1} Velocidad";
            case EAttributeType.LifeSteal: return $"Vampirismo\n{sign}{value*100:F0}% Robo de Vida";
            case EAttributeType.CritChance: return $"Golpe Letal\n{sign}{value:F0}% Prob. Crítico";
            default: return $"{type}: {value}";
        }
    }

    public void ApplySelectedOption(int index)
    {
        if (index < 0 || index >= currentRoundOptions.Count) return;
        UpgradeOption choice = currentRoundOptions[index];
        AbilitySystemComponent asc = Player.GetComponent<AbilitySystemComponent>();

        float currentVal = asc.GetAttributeValue(choice.Attribute);
        float newVal = currentVal + choice.Amount;
        if (choice.Attribute == EAttributeType.AtkSpeed && newVal < 0.1f) newVal = 0.1f; 

        asc.UpgradeAttribute(choice.Attribute, choice.Amount);

        if (choice.Attribute == EAttributeType.MaxHealth)
        {
            float currentHp = asc.GetAttributeValue(EAttributeType.Health);
            asc.SetCurrentAttributeValue(EAttributeType.Health, currentHp + choice.Amount);
        }

        Debug.Log($"Mejora aplicada: {choice.Description}");
        StartCoroutine(StartRoundRoutine());
    }

    private IEnumerator StartRoundRoutine()
    {
        roundInProgress = false; 
        yield return new WaitForSeconds(1f); 

        int enemiesToSpawn = BaseEnemies + ((currentRound - 1) * EnemiesPerRound);
        
        enemiesAlive = enemiesToSpawn; 
        Debug.Log($"Iniciando Ronda {currentRound}: {enemiesToSpawn} Enemigos.");

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(0.5f); 
        }
        
        roundInProgress = true; 
    }

    private void SpawnEnemy()
    {
        if (SpawnPoints.Length == 0 || EnemyTypes.Count == 0) return;

        // --- SELECCIÓN DE ENEMIGO ---
        // Filtramos la lista: Solo los que tengan MinRound <= Ronda Actual
        // Usamos Linq para hacerlo en una línea (requiere 'using System.Linq;')
        var validEnemies = EnemyTypes.Where(e => e.MinRoundToSpawn <= currentRound).ToList();

        if (validEnemies.Count == 0) return; // Seguridad

        // Elegimos uno al azar de los válidos
        EnemySpawnConfig chosenEnemy = validEnemies[Random.Range(0, validEnemies.Count)];
        // ---------------------------

        Transform point = SpawnPoints[Random.Range(0, SpawnPoints.Length)];
        Vector3 randomOffset = Random.insideUnitSphere * 1.5f; 
        randomOffset.y = 0;

        GameObject enemyObj = Instantiate(chosenEnemy.Prefab, point.position + randomOffset, Quaternion.identity);
        
        NPC_WaveEnemy script = enemyObj.GetComponent<NPC_WaveEnemy>();
        if (script != null)
        {
            script.InitializeEnemy(currentRound);
        }
    }

    private void CheckLevelUnlocks()
    {
        AbilitySystemComponent asc = Player.GetComponent<AbilitySystemComponent>();
        int level = (int)asc.GetAttributeValue(EAttributeType.Level);

        if (level >= 3 && Player.AbilityQ == null)
        {
            GameplayAbility newAbility = asc.GrantAbility(Skill_Q_Buff);
            Player.AbilityQ = newAbility;
        }
        if (level >= 6 && Player.MovementAbility == null)
        {
            GameplayAbility newAbility = asc.GrantAbility(Skill_Shift_Dash);
            Player.MovementAbility = newAbility;
        }
        if (level >= 9 && Player.AbilityE == null)
        {
            GameplayAbility newAbility = asc.GrantAbility(Skill_E_Combo);
            Player.AbilityE = newAbility;
        }
        if (level >= 15 && Player.AbilityR == null)
        {
            GameplayAbility newAbility = asc.GrantAbility(Skill_R_Ulti);
            Player.AbilityR = newAbility;
        }
        FindFirstObjectByType<UI_PlayerHUD>().InitializeHUD();
    }
}