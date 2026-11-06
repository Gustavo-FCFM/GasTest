using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_AbilityDenied
//
// Habilidad no disponible: si aprietas algo que no se puede usar (en cooldown, sin maná,
// desarmado), su ícono del HUD tiembla y parpadea en rojo. Antes no pasaba nada a la vista y
// parecía que el botón no había entrado.
//
// Va en el mismo objeto que UI_PlayerHUD (Player Camera ▸ UI) y usa sus íconos, sin piezas
// propias. Lo avisa PlayerController al apretar, por CombatFeel.AbilityRefused. Sus números, en
// el asset CombatFeel.
// ============================================================
[RequireComponent(typeof(UI_PlayerHUD))]
public class UI_AbilityDenied : MonoBehaviour
{
    private class Flash
    {
        public EAbilityInput Slot;
        public Image[]   Images;
        public Color[]   Colors;      // los de antes, para devolvérselos
        public Vector2[] Positions;
        public float     StartedAt;
    }

    private readonly List<Flash> _active = new List<Flash>();

    private UI_PlayerHUD _hud;

    private void Awake() => _hud = GetComponent<UI_PlayerHUD>();

    private void OnEnable() => CombatFeel.AbilityRefused += OnDenied;

    private void OnDisable()
    {
        CombatFeel.AbilityRefused -= OnDenied;

        foreach (Flash flash in _active) Restore(flash);
        _active.Clear();
    }

    private void OnDenied(EAbilityInput slot)
    {
        if (!CombatFeel.AbilityDeniedOn) return;

        // Ya temblando: vuelve a empezar, sin leer como "lo de antes" el rojo y el corrimiento.
        foreach (Flash flash in _active)
        {
            if (flash.Slot != slot) continue;
            flash.StartedAt = Time.time;
            return;
        }

        Image[] images = ImagesOf(slot);
        if (images == null) return;

        var started = new Flash
        {
            Slot      = slot,
            Images    = images,
            Colors    = new Color[images.Length],
            Positions = new Vector2[images.Length],
            StartedAt = Time.time,
        };
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null) continue;
            started.Colors[i]    = images[i].color;
            started.Positions[i] = images[i].rectTransform.anchoredPosition;
        }
        _active.Add(started);
    }

    private Image[] ImagesOf(EAbilityInput slot)
    {
        if (slot == EAbilityInput.Action3)
            return _hud.UltimateSlot != null ? new[] { _hud.UltimateSlot.iconBackground, _hud.UltimateSlot.iconFill } : null;

        UI_AbilitySlot ability = AbilitySlotOf(slot);
        return ability != null ? new[] { ability.iconImage, ability.cooldownOverlay } : null;
    }

    private UI_AbilitySlot AbilitySlotOf(EAbilityInput slot)
    {
        switch (slot)
        {
            case EAbilityInput.Movement:        return _hud.slotShift;
            case EAbilityInput.Action1:         return _hud.slotQ;
            case EAbilityInput.Action2:         return _hud.slotE;
            case EAbilityInput.PrimaryAttack:   return _hud.slotLMB;
            case EAbilityInput.SecondaryAttack: return _hud.slotRMB;
            default:                            return null;
        }
    }

    private void Update()
    {
        CombatFeel feel = CombatFeel.Config;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Flash flash = _active[i];
            float t = (Time.time - flash.StartedAt) / Mathf.Max(0.01f, feel.DeniedSeconds);
            if (t >= 1f)
            {
                Restore(flash);
                _active.RemoveAt(i);
                continue;
            }

            // Tiembla de lado a lado y se va calmando, mientras el rojo vuelve a su color.
            float x = Mathf.Sin(t * feel.DeniedShakes * Mathf.PI * 2f) * feel.DeniedShakePixels * (1f - t);

            for (int k = 0; k < flash.Images.Length; k++)
            {
                Image image = flash.Images[k];
                if (image == null) continue;

                Color c = Color.Lerp(feel.DeniedColor, flash.Colors[k], t);
                c.a = flash.Colors[k].a;
                image.color = c;
                image.rectTransform.anchoredPosition = flash.Positions[k] + new Vector2(x, 0f);
            }
        }
    }

    private static void Restore(Flash flash)
    {
        for (int i = 0; i < flash.Images.Length; i++)
        {
            if (flash.Images[i] == null) continue;
            flash.Images[i].color = flash.Colors[i];
            flash.Images[i].rectTransform.anchoredPosition = flash.Positions[i];
        }
    }
}
