using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

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
// Animator del jugador (con la animación de caminar) + este script.
//
// Desde el 20 de octubre de 2026 es una invocación (Entity_Summon): el límite de la Copia
// exacta (la más vieja se va; las de la Fiesta no cuentan), irse si el Ilusionista muere
// ("Si el dueño muere: desaparece"), se va o cambia de clase, los resuelve la base.
// PlayerCopyManager solo las crea.
// ============================================================
[RequireComponent(typeof(NetworkObject))]
public class Entity_PlayerCopy : Entity_Summon
{
    [Header("Referencias")]
    [Tooltip("ASC del señuelo (normalmente en la misma raíz). Da el equipo y recibe el golpe.")]
    [Label("ASC")]
    public AbilitySystemComponent Asc;
    [Tooltip("Animator de la copia. El controller (animator override) se toma de la clase del jugador copiado.")]
    [Label("Animator")]
    public Animator WalkAnimator;

    [Header("Sockets de arma (bones del Avatar, se buscan por nombre)")]
    [Tooltip("Nombre del socket de la mano principal. Se busca por nombre en la jerarquía (los bones " +
             "del Avatar son un prefab anidado y no se pueden arrastrar como referencia).")]
    [Label("Socket mano principal")]
    public string MainHandSocketName = "Socket_MainHand";
    [Tooltip("Nombre del socket de la mano secundaria.")]
    [Label("Socket mano secundaria")]
    public string OffHandSocketName = "Socket_OffHand";

    [Header("Efectos al ser golpeada")]
    [Tooltip("Flashbang: se aplica al enemigo que golpea la copia (otorga Status_Blinded).")]
    [Label("Efecto de ceguera")]
    public GameplayEffect BlindEffect;
    [Tooltip("Herida mortal: se aplica al enemigo que golpea la copia (el GE apilable Mortal Wounds).")]
    [Label("Efecto de herida")]
    public GameplayEffect WoundEffect;

    [Header("Movimiento")]
    [Tooltip("A qué distancia (horizontal) del objetivo se considera que llegó y se queda idle.")]
    [Label("Distancia de llegada (m)")]
    public float StopDistance = 0.4f;
    [Tooltip("Nombre del parámetro float del Animator que controla la mezcla caminar/idle.")]
    [Label("Parámetro de velocidad")]
    public string AnimatorSpeedParam = "Speed";
    [Tooltip("Capas de pared/entorno que frenan a la copia (no las atraviesa). Asigná la misma capa de paredes que usa el Dash.")]
    [Label("Capas que frenan")]
    public LayerMask WallLayer;
    [Tooltip("Radio del chequeo de pared al caminar.")]
    [Label("Radio de chequeo de pared (m)")]
    public float WallCheckRadius = 0.4f;

    // Índice de clase del jugador COPIADO, sincronizado a todos los peers. Cada peer
    // lo resuelve a una clase (vía PlayerController.ResolveClassByIndex) para copiar
    // su arma y animator override. -1 = todavía sin asignar.
    private readonly SyncVar<int> _sourceClassIndex = new SyncVar<int>(-1);

    // El jugador COPIADO, en todos los peers: su barra de vida (UI_WorldHealthbar.Mirror)
    // muestra la de él —nombre, vida, buffs, clase—, así el enemigo no sabe cuál es el real.
    private readonly SyncVar<NetworkObject> _sourcePlayer = new SyncVar<NetworkObject>();
    private bool _nameplateMirrored;

    // Estado solo-servidor.
    private Vector3 _target;     // punto objetivo (se camina hacia su proyección horizontal)
    private float   _speed;      // velocidad de caminado (la del jugador al invocar)
    private bool    _arrived;
    private bool    _exploding;
    private AbilitySystemComponent _attacker; // quién la golpeó (dispara la explosión)
    private bool    _resolved;

    // Para animar en todos los peers según el movimiento observado.
    private Vector3 _lastAnimPos;
    private bool    _hasLastAnimPos;
    private bool    _visualsApplied; // el arma/animator del jugador copiado ya se aplicó

    // Aplica el ARMA + animator override del jugador copiado (resuelto por su índice
    // de clase). Corre en cada peer cuando el índice ya llegó y hay jugador local para
    // resolver la clase — se reintenta desde Update hasta lograrlo (una sola vez). El
    // avatar es compartido, así que solo cambian arma y animación por clase.
    // El Animator del modelo. Se resuelve solo si la ranura quedó vacía: el prefab del
    // señuelo ya se quedó una vez sin animación de caminar por eso, y el arreglo no puede
    // depender de acordarse de arrastrarlo cada vez que se cambia el modelo.
    protected override void Awake()
    {
        base.Awake();
        if (WalkAnimator == null) WalkAnimator = GetComponentInChildren<Animator>();
    }

    private void TryApplyVisuals()
    {
        if (_visualsApplied) return;

        int idx = _sourceClassIndex.Value;
        if (idx < 0) return;                                   // el índice todavía no sincronizó

        PlayerController localPc = PlayerController.LocalPlayer;
        if (localPc == null) return;                           // sin jugador local no se puede resolver (reintenta)

        _visualsApplied = true;

        CharacterClassDefinition cls = localPc.ResolveClassByIndex(idx);
        if (cls == null) return;                               // índice inválido: sin arma (degradación silenciosa)

        // Mismo armado que el jugador, así la copia de un Clérigo también sostiene el libro.
        if (WalkAnimator != null && cls.ClassAnimatorOverride != null)
        {
            WalkAnimator.runtimeAnimatorController = PlayerController.BuildClassAnimator(cls, cls.ClassAnimatorOverride);
            PlayerController.ApplyOffHandPoseWeight(WalkAnimator, cls);
        }

        EquipWeapon(cls.MainHandWeaponPrefab, FindDeep(transform, MainHandSocketName),
                    cls.MainHandPositionOffset, cls.MainHandRotationOffset);
        EquipWeapon(cls.OffHandWeaponPrefab,  FindDeep(transform, OffHandSocketName),
                    cls.OffHandPositionOffset,  cls.OffHandRotationOffset);
    }

    // Instancia un arma en su socket con la MISMA pose que le da la clase al jugador
    // real (ver PlayerController.UpdateVisuals): si no, la copia mostraría el arma con
    // otra orientación que el original. Apaga el rastro del arma: es solo para los
    // golpes del jugador, el señuelo no lo necesita.
    private void EquipWeapon(GameObject prefab, Transform socket,
                             Vector3 positionOffset, Vector3 rotationOffset)
    {
        if (prefab == null || socket == null) return;
        GameObject weapon = Instantiate(prefab, socket);
        weapon.transform.SetLocalPositionAndRotation(positionOffset, Quaternion.Euler(rotationOffset));
        Transform trail = weapon.transform.Find("WeaponTrail");
        if (trail != null) trail.gameObject.SetActive(false);
    }

    // Busca un transform por nombre en toda la jerarquía (profundidad primero).
    // transform.Find no sirve: los sockets están varios niveles adentro del esqueleto.
    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    // La llama PlayerCopyManager en el servidor, justo después de crearla con Entity_Summon.Spawn
    // (que ya le dio el dueño y el equipo).
    // sourceClassIndex = índice de clase del jugador COPIADO (para su arma/anim); en la
    // Copia exacta es el propio Ilusionista, en Fiesta puede ser un aliado.
    // source = el jugador copiado (su barra de vida es la que muestra la copia).
    public void Begin(Vector3 target, float speed, int sourceClassIndex, AbilitySystemComponent source = null)
    {
        _target   = target;
        _speed    = Mathf.Max(0f, speed);
        _sourceClassIndex.Value = sourceClassIndex;
        _sourcePlayer.Value     = source != null ? source.GetComponent<NetworkObject>() : null;

        if (Asc != null)
        {
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
        if (_exploding || attacker == null || ReferenceEquals(attacker, Summoner)) return;
        _exploding = true;
        _attacker  = attacker;
    }

    protected override void Update()
    {
        base.Update();

        // Copia el arma/animator del jugador origen apenas se pueda (todos los peers).
        TryApplyVisuals();

        // Y su barra de vida, apenas llega quién es.
        if (!_nameplateMirrored && _sourcePlayer.Value != null)
        {
            _nameplateMirrored = true;
            UI_WorldHealthbar bar = GetComponent<UI_WorldHealthbar>();
            if (bar != null) bar.Mirror(_sourcePlayer.Value.gameObject);
        }

        // Animación (todos los peers): velocidad horizontal observada → parámetros del
        // Animator, así la copia camina mientras se mueve y queda idle al llegar. Como
        // la copia siempre ENCARA su dirección de avance, en su espacio local se mueve
        // hacia adelante (MoveY = velocidad, MoveX = 0). Seteamos Speed y MoveX/MoveY
        // igual que PlayerController.UpdateAnimations, para blends 1D o 2D
        // (los SetFloat sobre un parámetro inexistente son no-ops inofensivos).
        if (WalkAnimator != null)
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

            // La copia siempre encara su avance, así que se mueve "de frente":
            // MoveY = 1 mientras camina, 0 al frenar; MoveX = 0. MoveX/MoveY van
            // normalizados (~[-1,1]) como en el jugador, no en velocidad cruda. Todo
            // sale del movimiento OBSERVADO (delta de posición), así anda en todos los
            // peers sin conocer _speed (que solo existe en el servidor). Speed va crudo.
            bool moving = observed > 0.3f;
            if (!string.IsNullOrEmpty(AnimatorSpeedParam))
                WalkAnimator.SetFloat(AnimatorSpeedParam, observed, 0.1f, Time.deltaTime);
            WalkAnimator.SetFloat("MoveX", 0f,               0.1f, Time.deltaTime);
            WalkAnimator.SetFloat("MoveY", moving ? 1f : 0f, 0.1f, Time.deltaTime);
        }

        // La lógica (caminar y explotar) es autoridad del servidor.
        if (!IsLiveOnServer || _resolved) return;

        if (_exploding) { Explode(); return; }

        if (!_arrived) Walk();
        StickToGround();
    }

    // Que pise el piso (20 de octubre de 2026: lanzada en pleno salto se quedaba flotando, porque
    // caminaba a la altura en la que había nacido). Mira el piso desde la cintura: si está abajo,
    // cae (cada vez más rápido, como el jugador); si está un poco arriba (un escalón, una rampa),
    // lo sube. Corre siempre, también quieta: una copia que llegó en el aire baja igual.
    private const float FallGravity = 20f;
    private float _fallSpeed;

    private void StickToGround()
    {
        Vector3 pos = transform.position;
        if (!Physics.Raycast(pos + Vector3.up * 0.9f, Vector3.down, out RaycastHit hit, 60f, GroundMask,
                             QueryTriggerInteraction.Ignore))
            return;   // sin piso abajo (fuera del mapa): se queda donde está

        float gap = pos.y - hit.point.y;
        if (gap <= 0.02f)
        {
            pos.y      = hit.point.y;   // en el piso (o subiendo un escalón)
            _fallSpeed = 0f;
        }
        else
        {
            _fallSpeed += FallGravity * Time.deltaTime;
            pos.y       = Mathf.Max(hit.point.y, pos.y - _fallSpeed * Time.deltaTime);
        }
        transform.position = pos;
    }

    // Camina en línea recta (horizontal) hacia el objetivo (no sigue el ángulo vertical del
    // punto de mira); la altura la pone el piso (StickToGround). Al llegar, idle.
    // No atraviesa paredes: si hay una en el tramo, frena ahí y se queda quieta.
    private void Walk()
    {
        Vector3 pos = transform.position;
        Vector3 to  = _target - pos; to.y = 0f;
        float dist  = to.magnitude;

        if (dist <= StopDistance) { _arrived = true; return; }

        Vector3 dir  = to / dist;
        float   step = _speed * Time.deltaTime;

        // Pared adelante (el collider es trigger y el movimiento es por transform, así
        // que no choca solo): frenamos antes de atravesarla. Chequeo a la altura del
        // torso para no pegar contra el piso.
        Vector3 castOrigin = pos + Vector3.up * 0.9f;
        if (Physics.SphereCast(castOrigin, WallCheckRadius, dir, out _,
                               step + WallCheckRadius, WallLayer, QueryTriggerInteraction.Ignore))
        {
            _arrived = true; // se queda donde está, sin meterse en la pared
            return;
        }

        transform.position = pos + dir * step;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    // Aplica flashbang + Herida SOLO al enemigo que la golpeó y se despawnea.
    private void Explode()
    {
        if (_attacker != null && !_attacker.HasTag(EGameplayTag.State_Dead))
        {
            if (BlindEffect != null) _attacker.ApplyGameplayEffect(BlindEffect, Summoner);
            if (WoundEffect != null) _attacker.ApplyGameplayEffect(WoundEffect, Summoner);
        }
        Dissipate();
    }

    // Despawn en red (borra la copia en todos los peers), al explotar. El límite y la muerte
    // del Ilusionista los resuelve la base (Entity_Summon).
    public void Dissipate()
    {
        if (_resolved) return;
        _resolved = true;
        if (Asc != null) Asc.OnTookDamage -= HandleTookDamage;
        ServerDespawn();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (Asc != null) Asc.OnTookDamage -= HandleTookDamage;
    }
}
