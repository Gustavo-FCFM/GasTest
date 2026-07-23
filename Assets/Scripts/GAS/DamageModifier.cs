// ============================================================
// IDamageModifier / DamageContext
//
// Pipeline de modificadores de daño SALIENTE. En vez de tener la lógica de cada
// clase (backstab del Pícaro, crítico mejorado del Asesino...) hardcodeada en
// AbilitySystemComponent.ExecuteInstantEffect, ahora cada pasiva registra un
// IDamageModifier en el ASC de su dueño, y el ASC recorre la lista cuando ese
// personaje reparte daño.
//
// Los modificadores se registran/desregistran desde los "comportamientos de
// clase" (los prefabs de PassiveBehaviorsPrefab), así que solo existen mientras
// la clase que los trae está equipada. El core queda genérico: no sabe qué es un
// backstab, solo corre modificadores y resuelve las banderas de crítico.
// ============================================================

// Contexto de UN golpe de daño, que los modificadores pueden leer y mutar. Es un
// struct pasado por ref: sin allocation por golpe y sin problemas de reentrada.
public struct DamageContext
{
    public AbilitySystemComponent Source;   // Atacante (dueño de los modificadores)
    public AbilitySystemComponent Target;   // Víctima
    public bool  IsPeriodicTick;            // True si es un tick de DoT (los modificadores suelen ignorarlo)
    public float Magnitude;                 // Magnitud actual (negativa = daño). Mutable por los modificadores.

    // Banderas de crítico. El ASC las resuelve al final multiplicando por CritDamage:
    //   IsCrit          → "es crítico" (varias fuentes = OR, no se apilan entre sí).
    //   IsImprovedCrit  → capa APARTE que se acumula con IsCrit (otro x CritDamage).
    public bool IsCrit;
    public bool IsImprovedCrit;
}

public interface IDamageModifier
{
    // Ajusta el contexto de un golpe saliente del dueño de este modificador.
    void ModifyOutgoingDamage(ref DamageContext ctx);
}
