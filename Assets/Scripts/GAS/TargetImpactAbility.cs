using UnityEngine;

// ============================================================
// TargetImpactAbility
//
// Base de las habilidades que dibujan un VFX de impacto sobre un PERSONAJE: la
// curación apuntada, marcar a un enemigo, revivir a un aliado, la Intercepción heroica.
//
// El VFX ahora es una entrada "Al golpear" de la lista de VFX de la habilidad (con
// "Lo sigue" para pegárselo al personaje, el Offset desde sus pies y cuánto dura). Los
// tres campos que tenía esta clase (ImpactVFX, AttachImpactVFX, ImpactVFXOffset,
// ImpactVFXLifetime) se pasan solos a esa entrada al cargar el asset.
//
// Cómo se usa desde una habilidad: PlayImpactVFXOnTarget(objetivo) en el servidor.
// Manda el OBJETIVO por red (NetworkASC.ServerPlayAbilityVFXOn), no una posición, así
// cada pantalla le pega el VFX a él y lo sigue si se mueve.
// ============================================================
public abstract class TargetImpactAbility : GameplayAbility
{
    // Reproduce los VFX "al golpear" sobre el objetivo en todas las pantallas. Solo servidor.
    protected void PlayImpactVFXOnTarget(AbilitySystemComponent target) => BroadcastHitVFX(target);

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GameObject ImpactVFX;
    [SerializeField, HideInInspector] private bool       AttachImpactVFX = true;
    [SerializeField, HideInInspector] private Vector3    ImpactVFXOffset = new Vector3(0f, 1f, 0f);
    [SerializeField, HideInInspector] private float      ImpactVFXLifetime = 2f;

    // Dónde salía el VFX viejo: sobre el personaje (casi todas) o en un punto (la
    // Intercepción, donde aterriza el Paladín).
    protected virtual EVisualWhen LegacyImpactVFXWhen => EVisualWhen.OnHit;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        bool onTarget = LegacyImpactVFXWhen == EVisualWhen.OnHit;
        UpgradeVisual(ref ImpactVFX, new AbilityVisual
        {
            When        = LegacyImpactVFXWhen,
            Attach      = onTarget && AttachImpactVFX,
            // En un punto se pedía a la altura del pecho, sin mirar el offset del personaje.
            Offset      = onTarget ? ImpactVFXOffset : Vector3.up,
            DestroyTime = ImpactVFXLifetime,
        }, ref changed);
    }
}
