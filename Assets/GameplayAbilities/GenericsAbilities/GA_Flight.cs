using UnityEngine;

// ============================================================
// GA_Flight  (genérico — vuelo libre)
//
// Como el vuelo de Iron Man (Marvel Rivals) o el de Spellbreak: al activarla da un
// impulso hacia arriba y, mientras dure Status_Flying, el dueño vuela libre: sin gravedad,
// WASD lo mueve en 3D hacia donde mira la cámara (más rápido si solo va hacia adelante),
// Espacio sube y Ctrl baja. No aterriza aunque toque el piso. Quieto en el aire va
// cayendo despacio (IdleSinkSpeed): para mantenerse arriba hay que moverse.
// Al terminar en el aire cae lento, y aturdido en pleno vuelo también (no se queda colgado).
// Recibir daño de otro o quedar enraizado TERMINA el vuelo (lo hace el ASC: quita los
// efectos que dan Status_Flying).
//
// Un efecto "Al activarse → El lanzador" TIENE que otorgar Status_Flying, y su duración es la del vuelo. El movimiento
// lo hace el cliente dueño (PlayerController → VUELO LIBRE): el transform es suyo.
// Los bots no la usan (MovesThroughOwner).
// ============================================================
[CreateAssetMenu(fileName = "GA_Flight", menuName = "GAS/Generics/Flight")]
public class GA_Flight : GA_SelfBuff
{
    [Section("Vuelo")]
    [Tooltip("Velocidad hacia arriba del despegue. Se disipa sola en menos de un segundo.")]
    public float LaunchSpeed = 12f;

    [Tooltip("Velocidad de vuelo = velocidad de movimiento × esto.")]
    public float SpeedMultiplier = 1.5f;

    [Tooltip("Extra si SOLO va hacia adelante (sin moverse de lado): × esto encima de lo anterior.")]
    public float ForwardBoost = 1.5f;

    [Tooltip("Metros por segundo al subir (Espacio) o bajar (Ctrl).")]
    public float VerticalSpeed = 6f;

    [Tooltip("Quieto en el aire (sin WASD ni subir/bajar) va cayendo despacio, acelerando en 1 s " +
             "hasta esta velocidad (m/s). Obliga a moverse para no bajar. 0 = flota quieto para siempre.")]
    public float IdleSinkSpeed = 1.5f;

    public override bool MovesThroughOwner => true;

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        if (!AnyEffectGrants(EGameplayTag.Status_Flying, EEffectWhen.OnActivate, EEffectTarget.Self))
            Debug.LogWarning($"[{AbilityName}] Ningún efecto 'Al activarse → El lanzador' otorga Status_Flying: no va a volar.");

        base.Activate();

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) netAsc.ServerStartFlight(LaunchSpeed, SpeedMultiplier, ForwardBoost, VerticalSpeed, IdleSinkSpeed);
    }
}
