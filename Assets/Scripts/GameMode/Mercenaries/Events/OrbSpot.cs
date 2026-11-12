using System.Collections.Generic;
using UnityEngine;

// ============================================================
// OrbSpot
//
// Un lugar donde puede aparecer un ORBE DE DEFINITIVA (11 de noviembre de 2026, pedido de Gustavo):
// uno por sarcófago del Cementerio. Va como hijo del sarcófago y el orbe flota sobre su tapa, así que si
// el sarcófago se baja o se mueve, el orbe va con él (la altura se mide al aparecer, del collider o de la
// malla del padre).
//
// Cuáles están activos lo decide y lo sincroniza MatchEvent_Orbs; este componente solo dice dónde está
// y, en cada pantalla, pone o saca el modelo del orbe ("Orbe" del evento).
//
// NO ES UN OBJETO DE RED, como los santuarios y las campanas: se ordenan por su posición (ver All) y el
// evento los nombra por ese número.
// ============================================================
public class OrbSpot : MonoBehaviour
{
    [Tooltip("A qué altura flota el orbe sobre la tapa del sarcófago.")]
    [Label("Flota a (m)")]
    public float Hover = 0.9f;

    // ¿Tiene un orbe ahora? Vale en cualquier máquina (lo sincroniza MatchEvent_Orbs).
    public bool HasOrb => MatchEvent_Orbs.Instance != null && MatchEvent_Orbs.Instance.IsActive(Index);

    // Donde está el orbe: sobre lo más alto del sarcófago (el padre), o sobre este punto si no hay padre.
    public Vector3 OrbPoint
    {
        get
        {
            Transform body = transform.parent;
            Vector3 p = transform.position;
            float top = p.y;

            if (body != null)
            {
                if (body.TryGetComponent(out Collider col) && col.enabled) top = col.bounds.max.y;
                else if (body.TryGetComponent(out Renderer rend)) top = rend.bounds.max.y;
            }
            return new Vector3(p.x, top + Hover, p.z);
        }
    }

    // =========================================================
    // TODOS LOS LUGARES, EN EL MISMO ORDEN EN TODAS LAS MÁQUINAS
    // =========================================================

    private static readonly List<OrbSpot> s_all = new List<OrbSpot>();
    private static bool s_sorted;

    public static IReadOnlyList<OrbSpot> All
    {
        get
        {
            if (!s_sorted)
            {
                s_all.RemoveAll(s => s == null);
                s_all.Sort(CompareByPosition);
                s_sorted = true;
            }
            return s_all;
        }
    }

    public static OrbSpot ByIndex(int index) => index >= 0 && index < All.Count ? All[index] : null;

    private int _index = -1;
    private bool _indexValid;

    public int Index
    {
        get
        {
            if (_indexValid && s_sorted) return _index;
            IReadOnlyList<OrbSpot> all = All;
            _index = -1;
            for (int i = 0; i < all.Count; i++) if (all[i] == this) { _index = i; break; }
            _indexValid = true;
            return _index;
        }
    }

    // Por posición redondeada al centímetro: x, después z, después y.
    private static int CompareByPosition(OrbSpot a, OrbSpot b)
    {
        Vector3Int pa = Vector3Int.RoundToInt(a.transform.position * 100f);
        Vector3Int pb = Vector3Int.RoundToInt(b.transform.position * 100f);
        if (pa.x != pb.x) return pa.x.CompareTo(pb.x);
        if (pa.z != pb.z) return pa.z.CompareTo(pb.z);
        return pa.y.CompareTo(pb.y);
    }

    private void OnEnable()
    {
        if (!s_all.Contains(this)) s_all.Add(this);
        s_sorted = false;
        InvalidateAll();
    }

    private void OnDisable()
    {
        s_all.Remove(this);
        s_sorted = false;
        InvalidateAll();
        HideOrb();
    }

    private static void InvalidateAll()
    {
        foreach (OrbSpot s in s_all) if (s != null) s._indexValid = false;
    }

    // =========================================================
    // EL ORBE EN ESTA PANTALLA
    // =========================================================

    private GameObject _orb;

    private void Update()
    {
        bool wanted = HasOrb;
        if (wanted && _orb == null) ShowOrb();
        else if (!wanted && _orb != null) HideOrb();
    }

    private void ShowOrb()
    {
        MatchEvent_Orbs ev = MatchEvent_Orbs.Instance;
        if (ev == null || ev.OrbVisual == null) return;

        // Suelto en la escena, no colgado del sarcófago: el sarcófago tiene escala desigual y lo deformaría.
        _orb = Instantiate(ev.OrbVisual, OrbPoint, Quaternion.identity);
        _orb.name = "UltOrb";
    }

    private void HideOrb()
    {
        if (_orb != null) Destroy(_orb);
        _orb = null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.8f);
        Gizmos.DrawWireSphere(OrbPoint, 0.3f);
    }
}
