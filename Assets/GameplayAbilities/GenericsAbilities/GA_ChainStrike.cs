using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_ChainStrike  (genérico — golpe en cadena: la Muerte silenciosa del Shinobi)
//
// Elige al enemigo de la mira (como el Golpe mortal del Pícaro), aparece a su espalda y le
// aplica la lista de efectos. Se queda ahí StayTime, intocable (WhileActiveEffect), y
// busca al SIGUIENTE: el enemigo más cerca del último golpeado, a JumpRange o menos,
// primero los JUGADORES y entre ellos el que más vida le falta. Salta a él y repite, hasta
// que no quede ninguno sin golpear (a cada uno, una sola vez).
//
// El daño ("30 % de su vida faltante + el daño normal") lo pone el GE de la lista: un
// Modifier que escala con el ataque y con la vida faltante del objetivo.
//
// RED: cada salto es un teletransporte del DUEÑO (ServerTeleportOwnerTo, el transform es
// suyo) y la animación de cada golpe va a todas las pantallas. Los bots no la usan.
// ============================================================
[CreateAssetMenu(fileName = "GA_ChainStrike", menuName = "GAS/Generics/Chain Strike")]
public class GA_ChainStrike : GameplayAbility
{
    [Section(AbilitySection.Targeting)]
    [Tooltip("Alcance para elegir al PRIMER enemigo, con la mira.")]
    public float MaxRange = 15f;

    [Tooltip("Ángulo máximo (grados) entre la mira y el primer enemigo.")]
    public float SelectionAngle = 25f;

    [Section("Cadena")]
    [Tooltip("A qué distancia del último golpeado busca al siguiente.")]
    public float JumpRange = 10f;

    [Tooltip("Segundos que se queda sobre cada enemigo antes de saltar al siguiente.")]
    public float StayTime = 0.5f;

    [Tooltip("Primero los JUGADORES (el que más vida le falta); los monstruos y NPCs, solo si no queda ninguno.")]
    public bool PrioritizePlayers = true;

    [Tooltip("Tope de enemigos por cadena. 0 = sin tope (hasta que no quede ninguno sin golpear).")]
    public int MaxTargets = 0;

    [Tooltip("Lo que lleva mientras dura la cadena, y se le saca al terminar: intocable (un GE con " +
             "Status_Immunity y Status_Unstoppable, de duración larga).")]
    public GameplayEffect WhileActiveEffect;

    [Section(AbilitySection.Movement)]
    [Tooltip("A qué distancia detrás de cada enemigo aparece.")]
    public float BehindDistance = 1.2f;

    public override bool MovesThroughOwner => true;

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        AbilitySystemComponent first = FindBestTargetInAim(MaxRange, SelectionAngle, ETargetAffiliation.Enemies);
        if (first == null)
        {
            // Sin nadie a tiro no se cobra nada.
            EndAbility();
            return;
        }

        CommitAbility();

        if (WhileActiveEffect != null) OwnerASC.ApplyGameplayEffect(WhileActiveEffect, OwnerASC);
        OwnerASC.StartAbilityCoroutine(ChainRoutine(first));
    }

    public override bool CanPredictActivation()
        => FindBestTargetInAim(MaxRange, SelectionAngle, ETargetAffiliation.Enemies) != null;

    private IEnumerator ChainRoutine(AbilitySystemComponent current)
    {
        var hit = new HashSet<AbilitySystemComponent>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        PlayerController pc = OwnerASC.GetComponent<PlayerController>();

        while (current != null)
        {
            if (OwnerASC == null || OwnerASC.HasTag(EGameplayTag.State_Dead)) break;

            // A su espalda, a su altura, mirándolo (con la cámara: si no, sigue mirando a donde miraba).
            Vector3 behind = current.transform.position - current.transform.forward * BehindDistance;
            behind.y = current.transform.position.y;
            Vector3 face = current.transform.position - behind;
            if (netAsc != null)  netAsc.ServerTeleportOwnerTo(behind, face, turnCamera: true);
            else if (pc != null) pc.TeleportTo(behind, face, turnCamera: true);

            if (netAsc != null) netAsc.ServerPlayAbilityAnimationOnAll(this);

            ApplyHitEffects(current, firstHit: hit.Count == 0);
            BroadcastHitVFX(current);
            hit.Add(current);

            if (MaxTargets > 0 && hit.Count >= MaxTargets) break;

            yield return new WaitForSeconds(StayTime);

            current = FindNext(current.transform.position, hit);
        }

        if (OwnerASC != null && WhileActiveEffect != null) OwnerASC.RemoveEffectsByDefinition(WhileActiveEffect);
        EndAbility();
    }

    // El siguiente: un enemigo vivo y visible a JumpRange del último, que no haya golpeado.
    // Primero los jugadores (si PrioritizePlayers) y, a igualdad, el que más vida le falta.
    private AbilitySystemComponent FindNext(Vector3 from, HashSet<AbilitySystemComponent> hit)
    {
        AbilitySystemComponent best = null;
        bool  bestIsPlayer = false;
        float bestMissing  = -1f;

        foreach (Collider c in Physics.OverlapSphere(from, JumpRange, TargetLayer))
        {
            AbilitySystemComponent asc = c.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || ReferenceEquals(asc, OwnerASC) || hit.Contains(asc) || !IsEnemy(asc)) continue;
            if (asc.HasTag(EGameplayTag.State_Dead) || asc.IsHiddenFromEnemies) continue;

            bool  isPlayer = PrioritizePlayers && asc.GetComponent<PlayerController>() != null;
            float missing  = asc.GetAttributeValue(EAttributeType.MaxHealth) - asc.GetAttributeValue(EAttributeType.Health);

            bool better = best == null
                       || (isPlayer && !bestIsPlayer)
                       || (isPlayer == bestIsPlayer && missing > bestMissing);
            if (!better) continue;

            best = asc; bestIsPlayer = isPlayer; bestMissing = missing;
        }
        return best;
    }

    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.8f);
        Gizmos.DrawLine(origin.position + Vector3.up, origin.position + Vector3.up + origin.forward * MaxRange);
        Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.35f);
        Gizmos.DrawWireSphere(origin.position + origin.forward * MaxRange, JumpRange);
    }

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
    {
        if (origin == null) return;
        AbilityHandles.Selection(this, origin, ref MaxRange, ref SelectionAngle);
        AbilityHandles.Radius(this, "Jump Range", origin.position + origin.forward * MaxRange, ref JumpRange,
                              AbilityHandles.RadiusColor, origin.right);
    }
#endif
}
