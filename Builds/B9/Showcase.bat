@echo off
rem Mercenaries - el showcase: tres ventanas del juego en esta PC, una por monitor.
rem   Ventana 1 (monitor 1): teclado y mouse. Es la que suena.
rem   Ventanas 2 y 3 (monitores 2 y 3): una con cada control, sin sonido. Al abrir piden
rem   "Aprieta A en tu control": clic en la ventana y A en el control que juega ahi.
rem Cada ventana deja su log al lado (Showcase_1.log, ...).
rem Lo arma cada build (MercShowcaseBuildBat). Para otra combinacion, editar este archivo.
cd /d "%~dp0"
start "" "GasTest.exe" -kbm -monitor 1 -logFile "%~dp0Showcase_1.log"
timeout /t 2 /nobreak >nul
start "" "GasTest.exe" -pad -mute -monitor 2 -logFile "%~dp0Showcase_2.log"
timeout /t 2 /nobreak >nul
start "" "GasTest.exe" -pad -mute -monitor 3 -logFile "%~dp0Showcase_3.log"
