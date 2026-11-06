using UnityEngine;

// Cuánta vida perdió el jugador local desde la última vez que se preguntó. Lee la vida
// sincronizada, así que sirve igual en el host y en un cliente, y lo que la usa no necesita que
// nadie le avise de los golpes: un efecto que la mira se puede borrar sin tocar el combate.
public class LocalDamageWatch
{
    private PlayerController              _player;
    private NetworkAbilitySystemComponent _net;
    private float _lastHealth = -1f;

    // Lo perdido desde la vez anterior, en % de la vida máxima. 0 si no perdió nada.
    public float PollLostPercent()
    {
        PlayerController player = PlayerController.LocalPlayer;
        if (player != _player)
        {
            _player     = player;
            _net        = player != null ? player.GetComponent<NetworkAbilitySystemComponent>() : null;
            _lastHealth = -1f;
        }
        if (_net == null) return 0f;

        float health = _net.NetHealth;
        float max    = _net.NetMaxHealth;
        float before = _lastHealth;
        _lastHealth  = health;

        if (before < 0f || max <= 0f || health >= before - 0.01f) return 0f;

        // Cuando se acaba un bono de vida máxima la vida se recorta y "baja" sin que nadie pegue:
        // se reconoce porque queda justo en el máximo (como en UI_ClassMenu y MercObjective).
        if (health >= max - 0.01f) return 0f;

        return (before - health) / max * 100f;
    }
}
