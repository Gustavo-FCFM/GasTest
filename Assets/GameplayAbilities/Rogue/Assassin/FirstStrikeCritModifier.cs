using UnityEngine;
using System.Collections.Generic;

// ============================================================
// FirstStrikeCritModifier  (Crítico mejorado — pasiva del Asesino)
//
// Suma una capa de crítico APARTE ("crítico mejorado") al PRIMER golpe contra un
// enemigo tras un lapso sin pegarle, con una reutilización global entre golpes
// mejorados. Es un IDamageModifier: se registra en el ASC del jugador y
// ExecuteInstantEffect lo corre al repartir daño (ver AbilitySystemComponent.ResolveOutgoingDamage).
//
// El ESTADO (última vez que golpeó a cada enemigo, último crítico mejorado) vive
// acá, no en el ASC. El HUD y el nameplate leen la disponibilidad a través del ASC
// (IsFirstStrikeReady / IsFirstStrikeReadyAgainst), que delega en este componente.
//
// La presencia de este componente = la pasiva activa; no hace falta ningún tag.
//
// SETUP: va en el PassiveBehaviorsPrefab del Asesino. Como vive en un hijo del
// jugador, busca el ASC en el PADRE.
// ============================================================
public class FirstStrikeCritModifier : MonoBehaviour, IDamageModifier
{
    [Tooltip("Ventana por objetivo: un enemigo vuelve a estar 'fresco' para un crítico mejorado " +
             "si no lo golpeás en estos segundos.")]
    public float FirstStrikeWindow = 6f;
    [Tooltip("Reutilización global: mínimo de segundos entre dos críticos mejorados (contra cualquiera).")]
    public float FirstStrikeCooldown = 2f;

    private AbilitySystemComponent _asc;

    // Última vez que ESTE personaje golpeó a cada enemigo (para la ventana por objetivo).
    private readonly Dictionary<AbilitySystemComponent, float> _lastStrikeTime
        = new Dictionary<AbilitySystemComponent, float>();
    // Última vez que disparó un crítico mejorado (para la reutilización global).
    private float _lastCrit = -999f;

    private void Awake() => _asc = GetComponentInParent<AbilitySystemComponent>();

    private void OnEnable()  { if (_asc != null) _asc.RegisterDamageModifier(this); }
    private void OnDisable() { if (_asc != null) _asc.UnregisterDamageModifier(this); }

    // Marca "crítico mejorado" en el primer golpe fresco (y consume el cooldown).
    // No aplica a ticks de DoT.
    public void ModifyOutgoingDamage(ref DamageContext ctx)
    {
        if (ctx.IsPeriodicTick || ctx.Target == null) return;
        if (Consume(ctx.Target)) ctx.IsImprovedCrit = true;
    }

    // Marca el golpe contra 'target' y devuelve true si corresponde crítico
    // mejorado: primer golpe dentro de la ventana Y pasado el cooldown global.
    private bool Consume(AbilitySystemComponent target)
    {
        float now = Time.time;
        bool  isFresh = !_lastStrikeTime.TryGetValue(target, out float last) ||
                        (now - last) >= FirstStrikeWindow;

        _lastStrikeTime[target] = now;

        if (!isFresh) return false;
        if (now - _lastCrit < FirstStrikeCooldown) return false;

        _lastCrit = now;
        return true;
    }

    // ¿Está disponible el crítico mejorado (pasó la reutilización global)? Lo usa el
    // feedback del HUD a través del ASC.
    public bool IsReady => Time.time - _lastCrit >= FirstStrikeCooldown;

    // ¿Le puedo clavar un crítico mejorado a ESTE enemigo ahora? Reutilización
    // global + "frescura" del objetivo (que no lo hayas golpeado en FirstStrikeWindow).
    //
    // NOTA DE RED: _lastStrikeTime solo se llena en el SERVIDOR (donde se aplica el
    // daño). En el host es exacto; en un cliente remoto la "frescura" no se conoce,
    // así que ahí cae en la disponibilidad global (mismo que IsReady). Suficiente
    // para el aviso visual del nameplate.
    public bool IsReadyAgainst(AbilitySystemComponent target)
    {
        if (target == null || !IsReady) return false;
        return !_lastStrikeTime.TryGetValue(target, out float last) ||
               (Time.time - last >= FirstStrikeWindow);
    }
}
