using UnityEngine;
using System.Collections.Generic;

// ============================================================
// PlayerVisibility
//
// Hace que un personaje con el tag Status_Invisible desaparezca SOLO para sus
// enemigos. Vos y tus aliados lo siguen viendo.
//
// CÓMO FUNCIONA EN RED: no hace falta nada especial. El tag lo aplica el
// servidor (vía un GameplayEffect, ej. Emboscada sombría del Asesino) y viaja a
// todos los clientes por NetTags, así que la copia local del invisible tiene el
// tag en TODAS las pantallas. Lo que cambia en cada pantalla es la AFILIACIÓN:
// cada cliente compara al invisible contra SU jugador local (PlayerController.
// LocalPlayer) y decide si esconderlo. Por eso el mismo personaje puede estar
// oculto en la pantalla del enemigo y visible en la del aliado, sin sincronizar
// nada extra.
//
// Solo apaga Renderers (mallas, armas, partículas). No toca colliders: las
// habilidades del servidor tienen que poder seguir golpeando a un invisible.
//
// PREFAB: agregá este componente al prefab del Player (junto al ASC).
// ============================================================
[RequireComponent(typeof(AbilitySystemComponent))]
public class PlayerVisibility : MonoBehaviour
{
    private AbilitySystemComponent _asc;

    // Los Renderers que apagamos NOSOTROS, para volver a prender exactamente esos
    // (y no uno que estaba apagado a propósito por otra razón).
    private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
    private bool _hidden;

    private void Awake()
    {
        _asc = GetComponent<AbilitySystemComponent>();
    }

    private void Update()
    {
        if (_asc == null) return;

        bool shouldHide = _asc.HasTag(EGameplayTag.Status_Invisible) && IsEnemyOfLocalPlayer();
        if (shouldHide == _hidden) return;

        if (shouldHide) Hide();
        else            Show();

        _hidden = shouldHide;
    }

    // Al destruirse (o despawnear) nos aseguramos de no dejar Renderers apagados.
    private void OnDisable()
    {
        if (_hidden) { Show(); _hidden = false; }
    }

    // ¿El personaje de esta pantalla es enemigo de este? Si no hay jugador local
    // (ej. un servidor sin cliente) o soy yo mismo, no se oculta nada.
    private bool IsEnemyOfLocalPlayer()
    {
        PlayerController local = PlayerController.LocalPlayer;
        if (local == null) return false;

        AbilitySystemComponent localASC = local.GetComponent<AbilitySystemComponent>();
        if (localASC == null || ReferenceEquals(localASC, _asc)) return false; // a mí siempre me veo

        return localASC.IsEnemyOf(_asc);
    }

    // Apaga los Renderers visibles ahora mismo y se los guarda. Se recalculan cada
    // vez (y no se cachean en Awake) porque las armas se instancian en runtime al
    // equipar la clase — ver PlayerController.UpdateVisuals.
    private void Hide()
    {
        _hiddenRenderers.Clear();

        foreach (Renderer r in GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled) continue;
            r.enabled = false;
            _hiddenRenderers.Add(r);
        }
    }

    // Vuelve a prender solo los que apagamos nosotros.
    private void Show()
    {
        foreach (Renderer r in _hiddenRenderers)
            if (r != null) r.enabled = true;

        _hiddenRenderers.Clear();
    }
}
