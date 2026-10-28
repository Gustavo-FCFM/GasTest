using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// ============================================================
// MercShowcaseBuildBat
//
// Cada build de Windows sale con un Showcase.bat al lado del .exe (28 de octubre de 2026, etapa
// 3 del showcase): abre tres ventanas del juego en esta PC, una por monitor.
//   · Ventana 1 (monitor 1): teclado y mouse (-kbm). Es la que suena.
//   · Ventanas 2 y 3 (monitores 2 y 3): una con cada control (-pad), sin sonido (-mute). Al
//     abrir piden "Aprieta A en tu control": clic en la ventana y A en el control que va a
//     jugar ahí (ver LocalSeat).
// Cada una escribe su propio log al lado (Showcase_1.log, …): con tres ventanas, el Player.log
// de siempre se lo pelean.
//
// No es de una vez: corre solo en cada build. Para otra combinación (dos ventanas, todas con
// control), se edita el .bat que salió, o el texto de abajo.
// ============================================================
public class MercShowcaseBuildBat : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        BuildTarget target = report.summary.platform;
        if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64) return;

        string exePath = report.summary.outputPath;
        string folder  = Path.GetDirectoryName(exePath);
        string exe     = Path.GetFileName(exePath);
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(exe)) return;

        string bat =
            "@echo off\r\n" +
            "rem Mercenaries - el showcase: tres ventanas del juego en esta PC, una por monitor.\r\n" +
            "rem   Ventana 1 (monitor 1): teclado y mouse. Es la que suena.\r\n" +
            "rem   Ventanas 2 y 3 (monitores 2 y 3): una con cada control, sin sonido. Al abrir piden\r\n" +
            "rem   \"Aprieta A en tu control\": clic en la ventana y A en el control que juega ahi.\r\n" +
            "rem Cada ventana deja su log al lado (Showcase_1.log, ...).\r\n" +
            "rem Lo arma cada build (MercShowcaseBuildBat). Para otra combinacion, editar este archivo.\r\n" +
            "cd /d \"%~dp0\"\r\n" +
            $"start \"\" \"{exe}\" -kbm -monitor 1 -logFile \"%~dp0Showcase_1.log\"\r\n" +
            "timeout /t 2 /nobreak >nul\r\n" +
            $"start \"\" \"{exe}\" -pad -mute -monitor 2 -logFile \"%~dp0Showcase_2.log\"\r\n" +
            "timeout /t 2 /nobreak >nul\r\n" +
            $"start \"\" \"{exe}\" -pad -mute -monitor 3 -logFile \"%~dp0Showcase_3.log\"\r\n";

        string path = Path.Combine(folder, "Showcase.bat");
        File.WriteAllText(path, bat, new System.Text.UTF8Encoding(false));
        Debug.Log($"[Showcase] Dejé {path}: abre tres ventanas (teclado y mouse + dos controles).");
    }
}
