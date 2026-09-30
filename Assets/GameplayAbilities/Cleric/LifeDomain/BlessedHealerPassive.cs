using UnityEngine;

// ============================================================
// BlessedHealerPassive  (Médico bendecido — pasiva extra del Dominio de la vida)
//
// "Sus curaciones se potencian según la vida faltante del objetivo."
//
// Es un IHealModifier: se registra en el ASC del Clérigo y ExecuteInstantEffect lo
// corre sobre cada curación que él reparte (ver DamageModifier.cs). El bono crece en
// línea recta con la vida que le FALTA al curado, medida antes de curarlo:
//
//     curación × (1 + MaxBonus × vida faltante / vida máxima)
//
// Con MaxBonus 1: a un aliado lleno no le suma nada, a uno a media vida +50 %, y a
// uno casi muerto casi el doble. Vale también para la que se hace a sí mismo y para
// los ticks de una curación con el tiempo.
//
// SETUP: va en el PassiveBehaviorsPrefab de la subclase (junto con la Bendición, que
// la subclase tiene que volver a traer si usa su propio prefab).
// ============================================================
public class BlessedHealerPassive : MonoBehaviour, IHealModifier
{
    [Tooltip("Bono máximo, con el objetivo casi sin vida. 1 = hasta el doble de curación; " +
             "0.5 = hasta +50 %. Escala en línea recta con la vida que le falta.")]
    public float MaxBonus = 1f;

    private AbilitySystemComponent _asc;

    private void Awake() => _asc = GetComponentInParent<AbilitySystemComponent>();

    private void OnEnable()  { if (_asc != null) _asc.RegisterHealModifier(this); }
    private void OnDisable() { if (_asc != null) _asc.UnregisterHealModifier(this); }

    public void ModifyOutgoingHeal(ref HealContext ctx)
    {
        if (ctx.Target == null) return;

        float max = ctx.Target.GetAttributeValue(EAttributeType.MaxHealth);
        if (max <= 0f) return;

        float missing = Mathf.Clamp01(1f - ctx.Target.GetAttributeValue(EAttributeType.Health) / max);
        ctx.Magnitude *= 1f + MaxBonus * missing;
    }
}
