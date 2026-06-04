using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager_Arena : MonoBehaviour
{
    private int playerCount = 0;

    // Este método es llamado automáticamente por el PlayerInputManager de Unity
    // cuando un jugador presiona un botón para unirse.
    public void OnPlayerJoined(PlayerInput playerInput)
    {
        playerCount++;
        
        AbilitySystemComponent asc = playerInput.GetComponent<AbilitySystemComponent>();
        PlayerController pc = playerInput.GetComponent<PlayerController>();

        if (asc != null)
        {
            // Asignamos el TeamID. J1 = 1, J2 = 2, J3 = 3... (Todos contra Todos)
            asc.TeamID = playerCount;
            Debug.Log($"Jugador {playerCount} unido al Equipo {asc.TeamID}");
        }

        if (pc != null)
        {
            // Al nacer, forzamos que se abra su menú de clases principales
            pc.OpenBaseClassMenuOnSpawn();
        }
    }
}