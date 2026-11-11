using UnityEngine;

// ============================================================
// MatchEvent_Gates
//
// Cuándo se levantan las rejas de las bases. Solo da el segundo: cada reja (MercGate) lo lee y se
// mueve sola en cada pantalla, como siempre. En 0 se abren apenas arranca la partida.
// ============================================================
public class MatchEvent_Gates : MatchEvent
{
    protected override void Reset()
    {
        base.Reset();
        StartAt = 0f;
    }
}
