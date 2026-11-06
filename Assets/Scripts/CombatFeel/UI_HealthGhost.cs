using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_HealthGhost
//
// El daño fantasma: al recibir un golpe la vida baja al instante y queda un tramo de otro color
// que se vacía un momento después. Se ve de un vistazo cuánto sacó el golpe.
//
// Es una Image dentro del Fill Area de una barra de vida, DETRÁS del relleno. Solo mira cuánto
// marca la barra, así que nadie le avisa nada y sirve igual en tu barra y en la de arriba de la
// cabeza. El COLOR de la Image es el del tramo. Sus tiempos, en el asset CombatFeel.
//
// VIVE EN: Prefabs/UI/Nameplate (HealthBar ▸ Fill Area ▸ HealthGhost) y en Player Camera (la
// barra del HUD).
// ============================================================
[RequireComponent(typeof(Image))]
public class UI_HealthGhost : MonoBehaviour
{
    [Tooltip("La barra de vida a la que sigue.")]
    [Label("Barra de vida")]
    public Slider HealthBar;

    private Image _image;
    private float _ghost = -1f;   // hasta dónde llega el tramo (0 a 1); -1 = todavía no leyó la barra
    private float _lastHealth;
    private float _shrinksAt;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.raycastTarget = false;
        _image.enabled = false;
    }

    // Una barra que estuvo escondida no muestra lo que pasó mientras tanto.
    private void OnEnable() => _ghost = -1f;

    private void LateUpdate()
    {
        if (HealthBar == null || HealthBar.fillRect == null) return;

        float health = HealthBar.normalizedValue;
        CombatFeel feel = CombatFeel.Config;

        if (_ghost < 0f || health >= _ghost || !CombatFeel.HealthGhostOn)
            _ghost = health;
        else if (health < _lastHealth)
            _shrinksAt = Time.time + feel.GhostDelaySeconds;      // otro golpe: el tramo espera de nuevo
        else if (Time.time >= _shrinksAt)
            _ghost = Mathf.MoveTowards(_ghost, health, feel.GhostShrinkPerSecond * Time.deltaTime);

        _lastHealth = health;

        bool show = _ghost > health + 0.001f;
        if (_image.enabled != show) _image.enabled = show;
        if (show) Fit(HealthBar.fillRect);
    }

    // De 0 hasta donde llega el tramo, con la misma forma que el relleno de vida leída EN VIVO: al
    // relleno de un Slider lo maneja el Slider, y en el prefab sus anclas quedan guardadas en 0.
    private void Fit(RectTransform fill)
    {
        RectTransform rt = (RectTransform)transform;

        rt.anchorMin = new Vector2(0f, fill.anchorMin.y);
        rt.anchorMax = new Vector2(_ghost, fill.anchorMax.y);
        rt.pivot     = fill.pivot;
        rt.offsetMin = fill.offsetMin;
        rt.offsetMax = fill.offsetMax;
    }
}
