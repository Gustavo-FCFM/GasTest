using UnityEngine;
using System.Collections;

public class NPC_Target : MonoBehaviour
{
    private AbilitySystemComponent ASC;
    
    [Header("Configuración de Respawn")]
    public float RespawnTime = 3f;
    public GameObject VisualModel; // Arrastra aquí la cápsula/malla (hijo)
    public Collider MainCollider;  // El collider principal

    [Header("Recompensa")]
    public float ExperienceReward = 150f; // Cuánta XP da al morir

    void Awake()
    {
        ASC = GetComponent<AbilitySystemComponent>();
        if (MainCollider == null) MainCollider = GetComponent<Collider>();
    }

    void Start()
    {
        // Nos suscribimos al evento de muerte del ASC
        if (ASC != null)
        {
            ASC.OnDeath += HandleDeath;
        }
    }

    void OnDestroy()
    {
        // Buena práctica: desuscribirse para evitar errores de memoria
        if (ASC != null) ASC.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        // 1. DAR EXPERIENCIA AL JUGADOR
        GiveExperienceToPlayer();

        // 2. INICIAR RESPAWN
        StartCoroutine(RespawnRoutine());
    }

    private void GiveExperienceToPlayer()
    {
        // Buscamos al jugador en la escena
        // Nota: En Unity 2023 se usa FindFirstObjectByType, en anteriores FindObjectOfType
        PlayerController player = FindFirstObjectByType<PlayerController>();
        
        if (player != null)
        {
            AbilitySystemComponent playerASC = player.GetComponent<AbilitySystemComponent>();
            if (playerASC != null)
            {
                playerASC.GainExperience(ExperienceReward);
                Debug.Log($"Jugador recibió {ExperienceReward} de experiencia.");
            }
        }
    }

    private IEnumerator RespawnRoutine()
    {
        // A. "Morir" (Apagar gráficos y colisiones, pero NO destruir el objeto)
        if (VisualModel) VisualModel.SetActive(false);
        if (MainCollider) MainCollider.enabled = false;
        
        // Apagamos también el Canvas de vida si lo tiene como hijo
        var canvas = GetComponentInChildren<Canvas>();
        if (canvas) canvas.enabled = false;

        // B. Esperar
        yield return new WaitForSeconds(RespawnTime);

        // C. Revivir (Lógica interna del ASC)
        if (ASC != null) ASC.Revive();

        // D. "Aparecer" (Encender todo de nuevo)
        if (VisualModel) VisualModel.SetActive(true);
        if (MainCollider) MainCollider.enabled = true;
        if (canvas) canvas.enabled = true;
    }
}