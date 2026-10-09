using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// ============================================================
// Loc — los textos del juego en el idioma elegido
//
// Ningún texto que vea el jugador se escribe directo en el código: se pide por su CLAVE,
//     Loc.T("menu.play")                              → "Jugar" / "Play"
//     Loc.T("lobby.status.missing", ("missing", 2), ("players", 5))
// y la tabla dice qué es en cada idioma. La tabla es Resources/Localization.csv: una fila
// por texto, con las columnas key;es;en. Se abre con Excel o Google Sheets, así la puede
// completar cualquiera que ayude a traducir, sin tocar código ni Unity.
//
// EL CSV:
//   · Separado por punto y coma (lo que usa Excel en español). Con comas también se lee:
//     el separador se toma del encabezado.
//   · Se guarda en UTF-8 ("CSV UTF-8" en Excel); si no, se rompen las tildes y la ñ.
//   · Un texto con ; o con saltos de línea va entre comillas; una comilla adentro, doble ("").
//   · Las líneas vacías y las que empiezan con # se ignoran: sirven de títulos.
//   · Las variables van entre llaves con su NOMBRE: {players}. Quien traduce las puede mover
//     dentro de la frase, pero no las traduce.
//   · Se puede usar el texto enriquecido de TextMeshPro (<b>, <color=...>).
//   · El orden de las columnas no importa: se buscan por su nombre (es, en). Un idioma
//     nuevo es una columna más y un valor más en ELanguage.
//
// QUÉ IDIOMA: GameSettings.Language (el jugador lo elige en Ajustes; arranca en español).
// Al cambiarlo se dispara OnLanguageChanged: lo que esté en pantalla se vuelve a escribir
// solo si está atado con LocalizedText, o si su dueño escucha el evento.
//
// SI FALTA: una clave sin texto en el idioma elegido usa el otro idioma; una clave que no
// está en la tabla se muestra tal cual (y en el editor avisa una vez por la consola), así un
// texto olvidado se ve enseguida en pantalla.
//
// "MERCENARIES" no pasa por acá: es el nombre del juego, igual en los dos idiomas.
// ============================================================
public enum ELanguage
{
    // Se amplía SOLO al final: se guarda por número en PlayerPrefs.
    Spanish = 0,
    English = 1,
}

public static class Loc
{
    private const string TableResource = "Localization";   // Resources/Localization.csv

    public static ELanguage Language => GameSettings.Language;

    // Cambió el idioma: lo que esté en pantalla tiene que volver a escribirse.
    public static event Action OnLanguageChanged;

    // Los idiomas, en el orden en que los ofrece Ajustes.
    public static readonly ELanguage[] Languages = { ELanguage.Spanish, ELanguage.English };

    // El nombre de cada idioma EN SU PROPIO idioma: así lo encuentra quien no entiende el otro.
    public static string LanguageName(ELanguage language)
        => language == ELanguage.English ? "English" : "Español";

    // La columna de cada idioma en el CSV.
    private static string ColumnName(ELanguage language)
        => language == ELanguage.English ? "en" : "es";

    // =========================================================
    // TEXTOS
    // =========================================================

    // El texto de 'key' en el idioma elegido.
    public static string T(string key)
    {
        if (TryT(key, out string text)) return text;
        WarnMissing(key);
        return key;
    }

    // Igual, reemplazando las variables: Loc.T("lobby.team", ("team", 2)) → "Equipo 2".
    public static string T(string key, params (string name, object value)[] args)
    {
        string text = T(key);
        if (args == null) return text;

        foreach ((string name, object value) in args)
            text = text.Replace("{" + name + "}", value != null ? value.ToString() : "");
        return text;
    }

    // El CONTENIDO (nombres y descripciones de clases, habilidades y opciones de rueda) no
    // está en la tabla: cada asset trae su texto en inglés y, al lado, el de español
    // (ClassName / ClassNameEs, AbilityName / AbilityNameEs...). Así no se rompe al renombrar
    // el asset. Esto elige el del idioma actual; sin español cargado, sale el inglés.
    public static string Pick(string english, string spanish)
        => Language == ELanguage.Spanish && !string.IsNullOrEmpty(spanish) ? spanish : english;

    // Para lo que PUEDE tener traducción pero no siempre (los nombres de los niveles de
    // calidad, que salen del proyecto): si la clave no está, no avisa.
    public static string TOr(string key, string fallback)
        => TryT(key, out string text) ? text : fallback;

    public static bool TryT(string key, out string text)
    {
        text = null;
        if (string.IsNullOrEmpty(key)) { text = ""; return true; }

        EnsureLoaded();
        if (!_table.TryGetValue(key, out string[] texts)) return false;

        int wanted = (int)Language;
        if (wanted >= 0 && wanted < texts.Length && !string.IsNullOrEmpty(texts[wanted]))
        {
            text = texts[wanted];
            return true;
        }

        // Sin texto en ese idioma: el primero que haya.
        foreach (string other in texts)
            if (!string.IsNullOrEmpty(other)) { text = other; return true; }

        return false;
    }

    // Lo llama GameSettings cuando el jugador cambia el idioma.
    public static void NotifyLanguageChanged() => OnLanguageChanged?.Invoke();

    // =========================================================
    // LA TABLA
    // =========================================================

    // Clave → texto por idioma (índice = valor de ELanguage).
    private static Dictionary<string, string[]> _table;
    private static readonly HashSet<string> _warned = new HashSet<string>();

    // Cada Play vuelve a leer el CSV: si se editó con el editor abierto, se ve el cambio
    // sin reiniciar Unity (también con la recarga de dominio apagada).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForPlay()
    {
        _table = null;
        _warned.Clear();
        OnLanguageChanged = null;
    }

    private static void EnsureLoaded()
    {
        if (_table != null) return;
        _table = new Dictionary<string, string[]>();

        TextAsset csv = Resources.Load<TextAsset>(TableResource);
        if (csv == null)
        {
            Debug.LogWarning($"[Loc] No está Resources/{TableResource}.csv: todos los textos van a salir como su clave.");
            return;
        }

        Load(csv.text);
    }

    private static void Load(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (text[0] == '﻿') text = text.Substring(1);   // la marca de UTF-8 que deja Excel

        List<List<string>> rows = ParseCsv(text, DetectSeparator(text));

        // El encabezado: la primera fila que no es comentario. De ahí sale qué columna es
        // cada idioma.
        int header = -1;
        for (int r = 0; r < rows.Count && header < 0; r++)
            if (!IsSkipped(rows[r])) header = r;
        if (header < 0) return;

        int languageCount = Enum.GetValues(typeof(ELanguage)).Length;
        int[] columnOf = new int[languageCount];
        for (int l = 0; l < languageCount; l++)
            columnOf[l] = rows[header].FindIndex(c => c.Trim().Equals(ColumnName((ELanguage)l),
                                                                      StringComparison.OrdinalIgnoreCase));

        for (int r = header + 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (IsSkipped(row)) continue;

            string key = row[0].Trim();
            var texts = new string[languageCount];
            for (int l = 0; l < languageCount; l++)
            {
                int c = columnOf[l];
                texts[l] = c > 0 && c < row.Count ? row[c] : null;
            }

            if (_table.ContainsKey(key))
                Debug.LogWarning($"[Loc] La clave '{key}' está dos veces en {TableResource}.csv; vale la última.");
            _table[key] = texts;
        }
    }

    private static bool IsSkipped(List<string> row)
        => row.Count == 0 || string.IsNullOrWhiteSpace(row[0]) || row[0].TrimStart().StartsWith("#");

    // Punto y coma o coma: lo que aparezca en la primera línea con contenido.
    private static char DetectSeparator(string text)
    {
        foreach (string line in text.Split('\n'))
        {
            string l = line.Trim();
            if (l.Length == 0 || l.StartsWith("#")) continue;
            return l.Contains(";") ? ';' : ',';
        }
        return ';';
    }

    // CSV con comillas: un campo entre comillas puede tener el separador, saltos de línea y
    // comillas dobladas ("").
    // Pública para la herramienta que importa los nombres traducidos (mismo formato de CSV).
    public static List<List<string>> ParseCsv(string text, char separator)
    {
        var rows  = new List<List<string>>();
        var row   = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (quoted)
            {
                if (c != '"')               { field.Append(c); continue; }
                if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; continue; }
                quoted = false;
                continue;
            }

            if (c == '"' && field.Length == 0) { quoted = true; continue; }
            if (c == separator) { row.Add(field.ToString()); field.Clear(); continue; }
            if (c == '\r') continue;
            if (c == '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
                continue;
            }
            field.Append(c);
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }

    private static void WarnMissing(string key)
    {
        if (!Application.isEditor || !_warned.Add(key)) return;
        Debug.LogWarning($"[Loc] Falta la clave '{key}' en Resources/{TableResource}.csv.");
    }
}
