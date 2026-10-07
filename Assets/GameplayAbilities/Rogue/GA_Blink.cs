using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GA_Blink  (Golpe mortal del Pícaro)
//
// Habilidad de objetivo único: elige al enemigo que el jugador tiene en la
// mira (el más centrado dentro de un ángulo/rango), se teletransporta a su
// ESPALDA y le aplica la lista de efectos (el daño en base a un porcentaje de su
// vida faltante lo configura el GameplayEffect vía Modifier.UseTargetHealthScaling).
// Si el golpe MATA al objetivo, reinicia su propio cooldown.
//
// Como toda GameplayAbility del proyecto, Activate() corre en el servidor
// (autoridad de daño). El teletransporte del CharacterController, en cambio,
// tiene que ejecutarse en el proceso dueño — se delega en
// NetworkAbilitySystemComponent.ServerTeleportOwnerTo (mismo patrón que el salto).
// ============================================================
[CreateAssetMenu(fileName = "GA_Blink", menuName = "GAS/Specific Abilities/Rogue/Blink Strike")]
public class GA_Blink : GameplayAbility
{
    [Section(AbilitySection.Targeting)]
    [Tooltip("Alcance máximo para buscar al enemigo objetivo.")]
    public float MaxRange = 12f;
    [Tooltip("Ángulo máximo (grados) entre la mira y el enemigo para que cuente como objetivo.")]
    public float SelectionAngle = 25f;

    [Section(AbilitySection.Movement)]
    [Tooltip("A qué distancia por detrás del enemigo aparece el jugador.")]
    public float BehindDistance = 1.5f;

    // Valida, elige objetivo, teletransporta detrás de él, aplica los efectos y
    // (si lo mata) reinicia el cooldown.
    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        AbilitySystemComponent target = FindTarget();

        // Sin objetivo válido: no gastamos cooldown/costo. Liberamos el estado
        // "atacando" del dueño (EndAbility avisa al host y a los clientes) y salimos.
        if (target == null)
        {
            EndAbility();
            return;
        }

        CommitAbility();

        // Punto a la espalda del objetivo (según hacia dónde MIRA el objetivo),
        // manteniendo su altura para no aparecer flotando o clavado en el piso.
        Vector3 behind = target.transform.position - target.transform.forward * BehindDistance;
        behind.y = target.transform.position.y;
        Vector3 faceDir = target.transform.position - behind;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        // El teletransporte se ejecuta en el proceso dueño (CC client-authoritative).
        // Con la cámara girada hacia el objetivo: si no, el Pícaro aparece a su
        // espalda pero sigue mirando hacia donde miraba antes, y no le puede seguir
        // pegando.
        if (netAsc != null) netAsc.ServerTeleportOwnerTo(behind, faceDir, turnCamera: true);
        else if (pc != null) pc.TeleportTo(behind, faceDir, turnCamera: true); // fallback sin red

        if (pc != null) pc.PlayAnimation(this);

        // Daño (con autoridad de servidor). Guardamos si el objetivo ya estaba
        // muerto para saber si fue ESTA habilidad la que lo mató.
        bool wasDead = target.HasTag(EGameplayTag.State_Dead);
        if (!HasEffects(EEffectWhen.OnHit)) Debug.LogWarning($"[{AbilityName}] activado sin ningún efecto 'Al golpear'.");
        ApplyHitEffects(target, firstHit: true);
        ChargeUltimate();

        BroadcastHitVFX(target);

        // Si el golpe mató al objetivo, reiniciar el cooldown (dejar la habilidad
        // lista de nuevo). ApplyGameplayEffect es síncrono, así que el tag de
        // muerte ya está puesto si lo mató.
        bool killed = !wasDead && target.HasTag(EGameplayTag.State_Dead);
        if (killed && CooldownEffect != null && CooldownEffect.GrantedTags.Count > 0)
            OwnerASC.ReduceCooldownByTag(CooldownEffect.GrantedTags[0], 99999f);

        EndAbility();
    }

    // Elige al enemigo más alineado con la mira del jugador, dentro de MaxRange y
    // SelectionAngle. En el servidor, GetAimPoint() usa el NetworkAimPoint que el
    // dueño envió con el input (ver ServerActivateAbility).
    private AbilitySystemComponent FindTarget()
        => FindBestTargetInAim(MaxRange, SelectionAngle, ETargetAffiliation.Enemies);

    // Sin nadie a tiro, el dueño no anticipa nada: el servidor va a descartar la
    // activación igual (Activate() sale sin comprometer cooldown ni costo), y hacer el
    // gesto en el vacío se ve como si la habilidad se hubiera gastado.
    public override bool CanPredictActivation() => FindTarget() != null;

    // Vista previa del alcance de selección en el Editor.
    public override void DrawGizmos(Transform origin)
        => DrawSelectionGizmo(origin, MaxRange, SelectionAngle, new Color(0.6f, 0.1f, 0.8f, 0.9f));

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
        => AbilityHandles.Selection(this, origin, ref MaxRange, ref SelectionAngle);
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GameplayEffect DamageEffect;
    [SerializeField, HideInInspector] private List<GameplayEffect> AdditionalEffects;
    [SerializeField, HideInInspector] private GameObject ImpactVFX;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        UpgradeEffect(ref DamageEffect, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeEffects(AdditionalEffects, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeVisual(ref ImpactVFX, new AbilityVisual { When = EVisualWhen.OnHit, Offset = Vector3.up, DestroyTime = 2f },
                      ref changed);
    }
}
