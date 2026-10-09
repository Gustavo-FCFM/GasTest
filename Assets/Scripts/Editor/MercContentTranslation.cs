using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================
// MercContentTranslation — los nombres y descripciones en español, de una sola vez
//
// El contenido (clases, habilidades, opciones de la rueda) no va en Localization.csv: cada
// asset trae su texto en inglés y, al lado, el de español (ClassName / ClassNameEs,
// AbilityName / AbilityNameEs, el Name / NameEs de cada tótem...). Ver Loc.Pick.
//
// Cargarlos a mano asset por asset serían 200 y pico de inspectores. Estas dos opciones
// los pasan por un CSV que se abre con Excel:
//
//   · Exportar: escribe DesignDocuments/Traduccion_Contenido.csv con TODOS los textos, el
//     inglés y el español que ya tengan. Sirve para ver qué falta o mandárselo a quien
//     traduzca.
//   · Importar: lee ese CSV y escribe la columna "es" en cada asset.
//
// Cada fila dice el GUID del asset (no su ruta ni su nombre: Gustavo renombra assets, y
// el GUID no cambia) y el campo en español que llena. La columna "asset" y la "en" son solo
// para que se lea; al importar no se usan. Una celda "es" VACÍA no toca el asset.
//
// El CSV va con punto y coma y en UTF-8, como Localization.csv.
// ============================================================
public static class MercContentTranslation
{
    private const string CsvPath = "DesignDocuments/Traduccion_Contenido.csv";   // desde la raíz del proyecto

    // Un texto traducible: el campo en inglés y su par en español, dentro de un asset.
    private struct Entry
    {
        public string Guid, Asset, EnglishPath, SpanishPath;
    }

    [MenuItem("Mercenarios/Idiomas/Exportar nombres para traducir", false, 20)]
    public static void Export()
    {
        var sb = new StringBuilder();
        sb.Append("guid;asset;field;en;es\n");

        int count = 0;
        foreach (Entry e in CollectEntries())
        {
            Object asset = LoadByGuid(e.Guid);
            var so = new SerializedObject(asset);
            SerializedProperty en = so.FindProperty(e.EnglishPath);
            SerializedProperty es = so.FindProperty(e.SpanishPath);
            if (en == null || es == null) continue;

            sb.Append(e.Guid).Append(';')
              .Append(Quote(e.Asset)).Append(';')
              .Append(Quote(e.SpanishPath)).Append(';')
              .Append(Quote(en.stringValue)).Append(';')
              .Append(Quote(es.stringValue)).Append('\n');
            count++;
        }

        // Con la marca de UTF-8: así Excel lo abre con las tildes bien.
        File.WriteAllText(CsvPath, sb.ToString(), new UTF8Encoding(true));
        Debug.Log($"[Idiomas] {count} textos exportados a {CsvPath}.");
        EditorUtility.RevealInFinder(CsvPath);
    }

    [MenuItem("Mercenarios/Idiomas/Importar nombres traducidos", false, 21)]
    public static void Import()
    {
        if (!File.Exists(CsvPath))
        {
            EditorUtility.DisplayDialog("Importar nombres traducidos",
                $"No está {CsvPath}. Exportalo primero (Mercenarios ▸ Idiomas ▸ Exportar).", "OK");
            return;
        }

        string text = File.ReadAllText(CsvPath, Encoding.UTF8);
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
        string firstLine = text.Split('\n')[0];
        List<List<string>> rows = Loc.ParseCsv(text, firstLine.Contains(";") ? ';' : ',');
        if (rows.Count == 0) return;

        List<string> header = rows[0];
        int cGuid  = header.FindIndex(h => h.Trim() == "guid");
        int cField = header.FindIndex(h => h.Trim() == "field");
        int cEs    = header.FindIndex(h => h.Trim() == "es");
        if (cGuid < 0 || cField < 0 || cEs < 0)
        {
            Debug.LogError($"[Idiomas] {CsvPath} no tiene las columnas guid, field y es.");
            return;
        }

        int changed = 0, same = 0, missing = 0;
        var touched = new HashSet<Object>();

        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.Count <= Mathf.Max(cGuid, cField, cEs)) continue;

            string guid = row[cGuid].Trim(), field = row[cField].Trim(), es = row[cEs].Trim();
            if (guid.Length == 0 || es.Length == 0) continue;

            Object asset = LoadByGuid(guid);
            SerializedObject so = asset != null ? new SerializedObject(asset) : null;
            SerializedProperty prop = so?.FindProperty(field);
            if (prop == null || prop.propertyType != SerializedPropertyType.String)
            {
                missing++;
                Debug.LogWarning($"[Idiomas] Fila {r + 1}: no encontré '{field}' en el asset {guid} " +
                                 $"({AssetDatabase.GUIDToAssetPath(guid)}).");
                continue;
            }

            if (prop.stringValue == es) { same++; continue; }

            prop.stringValue = es;
            so.ApplyModifiedProperties();
            touched.Add(asset);
            changed++;
        }

        foreach (Object asset in touched) EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Idiomas] Importados: {changed} textos cambiados en {touched.Count} assets " +
                  $"({same} ya estaban igual, {missing} filas sin destino).");
    }

    // Todos los textos traducibles del proyecto: clases, habilidades y opciones de rueda.
    private static IEnumerable<Entry> CollectEntries()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterClassDefinition"))
        {
            string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            yield return new Entry { Guid = guid, Asset = name, EnglishPath = "ClassName",   SpanishPath = "ClassNameEs" };
            yield return new Entry { Guid = guid, Asset = name, EnglishPath = "Description", SpanishPath = "DescriptionEs" };
        }

        foreach (string guid in AssetDatabase.FindAssets("t:GameplayAbility"))
        {
            string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            yield return new Entry { Guid = guid, Asset = name, EnglishPath = "AbilityName", SpanishPath = "AbilityNameEs" };

            // Las opciones de una rueda (los tótems del Chamán).
            Object asset = LoadByGuid(guid);
            if (asset == null) continue;
            SerializedProperty totems = new SerializedObject(asset).FindProperty("Totems");
            if (totems == null || !totems.isArray) continue;

            for (int i = 0; i < totems.arraySize; i++)
            {
                string item = $"Totems.Array.data[{i}].";
                yield return new Entry { Guid = guid, Asset = name, EnglishPath = item + "Name",        SpanishPath = item + "NameEs" };
                yield return new Entry { Guid = guid, Asset = name, EnglishPath = item + "Description", SpanishPath = item + "DescriptionEs" };
            }
        }
    }

    private static Object LoadByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
    }

    // Entre comillas si lleva punto y coma, comillas o saltos de línea (como pide el CSV).
    private static string Quote(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
