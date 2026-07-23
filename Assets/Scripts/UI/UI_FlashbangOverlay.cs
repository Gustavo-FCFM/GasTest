using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_FlashbangOverlay  (efecto "flashbang" del Status_Blinded)
//
// Ilumina una imagen blanca de pantalla completa cuando el JUGADOR LOCAL recibe el
// tag Status_Blinded (se lo aplica la Copia exacta del Ilusionista al enemigo que
// la golpea). Es un flash instantáneo que se desvanece de a poco: te ciega en el
// momento y recuperás la visión gradualmente.
//
// Es 100% client-side, igual que PlayerVisibility: el tag viaja a todos los
// clientes por NetTags, así que la copia local del jugador cegado tiene el tag en
// SU pantalla. Este overlay solo mira al jugador local (PlayerController.LocalPlayer)
// y no sincroniza nada.
//
// SETUP: un Canvas (Screen Space - Overlay) con un sort order alto (por encima del
// HUD) → una Image blanca que cubra toda la pantalla, con "Raycast Target" APAGADO
// (no debe bloquear clics). Poné este componente (en el Canvas o la Image) y asigná
// 'Overlay'. El script solo maneja el ALPHA de la imagen; el color/sprite lo elegís
// vos (blanco puro = flashbang clásico). Consejo: la duración del GE_Blinded conviene
// que sea parecida a FadeDuration para que "estar cegado" dure lo que se ve.
// ============================================================
public class UI_FlashbangOverlay : MonoBehaviour
{
    [Tooltip("Imagen blanca de pantalla completa. Este script solo maneja su alpha.")]
    public Image Overlay;
    [Tooltip("Segundos desde el flash total hasta despejarse por completo.")]
    public float FadeDuration = 2f;
    [Range(0f, 1f)]
    [Tooltip("Opacidad máxima del flash (1 = blanco total en el instante del golpe).")]
    public float MaxAlpha = 1f;
    [Tooltip("Curva del desvanecimiento (X = progreso 0..1 del fade, Y = intensidad). Por defecto lineal descendente.")]
    public AnimationCurve FadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    private float _fadeTimer = float.MaxValue; // tiempo transcurrido del fade actual (arranca "ya despejado")
    private bool  _wasBlinded;
    private AbilitySystemComponent _localASC;

    private void Start()
    {
        // Arranca despejado.
        if (Overlay != null) { _fadeTimer = float.MaxValue; ApplyAlpha(0f); }
    }

    private void Update()
    {
        // ASC del jugador local (puede aparecer tarde, o cambiar al respawnear).
        if (_localASC == null)
        {
            PlayerController local = PlayerController.LocalPlayer;
            _localASC = local != null ? local.GetComponent<AbilitySystemComponent>() : null;
        }

        bool blinded = _localASC != null && _localASC.HasTag(EGameplayTag.Status_Blinded);

        // Flanco de subida (no cegado → cegado): dispara el flash desde el máximo.
        if (blinded && !_wasBlinded) _fadeTimer = 0f;
        _wasBlinded = blinded;

        // Avanza el desvanecimiento. Fuera de un flash, _fadeTimer >= FadeDuration → alpha 0.
        float alpha = 0f;
        if (_fadeTimer < FadeDuration)
        {
            float k = FadeDuration > 0f ? _fadeTimer / FadeDuration : 1f;
            alpha = MaxAlpha * Mathf.Clamp01(FadeCurve.Evaluate(k));
            _fadeTimer += Time.deltaTime;
        }

        ApplyAlpha(alpha);
    }

    // Deja de dibujar cuando está invisible (evita un draw call de pantalla completa
    // inútil) y solo escribe el color si cambió.
    private void ApplyAlpha(float a)
    {
        if (Overlay == null) return;

        bool visible = a > 0.001f;
        if (Overlay.enabled != visible) Overlay.enabled = visible;
        if (!visible) return;

        Color c = Overlay.color;
        if (!Mathf.Approximately(c.a, a)) { c.a = a; Overlay.color = c; }
    }
}
