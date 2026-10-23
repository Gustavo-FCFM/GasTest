using UnityEngine;

// ============================================================
// GA_Flight  (genérico — vuelo libre)
//
// Como el vuelo de Iron Man (Marvel Rivals) o el de Spellbreak: al activarla da un
// impulso (hacia arriba, o hacia donde mira: "Despegue") y, mientras dure Status_Flying,
// el dueño vuela libre: sin gravedad,
// WASD lo mueve en 3D hacia donde mira la cámara (más rápido si solo va hacia adelante),
// Espacio sube y Ctrl baja. No aterriza aunque toque el piso. Quieto en el aire va
// cayendo despacio (IdleSinkSpeed): para mantenerse arriba hay que moverse.
// Al terminar en el aire cae lento, y aturdido en pleno vuelo también (no se queda colgado).
// Recibir daño de otro o quedar enraizado TERMINA el vuelo (lo hace el ASC: quita los
// efectos que dan Status_Flying).
//
// Un efecto "Al activarse → El lanzador" TIENE que otorgar Status_Flying, y su duración es la del vuelo. El movimiento
// lo hace el cliente dueño (PlayerController → VUELO LIBRE): el transform es suyo.
// Un bot no vuela (MovesThroughOwner): la usa solo como definitiva, sin el vuelo (GroundForBot).
// ============================================================
[CreateAssetMenu(fileName = "GA_Flight", menuName = "GAS/Generics/Flight")]
public class GA_Flight : GA_SelfBuff
{
    public enum ELaunchDirection
    {
        [InspectorName("Hacia arriba")]     Up,
        [InspectorName("Hacia donde mira")] TowardAim,
    }

    [Section("Vuelo")]
    [Tooltip("Hacia dónde sale el impulso del despegue.\n\n" +
             "· Hacia arriba: sube y desde ahí vuela.\n" +
             "· Hacia donde mira: sale disparado hacia la mira (el Mago, 21 de octubre de 2026). " +
             "Desde el piso, siempre un poco hacia arriba (ver el ángulo mínimo); en el aire, exacto " +
             "a la mira: mirando abajo, en picada.")]
    [Label("Despegue")]
    public ELaunchDirection LaunchDirection = ELaunchDirection.Up;

    [ShowIf(nameof(LaunchDirection), ELaunchDirection.TowardAim)]
    [Tooltip("Desde el piso, el impulso sale por lo menos con este ángulo hacia arriba aunque mire " +
             "al frente o al piso: así despega en vez de arrastrarse o chocar contra el suelo.")]
    [Label("Desde el piso: ángulo mínimo hacia arriba (°)")]
    public float MinLaunchUpAngle = 20f;

    [Tooltip("Velocidad del impulso del despegue (hacia arriba o hacia la mira). Se disipa sola en " +
             "menos de un segundo: recorre unos velocidad ÷ 3 metros.")]
    [Label("Velocidad de despegue (m/s)")]
    public float LaunchSpeed = 12f;

    [Tooltip("Velocidad de vuelo = velocidad de movimiento × esto.")]
    [Label("Velocidad (×)")]
    public float SpeedMultiplier = 1.5f;

    [Tooltip("Extra si SOLO va hacia adelante (sin moverse de lado): × esto encima de lo anterior.")]
    [Label("Extra hacia adelante (×)")]
    public float ForwardBoost = 1.5f;

    [Tooltip("Metros por segundo al subir (Espacio) o bajar (Ctrl).")]
    [Label("Subir/bajar (m/s)")]
    public float VerticalSpeed = 6f;

    [Tooltip("Quieto en el aire (sin WASD ni subir/bajar) va cayendo despacio, acelerando en 1 s " +
             "hasta esta velocidad (m/s). Obliga a moverse para no bajar. 0 = flota quieto para siempre.")]
    [Label("Caída quieto (m/s)")]
    public float IdleSinkSpeed = 1.5f;

    public override bool MovesThroughOwner => true;

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        if (!AnyEffectGrants(EGameplayTag.Status_Flying, EEffectWhen.OnActivate, EEffectTarget.Self))
            Debug.LogWarning($"[{AbilityName}] Ningún efecto 'Al activarse → El lanzador' otorga Status_Flying: no va a volar.");

        base.Activate();

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null && pc.IsBot)
        {
            if (CommittedThisActivation) GroundForBot();
            return;
        }

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) netAsc.ServerStartFlight(LaunchSpeed, SpeedMultiplier, ForwardBoost, VerticalSpeed, IdleSinkSpeed,
                                                     LaunchDirection == ELaunchDirection.TowardAim, MinLaunchUpAngle);
    }

    // UN BOT NO VUELA (23 de octubre de 2026): no tiene dueño que mueva su cuerpo (ServerStartFlight no le
    // llega) y camina por NavMesh. Lo que la habilidad da ADEMÁS del vuelo se lo queda (la Sobrecarga de
    // hechizos de la Evocación: la resistencia, el maná y las Q al instante): cada efecto que daba el
    // vuelo se cambia por una copia sin Status_Flying ni Status_SteadyFlight, si le queda algo. Por eso
    // un bot la puede usar como definitiva (BotController); el vuelo del Shift sigue sin usarla.
    [System.NonSerialized] private System.Collections.Generic.Dictionary<GameplayEffect, GameplayEffect> _grounded;

    private void GroundForBot()
    {
        if (Effects == null) return;

        foreach (AbilityEffect e in Effects)
        {
            if (e.Effect == null || e.When != EEffectWhen.OnActivate || e.ApplyTo != EEffectTarget.Self) continue;
            if (e.Effect.GrantedTags == null || !e.Effect.GrantedTags.Contains(EGameplayTag.Status_Flying)) continue;

            OwnerASC.RemoveEffectsByDefinition(e.Effect);

            _grounded ??= new System.Collections.Generic.Dictionary<GameplayEffect, GameplayEffect>();
            if (!_grounded.TryGetValue(e.Effect, out GameplayEffect grounded))
            {
                grounded = e.Effect.CreateCopyWithoutTags(EGameplayTag.Status_Flying, EGameplayTag.Status_SteadyFlight);
                _grounded[e.Effect] = grounded;
            }
            if (grounded != null) OwnerASC.ApplyGameplayEffect(grounded, OwnerASC);
        }
    }
}
