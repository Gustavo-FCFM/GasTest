using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// MenuNavigation
//
// Enlaza a mano (navegación Explicit) los controles de una pantalla de menú, POR FILAS, para que
// el stick los recorra en orden y sin salirse de ella (28 de octubre de 2026). Lo usan las
// pantallas que no cambian mientras están abiertas: el teclado en pantalla y los Ajustes.
//
// POR QUÉ: la navegación automática de Unity elige, para cada dirección, el control más cercano
// de TODA la escena, también los de la pantalla de atrás. Con el teclado o los Ajustes abiertos
// encima de otro menú, desde algunas teclas o desde la barra de sensibilidad el stick saltaba a
// un botón de atrás, MenuInput lo devolvía y no había forma de seguir (Gustavo: "a veces no puedo
// pasarme de unas letras a otras", "no puedo bajar más allá de la sensibilidad"). Y aun dentro de
// la pantalla, la automática se salta lo que queda corrido a un costado (la casilla de Invertir
// Y, a la derecha, debajo de la barra de sensibilidad).
//
// LAS FILAS salen de DÓNDE están los controles, así que se pueden mover, copiar o borrar a mano en
// el prefab: los que tienen el centro a la misma altura (menos de medio control de diferencia)
// son una fila. Izquierda y derecha recorren la fila; arriba y abajo van al control de la fila de
// al lado que queda más cerca en horizontal. Una barra (Slider) no se enlaza a lo largo: así el
// stick le cambia el valor, como siempre.
//
// Las pantallas que cambian solas (la sala) siguen con la automática, y MenuInput no deja que el
// stick se salga de la pantalla de arriba.
// ============================================================
public static class MenuNavigation
{
    private struct Cell
    {
        public Selectable Control;
        public Rect       Rect;   // en el espacio de root
    }

    private static readonly Vector3[] _corners = new Vector3[4];

    // Enlaza los controles activos de debajo de root. wrapRows: desde una punta de la fila se pasa
    // a la otra (el teclado; en los Ajustes no). Llamarlo con la pantalla ya prendida.
    public static void LinkRows(Transform root, bool wrapRows)
    {
        if (root == null) return;
        if (root is RectTransform rootRect) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);

        var cells = new List<Cell>();
        foreach (Selectable s in root.GetComponentsInChildren<Selectable>())
        {
            if (!s.IsActive() || !s.IsInteractable() || s.navigation.mode == Navigation.Mode.None) continue;
            if (!(s.transform is RectTransform rt)) continue;

            rt.GetWorldCorners(_corners);
            Vector2 a = root.InverseTransformPoint(_corners[0]);
            Vector2 b = root.InverseTransformPoint(_corners[2]);
            cells.Add(new Cell { Control = s, Rect = Rect.MinMaxRect(
                Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)) });
        }

        // Las filas, de arriba hacia abajo, y cada una de izquierda a derecha.
        cells.Sort((p, q) => q.Rect.center.y.CompareTo(p.Rect.center.y));
        var rows = new List<List<Cell>>();
        foreach (Cell cell in cells)
        {
            List<Cell> row = rows.Count > 0 ? rows[rows.Count - 1] : null;
            if (row == null || Mathf.Abs(row[0].Rect.center.y - cell.Rect.center.y) > row[0].Rect.height * 0.5f)
                rows.Add(row = new List<Cell>());
            row.Add(cell);
        }
        foreach (List<Cell> row in rows) row.Sort((p, q) => p.Rect.center.x.CompareTo(q.Rect.center.x));

        for (int r = 0; r < rows.Count; r++)
        {
            List<Cell> row = rows[r];
            for (int i = 0; i < row.Count; i++)
            {
                Slider slider = row[i].Control as Slider;
                bool sideways = slider != null && (slider.direction == Slider.Direction.LeftToRight ||
                                                   slider.direction == Slider.Direction.RightToLeft);
                bool upright  = slider != null && !sideways;
                bool first = i == 0, last = i == row.Count - 1;
                float x = row[i].Rect.center.x;

                row[i].Control.navigation = new Navigation
                {
                    mode          = Navigation.Mode.Explicit,
                    selectOnLeft  = sideways || (first && !wrapRows) ? null : row[(i - 1 + row.Count) % row.Count].Control,
                    selectOnRight = sideways || (last && !wrapRows)  ? null : row[(i + 1) % row.Count].Control,
                    selectOnUp    = upright || r == 0               ? null : Closest(rows[r - 1], x),
                    selectOnDown  = upright || r == rows.Count - 1  ? null : Closest(rows[r + 1], x),
                };
            }
        }
    }

    // El control de esa fila más cercano en horizontal a x (uno que tenga x encima, si hay: la
    // barra espaciadora para las letras de arriba de ella).
    private static Selectable Closest(List<Cell> row, float x)
    {
        Selectable best = null;
        float bestDistance = float.MaxValue;
        foreach (Cell cell in row)
        {
            float d = x < cell.Rect.xMin ? cell.Rect.xMin - x : x > cell.Rect.xMax ? x - cell.Rect.xMax : 0f;
            if (d < bestDistance) { bestDistance = d; best = cell.Control; }
        }
        return best;
    }
}
