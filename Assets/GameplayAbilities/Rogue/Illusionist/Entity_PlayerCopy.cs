using UnityEngine;
using FishNet.Object;

// ============================================================
// Entity_PlayerCopy  (Copia exacta — señuelo del Ilusionista)
//
// Una copia VISUAL del jugador que camina desde donde se generó hasta el punto al
// que el Ilusionista apuntaba al invocarla, con la animación y velocidad del
// jugador. No tiene límite de tiempo: se queda ahí (idle) hasta que la golpean o
// hasta que su invocador muere.
//
// Al ser GOLPEADA por un enemigo: SOLO ese enemigo recibe un "flashbang"
// (Status_Blinded) y una Herida mortal, y la copia explota (se despawnea). Como es
// un señuelo, su ASC lleva Status_Immortal para que el propio golpe no la "mate"
// antes de explotar; la reacción la maneja OnTookDamage.
//
// RED: NetworkObject + NetworkTransform (server-authoritative, syncroniza pos/rot).
// El SERVIDOR simula el caminar y resuelve el golpe; la animación la corre cada
// peer localmente según la velocidad observada (delta de posición), sin red extra.
//
// PREFAB: raíz con NetworkObject + NetworkTransform + AbilitySystemComponent (con un
// CharacterRoleDefinition mínimo que tenga Health/MaxHealth, para que el daño lo
// registre) + un Collider en la capa de personajes (CharacterLayer) + la malla y el
// Animator del jugador (con la animación de caminar) + este script. Las copias las
// gestiona PlayerCopyManager (FIFO, límite, limpieza por muerte).
// ============================================================
[RequireComponent(typeof(NetworkObject))]
public class Entity_PlayerCopy : NetworkBehaviour
{
    [Header("Referencias")]
    [Tooltip("ASC del señuelo (normalmente en la misma raíz). Da el equipo y recibe el golpe.")]
    public AbilitySystemComponent Asc;
    [Tooltip("Animator de la copia (con la animación de caminar del jugador).")]
    public Animator WalkAnimator;

    [Header("Efectos al ser golpeada")]
    [Tooltip("Flashbang: se aplica al enemigo que golpea la copia (otorga Status_Blinded).")]
    public GameplayEffect BlindEffect;
    [Tooltip("Herida mortal: se aplica al enemigo que golpea la copia (el GE apilable Mortal Wounds).")]
    public GameplayEffect WoundEffect;

    [Header("Movimiento")]
    [Tooltip("A qué distancia (horizontal) del objetivo se considera que llegó y se queda idle.")]
    public float StopDistance = 0.4f;
    [Tooltip("Nombre del parámetro float del Animator que controla la mezcla caminar/idle.")]
    public string AnimatorSpeedParam = "Speed";

    // Estado solo-servidor.
    private AbilitySystemComponent _ownerASC;
    private Vector3 _target;     // punto objetivo (se camina hacia su proyección horizontal)
    private float   _speed;      // velocidad de caminado (la del jugador al invocar)
    private bool    _arrived;
    private bool    _exploding;
    private AbilitySystemComponent _attacker; // quién la golpeó (dispara la explosión)
    private bool    _resolved;

    // Para animar en todos los peers según el movimiento observado.
    private Vector3 _lastAnimPos;
    private bool    _hasLastAnimPos;

    // La llama PlayerCopyManager en el servidor justo después de spawnear.
    public void ServerInit(AbilitySystemComponent owner, Vector3 target, float speed)
    {
        _ownerASC = owner;
        _target   = target;
        _speed    = Mathf.Max(0f, speed);

        if (Asc != null)
        {
            Asc.TeamID = owner != null ? owner.TeamID : 0;
            // Inmortal: un golpe no debe "matar" al señuelo antes de explotar; la
            // reacción la maneja OnTookDamage (abajo).
            Asc.AddTag(EGameplayTag.Status_Immortal);
            Asc.OnTookDamage += HandleTookDamage;
        }

        // Encara de una hacia el objetivo (horizontal).
        Vector3 flat = _target - transform.position; flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat.normalized);
    }

    // Un enemigo golpeó la copia: marca la explosión (una sola vez). La resolución
    // se difiere a Update para no mutar/despawnear en medio de ExecuteInstantEffect.
    private void HandleTookDamage(AbilitySystemComponent attacker)
    {
        if (_exploding || attacker == null || ReferenceEquals(attacker, _ownerASC)) return;
        _exploding = true;
        _attacker  = attacker;
    }

    private void Update()
    {
        // Animación (todos los peers): velocidad horizontal observada → parámetro del
        // Animator, así la copia camina mientras se mueve y queda idle al llegar.
        if (WalkAnimator != null && !string.IsNullOrEmpty(AnimatorSpeedParam))
        {
            Vector3 p = transform.position;
            float observed = 0f;
            if (_hasLastAnimPos)
            {
                Vector3 d = p - _lastAnimPos; d.y = 0f;
                observed = d.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            }
            _lastAnimPos = p;
            _hasLastAnimPos = true;
            WalkAnimator.SetFloat(AnimatorSpeedParam, observed, 0.1f, Time.deltaTime);
        }

        // La lógica (caminar y explotar) es autoridad del servidor.
        if (!IsServerInitialized || _resolved) return;

        if (_exploding) { Explode(); return; }

        if (!_arrived) Walk();
    }

    // Camina en línea recta (horizontal) hacia el objetivo, manteniendo su altura
    // de spawn (no sigue el ángulo vertical del punto de mira). Al llegar, idle.
    private void Walk()
    {
        Vector3 pos = transform.position;
        Vector3 to  = _target - pos; to.y = 0f;
        float dist  = to.magnitude;

        if (dist <= StopDistance) { _arrived = true; return; }

        Vector3 dir = to / dist;
        transform.position  = pos + dir * _speed * Time.deltaTime;
        transform.rotation  = Quaternion.LookRotation(dir);
    }

    // Aplica flashbang + Herida SOLO al enemigo que la golpeó y se despawnea.
    private void Explode()
    {
        if (_attacker != null && !_attacker.HasTag(EGameplayTag.State_Dead))
        {
            if (BlindEffect != null) _attacker.ApplyGameplayEffect(BlindEffect, _ownerASC);
            if (WoundEffect != null) _attacker.ApplyGameplayEffect(WoundEffect, _ownerASC);
        }
        Dissipate();
    }

    // Despawn en red (borra la copia en todos los peers). La usa la explosión y el
    // PlayerCopyManager (límite FIFO / muerte del invocador).
    public void Dissipate()
    {
        if (_resolved) return;
        _resolved = true;
        if (Asc != null) Asc.OnTookDamage -= HandleTookDamage;
        if (IsServerInitialized && IsSpawned) ServerManager.Despawn(gameObject);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (Asc != null) Asc.OnTookDamage -= HandleTookDamage;
    }
}
