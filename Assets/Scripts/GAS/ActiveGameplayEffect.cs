// ============================================================
// ActiveGameplayEffect
//
// Representa UNA instancia en curso de un GameplayEffect con
// duración (un buff, un debuff, un cooldown...) aplicada a un
// AbilitySystemComponent. El GameplayEffect en sí es solo la
// "receta" (ScriptableObject compartido); esta clase es el
// cronómetro runtime de una aplicación concreta de esa receta.
// ============================================================
public class ActiveGameplayEffect
{
    // El GameplayEffect (receta) del que salió esta instancia. Varias
    // ActiveGameplayEffect pueden compartir la misma Definition si el
    // efecto permite stackearse.
    public GameplayEffect Definition;

    // Segundos que faltan para que el efecto termine. Baja cada frame en
    // AbilitySystemComponent.ProcessActiveEffects(); al llegar a 0 el
    // efecto se remueve solo.
    public float DurationRemaining;

    // Duración total con la que arrancó este efecto (el override que le
    // haya pasado ApplyGameplayEffect, o si no el Duration del asset). La
    // UI la usa como denominador para dibujar la barra de progreso.
    public float TotalDuration;

    // Segundos que faltan para el próximo "tick" de un efecto periódico
    // (ej: veneno que daña cada 2s). Solo aplica si Definition.Period > 0.
    public float PeriodRemaining;

    // True cuando ya no queda tiempo — AbilitySystemComponent lo usa para
    // saber cuáles efectos limpiar en su barrido de cada frame.
    public bool IsExpired => DurationRemaining <= 0;

    // Arranca el cronómetro de una nueva aplicación del efecto.
    // durationOverride > 0 pisa la duración del asset (lo usan los
    // cooldowns dinámicos, ej. basados en velocidad de ataque).
    public ActiveGameplayEffect(GameplayEffect definition, float durationOverride = -1f)
    {
        Definition = definition;

        if (durationOverride > 0)
        {
            DurationRemaining = durationOverride;
            TotalDuration = durationOverride;
        }
        else
        {
            DurationRemaining = definition.Duration;
            TotalDuration = definition.Duration;
        }

        PeriodRemaining = definition.Period;
    }
}
