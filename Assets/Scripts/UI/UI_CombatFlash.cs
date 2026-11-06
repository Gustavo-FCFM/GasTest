using System.Collections.Generic;
using UnityEngine;

// ============================================================
// UI_CombatFlash
//
// El destello de "me están pegando": un contorno que aparece sobre el personaje que recibe algo
// y se encoge hasta desaparecer. Rojo si recibió daño, verde si lo curaron, celeste si el golpe
// lo frenó entero un escudo, y dorado, más largo y más grueso, al morir (sus números están en el
// asset CombatFeel). Es distinto del VFX de golpe de cada habilidad: ese lo pone el que ataca y
// sale aunque pegue en una pared; este solo sale sobre quien de verdad recibió algo.
//
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ DamageNumbers), desde el 4 de noviembre de 2026. Lo
// dispara el mismo aviso del número flotante (NetworkASC.ObserversCombatNumber), así que se ve
// en todas las pantallas sin tocar ningún GE, y un invisible no se delata.
//
// Cada destello crea su contorno y lo borra al terminar, en vez de guardarlo por personaje: el
// cuerpo cambia al cambiar de clase, y un contorno guardado quedaría con la malla vieja.
// ============================================================
public class UI_CombatFlash : MonoBehaviour
{
    private const string OutlineName = "Flash" + CharacterOutline.Suffix;

    [Header("Colores")]
    [Label("Daño")]
    public Color DamageColor = new Color(1f, 0.05f, 0.05f);

    [Tooltip("Un golpe crítico.")]
    [Label("Crítico")]
    public Color CriticalColor = new Color(1f, 0.55f, 0f);

    [Label("Curación")]
    public Color HealColor = new Color(0.1f, 1f, 0.2f);

    [Tooltip("Un golpe que frenó entero un escudo: no bajó la vida.")]
    [Label("Escudo")]
    public Color ShieldColor = new Color(0.1f, 0.7f, 1f);

    [Header("Forma")]
    [Tooltip("Cuánto dura desde que aparece hasta que se apaga.")]
    [Label("Duración (s)")]
    public float Seconds = 0.18f;

    [Tooltip("El grosor del contorno al aparecer. Se va encogiendo hasta 0.")]
    [Label("Grosor (m)")]
    public float Width = 0.04f;

    [Tooltip("Si también destella con cada tic de una quemadura o una herida.")]
    [Label("Destellar con el daño en el tiempo")]
    public bool FlashDamageOverTime = true;

    private class ActiveFlash
    {
        public readonly List<Renderer> Renderers = new List<Renderer>();
        public Color Color;
        public float StartedAt;
        public float Seconds;
        public float Width;
        public bool  Kill;   // el de morir no lo pisa el del último golpe, que llega por otro canal
    }

    private static UI_CombatFlash _instance;

    private readonly Dictionary<Component, ActiveFlash> _active = new Dictionary<Component, ActiveFlash>();
    private readonly List<Component> _finished = new List<Component>();

    private Material _material;
    private MaterialPropertyBlock _block;

    public static UI_CombatFlash Get()
    {
        if (_instance == null) _instance = FindFirstObjectByType<UI_CombatFlash>(FindObjectsInactive.Include);
        return _instance;
    }

    private void Awake()
    {
        _instance = this;
        _material = CharacterOutline.CreateMaterial(Width);
        _block    = new MaterialPropertyBlock();
    }

    private void OnDestroy()
    {
        foreach (ActiveFlash flash in _active.Values) Remove(flash);
        _active.Clear();
        if (_material != null) Destroy(_material);
    }

    // Un destello sobre ese personaje. Si ya tenía uno, lo reinicia con el color nuevo.
    public void Show(Component body, ECombatNumber kind, bool shielded)
    {
        if (body == null || _material == null) return;
        if (kind == ECombatNumber.DamageOverTime && !FlashDamageOverTime) return;

        ActiveFlash flash = FlashOn(body);
        if (flash == null || flash.Kill) return;

        flash.Color     = ColorFor(kind, shielded);
        flash.StartedAt = Time.time;
        flash.Seconds   = Seconds;
        flash.Width     = Width;
    }

    // El destello de morir, sobre quien acaba de caer.
    public void ShowKill(Component body)
    {
        if (body == null || _material == null || !CombatFeel.KillFlashOn) return;

        ActiveFlash flash = FlashOn(body);
        if (flash == null) return;

        CombatFeel feel = CombatFeel.Config;
        flash.Color     = feel.KillFlashColor;
        flash.StartedAt = Time.time;
        flash.Seconds   = feel.KillFlashSeconds;
        flash.Width     = feel.KillFlashWidth;
        flash.Kill      = true;
    }

    // El destello que ya tiene, o uno nuevo. Null si no tiene nada que contornear.
    private ActiveFlash FlashOn(Component body)
    {
        if (_active.TryGetValue(body, out ActiveFlash flash)) return flash;

        flash = new ActiveFlash();
        foreach (SkinnedMeshRenderer r in CharacterOutline.Create(body, _material, OutlineName)) flash.Renderers.Add(r);
        if (flash.Renderers.Count == 0) flash.Renderers.AddRange(CharacterOutline.CreateRigid(body, _material, OutlineName));
        if (flash.Renderers.Count == 0) return null;

        _active[body] = flash;
        return flash;
    }

    private Color ColorFor(ECombatNumber kind, bool shielded)
    {
        if (shielded) return ShieldColor;

        switch (kind)
        {
            case ECombatNumber.Heal:           return HealColor;
            case ECombatNumber.CriticalDamage: return CriticalColor;
            default:                           return DamageColor;
        }
    }

    private void Update()
    {
        if (_active.Count == 0) return;

        _finished.Clear();
        foreach (KeyValuePair<Component, ActiveFlash> entry in _active)
        {
            float t = entry.Value.Seconds > 0f ? (Time.time - entry.Value.StartedAt) / entry.Value.Seconds : 1f;
            if (entry.Key == null || t >= 1f)
            {
                Remove(entry.Value);
                _finished.Add(entry.Key);
                continue;
            }
            Paint(entry.Value, entry.Value.Width * (1f - t));
        }

        foreach (Component done in _finished) _active.Remove(done);
    }

    private void Paint(ActiveFlash flash, float width)
    {
        _block.SetColor("_Color", flash.Color);
        _block.SetFloat("_Width", width);

        foreach (Renderer r in flash.Renderers)
            if (r != null) r.SetPropertyBlock(_block);
    }

    private static void Remove(ActiveFlash flash)
    {
        foreach (Renderer r in flash.Renderers)
            if (r != null) Destroy(r.gameObject);
    }
}
