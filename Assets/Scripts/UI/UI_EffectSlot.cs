using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_EffectSlot
//
// Un ícono de la barra de buffs/debuffs, con su borde de color
// (verde/rojo) y un reloj de progreso. Soporta dos modos: "local"
// (sigue en vivo un ActiveGameplayEffect real, para el host o un
// objetivo sin red) y "en red" (el progreso se lo pasa desde afuera
// UI_EffectContainer, calculado a partir de datos sincronizados).
// ============================================================
public class UI_EffectSlot : MonoBehaviour
{
    [Header("Referencias UI")]
    public Image IconImage;       // Ícono del efecto, en el centro
    public Image BorderImage;     // Marco de color (verde = buff, rojo = debuff)
    public Image CooldownOverlay; // Sombra que gira mostrando cuánto falta (Type: Filled)

    [Header("Configuración Colores")]
    public Color BuffColor = Color.green;
    public Color DebuffColor = Color.red;

    // Efecto en vivo que este slot sigue en modo local (null en modo red).
    private ActiveGameplayEffect liveEffect;

    // Configura ícono y color de borde a partir de la definición del
    // efecto. La usan ambos modos; lo único que cambia entre ellos es de
    // dónde sale el progreso de la barra (ver SetupLocal/SetProgress).
    public void Setup(GameplayEffect definition)
    {
        liveEffect = null;

        if (definition.Icon != null)
        {
            IconImage.sprite = definition.Icon;
            IconImage.enabled = true;
        }
        else
        {
            IconImage.enabled = false;
        }

        switch (definition.EffectType)
        {
            case GameplayEffect.EEffectType.Buff:
                BorderImage.color = BuffColor;
                break;
            case GameplayEffect.EEffectType.Debuff:
                BorderImage.color = DebuffColor;
                break;
        }

        if (CooldownOverlay != null) CooldownOverlay.fillAmount = 0;
    }

    // Modo local: guarda la referencia viva y se actualiza solo en
    // Update(). Lo usa UI_EffectContainer cuando el ASC objetivo es la
    // copia autoritativa (host) o no tiene red (ej. un Dummy).
    public void SetupLocal(ActiveGameplayEffect effect)
    {
        Setup(effect.Definition);
        liveEffect = effect;
    }

    // Modo en red: UI_EffectContainer llama esto cada frame con el
    // progreso ya calculado desde NetworkAbilitySystemComponent, porque
    // en un cliente remoto no existe ningún ActiveGameplayEffect real que
    // consultar.
    public void SetProgress(float remaining, float total)
    {
        if (CooldownOverlay != null && total > 0)
            CooldownOverlay.fillAmount = remaining / total;
    }

    // Solo hace algo en modo local: recalcula el progreso desde el
    // ActiveGameplayEffect real. En modo red, SetProgress ya lo hace
    // desde afuera y esta función no tiene nada que actualizar.
    void Update()
    {
        if (liveEffect == null) return;

        float progress = liveEffect.TotalDuration > 0
            ? liveEffect.DurationRemaining / liveEffect.TotalDuration
            : 0f;

        if (CooldownOverlay != null) CooldownOverlay.fillAmount = progress;
    }
}
