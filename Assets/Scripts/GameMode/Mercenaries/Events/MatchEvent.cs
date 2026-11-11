using FishNet.Object;
using UnityEngine;

// ============================================================
// MatchEvent
//
// Un evento de la partida: algo que pasa a cierto segundo de la partida (las rejas se abren, aparece
// el Objetivo, empiezan los santuarios). Cada evento es un HIJO del prefab de eventos
// (Prefabs/GameMode/MatchEvents) y GameModeEventManager los va prendiendo según el reloj.
//
// PARA UN EVENTO NUEVO: una clase que herede de esta, que sobreescriba lo que necesite
// (OnEventBegin, OnEventTick…) y un hijo más en el prefab con ese componente. Nada más: el manager
// lo encuentra solo.
//
// Es un NetworkBehaviour para que el evento que tenga algo que mostrar en todas las pantallas (los
// santuarios) lo sincronice con sus SyncVar. Los que solo dicen "a qué segundo" no usan la red.
// Todo lo de abajo corre en el SERVIDOR; las pantallas solo leen lo sincronizado.
// ============================================================
public abstract class MatchEvent : NetworkBehaviour
{
    [Tooltip("Segundos desde que arranca la partida (después de la preparación).")]
    [Label("Empieza a los (s)")]
    public float StartAt;

    // Ya empezó en esta partida (solo servidor).
    public bool HasBegun { get; private set; }

    // Los llama GameModeEventManager.
    internal void ServerReset()
    {
        HasBegun = false;
        OnEventReset();
    }

    internal void ServerBegin()
    {
        HasBegun = true;
        OnEventBegin();
    }

    internal void ServerTick(float deltaTime) => OnEventTick(deltaTime);

    internal void ServerEnd() => OnEventEnd();

    // Arranca una partida nueva: dejar todo como al principio.
    protected virtual void OnEventReset() { }

    // Llegó su segundo.
    protected virtual void OnEventBegin() { }

    // Cada cuadro desde que empezó, mientras dure la partida.
    protected virtual void OnEventTick(float deltaTime) { }

    // Se terminó la partida (o se volvió a la sala).
    protected virtual void OnEventEnd() { }
}
