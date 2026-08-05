using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GameplayAbility
//
// Clase base abstracta de toda habilidad del juego (ScriptableObject,
// una instancia por personaje que la tiene otorgada — ver
// AbilitySystemComponent.GrantAbility). Define el ciclo de vida común
// (costo, cooldown, activación, fin) y utilidades compartidas
// (afiliación, VFX, gizmos de editor); cada subclase concreta
// (GA_ConeAttack, GA_LeapAttack, etc.) implementa Activate() con su
// propia lógica de detección/daño.
// ============================================================
public abstract class GameplayAbility : ScriptableObject
{
    // =========================================================
    // CONFIGURACIÓN GENERAL
    // =========================================================

    [Header("Configuración General")]
    public string AbilityName = "New Ability";
    public Sprite AbilityIcon;

    [Header("Costes")]
    // Efecto instantáneo que se descuenta al activar (ej: -20 de maná).
    public GameplayEffect CostEffect;

    [Header("Cooldown")]
    // Efecto CON duración que bloquea reactivar la habilidad mientras esté
    // activo (ver CanActivate). Lo IMPORTANTE del GE acá es su primer GrantedTag:
    // es la "identidad" del cooldown para la UI, la carga de ultimate y el
    // bloqueo por slot. La DURACIÓN normalmente la define CooldownDuration (abajo),
    // así podés REUSAR un mismo GE de cooldown para muchas habilidades (uno por
    // slot/tag) en vez de crear uno por cada una.
    public GameplayEffect CooldownEffect;

    [Tooltip("Duración del cooldown en segundos, configurada acá en el GA. Si es > 0, " +
             "pisa el Duration del CooldownEffect (reusá un mismo GE de cooldown y ajustá " +
             "el tiempo por habilidad). 0 = usar el Duration del GE. Lo ignora " +
             "UseAttackSpeedAsCooldown (ese tiene prioridad).")]
    public float CooldownDuration = 0f;

    [Header("Cooldown Dinámico")]
    [Tooltip("Marcá esto en los ATAQUES BÁSICOS: el cooldown sale del stat AtkSpeed del dueño " +
             "(ignorando CooldownDuration y el Duration del CooldownEffect), o sea que el cooldown " +
             "ES el ritmo de ataque.\n\n" +
             "También controla la ANIMACIÓN: al ser un ritmo, el clip se estira/comprime para durar " +
             "exactamente ese tiempo y el swing entra justo entre golpe y golpe. Las habilidades " +
             "normales (cooldown = espera, no ritmo) reproducen su clip a velocidad natural.")]
    public bool UseAttackSpeedAsCooldown = false;

    [Header("Ultimate Charge")]
    // Cuánto adelanta el cooldown de la ultimate cada vez que esta
    // habilidad conecta un golpe (ver ChargeUltimate).
    public float UltimateChargeAmount = 0f;

    [Header("Bloqueos")]
    // Si el dueño tiene cualquiera de estos tags, CanActivate() falla
    // (ej: no se puede atacar si está Silenciado).
    public List<EGameplayTag> ActivationBlockedTags;

    [Tooltip("Al revés que ActivationBlockedTags: el dueño DEBE tener TODOS estos tags para " +
             "poder activarla (ej: 'Marcado para morir' del Asesino solo se lanza estando invisible). " +
             "Vacío = sin requisitos.")]
    public List<EGameplayTag> ActivationRequiredTags;

    [Header("Animación")]
    [Tooltip("FORMA RECOMENDADA: arrastrá acá el clip de esta habilidad y listo — no hace falta " +
             "crear un estado en el Animator, ni reservar un AnimationID, ni mapear el clip en el " +
             "override de cada clase. El clip se mete en runtime en la ranura genérica de acción " +
             "(ver PlayerController.ActionClipSlotName). Como el clip vive en ESTE asset, cada peer " +
             "reproduce el mismo sin sincronizar nada extra.\n\n" +
             "Si lo dejás vacío se usa el esquema viejo de AnimationTriggerName + AnimationID.")]
    public AnimationClip AnimationClip;

    [Tooltip("Esquema viejo (solo se usa si AnimationClip está vacío): nombre del trigger del Animator.")]
    public string AnimationTriggerName = "AttackTrigger";

    [Tooltip("Esquema viejo (solo se usa si AnimationClip está vacío). 1=Melee, 2=Proyectil, 3=Salto, 4=Extra")]
    public int AnimationID = 1;

    // Nombre del Animation Event que marca el FRAME DE IMPACTO dentro de un clip.
    //
    // Es una CONSTANTE y no un campo del inspector a propósito: el evento tiene que
    // llamar a un método que exista en el receptor (PlayerController.AnimationEvent_HitFrame,
    // vía PlayerAnimationEvents). Con cualquier otro nombre, Unity no encontraría el
    // receptor y llenaría la consola de "AnimationEvent has no receiver!". O sea que
    // poder editarlo por habilidad era una libertad falsa.
    //
    // Cómo se usa: en el clip (ventana Animation, o el importer del FBX → Events) se
    // agrega un evento con esta función en el frame donde el arma conecta; el servidor
    // lee ese timestamp del asset y resuelve el golpe ahí, sin calcular tiempos a mano.
    // Varios eventos en el mismo clip = golpe escalonado (barridos que 'recorren').
    // Sin eventos (o sin clip), se usa el delay fijo de la habilidad.
    public const string HitFrameEventName = "AnimationEvent_HitFrame";

    // Velocidad a la que hay que reproducir la animación de esta habilidad.
    //
    // El clip se ESTIRA/COMPRIME para durar lo que el ritmo de ataque SOLO cuando
    // UseAttackSpeedAsCooldown está activo — que es precisamente lo que define a un
    // ataque básico: su cooldown ES cada cuánto podés volver a pegar, así que el swing
    // tiene que entrar justo en ese hueco (clip de 2s con 0.8s de ritmo → 2.5x).
    //
    // En cualquier otra habilidad el cooldown NO es un ritmo sino una espera, y estirar
    // el clip para llenarlo sería absurdo (un ult con 60s de cooldown quedaría a cámara
    // lenta). Esas reproducen su clip a velocidad natural.
    //
    // El MISMO valor lo usan el Animator (para reproducir) y HitTimingRoutine (para
    // programar el golpe), así el daño siempre cae en el frame que se ve.
    public float ResolveAnimationSpeed()
    {
        if (AnimationClip != null && UseAttackSpeedAsCooldown && AnimationClip.length > 0.001f)
        {
            float target = ResolveCooldownDuration();
            // Clamp: un ritmo minúsculo no debe dar una animación ilegible.
            if (target > 0f) return Mathf.Clamp(AnimationClip.length / target, 0.1f, 10f);
        }

        // Con clip pero sin ritmo de ataque: velocidad natural del clip.
        if (AnimationClip != null) return 1f;

        // Esquema viejo (sin clip): el multiplicador global de siempre.
        float atkSpeed = OwnerASC != null ? OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed) : 0f;
        return atkSpeed > 0f ? 1f / atkSpeed : 1f;
    }

    // Caché de los tiempos leídos (AnimationClip.events aloca un array en cada
    // acceso). Se invalida solo si cambia el clip — los pasos de combo pueden
    // pisarlo (ver ComboStep.AnimationClipOverride).
    [System.NonSerialized] private AnimationClip _hitTimesClip;
    [System.NonSerialized] private List<float>   _hitTimesCache;
    // El aviso de "al clip le faltan los eventos de impacto" se da una sola vez.
    [System.NonSerialized] private bool _warnedNoHitFrames;

    // Momentos (en segundos DENTRO del clip) en los que este ataque conecta, leídos
    // de los Animation Events del AnimationClip. Lista vacía = el clip no los define
    // y hay que caer al delay fijo de la habilidad.
    //
    // Esto es lo que permite que el daño caiga exactamente cuando el arma golpea sin
    // configurar ningún número: el tiempo sale de la animación. Y como lo lee el
    // SERVIDOR desde el asset, no depende de que ningún cliente reporte nada (los
    // Animation Events reales corren en cada cliente y no serían confiables).
    public List<float> GetHitFrameTimes()
    {
        if (_hitTimesClip == AnimationClip && _hitTimesCache != null) return _hitTimesCache;

        _hitTimesClip  = AnimationClip;
        _hitTimesCache = new List<float>();

        if (AnimationClip != null)
        {
            foreach (AnimationEvent evt in AnimationClip.events)
                if (evt.functionName == HitFrameEventName) _hitTimesCache.Add(evt.time);

            _hitTimesCache.Sort();
        }
        return _hitTimesCache;
    }

    // Corre el TIMING de un ataque y llama a 'onHit' en cada momento de impacto. Los
    // tiempos salen de los Animation Events del clip, escalados por la velocidad a la
    // que se reproduce (ver ResolveAnimationSpeed): así el golpe cae siempre en el
    // frame que se ve, aunque el swing se comprima por el ritmo de ataque.
    //
    // A 'onHit' se le pasa un conjunto COMPARTIDO de ya golpeados: un swing con
    // varios frames de impacto (un barrido escalonado) le pega UNA sola vez a cada
    // enemigo, aunque siga dentro del área en el siguiente test.
    protected IEnumerator HitTimingRoutine(System.Action<HashSet<AbilitySystemComponent>> onHit)
    {
        // La MISMA velocidad a la que se reproduce el clip: si el swing se comprime
        // para entrar en el ritmo de ataque, el golpe se adelanta en igual proporción.
        float speedMultiplier = ResolveAnimationSpeed();
        if (speedMultiplier <= 0f) speedMultiplier = 1f;

        HashSet<AbilitySystemComponent> alreadyHit = new HashSet<AbilitySystemComponent>();
        List<float> hitTimes = GetHitFrameTimes();

        // Sin eventos en el clip no hay con qué sincronizar: el golpe sale de una. Se
        // avisa una vez, porque casi siempre significa que al clip le falta el evento.
        if (hitTimes.Count == 0)
        {
            if (!_warnedNoHitFrames)
            {
                _warnedNoHitFrames = true;
                Debug.LogWarning($"[{AbilityName}] Su clip no tiene eventos '{HitFrameEventName}', " +
                                 $"así que el golpe se resuelve al instante (sin sincronizar con la " +
                                 $"animación). Agregá el evento en el frame de impacto del clip.");
            }
            onHit?.Invoke(alreadyHit);
            yield break;
        }

        // Con eventos: un test por cada uno, esperando lo que falte hasta ese momento
        // del clip (los tiempos son absolutos dentro del clip, por eso el descuento).
        float elapsed = 0f;
        foreach (float time in hitTimes)
        {
            float wait = (time - elapsed) / speedMultiplier;
            if (wait > 0f) yield return new WaitForSeconds(wait);
            elapsed = time;
            onHit?.Invoke(alreadyHit);
        }
    }

    [Header("Detección")]
    // Capas de física que puede golpear esta habilidad. Es un FILTRO DE FÍSICA:
    // la detección (Physics.OverlapSphere/Box/Capsule) solo considera colliders
    // en estas capas. La afiliación amigo/enemigo se resuelve aparte en código
    // (IsEnemyOf), no acá — por eso normalmente esto apunta a la capa de
    // personajes (jugadores + NPCs) y el filtro de equipo lo hace la habilidad.
    // La geometría de cada ataque (radio/largo/ángulo) la define cada habilidad
    // concreta con sus propios campos.
    public LayerMask TargetLayer;

    // Un paso de la secuencia visual automática (ver VisualsSequence).
    [System.Serializable]
    public struct AbilityVisual
    {
        public GameObject   VFXPrefab;      // Qué instanciar
        public float        Delay;          // Espera antes de instanciarlo, en segundos
        public Vector3      Offset;         // Desplazamiento local respecto al dueño
        public Vector3      RotationOffset; // Rotación local extra
        public Vector3      Scale;          // Escala final (Vector3.zero = usar Vector3.one)
        public bool         AttachToOwner;  // Si sigue al dueño como hijo de su transform
        public float        DestroyTime;    // Se destruye solo tras estos segundos (si EndWithTag es None)
        public EGameplayTag EndWithTag;     // En vez de DestroyTime, se destruye cuando el dueño pierde este tag
    }

    [Header("Visuales de Habilidad (Automáticos)")]
    // Secuencia de VFX que se reproduce sola al hacer CommitAbility(), sin
    // que la habilidad tenga que instanciarlos a mano.
    public List<AbilityVisual> VisualsSequence;

    // Personaje dueño de esta instancia de habilidad. Lo asigna
    // Initialize() al otorgarla; el resto de la clase asume que nunca es
    // null salvo mientras la habilidad todavía no fue otorgada.
    protected AbilitySystemComponent OwnerASC;

    // Cuando esta instancia es un CLON (GrantAbility y los pasos de combo la
    // crean con Instantiate), apunta al asset-template original. Lo usa
    // GameplayAbilityRegistry para resolver el índice de red de un clon.
    // NonSerialized a propósito: es estado de runtime, no se guarda en el asset
    // ni se copia al clonar (se setea a mano justo después del Instantiate).
    [System.NonSerialized] public GameplayAbility SourceTemplate;

    // Para no repetir el aviso de "CooldownEffect sin tag" en cada activación.
    [System.NonSerialized] private bool _warnedNoCooldownTag;

    // True si este código está corriendo en el servidor (o si no hay red,
    // ej. un NPC). Cada Activate() concreto debe empezar con
    // "if (!IsServer) return;" para que la lógica de juego (daño,
    // detección) tenga autoridad única en el servidor.
    protected bool IsServer
    {
        get
        {
            if (OwnerASC == null) return true;
            NetworkAbilitySystemComponent netASC =
                OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
            if (netASC == null) return true; // sin red (singleplayer/NPC): siempre "servidor"
            return netASC.IsServerInitialized;
        }
    }

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    // La llama AbilitySystemComponent.GrantAbility() al otorgar esta
    // instancia a un personaje.
    public void Initialize(AbilitySystemComponent asc)
    {
        OwnerASC = asc;
    }

    // Valida si la habilidad se puede activar ahora mismo: dueño vivo,
    // sin tags bloqueantes, sin cooldown activo, y con costo pagable.
    // Cada subclase puede sobreescribirla para agregar condiciones extra
    // (ver GA_ImmortalWrath, que solo se activa estando muerto).
    public virtual bool CanActivate()
    {
        if (OwnerASC == null) return false;
        if (OwnerASC.HasTag(EGameplayTag.State_Dead)) return false;

        if (ActivationBlockedTags != null)
            foreach (EGameplayTag tag in ActivationBlockedTags)
                if (OwnerASC.HasTag(tag)) return false;

        if (ActivationRequiredTags != null)
            foreach (EGameplayTag tag in ActivationRequiredTags)
                if (!OwnerASC.HasTag(tag)) return false;

        if (CooldownEffect != null && CooldownEffect.GrantedTags.Count > 0)
            if (OwnerASC.HasTag(CooldownEffect.GrantedTags[0])) return false;

        if (CostEffect != null && !OwnerASC.CanAffordGameplayEffect(CostEffect)) return false;

        return true;
    }

    // Lógica concreta de la habilidad (detección, daño, movimiento...).
    // Cada subclase la implementa; debe empezar con "if (!IsServer) return;"
    // y llamar CommitAbility()/EndAbility() en los momentos correctos.
    public abstract void Activate();

    // Descuenta el costo, aplica el cooldown, y arranca la secuencia
    // visual automática si la habilidad tiene una configurada. Cada
    // Activate() concreto la llama una vez al confirmar que sí se va a
    // ejecutar.
    protected void CommitAbility()
    {
        if (OwnerASC == null) return;

        if (CostEffect != null)
            OwnerASC.ApplyGameplayEffect(CostEffect, this);

        if (CooldownEffect != null)
        {
            // Sin GrantedTags, CanActivate() no tiene con qué bloquear la
            // reactivación (ver el guard de GrantedTags.Count ahí): la habilidad
            // quedaría SIN cooldown real, en silencio. Avisamos una sola vez.
            if (!_warnedNoCooldownTag && (CooldownEffect.GrantedTags == null || CooldownEffect.GrantedTags.Count == 0))
            {
                _warnedNoCooldownTag = true;
                Debug.LogWarning($"[{AbilityName}] Su CooldownEffect '{CooldownEffect.name}' no tiene GrantedTags: " +
                                 $"CanActivate no puede bloquear la reactivación, así que la habilidad no va a tener " +
                                 $"cooldown real. Agregale un tag de cooldown al GE.");
            }

            OwnerASC.ApplyGameplayEffect(CooldownEffect, this, ResolveCooldownDuration());
        }

        if (VisualsSequence != null && VisualsSequence.Count > 0)
        {
            // Instantiate() dentro de PlayVisualsSequence() corre en el
            // proceso que llama a CommitAbility() (el servidor) — un cliente
            // remoto nunca vería estos VFX. ServerPlayAbilityVisualsSequence
            // corre la secuencia acá mismo Y le pide a cada cliente que
            // corra SU PROPIA copia de la misma corutina (mismos delays,
            // offsets, etc. — el resultado es idéntico en todos los peers
            // sin necesidad de sincronizar nada más).
            NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
            if (netAsc != null) netAsc.ServerPlayAbilityVisualsSequence(this);
            else OwnerASC.StartAbilityCoroutine(PlayVisualsSequence()); // fallback sin red
        }
    }

    // Cierra la activación: libera el estado "atacando" del dueño y le
    // avisa al servidor que la habilidad terminó (para que replique el
    // fin del ataque al dueño remoto, si lo hay). Cada Activate() concreto
    // la llama al finalizar su secuencia (con o sin delay).
    public virtual void EndAbility()
    {
        // Esto corre en el servidor (Activate() ya lo garantiza). Si el dueño
        // es un cliente remoto (no el host), pc.FinishAttack() de acá solo
        // resetea isAttacking en la copia del servidor — la copia real del
        // dueño nunca se entera y queda trabada en isAttacking = true para
        // siempre (no puede volver a atacar, ni girar al moverse).
        // Por eso también avisamos por red al dueño.
        PlayerController pc = OwnerASC?.GetComponent<PlayerController>();
        if (pc != null) pc.FinishAttack();

        NetworkAbilitySystemComponent netASC = OwnerASC?.GetComponent<NetworkAbilitySystemComponent>();
        if (netASC != null) netASC.ServerNotifyAbilityEnded();
    }

    // Resuelve la duración del cooldown en segundos, con prioridad:
    // AtkSpeed dinámico > CooldownDuration (del GA) > Duration del CooldownEffect.
    // La usan tanto el cooldown normal (CommitAbility) como el tiempo de recarga
    // por carga de las habilidades con cargas (ver GA_Dash) — así un mismo valor
    // define el cooldown Y cuánto tarda en volver cada carga.
    protected float ResolveCooldownDuration()
    {
        if (UseAttackSpeedAsCooldown && OwnerASC != null)
        {
            float spd = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
            if (spd > 0) return spd;
        }
        if (CooldownDuration > 0) return CooldownDuration;
        return CooldownEffect != null ? CooldownEffect.Duration : 0f;
    }

    // Adelanta el cooldown de la ultimate del dueño en UltimateChargeAmount.
    // Cada habilidad que "carga" la ultimate la llama al conectar un golpe.
    protected void ChargeUltimate()
    {
        if (UltimateChargeAmount > 0 && OwnerASC != null)
            OwnerASC.ReduceCooldownByTag(EGameplayTag.Ability_Cooldown_Ultimate, UltimateChargeAmount);
    }

    // Aplica una lista de GameplayEffect a un objetivo (usando al dueño como
    // fuente), ignorando entradas nulas. Atajo para las habilidades que aplican
    // efectos "extra" además de su daño principal (ralentizar, marcar, heridas,
    // etc.) — ver el campo AdditionalEffects de cada una.
    protected void ApplyEffectsTo(List<GameplayEffect> effects, AbilitySystemComponent target)
    {
        if (effects == null || target == null) return;
        foreach (GameplayEffect effect in effects)
            if (effect != null) target.ApplyGameplayEffect(effect, OwnerASC);
    }

    // =========================================================
    // AFILIACIÓN — atajos hacia AbilitySystemComponent.IsEnemyOf/IsAllyOf
    // usando al dueño de esta habilidad como referencia
    // =========================================================

    protected bool IsEnemy(AbilitySystemComponent target)
        => OwnerASC != null && OwnerASC.IsEnemyOf(target);

    protected bool IsAlly(AbilitySystemComponent target, bool includeSelf = true)
        => OwnerASC != null && OwnerASC.IsAllyOf(target, includeSelf);

    // =========================================================
    // VFX
    // =========================================================

    // Reproduce la secuencia configurada en VisualsSequence (con sus
    // delays/offsets/escalas). Público para que
    // NetworkAbilitySystemComponent pueda arrancarla en cada peer por
    // igual (ver ServerPlayAbilityVisualsSequence) — el resultado sale
    // idéntico en todos porque solo depende de OwnerASC.transform/tags,
    // que ya están sincronizados.
    public System.Collections.IEnumerator PlayVisualsSequence() => PlayVisualsSequence(OwnerASC);

    // Overload con dueño explícito. El peer OBSERVADOR resuelve esta habilidad
    // como el asset-template compartido (vía GameplayAbilityRegistry), que no
    // tiene OwnerASC propio, así que le pasa su ASC acá. Se lo toma por
    // parámetro (en vez de mutar el campo OwnerASC del template compartido)
    // porque la corutina se extiende varios frames y dos jugadores podrían
    // correr la misma secuencia a la vez.
    public System.Collections.IEnumerator PlayVisualsSequence(AbilitySystemComponent owner)
    {
        if (owner == null) yield break;

        float mult = 1f;
        float spd  = owner.GetAttributeValue(EAttributeType.AtkSpeed);
        if (spd > 0) mult = 1f / spd;

        foreach (var v in VisualsSequence)
        {
            if (v.VFXPrefab == null) continue;

            if (v.Delay > 0)
                yield return new WaitForSeconds(v.Delay / mult);

            Vector3    pos = owner.transform.position + owner.transform.TransformDirection(v.Offset);
            Quaternion rot = owner.transform.rotation * Quaternion.Euler(v.RotationOffset);
            GameObject vfx = v.AttachToOwner
                ? Instantiate(v.VFXPrefab, pos, rot, owner.transform)
                : Instantiate(v.VFXPrefab, pos, rot);

            vfx.transform.localScale = (v.Scale != Vector3.zero) ? v.Scale : Vector3.one;

            if (v.EndWithTag != EGameplayTag.None)
                owner.StartAbilityCoroutine(DestroyVfxWhenTagRemoved(owner, vfx, v.EndWithTag));
            else if (v.DestroyTime > 0)
                Destroy(vfx, v.DestroyTime);
        }
    }

    // Destruye un VFX de la secuencia cuando el dueño pierde el tag
    // EndWithTag configurado (en vez de por un tiempo fijo).
    private System.Collections.IEnumerator DestroyVfxWhenTagRemoved(AbilitySystemComponent owner, GameObject vfx, EGameplayTag tag)
    {
        // Primero esperamos a que el tag APAREZCA. En el servidor/host el efecto
        // que lo otorga se aplica en el mismo frame, pero en un cliente remoto el
        // tag llega por NetTags (asíncrono, puede tardar varios frames). Sin esta
        // espera, el while de abajo veía "no tiene el tag" y destruía el VFX al
        // instante en los observadores (el aura del buff parpadeaba y desaparecía).
        const float tagWaitTimeout = 1f;
        float elapsed = 0f;
        while (elapsed < tagWaitTimeout && owner != null && vfx != null && !owner.HasTag(tag))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ahora sí: vive mientras el dueño conserve el tag.
        while (owner != null && owner.HasTag(tag) && vfx != null)
            yield return null;

        if (vfx != null) Destroy(vfx);
    }

    // Reproduce el VFX de impacto puntual de la habilidad (no la
    // secuencia automática de arriba). No hace nada por defecto — cada
    // subclase con un efecto de impacto (ImpactVFX, HitVFX...) la
    // sobreescribe. Se llama en cada peer por separado a través de
    // NetworkAbilitySystemComponent.ServerPlayAbilityVFX(), que resuelve
    // esta MISMA habilidad en la copia local de cada cliente — así no
    // hace falta sincronizar el GameObject del VFX por red.
    public virtual void PlayImpactVFX(Vector3 position) { }

    // Reproduce el VFX de impacto usando un dueño puntual. El peer OBSERVADOR
    // resuelve esta habilidad como el asset-template compartido (vía
    // GameplayAbilityRegistry), que no tiene OwnerASC propio; algunos overrides
    // (GA_SelfBuff, GA_ContinuousAoE) lo necesitan para parentar/posicionar el
    // VFX en el jugador. El swap es sincrónico —PlayImpactVFX instancia y
    // retorna en el mismo frame, y Unity es single-thread—, así que restaurar
    // OwnerASC al final deja el template intacto para cualquier otro jugador.
    public void PlayImpactVFXFor(AbilitySystemComponent owner, Vector3 position)
    {
        AbilitySystemComponent prev = OwnerASC;
        OwnerASC = owner;
        PlayImpactVFX(position);
        OwnerASC = prev;
    }

    // =========================================================
    // GIZMOS — vista previa del área real de la habilidad en el Editor
    // =========================================================

    // No hace nada por defecto. PlayerController.OnDrawGizmosSelected()
    // la llama para cada habilidad de la clase equipada (CurrentClassDef),
    // tanto en modo Play como fuera de él — así se puede ajustar
    // Range/AbilityRadius/etc. en el Inspector del asset y ver el área
    // real actualizarse en la Scene view sin tener que jugar para
    // probarlo. Cada habilidad concreta con área de golpe la sobreescribe
    // dibujando EXACTAMENTE los mismos valores que usa en su propio
    // Physics.Overlap...(), para que el gizmo nunca se desincronice de lo
    // que realmente golpea.
    public virtual void DrawGizmos(Transform origin) { }
}
