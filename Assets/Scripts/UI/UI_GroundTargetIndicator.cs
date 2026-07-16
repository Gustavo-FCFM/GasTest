using UnityEngine;

// ============================================================
// UI_GroundTargetIndicator
//
// Marcador en el suelo (la "X") para las habilidades que se apuntan eligiendo
// una zona — las que implementan IGroundTargetAbility. PlayerController lo
// muestra mientras se mantiene el botón, lo mueve siguiendo la mira, y lo
// esconde al soltar.
//
// Es genérico: el mismo marcador sirve para cualquier habilidad de zona (Mago,
// Clérigo, Pirata...), y se escala según el TargetRadius de cada una para que la
// vista previa sea del tamaño real del área.
//
// Solo es visual y solo corre en el DUEÑO (nadie más necesita ver a dónde está
// por apuntar), así que no lleva nada de red.
//
// SETUP: poné este componente en el prefab "Player Camera" (junto al
// UI_RadialMenu) y asignale un MarkerPrefab.
// ============================================================
public class UI_GroundTargetIndicator : MonoBehaviour
{
    // Instancia única de la escena, para que PlayerController la use sin tener
    // que asignarla a mano (mismo criterio que UI_RadialMenu.Instance).
    public static UI_GroundTargetIndicator Instance;

    [Header("Marcador")]
    [Tooltip("Prefab que se dibuja en el suelo (ej: un quad con una X). Se escala en X/Z al " +
             "diámetro de la zona, así que hacelo de 1x1 unidad para que la escala sea directa.")]
    public GameObject MarkerPrefab;

    [Tooltip("Capas del suelo/entorno sobre las que se apoya el marcador.")]
    public LayerMask GroundLayer;

    // Instancia viva del marcador (se crea una sola vez y se reusa).
    private GameObject _marker;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Muestra el marcador con el tamaño de la zona (radius = radio, así que la
    // escala es el diámetro).
    public void Show(float radius)
    {
        if (MarkerPrefab == null) return;

        if (_marker == null) _marker = Instantiate(MarkerPrefab);

        _marker.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
        _marker.SetActive(true);
    }

    // Mueve el marcador al punto apuntado, apoyándolo en el suelo. El punto de
    // mira puede quedar en el aire (ej. apuntando al cielo o a una pared), así que
    // tiramos un rayo hacia abajo para pegarlo al piso — mismo criterio que el
    // ground-snapping de GA_SpawnTotem.
    public void UpdatePosition(Vector3 aimPoint)
    {
        if (_marker == null) return;

        Vector3 position = aimPoint;
        if (Physics.Raycast(aimPoint + Vector3.up * 10f, Vector3.down,
                            out RaycastHit hit, 30f, GroundLayer, QueryTriggerInteraction.Ignore))
            position = hit.point;

        // Offset chico hacia arriba para que no pelee con el suelo (z-fighting).
        _marker.transform.position = position + Vector3.up * 0.05f;
    }

    public void Hide()
    {
        if (_marker != null) _marker.SetActive(false);
    }
}
