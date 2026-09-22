using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

// ============================================================
// HealthPack
//
// Botiquín: una cruz flotante en el piso que cura al que le pasa por encima y
// desaparece un rato. Los de Overwatch / Marvel Rivals. Existe para que un tanque o un
// DPS puedan recuperarse sin volver caminando a la base — y para dar puntos del mapa
// por los que vale la pena pelear.
//
// SIRVE A CUALQUIER MODO, por eso vive en GameMode/ y no en Mercenaries/: no sabe nada
// de equipos, bases ni objetivo. Se pone donde se quiera y funciona.
//
// CÓMO VIAJA POR LA RED: un solo SyncVar con el instante (tick) en que vuelve a estar
// listo. De ahí sale TODO lo demás en cada cliente sin más tráfico: si ya pasó, está
// activo (cruz visible); si falta, está en recarga (cuenta regresiva en el disco). El
// servidor es el único que decide curar.
//
// Lo instala `Mercenarios ▸ Instalar botiquines` — o a mano: un GameObject con este
// componente donde se quiera uno.
// ============================================================
[RequireComponent(typeof(SphereCollider))]
public class HealthPack : NetworkBehaviour
{
    [Header("Curación")]
    [Tooltip("Vida que devuelve. 75 es un tercio de un tanque y casi toda la de un pícaro: " +
             "suficiente para salvar una pelea, no tanto como para no volver nunca a la base.")]
    public float HealAmount = 75f;

    [Tooltip("Segundos hasta que vuelve a aparecer.")]
    public float RespawnSeconds = 20f;

    [Tooltip("Radio en el que se recoge. Se aplica al SphereCollider del objeto.")]
    public float PickupRadius = 1.6f;

    [Tooltip("No lo levanta quien está a full: así no se desperdicia al pasar caminando.")]
    public bool OnlyIfHurt = true;

    [Header("Visual")]
    [Tooltip("La cruz que flota. Si se deja vacío se arma una por código (dos barras verdes).")]
    public GameObject CrossVisual;

    [Tooltip("Altura a la que flota la cruz sobre el piso.")]
    public float CrossHeight = 1.1f;

    [Tooltip("Color de la cruz y del disco.")]
    public Color Tint = new Color(0.25f, 0.95f, 0.45f);

    [Tooltip("VFX al recogerlo (opcional). Se instancia en el jugador y se destruye solo.")]
    public GameObject PickupVFX;
    public float PickupVFXLifetime = 2f;

    [Header("Sonido")]
    public SfxCue PickupSound;

    // Tick del servidor en el que vuelve a estar listo. 0 = listo desde siempre.
    // Es TODO lo que viaja: cada cliente compara con su tick actual y sabe si mostrar
    // la cruz o la cuenta regresiva.
    private readonly SyncVar<uint> _netReadyTick = new SyncVar<uint>(0);

    private SphereCollider _trigger;
    private HealthPackVisual _visual;

    // =========================================================
    // CICLO
    // =========================================================

    private void Awake()
    {
        _trigger = GetComponent<SphereCollider>();
        _trigger.isTrigger = true;
        _trigger.radius    = PickupRadius;

        _visual = GetComponentInChildren<HealthPackVisual>();
        if (_visual == null)
        {
            GameObject go = new GameObject("Visual");
            go.transform.SetParent(transform, false);
            _visual = go.AddComponent<HealthPackVisual>();
        }
        _visual.Build(this);
    }

    private void Update()
    {
        _visual.Refresh(IsReady, RechargeNormalized);
    }

    // ¿Se puede levantar ahora? Lo calculan todos los peers igual, con el tick.
    public bool IsReady
    {
        get
        {
            if (TimeManager == null) return _netReadyTick.Value == 0;
            return TimeManager.Tick >= _netReadyTick.Value;
        }
    }

    // 0 = recién consumido, 1 = a punto de volver. Es lo que llena el disco.
    public float RechargeNormalized
    {
        get
        {
            if (IsReady || TimeManager == null) return 1f;

            float total = RespawnSeconds;
            if (total <= 0f) return 1f;

            float remaining = (float)TimeManager.TicksToTime(_netReadyTick.Value - TimeManager.Tick);
            return Mathf.Clamp01(1f - remaining / total);
        }
    }

    // =========================================================
    // RECOGER (servidor)
    // =========================================================

    private void OnTriggerEnter(Collider other) => TryHeal(other);

    // También al QUEDARSE dentro: si te pasa por encima mientras está en recarga, o
    // entrás con la vida llena y te pegan ahí mismo, lo levantás igual sin salir y
    // volver a entrar.
    private void OnTriggerStay(Collider other) => TryHeal(other);

    private void TryHeal(Collider other)
    {
        if (!IsServerInitialized || !IsReady || other == null) return;

        AbilitySystemComponent asc = other.GetComponentInParent<AbilitySystemComponent>();
        if (asc == null) return;

        // Solo personajes vivos que puedan curarse. Un NPC no lo levanta: son del
        // equipo 4 y esto es para los jugadores (y sus bots).
        if (asc.GetComponent<PlayerController>() == null) return;
        if (asc.HasTag(EGameplayTag.State_Dead)) return;

        float health = asc.GetAttributeValue(EAttributeType.Health);
        float max    = asc.GetAttributeValue(EAttributeType.MaxHealth);
        if (OnlyIfHurt && health >= max - 0.01f) return;

        asc.SetCurrentAttributeValue(EAttributeType.Health, Mathf.Min(health + HealAmount, max));

        uint delay = TimeManager != null ? TimeManager.TimeToTicks(RespawnSeconds) : 0;
        _netReadyTick.Value = (TimeManager != null ? TimeManager.Tick : 0) + delay;

        ObserversPlayPickup(asc.transform.position);
    }

    // El feedback de recoger, en todos los peers. Va aparte del SyncVar porque es un
    // evento puntual: el SyncVar solo dice "está en recarga", no "acaba de pasar".
    [ObserversRpc]
    private void ObserversPlayPickup(Vector3 position)
    {
        // Si este botiquín no trae su propio sonido, usa el de la biblioteca: así se
        // cargan los veinte de una sola vez.
        SfxCue cue = PickupSound != null && !PickupSound.IsEmpty ? PickupSound
                   : (AudioLibrary.Instance != null ? AudioLibrary.Instance.HealthPack : null);
        AudioManager.Play(cue, position);

        if (PickupVFX == null) return;
        GameObject vfx = Instantiate(PickupVFX, position, Quaternion.identity);
        Destroy(vfx, PickupVFXLifetime);
    }

    private void OnValidate()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        if (col != null) { col.isTrigger = true; col.radius = PickupRadius; }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(Tint.r, Tint.g, Tint.b, 0.35f);
        Gizmos.DrawSphere(transform.position, PickupRadius);
    }
}
