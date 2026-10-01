using UnityEngine;
using System.Collections.Generic;

// ============================================================
// WeakPointPassive  (Punto débil — pasiva extra del Maestro de batalla)
//
// "Al dañar a un enemigo lo marca un corto periodo; el marcado recibe daño aumentado."
//
// Escucha OnDealtDamage del dueño (lo dispara el pipeline de daño cuando su golpe le
// baja la vida a alguien) y le aplica MarkEffect al golpeado: un GE con Vulnerability y
// duración, con Refresh para que pegarle seguido lo mantenga marcado.
//
// Con Romper límites (mientras el dueño tenga EmpoweredWhileTag) aplica EmpoweredMark,
// la versión más fuerte. Las dos marcas comparten grupo con ReplacesGroup, así nunca se
// acumulan: la última reemplaza a la otra.
//
// Se aplica en el Update y no en el aviso mismo, por la misma razón que la Bendición:
// un golpe con el tiempo avisa DESDE adentro del recorrido de los efectos del golpeado,
// y agregarle un efecto ahí rompería esa iteración.
//
// Vive en el PassiveBehaviorsPrefab de la subclase. Solo actúa en el servidor.
// ============================================================
public class WeakPointPassive : MonoBehaviour
{
    [Tooltip("La marca normal (GE_WeakPoint): Vulnerability con duración.")]
    public GameplayEffect MarkEffect;

    [Tooltip("La marca mientras el dueño tiene EmpoweredWhileTag (GE_WeakPointEmpowered).")]
    public GameplayEffect EmpoweredMark;

    [Tooltip("El tag que potencia la marca (Status_BreakLimits, de Romper límites).")]
    public EGameplayTag EmpoweredWhileTag = EGameplayTag.Status_BreakLimits;

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;

    private readonly HashSet<AbilitySystemComponent> _pending = new HashSet<AbilitySystemComponent>();

    private bool IsServer => _netAsc == null || _netAsc.IsServerInitialized;

    private void Awake()
    {
        _asc    = GetComponentInParent<AbilitySystemComponent>();
        _netAsc = _asc != null ? _asc.GetComponent<NetworkAbilitySystemComponent>() : null;
    }

    private void OnEnable()  { if (_asc != null) _asc.OnDealtDamage += HandleDealtDamage; }
    private void OnDisable() { if (_asc != null) _asc.OnDealtDamage -= HandleDealtDamage; }

    private void HandleDealtDamage(AbilitySystemComponent victim, float damage)
    {
        if (!IsServer || victim == null || damage <= 0f || ReferenceEquals(victim, _asc)) return;
        _pending.Add(victim);
    }

    private void Update()
    {
        if (_pending.Count == 0) return;

        bool empowered = EmpoweredMark != null && EmpoweredWhileTag != EGameplayTag.None &&
                         _asc != null && _asc.HasTag(EmpoweredWhileTag);
        GameplayEffect mark = empowered ? EmpoweredMark : MarkEffect;

        if (mark != null)
            foreach (AbilitySystemComponent victim in _pending)
                if (victim != null && !victim.HasTag(EGameplayTag.State_Dead))
                    victim.ApplyGameplayEffect(mark, _asc);

        _pending.Clear();
    }
}
