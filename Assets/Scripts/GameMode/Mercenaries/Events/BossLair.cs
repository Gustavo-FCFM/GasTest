using System.Collections.Generic;
using UnityEngine;

// ============================================================
// BossLair
//
// Donde puede aparecer el JEFE del evento de mitad de partida (MatchEvent_Boss, 11 de noviembre de
// 2026). Es solo un punto en la escena: si hay varios, el evento elige uno al azar. En el Cementerio hay
// uno en la capilla del centro, a la misma distancia de las tres bases.
//
// Se mueve a mano. El jefe aparece sobre el NavMesh más cercano a este punto y, como cualquier NPC,
// vuelve a él si lo alejan demasiado.
// ============================================================
public class BossLair : MonoBehaviour
{
    private static readonly List<BossLair> s_all = new List<BossLair>();

    public static IReadOnlyList<BossLair> All => s_all;

    private void OnEnable()
    {
        if (!s_all.Contains(this)) s_all.Add(this);
    }

    private void OnDisable()
    {
        s_all.Remove(this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.8f, 0.1f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 1f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 4f);
    }
}
