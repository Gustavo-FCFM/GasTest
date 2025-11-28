using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_LineAttack", menuName = "GAS/Generics/Line Attack")]
public class GA_LineAttack : GameplayAbility
{
    [Header("Configuración de Línea")]
    public float Length = 5f;  // Largo de la línea
    public float Width = 2f;   // Ancho de la línea
    public LayerMask TargetLayer;

    [Header("Efectos")]
    public GameplayEffect DamageEffect;

    public override void Activate()
    {
        if (!CanActivate()) return;

        // 1. Coste (Opcional, si es parte de un combo el coste suele ser 0)
        if (CostEffect != null) OwnerASC.ApplyGameplayEffect(CostEffect, this);

        // 2. Calcular la caja (BoxCast)
        Vector3 origin = OwnerASC.transform.position;
        Vector3 direction = OwnerASC.transform.forward;
        Vector3 center = origin + (direction * (Length / 2)); // El centro de la caja está a mitad de camino
        Vector3 halfExtents = new Vector3(Width / 2, 1, Length / 2); // Tamaño mitad

        // Visualización Debug (Solo en Scene view)
        // Debug.DrawRay(origin, direction * Length, Color.red, 1f);

        Collider[] hits = Physics.OverlapBox(center, halfExtents, OwnerASC.transform.rotation, TargetLayer);
        HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();

        foreach (var hit in hits)
        {
            AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC != null && targetASC != OwnerASC && !enemiesHit.Contains(targetASC))
            {
                // Aplicar Daño
                targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
                enemiesHit.Add(targetASC);
                ChargeUltimate();
                Debug.Log($"¡Golpe Lineal a {hit.name}!");
            }
        }

        EndAbility();
    }
}