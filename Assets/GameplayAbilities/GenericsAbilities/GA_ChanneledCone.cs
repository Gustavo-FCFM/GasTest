using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_ChanneledCone
//
// Un cono SOSTENIDO: mientras dura (ChannelDuration), cada TickInterval le aplica la lista
// de efectos a lo que esté dentro del cono, que sigue a la mira en vivo. Es un aliento: el
// del Dragón rojo del Maestro elemental (8 de octubre de 2026, pedido de Gustavo), 3 s y un
// golpe por segundo.
//
// En cada tick puede además dejar una ZONA donde apunta la mira (TickZone): un
// GA_ContinuousAoE que se reusa tal cual —su radio, su duración, sus ticks, sus efectos y
// su VFX—, sin su costo, su cooldown ni su animación (ver GA_ContinuousAoE.DeployZoneAt).
// La zona cae al piso bajo el punto de mira, a no más de ZoneMaxRange del lanzador.
//
// El jugador queda LIBRE: se mueve, el cuerpo sigue a la cámara y el cono con él. Lo que
// no puede es usar otras habilidades mientras dura (Status_Channeling, como el molinete;
// las marcadas "Usable While Channeling" sí). Lo corta morir o recibir cualquiera de los
// tags de su ActivationBlockedTags (aturdido, silenciado...).
//
// LA MIRA DURANTE EL CANALIZADO: el servidor solo conoce la del instante de activar. Le
// pide al dueño que se la mande mientras dura (PlayerController.ServerStreamAimFor); en el
// host, GetAimPoint la lee en vivo.
// ============================================================
[CreateAssetMenu(fileName = "GA_ChanneledCone", menuName = "GAS/Generics/Channeled Cone")]
public class GA_ChanneledCone : GameplayAbility, IChanneledAbility
{
    [Section(AbilitySection.Shape)]
    [Tooltip("Alcance del cono, en metros, desde el punto de salida.")]
    public float Range = 6f;

    [Range(0f, 360f)]
    [Tooltip("Apertura del cono, en grados (el ángulo completo). 90 = un cuarto de círculo.")]
    public float ConeAngle = 60f;

    [Tooltip("El cono se inclina hacia donde apunta la mira (arriba o abajo). Apagado = siempre " +
             "horizontal: solo importa el ángulo visto desde el cielo.")]
    public bool UseVerticalAim = true;

    [Tooltip("Desde dónde sale el cono, en espacio local del dueño (X = derecha, Y = arriba, " +
             "Z = adelante). Un aliento sale de la boca: (0, 1.5, 0).")]
    public Vector3 OriginOffset = new Vector3(0f, 1.5f, 0f);

    [Section(AbilitySection.Timing)]
    [Tooltip("Cuánto dura el canalizado, en segundos.")]
    public float ChannelDuration = 3f;

    [Tooltip("Cada cuántos segundos golpea el cono (y suelta su zona). El primero sale al empezar: " +
             "3 s cada 1 s = 3 golpes.")]
    public float TickInterval = 1f;

    [Tooltip("Mientras dura, no puede usar otras habilidades (salvo las que tengan 'Usable While " +
             "Channeling' en su asset).")]
    public bool BlockOtherAbilities = true;

    [Section("Zona por tick")]
    [Tooltip("OPCIONAL: zona que deja en cada tick donde apunta la mira. Se usa esa habilidad tal " +
             "cual (radio, duración, ticks, efectos y VFX) pero sin su costo, cooldown ni animación.")]
    public GA_ContinuousAoE TickZone;

    [ShowIf(nameof(TickZone))]
    [Tooltip("Hasta qué distancia del lanzador puede caer la zona. Mirando más lejos, cae a esta " +
             "distancia en esa dirección.")]
    public float ZoneMaxRange = 6f;

    [Section(AbilitySection.Animation)]
    [Tooltip("Clip en BUCLE mientras dura. Reusa las ranuras del mantenido en el Animator (las del " +
             "escudo), así que no hace falta crear estados. Vacío = el clip suelto de la habilidad.")]
    public AnimationClip ChannelLoopAnimation;

    [Tooltip("OPCIONAL: arranque, una sola vez, antes de entrar al bucle.")]
    public AnimationClip ChannelStartAnimation;

    [Tooltip("OPCIONAL: remate al terminar.")]
    public AnimationClip ChannelEndAnimation;

    // IChanneledAbility: la capa de red lee los clips por acá para replicarlos.
    public AnimationClip ChannelStartClip => ChannelStartAnimation;
    public AnimationClip ChannelLoopClip  => ChannelLoopAnimation;
    public AnimationClip ChannelEndClip   => ChannelEndAnimation;
    public float         SpinSpeed        => 0f;

    // Con clip de bucle, la animación de las demás pantallas la manda el canalizado: el clip
    // suelto que el servidor manda después de Activate() pisaría la pose (ver GA_Whirlwind).
    public override bool BroadcastsOwnAnimation => ChannelLoopAnimation != null;

    // Un VFX "al lanzar" con Destroy Time en 0 dura lo que el canalizado (el aliento en sí).
    protected override float VisualDefaultLifetime => ChannelDuration;

    // Ni personajes ni paredes invisibles: el piso donde cae la zona.
    private const int GroundMask = ~((1 << 7) | (1 << 2));

    // La copia de la zona para este dueño: cada tick la vuelve a desplegar.
    [System.NonSerialized] private GA_ContinuousAoE       _zone;
    [System.NonSerialized] private AbilitySystemComponent _zoneOwner;

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();
        if (OwnerASC == null) return;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        if (ChannelLoopAnimation != null && netAsc != null) netAsc.ServerPlayChannelAnimation(this, true);
        else if (pc != null)                                pc.PlayAnimation(this);

        if (pc != null && netAsc != null) pc.ServerStreamAimFor(ChannelDuration + 0.5f);

        ShowCastBar(ChannelDuration, channel: true);
        if (BlockOtherAbilities) OwnerASC.AddTag(EGameplayTag.Status_Channeling);

        // Libre YA, como el molinete: con el ataque "en curso" el cuerpo dejaría de seguir a
        // la cámara (PlayerController no lo gira mientras ataca) y el cono quedaría clavado
        // hacia donde miraba al empezar.
        EndAbility();

        OwnerASC.StartAbilityCoroutine(ChannelRoutine(pc, netAsc));
    }

    // Los ticks, y al final (o al cortarse) apaga todo lo que prendió Activate. El finally
    // corre aunque la corutina se interrumpa (muerte, respawn, cambio de clase): sin él, el
    // tag de canalizado quedaría puesto y no podría volver a usar ninguna habilidad.
    private IEnumerator ChannelRoutine(PlayerController pc, NetworkAbilitySystemComponent netAsc)
    {
        bool interrupted = false;
        try
        {
            float tick  = Mathf.Max(0.05f, TickInterval);
            int   ticks = Mathf.Max(1, Mathf.RoundToInt(ChannelDuration / tick));

            for (int i = 0; i < ticks; i++)
            {
                if (IsInterrupted()) { interrupted = true; yield break; }

                Vector3 aimPoint = CurrentAimPoint(pc);
                HitCone(aimPoint);
                if (TickZone != null) DropZone(aimPoint);

                yield return new WaitForSeconds(tick);
            }
        }
        finally
        {
            HideCastBar(interrupted);
            if (ChannelLoopAnimation != null && netAsc != null) netAsc.ServerPlayChannelAnimation(this, false);
            if (BlockOtherAbilities && OwnerASC != null) OwnerASC.RemoveTag(EGameplayTag.Status_Channeling);
        }
    }

    // Lo que impide lanzarla también la corta a mitad.
    private bool IsInterrupted()
    {
        if (OwnerASC == null || OwnerASC.HasTag(EGameplayTag.State_Dead)) return true;
        if (ActivationBlockedTags != null)
            foreach (EGameplayTag tag in ActivationBlockedTags)
                if (OwnerASC.HasTag(tag)) return true;
        return false;
    }

    // La mira de ahora: en el host, su cámara en vivo; en un cliente, la que manda mientras
    // dura el canalizado. Vector3.zero = nunca llegó ninguna.
    private Vector3 CurrentAimPoint(PlayerController pc)
        => pc != null ? pc.GetAimPoint() : Vector3.zero;

    // El frente del cuerpo (que sigue a la cámara), inclinado hacia la mira si corresponde.
    private Vector3 ConeDirection(Vector3 origin, Vector3 aimPoint)
    {
        Vector3 forward = OwnerASC.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return Vector3.forward;
        forward.Normalize();

        if (!UseVerticalAim || aimPoint == Vector3.zero) return forward;

        Vector3 toAim      = aimPoint - origin;
        float   horizontal = new Vector2(toAim.x, toAim.z).magnitude;
        if (horizontal < 0.01f) return forward;

        float pitch = Mathf.Atan2(toAim.y, horizontal);
        return forward * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
    }

    // Un golpe del cono. El ángulo se mide hacia el CENTRO del collider de cada uno, no a
    // sus pies: el cono sale de la boca, y con los pies un enemigo pegado quedaría "abajo".
    private void HitCone(Vector3 aimPoint)
    {
        Vector3 origin    = OwnerASC.transform.TransformPoint(OriginOffset);
        Vector3 direction = ConeDirection(origin, aimPoint);

        Collider[] found = Physics.OverlapSphere(origin, Range, TargetLayer);

        // Del más cercano al más lejano: "el primer golpe" cae en el que está adelante.
        System.Array.Sort(found, (a, b) =>
            (a.bounds.center - origin).sqrMagnitude.CompareTo((b.bounds.center - origin).sqrMagnitude));

        var hit = new HashSet<AbilitySystemComponent>();
        foreach (Collider col in found)
        {
            AbilitySystemComponent target = col.GetComponentInParent<AbilitySystemComponent>();
            if (target == null || target == OwnerASC || hit.Contains(target)) continue;

            Vector3 toTarget = col.bounds.center - origin;
            if (!UseVerticalAim) toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f && Vector3.Angle(direction, toTarget) >= ConeAngle / 2f) continue;

            hit.Add(target);
            if (!ApplyHitEffects(target, firstHit: hit.Count == 1)) continue;

            if (IsEnemy(target)) ChargeUltimate();
            BroadcastHitVFX(target);
        }
    }

    // Suelta la zona en el piso bajo la mira, recortada a ZoneMaxRange. Si debajo no hay
    // piso (mira al vacío), ese tick no deja zona.
    private void DropZone(Vector3 aimPoint)
    {
        Vector3 from  = OwnerASC.transform.position;
        Vector3 point = aimPoint != Vector3.zero ? aimPoint : from + OwnerASC.transform.forward * ZoneMaxRange;

        Vector3 flat = point - from;
        flat.y = 0f;
        if (flat.magnitude > ZoneMaxRange)
        {
            Vector3 clamped = from + flat.normalized * ZoneMaxRange;
            point = new Vector3(clamped.x, point.y, clamped.z);
        }

        // Mirando al cielo, el punto queda muy alto: se baja desde una altura razonable para
        // no chocar con un techo o una plataforma de arriba.
        Vector3 start = new Vector3(point.x, Mathf.Min(point.y, from.y + 4f) + 0.5f, point.z);
        if (!Physics.Raycast(start, Vector3.down, out RaycastHit ground, 50f, GroundMask,
                             QueryTriggerInteraction.Ignore))
            return;

        if (_zone == null || _zoneOwner != OwnerASC)
        {
            _zone = Instantiate(TickZone);
            _zone.Initialize(OwnerASC);
            _zone.SourceTemplate = TickZone;   // su identidad en la red (el VFX de la zona)
            _zone.CooldownEffect = null;
            _zone.CostEffect     = null;
            _zone.DisableCharges();
            _zoneOwner = OwnerASC;
        }
        _zone.DeployZoneAt(ground.point);
    }

    // Vista previa: el cono y, con zona, hasta dónde puede caer.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Gizmos.color = new Color(1f, 0.35f, 0.1f, 1f);

        Vector3 center  = origin.TransformPoint(OriginOffset);
        float   halfAng = ConeAngle / 2f;
        const int segments = 24;

        Vector3 prev = center + Quaternion.Euler(0, -halfAng, 0) * origin.forward * Range;
        Gizmos.DrawLine(center, prev);
        for (int i = 1; i <= segments; i++)
        {
            float   angle = -halfAng + ConeAngle * i / segments;
            Vector3 point = center + Quaternion.Euler(0, angle, 0) * origin.forward * Range;
            Gizmos.DrawLine(prev, point);
            prev = point;
        }
        Gizmos.DrawLine(center, prev);

        if (TickZone != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(origin.position + origin.forward * ZoneMaxRange, TickZone.Radius);
        }
    }

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
    {
        if (origin == null) return;

        Vector3 center = origin.TransformPoint(OriginOffset);
        AbilityHandles.Distance(this, "Range", center, origin.forward, ref Range, AbilityHandles.DistanceColor);
        AbilityHandles.Angle(this, "Cone Angle", center, origin.forward, Range, ref ConeAngle, AbilityHandles.AngleColor);
        AbilityHandles.Offset(this, "Origin Offset", origin, ref OriginOffset, AbilityHandles.OffsetColor);
        if (TickZone != null)
            AbilityHandles.Distance(this, "Zone Max Range", origin.position, origin.forward, ref ZoneMaxRange,
                                    AbilityHandles.DistanceColor);
    }
#endif
}
