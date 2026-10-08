using System;
using TMPro;
using UnityEngine;

// ============================================================
// LocalizedText
//
// Ata un texto de TextMeshPro a una clave de Loc: lo escribe en el idioma elegido y lo
// vuelve a escribir solo cuando el jugador cambia de idioma. Dos formas de usarlo:
//
//   · Un texto FIJO de un prefab o una escena: agregarle este componente al objeto del
//     texto y poner la Key (la clave de Resources/Localization.csv).
//   · La UI que se arma por CÓDIGO (casi toda): atarlo al crearlo,
//         LocalizedText.Bind(texto, "menu.play");
//     o con una función, si lleva variables o un formato:
//         LocalizedText.Bind(texto, () => Loc.T("settings.section.camera").ToUpperInvariant());
//
// Lo que cambia solo (una línea de estado que se recalcula) no lo necesita: su dueño ya
// llama a Loc.T cada vez que la escribe, y escucha Loc.OnLanguageChanged para redibujar.
// ============================================================
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("La clave en Resources/Localization.csv (por ejemplo menu.play).")]
    public string Key;

    private TMP_Text     _text;
    private Func<string> _build;

    public static LocalizedText Bind(TMP_Text text, string key)
    {
        LocalizedText localized = Attach(text);
        localized.Key    = key;
        localized._build = null;
        localized.Refresh();
        return localized;
    }

    public static LocalizedText Bind(TMP_Text text, Func<string> build)
    {
        LocalizedText localized = Attach(text);
        localized._build = build;
        localized.Refresh();
        return localized;
    }

    private static LocalizedText Attach(TMP_Text text)
    {
        LocalizedText localized = text.GetComponent<LocalizedText>();
        if (localized == null) localized = text.gameObject.AddComponent<LocalizedText>();
        localized._text = text;
        return localized;
    }

    // Mientras está apagado no escucha: al prenderse se escribe de nuevo, así que tampoco
    // se pierde un cambio de idioma hecho con el menú cerrado.
    private void OnEnable()
    {
        Loc.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable() => Loc.OnLanguageChanged -= Refresh;

    public void Refresh()
    {
        if (_text == null) _text = GetComponent<TMP_Text>();
        if (_text == null) return;

        if (_build != null)                 _text.text = _build();
        else if (!string.IsNullOrEmpty(Key)) _text.text = Loc.T(Key);
    }
}
