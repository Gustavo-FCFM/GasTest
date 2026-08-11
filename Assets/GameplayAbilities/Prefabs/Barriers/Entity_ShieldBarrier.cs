using UnityEngine;
using System.Collections.Generic;

// ============================================================
// Entity_ShieldBarrier
//
// La BARRERA de una habilidad de bloqueo (el escudo del Paladín, y más adelante
// la postura del Guerrero o el escudo sobre un aliado de Juramento de la
// conquista). Es un volumen —el BoxCollider de este GameObject— que se enciende
// mientras su dueño tenga el tag de bloqueo, y que reduce el daño de todo golpe
// cuya TRAYECTORIA lo atraviese.
//
// POR QUÉ UN VOLUMEN Y NO UN "¿ESTOY MIRANDO AL ATACANTE?":
// La mayoría de los ataques del juego (conos, líneas, AoE, hitscan) NO son objetos
// físicos: resuelven daño llamando ApplyGameplayEffect directo sobre la víctima,
// así que nunca chocarían con un collider. Por eso el bloqueo se resuelve del lado
// del que RECIBE, con un raycast del atacante a la víctima contra este collider
// (ver Blocks). Eso da tres cosas gratis:
//   · la geometría de bloqueo es EXACTAMENTE la que se ve, sin ángulos a ojo;
//   · protege a los aliados que estén detrás de la barrera, no solo al dueño;
//   · el mismo componente sirve para un escudo puesto sobre OTRO jugador.
// Los proyectiles físicos (GC_Projectile) sí chocan de verdad con el collider y se
// paran ahí — ver el chequeo de barrera en ese script.
//
// SE ENCIENDE SOLO: no hace falta ninguna RPC. Mira el tag ActiveTag del dueño,
// que el GameplayEffect del bloqueo otorga y que ya viaja a todos los peers por
// NetTags. En el servidor el tag está al instante (autoridad), y en los clientes
// llega con la latencia normal — que para un visual es irrelevante.
//
// SETUP: este componente va en un HIJO del PassiveBehaviorsPrefab de la clase (que
// PlayerController instancia en TODOS los peers, así que la barrera existe en
// todos). Necesita un BoxCollider marcado como TRIGGER —para no empujar a nadie ni
// pelearse con el CharacterController— en una capa que colisione con la de los
// proyectiles en la matriz de física. El mesh visual va como hijo de este objeto.
// Ajustá el tamaño y el centro del BoxCollider: ESE es el escudo.
// ============================================================
[RequireComponent(typeof(BoxCollider))]
public class Entity_ShieldBarrier : MonoBehaviour, IIncomingDamageModifier
{
    [Header("Activación")]
    [Tooltip("Mientras el dueño tenga este tag, la barrera está levantada. Lo otorga el " +
             "GameplayEffect de la habilidad de bloqueo (ver GA_ShieldBlock.BlockEffect).")]
    public EGameplayTag ActiveTag = EGameplayTag.Status_Blocking;

    [Header("Bloqueo")]
    [Tooltip("Fracción del daño que la barrera frena. 1 = inmunidad total por ese lado, " +
             "0.7 = te llega el 30%. Se aplica ANTES de Resistencia/Defensa, así que las dos " +
             "cosas se acumulan.")]
    [Range(0f, 1f)]
    public float DamageReduction = 0.7f;

    [Tooltip("Energía que cuesta frenar 1 punto de daño. Con 1, un golpe de 40 frenado al 70% " +
             "gasta 28 de energía. Si no queda energía para frenar todo, frena solo la parte " +
             "que alcance y el resto pasa.")]
    public float EnergyPerDamageBlocked = 1f;

    [Tooltip("Altura (desde los pies) a la que se mide la trayectoria del golpe. Si midiéramos " +
             "de pie a pie, el segmento atacante→víctima pasaría por DEBAJO de la barrera y no " +
             "bloquearía nunca. ~1.2 = a la altura del pecho.")]
    public float AimHeight = 1.2f;

    [Tooltip("Energía que cuesta parar UN proyectil físico. Los proyectiles se resuelven por " +
             "colisión con este collider (no por el pipeline de daño), así que no sabemos cuánto " +
             "daño traían: se cobra un costo fijo por proyectil detenido.")]
    public float EnergyPerProjectileBlocked = 10f;

    [Header("Protección a Aliados")]
    [Tooltip("Si la barrera también protege a los aliados que queden detrás (lo que la convierte " +
             "en un escudo de equipo estilo Reinhardt). Desactivalo para un escudo puramente personal.")]
    public bool ProtectAllies = true;

    [Tooltip("Solo con Protect Allies: a qué distancia del dueño se buscan aliados a los que " +
             "cubrir. No define el bloqueo (eso lo decide la geometría de la barrera), solo a " +
             "quiénes se les consulta — poner un valor holgado.")]
    public float AllyScanRange = 12f;

    [Tooltip("Capa de los personajes, para buscar a los aliados a cubrir.")]
    public LayerMask CharacterLayer;

    [Tooltip("Cada cuánto se revisa qué aliados están cerca. No hace falta que sea rápido: un " +
             "aliado recién llegado igual queda cubierto en menos de un tick, y el bloqueo real " +
             "siempre se decide con la geometría al momento del golpe.")]
    public float AllyScanInterval = 0.25f;

    // Se dispara cada vez que la barrera frena daño, con cuánto frenó. Lo usa la
    // pasiva del Paladín para curar a su aura al bloquear ("cada vez que evite daño,
    // el jugador sanará a los aliados que estén en su aura").
    public event System.Action<float> OnDamageBlocked;

    private AbilitySystemComponent _ownerASC;
    private BoxCollider            _box;
    private Transform              _visual;

    // ASCs en los que este modificador está registrado ahora mismo (el dueño +
    // los aliados cubiertos). Hay que llevar la cuenta para poder darse de baja
    // exactamente de los mismos al bajar la barrera o al alejarse un aliado.
    private readonly List<AbilitySystemComponent> _registeredOn = new List<AbilitySystemComponent>();
    private readonly List<AbilitySystemComponent> _scanBuffer   = new List<AbilitySystemComponent>();

    private bool  _raised;
    private float _scanTimer;

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    private void Awake()
    {
        // El ASC está en el jugador: este componente vive en un nieto (hijo del
        // PassiveBehaviorsPrefab, que a su vez es hijo del jugador).
        _ownerASC = GetComponentInParent<AbilitySystemComponent>();
        _box      = GetComponent<BoxCollider>();

        if (_box != null && !_box.isTrigger)
            Debug.LogWarning($"[{name}] El BoxCollider de la barrera NO es trigger. Siendo sólido " +
                             $"empuja a los jugadores y pelea con el CharacterController del dueño. " +
                             $"Marcalo como Is Trigger.");

        // El mesh visual (si lo hay) se apaga solo, sin apagar este GameObject: si
        // desactiváramos el objeto entero, este script dejaría de correr y nunca
        // volvería a ver el tag para levantarse.
        _visual = transform.childCount > 0 ? transform.GetChild(0) : null;

        SetRaised(false);
    }

    private void OnDisable() => SetRaised(false);
    private void OnDestroy() => SetRaised(false);

    // Sigue el tag del dueño: levantar/bajar la barrera y, mientras está arriba,
    // refrescar cada tanto la lista de aliados cubiertos.
    private void Update()
    {
        if (_ownerASC == null) return;

        bool shouldBeRaised = _ownerASC.HasTag(ActiveTag);
        if (shouldBeRaised != _raised) SetRaised(shouldBeRaised);

        if (!_raised || !ProtectAllies) return;

        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0f) return;
        _scanTimer = Mathf.Max(0.05f, AllyScanInterval);
        RefreshCoveredAllies();
    }

    // =========================================================
    // LEVANTAR / BAJAR
    // =========================================================

    private void SetRaised(bool raised)
    {
        if (_raised == raised) return;
        _raised = raised;

        if (_box    != null) _box.enabled = raised;
        if (_visual != null) _visual.gameObject.SetActive(raised);

        if (raised)
        {
            Register(_ownerASC);   // el dueño siempre queda cubierto
            _scanTimer = 0f;       // que el primer barrido de aliados sea inmediato
        }
        else
        {
            UnregisterAll();
        }
    }

    // =========================================================
    // A QUIÉN CUBRE
    // =========================================================

    // Registra el modificador en los aliados cercanos y lo saca de los que ya no
    // están. Estar registrado NO significa estar protegido: solo significa "a este
    // se le va a consultar la barrera cuando reciba un golpe". Si el aliado no está
    // realmente detrás, el raycast de Blocks() da false y el golpe pasa entero.
    private void RefreshCoveredAllies()
    {
        _scanBuffer.Clear();
        _scanBuffer.Add(_ownerASC);

        Collider[] cols = Physics.OverlapSphere(_ownerASC.transform.position, AllyScanRange, CharacterLayer);
        foreach (var c in cols)
        {
            AbilitySystemComponent asc = c.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || _scanBuffer.Contains(asc)) continue;
            if (!_ownerASC.IsAllyOf(asc, includeSelf: true)) continue;
            _scanBuffer.Add(asc);
        }

        // Bajas: los que estaban registrados y ya no aparecen.
        for (int i = _registeredOn.Count - 1; i >= 0; i--)
        {
            AbilitySystemComponent asc = _registeredOn[i];
            if (asc != null && _scanBuffer.Contains(asc)) continue;

            asc?.UnregisterIncomingDamageModifier(this);
            _registeredOn.RemoveAt(i);
        }

        // Altas: los nuevos.
        foreach (var asc in _scanBuffer) Register(asc);
    }

    private void Register(AbilitySystemComponent asc)
    {
        if (asc == null || _registeredOn.Contains(asc)) return;
        asc.RegisterIncomingDamageModifier(this);
        _registeredOn.Add(asc);
    }

    private void UnregisterAll()
    {
        foreach (var asc in _registeredOn) asc?.UnregisterIncomingDamageModifier(this);
        _registeredOn.Clear();
    }

    // =========================================================
    // BLOQUEO (IIncomingDamageModifier)
    // =========================================================

    // Corre en el ASC del que RECIBE el golpe, antes de sus defensas. Ver
    // AbilitySystemComponent.ResolveIncomingDamage.
    public void ModifyIncomingDamage(ref IncomingDamageContext ctx)
    {
        if (!_raised || _ownerASC == null || _box == null) return;

        // Sin atacante no hay trayectoria que cortar (caídas, zonas de muerte).
        if (ctx.Source == null || ctx.Target == null) return;

        // El fuego amigo no se bloquea (y un escudo no te protege de vos mismo).
        if (ReferenceEquals(ctx.Source, _ownerASC) || _ownerASC.IsAllyOf(ctx.Source)) return;

        float incoming = -ctx.Magnitude;
        if (incoming <= 0f) return;

        // ¿La barrera se interpone de verdad entre el atacante y la víctima?
        if (!Blocks(ctx.Source.transform.position, ctx.Target.transform.position)) return;

        float energy = _ownerASC.GetAttributeValue(EAttributeType.Energy);
        if (energy <= 0f) return; // sin energía no frena nada (la habilidad ya está bajando el escudo)

        float blocked = incoming * DamageReduction;

        // Si no alcanza la energía para frenar todo, se frena solo lo que se pueda
        // pagar y el resto pasa. Así el escudo se "rompe" en el golpe grande que lo
        // agota, en vez de aguantarlo entero gratis.
        float cost = blocked * EnergyPerDamageBlocked;
        if (EnergyPerDamageBlocked > 0f && cost > energy)
        {
            blocked = energy / EnergyPerDamageBlocked;
            cost    = energy;
        }

        if (cost > 0f)
            _ownerASC.SetCurrentAttributeValue(EAttributeType.Energy, energy - cost);

        ctx.Magnitude  = -(incoming - blocked);
        ctx.WasBlocked = true;

        OnDamageBlocked?.Invoke(blocked);
    }

    // True si el segmento atacante → víctima atraviesa el volumen de la barrera.
    //
    // Se mide a la altura del pecho (AimHeight): a ras del piso el segmento pasaría
    // por debajo del escudo y no bloquearía nunca.
    //
    // Collider.Raycast devuelve false si el rayo ARRANCA dentro del collider, y eso
    // es justo lo que queremos: un enemigo que se te mete encima del escudo te pega
    // igual, como en cualquier hero shooter.
    private bool Blocks(Vector3 attackerPos, Vector3 victimPos)
    {
        Vector3 from = attackerPos + Vector3.up * AimHeight;
        Vector3 to   = victimPos   + Vector3.up * AimHeight;

        Vector3 delta = to - from;
        float   dist  = delta.magnitude;
        if (dist < 0.001f) return false;

        return _box.Raycast(new Ray(from, delta / dist), out _, dist);
    }

    // True si esta barrera está levantada. Lo consulta GC_Projectile al chocar.
    public bool IsRaised => _raised;

    // Le avisa a la barrera que paró un PROYECTIL físico (que se resuelve por
    // colisión, no por el pipeline de daño). Cobra EnergyPerProjectileBlocked y
    // dispara el mismo feedback que un bloqueo normal.
    public void NotifyProjectileBlocked()
    {
        if (!_raised || _ownerASC == null) return;

        if (EnergyPerProjectileBlocked > 0f)
        {
            float energy = _ownerASC.GetAttributeValue(EAttributeType.Energy);
            _ownerASC.SetCurrentAttributeValue(EAttributeType.Energy,
                                               Mathf.Max(0f, energy - EnergyPerProjectileBlocked));
        }

        OnDamageBlocked?.Invoke(EnergyPerProjectileBlocked);
    }

    // True si este golpe viene de un enemigo del dueño de la barrera. Lo usa
    // GC_Projectile para dejar pasar los proyectiles aliados.
    public bool IsHostile(AbilitySystemComponent shooter)
        => shooter != null && _ownerASC != null && _ownerASC.IsEnemyOf(shooter);

    // Vista previa del volumen real de bloqueo en el Editor, con los mismos
    // valores del BoxCollider que usa Blocks().
    private void OnDrawGizmosSelected()
    {
        BoxCollider box = _box != null ? _box : GetComponent<BoxCollider>();
        if (box == null) return;

        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix  = transform.localToWorldMatrix;

        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.25f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 1f);
        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = prev;
    }
}
