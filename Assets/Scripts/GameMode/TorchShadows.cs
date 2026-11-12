using System.Collections.Generic;
using UnityEngine;

// ============================================================
// TorchShadows
//
// Las sombras de las antorchas (12 de noviembre de 2026, pedido de Gustavo). Una luz puntual con sombra dibuja la
// escena SEIS veces más (una por cara de un cubo): con las 234 del Cementerio sería imposible. Así que solo las más
// cercanas a la cámara la proyectan, y se van pasando el turno mientras uno camina: se ve como si todas la tuvieran.
//
//   · Cada antorcha (TorchFlicker) se anota al prenderse. Cada 0.25 s se eligen las N más cercanas a la cámara
//     dentro de un radio; las elegidas tienen ventaja (cuentan como un 15 % más cerca) para que dos antorchas a la
//     misma distancia no se peleen el turno.
//   · La sombra aparece y se va con un fundido (TorchFlicker la lleva), no de golpe.
//   · Cuántas y hasta dónde se leen del prefab Torch ("Sombras: cuántas a la vez", "Sombras: hasta (m)").
//   · Ajustes ▸ Pantalla ▸ "Sombras de las antorchas" (GameSettings.TorchShadows) las apaga todas.
//
// No es de red: cada pantalla elige las suyas, según su cámara. La luna (la direccional) hace sombras aparte.
// ============================================================
public static class TorchShadows
{
    private static readonly List<TorchFlicker> All = new List<TorchFlicker>();
    private static readonly HashSet<TorchFlicker> Chosen = new HashSet<TorchFlicker>();
    private static readonly List<(float dist, TorchFlicker torch)> Ranking = new List<(float, TorchFlicker)>();

    private const float RankEvery = 0.25f;    // s entre una elección y la siguiente
    private const float StickyBonus = 0.85f;  // la elegida cuenta como un 15 % más cerca

    private static int _lastFrame = -1;
    private static float _nextRank;
    private static Camera _cam;

    public static void Register(TorchFlicker torch)
    {
        if (torch != null && !All.Contains(torch)) All.Add(torch);
    }

    public static void Unregister(TorchFlicker torch)
    {
        All.Remove(torch);
        Chosen.Remove(torch);
    }

    // ¿Esta antorcha tiene que proyectar sombra ahora?
    public static bool IsChosen(TorchFlicker torch) => Chosen.Contains(torch);

    // La llama cada antorcha en su Update; trabaja una vez por cuadro (la primera que llega) y elige cada 0.25 s.
    public static void Tick(TorchFlicker config)
    {
        if (Time.frameCount == _lastFrame) return;
        _lastFrame = Time.frameCount;
        if (Time.unscaledTime < _nextRank) return;
        _nextRank = Time.unscaledTime + RankEvery;

        Chosen.RemoveWhere(t => t == null);
        if (!GameSettings.TorchShadows || config.ShadowCount <= 0) { Chosen.Clear(); return; }

        // La cámara de quien mira. Se busca de nuevo si se apagó (al aparecer, el jugador apaga la del lobby).
        if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
        if (_cam == null) { Chosen.Clear(); return; }

        Vector3 eye = _cam.transform.position;
        float maxSqr = config.ShadowRange * config.ShadowRange;

        Ranking.Clear();
        foreach (TorchFlicker t in All)
        {
            if (t == null || t.Glow == null || !t.Glow.isActiveAndEnabled) continue;
            float d = (t.Glow.transform.position - eye).sqrMagnitude;
            if (d > maxSqr) continue;
            if (Chosen.Contains(t)) d *= StickyBonus * StickyBonus;   // al cuadrado: d está al cuadrado
            Ranking.Add((d, t));
        }
        Ranking.Sort((a, b) => a.dist.CompareTo(b.dist));

        Chosen.Clear();
        for (int i = 0; i < Ranking.Count && i < config.ShadowCount; i++) Chosen.Add(Ranking[i].torch);
    }
}
