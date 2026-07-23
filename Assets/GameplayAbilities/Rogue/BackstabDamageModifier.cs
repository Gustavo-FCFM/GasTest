using UnityEngine;

// ============================================================
// BackstabDamageModifier  (Ataque Furtivo — pasiva base del Pícaro)
//
// Marca el golpe como crítico cuando el atacante está POR LA ESPALDA del objetivo.
// Es un IDamageModifier: se registra en el ASC del jugador y ExecuteInstantEffect
// lo corre al repartir daño (ver DamageModifier / AbilitySystemComponent.ResolveOutgoingDamage).
//
// Antes esto vivía hardcodeado en el ASC detrás del tag Passive_Backstab. Ahora la
// simple PRESENCIA de este componente = la pasiva activa, así que no hace falta el
// tag: el modificador solo existe mientras la clase Pícaro está equipada.
//
// SETUP: va en el PassiveBehaviorsPrefab del Pícaro (y sus subclases lo heredan si
// usan su propio prefab con este componente). Como vive en un hijo del jugador,
// busca el ASC en el PADRE.
// ============================================================
public class BackstabDamageModifier : MonoBehaviour, IDamageModifier
{
    private AbilitySystemComponent _asc;

    private void Awake() => _asc = GetComponentInParent<AbilitySystemComponent>();

    private void OnEnable()  { if (_asc != null) _asc.RegisterDamageModifier(this); }
    private void OnDisable() { if (_asc != null) _asc.UnregisterDamageModifier(this); }

    // El backstab es posicional (no aplica a ticks de DoT). IsBackstab lo evalúa el
    // OBJETIVO usando hacia dónde mira él, no el atacante.
    public void ModifyOutgoingDamage(ref DamageContext ctx)
    {
        if (ctx.IsPeriodicTick || ctx.Target == null) return;
        if (ctx.Target.IsBackstab(ctx.Source)) ctx.IsCrit = true;
    }
}
