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
public abstract class GameplayAbility : ScriptableObject, IChargedAbility
{
    // =========================================================
    // CONFIGURACIÓN GENERAL
    // =========================================================

    [Section(AbilitySection.General)]
    [Tooltip("El nombre en inglés (lo ve el jugador con el juego en inglés: la barra de casteo y la de carga).")]
    public string AbilityName = "New Ability";

    [Tooltip("El nombre en español. Vacío = sale el inglés. Se cargan todos de una con " +
             "Mercenarios ▸ Idiomas ▸ Importar nombres traducidos.")]
    public string AbilityNameEs;

    public Sprite AbilityIcon;

    // El nombre que ve el jugador, en su idioma.
    public string DisplayName => Loc.Pick(AbilityName, AbilityNameEs);

    [Section(AbilitySection.CostCooldown)]
    [Tooltip("Efecto instantáneo que se descuenta al activar (ej: -20 de maná).")]
    public GameplayEffect CostEffect;

    // Efecto CON duración que bloquea reactivar la habilidad mientras esté
    // activo (ver CanActivate). Lo IMPORTANTE del GE acá es su primer GrantedTag:
    // es la "identidad" del cooldown para la UI, la carga de ultimate y el
    // bloqueo por slot. La DURACIÓN normalmente la define CooldownDuration (abajo),
    // así podés REUSAR un mismo GE de cooldown para muchas habilidades (uno por
    // slot/tag) en vez de crear uno por cada una.
    [Tooltip("GE con duración que bloquea volver a usarla. Lo que importa es su PRIMER " +
             "GrantedTag (la identidad del cooldown); la duración normalmente sale de " +
             "CooldownDuration, así un mismo GE sirve para muchas habilidades.")]
    public GameplayEffect CooldownEffect;

    [Tooltip("Duración del cooldown en segundos, configurada acá en el GA. Si es > 0, " +
             "pisa el Duration del CooldownEffect (reusá un mismo GE de cooldown y ajustá " +
             "el tiempo por habilidad). 0 = usar el Duration del GE. Lo ignora " +
             "UseAttackSpeedAsCooldown (ese tiene prioridad).")]
    public float CooldownDuration = 0f;

    [Tooltip("Marcá esto en los ATAQUES BÁSICOS: el cooldown sale del stat AtkSpeed del dueño " +
             "(ignorando CooldownDuration y el Duration del CooldownEffect), o sea que el cooldown " +
             "ES el ritmo de ataque.\n\n" +
             "También controla la ANIMACIÓN: al ser un ritmo, el clip se estira/comprime para durar " +
             "exactamente ese tiempo y el swing entra justo entre golpe y golpe. Las habilidades " +
             "normales (cooldown = espera, no ritmo) reproducen su clip a velocidad natural.")]
    public bool UseAttackSpeedAsCooldown = false;

    // Cuánto adelanta el cooldown de la ultimate cada vez que esta
    // habilidad conecta un golpe (ver ChargeUltimate).
    [Tooltip("Segundos que le resta al cooldown de la DEFINITIVA cada golpe que conecta. 0 = nada.")]
    public float UltimateChargeAmount = 0f;

    // Si el dueño tiene cualquiera de estos tags, CanActivate() falla
    // (ej: no se puede atacar si está Silenciado).
    [Section(AbilitySection.Rules, startCollapsed: true)]
    [Tooltip("Si el lanzador tiene CUALQUIERA de estos tags, no la puede usar. Por tipo de acción: " +
             "State_Stunned en todas; State_Disarmed en las de arma; State_Silenced en las de " +
             "magia; State_Rooted en las de movimiento.")]
    public List<EGameplayTag> ActivationBlockedTags;

    [Tooltip("Al revés que ActivationBlockedTags: el dueño DEBE tener TODOS estos tags para " +
             "poder activarla (ej: 'Marcado para morir' del Asesino solo se lanza estando invisible). " +
             "Vacío = sin requisitos.")]
    public List<EGameplayTag> ActivationRequiredTags;

    [Tooltip("Esta habilidad SÍ se puede usar mientras el personaje está canalizando otra (el " +
             "molinete del bárbaro).\n\n" +
             "Un canalizado con BlockOtherAbilities bloquea TODO lo demás: la lista de lo " +
             "permitido se arma marcando esta casilla en las pocas que sí, no enumerando las " +
             "muchas que no. Así una habilidad nueva nace bloqueada por defecto, que es lo " +
             "seguro — si fuera al revés habría que acordarse de agregarla a una lista.")]
    public bool UsableWhileChanneling = false;

    [Tooltip("Esta habilidad SÍ se puede usar mientras se sostiene una habilidad de MANTENER " +
             "(el escudo). Lo normal es que el escudo ocupe las manos y bloquee todo; la Carga " +
             "defensiva del Guerrero es la excepción: carga con el escudo arriba.\n\n" +
             "El escudo SIGUE arriba durante y después de esta habilidad: se baja soltando el " +
             "botón, como siempre. Mientras se sostiene, esta habilidad no reproduce su " +
             "animación, para no romper la pose del escudo.")]
    public bool UsableWhileHolding = false;

    // Si se puede usar con un mantenido arriba. Virtual porque un GA_TagSwitch lo
    // resuelve según la variante que se dispararía (la Carga defensiva sí, la ofensiva no).
    public virtual bool CanUseWhileHolding => UsableWhileHolding;

    [Section(AbilitySection.Animation)]
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

    [Section(AbilitySection.Sound)]
    [Tooltip("Al LANZAR: suena en el personaje junto con la animación. Lo oyen todos los peers " +
             "por el mismo camino que la animación. Vacío = silencio.")]
    public SfxCue CastSound;

    [Tooltip("Al IMPACTAR: suena con los VFX de golpe e impacto (en cada objetivo alcanzado, o " +
             "donde cae la habilidad), en todos los peers. Vacío = silencio.")]
    public SfxCue ImpactSound;

    // =========================================================
    // EFECTOS Y VFX (listas con condiciones)
    //
    // Las DOS listas que reemplazan a los campos sueltos de cada GA (DamageEffect,
    // AdditionalEffects, TargetEffects, FirstHitEffects, HitVFX, ImpactVFX...). Viven en la
    // base porque las reglas son las mismas para todas: cada entrada dice a quién y cuándo.
    // Cada GA decide en qué momentos llama a ApplyHitEffects / BroadcastHitVFX /
    // BroadcastImpactVFX; "al activarse" y "al lanzar" los resuelve CommitAbility para todas.
    // =========================================================

    [Section(AbilitySection.Stacks, startCollapsed: true)]
    [Tooltip("Acumulaciones que esta habilidad LEE al activarse, contadas por este tag (cada " +
             "acumulación lo da una vez: las Artes marciales dan Status_MartialArts). Las entradas " +
             "de Efectos que escalan con acumulaciones usan cuántas había. None = no lee ninguna.")]
    public EGameplayTag StacksTag = EGameplayTag.None;

    [ShowIf(nameof(StacksTag), EGameplayTag.None, true)]
    [Tooltip("Si gasta las acumulaciones que leyó, y cuándo. 'Al terminar' conserva lo que dan " +
             "mientras dura la habilidad: el +1 de ataque por acumulación del Samurái, con el que " +
             "curan los Cortes devastadores.")]
    public EStackConsume ConsumeStacks = EStackConsume.Never;

    // Cuántas acumulaciones había al activarse (las lee CommitAbility). -1 = no leyó. Un
    // combo se la pasa a sus pasos, así un golpe del combo escala con lo que leyó el combo.
    [System.NonSerialized] public int StackSnapshot = -1;
    [System.NonSerialized] private bool _consumeStacksOnEnd;

    [Section(AbilitySection.Effects)]
    [Tooltip("Los GameplayEffect de la habilidad. Cada entrada dice CUÁNDO (al golpear, al " +
             "primer golpe, al activarse, al matar), A QUIÉN (enemigos, aliados, el lanzador, " +
             "todos) y, si querés, una CONDICIÓN (que el objetivo tenga un tag).\n\n" +
             "Se aplican en el orden de la lista. El primero 'al golpear, a enemigos' sin condición " +
             "es el DAÑO PRINCIPAL: el que mide una barrera al frenar un proyectil.")]
    public List<AbilityEffect> Effects = new List<AbilityEffect>();

    [Section(AbilitySection.Visuals)]
    [Tooltip("Los VFX de la habilidad. Cada entrada dice CUÁNDO aparece (al lanzar, al golpear, " +
             "en el impacto), dónde, de qué tamaño y cuánto dura. Se ven en todas las pantallas.")]
    [UnityEngine.Serialization.FormerlySerializedAs("VisualsSequence")]
    public List<AbilityVisual> Visuals = new List<AbilityVisual>();

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
        // Velocidad impuesta desde afuera: la fijan los combos en cada paso, porque el
        // ritmo lo conoce el PADRE (es él quien tiene UseAttackSpeedAsCooldown) y no el
        // paso suelto, que por sí solo devolvería velocidad natural. Ver
        // GA_ComboSequence/GA_AlternatingCombo.
        if (AnimationSpeedOverride > 0f) return AnimationSpeedOverride;

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
    // ---------------------------------------------------------
    // INTERRUPCIÓN DEL ATAQUE BÁSICO
    //
    // El ataque principal se puede cortar con otra habilidad (un pícaro a mitad del
    // swing que se escapa con el dash). Solo las instancias marcadas IsInterruptible (la
    // del slot PrimaryAttack, y los pasos de combo que clona) miran el contador de
    // cancelación del ASC: cuando otra habilidad activa lo incrementa
    // (AbilitySystemComponent.CancelInterruptibleAbilities), el timing en curso se corta
    // sin pegar y sin llamar EndAbility — el "fin" lo va a mandar la habilidad nueva.
    // El cooldown ya pagado NO se devuelve: cancelar tiene ese costo.
    //
    // SE PUEDE MARCAR EN EL ASSET. El slot PrimaryAttack lo recibe siempre (lo fuerza
    // EquipCharacterClass) y sus pasos de combo lo heredan; cualquier otra habilidad que
    // quiera poder cortarse lo pide acá. Los lanzamientos (hacha, dagas) lo usan para
    // que se pueda cancelar el tiro antes de soltar, o encadenar otra cosa apenas soltó
    // sin esperar el remate de la animación.
    // ---------------------------------------------------------
    [Section(AbilitySection.Rules)]
    [Tooltip("Otra habilidad puede CORTAR esta a mitad de camino. Lo que ya pegó (o ya " +
             "salió) queda; lo que faltaba no pasa, y el cooldown ya pagado NO se " +
             "devuelve. El ataque principal lo trae puesto de fábrica.")]
    public bool IsInterruptible;

    // True si el último HitTimingRoutine se cortó por una interrupción. Quien lo llame
    // tiene que mirarlo y salir sin EndAbility (ver GA_ConeAttack).
    [System.NonSerialized] protected bool WasCancelled;

    protected IEnumerator HitTimingRoutine(System.Action<HashSet<AbilitySystemComponent>> onHit)
    {
        WasCancelled = false;
        int cancelSerial = OwnerASC != null ? OwnerASC.CancelSerial : 0;

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

            if (IsInterruptible && OwnerASC != null && OwnerASC.CancelSerial != cancelSerial)
            {
                WasCancelled = true;
                yield break;
            }

            onHit?.Invoke(alreadyHit);
        }
    }

    // Capas de física que puede golpear esta habilidad. Es un FILTRO DE FÍSICA:
    // la detección (Physics.OverlapSphere/Box/Capsule) solo considera colliders
    // en estas capas. La afiliación amigo/enemigo se resuelve aparte en código
    // (IsEnemyOf), no acá — por eso normalmente esto apunta a la capa de
    // personajes (jugadores + NPCs) y el filtro de equipo lo hace la habilidad.
    // La geometría de cada ataque (radio/largo/ángulo) la define cada habilidad
    // concreta con sus propios campos.
    [Section(AbilitySection.Targeting)]
    [Tooltip("Capas de física que puede alcanzar (normalmente 'Character', la 7). Es solo el " +
             "filtro de física: quién es amigo o enemigo lo decide el código.")]
    public LayerMask TargetLayer;

    // ¿Esta habilidad busca personajes con TargetLayer? Las que no (un buff propio, un
    // teletransporte, los envoltorios que delegan en otra) lo apagan y el Inspector
    // esconde el campo: un campo que no hace nada invita a tocarlo y esperar un resultado.
    public virtual bool UsesTargetLayer => true;

    // ¿Esta habilidad GOLPEA a alguien (llama a ApplyHitEffects)? Las que no solo aceptan
    // efectos "al activarse": el Inspector marca en rojo una entrada "al golpear" en un
    // buff propio, que nunca se aplicaría.
    public virtual bool UsesHitEffects => true;

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

    // Velocidad de animación forzada desde afuera (0 = sin forzar, se calcula sola).
    // La usan los combos: el ritmo de ataque lo conoce el PADRE, así que se lo imponen
    // a cada paso — que por sí solo devolvería velocidad natural, porque no tiene
    // UseAttackSpeedAsCooldown ni cooldown propio. Ver ResolveAnimationSpeed.
    // NonSerialized: estado de runtime por instancia, igual que SourceTemplate.
    [System.NonSerialized] public float AnimationSpeedOverride;

    // Clip de un PASO de esta habilidad, si es un combo (ver GA_ComboSequence /
    // GA_AlternatingCombo). Devuelve null en cualquier otra habilidad.
    //
    // Existe para la RED: un paso puede traer un AnimationClipOverride que NO es el
    // clip del asset de la habilidad del paso, así que un observador no lo puede
    // deducir resolviendo esa habilidad en el registro. En vez de mandar el clip (no
    // se puede serializar), se mandan las COORDENADAS del paso y cada peer lo resuelve
    // contra su propia copia del asset del combo — que sí está en el registro por ser
    // la habilidad de nivel superior.
    public virtual AnimationClip GetStepAnimationClip(int sequenceIndex, int stepIndex) => null;

    // De qué habilidad hay que sacar la animación al activar ESTA. Por defecto, de
    // sí misma — que es el caso de todas menos las que DELEGAN en otra.
    //
    // Existe por las habilidades "envoltorio" (GA_TagSwitch: el ataque principal que
    // cambia según un tag). Ahí la animación correcta no es la del envoltorio —que ni
    // siquiera tiene clip— sino la de la variante que realmente se va a ejecutar. Sin
    // este gancho, el dueño predecía la animación del envoltorio (un trigger sin
    // estado asociado) y a los observadores les llegaba su índice de registro, así
    // que ninguno veía el ataque de verdad.
    //
    // Lo consultan PlayerController.ApplyAbilityAnimation (predicción del dueño) y
    // NetworkASC.ServerActivateAbility (réplica a observadores), o sea las DOS puntas.
    // Se puede resolver en el dueño porque los tags se sincronizan.
    public virtual GameplayAbility ResolveAnimationSource() => this;

    // Lo que el DUEÑO puede anticipar de los visuales, además de la animación: se llama
    // en el cliente dueño al apretar, junto con la predicción (ver
    // PlayerController.HandleAbilityInput). Por defecto no hace nada.
    //
    // Existe por el hacha: el servidor la esconde de la mano recién cuando su propia
    // cuenta llega al frame de soltar, y ese aviso vuelve al dueño DOS viajes después
    // de que él ya vio su animación lanzarla. En el host no se nota (viaje cero), pero
    // un cliente conectado veía el hacha todavía en la mano mientras el proyectil ya
    // salía: dos hachas al mismo tiempo.
    public virtual void PredictOwnerVisuals(PlayerController pc) { }

    // Lo mismo, pero al SOLTAR el botón de un mantenido: un lanzamiento apuntado
    // (GA_ProjectileShoot con AimBeforeThrow) muestra el lanzamiento en el acto y esconde
    // el arma a tiempo, sin esperar el viaje al servidor. Lo llama PlayerController.
    public virtual void PredictOwnerReleaseVisuals(PlayerController pc) { }

    // ¿Vale la pena que el dueño ANTICIPE la animación, antes de que el servidor
    // conteste? Por defecto sí. Las de objetivo único dicen que no cuando no hay nadie
    // a tiro: el servidor va a descartar la activación igual, y anticiparla hace que el
    // personaje haga el gesto en el vacío. Se resuelve en el dueño sin problema — la
    // búsqueda es física y tags, las dos cosas están de su lado.
    public virtual bool CanPredictActivation() => true;

    // True si esta habilidad se encarga ELLA de replicar su animación a los
    // observadores, y por lo tanto el servidor no tiene que mandar la suya encima.
    // La usa un combo: cada paso manda la propia (ver GA_ComboSequence). Sin esto, la
    // animación del combo padre llegaba justo después de la del primer paso y la
    // pisaba — en la pantalla de los demás el primer golpe no se veía, solo el segundo.
    public virtual bool BroadcastsOwnAnimation => false;

    // El ícono que el HUD tiene que mostrar AHORA. Casi siempre es AbilityIcon; un
    // GA_TagSwitch con ShowVariantIcon lo cambia según el tag (la postura del Guerrero).
    // UI_AbilitySlot lo revisa cada frame.
    public virtual Sprite CurrentIcon => AbilityIcon;

    // Si mientras se MANTIENE el botón la cámara del dueño se acerca a la mira (los
    // lanzamientos: mantener para apuntar, soltar para lanzar). Lo lee PlayerController
    // del mantenido en curso. Ver GA_ChargedAttack.AimCamera.
    public virtual bool AimsCameraWhileHeld => false;

    // Las etapas de una carga, para la barra de carga del dueño (UI_ChargeBar): en
    // stageTimes, los segundos en que empieza cada etapa; en maxTime, cuándo se suelta
    // sola. false = esta habilidad no tiene etapas (no se dibuja barra).
    public virtual bool GetChargeStages(List<float> stageTimes, out float maxTime)
    {
        maxTime = 0f;
        return false;
    }

    // Barra de canalizar del DUEÑO (UI_CastBar), estilo WoW. channel = se vacía mientras
    // dura (un canalizado, el molinete); si no, se llena hasta que la habilidad sale (una
    // carga, el Golpe final). Llamarlas en el servidor; HideCastBar(true) la pone roja.
    protected void ShowCastBar(float duration, bool channel)
    {
        NetworkAbilitySystemComponent netAsc = OwnerASC != null ? OwnerASC.GetComponent<NetworkAbilitySystemComponent>() : null;
        if (netAsc != null && netAsc.IsServerInitialized) netAsc.ServerShowCastBar(this, duration, channel);
    }

    protected void HideCastBar(bool interrupted)
    {
        NetworkAbilitySystemComponent netAsc = OwnerASC != null ? OwnerASC.GetComponent<NetworkAbilitySystemComponent>() : null;
        if (netAsc != null && netAsc.IsServerInitialized) netAsc.ServerHideCastBar(interrupted);
    }

    // Multiplicador del alcance de los golpes cuerpo a cuerpo (conos y líneas): 1 +
    // MeleeRangeBonus del dueño. El Avatar del Guardián lo sube al crecer.
    protected float MeleeReach
        => OwnerASC != null ? Mathf.Max(0.1f, 1f + OwnerASC.GetAttributeValue(EAttributeType.MeleeRangeBonus)) : 1f;

    // True si ESTA activación llegó a CommitAbility, o sea si de verdad se ejecutó.
    // Una habilidad que se planta sola —el Golpe mortal del Pícaro sin nadie a tiro—
    // sale de Activate() sin haber comprometido nada, y entonces tampoco tiene que
    // animarse: la reinicia ServerActivateAbility antes de cada Activate().
    [System.NonSerialized] public bool CommittedThisActivation;

    // True si ESTA activación arrancó de verdad aunque cobre MÁS TARDE. Un lanzamiento
    // (GA_ProjectileShoot) cobra al soltar el proyectil, no al empezar la animación: al
    // salir de Activate() todavía no comprometió nada, y sin esta marca el servidor
    // creía que se había plantado y no les mandaba la animación a los demás (el hacha
    // del Bárbaro y la daga del Pícaro volaban sin que se viera el brazo). La reinicia
    // ServerActivateAbility junto con la de arriba.
    [System.NonSerialized] public bool StartedThisActivation;

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

        // Canalizado en curso: solo pasan las marcadas como usables durante uno. El
        // molinete del bárbaro deja seguir usando Frenzy y el salto, y nada más.
        if (!UsableWhileChanneling && OwnerASC.HasTag(EGameplayTag.Status_Channeling)) return false;

        // Desarmado, silenciado y enraizado NO se resuelven acá: cada habilidad dice en su
        // ActivationBlockedTags qué la bloquea (1 de octubre, pedido de Gustavo):
        //   · State_Disarmed  → las acciones de ARMA (golpear con ella o lanzarla).
        //   · State_Silenced  → las de MAGIA o fantasía.
        //   · State_Rooted    → las de MOVIMIENTO.
        //   · State_Stunned   → todas.
        // Antes el desarme bloqueaba "el ataque básico" de cualquier clase desde acá, pero
        // eso no distinguía un tajo de un hechizo.

        if (ActivationRequiredTags != null)
            foreach (EGameplayTag tag in ActivationRequiredTags)
                if (!OwnerASC.HasTag(tag)) return false;

        if (CooldownEffect != null && CooldownEffect.GrantedTags.Count > 0)
            if (OwnerASC.HasTag(CooldownEffect.GrantedTags[0])) return false;

        if (CostEffect != null && !OwnerASC.CanAffordGameplayEffect(CostEffect)) return false;

        // Sin cargas disponibles no se puede activar. Red de seguridad más que gate
        // real: el bloqueo de cara al jugador lo hace el TAG del cooldown (que sí se
        // sincroniza al dueño), y ese tag solo se aplica al gastar la última carga —
        // ver CommitAbility. En un cliente el conteo arranca "lleno" porque el estado
        // es server-only, así que su predicción sigue siendo permisiva, igual que antes.
        if (UsesCharges && ChargesRemaining <= 0) return false;

        return true;
    }

    // Lógica concreta de la habilidad (detección, daño, movimiento...).
    // Cada subclase la implementa; debe empezar con "if (!IsServer) return;"
    // y llamar CommitAbility()/EndAbility() en los momentos correctos.
    public abstract void Activate();

    // ¿Esta habilidad mueve al personaje DESDE EL DUEÑO?
    //
    // El transform del jugador es client-authoritative, así que saltos, dashes y
    // teletransportes los ejecuta la conexión dueña: el servidor le manda un TargetRpc y
    // el impulso lo aplica el Update() del PlayerController de ese cliente.
    //
    // UN BOT NO TIENE DUEÑO. Si activa una de estas, FishNet avisa "Target is not an
    // observer", el bot paga el cooldown y NO SE MUEVE. Marcarlas acá deja que el
    // BotController simplemente no las proponga, en vez de tener que enumerar tipos.
    //
    // Marcala en true si tu habilidad llama a ServerStartLeap, ServerStartDash,
    // ServerTeleportOwnerTo, ApplyAbilityVelocity o ApplyDashVelocity.
    public virtual bool MovesThroughOwner => false;

    // Descuenta el costo, aplica el cooldown, y arranca la secuencia
    // visual automática si la habilidad tiene una configurada. Cada
    // Activate() concreto la llama una vez al confirmar que sí se va a
    // ejecutar.
    protected void CommitAbility()
    {
        if (OwnerASC == null) return;

        CommittedThisActivation = true;

        // Las acumulaciones, ANTES de los efectos "al activarse": esos ya pueden escalar.
        ReadStacks();

        // Gasta una carga y arranca la recarga. Con MaxCharges <= 1 no hace nada:
        // la habilidad se comporta como siempre (cooldown en cada uso).
        bool spentLastCharge = ConsumeCharge();

        if (CostEffect != null)
            OwnerASC.ApplyGameplayEffect(CostEffect, this);

        // Con cargas, el cooldown se aplica SOLO al gastar la última: mientras queden
        // cargas no debe haber tag, o el jugador no podría encadenarlas.
        if (CooldownEffect != null && spentLastCharge)
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

        // Los efectos "al activarse" (un buff propio al lanzar). Una habilidad que tiene que
        // aplicarlos en otro momento (la Ira inmortal, que primero revive) los pide ella.
        if (ApplyActivationEffectsOnCommit) ApplyActivationEffects();

        PlayCastVisuals();
    }

    // Los VFX "al lanzar" de la lista, en todas las pantallas. Lo hace CommitAbility; las
    // habilidades que no pasan por ahí (el escudo, que cobra a mano) lo llaman ellas.
    protected void PlayCastVisuals()
    {
        if (OwnerASC == null) return;

        if (HasVisuals(EVisualWhen.OnCast))
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

    // =========================================================
    // CARGAS  (varios usos antes de tener que esperar la recarga)
    //
    // Vive acá y no en cada habilidad porque el esquema es SIEMPRE el mismo y estaba
    // duplicado idéntico en el dash, la copia exacta y la intercepción heroica. Ahora
    // cualquier GA tiene cargas con solo subir MaxCharges — incluido un GA_SelfBuff
    // como el Castigo divino del Paladín, que era el caso que faltaba.
    //
    // CÓMO SE BLOQUEA (la parte no obvia): el estado de cargas es SERVER-ONLY, porque
    // Activate() es server-only y la instancia del dueño nunca lo tocaría. Si el gate
    // dependiera del contador, la predicción del dueño no coincidiría con el servidor.
    // Por eso el bloqueo de cara al jugador se hace con el TAG del cooldown, que sí se
    // sincroniza (NetTags): el CooldownEffect se aplica únicamente al gastar la ÚLTIMA
    // carga, así que mientras queden cargas no hay tag y el jugador puede encadenarlas.
    // Al recuperar una carga se limpia el tag a mano, para no esperar a que el GE
    // expire solo (si no, quedaría un desfase entre "tengo carga" y "puedo usarla").
    //
    // COOLDOWN = RECARGA POR CARGA: un solo valor (ResolveCooldownDuration) define
    // cuánto dura el cooldown Y cuánto tarda en volver cada carga.
    // =========================================================

    [Section(AbilitySection.CostCooldown)]
    [Tooltip("Usos disponibles antes de tener que esperar la recarga. 1 (o 0) = sin sistema de " +
             "cargas: cooldown normal en cada uso, como cualquier habilidad.\n\n" +
             "Con 2 o más, el cooldown se aplica solo al gastar la ÚLTIMA carga, y cada carga " +
             "tarda en volver lo que dure ese mismo cooldown.")]
    public int MaxCharges = 1;

    // IChargedAbility: la UI lo lee para mostrar el contador junto al ícono (solo si es > 1).
    public int MaxChargeCount => MaxCharges;

    // Cargas disponibles en el SERVIDOR. -1 = sin inicializar (se toma como lleno).
    // NonSerialized: estado de runtime por instancia otorgada, no se guarda en el asset.
    [System.NonSerialized] private int  _charges = -1;
    [System.NonSerialized] private bool _recharging;

    // True si esta habilidad usa el sistema de cargas.
    protected bool UsesCharges => MaxCharges > 1;

    // Cargas que quedan ahora mismo (inicializa perezosamente al máximo).
    protected int ChargesRemaining
    {
        get
        {
            if (_charges < 0) _charges = Mathf.Max(1, MaxCharges);
            return _charges;
        }
    }

    // Gasta una carga y arranca la recarga si hace falta. Devuelve true si con esto se
    // agotó la última (o si la habilidad no usa cargas), que es cuando corresponde
    // aplicar el cooldown. La llama CommitAbility.
    private bool ConsumeCharge()
    {
        if (!UsesCharges) return true;   // sin cargas: cooldown en cada uso, como siempre

        int remaining = ChargesRemaining;
        if (remaining > 0) _charges = remaining - 1;

        ReportCharges();
        StartRecharge();

        return _charges <= 0;
    }

    // Devuelve una carga (sin pasarse del máximo) y deja la habilidad usable ya mismo.
    // La usa el dash para su reembolso cuando un enemigo atravesado muere.
    protected void RefundCharge()
    {
        if (!UsesCharges) return;

        if (ChargesRemaining < MaxCharges) _charges = ChargesRemaining + 1;

        ClearCooldownTag();
        ReportCharges();
        StartRecharge();
    }

    // Baja a 0 el cooldown vigente de esta habilidad, para que vuelva a estar
    // disponible al instante en vez de esperar a que el GE expire.
    protected void ClearCooldownTag()
    {
        if (OwnerASC == null || CooldownEffect == null) return;
        if (CooldownEffect.GrantedTags == null || CooldownEffect.GrantedTags.Count == 0) return;

        OwnerASC.ReduceCooldownByTag(CooldownEffect.GrantedTags[0], 99999f);
    }

    // Publica las cargas para que la UI del dueño las muestre. Solo tiene sentido en
    // habilidades con cargas: si no, ensuciaría NetCharges con una entrada por cada
    // habilidad del juego.
    private void ReportCharges()
    {
        if (!UsesCharges || OwnerASC == null) return;

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) netAsc.ServerReportCharges(this, _charges);
    }

    private void StartRecharge()
    {
        if (!UsesCharges || _recharging || OwnerASC == null) return;
        OwnerASC.StartAbilityCoroutine(RechargeRoutine());
    }

    // Devuelve 1 carga cada "cooldown" hasta llenar MaxCharges.
    private System.Collections.IEnumerator RechargeRoutine()
    {
        _recharging = true;

        while (ChargesRemaining < MaxCharges)
        {
            float cd = ResolveCooldownDuration();
            if (cd <= 0f) cd = 1f; // salvaguarda si la habilidad no tiene cooldown configurado

            yield return new WaitForSeconds(cd);

            if (ChargesRemaining < MaxCharges)
            {
                _charges = ChargesRemaining + 1;
                ReportCharges();
                ClearCooldownTag();
            }
        }

        _recharging = false;
    }

    // Deja la habilidad como recién otorgada: cargas llenas y cooldown en cero. La
    // llama el ASC al revivir, para todas menos la definitiva (ver IsUltimate). La
    // recarga en curso, si la hay, se corta sola: RechargeRoutine ve las cargas llenas
    // en su próxima vuelta y termina.
    public void ResetForRespawn()
    {
        if (UsesCharges)
        {
            _charges = MaxCharges;
            ReportCharges();
        }
        ClearCooldownTag();
    }

    // True en la instancia que ocupa el slot de la DEFINITIVA (R). La marca
    // PlayerController al equipar; el respawn la usa para no resetearle el cooldown.
    [System.NonSerialized] public bool IsUltimate;

    // True si esta instancia es el ATAQUE BÁSICO (clic izquierdo) de su dueño. La marca
    // PlayerController al equipar, igual que IsUltimate. (Hasta el 1 de octubre la usaba
    // el desarme; ahora el desarme va por el ActivationBlockedTags de cada habilidad de
    // arma. Se deja la marca por si otra regla necesita saber cuál es el básico.)
    [System.NonSerialized] public bool IsBasicAttack;

    // Apaga el sistema de cargas en ESTA instancia. La usan los combos y el TagSwitch
    // al clonar un paso/variante: ese clon es solo la ejecución, su ciclo de vida
    // (costo, cooldown y por lo tanto también las cargas) es del padre. Sin esto, un
    // paso cuyo asset tuviera cargas las gastaría por su cuenta y terminaría
    // bloqueándose en silencio, igual que pasaba con el cooldown.
    public void DisableCharges()
    {
        MaxCharges = 1;
        _charges   = -1;
    }

    // Cierra la activación: libera el estado "atacando" del dueño y le
    // avisa al servidor que la habilidad terminó (para que replique el
    // fin del ataque al dueño remoto, si lo hay). Cada Activate() concreto
    // la llama al finalizar su secuencia (con o sin delay).
    public virtual void EndAbility()
    {
        // Las acumulaciones que se gastan "al terminar" (ver ReadStacks).
        if (_consumeStacksOnEnd)
        {
            _consumeStacksOnEnd = false;
            if (OwnerASC != null) OwnerASC.RemoveEffectsWithTag(StacksTag);
        }

        // Esto corre en el servidor (Activate() ya lo garantiza). Si el dueño
        // es un cliente remoto (no el host), pc.FinishAttack() de acá solo
        // resetea isAttacking en la copia del servidor — la copia real del
        // dueño nunca se entera y queda trabada en isAttacking = true para
        // siempre (no puede volver a atacar, ni girar al moverse).
        // Por eso también avisamos por red al dueño.
        PlayerController pc = OwnerASC?.GetComponent<PlayerController>();
        // Con CUÁL terminó: si el dueño está manteniendo el escudo, que termine otra
        // habilidad (el básico que el escudo cortó) no lo baja. Ver FinishAttack.
        //
        // Una variante de un GA_TagSwitch avisa con el nombre del SWITCH (ReportEndAs):
        // el dueño apretó el switch, no la variante. Sin esto, un mantenido dentro de un
        // switch (el clic derecho del Maestro de batalla) que el servidor corta por su
        // cuenta avisaba con un nombre que el dueño no reconocía, y quedaba trabado.
        GameplayAbility reported = ReportEndAs != null ? ReportEndAs : this;
        if (pc != null) pc.FinishAttack(reported);

        NetworkAbilitySystemComponent netASC = OwnerASC?.GetComponent<NetworkAbilitySystemComponent>();
        if (netASC != null) netASC.ServerNotifyAbilityEnded(reported);
    }

    // Con qué nombre avisa su fin (ver EndAbility). Lo pone GA_TagSwitch en sus variantes.
    [System.NonSerialized] public GameplayAbility ReportEndAs;

    // El cooldown que el HUD muestra para esta habilidad. Casi siempre es el propio; un
    // GA_HoldTagSwitch muestra el de la variante que se dispararía ahora.
    public virtual GameplayEffect CooldownEffectForDisplay => CooldownEffect;

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

    // La misma duración, para quien la necesita desde afuera: el PlayerController la
    // usa para poner la definitiva en un porcentaje exacto al cambiar de clase.
    public float CooldownDurationSeconds => ResolveCooldownDuration();

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
    // REPARTO POR AFILIACIÓN
    // =========================================================

    // Aplica a 'target' lo que corresponda según su AFILIACIÓN con el dueño, y
    // devuelve true si el objetivo era válido (o sea, si hubo que hacerle algo).
    //
    // Es el punto único que reparte "esto le pasa a los enemigos" vs "esto a los
    // aliados", para que cada habilidad no repita el if. El daño va aparte porque
    // cada habilidad lo dispara con su propio campo (DamageEffect) y necesita saber
    // si el golpe conectó (para el VFX de impacto y la carga de ultimate).
    //
    // POR QUÉ allyEffects VIENE POR PARÁMETRO Y NO ES UN CAMPO DE ACÁ: de las dos
    // docenas de habilidades del juego, solo un puñado le hace algo a los aliados que
    // alcanza. Con la lista en la base, TODAS mostraban en el inspector un campo que
    // nunca iban a usar. Ahora cada habilidad que sí lo necesita declara la suya (ver
    // TargetEffects en GA_ConeAttack, GA_LineAttack y GA_ProjectileShoot).
    //
    // Lista vacía o nula = los aliados se saltean por completo, que es el
    // comportamiento clásico de un ataque normal.
    protected bool ApplyAffiliationEffects(AbilitySystemComponent target, GameplayEffect enemyDamage,
                                           List<GameplayEffect> allyEffects)
    {
        if (target == null) return false;

        if (IsEnemy(target))
        {
            if (enemyDamage != null) target.ApplyGameplayEffect(enemyDamage, OwnerASC);
            return true;
        }

        // Aliado (incluido uno mismo): solo cuenta como objetivo si la habilidad
        // tiene efectos configurados para él.
        if (allyEffects != null && allyEffects.Count > 0 && IsAlly(target))
        {
            ApplyEffectsTo(allyEffects, target);
            return true;
        }

        return false;
    }

    // =========================================================
    // LA LISTA DE EFECTOS (Effects)
    // =========================================================

    // ¿Hay alguna entrada con efecto para este momento?
    public bool HasEffects(EEffectWhen when)
    {
        if (Effects == null) return false;
        foreach (AbilityEffect e in Effects)
            if (e.Effect != null && e.When == when) return true;
        return false;
    }

    // ¿Le hace algo a los ALIADOS que alcanza? Con eso un ataque empieza a tenerlos en
    // cuenta (la estela del Castigo divino, que daña enemigos y cura aliados a su paso).
    // Sin nada para ellos, los aliados se saltean: el comportamiento clásico.
    public bool HasAllyHitEffects
    {
        get
        {
            if (Effects == null) return false;
            foreach (AbilityEffect e in Effects)
                if (e.Effect != null && IsHitTiming(e.When) &&
                    (e.ApplyTo == EEffectTarget.Allies || e.ApplyTo == EEffectTarget.Everyone))
                    return true;
            return false;
        }
    }

    // El DAÑO PRINCIPAL: la primera entrada "al golpear, a enemigos" sin condición. Lo usan
    // la barrera (para medir cuánto frenó un proyectil) y el proyectil devuelto por un parry
    // (que lo reemplaza por el daño que traía).
    public GameplayEffect PrimaryDamageEffect
    {
        get
        {
            if (Effects == null) return null;
            foreach (AbilityEffect e in Effects)
                if (e.Effect != null && e.When == EEffectWhen.OnHit && e.ApplyTo == EEffectTarget.Enemies &&
                    e.OnlyIfTargetHas == EGameplayTag.None)
                    return e.Effect;
            return null;
        }
    }

    // ¿Algún efecto de la lista otorga este tag? Lo usan los bots para entender qué hace
    // una habilidad mirando lo que aplica, no su nombre (¿aturde? ¿me vuelve invisible?).
    public bool AnyEffectGrants(EGameplayTag tag, EEffectWhen? when = null, EEffectTarget? applyTo = null)
    {
        if (Effects == null) return false;
        foreach (AbilityEffect e in Effects)
        {
            if (e.Effect == null || e.Effect.GrantedTags == null) continue;
            if (when.HasValue && e.When != when.Value) continue;
            if (applyTo.HasValue && e.ApplyTo != applyTo.Value) continue;
            if (e.Effect.GrantedTags.Contains(tag)) return true;
        }
        return false;
    }

    // Los efectos de un momento y un destino, en orden (los bots, y quien necesite mirarlos).
    public void GetEffects(EEffectWhen when, EEffectTarget applyTo, List<GameplayEffect> into)
    {
        if (Effects == null || into == null) return;
        foreach (AbilityEffect e in Effects)
            if (e.Effect != null && e.When == when && e.ApplyTo == applyTo) into.Add(e.Effect);
    }

    private static bool IsHitTiming(EEffectWhen when)
        => when == EEffectWhen.OnHit || when == EEffectWhen.OnFirstHit;

    // =========================================================
    // ACUMULACIONES (sección Acumulaciones)
    // =========================================================

    // Al activarse (CommitAbility): cuántas acumulaciones hay, y si se gastan ya o al
    // terminar. Sin StacksTag no lee nada y conserva lo que le haya pasado un combo.
    private void ReadStacks()
    {
        _consumeStacksOnEnd = false;
        if (StacksTag == EGameplayTag.None || OwnerASC == null) return;

        StackSnapshot = OwnerASC.GetTagCount(StacksTag);
        if (StackSnapshot <= 0) return;

        if (ConsumeStacks == EStackConsume.OnActivate) OwnerASC.RemoveEffectsWithTag(StacksTag);
        else if (ConsumeStacks == EStackConsume.OnEnd) _consumeStacksOnEnd = true;
    }

    // Cuántas veces se aplica una entrada según las acumulaciones leídas, y con qué
    // duración (-1 = la del GE). La regla: lo que escala, sin acumulaciones no se aplica.
    private int StackApplications(AbilityEffect e, out float duration)
    {
        duration = -1f;
        int stacks = Mathf.Max(0, StackSnapshot);

        switch (e.StackScaling)
        {
            case EStackScaling.OnlyWithStacks:   return stacks > 0 ? 1 : 0;
            case EStackScaling.OncePerStack:     return stacks;
            case EStackScaling.DurationPerStack:
                if (stacks <= 0 || e.PerStack <= 0f) return 0;
                duration = e.PerStack * stacks;
                return 1;
            default:                             return 1;
        }
    }

    // Aplica una entrada a 'to' tantas veces como diga su escalado.
    private static void ApplyScaled(AbilitySystemComponent to, GameplayEffect effect, object source,
                                    int times, float duration)
    {
        for (int i = 0; i < times; i++) to.ApplyGameplayEffect(effect, source, duration);
    }

    // ¿Se cumple la condición de la entrada? 'subject' es el objetivo alcanzado (o el
    // lanzador, en los efectos al activarse).
    private static bool PassesCondition(AbilityEffect e, AbilitySystemComponent subject)
        => e.OnlyIfTargetHas == EGameplayTag.None || (subject != null && subject.HasTag(e.OnlyIfTargetHas));

    // Si CommitAbility aplica solo los efectos "al activarse". La Ira inmortal lo apaga: se
    // cobra estando muerta, y un buff aplicado antes de revivir se perdería al revivir.
    protected virtual bool ApplyActivationEffectsOnCommit => true;

    // Los efectos "al activarse": al lanzador. Una entrada "al activarse" para enemigos o
    // aliados no tiene a quién ir (todavía no golpeó a nadie): el Inspector la marca.
    protected void ApplyActivationEffects()
    {
        if (OwnerASC == null || Effects == null) return;

        foreach (AbilityEffect e in Effects)
        {
            if (e.Effect == null || e.When != EEffectWhen.OnActivate || e.ApplyTo != EEffectTarget.Self) continue;
            if (!PassesCondition(e, OwnerASC)) continue;

            int times = StackApplications(e, out float duration);
            ApplyScaled(OwnerASC, e.Effect, OwnerASC, times, duration);
        }
    }

    // Aplica a 'target' lo que le toca por haber sido ALCANZADO por esta habilidad, y
    // devuelve true si era un objetivo válido:
    //   · un enemigo, siempre;
    //   · un aliado (o uno mismo), solo si la lista tiene algo para los aliados.
    //
    // firstHit: es el primer objetivo de esta activación (lo decide cada habilidad, que
    // sabe en qué orden alcanza): recibe además las entradas "al primer golpe".
    //
    // Va en el orden de la lista, así el aturdido de primer golpe puede ir antes del daño
    // si así está cargado. Las entradas "al lanzador" se aplican una vez por cada ENEMIGO
    // golpeado; las "al matar", cuando el golpe deja muerto a un enemigo que estaba vivo.
    protected bool ApplyHitEffects(AbilitySystemComponent target, bool firstHit = false)
        => ApplyHitEffects(target, firstHit, OwnerASC);

    // La versión completa, para quien golpea "en nombre" de la habilidad (el proyectil):
    //   · source: quién figura como autor del golpe (el que lo devolvió, tras un parry).
    //   · primaryOverride: reemplaza al DAÑO PRINCIPAL (el daño que trae un proyectil
    //     devuelto, calculado con los stats de quien lo tiró).
    //   · onlyHostile: solo lo que va a los enemigos — nada para aliados, ni para el autor,
    //     ni de primer golpe (un proyectil devuelto no premia al que lo devolvió con los
    //     efectos de otra clase).
    public bool ApplyHitEffects(AbilitySystemComponent target, bool firstHit, AbilitySystemComponent source,
                                GameplayEffect primaryOverride = null, bool onlyHostile = false)
    {
        if (target == null || source == null) return false;

        bool enemy = source.IsEnemyOf(target);
        bool ally  = !enemy && !onlyHostile && source.IsAllyOf(target, includeSelf: true);

        if (!enemy && !(ally && HasAllyHitEffects)) return false;
        if (Effects == null) return true;

        bool wasAlive = !target.HasTag(EGameplayTag.State_Dead);
        GameplayEffect primary = primaryOverride != null ? PrimaryDamageEffect : null;
        bool primaryReplaced = false;

        foreach (AbilityEffect e in Effects)
        {
            if (e.Effect == null) continue;

            bool timing = e.When == EEffectWhen.OnHit ||
                          (e.When == EEffectWhen.OnFirstHit && firstHit && !onlyHostile);
            if (!timing) continue;

            // ¿Le toca a este objetivo? (el lanzador cobra lo suyo por cada ENEMIGO golpeado)
            bool reaches;
            switch (e.ApplyTo)
            {
                case EEffectTarget.Enemies:  reaches = enemy;                 break;
                case EEffectTarget.Allies:   reaches = ally;                  break;
                case EEffectTarget.Everyone: reaches = enemy || ally;         break;
                default:                     reaches = enemy && !onlyHostile; break;   // Self
            }
            if (!reaches || !PassesCondition(e, target)) continue;

            // Con las acumulaciones leídas: sin ellas, lo que escala no se aplica.
            int times = StackApplications(e, out float duration);
            if (times <= 0) continue;

            // El filtro del primer golpe va al final: puede ANOTAR algo (el proyectil anota
            // cuándo se puede volver a aturdir a ese enemigo), y no tiene que anotar nada que
            // después no se aplique.
            if (e.When == EEffectWhen.OnFirstHit && !CanApplyFirstHitEffect(target, e.Effect)) continue;

            GameplayEffect effect = e.Effect;
            if (!primaryReplaced && primary != null && effect == primary && e.When == EEffectWhen.OnHit &&
                e.ApplyTo == EEffectTarget.Enemies)
            {
                effect = primaryOverride;
                primaryReplaced = true;
            }

            if (e.ApplyTo == EEffectTarget.Self) ApplyScaled(source, effect, source, times, duration);
            else                                 ApplyScaled(target, effect, source, times, duration);
        }

        if (enemy && !onlyHostile && wasAlive && target.HasTag(EGameplayTag.State_Dead))
            ApplyKillEffects(target, source);

        return true;
    }

    // Las entradas "al matar": al lanzador (las demás no tienen sentido: el Inspector las
    // marca). 'victim' es el que murió, para la condición de tag.
    private void ApplyKillEffects(AbilitySystemComponent victim, AbilitySystemComponent source)
    {
        foreach (AbilityEffect e in Effects)
        {
            if (e.Effect == null || e.When != EEffectWhen.OnKill || e.ApplyTo != EEffectTarget.Self) continue;
            if (!PassesCondition(e, victim)) continue;

            int times = StackApplications(e, out float duration);
            ApplyScaled(source, e.Effect, source, times, duration);
        }
    }

    // Filtro extra para las entradas "al primer golpe". El proyectil lo usa para no
    // re-aturdir al mismo enemigo con cada disparo (FirstHitCooldownPerTarget).
    protected virtual bool CanApplyFirstHitEffect(AbilitySystemComponent target, GameplayEffect effect) => true;

    // ¿Qué momentos acepta esta habilidad? Para que el Inspector marque las entradas que
    // nunca se van a aplicar. "Al activarse" lo aceptan todas (lo hace CommitAbility).
    public virtual bool SupportsEffectTiming(EEffectWhen when)
        => when == EEffectWhen.OnActivate || UsesHitEffects;

    // ¿Qué VFX acepta? "Al lanzar" todas; "al golpear" las que golpean; "en el impacto" las
    // que tienen un punto donde caen.
    public virtual bool SupportsVisualTiming(EVisualWhen when)
        => when == EVisualWhen.OnCast || (when == EVisualWhen.OnHit && UsesHitEffects);

    // =========================================================
    // AFILIACIÓN — atajos hacia AbilitySystemComponent.IsEnemyOf/IsAllyOf
    // usando al dueño de esta habilidad como referencia
    // =========================================================

    // =========================================================
    // DIRECCIÓN DEL GOLPE (puntería vertical)
    // =========================================================

    // Altura desde la que se mide la inclinación. El pivote del personaje está en los
    // pies y la cámara a la altura de la cabeza: midiendo desde el piso, apuntar al
    // horizonte daría una inclinación hacia arriba que no existe.
    private const float AimOriginHeight = 1.4f;

    // Hacia dónde sale el golpe. Sin puntería vertical es el frente del cuerpo, plano —
    // el comportamiento de siempre.
    //
    // EL INTERRUPTOR NO VIVE ACÁ: lo declara cada habilidad que de verdad lo usa (hoy
    // GA_ConeAttack y GA_LineAttack) y se pasa por parámetro. Es la misma decisión que
    // se tomó con AllyEffects: un campo en la clase base aparece en el Inspector de las
    // 78 habilidades del juego, incluidas las zonas, los buffs y los dashes, donde no
    // hace absolutamente nada — y un campo que no hace nada es peor que no tenerlo,
    // porque invita a prenderlo y esperar un resultado.
    //
    // CÓMO SE COMBINAN LAS DOS FUENTES, y por qué:
    //   · El GIRO sale del cuerpo, que gira en vivo hacia la cámara. Por eso el defensor
    //     puede leer el swing y esquivarlo, y el atacante corregir sobre la marcha.
    //   · La INCLINACIÓN sale del punto de mira, que el dueño mandó AL ACTIVAR y queda
    //     congelado (el servidor no tiene la cámara del jugador).
    //
    // Usar el punto de mira entero para las dos cosas sería un retroceso: el ataque
    // saldría hacia donde apuntabas al apretar y dejaría de acompañar el giro, que es
    // justo lo que hoy funciona bien.
    protected Vector3 ResolveAttackDirection(bool useVerticalAim)
    {
        Vector3 bodyForward = OwnerASC != null ? OwnerASC.transform.forward : Vector3.forward;
        bodyForward.y = 0f;

        if (bodyForward.sqrMagnitude < 0.0001f) return Vector3.forward;
        bodyForward.Normalize();

        if (!useVerticalAim || OwnerASC == null) return bodyForward;

        // Los NPCs no tienen cámara: se quedan con el golpe plano.
        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc == null) return bodyForward;

        // Se lee NetworkAimPoint y no GetAimPoint() a propósito: en el host, GetAimPoint
        // haría un raycast de cámara EN VIVO y en los clientes remotos devolvería el
        // punto congelado. Serían dos reglas distintas según quién hostea. Este campo lo
        // escribe el servidor al activar, igual para todos.
        Vector3 aimPoint = pc.NetworkAimPoint;
        if (aimPoint == Vector3.zero) return bodyForward;   // nunca se envió

        Vector3 origin = OwnerASC.transform.position + Vector3.up * AimOriginHeight;
        Vector3 toAim  = aimPoint - origin;

        float horizontal = new Vector2(toAim.x, toAim.z).magnitude;
        if (horizontal < 0.01f) return bodyForward;         // apuntando a los pies

        // La inclinación de la mira, aplicada al frente del cuerpo.
        float pitch = Mathf.Atan2(toAim.y, horizontal);
        return bodyForward * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
    }

    protected bool IsEnemy(AbilitySystemComponent target)
        => OwnerASC != null && OwnerASC.IsEnemyOf(target);

    protected bool IsAlly(AbilitySystemComponent target, bool includeSelf = true)
        => OwnerASC != null && OwnerASC.IsAllyOf(target, includeSelf);

    // =========================================================
    // SELECCIÓN DE OBJETIVO APUNTADO
    // =========================================================

    // A quién busca FindBestTargetInAim.
    protected enum ETargetAffiliation { Enemies, Allies }

    // Devuelve el personaje MÁS CENTRADO en la mira del dueño, dentro de 'maxRange' y
    // de un cono de 'selectionAngle' grados. null si no hay ninguno válido.
    //
    // Vive acá porque es la misma búsqueda para todas las habilidades de objetivo
    // único, cambiando solo a quién apuntan: el Golpe mortal del Pícaro (enemigos, para
    // aparecer detrás), la Intercepción heroica del Paladín (aliados, para aparecer
    // delante) y las de seleccionar-y-aplicar (GA_Target). Estaba copiada casi
    // igual en cada una.
    //
    // En el servidor, GetAimPoint() usa el NetworkAimPoint que el dueño mandó junto con
    // el input (ver NetworkASC.ServerActivateAbility), así que la selección se resuelve
    // con la mira REAL del jugador y no con hacia dónde apunta su cuerpo.
    //
    // El filtro de física sale de TargetLayer; la afiliación se resuelve en código.
    protected AbilitySystemComponent FindBestTargetInAim(float maxRange, float selectionAngle,
                                                        ETargetAffiliation affiliation,
                                                        bool includeSelf = false,
                                                        bool allowDead   = false)
    {
        if (OwnerASC == null) return null;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        Vector3 origin = OwnerASC.transform.position;

        // EL RAYO DE LA RETÍCULA, no la línea desde los pies del personaje.
        //
        // Antes se medía el ángulo desde el PERSONAJE hasta cada candidato, contra la
        // dirección personaje→punto de mira. Pero la cámara está detrás y al hombro:
        // esas dos líneas no son la misma, y cuanto más cerca está el objetivo más se
        // abren. El resultado era que apuntar a un enemigo del fondo elegía al que
        // tenías pegado — "siempre el más cercano", aunque no estuviera en la retícula.
        //
        // Midiendo desde la CÁMARA a lo largo de su rayo, "el más centrado" quiere decir
        // lo que el jugador ve. El alcance se sigue midiendo desde el personaje (el
        // OverlapSphere de abajo), que es lo correcto para una habilidad.
        //
        // El origen viaja con el pedido de activación (PlayerController.NetworkAimOrigin),
        // así que el servidor resuelve con el mismo rayo que vio el dueño.
        Vector3 aimOrigin = pc != null ? pc.GetAimOrigin() : origin + Vector3.up * 1.6f;

        // El punto de mira se pide LEJOS a propósito. Con el alcance de la habilidad, un
        // rayo de 12 metros desde una cámara que ya está 5 detrás del personaje se queda
        // corto, y si no choca contra nada devuelve un punto casi encima del jugador:
        // la dirección salía de ahí y era pura ruleta.
        Vector3 aimPoint = pc != null ? pc.GetAimPoint(AimRayLength)
                                      : origin + OwnerASC.transform.forward * maxRange;

        Vector3 aimDir = aimPoint - aimOrigin;
        if (aimDir.sqrMagnitude < 0.0001f) aimDir = OwnerASC.transform.forward;
        aimDir.Normalize();

        Collider[] cols = Physics.OverlapSphere(origin, maxRange, TargetLayer);
        AbilitySystemComponent best = null;
        // Umbral de "está dentro del cono": comparar cosenos evita un Acos por candidato.
        float bestAlign = Mathf.Cos(selectionAngle * Mathf.Deg2Rad);

        foreach (var c in cols)
        {
            AbilitySystemComponent asc = c.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null) continue;
            if (!includeSelf && ReferenceEquals(asc, OwnerASC)) continue;
            if (!allowDead && asc.HasTag(EGameplayTag.State_Dead)) continue;

            bool valid = affiliation == ETargetAffiliation.Allies
                ? IsAlly(asc, includeSelf)
                : IsEnemy(asc);
            if (!valid) continue;

            // Se apunta al TORSO, no a los pies: el transform de un personaje está en el
            // suelo, y desde una cámara que mira un poco hacia abajo los pies de alguien
            // cercano quedan bastante más lejos de la retícula que su cuerpo.
            Vector3 toTarget = asc.transform.position + Vector3.up * TargetCenterHeight - aimOrigin;
            if (toTarget.sqrMagnitude < 0.0001f) continue;

            float align = Vector3.Dot(aimDir, toTarget.normalized);
            if (align > bestAlign) { bestAlign = align; best = asc; } // el más centrado
        }
        return best;
    }

    // Largo del rayo con el que se resuelve la DIRECCIÓN de la mira. No limita el
    // alcance de nada: solo sirve para que el punto quede lo bastante lejos como para
    // que la dirección sea estable.
    private const float AimRayLength = 200f;

    // A qué altura del transform está el centro de un personaje. Los modelos del
    // proyecto tienen el pivote en los pies y miden algo menos de dos metros.
    private const float TargetCenterHeight = 1f;

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

        if (Visuals == null) yield break;

        foreach (var v in Visuals)
        {
            // Solo las de "al lanzar": las de golpe e impacto las dispara la habilidad
            // cuando pasa eso (ver BroadcastHitVFX / BroadcastImpactVFX).
            if (v.When != EVisualWhen.OnCast || v.VFXPrefab == null) continue;

            if (v.Delay > 0)
                yield return new WaitForSeconds(v.Delay / mult);

            Vector3    pos = owner.transform.position + owner.transform.TransformDirection(v.Offset);
            Quaternion rot = owner.transform.rotation * Quaternion.Euler(v.RotationOffset);
            GameObject vfx = v.Attach
                ? Instantiate(v.VFXPrefab, pos, rot, owner.transform)
                : Instantiate(v.VFXPrefab, pos, rot);

            vfx.transform.localScale = (v.Scale != Vector3.zero) ? v.Scale : Vector3.one;

            if (v.EndWithTag != EGameplayTag.None || v.EndWhenAttributeDepleted)
                owner.StartAbilityCoroutine(DestroyVfxWhenExpired(owner, vfx, v.EndWithTag,
                                                                 v.EndWhenAttributeDepleted,
                                                                 v.DepletedAttribute));
            else if (v.DestroyTime > 0)
                Destroy(vfx, v.DestroyTime);
        }
    }

    // Mantiene vivo un VFX de la secuencia hasta que se cumpla su condición de fin: que
    // el dueño pierda un TAG, que se le agote un ATRIBUTO, o lo que pase primero si
    // están configuradas las dos.
    //
    // El caso que motivó lo del atributo es el escudo del Frenzy: el aura del buff y la
    // burbuja del escudo son dos entradas de la MISMA secuencia, pero el escudo se
    // consume con los golpes mientras el buff sigue corriendo. Atado solo al tag, la
    // burbuja quedaba puesta sin nada que absorber.
    private System.Collections.IEnumerator DestroyVfxWhenExpired(
        AbilitySystemComponent owner, GameObject vfx,
        EGameplayTag tag, bool watchAttribute, EAttributeType attribute)
    {
        bool watchTag = tag != EGameplayTag.None;

        // Primero esperamos a que la condición se CUMPLA. En el servidor/host el efecto
        // se aplica en el mismo frame, pero en un cliente remoto el tag llega por
        // NetTags y el atributo por su SyncVar — los dos asincrónicos. Sin esta espera,
        // el bucle de abajo vería "sin tag / escudo en cero" y destruiría el VFX al
        // instante en los observadores (el aura parpadeaba y desaparecía).
        const float startTimeout = 1f;
        float elapsed = 0f;
        while (elapsed < startTimeout && owner != null && vfx != null
               && !VisualShouldLive(owner, watchTag, tag, watchAttribute, attribute))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ahora sí: vive mientras se sostenga la condición.
        while (owner != null && vfx != null
               && VisualShouldLive(owner, watchTag, tag, watchAttribute, attribute))
            yield return null;

        if (vfx != null) Destroy(vfx);
    }

    // ¿Sigue en pie lo que mantiene vivo al VFX? Con las dos condiciones activas tienen
    // que cumplirse AMBAS: el VFX muere en cuanto falla la primera.
    private static bool VisualShouldLive(AbilitySystemComponent owner,
                                         bool watchTag, EGameplayTag tag,
                                         bool watchAttribute, EAttributeType attribute)
    {
        if (watchTag       && !owner.HasTag(tag))                       return false;
        if (watchAttribute && owner.GetAttributeValue(attribute) <= 0f) return false;
        return true;
    }

    // ¿Hay alguna entrada de VFX para este momento?
    public bool HasVisuals(EVisualWhen when)
    {
        if (Visuals == null) return false;
        foreach (AbilityVisual v in Visuals)
            if (v.VFXPrefab != null && v.When == when) return true;
        return false;
    }

    private bool HasImpactSound => ImpactSound != null && ImpactSound.Clips != null && ImpactSound.Clips.Length > 0;

    // ¿Tiene algo que mostrar o hacer sonar en el punto de impacto? (el proyectil no manda
    // nada por red si no).
    public bool HasImpactFeedback => HasVisuals(EVisualWhen.OnImpact) || HasImpactSound;

    // SERVIDOR: los VFX "al golpear" sobre 'target' (y el sonido de impacto), en todas las
    // pantallas. Se manda al personaje y no una posición: así un VFX pegado lo sigue.
    // withSound = false cuando el mismo golpe ya suena por su impacto (el proyectil).
    public void BroadcastHitVFX(AbilitySystemComponent target, bool withSound = true)
    {
        if (target == null) return;
        if (!HasVisuals(EVisualWhen.OnHit) && !(withSound && HasImpactSound)) return;

        NetworkAbilitySystemComponent netAsc = OwnerASC != null
            ? OwnerASC.GetComponent<NetworkAbilitySystemComponent>() : null;

        if (netAsc != null) netAsc.ServerPlayAbilityVFXOn(this, target, withSound);
        else                PlayImpactVFXOn(target);   // sin red (escena de pruebas suelta)
    }

    // SERVIDOR: los VFX "en el impacto" en un punto del mundo (y el sonido), en todas las
    // pantallas: donde estalla un área, donde aterriza un salto, donde choca un tiro.
    public void BroadcastImpactVFX(Vector3 point)
    {
        if (!HasVisuals(EVisualWhen.OnImpact) && !HasImpactSound) return;

        NetworkAbilitySystemComponent netAsc = OwnerASC != null
            ? OwnerASC.GetComponent<NetworkAbilitySystemComponent>() : null;

        if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, point);
        else                PlayImpactVFX(point);
    }

    // Los VFX "en el impacto" en un punto. Lo corre CADA peer con su propia copia de la
    // habilidad (NetworkAbilitySystemComponent.ServerPlayAbilityVFX resuelve esta MISMA
    // habilidad en cada cliente), así no hace falta sincronizar el GameObject del VFX.
    // Virtual por si una habilidad necesita algo que la lista no cubre.
    public virtual void PlayImpactVFX(Vector3 position)
        => SpawnVisuals(EVisualWhen.OnImpact, OwnerASC, null, position);

    // Los VFX "al golpear" sobre un PERSONAJE: lo que llega por
    // NetworkAbilitySystemComponent.ServerPlayAbilityVFXOn().
    public virtual void PlayImpactVFXOn(AbilitySystemComponent target)
    {
        if (target != null) SpawnVisuals(EVisualWhen.OnHit, OwnerASC, target, target.transform.position);
    }

    // Los mismos, con un dueño puntual. El peer OBSERVADOR resuelve esta habilidad como el
    // asset-template compartido (vía GameplayAbilityRegistry), que no tiene OwnerASC propio;
    // los VFX lo necesitan para parentarse al jugador o para esperar su Delay. El swap es
    // sincrónico —instancian y retornan en el mismo frame, y Unity es single-thread—, así
    // que restaurar OwnerASC al final deja el template intacto para cualquier otro jugador.
    public void PlayImpactVFXFor(AbilitySystemComponent owner, Vector3 position)
    {
        AbilitySystemComponent prev = OwnerASC;
        OwnerASC = owner;
        PlayImpactVFX(position);
        OwnerASC = prev;
    }

    public void PlayImpactVFXOnFor(AbilitySystemComponent owner, AbilitySystemComponent target)
    {
        AbilitySystemComponent prev = OwnerASC;
        OwnerASC = owner;
        PlayImpactVFXOn(target);
        OwnerASC = prev;
    }

    // Los VFX "al golpear" en una POSICIÓN: el respaldo para un objetivo sin NetworkObject
    // (no se lo puede mandar por red), que en las demás pantallas sale donde estaba.
    public void PlayHitVFXAtFor(AbilitySystemComponent owner, Vector3 position)
        => SpawnVisuals(EVisualWhen.OnHit, owner, null, position);

    // ---------------------------------------------------------
    // Lo que cada habilidad le aporta a sus VFX de impacto
    // ---------------------------------------------------------

    // Radio del área, para las entradas con "Calzar con el área". 0 = no tiene área.
    public virtual float VisualAreaRadius => 0f;

    // Cuánto dura un VFX de golpe o impacto con DestroyTime en 0: lo que dura la habilidad
    // (un área, su duración). Por defecto, 2 s.
    protected virtual float VisualDefaultLifetime => 2f;

    // A quién se pega un VFX de impacto con "Lo sigue": al lanzador, SOLO si el área lo
    // sigue (una zona que se mueve con él). null = queda fijo donde apareció.
    protected virtual Transform VisualImpactParent(AbilitySystemComponent owner) => null;

    // Instancia las entradas de un momento. target != null = sobre un personaje (al golpear).
    private void SpawnVisuals(EVisualWhen when, AbilitySystemComponent owner,
                              AbilitySystemComponent target, Vector3 point)
    {
        if (Visuals == null) return;

        foreach (AbilityVisual v in Visuals)
        {
            if (v.When != when || v.VFXPrefab == null) continue;

            // Con espera, la corutina corre en el ASC del dueño (el template del observador
            // no tiene dónde correrla). Sin dueño no hay espera: sale ya.
            if (v.Delay > 0f && owner != null)
                owner.StartAbilityCoroutine(SpawnVisualDelayed(v, owner, target, point));
            else
                SpawnVisual(v, owner, target, point);
        }
    }

    private IEnumerator SpawnVisualDelayed(AbilityVisual v, AbilitySystemComponent owner,
                                           AbilitySystemComponent target, Vector3 point)
    {
        yield return new WaitForSeconds(v.Delay);
        // El objetivo pudo desaparecer en la espera: sale donde estaba.
        if (target == null) SpawnVisual(v, owner, null, point);
        else                SpawnVisual(v, owner, target, target.transform.position);
    }

    private void SpawnVisual(AbilityVisual v, AbilitySystemComponent owner,
                             AbilitySystemComponent target, Vector3 point)
    {
        // Con la rotación del PREFAB, no cero: muchos VFX de los packs acuestan el círculo
        // girando su raíz −90° en X (Zone of Truth), y con Quaternion.identity quedaban parados.
        Quaternion rot = Quaternion.Euler(v.RotationOffset) * v.VFXPrefab.transform.rotation;
        GameObject vfx = Instantiate(v.VFXPrefab, point + v.Offset, rot);

        Transform parent = null;
        if (v.Attach)
            parent = target != null ? target.transform : (v.When == EVisualWhen.OnImpact ? VisualImpactParent(owner) : null);
        if (parent != null) vfx.transform.SetParent(parent, true);

        if (v.Scale != Vector3.zero) vfx.transform.localScale = v.Scale;

        float lifetime = v.DestroyTime > 0f ? v.DestroyTime : VisualDefaultLifetime;
        float radius   = VisualAreaRadius;

        // Con VFX_AreaVisual el círculo calza EXACTO con el radio y se desvanece al
        // terminar; si no, cae al multiplicador a ojo.
        if (v.MatchAreaSize && radius > 0f)
            VFX_AreaVisual.Configure(vfx, radius, v.AreaSizeMultiplier > 0f ? v.AreaSizeMultiplier : 1f, lifetime);
        else
            Destroy(vfx, lifetime);
    }

    // =========================================================
    // DATOS VIEJOS → FORMATO NUEVO
    //
    // Cada GA tenía sus propios campos sueltos para los efectos y los VFX. Esos campos
    // siguen existiendo (escondidos, con su nombre de siempre) SOLO para poder leer lo que
    // ya estaba cargado en los assets: al cargarse, cada habilidad los pasa a las listas
    // nuevas y los vacía. No hay que volver a cargar nada a mano.
    //
    // Pasa en memoria cada vez que se carga el asset (en el editor y en la build), así que
    // funciona aunque el asset nunca se vuelva a guardar. 'Mercenarios ▸ Guardar las
    // habilidades en el formato nuevo' los guarda en disco de una vez.
    // =========================================================

    // True si al cargarse pasó algo del formato viejo al nuevo y todavía no se guardó.
    [System.NonSerialized] public bool UpgradedOnLoad;

    protected virtual void OnEnable()
    {
        if (UpgradeLegacyData()) UpgradedOnLoad = true;
    }

    // Pasa los campos viejos a las listas. true = cambió algo.
    public bool UpgradeLegacyData()
    {
        if (Effects == null) Effects = new List<AbilityEffect>();
        if (Visuals == null) Visuals = new List<AbilityVisual>();

        bool changed = false;
        OnUpgradeLegacyData(ref changed);
        return changed;
    }

    // Cada GA con campos viejos los pasa acá (llamando primero a la base).
    protected virtual void OnUpgradeLegacyData(ref bool changed) { }

    // Ayudas para OnUpgradeLegacyData: agregan y vacían el campo viejo.
    protected void UpgradeEffect(ref GameplayEffect legacy, EEffectWhen when, EEffectTarget applyTo, ref bool changed)
    {
        if (legacy == null) return;
        Effects.Add(new AbilityEffect(legacy, when, applyTo));
        legacy = null;
        changed = true;
    }

    protected void UpgradeEffects(List<GameplayEffect> legacy, EEffectWhen when, EEffectTarget applyTo, ref bool changed)
    {
        if (legacy == null || legacy.Count == 0) return;
        foreach (GameplayEffect e in legacy)
            if (e != null) Effects.Add(new AbilityEffect(e, when, applyTo));
        legacy.Clear();
        changed = true;
    }

    protected void UpgradeVisual(ref GameObject legacy, AbilityVisual visual, ref bool changed)
    {
        if (legacy == null) return;
        visual.VFXPrefab = legacy;
        Visuals.Add(visual);
        legacy = null;
        changed = true;
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

    // El gizmo de las habilidades de OBJETIVO ÚNICO: el alcance (esfera) y el cono de
    // selección alrededor de la mira (selectionAngle es la MITAD: "hasta X° de la mira").
    // La mira sale de la cámara y no del cuerpo, así que es una aproximación: sirve para
    // ver la escala, no para medir al centímetro.
    protected static void DrawSelectionGizmo(Transform origin, float range, float selectionAngle, Color color)
    {
        if (origin == null) return;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(origin.position, range);

        Vector3 chest = origin.position + Vector3.up;
        Gizmos.color = new Color(color.r, color.g, color.b, color.a * 0.7f);
        for (int i = 0; i < 4; i++)
        {
            Quaternion around = Quaternion.AngleAxis(i * 90f, origin.forward);
            Vector3 edge = around * (Quaternion.AngleAxis(selectionAngle, origin.up) * origin.forward);
            Gizmos.DrawLine(chest, chest + edge * range);
        }
    }

#if UNITY_EDITOR
    // MANIJAS en la Scene view para ajustar los números de la forma arrastrando (alcance,
    // radio, ángulo, desde dónde sale). Las dibuja el muñeco de prueba (AbilityPreview)
    // con esta habilidad elegida; cada cambio tiene Deshacer y queda guardado en el asset.
    // Cada GA con forma la sobreescribe usando las ayudas de AbilityHandles.
    public virtual void DrawSceneHandles(Transform origin) { }
#endif
}
