using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GA_HitscanShot
//
// Disparo HITSCAN ("escaneo instantáneo"), el modelo clásico de las armas de fuego
// en un FPS: no se crea ninguna bala que viaje: se traza un rayo desde el arma hacia
// donde apunta la retícula y el impacto se resuelve en el MISMO frame. Si algo está
// en la línea, le pega ya (a diferencia de GC_Projectile, que sí viaja y se puede
// esquivar). Para el jugador se siente instantáneo y preciso.
//
// El rayo se corta en la primera PARED (WallLayer) y le pega al primer enemigo que
// haya antes de eso. BulletRadius > 0 usa un SphereCast en vez de una línea fina:
// una bala "gorda" perdona un poco la puntería (lo que en shooters se llama ayuda de
// apuntado); 0 sería una línea perfecta.
//
// RETROCESO (opcional): además del disparo, empuja al tirador en la dirección
// CONTRARIA al tiro, con el mismo sistema de impulso del dash. Como usa la dirección
// 3D completa de la mira, disparar hacia ABAJO te impulsa hacia ARRIBA (el clásico
// "rocket jump"), y disparar al frente te tira hacia atrás. Con RecoilSpeed = 0 no
// hay retroceso y queda un disparo normal.
//
// RED: la resolución del tiro es server-authoritative (Activate es server-only) y usa
// el punto de mira que el dueño ya sincroniza (GetAimPoint → NetworkAimPoint). El
// empujón lo ejecuta el proceso DUEÑO vía ServerStartDash, porque el
// CharacterController es client-authoritative.
// ============================================================
[CreateAssetMenu(fileName = "GA_HitscanShot", menuName = "GAS/Generics/Hitscan Shot")]
public class GA_HitscanShot : GameplayAbility
{
    [Header("Disparo")]
    [Tooltip("Alcance máximo del disparo.")]
    public float MaxRange = 50f;

    [Tooltip("Grosor de la bala. 0 = línea fina (puntería exacta); > 0 perdona un poco la puntería.")]
    public float BulletRadius = 0.15f;

    [Tooltip("Altura del cañón sobre el pivote del personaje (desde dónde sale el rayo).")]
    public float MuzzleHeight = 1.4f;

    [Tooltip("Capas que FRENAN el disparo (paredes/entorno).")]
    public LayerMask WallLayer;

    [Header("Efectos al impactar")]
    [Tooltip("Daño al enemigo alcanzado.")]
    public GameplayEffect DamageEffect;

    [Tooltip("Efectos EXTRA que se le aplican al enemigo alcanzado, además del daño. Opcional.")]
    public List<GameplayEffect> AdditionalEffects;

    [Tooltip("VFX del impacto (chispa/sangre). Se reproduce en todos los peers.")]
    public GameObject HitVFX;

    [Header("Retroceso (impulso contrario al tiro)")]
    [Tooltip("Velocidad del empujón hacia atrás. 0 = sin retroceso.")]
    public float RecoilSpeed = 0f;

    [Tooltip("Cuánto dura el empujón (después cae por gravedad).")]
    public float RecoilDuration = 0.25f;

    [Tooltip("Capa de JUGADORES a atravesar durante el empujón (igual que en el dash).")]
    public LayerMask ExcludePlayerLayer;

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC == null) return;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        // Origen del rayo: a la altura del arma, no a los pies.
        Vector3 origin = OwnerASC.transform.position + Vector3.up * MuzzleHeight;

        // Dirección 3D hacia la retícula (incluye el ángulo vertical: es lo que
        // permite el impulso hacia arriba al disparar al piso).
        Vector3 aimPoint = pc != null ? pc.GetAimPoint(MaxRange)
                                      : origin + OwnerASC.transform.forward * MaxRange;
        Vector3 dir = aimPoint - origin;
        if (dir.sqrMagnitude < 0.0001f) dir = OwnerASC.transform.forward;
        dir.Normalize();

        ResolveShot(origin, dir, pc, netAsc);

        if (pc != null) pc.PlayAnimation(this);

        // Retroceso: impulso en la dirección OPUESTA al disparo. faceVelocity=false
        // para que el cuerpo siga mirando a donde disparó mientras sale despedido.
        bool recoiled = false;
        if (RecoilSpeed > 0f)
        {
            Vector3 recoilVelocity = -dir * RecoilSpeed;
            if (netAsc != null)
            {
                netAsc.ServerStartDash(recoilVelocity, RecoilDuration, ExcludePlayerLayer.value, false);
                recoiled = true;
            }
            else if (pc != null)
            {
                pc.ApplyDashVelocity(recoilVelocity, false); // fallback sin red
            }
        }

        // Con retroceso en red NO llamamos EndAbility: el fin del impulso (restaurar
        // colisión y liberar "atacando") lo maneja el dueño en DashRoutine, igual que
        // en GA_Dash. Sin retroceso, cerramos acá.
        if (!recoiled) EndAbility();
    }

    // Traza el rayo: lo corta en la primera pared y le aplica los efectos al primer
    // enemigo que haya antes de ese punto.
    private void ResolveShot(Vector3 origin, Vector3 dir, PlayerController pc,
                             NetworkAbilitySystemComponent netAsc)
    {
        // 1) ¿Hasta dónde llega? La pared más cercana recorta el alcance.
        float distance = MaxRange;
        Vector3 impactPoint = origin + dir * MaxRange;

        if (Physics.SphereCast(origin, Mathf.Max(0.01f, BulletRadius), dir, out RaycastHit wallHit,
                               MaxRange, WallLayer, QueryTriggerInteraction.Ignore))
        {
            distance    = wallHit.distance;
            impactPoint = wallHit.point;
        }

        // 2) Primer enemigo dentro de ese tramo. SphereCastAll no viene ordenado, así
        // que buscamos el de menor distancia a mano.
        RaycastHit[] hits = Physics.SphereCastAll(origin, Mathf.Max(0.01f, BulletRadius), dir,
                                                  distance, TargetLayer, QueryTriggerInteraction.Collide);

        AbilitySystemComponent victim = null;
        float bestDist = float.MaxValue;

        foreach (RaycastHit h in hits)
        {
            AbilitySystemComponent asc = h.collider.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || ReferenceEquals(asc, OwnerASC) || !IsEnemy(asc)) continue;
            if (asc.HasTag(EGameplayTag.State_Dead)) continue;

            // distance 0 = el collider ya solapaba el origen; lo tomamos como el más cercano.
            float d = h.distance <= 0f ? 0f : h.distance;
            if (d < bestDist)
            {
                bestDist    = d;
                victim      = asc;
                impactPoint = h.distance <= 0f ? asc.transform.position + Vector3.up : h.point;
            }
        }

        if (victim != null)
        {
            if (DamageEffect != null) victim.ApplyGameplayEffect(DamageEffect, OwnerASC);
            ApplyEffectsTo(AdditionalEffects, victim);
            ChargeUltimate();
        }

        // VFX del impacto (en la pared o en el enemigo) para todos los peers.
        if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, impactPoint);
        else PlayImpactVFX(impactPoint);
    }

    public override void PlayImpactVFX(Vector3 position)
    {
        if (HitVFX == null) return;
        GameObject vfx = Instantiate(HitVFX, position, Quaternion.identity);
        Destroy(vfx, 1.5f);
    }

    // Vista previa del disparo en el Editor.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Vector3 p0 = origin.position + Vector3.up * MuzzleHeight;
        Vector3 p1 = p0 + origin.forward * MaxRange;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawLine(p0, p1);
        if (BulletRadius > 0f) Gizmos.DrawWireSphere(p1, BulletRadius);
    }
}
