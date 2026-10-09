; -- 64Bit.iss --
; Demonstrates installation of a program built for the x64 (a.k.a. AMD64)
; architecture.
; To successfully run this installation and the program it installs,
; you must have a "x64" edition of Windows or Windows 11 on Arm.

; SEE THE DOCUMENTATION FOR DETAILS ON CREATING .ISS SCRIPT FILES!

[Setup]
AppName=GasTest
AppVersion=0.0
WizardStyle=modern dynamic
DefaultDirName={autopf}\My Program
DefaultGroupName=My Program
UninstallDisplayIcon={app}\MyProg.exe
Compression=lzma2
SolidCompression=yes
OutputDir="exebuild/GasTest.exe"
; "ArchitecturesAllowed=x64compatible" specifies that Setup cannot run
; on anything but x64 and Windows 11 on Arm.
ArchitecturesAllowed=x64compatible
; "ArchitecturesInstallIn64BitMode=x64compatible" requests that the
; install be done in "64-bit mode" on x64 or Windows 11 on Arm,
; meaning it should use the native 64-bit Program Files directory and
; the 64-bit view of the registry.
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "GasTest.exe"; DestDir: "{app}"; DestName: "GasTest.exe"
Source: "FishNet.SDK.Id"; DestDir: "{app}"
Source: "UnityCrashHandler64.exe"; DestDir: "{app}";
Source: "UnityPlayer.dll"; DestDir: "{app}";
Source: "D3D12/*"; DestDir: "{app}/D3D12"
Source: "GasTest_BurstDebugInformation_DoNotShip/*"; DestDir: "{app}/GasTest_BurstDebugInformation_DoNotShip" ; Flags: ignoreversion recursesubdirs
Source: "GasTest_Data/*"; DestDir: "{app}/GasTest_Data"; Flags: ignoreversion recursesubdirs
Source: "MonoBleedingEdge/*"; DestDir: "{app}/MonoBleedingEdge"; Flags: ignoreversion recursesubdirs



[Icons]
Name: "{group}\My Program"; Filename: "{app}\MyProg.exe"
