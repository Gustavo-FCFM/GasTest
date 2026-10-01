using UnityEngine;

// ============================================================
// GA_Resurrection  (Resurrección — definitiva del Clérigo, Dominio de la vida)
//
// "Selecciona el cuerpo de un aliado muerto y lo devuelve a la vida con la mitad de
// sus puntos de vida."
//
// POR QUÉ NO ES UN GA_Target CON AllowDeadTargets: un muerto con ragdoll apaga su
// cápsula y pasa los huesos a otra capa (para que los ataques no le peguen al
// cadáver), así que el OverlapSphere por la capa de personajes no lo encuentra. Acá se
// recorren los jugadores y se apunta al CUERPO: la cadera del ragdoll si está suelto,
// o el transform si murió con la animación.
//
// El muerto vuelve DONDE CAYÓ (su transform, no donde rodó el ragdoll: al revivir el
// modelo vuelve a su raíz), con HealthFraction de su vida máxima y sus cooldowns como
// estaban. Su reaparición en la base se cancela (NetworkASC.ServerResurrect). La
// ventana es la de la reaparición: pasados esos segundos ya no hay cuerpo.
//
// Sin ningún aliado muerto en la mira no se lanza y no gasta la definitiva.
// ============================================================
[CreateAssetMenu(fileName = "GA_Resurrection", menuName = "GAS/Cleric/Resurrection")]
public class GA_Resurrection : TargetImpactAbility
{
    [Header("Selección")]
    [Tooltip("Distancia máxima desde el Clérigo hasta el cuerpo.")]
    public float MaxRange = 8f;

    [Tooltip("Ángulo máximo (grados) entre la mira y el cuerpo. Un cuerpo en el piso queda " +
             "debajo de la retícula, así que conviene más generoso que el de GA_Target.")]
    public float SelectionAngle = 35f;

    [Header("Resurrección")]
    [Tooltip("Con cuánta vida vuelve, como fracción de su vida máxima. 0.5 = la mitad.")]
    [Range(0.05f, 1f)]
    public float HealthFraction = 0.5f;

    // =========================================================
    // ACTIVACIÓN
    // =========================================================

    public override bool CanActivate() => base.CanActivate() && FindDeadAlly() != null;

    // Lista para usarse (sin cooldown, sin aturdir ni silenciar), SIN pedir un cuerpo en
    // la mira. La usa el contorno de los aliados muertos (DeadAllyHighlighter) para
    // pintarlos de verde o de rojo. Funciona en el dueño: los tags viajan sincronizados.
    public bool IsReady => base.CanActivate();

    // Sin cuerpo en la mira el dueño no anticipa la animación: el servidor la va a
    // descartar igual.
    public override bool CanPredictActivation() => FindDeadAlly() != null;

    public override void Activate()
    {
        if (!IsServer) return;

        AbilitySystemComponent ally = CanActivate() ? FindDeadAlly() : null;
        if (ally == null)
        {
            EndAbility();
            return;
        }

        CommitAbility();

        NetworkAbilitySystemComponent allyNet = ally.GetComponent<NetworkAbilitySystemComponent>();
        if (allyNet != null) allyNet.ServerResurrect(HealthFraction);
        else                 ally.Revive(resetAbilities: false, healthFraction: HealthFraction); // sin red

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.RotateToAim();
            pc.PlayAnimation(this);
        }

        PlayImpactVFXOnTarget(ally);

        EndAbility();
    }

    // =========================================================
    // BUSCAR EL CUERPO
    // =========================================================

    // El aliado muerto más centrado en la mira, dentro del alcance. Mismo criterio que
    // FindBestTargetInAim (el rayo de la retícula, no la línea desde los pies), pero
    // recorriendo los jugadores en vez de la física.
    private AbilitySystemComponent FindDeadAlly()
    {
        if (OwnerASC == null) return null;

        PlayerController ownerPc = OwnerASC.GetComponent<PlayerController>();
        Vector3 origin    = OwnerASC.transform.position;
        Vector3 aimOrigin = ownerPc != null ? ownerPc.GetAimOrigin() : origin + Vector3.up * 1.6f;
        Vector3 aimPoint  = ownerPc != null ? ownerPc.GetAimPoint(200f)
                                            : origin + OwnerASC.transform.forward * MaxRange;

        Vector3 aimDir = aimPoint - aimOrigin;
        if (aimDir.sqrMagnitude < 0.0001f) aimDir = OwnerASC.transform.forward;
        aimDir.Normalize();

        AbilitySystemComponent best = null;
        float bestAlign = Mathf.Cos(SelectionAngle * Mathf.Deg2Rad);

        // Se busca solo al usarla (al apretar la R): recorrer los jugadores de la escena
        // no es un costo por frame.
        foreach (PlayerController pc in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            AbilitySystemComponent asc = pc.GetComponent<AbilitySystemComponent>();
            if (asc == null || ReferenceEquals(asc, OwnerASC)) continue;
            if (!asc.HasTag(EGameplayTag.State_Dead) || !IsAlly(asc, includeSelf: false)) continue;

            Vector3 body = BodyPosition(pc);
            if ((body - origin).sqrMagnitude > MaxRange * MaxRange) continue;

            Vector3 toBody = body - aimOrigin;
            if (toBody.sqrMagnitude < 0.0001f) continue;

            float align = Vector3.Dot(aimDir, toBody.normalized);
            if (align > bestAlign) { bestAlign = align; best = asc; }
        }
        return best;
    }

    // Dónde está el cuerpo que se ve: la cadera del ragdoll si está suelto, o un poco
    // arriba de los pies si murió con la animación (queda tirado en el piso).
    public static Vector3 BodyPosition(PlayerController pc)
    {
        RagdollController ragdoll = pc.GetComponent<RagdollController>();
        if (ragdoll != null && ragdoll.IsActive && ragdoll.Hips != null) return ragdoll.Hips.position;
        return pc.transform.position + Vector3.up * 0.3f;
    }

    // =========================================================
    // VISUALES Y GIZMOS
    // =========================================================

    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Gizmos.color = new Color(1f, 0.9f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(origin.position, MaxRange);
    }
}
