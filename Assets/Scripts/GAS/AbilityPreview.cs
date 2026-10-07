using UnityEngine;
using System.Collections.Generic;

// ============================================================
// AbilityPreview  (el muñeco de prueba)
//
// Herramienta SOLO DE EDITOR para dimensionar habilidades sin adivinar. Ponelo en un
// GameObject vacío en la escena (o con el botón "Poner un muñeco de prueba" del
// inspector de cualquier habilidad) y dibuja las formas reales (cono, línea, área,
// trayectoria del proyectil, etc.) desde la posición y rotación de ese objeto:
//
//  · La habilidad que tengas ELEGIDA en el Project se dibuja sola, con sus MANIJAS: se
//    ajusta arrastrando en la Scene view (alcance, radio, ángulo, desde dónde sale) y el
//    número cambia en el asset, con Ctrl+Z.
//  · Las de la lista Abilities se dibujan siempre (para comparar varias a la vez); con
//    el muñeco seleccionado, también con manijas.
//
// Sirve para lo que antes había que hacer a ojo:
//  · Comparar el alcance de varias habilidades a la vez.
//  · Medir contra un enemigo real: movés este objeto al lado de un personaje de la
//    escena y ves si el cono lo agarra.
//  · Ajustar los números y ver el cambio al instante, sin entrar a Play.
//
// Los anillos de distancia (con su etiqueta en metros) y la silueta de un personaje
// (1.8 m) dan la referencia que falta para saber si "2 de Range" es corto o largo.
//
// No hace NADA en el juego: es puro Gizmo. Podés dejarlo en la escena de pruebas.
// ============================================================
public class AbilityPreview : MonoBehaviour
{
    [Header("Habilidades a previsualizar")]
    [Tooltip("Assets de habilidad que se dibujan SIEMPRE desde este objeto (para comparar). La " +
             "habilidad elegida en el Project se dibuja aparte, sin tener que agregarla acá.")]
    public List<GameplayAbility> Abilities = new List<GameplayAbility>();

    [Tooltip("Dibuja también la habilidad (o habilidades) elegida en el Project, con sus manijas.")]
    public bool ShowSelectedAbility = true;

    [Header("Regla de distancia")]
    [Tooltip("Dibuja anillos concéntricos para medir alcances de un vistazo.")]
    public bool DrawDistanceRings = true;
    [Tooltip("Separación entre anillos, en metros.")]
    public float RingSpacing = 1f;
    [Tooltip("Cuántos anillos dibujar.")]
    public int RingCount = 10;

    [Header("Visualización")]
    [Tooltip("Dibuja una silueta de personaje (1.8 m de alto) con una flecha hacia adelante: la " +
             "escala y hacia dónde sale cada habilidad.")]
    public bool DrawBody = true;

    [Tooltip("Si está activo, las habilidades de la lista solo se dibujan al SELECCIONAR este " +
             "objeto. Desactivalo para verlas siempre (útil al mover otros objetos alrededor). La " +
             "habilidad elegida en el Project se dibuja igual.")]
    public bool OnlyWhenSelected = true;

    private void OnDrawGizmosSelected()
    {
        if (OnlyWhenSelected) DrawList();
    }

    private void OnDrawGizmos()
    {
        if (DrawBody) DrawBodyGizmo();
        if (!OnlyWhenSelected) DrawList();

#if UNITY_EDITOR
        // La habilidad elegida en el Project (un asset no tiene OnDrawGizmos propio).
        if (ShowSelectedAbility)
        {
            bool any = false;
            foreach (Object o in UnityEditor.Selection.objects)
            {
                if (!(o is GameplayAbility ability)) continue;
                ability.DrawGizmos(transform);
                any = true;
            }
            // Los anillos, si la lista no los dibujó ya (la dibuja siempre, o con el muñeco elegido).
            bool listDrewRings = !OnlyWhenSelected || UnityEditor.Selection.Contains(gameObject);
            if (any && DrawDistanceRings && !listDrewRings) DrawRings();
        }
#endif
    }

    private void DrawList()
    {
        if (DrawDistanceRings) DrawRings();

        // Cada habilidad dibuja SU propia forma (el mismo DrawGizmos que usa el
        // PlayerController), así que lo que ves es la geometría real que se va a usar.
        if (Abilities == null) return;
        foreach (GameplayAbility ability in Abilities)
            if (ability != null) ability.DrawGizmos(transform);
    }

    // Una cápsula de alambre del tamaño de un personaje, y una flecha hacia adelante.
    private void DrawBodyGizmo()
    {
        const float height = 1.8f, radius = 0.35f;
        Vector3 p = transform.position;

        Gizmos.color = new Color(1f, 1f, 1f, 0.45f);
        DrawCircle(p + Vector3.up * radius, radius, 20);
        DrawCircle(p + Vector3.up * (height - radius), radius, 20);
        foreach (Vector3 side in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            Vector3 s = transform.rotation * side * radius;
            Gizmos.DrawLine(p + s + Vector3.up * radius, p + s + Vector3.up * (height - radius));
        }
        Gizmos.DrawWireSphere(p + Vector3.up * (height - 0.15f), 0.15f);

        // Hacia dónde mira: de donde salen los conos, las líneas, los proyectiles.
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
        Vector3 chest = p + Vector3.up * 1.2f;
        Vector3 tip   = chest + transform.forward * 0.9f;
        Gizmos.DrawLine(chest, tip);
        Gizmos.DrawLine(tip, tip - transform.forward * 0.2f + transform.right * 0.12f);
        Gizmos.DrawLine(tip, tip - transform.forward * 0.2f - transform.right * 0.12f);
    }

    // Anillos concéntricos en el piso, con la distancia escrita en cada uno.
    private void DrawRings()
    {
        if (RingSpacing <= 0f || RingCount <= 0) return;

        for (int i = 1; i <= RingCount; i++)
        {
            float radius = RingSpacing * i;
            // Cada 5 anillos, uno más marcado para leer la escala más rápido.
            Gizmos.color = (i % 5 == 0) ? new Color(1f, 1f, 1f, 0.5f)
                                        : new Color(1f, 1f, 1f, 0.18f);
            DrawCircle(transform.position, radius);

#if UNITY_EDITOR
            UnityEditor.Handles.color = new Color(1f, 1f, 1f, 0.6f);
            UnityEditor.Handles.Label(transform.position + transform.forward * radius,
                                      $"{radius:0.#}m");
#endif
        }
    }

    // Círculo horizontal hecho con segmentos (Gizmos no tiene primitiva de círculo).
    private static void DrawCircle(Vector3 center, float radius, int segments = 48)
    {
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (Mathf.PI * 2f) * i / segments;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev, point);
            prev = point;
        }
    }
}
