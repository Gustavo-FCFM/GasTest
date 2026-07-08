using UnityEngine;
using System.Collections;
using FishNet.Object;

// ============================================================
// Entity_Totem
//
// Tótem invocado que le aplica un aura periódica a los aliados
// dentro de su radio (potenciada si el chamán que lo creó está
// enfurecido). A diferencia de las habilidades, este SÍ es un
// NetworkObject de verdad, así que su VFX de aura es puramente
// local (cada peer lo instancia en su propio Start()) y solo la
// aplicación del efecto en sí corre con autoridad de servidor.
// ============================================================
[RequireComponent(typeof(AbilitySystemComponent))]
public class Entity_Totem : NetworkBehaviour
{
    private AbilitySystemComponent ASC;

    [Header("Configuración del Tótem")]
    [Tooltip("El efecto (GameplayEffect) normal que este tótem aplicará a los aliados.")]
    public GameplayEffect AuraEffect;

    [Header("Sinergia: Ira del Tótem")]
    [Tooltip("El efecto potenciado que se aplica si el Chamán está Enfurecido.")]
    public GameplayEffect EmpoweredAuraEffect;

    [Tooltip("La etiqueta que el tótem buscará en el creador para saber si está Enfurecido (Ej. Status_Frenzy).")]
    public EGameplayTag RageTag;

    public float AuraRadius = 7f;
    // Cada cuánto se reaplica el aura a los aliados dentro del radio.
    public float TickRate = 0.5f;
    public LayerMask CharacterLayer;

    [Header("Visuales")]
    public GameObject AuraVFXPrefab;
    public float VfxScaleMultiplier = 2.0f;
    private GameObject currentAuraVFX;

    // Equipo del tótem (heredado de quien lo invocó) y referencia a su
    // creador, para chequear la sinergia de Ira. Los asigna quien lo
    // spawnea (GA_SpawnTotem/GA_ElementalFury) antes de Spawn().
    [HideInInspector] public int MyTeamID;
    [HideInInspector] public AbilitySystemComponent CreatorASC;

    // Cachea el ASC local.
    private void Awake()
    {
        ASC = GetComponent<AbilitySystemComponent>();
    }

    // Configura el equipo del ASC, reproduce el VFX de aura (local en
    // cada peer), y arranca el bucle de aplicación de efecto solo en el
    // servidor.
    private void Start()
    {
        if (ASC != null)
        {
            ASC.OnDeath += HandleTotemDestruction;
            ASC.TeamID = MyTeamID;
        }

        // Puramente visual: cada copia (servidor Y cada cliente, una vez que
        // el tótem es un NetworkObject spawneado de verdad) corre su propio
        // Start() localmente, así que esto ya se ve igual en todos lados sin
        // necesidad de RPC.
        if (AuraVFXPrefab != null)
        {
            currentAuraVFX = Instantiate(AuraVFXPrefab, transform.position, Quaternion.identity, transform);
            float finalScale = AuraRadius * VfxScaleMultiplier;
            currentAuraVFX.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
        }

        // La aplicación del aura (ApplyGameplayEffect) sí es autoridad del
        // servidor — si cada cliente también la corriera sobre su propia
        // copia local del tótem, aplicaría el efecto por duplicado.
        if (IsServerInitialized)
            StartCoroutine(AuraRoutine());
    }

    // Reaplica el aura a los aliados cada TickInterval segundos, mientras
    // el tótem exista.
    private IEnumerator AuraRoutine()
    {
        while (true)
        {
            ApplyAuraToAllies();
            yield return new WaitForSeconds(TickRate);
        }
    }

    // Busca aliados (mismo TeamID, no neutral, no muertos) dentro de
    // AuraRadius y les aplica AuraEffect — o EmpoweredAuraEffect si el
    // creador del tótem tiene el tag RageTag.
    private void ApplyAuraToAllies()
    {
        GameplayEffect effectToApply = AuraEffect;

        if (CreatorASC != null && CreatorASC.HasTag(RageTag))
        {
            if (EmpoweredAuraEffect != null)
            {
                effectToApply = EmpoweredAuraEffect;
            }
        }

        if (effectToApply == null) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, AuraRadius, CharacterLayer);

        foreach (Collider hit in hits)
        {
            AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();

            if (targetASC != null && !targetASC.HasTag(EGameplayTag.State_Dead))
            {
                if (targetASC.TeamID == MyTeamID && MyTeamID != 0)
                {
                    targetASC.ApplyGameplayEffect(effectToApply, ASC);
                }
            }
        }
    }

    // Al "morir" (vida en 0), despawnea el tótem para todos los peers.
    private void HandleTotemDestruction()
    {
        // La salud del tótem solo es autoritativa en el servidor (nada la
        // sincroniza a los clientes todavía), así que este evento solo
        // dispara de verdad ahí. Despawn (no Destroy) para que se borre
        // también en todos los clientes.
        if (!IsServerInitialized) return;

        Debug.Log("El tótem ha sido destruido.");
        if (IsSpawned) ServerManager.Despawn(gameObject);
    }

    // Vista previa del radio de aura en el Editor.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawSphere(transform.position, AuraRadius);
    }
}
