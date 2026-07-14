using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GA_LeapAttack
//
// Habilidad de salto con impacto en área al aterrizar (slam). El
// movimiento del salto en sí lo ejecuta el CLIENTE DUEÑO (es el
// único que mueve su propio CharacterController); esta clase solo
// arranca ese movimiento a través de NetworkAbilitySystemComponent
// y resuelve el daño/VFX del aterrizaje, siempre con autoridad de
// servidor. Ver comentarios en NetworkAbilitySystemComponent
// (sección SALTO) para el flujo completo.
// ============================================================
[CreateAssetMenu(fileName = "GA_LeapAttack", menuName = "GAS/Generics/Leap Attack")]
public class GA_LeapAttack : GameplayAbility
{
    [Header("Configuración del Salto")]
    // Impulso vertical inicial del salto.
    public float JumpVelocity = 15f;
    // Impulso hacia adelante (dirección de la cámara del dueño) del salto.
    public float ForwardForce = 5f;

    [Header("Área de Impacto")]
    // Radio del área de daño al aterrizar (slam).
    public float AbilityRadius = 3f;

    [Header("Efectos")]
    // Daño que recibe cada enemigo dentro de AbilityRadius al aterrizar.
    public GameplayEffect DamageEffect;
    // Control de masas (aturdir, enraizar, etc.) que además se le aplica
    // a cada enemigo golpeado.
    public GameplayEffect CrowdControlEffect;
    // Efectos EXTRA que se aplican a cada enemigo golpeado al aterrizar. Opcional.
    public List<GameplayEffect> AdditionalEffects;

    [Header("Visuales")]
    public GameObject ImpactVFX;
    [Tooltip("Multiplica AbilityRadius para el tamaño del VFX. No afecta el daño real — solo lo visual.")]
    public float ImpactVFXScaleMultiplier = 2f;

    // Valida, cobra costo/cooldown, reproduce la animación y le pide a
    // NetworkAbilitySystemComponent que ejecute el salto en el cliente
    // dueño (ver ServerStartLeap).
    public override void Activate()
    {
        if (!IsServer) return;   // ← NUEVO
        if (!CanActivate()) return;

        CommitAbility();

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        if (pc != null)
            pc.PlayAnimation(AnimationTriggerName, AnimationID);

        // El salto en sí (mover el CharacterController) tiene que resolverse
        // en el proceso DUEÑO del jugador, no acá — Activate() corre en el
        // servidor, que para el host es el mismo proceso que el dueño, pero
        // para cualquier otro jugador es un proceso distinto. ServerStartLeap
        // manda un TargetRpc a la conexión dueña para que ejecute el salto
        // en su propia copia.
        if (netAsc != null)
            netAsc.ServerStartLeap(this, JumpVelocity, ForwardForce);
        else
            Debug.LogWarning("[GA_LeapAttack] No hay NetworkAbilitySystemComponent — el salto no se va a ejecutar en ningún lado.");

        Debug.Log($"Salto Furioso Iniciado: Fuerza Vertical: {JumpVelocity}");

        EndAbility();
    }

    // Resuelve el impacto del aterrizaje: reproduce el VFX en todos los
    // peers y aplica daño/CC a los enemigos dentro de AbilityRadius. Lo
    // llama NetworkAbilitySystemComponent.ServerResolveLeapImpact() —
    // siempre en el servidor — cuando el dueño (el único que simula su
    // propio CharacterController) le avisa que aterrizó.
    public void ExecuteImpactCheck()
    {
        Vector3 impactCenter = OwnerASC.transform.position;

        // El VFX de impacto es puramente cosmético, pero Instantiate() acá
        // (que corre en el servidor) solo lo crea en ESTE proceso — igual
        // que pasaba con el arma del proyectil, un cliente remoto nunca lo
        // ve. ServerPlayAbilityVFX lo reproduce localmente en el servidor Y
        // le avisa a los demás peers que hagan lo mismo con su propia copia.
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, impactCenter);
        else PlayImpactVFX(impactCenter); // fallback sin red (singleplayer/NPC)

        Collider[] hitColliders = Physics.OverlapSphere(impactCenter, AbilityRadius, TargetLayer);
        HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();

        foreach (var hitCollider in hitColliders)
        {
            AbilitySystemComponent targetASC = hitCollider.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC == null || ReferenceEquals(targetASC, OwnerASC)) continue; // ignora el propio collider del que saltó

            if (IsEnemy(targetASC) && !enemiesHit.Contains(targetASC))
            {
                if (DamageEffect != null)        targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
                ChargeUltimate();
                if (CrowdControlEffect != null)  targetASC.ApplyGameplayEffect(CrowdControlEffect, OwnerASC);
                ApplyEffectsTo(AdditionalEffects, targetASC);
                enemiesHit.Add(targetASC);
            }
        }
    }

    // Instancia ImpactVFX escalado según AbilityRadius. Llamado
    // localmente por cada peer (servidor y, vía ObserversRpc, todos los
    // clientes) para reproducir el VFX con su propia copia del prefab —
    // ver NetworkAbilitySystemComponent.ServerPlayAbilityVFX().
    public override void PlayImpactVFX(Vector3 position)
    {
        if (ImpactVFX == null) return;

        GameObject vfx = Instantiate(ImpactVFX, position, Quaternion.identity);
        float vfxScale  = AbilityRadius * ImpactVFXScaleMultiplier;
        vfx.transform.localScale = new Vector3(vfxScale, vfxScale, vfxScale);
        Destroy(vfx, 2.0f);
    }

    // El impacto real se resuelve alrededor de OwnerASC.transform.position al
    // ATERRIZAR (ver ExecuteImpactCheck arriba) — no sabemos de antemano dónde
    // va a caer, así que dibujamos la esfera centrada en la posición ACTUAL
    // del jugador en el Editor, con el mismo AbilityRadius que usa de verdad.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.25f);
        Gizmos.DrawSphere(origin.position, AbilityRadius);
        Gizmos.color = new Color(1f, 0.25f, 0.1f, 1f);
        Gizmos.DrawWireSphere(origin.position, AbilityRadius);
    }
}
