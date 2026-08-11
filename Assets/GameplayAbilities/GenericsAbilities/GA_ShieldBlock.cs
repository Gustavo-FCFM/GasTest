using UnityEngine;
using System.Collections;

// ============================================================
// GA_ShieldBlock  (genérico — "Apuntado" del Paladín, postura del Guerrero, ...)
//
// Habilidad de MANTENER (ver IHoldAbility): mientras sostenés el botón, el
// personaje levanta su escudo y una BARRERA se interpone frente a él, frenando
// todo golpe cuya trayectoria la atraviese y gastando ENERGÍA proporcional al
// daño frenado. Al soltar (o al quedarte sin energía) el escudo baja.
//
// REPARTO DE RESPONSABILIDADES — esta habilidad es solo el INTERRUPTOR:
//   · El bloqueo en sí (geometría, reducción, cobro de energía, a quién cubre) lo
//     resuelve Entity_ShieldBarrier, que vive en el PassiveBehaviorsPrefab de la
//     clase y se enciende sola al ver el tag.
//   · Lo que gana el jugador mientras bloquea (el tag, la velocidad reducida, más
//     resistencia, lo que sea) lo define BlockEffect — un GameplayEffect normal, así
//     que se ajusta desde el inspector sin tocar código.
//   · La animación en tres tiempos (levantar → mantener → bajar) la maneja
//     PlayerController con las ranuras genéricas de hold.
//
// Por eso el mismo asset sirve para el Paladín, el Guerrero, o un escudo puesto
// sobre un aliado: cambia el prefab de la barrera y el GE, no este script.
//
// COOLDOWN: se aplica al BAJAR el escudo, no al levantarlo. Un cooldown al
// activar bloquearía la propia habilidad mientras la estás sosteniendo; poniéndolo
// a la salida funciona como tiempo de recuperación, que es lo que se quiere.
// ============================================================
[CreateAssetMenu(fileName = "GA_ShieldBlock", menuName = "GAS/Generics/Shield Block")]
public class GA_ShieldBlock : GameplayAbility, IHoldAbility
{
    [Header("Estado de Bloqueo")]
    [Tooltip("GameplayEffect que se aplica al dueño MIENTRAS sostiene el escudo. Tiene que " +
             "otorgar el tag que la barrera escucha (Status_Blocking por defecto) — ese tag es lo " +
             "que la enciende, y viaja a todos los peers por NetTags.\n\n" +
             "Acá va también todo lo que quieras que gane el jugador mientras bloquea: menos " +
             "velocidad de movimiento, más resistencia, lo que sea. Ponele una Duration larga: " +
             "el efecto se retira a mano al soltar, la duración es solo un salvavidas por si algo " +
             "corta la habilidad de forma anormal.")]
    public GameplayEffect BlockEffect;

    [Header("Energía")]
    [Tooltip("Energía mínima necesaria para poder levantar el escudo. Con 0 se puede levantar " +
             "mientras quede cualquier resto (pero se cae al primer golpe).")]
    public float MinEnergyToActivate = 5f;

    [Tooltip("Energía que se consume por segundo con solo sostener el escudo, además de la que " +
             "cuesta frenar daño. 0 = sostenerlo es gratis y solo se paga al bloquear.")]
    public float EnergyDrainPerSecond = 0f;

    [Tooltip("Tiempo máximo que se puede sostener, en segundos. 0 = sin límite (lo limita la energía).")]
    public float MaxHoldTime = 0f;

    [Header("Animación de Mantener")]
    [Tooltip("Clip de SOSTENER el escudo, EN BUCLE (el personaje ya lo tiene levantado). Es el " +
             "único obligatorio: con solo este y un estado en el Animator, la habilidad ya se ve bien.")]
    public AnimationClip HoldClip;

    [Tooltip("OPCIONAL: clip de LEVANTAR el escudo (una vez, al presionar). Vacío = se entra " +
             "directo al bucle.")]
    public AnimationClip RaiseClip;

    [Tooltip("OPCIONAL: clip de BAJAR el escudo (una vez, al soltar). Vacío = se sale directo " +
             "del bucle.")]
    public AnimationClip LowerClip;

    [Tooltip("OPCIONAL: reacción al recibir un golpe EN el escudo (una vez, sin salir del " +
             "mantenido). Se dispara cada vez que la barrera frena daño.")]
    public AnimationClip ShieldHitClip;

    [Tooltip("Tiempo mínimo entre dos reacciones al golpe, en segundos. Sin esto, varios " +
             "impactos seguidos (un ataque rápido, un tick de área) reiniciarían el clip en cada " +
             "frame y no se vería nada.")]
    public float ShieldHitCooldown = 0.35f;

    // IHoldAbility: los clips salen del asset, así cada peer resuelve los suyos.
    public AnimationClip HoldLoopClip   => HoldClip;
    public AnimationClip HoldStartClip  => RaiseClip;
    public AnimationClip HoldEndClip    => LowerClip;
    public AnimationClip HoldImpactClip => ShieldHitClip;

    // Estado del mantenido. NonSerialized: es estado de runtime por instancia
    // otorgada, no se guarda en el asset (mismo criterio que las cargas de GA_Dash).
    [System.NonSerialized] private bool _holding;
    public bool IsHolding => _holding;

    // Barrera a la que estamos suscritos mientras dura el mantenido, y cuándo se
    // reprodujo la última reacción al golpe (para no reiniciar el clip en cada tick).
    [System.NonSerialized] private Entity_ShieldBarrier _subscribedBarrier;
    [System.NonSerialized] private float _lastShieldHitTime;

    // =========================================================
    // ACTIVACIÓN
    // =========================================================

    // Además de los chequeos de siempre, exige tener energía suficiente para
    // levantarlo. Corre también en el DUEÑO (la predicción del input lo llama antes
    // de pedirle nada al servidor), y la energía se sincroniza — así que el jugador
    // no ve el escudo levantarse para que se lo bajen medio segundo después.
    public override bool CanActivate()
    {
        if (!base.CanActivate()) return false;
        if (OwnerASC == null) return false;

        return OwnerASC.GetAttributeValue(EAttributeType.Energy) > MinEnergyToActivate;
    }

    // Levanta el escudo: aplica el estado de bloqueo (que enciende la barrera),
    // arranca la animación de mantener y queda esperando a que lo suelten.
    //
    // NO llama EndAbility() acá: la habilidad sigue viva todo el mantenido. La cierra
    // EndHold().
    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;
        if (_holding) return;   // ya estaba levantado (doble input): nada que hacer

        _holding = true;

        // Costo de entrada y visuales. NO usamos CommitAbility porque ese aplica
        // también el cooldown, y acá el cooldown va a la SALIDA (ver cabecera).
        if (CostEffect != null) OwnerASC.ApplyGameplayEffect(CostEffect, this);

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        if (VisualsSequence != null && VisualsSequence.Count > 0)
        {
            if (netAsc != null) netAsc.ServerPlayAbilityVisualsSequence(this);
            else OwnerASC.StartAbilityCoroutine(PlayVisualsSequence());
        }

        // Este es el tag que enciende la barrera en TODOS los peers (viaja por NetTags).
        if (BlockEffect != null) OwnerASC.ApplyGameplayEffect(BlockEffect, OwnerASC);
        else Debug.LogWarning($"[{AbilityName}] activada sin BlockEffect: sin un GE que otorgue el " +
                              $"tag de bloqueo, la barrera nunca se levanta.");

        SubscribeToBarrier();

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.PlayHoldAnimation(this);
        if (netAsc != null)
            netAsc.ServerBroadcastHoldAnimation(this, NetworkAbilitySystemComponent.EHoldAnimationPhase.Start);

        OwnerASC.StartAbilityCoroutine(HoldRoutine());
    }

    // =========================================================
    // REACCIÓN AL GOLPE EN EL ESCUDO
    // =========================================================

    // Se engancha al evento de la barrera mientras dura el mantenido. La suscripción
    // va y viene con el escudo (y no en una sola vez al otorgar la habilidad) porque
    // la barrera se destruye y se vuelve a crear al cambiar de clase.
    private void SubscribeToBarrier()
    {
        UnsubscribeFromBarrier();

        _subscribedBarrier = OwnerASC != null
            ? OwnerASC.GetComponentInChildren<Entity_ShieldBarrier>(true) : null;

        if (_subscribedBarrier == null)
        {
            WarnIfNoBarrier();
            return;
        }

        _subscribedBarrier.OnDamageBlocked += HandleDamageBlocked;
    }

    private void UnsubscribeFromBarrier()
    {
        if (_subscribedBarrier == null) return;
        _subscribedBarrier.OnDamageBlocked -= HandleDamageBlocked;
        _subscribedBarrier = null;
    }

    // La barrera frenó daño: el personaje acusa el impacto. Corre en el servidor (el
    // bloqueo se resuelve ahí), así que la animación se reproduce en el dueño y se
    // replica al resto. Con estrangulador de tiempo: varios impactos seguidos (un
    // ataque rápido, los ticks de un área) reiniciarían el clip en cada frame.
    private void HandleDamageBlocked(float amountBlocked)
    {
        if (!_holding || ShieldHitClip == null || OwnerASC == null) return;
        if (Time.time - _lastShieldHitTime < ShieldHitCooldown) return;
        _lastShieldHitTime = Time.time;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        // En el host el dueño es este mismo proceso; en un dueño remoto la copia
        // server-side no se anima (nadie la ve), y le llega por el RPC de abajo.
        if (pc != null && pc.IsOwner) pc.ApplyHoldImpactAnimation(ShieldHitClip);

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null)
            netAsc.ServerBroadcastHoldAnimation(this, NetworkAbilitySystemComponent.EHoldAnimationPhase.Impact);
    }

    // Vigila el mantenido en el servidor: drena la energía por segundo (si la
    // habilidad la cobra), y lo corta al quedarse sin energía, al morir, o al llegar
    // al tope de tiempo. Soltar el botón lo corta por afuera, vía EndHold().
    private IEnumerator HoldRoutine()
    {
        float elapsed = 0f;

        while (_holding)
        {
            if (OwnerASC == null || OwnerASC.HasTag(EGameplayTag.State_Dead)) break;

            if (EnergyDrainPerSecond > 0f)
            {
                float energy = OwnerASC.GetAttributeValue(EAttributeType.Energy);
                OwnerASC.SetCurrentAttributeValue(EAttributeType.Energy,
                                                  energy - EnergyDrainPerSecond * Time.deltaTime);
            }

            // La energía la consume sobre todo la BARRERA al frenar daño, no este
            // bucle: acá solo detectamos que se agotó, venga de donde venga.
            if (OwnerASC.GetAttributeValue(EAttributeType.Energy) <= 0f) break;

            elapsed += Time.deltaTime;
            if (MaxHoldTime > 0f && elapsed >= MaxHoldTime) break;

            yield return null;
        }

        // Si salimos por una de las condiciones de arriba (y no porque ya lo hayan
        // soltado), cerramos nosotros. EndHold es idempotente.
        if (_holding) EndHold();
    }

    // =========================================================
    // FIN DEL MANTENIDO (IHoldAbility)
    // =========================================================

    // Baja el escudo. La llaman: el dueño al soltar el botón (vía
    // NetworkASC.ServerRequestEndHoldAbility), y HoldRoutine al agotarse la energía,
    // el tiempo, o al morir. Idempotente a propósito: esas vías pueden pisarse
    // (soltás el botón el mismo frame en que se te acaba la energía).
    public void EndHold()
    {
        if (!_holding) return;
        _holding = false;

        UnsubscribeFromBarrier();

        if (OwnerASC == null) return;

        // Retirar el estado de bloqueo apaga la barrera en todos los peers (se va el
        // tag → Entity_ShieldBarrier se baja sola).
        if (BlockEffect != null) OwnerASC.RemoveEffectsByDefinition(BlockEffect);

        // El cooldown va acá, a la salida: es tiempo de recuperación, no un bloqueo
        // mientras sostenés (ver cabecera).
        if (CooldownEffect != null)
            OwnerASC.ApplyGameplayEffect(CooldownEffect, this, ResolveCooldownDuration());

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.StopHoldAnimation();
        if (netAsc != null)
            netAsc.ServerBroadcastHoldAnimation(this, NetworkAbilitySystemComponent.EHoldAnimationPhase.Stop);

        // Libera isAttacking en el dueño (y le avisa por red si es remoto).
        EndAbility();
    }

    // Aviso de configuración: sin una barrera en el prefab de la clase, la habilidad
    // aplica el buff pero no bloquea NADA, y eso sería muy difícil de diagnosticar.
    [System.NonSerialized] private bool _warnedNoBarrier;
    private void WarnIfNoBarrier()
    {
        if (_warnedNoBarrier || OwnerASC == null) return;
        if (OwnerASC.GetComponentInChildren<Entity_ShieldBarrier>(true) != null) return;

        _warnedNoBarrier = true;
        Debug.LogWarning($"[{AbilityName}] El jugador no tiene ningún Entity_ShieldBarrier: el escudo " +
                         $"va a aplicar su buff pero no va a frenar daño. Agregá la barrera como hijo " +
                         $"del PassiveBehaviorsPrefab de la clase.");
    }
}
