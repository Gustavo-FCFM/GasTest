using System.Collections.Generic;

// ============================================================
// AbilityEcho / IAbilityEchoSource  (ecos — la Proyección arcana de la Abjuración, 22 de octubre de 2026)
//
// "Lo que tira el dueño sale ADEMÁS desde otro personaje." Un IAbilityEchoSource se registra en el
// ASC del dueño (como un IDamageModifier, desde el PassiveBehaviorsPrefab de la clase) y, cada vez
// que una habilidad suya DISPARA (el proyectil sale, el rayo se resuelve, la grieta arranca), dice
// desde quiénes sale además y con qué parte del daño. La habilidad tira una copia desde cada uno,
// hacia donde apunta el dueño (GameplayAbility.FireEchoes).
//
// La copia es otra instancia de la misma habilidad (GameplayAbility.IsEcho): mismo dueño (los
// golpes son suyos: la baja, los puntos, las pasivas), sin costo ni cooldown, con el daño escalado
// (DamageScale) y sin cargar la definitiva. Por ahora lo saben hacer GA_ProjectileShoot,
// GA_HitscanShot (y la Descarga del Mago) y GA_GroundFissure.
// ============================================================
public struct AbilityEcho
{
    public AbilitySystemComponent From;   // desde quién sale la copia
    public float DamageScale;             // parte del daño que hace (0.5 = la mitad)
}

public interface IAbilityEchoSource
{
    // 'ability' acaba de disparar: agregar a 'echoes' desde quiénes sale además. Servidor.
    void CollectEchoes(GameplayAbility ability, List<AbilityEcho> echoes);
}
