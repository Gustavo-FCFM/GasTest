using UnityEngine;

// ============================================================
// DeathZone
//
// Trigger de "vacío" en la arena: cuando un jugador cae dentro, lo
// teletransporta de vuelta a su punto de spawn. No es un
// NetworkBehaviour (existe igual en la escena de todos los peers),
// así que reacciona solo al dueño real del jugador para no pelear
// con la posición autoritativa que ese cliente ya está mandando.
// ============================================================
public class DeathZone : MonoBehaviour
{
    // Al entrar cualquier collider al trigger, si es un jugador (y somos
    // su dueño), lo manda de vuelta al spawn.
    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();

        if (player != null)
        {
            // El NetworkTransform del jugador es client-authoritative (ver
            // notas en Player Controller.cs), así que TeleportToSpawn() debe
            // correr solo en la copia dueña. Este trigger existe en la escena
            // de todos los peers (no es un NetworkBehaviour), así que sin esta
            // guardia, cualquier otro peer que vea al jugador remoto caer en
            // su propia física local también intentaría teletransportar esa
            // copia no-autoritativa, peleando con la posición real que manda
            // el dueño.
            if (!player.IsOwner) return;

            Debug.Log("[DeathZone] ¡Un jugador cayó al vacío! Regresándolo a la arena...");

            // Aquí podemos teletransportarlo, o más adelante, mandarle daño masivo a su ASC
            player.TeleportToSpawn();
        }
    }
}
