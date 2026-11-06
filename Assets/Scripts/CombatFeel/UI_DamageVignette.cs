using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_DamageVignette
//
// Los bordes de la pantalla: al recibir algo, las orillas de TU pantalla se tiñen y se apagan
// rápido, más fuerte cuanto más grande fue. Rojo si te pegaron, verde si te curaron, celeste si
// un escudo frenó el golpe entero: el mismo código de colores de los contornos, visto desde
// adentro. La viñeta de poca vida (UI_LowHealthVignette) es otra cosa: late mientras dura el
// peligro; esta dura un instante.
//
// Lo avisa el número flotante del jugador local (NetworkASC.ObserversCombatNumber, por
// CombatFeel.LocalCombatNumber).
//
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ CombatFeel ▸ DamageVignette): una Image que cubre la
// pantalla con la misma textura de la viñeta. El COLOR de la Image es el del daño a toda fuerza,
// opacidad incluida; el verde y el celeste, y los demás números, en el asset CombatFeel.
// ============================================================
[RequireComponent(typeof(Image))]
public class UI_DamageVignette : MonoBehaviour
{
    private Image _image;
    private Color _damageColor;
    private Color _color;
    private float _strength;   // 0 a 1

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.raycastTarget = false;
        _damageColor = _image.color;
        _color = _damageColor;
        _image.enabled = false;
    }

    private void OnEnable()  => CombatFeel.LocalCombatNumber += OnCombatNumber;
    private void OnDisable() => CombatFeel.LocalCombatNumber -= OnCombatNumber;

    private void OnCombatNumber(ECombatNumber kind, bool shielded, float percent)
    {
        if (!CombatFeel.DamageVignetteOn) return;

        CombatFeel feel = CombatFeel.Config;
        bool heal = kind == ECombatNumber.Heal;

        if ((heal || shielded) && !feel.ColoredEdges) return;

        float min = heal ? feel.EdgeHealMinPercent : feel.VignetteMinHitPercent;
        if (percent < min) return;

        float power    = Mathf.InverseLerp(min, feel.VignetteFullHitPercent, percent);
        float strength = Mathf.Lerp(feel.VignetteWeakStrength, 1f, power);

        // Una curación chica no le cambia el color a un golpe fuerte que se está apagando.
        if (strength < _strength) return;

        _strength = strength;
        _color    = heal ? feel.EdgeHealColor : shielded ? feel.EdgeShieldColor : _damageColor;
    }

    private void Update()
    {
        _strength = Mathf.MoveTowards(_strength, 0f, Time.deltaTime / Mathf.Max(0.01f, CombatFeel.Config.VignetteFadeSeconds));

        Color c = _color;
        c.a = _color.a * _strength;
        _image.color   = c;
        _image.enabled = c.a > 0.001f;
    }
}
