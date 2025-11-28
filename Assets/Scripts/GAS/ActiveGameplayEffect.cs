using UnityEngine;

public class ActiveGameplayEffect
{
    public GameplayEffect Definition; 
    public float DurationRemaining;
    public float TotalDuration; 
    public float PeriodRemaining;
    public bool IsExpired => DurationRemaining <= 0;

    public ActiveGameplayEffect(GameplayEffect definition, float durationOverride = -1f)
    {
        Definition = definition;

        // Lógica de Override corregida
        if (durationOverride > 0)
        {
            DurationRemaining = durationOverride;
            TotalDuration = durationOverride; // Guardamos el override
        }
        else
        {
            DurationRemaining = definition.Duration;
            TotalDuration = definition.Duration; // Guardamos el del asset
        }

        PeriodRemaining = definition.Period;
    }
}