using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("Combate")]
    public float AttackRange = 2.0f; 
    public float AttackCooldown = 1.5f; 
    
    [Header("Ataque (Habilidad - Mago)")]
    public GameplayAbility AbilityToUse; // Si tiene esto, usará la habilidad

    [Header("Ataque Simple (Melee - Fantasma)")]
    public float Damage = 10f; 
    public GameplayEffect DamageEffect; // Opcional, para aplicar debuffs

    [Header("Visuales")]
    public GameObject HitVFX; // Efecto al golpear (Solo para ataque simple)

    private Transform playerTarget;
    private NavMeshAgent agent;
    private AbilitySystemComponent myASC;
    private AbilitySystemComponent playerASC;
    
    private float lastAttackTime;
    private bool isDead = false;
    
    // Instancia viva de la habilidad
    private GameplayAbility runtimeAbility; 

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        myASC = GetComponent<AbilitySystemComponent>();

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null)
        {
            playerTarget = pc.transform;
            playerASC = pc.GetComponent<AbilitySystemComponent>();
        }

        if (myASC != null) myASC.OnDeath += StopAI;
        
        // Inicializar habilidad si la tiene (Mago)
        if (myASC != null && AbilityToUse != null)
        {
            runtimeAbility = myASC.GrantAbility(AbilityToUse);
            // Ajustar rango si es mago (opcional, mejor configurarlo en el inspector)
            if (AttackRange < 5f) agent.stoppingDistance = AttackRange - 1.0f;
        }
        else
        {
            agent.stoppingDistance = AttackRange - 0.5f;
        }

        agent.speed = 3.5f; 
    }

    void OnDestroy()
    {
        if (myASC != null) myASC.OnDeath -= StopAI;
    }

    void Update()
    {
        if (isDead || playerTarget == null) return;
        
        // Sincronizar velocidad (Slows)
        if (myASC != null)
        {
            float currentSpeed = myASC.GetAttributeValue(EAttributeType.MovSpeed);
            if (myASC.HasTag(EGameplayTag.State_Stunned) || myASC.HasTag(EGameplayTag.State_Rooted))
                agent.speed = 0;
            else
                agent.speed = currentSpeed;
        }

        if (playerASC != null && playerASC.HasTag(EGameplayTag.State_Dead))
        {
            agent.isStopped = true;
            return;
        }

        // LÓGICA DE PERSECUCIÓN
        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        if (distanceToPlayer > AttackRange)
        {
            agent.isStopped = false;
            agent.SetDestination(playerTarget.position);
        }
        else
        {
            agent.isStopped = true; 
            FaceTarget(); 

            if (Time.time >= lastAttackTime + AttackCooldown)
            {
                PerformAttack(); // <-- NOMBRE CORRECTO
            }
        }
    }

    private void FaceTarget()
    {
        Vector3 direction = (playerTarget.position - transform.position).normalized;
        direction.y = 0; 
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }
    }

    private void PerformAttack()
    {
        lastAttackTime = Time.time;

        // OPCIÓN A: Usar Habilidad (Mago)
        // La habilidad se encarga de sus propios efectos visuales (proyectiles, etc.)
        if (runtimeAbility != null)
        {
            if (runtimeAbility.CanActivate())
            {
                runtimeAbility.Activate();
            }
        }
        // OPCIÓN B: Ataque Simple (Fantasma Melee)
        else if (playerASC != null)
        {
            // Aplicar Daño/Efecto
            if (DamageEffect != null)
            {
                playerASC.ApplyGameplayEffect(DamageEffect, myASC);
            }
            else
            {
                float currentHp = playerASC.GetAttributeValue(EAttributeType.Health);
                playerASC.SetCurrentAttributeValue(EAttributeType.Health, currentHp - Damage);
            }

            // --- VFX DE GOLPE (Solo melee) ---
            if (HitVFX != null)
            {
                // Instanciar en el jugador
                GameObject vfx = Instantiate(HitVFX, playerTarget.position + Vector3.up, Quaternion.identity);
                Destroy(vfx, 1.0f);
            }
        }
    }

    private void StopAI()
    {
        isDead = true;
        if(agent != null) agent.enabled = false; 
        this.enabled = false; 
    }
}