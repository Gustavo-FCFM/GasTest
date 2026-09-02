using UnityEngine;

// ============================================================
// GA_Whirlwind
//
// Un área continua que ADEMÁS hace algo con el LANZADOR mientras dura: lo pone a girar
// sobre su eje (molinete tipo Garen), le sostiene una animación en bucle todo el rato, y
// le bloquea las demás habilidades.
//
// POR QUÉ ES UNA CLASE APARTE y no campos en GA_ContinuousAoE: esa clase la comparten el
// aura del jefe, la zona de hielo y el daño en área genérico — áreas estáticas que nunca
// van a girar a nadie ni bloquear nada. Meterles estos campos les ensuciaba el inspector
// con opciones que no significan nada para ellas.
//
// Todo lo del área en sí (radio, ticks, VFX, seguir al dueño, retícula) se hereda tal
// cual: acá solo viven los tres hooks que la base deja abiertos.
// ============================================================
[CreateAssetMenu(fileName = "GA_Whirlwind", menuName = "GAS/Generics/Whirlwind (AoE + giro)")]
public class GA_Whirlwind : GA_ContinuousAoE, IChanneledAbility
{
    [Header("Animación sostenida")]
    [Tooltip("Clip que se reproduce EN BUCLE mientras dura el área. Es lo que convierte el " +
             "molinete en un molinete: sin esto la habilidad dispara un mandoble suelto que " +
             "termina en un segundo, y el personaje se queda quieto los otros nueve mientras el " +
             "daño sigue ticando.\n\n" +
             "VACÍO = animación normal (un disparo suelto del AnimationClip de la habilidad).\n\n" +
             "Reusa las ranuras del MANTENIDO en el Animator, así que no hay que crear estados " +
             "nuevos: los del escudo sirven tal cual.")]
    public AnimationClip ChannelLoopAnimation;

    [Tooltip("OPCIONAL: arranque, una sola vez, antes de entrar al bucle.")]
    public AnimationClip ChannelStartAnimation;

    [Tooltip("OPCIONAL: remate al terminar el área.")]
    public AnimationClip ChannelEndAnimation;

    [Header("Giro")]
    [Tooltip("Grados por segundo que gira el MODELO sobre su eje mientras dura el área. " +
             "0 = sin giro.\n\n" +
             "Gira el modelo y NO la raíz a propósito: la raíz la maneja la mira y el " +
             "movimiento WASD se calcula relativo a la cámara. Como el área es un radio sin " +
             "dirección, girar el modelo no cambia a quién le pega.\n\n" +
             "360 = una vuelta por segundo. Para un molinete rápido, 500-700.")]
    public float ModelSpinSpeed = 0f;

    [Header("Bloqueo")]
    [Tooltip("Mientras dure el área, el personaje NO puede usar ninguna otra habilidad — salvo " +
             "las que tengan marcado 'Usable While Channeling' en su propio asset.\n\n" +
             "Así la lista de excepciones se arma desde el lado de las POCAS que sí (Frenzy y el " +
             "salto, para el molinete) en vez de enumerar todas las que no. Una habilidad nueva " +
             "nace bloqueada, que es lo seguro.\n\n" +
             "El bloqueo dura lo mismo que el área, no lo que tarde el jugador en quedar libre.")]
    public bool BlockOtherAbilities = false;

    // IChanneledAbility: la capa de red lee los clips y el giro por acá para replicarlos.
    public AnimationClip ChannelStartClip => ChannelStartAnimation;
    public AnimationClip ChannelLoopClip  => ChannelLoopAnimation;
    public AnimationClip ChannelEndClip   => ChannelEndAnimation;
    public float         SpinSpeed        => ModelSpinSpeed;

    // Con clip de bucle la animación la maneja el canalizado (sostenida + giro) en vez del
    // disparo suelto. Va por la capa de red porque Activate() corre en el SERVIDOR: sin el
    // RPC, el dueño remoto no vería nada.
    protected override bool TryPlayCustomAnimation(PlayerController pc)
    {
        if (ChannelLoopAnimation == null) return false;

        var netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc == null) return false;

        netAsc.ServerPlayChannelAnimation(this, true);
        return true;
    }

    // El bloqueo se marca con un TAG y no con un flag interno: así lo ve el CanActivate de
    // cualquier habilidad sin que ninguna tenga que conocer a esta.
    protected override void OnAreaStarted()
    {
        if (BlockOtherAbilities) OwnerASC.AddTag(EGameplayTag.Status_Channeling);
    }

    // Corre también si el área se interrumpe (la base lo llama desde un finally). Sin eso,
    // morir en pleno molinete dejaría el tag puesto y el jugador se quedaría sin poder usar
    // NINGUNA habilidad por el resto de la partida.
    protected override void OnAreaFinished()
    {
        if (ChannelLoopAnimation != null)
        {
            var netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
            if (netAsc != null) netAsc.ServerPlayChannelAnimation(this, false);
        }

        if (BlockOtherAbilities) OwnerASC.RemoveTag(EGameplayTag.Status_Channeling);
    }
}
