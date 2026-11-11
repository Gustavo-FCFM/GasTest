// ============================================================
// ShrineCaptureRules
//
// Las cuentas del "rey de la montaña" de los santuarios, solas, sin Unity ni red (11 de noviembre de
// 2026, las reglas de Gustavo):
//   · Cada equipo junta su propio porcentaje, de 0 a 100.
//   · Suma lo que le den (por segundo dentro, por bajas en la zona) aunque haya otro equipo.
//   · Pero solo puede llegar a 100 si NO hay otro equipo vivo dentro: si lo hay, se frena en 99.
//   · El primero que llega a 100 lo captura.
//
// MatchEvent_Shrines le pasa quién está dentro y cuánto sumar; esto devuelve si alguien capturó.
// Está aparte para poder probar las reglas sin abrir una partida.
// ============================================================
public sealed class ShrineCaptureRules
{
    public const int Full = 100;

    private readonly int[] _progress;

    // Hasta dónde llega un equipo con otro dentro. 99 con las reglas de Gustavo.
    public int ContestedCap = 99;

    public ShrineCaptureRules(int teamCount)
    {
        _progress = new int[teamCount];
    }

    public int TeamCount => _progress.Length;

    // El porcentaje de un equipo (índice 0 = equipo 1).
    public int Get(int teamIndex) => _progress[teamIndex];

    public void Clear()
    {
        for (int i = 0; i < _progress.Length; i++) _progress[i] = 0;
    }

    // ¿Hay algún OTRO equipo dentro, aparte de ese?
    public static bool AnotherTeamInside(int teamIndex, bool[] inside)
    {
        for (int i = 0; i < inside.Length; i++)
            if (i != teamIndex && inside[i]) return true;
        return false;
    }

    // Le suma al equipo y devuelve true si con eso llegó a 100 (capturó).
    public bool Add(int teamIndex, int amount, bool[] inside)
    {
        if (teamIndex < 0 || teamIndex >= _progress.Length || amount <= 0) return false;

        int cap = AnotherTeamInside(teamIndex, inside) ? ContestedCap : Full;
        int value = _progress[teamIndex];

        // Si ya estaba arriba del tope (no debería), no se le baja: solo no sube.
        if (value < cap) value = System.Math.Min(cap, value + amount);

        _progress[teamIndex] = value;
        return value >= Full;
    }
}
