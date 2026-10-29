# Builds

Las builds de Windows del juego, para compartirlas. Una carpeta por build: `B9`, `B10`…
(la carpeta que arma Unity, con `GasTest.exe`, `UnityPlayer.dll`, `GasTest_Data`,
`MonoBleedingEdge` y `D3D12`).

**Lo que git NO sube de acá** (está en el `.gitignore`):
- `GasTest_BurstDebugInformation_DoNotShip`: Unity pide no distribuirla.
- Los `.zip`: una build comprimida pasa los **100 MB por archivo** que acepta GitHub y el push
  falla. Para pasar un zip, adjuntarlo a un Release de GitHub.
- `Instaladores/` y `exebuild/`: lo que arma el instalador (`Installer/Mercenaries.iss`).

**Antes de subir una build:**
- Ningún archivo puede pasar los 100 MB (el más grande hoy es `sharedassets0.assets.resS`, unos
  83 MB). Si alguno se pasa, GitHub rechaza el push entero.
- Cada build pesa unos 200 MB y se queda en el historial de git PARA SIEMPRE, aunque después
  se borre: todos lo descargan al clonar. Subir solo las builds que valga la pena guardar.
