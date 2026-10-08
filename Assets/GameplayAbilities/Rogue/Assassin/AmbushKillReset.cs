using UnityEngine;

// ============================================================
// AmbushKillReset  (pasiva del Asesino — 8 de octubre de 2026, pedido de Gustavo)
//
// Al SALIR de la invisibilidad se abre una ventana de KillWindow segundos: si en ese
// rato mata algo (un jugador, un NPC, cualquier cosa con ASC), la habilidad de Ability
// (la Emboscada sombría) vuelve a estar lista al instante. Mientras sigue invisible
// también cuenta (una herida que mata sola, por ejemplo). Matar cierra la ventana: un
// reinicio por cada salida de la invisibilidad.
//
// El golpe que mata desde la invisibilidad también entra: atacar te saca de ella
// (BreakInvisibility) ANTES de que el daño se aplique, así que la ventana ya está abierta
// cuando la víctima muere.
//
// Server-side: la baja llega por AbilitySystemComponent.OnKilledTarget, que solo se
// dispara en el servidor. WindowEffect (opcional) es un buff visible que dura lo que la
// ventana, para que el jugador sepa que es el momento de rematar a alguien.
//
// SETUP: en el PassiveBehaviorsPrefab del Asesino (Assassin_Behaviour), al lado del
// Crítico mejorado. Como vive en un hijo del jugador, busca el ASC en el PADRE.
// ============================================================
public class AmbushKillReset : MonoBehaviour
{
    [Tooltip("La habilidad cuyo cooldown se reinicia al matar dentro de la ventana (la Emboscada sombría).")]
    public GameplayAbility Ability;

    [Tooltip("El tag que, al irse, abre la ventana.")]
    public EGameplayTag WindowTag = EGameplayTag.Status_Invisible;

    [Tooltip("Segundos de la ventana, contados desde que sale de la invisibilidad.")]
    public float KillWindow = 6f;

    [Tooltip("OPCIONAL: buff que se le pone mientras dura la ventana, para que la vea en su barra de " +
             "efectos. Su duración la pone KillWindow; se quita solo al reiniciar.")]
    public GameplayEffect WindowEffect;

    private AbilitySystemComponent _asc;
    private float _windowUntil = -1f;

    private void Awake() => _asc = GetComponentInParent<AbilitySystemComponent>();

    private void OnEnable()
    {
        if (_asc == null) return;
        _asc.OnTagRemovedCallback += HandleTagRemoved;
        _asc.OnKilledTarget       += HandleKill;
    }

    private void OnDisable()
    {
        if (_asc == null) return;
        _asc.OnTagRemovedCallback -= HandleTagRemoved;
        _asc.OnKilledTarget       -= HandleKill;
    }

    private bool IsServer
    {
        get
        {
            NetworkAbilitySystemComponent netAsc = _asc.GetComponent<NetworkAbilitySystemComponent>();
            return netAsc == null || netAsc.IsServerInitialized;
        }
    }

    // Salió de la invisibilidad: se abre la ventana. Morir también se la saca (el muerto
    // pierde sus buffs), y eso no tiene que abrir nada.
    private void HandleTagRemoved(EGameplayTag tag)
    {
        if (tag != WindowTag || !IsServer) return;
        if (_asc.HasTag(EGameplayTag.State_Dead) || _asc.GetAttributeValue(EAttributeType.Health) <= 0f) return;

        _windowUntil = Time.time + KillWindow;
        if (WindowEffect != null) _asc.ApplyGameplayEffect(WindowEffect, _asc, KillWindow);
    }

    private void HandleKill(AbilitySystemComponent victim)
    {
        bool open = _asc.HasTag(WindowTag) || Time.time < _windowUntil;
        if (!open || Ability == null || Ability.CooldownEffect == null) return;

        GameplayEffect cooldown = Ability.CooldownEffect;
        if (cooldown.GrantedTags == null || cooldown.GrantedTags.Count == 0) return;

        _asc.ReduceCooldownByTag(cooldown.GrantedTags[0], 99999f);

        _windowUntil = -1f;
        if (WindowEffect != null) _asc.RemoveEffectsByDefinition(WindowEffect);
    }
}
