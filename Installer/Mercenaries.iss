; ============================================================
; Mercenaries — instalador para Windows (Inno Setup 6)
;
; Arma un Mercenaries_Setup_<versión>.exe con una build de Unity: se instala en
; "Mercenaries", deja acceso directo en el escritorio y en el menú Inicio, y se
; desinstala desde "Aplicaciones instaladas". Basado en el ISCC.iss de vampireduke
; (pull request #3), corregido el 29 de octubre de 2026.
;
; CÓMO SE USA (con Inno Setup 6 instalado, jrsoftware.org):
;   · Abrir este archivo con Inno Setup y Build ▸ Compile (Ctrl+F9). Toma la build de
;     ..\Builds\B9 (la carpeta con GasTest.exe).
;   · Otra build, desde la terminal:
;       "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DBuild=B10 /DAppVersion=0.1.1 Mercenaries.iss
;   · También anda copiado DENTRO de la carpeta de una build (junto a GasTest.exe).
;   El setup sale en Builds\Instaladores.
;
; Lo que NO va: la carpeta *_BurstDebugInformation_DoNotShip (Unity pide no distribuirla).
; ============================================================

#ifndef Build
  #define Build "B9"
#endif
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

; Dónde está la build: al lado de este archivo si lo copiaron ahí, si no en ..\Builds\<Build>.
#if FileExists(AddBackslash(SourcePath) + "GasTest.exe")
  #define BuildDir "."
#else
  #define BuildDir "..\Builds\" + Build
#endif

[Setup]
; El AppId identifica al juego: una versión nueva instala ENCIMA de la anterior. No cambiarlo.
AppId={{1C35BAF3-E130-4CA0-8A21-7079E0B1ACC2}
AppName=Mercenaries
AppVersion={#AppVersion}
AppVerName=Mercenaries {#AppVersion}
AppPublisher=Gustavo Pedraza
SourceDir={#BuildDir}
; Sin pedir administrador: se instala en la carpeta del usuario (AppData\Local\Programs).
; El que quiera, elige "para todos los usuarios" y va a Program Files.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\Mercenaries
DefaultGroupName=Mercenaries
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\GasTest.exe
UninstallDisplayName=Mercenaries
OutputDir=..\Instaladores
OutputBaseFilename=Mercenaries_Setup_{#AppVersion}
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Marcada de entrada: el que no la quiera, la desmarca.
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "GasTest.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "UnityPlayer.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "UnityCrashHandler64.exe"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "FishNet.SDK.Id"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "Showcase.bat"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "GasTest_Data\*"; DestDir: "{app}\GasTest_Data"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "MonoBleedingEdge\*"; DestDir: "{app}\MonoBleedingEdge"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "D3D12\*"; DestDir: "{app}\D3D12"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\Mercenaries"; Filename: "{app}\GasTest.exe"; WorkingDir: "{app}"
Name: "{autoprograms}\Mercenaries (showcase, tres ventanas)"; Filename: "{app}\Showcase.bat"; WorkingDir: "{app}"; IconFilename: "{app}\GasTest.exe"; Check: ShowcaseInstalled
Name: "{autodesktop}\Mercenaries"; Filename: "{app}\GasTest.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\GasTest.exe"; Description: "{cm:LaunchProgram,Mercenaries}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
// El acceso directo del showcase solo si la build trae el Showcase.bat.
function ShowcaseInstalled: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\Showcase.bat'));
end;
