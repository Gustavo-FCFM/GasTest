using UnityEngine;

// ============================================================
// MatchEvent_Objective
//
// Cuándo aparece el PRIMER Objetivo. Solo da el segundo: MercenariesGameMode lo lee al arrancar la
// partida y hace lo de siempre (la cuenta atrás del marcador, el aviso, y los Objetivos siguientes
// tras cada entrega). Manda sobre "Primer Objetivo a los (s)" del modo.
// ============================================================
public class MatchEvent_Objective : MatchEvent
{
    protected override void Reset()
    {
        base.Reset();
        StartAt = 120f;
    }
}
