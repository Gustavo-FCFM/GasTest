using System.Collections.Generic;
using UnityEngine;

// ============================================================
// DeadAllyHighlighter  (contorno de los aliados muertos — Dominio de la vida)
//
// Le dibuja un contorno al CUERPO de cada aliado muerto, solo en la pantalla del
// Clérigo de la Vida, para que vea a quién puede resucitar:
//
//   · VERDE: la Resurrección está lista y el cuerpo está a su alcance. Apretar la R
//     (mirándolo) lo revive.
//   · ROJO: hay un aliado caído, pero todavía no: la R está en cooldown, o él está lejos.
//
// El contorno es una copia de cada SkinnedMeshRenderer del cuerpo, con los MISMOS
// huesos y el shader Mercenaries/Outline (malla inflada, caras de atrás): sigue al
// ragdoll mientras cae, sin tocar los materiales del personaje. Se borra solo cuando el
// aliado revive o reaparece en la base.
//
// Es puramente visual y LOCAL: el prefab de pasivas existe en todos los peers, pero
// solo hace algo en el jugador de esta pantalla (en los demás y en los bots, nada).
//
// SETUP: va en el PassiveBehaviorsPrefab de la subclase (ClericLifeBehaviours).
// ============================================================
public class DeadAllyHighlighter : MonoBehaviour
{
    [Tooltip("Se puede resucitar ya: la R lista y el cuerpo a su alcance.")]
    public Color ReadyColor = new Color(0.3f, 1f, 0.45f);

    [Tooltip("Aliado caído, pero todavía no se puede: la R en cooldown o el cuerpo lejos.")]
    public Color NotReadyColor = new Color(1f, 0.25f, 0.2f);

    [Tooltip("Grosor del contorno, en metros.")]
    public float Width = 0.025f;

    [Tooltip("Latido del color: cuántas veces por segundo, y cuánto se oscurece (0 = fijo).")]
    public float PulseSpeed = 3f;
    [Range(0f, 1f)] public float PulseAmount = 0.35f;

    [Tooltip("Cada cuánto se buscan cuerpos nuevos y se recalcula verde/rojo.")]
    public float ScanInterval = 0.2f;

    private const string OutlineName = "DeadAllyOutline";

    private AbilitySystemComponent _asc;
    private PlayerController       _pc;
    private Material               _material;
    private MaterialPropertyBlock  _block;
    private float                  _nextScan;

    // Por cuerpo: sus renderers de contorno y si hoy se puede resucitar.
    private readonly Dictionary<PlayerController, List<SkinnedMeshRenderer>> _outlines =
        new Dictionary<PlayerController, List<SkinnedMeshRenderer>>();
    private readonly Dictionary<PlayerController, bool> _canRevive = new Dictionary<PlayerController, bool>();
    private readonly HashSet<PlayerController> _seen = new HashSet<PlayerController>();
    private readonly List<PlayerController>    _gone = new List<PlayerController>();

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        _asc   = GetComponentInParent<AbilitySystemComponent>();
        _pc    = GetComponentInParent<PlayerController>();
        _block = new MaterialPropertyBlock();
    }

    private void OnDisable() => ClearAll();

    private void OnDestroy()
    {
        ClearAll();
        if (_material != null) Destroy(_material);
    }

    private void Update()
    {
        // Solo en la pantalla del propio Clérigo.
        if (_pc == null || _asc == null || _pc != PlayerController.LocalPlayer)
        {
            if (_outlines.Count > 0) ClearAll();
            return;
        }

        if (Time.time >= _nextScan)
        {
            _nextScan = Time.time + ScanInterval;
            Scan();
        }

        if (_outlines.Count == 0) return;

        // 1 → (1 - PulseAmount) y vuelta: late sin llegar a apagarse.
        float pulse = 1f - PulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed * Mathf.PI * 2f));

        foreach (var pair in _outlines)
        {
            Color c = _canRevive.TryGetValue(pair.Key, out bool ready) && ready ? ReadyColor : NotReadyColor;
            _block.SetColor(ColorId, c * pulse);
            foreach (SkinnedMeshRenderer r in pair.Value)
                if (r != null) r.SetPropertyBlock(_block);
        }
    }

    // =========================================================
    // BUSCAR CUERPOS
    // =========================================================

    private void Scan()
    {
        GA_Resurrection resurrection = FindResurrection();
        bool  ready = resurrection != null && resurrection.IsReady;
        float range = resurrection != null ? resurrection.MaxRange : 0f;
        Vector3 me  = _pc.transform.position;

        _seen.Clear();
        foreach (PlayerController other in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (other == _pc) continue;

            AbilitySystemComponent asc = other.GetComponent<AbilitySystemComponent>();
            if (asc == null || !asc.HasTag(EGameplayTag.State_Dead) || !_asc.IsAllyOf(asc, includeSelf: false))
                continue;

            _seen.Add(other);
            if (!_outlines.ContainsKey(other)) _outlines[other] = CreateOutline(other);

            Vector3 body = GA_Resurrection.BodyPosition(other);
            _canRevive[other] = ready && (body - me).sqrMagnitude <= range * range;
        }

        // Los que revivieron, reaparecieron o se fueron de la partida.
        _gone.Clear();
        foreach (PlayerController key in _outlines.Keys)
            if (key == null || !_seen.Contains(key)) _gone.Add(key);
        foreach (PlayerController key in _gone) RemoveOutline(key);
    }

    private GA_Resurrection FindResurrection()
    {
        foreach (GameplayAbility ability in _asc.GrantedAbilities)
            if (ability is GA_Resurrection r) return r;
        return null;
    }

    // =========================================================
    // CONTORNOS
    // =========================================================

    private List<SkinnedMeshRenderer> CreateOutline(PlayerController body)
        => CharacterOutline.Create(body, GetMaterial(), OutlineName);

    private void RemoveOutline(PlayerController key)
    {
        if (_outlines.TryGetValue(key, out List<SkinnedMeshRenderer> renderers))
            foreach (SkinnedMeshRenderer r in renderers)
                if (r != null) Destroy(r.gameObject);

        _outlines.Remove(key);
        _canRevive.Remove(key);
    }

    private void ClearAll()
    {
        foreach (List<SkinnedMeshRenderer> renderers in _outlines.Values)
            foreach (SkinnedMeshRenderer r in renderers)
                if (r != null) Destroy(r.gameObject);

        _outlines.Clear();
        _canRevive.Clear();
    }

    private Material GetMaterial()
    {
        if (_material != null) return _material;

        _material = CharacterOutline.CreateMaterial(Width);
        if (_material == null) enabled = false;   // sin el shader no hay contorno (ya avisó)
        return _material;
    }
}
