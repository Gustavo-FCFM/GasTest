using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Revisamos si lo que acaba de caer en la zona es un jugador
        PlayerController player = other.GetComponent<PlayerController>();
        
        if (player != null)
        {
            Debug.Log("[DeathZone] ¡Un jugador cayó al vacío! Regresándolo a la arena...");
            
            // Aquí podemos teletransportarlo, o más adelante, mandarle daño masivo a su ASC
            player.TeleportToSpawn(); 
        }
    }
}