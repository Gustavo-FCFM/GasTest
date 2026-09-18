# GasTest — contexto del proyecto

Juego: **Mercenaries**, hero shooter en tercera persona (Unity 6000.0.59f2, URP).
Motor de habilidades **propio** (GAS) + red con **FishNet**. Desarrollador único:
Gustavo Pedraza. Todo el código está comentado en español.

---

## Cómo trabajamos

**Los nombres van en INGLÉS.** Prefabs, GameObjects, materiales, escenas, carpetas,
clases y campos: `Objective_GoldBag`, `Base_Team1`, `Mat_ArenaSand`, `SpawnPoint_1`.
Los **comentarios, los `Debug.Log`, los textos de UI y los menús de editor siguen en
español** — el proyecto entero está así.

**El trabajo dentro del editor de Unity lo hace Gustavo.** Armar escenas, crear y
configurar prefabs, cablear referencias en el Inspector, acomodar geometría: eso es
suyo, porque lo está aprendiendo. Lo que se entrega desde acá es **código** — incluidas
las herramientas de editor que él después corre — más las instrucciones de qué tocar.
Cuando el pedido es concreto ("armame la arena", "generá el prefab"), se hace.

**No cablear prefabs por ruta fija** en herramientas de editor: él renombra los assets y
la referencia queda en null sin avisar. Buscarlos por COMPONENTE
(`AssetDatabase.FindAssets("t:Prefab")` + `GetComponent<T>()`), con la ruta solo como
atajo.

**Para verificar que compila, SIN cerrar el editor.** Unity trae su propio Roslyn, y deja
en `Library/Bee` el archivo con los argumentos exactos que usa para compilar. Se lo puede
invocar directo: tarda segundos y no abre una segunda instancia.

```bash
cd ".../Proyectos Gustavo/GasTest/GasTest"
D="/c/Program Files/Unity/Hub/Editor/6000.0.59f2/Editor/Data"
RSP=$(ls -t Library/Bee/artifacts/*.dag/Assembly-CSharp.rsp | head -1)
sed 's|-out:.*|-out:"Temp/_check.dll"|; s|-refout:.*|-refout:"Temp/_check.ref.dll"|' "$RSP" > /tmp/check.rsp
"$D/NetCoreRuntime/dotnet.exe" "$D/DotNetSdkRoslyn/csc.dll" "@/tmp/check.rsp" -nologo 2>&1 | grep "error CS"
```

Tres límites que conviene saber:

- **El `.rsp` lista los archivos fuente uno por uno.** Un `.cs` NUEVO no entra hasta que
  Unity regenere ese archivo — hay que agregarlo a mano a la copia (`echo '"Assets/..."'
  >> /tmp/check.rsp`) o compilar con Unity una vez.
  Y **no termina en salto de línea**: hacer `echo` antes de agregar, si no la primera ruta se
  pega a la línea anterior y ese archivo no compila (sale como "tipo no encontrado").
- **No corre el codegen de FishNet** (el ILPostProcess que genera los RPCs), así que un
  error que solo aparece ahí no lo detecta.
- Es para `Assembly-CSharp`. Para las herramientas de editor, el `.rsp` equivalente es
  `Assembly-CSharp-Editor.rsp`.

**Los `.cs` del proyecto están en CRLF** (los `.md` y los `.asset` van mezclados: revisar
con `file`). Un `perl -0pi` con `\n` en el patrón no encuentra nada y no avisa: hay que
escribir `\r\n` (o `\r?\n`) y meter `\r\n` también en el reemplazo. Y al leer un archivo
de reemplazo desde perl, `binmode` sin capa `:utf8` — con la capa, el resto del archivo
sale con las tildes rotas.

**Compilar con Unity en batch** sigue siendo la verificación completa, pero necesita el
editor CERRADO (no entran dos instancias sobre el mismo proyecto):

```bash
"C:/Program Files/Unity/Hub/Editor/6000.0.59f2/Editor/Unity.exe" -batchmode -quit -nographics -projectPath "C:/Users/MDyR/Desktop/Proyectos Gustavo/GasTest/GasTest" -logFile compile.log
```

---

## Mapa del proyecto

La regla, en una línea: **arte en Art, prefabs en Prefabs, datos en Attributes / Effects /
GameplayAbilities, código en Scripts.** Todo va por lo que ES, no por quién lo usa.

| Dónde | Qué |
|---|---|
| `Assets/Art/` | Modelos, materiales (`Materials/Arena` es la arena), animaciones (los AOC por clase), VFX, e `Icons/` (`Classes/` y `Abilities/` por clase). |
| `Assets/Audio/` | Los clips: `Sfx/` y `Music/`. La biblioteca que los referencia está en `Resources/AudioLibrary`. |
| `Assets/Prefabs/` | Todos los prefabs: `Player/` (jugador y su cámara), `Enemies/`, `Objective/`, `Projectiles/`, `Summons/`, `UI/`. |
| `Assets/Attributes/` | Clases jugables (`Class_*.asset`) y stats (`ASDef_*`), por clase; `Enemies/` y `Summons/` para los NPCs. |
| `Assets/Effects/` | Los `GE_*`, por tipo (Damage, Buffs, CC, Debuffs, Cooldowns). |
| `Assets/GameplayAbilities/` | Los `GA_*` por clase, **con sus scripts `GA_*.cs` al lado** — son contenido. `Enemies/` para las de los NPCs. |
| `Assets/Scripts/GAS/` | El motor de habilidades: ASC, efectos, habilidades base, atributos, tags, registros. `Entities/` son los objetos que las habilidades crean (proyectiles, tótems, barreras). |
| `Assets/Scripts/Network/` | Conexión y red: `NetworkAbilitySystemComponent`, `NetworkGameManager`, `ConnectionHUD` y `LobbyManager` (la sala de espera). |
| `Assets/Scripts/Player/` | El jugador y sus cámaras: `PlayerController`, input, animación, `ThirdPersonOrbitCam`, `SpectatorCamera`, `MenuOrbitCamera`. |
| `Assets/Scripts/UI/` | Toda la UI que no es de un modo: HUD de clase, menú de clases, sala (`UI_LobbyPanel`), menú principal, ajustes, `MercUIFactory`, y `UICursor` — el único dueño del cursor y del modo de input. |
| `Assets/Scripts/Settings/` | `GameSettings` (PlayerPrefs). |
| `Assets/Scripts/Audio/` | Sonido: `AudioManager`, `MusicPlayer`, `AudioLibrary` y `SfxCue`. |
| `Assets/Scripts/GameMode/` | Lo que sirve a CUALQUIER modo: `DeathZone`, `FloatingVisual`, `NPC_Target`. |
| `Assets/Scripts/GameMode/Mercenaries/` | **El modo Mercenarios**, y solo él: el modo, la arena, bases, objetivo, NPCs, bots y su HUD (`UI/`). Ver su `LEEME_ModoMercenarios.md`. Un modo nuevo (FFA) va en su propia carpeta al lado. |
| `Assets/Scripts/Editor/` | Todas las herramientas de editor (menú `Mercenarios ▸ …` y el inspector de habilidades). Tiene que llamarse `Editor` para que Unity no las meta en la build. |
| `Assets/Resources/` | Lo que se carga por nombre: los dos registros de red, el catálogo de enemigos, la biblioteca de audio. |
| `Assets/48toPlay/` | Restos de la game jam: los fantasmas y sus stats. Se reusan como NPCs. |
| `Assets/AssetsExtra/` | Packs comprados: FishNet, animaciones de Kevin Iglesias, Medieval Cute Series, Vefects, VFX. |
| `DesignDocuments/` | Diseño en `.docx`: arquitectura del GAS, guía de habilidades, las 8 clases, glosario. |

**Escenas:**
- `Assets/Scenes/Test_Network.unity` — pruebas rápidas de clases y habilidades.
- `Assets/Scenes/Mercenaries_Gamemode.unity` — **el modo de juego**, la arena 3c3c3. Es también la
  pantalla de inicio: el menú principal es un panel sobre la arena, no otra escena.

---

## Reglas técnicas que no se negocian

- **El servidor manda.** Toda la lógica de juego (daño, efectos, puntaje, experiencia)
  corre server-side; los clientes solo dibujan lo que llega sincronizado. Cada
  `GameplayAbility.Activate()` empieza con `if (!IsServer) return;`.
- **Los enums se amplían SOLO al final.** `EGameplayTag` y `EAttributeType` se serializan
  por número en los `.asset`; insertar un valor en el medio corre los índices y rompe
  todas las referencias ya guardadas.
- **Los jugadores viven en la capa 7 ("Character")**, no en la 6. Los ataques melee la
  usan como máscara en sus `Physics.OverlapX`.
- **`ASC.ActiveEffects` solo existe poblado en el servidor.** La UI remota lee de los
  diccionarios sincronizados del `NetworkAbilitySystemComponent`, no del ASC local.
- **El transform del jugador es client-authoritative**: teletransportes, saltos y dashes
  los ejecuta el DUEÑO por TargetRpc, nunca el servidor escribiendo el transform.
- **Los registros de red se actualizan a mano.** Cada `GA_*` o `GE_*` nuevo tiene que
  entrar en `GameplayAbilityRegistry` / `GameplayEffectRegistry` (los dos assets de
  `Assets/Resources/`), porque FishNet manda el ÍNDICE dentro de esas listas y no la
  referencia. Lo hace `Mercenarios ▸ Actualizar los registros de red`. Si falta uno, su
  VFX se ve **solo en el host**.
- **El cursor tiene un solo dueño: `UICursor`.** Ningún menú toca `Cursor.lockState` ni
  `PlayerInputProvider.SetUIMode` por su cuenta — los pide con `UICursor.Request(this)` y
  los suelta con `Release`. Si no, gana el último en cerrarse.
- **Los managers de escena reinician su sesión en `OnStartServer`.** Al parar el servidor
  FishNet destruye lo spawneado pero NO toca el estado de los NetworkObjects de escena
  (`LobbyManager`, `NetworkGameManager`, `MercenariesGameMode`): SyncVars, listas,
  puntajes. Desconectar → Iniciar Host en el mismo Play es un camino normal (el menú
  principal lo usa), así que todo estado de partida se limpia al arrancar el servidor.

Los detalles finos del GAS (pipeline de daño, acumulación de efectos, animaciones de
combo) están en `DesignDocuments/GAS_Arquitectura.docx`.

---

## Conexión para jugar con amigos

Túnel UDP de **playit.gg** al puerto **7770** — sin VPN y sin abrir puertos en el router.
El botón de Host solo aparece dentro del editor (ver `ConnectionHUD.HostOnlyInEditor`),
así que las builds que se reparten solo pueden ser cliente.

---

## Git

Repo: `.../Proyectos Gustavo/GasTest/GasTest` (la carpeta INTERNA; la de afuera no es el
repo). Remoto: `https://github.com/Gustavo-FCFM/GasTest.git`, rama `main`.

Al cambiar de máquina, sincronizar con **fetch + reset**, no con `pull`:

```bash
git fetch origin && git checkout main && git reset --hard origin/main
```

(La máquina de casa tuvo historia divergente en julio de 2026; un `pull` normal recrea
ese merge conflictivo. Lo descartado quedó en la rama `backup-merge-2026-07-15`.)
