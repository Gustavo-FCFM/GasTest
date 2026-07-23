using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;

// ============================================================
// Entity_IllusoryBlade  (cuchilla de las Cuchillas ilusorias)
//
// Cuchilla en DOS FASES, NO es un misil teledirigido:
//
//  1) ELEVACIÓN (RiseDuration seg): se genera donde estaba el Ilusionista al pegar
//     y se eleva DESDE ESE PUNTO FIJO girando sobre sí misma (giro visual). No lo
//     sigue si se mueve.
//  2) VIAJE: cumplido el segundo, toma una FOTO de dónde está el enemigo objetivo
//     EN ESE INSTANTE, se endereza apuntando ahí, y viaja en LÍNEA RECTA (lento,
//     para dar ventana de esquive) hacia ese punto fijo. Si el enemigo se movió,
//     la esquiva. Se detiene al impactar un enemigo (le pone una Herida, sin daño),
//     al chocar una pared, o al pasar TravelTimeout segundos de viaje.
//
// La lanza IllusoryBladesPassive (server-side). El MOVIMIENTO lo simula el servidor
// y se replica por NetworkTransform; el giro visual corre local en cada peer (no
// necesita red). Solo el servidor resuelve impactos.
//
// PREFAB: NetworkObject + NetworkTransform (server-authoritative, SIN Rigidbody) +
// una malla hija para "Visual" (la que gira) + este script.
// ============================================================
[RequireComponent(typeof(NetworkObject))]
public class Entity_IllusoryBlade : NetworkBehaviour
{
    [Header("Fase 1 · Elevación")]
    [Tooltip("Segundos que se eleva girando antes de lanzarse.")]
    public float RiseDuration = 1f;
    [Tooltip("Cuánto sube por encima del Ilusionista.")]
    public float RiseHeight = 2f;
    [Tooltip("Malla que gira durante la elevación (hijo). El giro es solo visual, no se sincroniza.")]
    public Transform Visual;
    [Tooltip("Velocidad del giro visual, en grados/seg.")]
    public float SpinSpeed = 720f;

    [Header("Fase 2 · Viaje")]
    [Tooltip("Velocidad del viaje recto (baja, para dar ventana de esquive).")]
    public float TravelSpeed = 6f;
    [Tooltip("Se disipa tras estos segundos de VIAJE si no impacta nada.")]
    public float TravelTimeout = 5f;
    [Tooltip("A qué distancia de un enemigo cuenta como impacto.")]
    public float HitRadius = 0.7f;
    [Tooltip("Capas de pared/entorno que frenan la cuchilla.")]
    public LayerMask WallLayer;
    [Tooltip("Capa de personajes, para detectar al enemigo impactado.")]
    public LayerMask CharacterLayer;

    // Estado solo-servidor.
    private AbilitySystemComponent _source;
    private int                    _sourceTeamId;
    private AbilitySystemComponent _target;
    private GameplayEffect         _woundEffect;

    private Vector3 _riseBase; // punto FIJO desde donde se eleva (donde nació)
    private float   _riseTimer;
    private bool    _launched;
    private Vector3 _travelDir;
    private float   _travelTimer;
    private bool    _resolved;

    // Giro visual local (corre en todos los peers durante la elevación).
    private float _spinTimer;

    // La llama IllusoryBladesPassive en el servidor justo después de spawnear.
    public void ServerInit(AbilitySystemComponent target, AbilitySystemComponent source, GameplayEffect wound)
    {
        _target       = target;
        _source       = source;
        _sourceTeamId = source != null ? source.TeamID : 0;
        _woundEffect  = wound;

        // Punto de nacimiento FIJO: desde acá se eleva (no sigue al Ilusionista).
        _riseBase = transform.position;
    }

    private void Update()
    {
        // Giro visual (cosmético) durante la elevación — en todos los peers, sin red.
        if (Visual != null && _spinTimer < RiseDuration)
        {
            _spinTimer += Time.deltaTime;
            Visual.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.Self);
        }

        // La lógica (movimiento e impactos) es autoridad del servidor.
        if (!IsServerInitialized || _resolved) return;

        if (!_launched) RiseAndLaunch();
        else            Travel();
    }

    // Fase 1: se eleva DESDE EL PUNTO DONDE NACIÓ (fijo, no sigue al Ilusionista).
    // Al cumplir RiseDuration, snapshotea la posición del enemigo y arranca el viaje.
    private void RiseAndLaunch()
    {
        _riseTimer += Time.deltaTime;

        float t = Mathf.Clamp01(_riseTimer / RiseDuration);
        transform.position = _riseBase + Vector3.up * (RiseHeight * t);

        if (_riseTimer < RiseDuration) return;

        // Snapshot: a dónde estaba el enemigo EN ESTE instante.
        if (_target == null || _target.HasTag(EGameplayTag.State_Dead)) { Dissipate(); return; }

        Vector3 snapshot = _target.transform.position + Vector3.up; // apuntar al torso
        _travelDir = snapshot - transform.position;
        if (_travelDir.sqrMagnitude < 0.0001f) { Dissipate(); return; }

        _travelDir.Normalize();
        // La PUNTA del prefab está en +Y local (no en el forward/+Z), así que
        // alineamos el eje Y con la dirección de viaje: la punta queda mirando al
        // objetivo mientras la cuchilla viaja recto hacia él.
        transform.rotation = Quaternion.FromToRotation(Vector3.up, _travelDir);
        _launched = true;
    }

    // Fase 2: viaja recto hasta impactar un enemigo, chocar una pared, o expirar.
    private void Travel()
    {
        _travelTimer += Time.deltaTime;
        if (_travelTimer >= TravelTimeout) { Dissipate(); return; }

        float step = TravelSpeed * Time.deltaTime;

        // Pared en el tramo → se disipa ahí.
        if (Physics.SphereCast(transform.position, HitRadius * 0.5f, _travelDir,
                               out RaycastHit wallHit, step, WallLayer, QueryTriggerInteraction.Ignore))
        {
            transform.position = wallHit.point;
            Dissipate();
            return;
        }

        transform.position += _travelDir * step;

        // Primer enemigo vivo dentro del radio → Herida (sin daño) y se disipa.
        Collider[] cols = Physics.OverlapSphere(transform.position, HitRadius, CharacterLayer);
        foreach (Collider c in cols)
        {
            AbilitySystemComponent asc = c.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || asc.HasTag(EGameplayTag.State_Dead) || !IsEnemy(asc)) continue;

            if (_woundEffect != null) asc.ApplyGameplayEffect(_woundEffect, _source);
            Dissipate();
            return;
        }
    }

    // Afiliación por el equipo capturado al nacer (el _source podría haber muerto).
    private bool IsEnemy(AbilitySystemComponent target)
    {
        if (target == null) return false;
        if (_sourceTeamId == 0 || target.TeamID == 0) return true;
        return _sourceTeamId != target.TeamID;
    }

    // Despawnea en red (borra la cuchilla en todos los peers).
    private void Dissipate()
    {
        if (_resolved) return;
        _resolved = true;
        if (IsServerInitialized && IsSpawned) ServerManager.Despawn(gameObject);
    }
}
