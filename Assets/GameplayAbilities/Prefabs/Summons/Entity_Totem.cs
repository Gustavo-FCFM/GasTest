using UnityEngine;
using System.Collections;
using FishNet.Object;

[RequireComponent(typeof(AbilitySystemComponent))]
public class Entity_Totem : NetworkBehaviour
{
    private AbilitySystemComponent ASC;

    [Header("Configuración del Tótem")]
    [Tooltip("El efecto (GameplayEffect) normal que este tótem aplicará a los aliados.")]
    public GameplayEffect AuraEffect;
    
    // --- NUEVAS VARIABLES PARA LA SINERGIA (IRA DEL TÓTEM) ---
    [Header("Sinergia: Ira del Tótem")]
    [Tooltip("El efecto potenciado que se aplica si el Chamán está Enfurecido.")]
    public GameplayEffect EmpoweredAuraEffect;
    
    [Tooltip("La etiqueta que el tótem buscará en el creador para saber si está Enfurecido (Ej. Status_Frenzy).")]
    public EGameplayTag RageTag; // Seleccione la etiqueta de su habilidad Enfurecer en el Inspector
    // ---------------------------------------------------------

    public float AuraRadius = 7f;
    public float TickRate = 0.5f;
    public LayerMask CharacterLayer;

    [Header("Visuales")]
    public GameObject AuraVFXPrefab;
    public float VfxScaleMultiplier = 2.0f;
    private GameObject currentAuraVFX;

    [HideInInspector] public int MyTeamID;
    [HideInInspector] public AbilitySystemComponent CreatorASC; // <--- Referencia al Chamán

    private void Awake()
    {
        ASC = GetComponent<AbilitySystemComponent>();
    }

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

    private IEnumerator AuraRoutine()
    {
        while (true)
        {
            ApplyAuraToAllies();
            yield return new WaitForSeconds(TickRate);
        }
    }

    private void ApplyAuraToAllies()
    {
        // 1. Determinar qué efecto usar en este "Tick"
        GameplayEffect effectToApply = AuraEffect;

        // Si tenemos un creador, y ese creador está enfurecido, cambiamos al efecto potenciado
        if (CreatorASC != null && CreatorASC.HasTag(RageTag))
        {
            if (EmpoweredAuraEffect != null)
            {
                effectToApply = EmpoweredAuraEffect;
            }
        }

        if (effectToApply == null) return;

        // 2. Aplicar el efecto seleccionado
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawSphere(transform.position, AuraRadius);
    }
}