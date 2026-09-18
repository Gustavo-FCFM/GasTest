using UnityEngine;
using System.Collections;

// ============================================================
// NPC_Target
//
// Objetivo de práctica (dummy) que, al morir, le da experiencia a
// "el jugador" y renace tras un tiempo (sin destruirse, solo
// apagando visuales/colisión y volviéndolos a prender). Busca al
// jugador con FindFirstObjectByType, así que en una escena con más
// de un PlayerController siempre premia al mismo, sin importar quién
// dio el golpe final.
// ============================================================
public class NPC_Target : MonoBehaviour
{
    private AbilitySystemComponent ASC;

    [Header("Configuración de Respawn")]
    public float RespawnTime = 3f;
    // Modelo/malla visual (hijo) que se apaga mientras está "muerto".
    public GameObject VisualModel;
    // Collider principal; si no se asigna, se toma el del propio GameObject.
    public Collider MainCollider;

    [Header("Recompensa")]
    // Experiencia que le da al jugador al morir este NPC.
    public float ExperienceReward = 150f;

    // Cachea el ASC y el collider principal si no fue asignado a mano.
    void Awake()
    {
        ASC = GetComponent<AbilitySystemComponent>();
        if (MainCollider == null) MainCollider = GetComponent<Collider>();
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

    // Al morir: reparte experiencia y arranca la rutina de respawn.
    private void HandleDeath()
    {
        GiveExperienceToPlayer();
        StartCoroutine(RespawnRoutine());
    }

    // Le da ExperienceReward de experiencia al primer PlayerController que
    // encuentre en la escena.
    private void GiveExperienceToPlayer()
    {
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

    // Apaga visuales/colisión/UI de vida, espera RespawnTime, revive el
    // ASC, y vuelve a prender todo.
    private IEnumerator RespawnRoutine()
    {
        if (VisualModel) VisualModel.SetActive(false);
        if (MainCollider) MainCollider.enabled = false;

        var canvas = GetComponentInChildren<Canvas>();
        if (canvas) canvas.enabled = false;

        yield return new WaitForSeconds(RespawnTime);

        if (ASC != null) ASC.Revive();

        if (VisualModel) VisualModel.SetActive(true);
        if (MainCollider) MainCollider.enabled = true;
        if (canvas) canvas.enabled = true;
    }
}
