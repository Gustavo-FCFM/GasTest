using UnityEngine;
using UnityEngine.UI;

public class UI_EffectSlot : MonoBehaviour
{
    [Header("Referencias UI")]
    public Image IconImage;       // El icono del centro
    public Image BorderImage;     // El marco de color (Verde/Rojo)
    public Image CooldownOverlay; // La sombra que gira (Filled Type)

    [Header("Configuración Colores")]
    public Color BuffColor = Color.green;
    public Color DebuffColor = Color.red;

    private ActiveGameplayEffect activeEffect;

    public void Setup(ActiveGameplayEffect effect)
    {
        activeEffect = effect;

        // 1. Configurar Icono
        if (effect.Definition.Icon != null)
        {
            IconImage.sprite = effect.Definition.Icon;
            IconImage.enabled = true;
        }
        else
        {
            IconImage.enabled = false;
        }

        // 2. Configurar Color según tipo
        switch (effect.Definition.EffectType)
        {
            case GameplayEffect.EEffectType.Buff:
                BorderImage.color = BuffColor;
                break;
            case GameplayEffect.EEffectType.Debuff:
                BorderImage.color = DebuffColor;
                break;
        }
    }

    void Update()
    {
        if (activeEffect == null) return;

        // 3. Actualizar el "Reloj" (Fill Amount)
        // Calculamos porcentaje: Tiempo Restante / Duración Total
        float progress = activeEffect.DurationRemaining / activeEffect.Definition.Duration;
        
        if (CooldownOverlay != null)
        {
            CooldownOverlay.fillAmount = progress;
        }
    }
}