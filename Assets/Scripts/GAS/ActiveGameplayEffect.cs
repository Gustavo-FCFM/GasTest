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

    // Quién aplicó el efecto (normalmente el AbilitySystemComponent del atacante).
    // Se guarda para que los ticks de un efecto periódico (ej: una DoT) sepan de
    // quién vienen: sin esto no había robo de vida, ni escalado con los stats del
    // atacante, ni crédito de la muerte si la DoT remataba.
    public object Source;

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

    // Cuánto ESCUDO otorgó este efecto al aplicarse. Shield es un "pool" (tiene
    // su propio valor actual que el daño va consumiendo), así que no puede pasar
    // por el sistema de modificadores como los demás stats: se suma al aplicar y
    // se resta al expirar. Guardamos el monto exacto acá para retirar justo eso
    // (nunca por debajo de 0, por si el daño ya se lo comió).
    // Ver AbilitySystemComponent.GrantTemporaryShield/RemoveActiveEffect.
    public float GrantedShield;

    // True cuando ya no queda tiempo — AbilitySystemComponent lo usa para
    // saber cuáles efectos limpiar en su barrido de cada frame.
    public bool IsExpired => DurationRemaining <= 0;

    // Arranca el cronómetro de una nueva aplicación del efecto.
    // durationOverride > 0 pisa la duración del asset (lo usan los
    // cooldowns dinámicos, ej. basados en velocidad de ataque).
    public ActiveGameplayEffect(GameplayEffect definition, float durationOverride = -1f, object source = null)
    {
        Definition = definition;
        Source     = source;

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
