using UnityEngine;
using System.Collections.Generic;

// ============================================================
// AbilityPreview
//
// Herramienta SOLO DE EDITOR para dimensionar habilidades sin adivinar. Ponelo en un
// GameObject vacío en la escena, arrastrale los assets de las habilidades que quieras
// ver, y dibuja sus formas reales (cono, línea, área, trayectoria del proyectil, etc.)
// desde la posición y rotación de ese objeto.
//
// Sirve para lo que antes había que hacer a ojo:
//  · Comparar el alcance de varias habilidades a la vez.
//  · Medir contra un enemigo real: movés este objeto al lado de un personaje de la
//    escena y ves si el cono lo agarra.
//  · Ajustar los números en el asset y ver el cambio al instante, sin entrar a Play.
//
// Los anillos de distancia (con su etiqueta en metros) dan la referencia que falta
// para saber si "2 de Range" es corto o largo en tu escala.
//
// No hace NADA en el juego: es puro Gizmo. Podés dejarlo en la escena de pruebas.
// ============================================================
public class AbilityPreview : MonoBehaviour
{
    [Header("Habilidades a previsualizar")]
    [Tooltip("Los assets de habilidad cuyas formas querés ver dibujadas desde este objeto.")]
    public List<GameplayAbility> Abilities = new List<GameplayAbility>();

    [Header("Regla de distancia")]
    [Tooltip("Dibuja anillos concéntricos para medir alcances de un vistazo.")]
    public bool DrawDistanceRings = true;
    [Tooltip("Separación entre anillos, en metros.")]
    public float RingSpacing = 1f;
    [Tooltip("Cuántos anillos dibujar.")]
    public int RingCount = 10;

    [Header("Visualización")]
    [Tooltip("Si está activo, las formas solo se dibujan al SELECCIONAR este objeto. " +
             "Desactivalo para verlas siempre (útil al mover otros objetos alrededor).")]
    public bool OnlyWhenSelected = true;

    private void OnDrawGizmosSelected()
    {
        if (OnlyWhenSelected) DrawPreview();
    }

    private void OnDrawGizmos()
    {
        if (!OnlyWhenSelected) DrawPreview();
    }

    private void DrawPreview()
    {
        if (DrawDistanceRings) DrawRings();

        // Cada habilidad dibuja SU propia forma (el mismo DrawGizmos que usa el
        // PlayerController), así que lo que ves es la geometría real que se va a usar.
        if (Abilities == null) return;
        foreach (GameplayAbility ability in Abilities)
            if (ability != null) ability.DrawGizmos(transform);
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
