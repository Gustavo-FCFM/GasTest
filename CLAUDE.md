# GasTest — contexto del proyecto

Juego: **Mercenaries**, hero shooter en tercera persona (Unity 6000.0.59f2, URP).
Motor de habilidades **propio** (GAS) + red con **FishNet**. Desarrollador único:
Gustavo Pedraza. Todo el código está comentado en español.

---

## Cómo trabajamos

**Los nombres van en INGLÉS.** Prefabs, GameObjects, materiales, escenas, carpetas,
clases y campos: `Objective_GoldBag`, `Base_Team1`, `Mat_ArenaSand`, `SpawnPoint_1`.
Los **comentarios, los `Debug.Log` y los menús de editor siguen en español** — el
proyecto entero está así.

**Lo que ve el jugador va en ESPAÑOL e INGLÉS, por `Loc`** (desde el 8 de octubre de
2026, pedido de Gustavo tras la prueba; antes era solo inglés). Ningún texto visible se
escribe en el código: se pide por clave, `Loc.T("menu.play")`, y la tabla
`Assets/Resources/Localization.csv` (columnas `key;es;en`, UTF-8, se abre con Excel) dice
qué es en cada idioma. Las variables van con nombre: `Loc.T("lobby.team", ("team", 2))`
con `Equipo {team}` en la tabla. Un texto fijo se ata con `LocalizedText` (se reescribe
solo al cambiar de idioma); lo que se recalcula llama a `Loc.T` cada vez y escucha
`Loc.OnLanguageChanged`. **El juego arranca en español**; el idioma se elige en Ajustes
(`GameSettings.Language`). "Mercenaries" no se traduce. Lo que el SERVIDOR le manda a los
clientes no puede ir como texto ya escrito (cada uno lo ve en su idioma): se manda qué es
(un enum, un índice) y cada pantalla lo escribe: el registro de bajas manda el índice de
la clase y qué NPC es (`ENpcLabel`), el desatascarse manda la CLAVE del motivo. **El CONTENIDO no va en la
tabla, va en el propio asset** (desde el 8 de octubre de 2026): el inglés y, al lado, el
español — `ClassName`/`ClassNameEs`, `Description`/`DescriptionEs`, `AbilityName`/
`AbilityNameEs`, el `Name`/`NameEs` de cada opción de rueda. La UI lee `DisplayName` /
`DisplayDescription` (`Loc.Pick`), nunca el campo en inglés directo. Así renombrar un asset no
rompe nada. Se cargan todos de una por `DesignDocuments/Traduccion_Contenido.csv` (por GUID) con
`Mercenarios ▸ Idiomas ▸ Importar nombres traducidos` / `Exportar`. Una habilidad o clase
nueva lleva su texto en español.

**El trabajo dentro del editor de Unity lo hace Gustavo.** Armar escenas, crear y
configurar prefabs, cablear referencias en el Inspector, acomodar geometría: eso es
suyo, porque lo está aprendiendo. Lo que se entrega desde acá es **código** — incluidas
las herramientas de editor que él después corre — más las instrucciones de qué tocar.
Cuando el pedido es concreto ("armame la arena", "generá el prefab"), se hace.

**No cablear prefabs por ruta fija** en herramientas de editor: él renombra los assets y
la referencia queda en null sin avisar. Buscarlos por COMPONENTE
(`AssetDatabase.FindAssets("t:Prefab")` + `GetComponent<T>()`), con la ruta solo como
atajo. **Ojo con los nombres repetidos al buscar por nombre:** hay dos `GA_Fireball` (el de
`48toPlay` es el del fantasma); el 22 de octubre de 2026 una herramienta agarró ese y la Bola de
fuego de la Evocación salió sin canalizar. Si un nombre puede repetirse, filtrar también por la
carpeta (o avisar si hay más de uno).

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
| `Assets/Prefabs/` | Todos los prefabs: `Player/` (jugador y su cámara), `Enemies/`, `Objective/`, `Projectiles/`, `Summons/`, `UI/`, `Map/` (el sector del cementerio). |
| `Assets/Attributes/` | Clases jugables (`Class_*.asset`) y stats (`ASDef_*`), por clase; `Enemies/` y `Summons/` para los NPCs. |
| `Assets/Effects/` | Los `GE_*`, por tipo (Damage, Buffs, CC, Debuffs, Cooldowns). |
| `Assets/GameplayAbilities/` | Los `GA_*` por clase, **con sus scripts `GA_*.cs` al lado** — son contenido. `Enemies/` para las de los NPCs. |
| `Assets/Scripts/GAS/` | El motor de habilidades: ASC, efectos, habilidades base, atributos, tags, registros. `Entities/` son los objetos que las habilidades crean (proyectiles, tótems, barreras). |
| `Assets/Scripts/Network/` | Conexión y red: `NetworkAbilitySystemComponent`, `NetworkGameManager`, `LobbyManager` (la sala de espera), `NetAddress` (las direcciones), `LanDiscovery` (la lista de partidas en la red) y `ConnectionHUD` (el recuadro viejo, solo para `Test_Network`). |
| `Assets/Scripts/Player/` | El jugador y sus cámaras: `PlayerController`, input, animación, `RagdollController`, `UpperBodyAim`, `ThirdPersonOrbitCam`, `SpectatorCamera`, `MenuOrbitCamera`. |
| `Assets/Scripts/UI/` | Toda la UI que no es de un modo: HUD de clase, menú de clases, sala (`UI_LobbyPanel`), menú principal, ajustes, `MercUIFactory`, `InputGlyphs` (la ÚNICA tabla de qué botón se dibuja: Q / RB / R1) y `UICursor` — el único dueño del cursor y del modo de input. |
| `Assets/Scripts/Settings/` | `GameSettings` (PlayerPrefs), incluido qué control eligió el jugador y el idioma. |
| `Assets/Scripts/Localization/` | `Loc` (los textos por clave, en español e inglés) y `LocalizedText`. La tabla es `Resources/Localization.csv`. |
| `Assets/Scripts/Audio/` | Sonido: `AudioManager`, `MusicPlayer`, `AudioLibrary` y `SfxCue`. |
| `Assets/Scripts/GameMode/` | Lo que sirve a CUALQUIER modo: `DeathZone`, `FloatingVisual`, `NPC_Target`, `HealthPack`. |
| `Assets/Scripts/GameMode/Mercenaries/` | **El modo Mercenarios**, y solo él: el modo, la arena, bases, objetivo, NPCs, bots y su HUD (`UI/`). Ver su `LEEME_ModoMercenarios.md`. Un modo nuevo (FFA) va en su propia carpeta al lado. |
| `Assets/Scripts/Editor/` | Todas las herramientas de editor (menú `Mercenarios ▸ …` y el inspector de habilidades). Tiene que llamarse `Editor` para que Unity no las meta en la build. |
| `Assets/Resources/` | Lo que se carga por nombre: los dos registros de red, el catálogo de enemigos, la biblioteca de audio y la tabla de textos (`Localization.csv`). |
| `Assets/48toPlay/` | Restos de la game jam: los fantasmas y sus stats. Se reusan como NPCs. |
| `Assets/AssetsExtra/` | Packs comprados: FishNet, animaciones de Kevin Iglesias, Medieval Cute Series, Vefects, VFX. |
| `DesignDocuments/` | Diseño en `.docx`: arquitectura del GAS, **arquitectura de red** (`Red_Arquitectura.docx`: servidor autoritativo, cómo viaja el ASC, todas las RPC), guía de habilidades, las 8 clases, glosario. |

**Escenas:**
- `Assets/Scenes/Test_Network.unity` — pruebas rápidas de clases y habilidades.
- `Assets/Scenes/Mercenaries_Gamemode.unity` — **el modo de juego**, la arena 3c3c3. Es también la
  pantalla de inicio: el menú principal es un panel sobre la arena, no otra escena.
- `Assets/Scenes/Mercenaries_Graveyard.unity` — **el mapa nuevo** (El Cementerio de los Tres
  Panteones), en greybox. Es una copia de la del modo con otro mapa: trae la sala, la red y
  los menús. Tres instancias de `Prefabs/Map/Graveyard_Sector` giradas 120°: lo que se
  cambie en el prefab cambia en los tres sectores. La generó una herramienta una sola
  vez; desde ahí se edita a mano (Gustavo ya le sacó muros y agrandó el centro).

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
- **La animación NO viaja por un `NetworkAnimator`: el jugador no tiene uno.** El que
  tenía estaba inerte (su campo Animator vacío y el Animator en un hijo, así que FishNet
  no lo encontraba y se apagaba solo, sin warning) y se quitó el 25 de septiembre de
  2026. **No volver a ponerlo** "para arreglar" algo: chocaría con lo que ya se manda.
  **Toda** la animación se manda a mano: las de habilidad, golpe, stun y muerte por las
  RPC del `NetworkASC`, y caminar/saltar/caer más el `AimPitch` por las RPC de
  `PlayerController`. Un parámetro nuevo del Animator que tenga que verse en las otras
  pantallas hay que mandarlo; no alcanza con escribirlo.
  Una habilidad con pose sostenida (un canalizado: el molinete, la carga del Golpe final)
  declara `BroadcastsOwnAnimation`: si no, el clip suelto que el servidor manda después de
  `Activate()` pisa la pose en las otras pantallas (el dueño no lo nota: ese RPC lo saltea).
- **Los modificadores `Multiply` se MULTIPLICAN entre sí** (×0.5 y ×0.5 = ×0.25):
  `AttributeValue` guarda la lista y rehace el producto. Hasta el 25 de septiembre de
  2026 se sumaban (×0.5 + ×0.5 = ×0). La velocidad de ataque tiene un tope de
  `MinAttackInterval` = 0.3 s entre ataques.
- **Los managers de escena reinician su sesión en `OnStartServer`.** Al parar el servidor
  FishNet destruye lo spawneado pero NO toca el estado de los NetworkObjects de escena
  (`LobbyManager`, `NetworkGameManager`, `MercenariesGameMode`): SyncVars, listas,
  puntajes. Desconectar → Iniciar Host en el mismo Play es un camino normal (el menú
  principal lo usa), así que todo estado de partida se limpia al arrancar el servidor.
- **Un NetworkObject de escena creado por código necesita que le asignen su `SceneId`.**
  FishNet se lo da al validar cada objeto de a uno en el editor; creando varios de un
  saque en una herramienta quedan en `SceneId 0`, y para FishNet eso no es un objeto de
  escena: no se spawnea (pasó con los botiquines, aparecía uno solo). Al terminar de
  crearlos hay que llamar `NetworkObject.CreateSceneId(Scene, force, out changed)` por
  reflexión — es lo mismo que corre *Tools ▸ Fish-Networking ▸ Utility ▸ Reserialize
  NetworkObjects*:

  ```csharp
  var m = typeof(NetworkObject).GetMethod("CreateSceneId",
      BindingFlags.Static | BindingFlags.NonPublic, null,
      new[] { typeof(Scene), typeof(bool), typeof(int).MakeByRefType() }, null);
  object[] args = { scene, false, 0 };   // true = rehacer TODOS (escena copiada de otra)
  m.Invoke(null, args);                  // args[2] = cuántos cambió
  ```
- **Una habilidad interrumpible se decide en DOS lados.** El servidor la corta al activar
  otra (`CancelSerial`), pero antes el cliente tiene que dejar leer el botón nuevo mientras
  hay una corriendo (`_attackInterruptible` / `_runningSlot` en
  `PlayerController.HandleAbilityInput`). Arreglar un solo lado hace que marcar
  `IsInterruptible` no cambie nada.
- **La definitiva carga por rol** (`EClassRole` en cada clase; ver
  `PlayerController` → "CARGA DE LA DEFINITIVA"). El daño aguantado y la curación se
  detectan solos en el pipeline de efectos del ASC; **una curación que escribe la vida
  directo** (como el aura del Paladín) tiene que avisar con `NotifyHealedAlly`, o el
  soporte no carga con ella — y con `ASC.ShowCombatNumber(curado, ECombatNumber.Heal)`,
  o no sale su número verde. Lo que pasa por `ExecuteInstantEffect` avisa las dos
  cosas solo. **Curarse a uno mismo no carga**: `OnHealedAlly` es solo para curar a
  OTRO. La pasiva que quiera enterarse de la autocuración (la Bendición del Clérigo)
  escucha `OnHealedSelf`, que va aparte justamente para no tocar la carga.

- **Qué bloquea a cada habilidad lo dice su `ActivationBlockedTags`, por TIPO de acción**
  (desde el 1 de octubre de 2026): `State_Stunned` en todas las que se aprietan;
  `State_Disarmed` en las de ARMA (golpear con ella o lanzarla; el bloqueo con escudo NO);
  `State_Silenced` en las de MAGIA o fantasía; `State_Rooted` en las de MOVIMIENTO. Una
  habilidad de dos tipos lleva los dos tags (cualquiera la bloquea). Las de "salir de
  apuros" (Comida de emergencia, Protección divina) solo llevan el aturdido. Una
  habilidad nueva tiene que llevar los suyos. Nada de esto se resuelve por código.

- **¿Es de mantener? Se pregunta `HoldInput.IsHold(habilidad)`, no `is IHoldAbility`**
  (desde el 1 de octubre de 2026). `GA_ProjectileShoot` implementa la interfaz pero solo
  es un mantenido con su casilla `AimBeforeThrow` (los lanzamientos apuntados). Un
  mantenido nuevo implementa `UsesHoldInput => true`.

- **Canalizar es una sección de cualquier GA, no código por habilidad** (desde el 19 de octubre
  de 2026): `CastMode` (se lanza al terminar / continuo), `CastSeconds`, clips de bucle.
  `ServerActivateAbility` llama a `BeginCast` en vez de `Activate`; cortada (lo que la bloquea,
  morir, otra habilidad si es interrumpible) **cuenta como lanzada**. En un lanzamiento apuntado
  lo resuelve `GA_ProjectileShoot` (mantener canaliza). Las que ya canalizan solas
  (`IChanneledAbility`) y las de rueda no pasan por ahí.

- **Todo lo que una habilidad deja en el mundo es una INVOCACIÓN: hereda de `Entity_Summon` y
  se crea con `Entity_Summon.Spawn(prefab, dueño, punto, giro, máximo)`** (desde el 19-20 de
  octubre de 2026; tótems, trampas, cuchillas, copias y compañeros). La base resuelve el equipo,
  el dueño en red (`SummonerObject`), "Dura", el máximo por dueño, si se va cuando el dueño muere,
  irse si el dueño se va o cambia de clase, su vida (`OnOwnDeath`, en Update) y la visibilidad por
  equipo. Lo que aplica algo a los de cerca cada tanto es una **`Summon_Zone`**, sin código. Una
  subclase sobreescribe `Awake`/`Update` como `override` llamando a `base.` (si no, la base no
  corre). Para apoyarla en el piso: `GroundUnderAim` (nunca arriba de un techo) o `GroundNear`.
  No se hace `Instantiate` + `Spawn` a mano. Una nueva sale de una VARIANTE de las plantillas
  `Prefabs/Summons/Summon_Base` (con vida) o `SummonZone_Base` (solo zona), y la pone el genérico
  `GA_SpawnSummon`: los pasos están en la guía de habilidades (7.1).

- **El choque de los proyectiles es un barrido del servidor** (`GC_Projectile.Sweep`, desde el 19
  de octubre de 2026), no `OnTriggerEnter`: el tramo recorrido en cada paso, con la forma del
  proyectil (la caja toma el tamaño del arma) y un radio aparte contra personajes.

- **Dónde va cada VFX.** Lo que dura lo que dura un buff o un debuff va en el
  `TargetVFX` del **GE** (aparece pegado a quien lo recibe, en todas las pantallas, y se
  va cuando el efecto termina; `TargetVFXLoop` repite las partículas de un solo
  disparo). No en la lista de VFX de la habilidad con `EndWithTag`. Los VFX de la
  habilidad van en su lista **`Visuals`** (cada entrada dice cuándo: al lanzar / al
  golpear / en el impacto); en código se disparan con `BroadcastHitVFX(objetivo)` y
  `BroadcastImpactVFX(punto)` — nunca un `Instantiate` a mano ni un campo `HitVFX` nuevo.
  Un GE tiene UN `TargetVFX`: si el buff necesita dos piezas, van juntas en un prefab
  (`VFX_AvengingAngel`, `VFX_FrenzyBuff`). Los `GE_*` se guardan en `Effects/`, nunca al
  lado de la habilidad. El VFX de **curar a un aliado** va en el momento **"Al curar"** de la
  habilidad (desde el 14 de octubre de 2026): sale solo sobre el curado, nunca sobre un enemigo;
  lo dispara `ApplyHitEffects` al aplicar un efecto que sube la vida. Cada GA elige el suyo.

- **Los efectos de una habilidad van en su lista `Effects`** (desde el 7 de octubre de
  2026): cada entrada dice cuándo (al golpear, al primer golpe, al activarse, al matar),
  a quién (enemigos, aliados, el lanzador, todos) y una condición de tag opcional. El
  código los aplica con `ApplyHitEffects(objetivo, primerGolpe)`; "al activarse" lo hace
  `CommitAbility`. Nada de campos sueltos `DamageEffect` / `AdditionalEffects` nuevos.
  Los campos nuevos de un GA (y de un GE, una clase, un ASDef, un perfil de pared o un componente) llevan
  **`[Label("...")]`** con su nombre en español: el Inspector se ve en español (desde el 13 de
  octubre de 2026) y sin `[Label]` el campo sale en inglés. **El nombre dice la unidad** (desde
  el 15 de octubre de 2026, etapa 6 de la revisión): `(m)`, `(s)`, `(m/s)`, `(°)`, `(px)`, `(×)` para
  multiplicadores, `(0 a 1)` para fracciones (los de juego van así, nunca "%" si el valor es 0.3),
  `(%)` solo si de verdad se escribe 30, `(vida)` / `(exp)` para puntos, y `(más = más rápido)`
  en los suavizados sin unidad. Un 0 especial va en el nombre: `Alcance máximo (m; 0 = sin tope)`. Un enum nuevo que se elige en el
  Inspector lleva `[InspectorName("...")]` en cada valor. Los campos nuevos de un GA llevan **`[Section]`** (con las constantes de
  `AbilitySection`) y, si dependen de otro, **`[ShowIf]`**; si tiene forma, que
  sobreescriba `DrawGizmos` y `DrawSceneHandles` (manijas con `AbilityHandles`).
  **Lo que gasta acumulaciones** (las de Artes marciales del Monje: cada una da
  `Status_MartialArts`) va por la sección **Acumulaciones** del GA (`StacksTag` +
  `ConsumeStacks`) y el "Con las acumulaciones" de cada entrada de efectos — nada de código
  por habilidad. La regla de Gustavo: sin acumulaciones, lo que escala no se aplica.
  **Cambiar el tipo de un campo ya cargado en assets** (uno suelto a lista, etc.): dejar
  el viejo como `[SerializeField, HideInInspector] private` con su mismo nombre y pasarlo
  en `OnUpgradeLegacyData` — así los assets no pierden lo cargado.

- **La UI del jugador vive en el prefab `Player Camera`** (desde el 14 de octubre de 2026,
  pedido de Gustavo): las barras de canalizar y de carga, los avisos de combate, la viñeta,
  la rueda, los compañeros, la curva de los lanzamientos, el material de la silueta y el
  marcador del piso de las habilidades apuntadas (`GroundTargetIndicator`: zona, franja y la
  casilla del muro como molde; desde el 21 de octubre de 2026, al jugar se suelta de la cámara
  para no temblar al girarla) están
  ARMADOS ahí (con un MOLDE apagado si algo se repite), y cada script los tiene por
  referencia: solo lee el juego y los anima. **Nada de crear canvas ni objetos de UI por
  código "encima".** Lo que se ve quieto se cambia en las piezas mismas; lo que cambia en
  juego, con campos en unidades absolutas y que digan cuáles (px del HUD, s, %), nunca
  relativos raros. El canvas del HUD es de 800×600 con match en ancho: lo pensado para un
  canvas de 1920 va × 800/1920. Una pieza nueva se pone con una herramienta de una vez y
  después se edita a mano. Lo que es de la PARTIDA y no del personaje (registro de bajas,
  pantalla de muerte, números flotantes, marcador) va al **prefab de partida `Prefabs/UI/Match_UI`**
  (uno por escena; desde el 14 de octubre de 2026: registro de bajas, pantalla de muerte, números,
  marcador, avisos de partida, marcadores del Objetivo y el espectador). Los menús (inicio, conexión,
  Ajustes, la sala y la pausa, más `MenuInput`) están en el **prefab de menús `Prefabs/UI/Menus_UI`**: cada fila de Ajustes lleva un
  `UI_SettingRow` y cada lugar de la sala un `UI_LobbySlot`. **El color de un botón de los menús va en el
  Button ("Normal Color" y los demás estados), con su imagen BLANCA** (`ButtonTint`, desde el 26 de octubre de 2026):
  así el elegido con el control se ve del mismo color, más oscuro (nunca más pálido: Gustavo no quiere colores deslavados). Pintar la imagen rompe los estados; desde código, `ButtonTint.Paint`.
  Los canvas de los dos prefabs son de
  1920×1080 y escalan con la pantalla. Están en la capa UI: si tapan la vista de escena, se apaga esa capa en el
  desplegable Layers del editor (no cambia el juego). Un `Graphic` propio necesita `[RequireComponent(typeof(CanvasRenderer))]`:
  sin él no se dibuja y no avisa. La **barra de vida de arriba de la cabeza** es UN prefab para todos,
  **`Prefabs/UI/Nameplate`** (`UI_Nameplate`: piezas y colores; desde el 19 de octubre de 2026): cada
  personaje lleva una instancia a su altura y `UI_WorldHealthbar` la encuentra sola. Un personaje o
  invocación nueva con vida lleva una instancia de ese prefab, nunca una barra armada aparte.

Los detalles finos del GAS (pipeline de daño, acumulación de efectos, animaciones de
combo) están en `DesignDocuments/GAS_Arquitectura.docx`.

---

## Conexión para jugar con amigos

Túnel UDP de **playit.gg** al puerto **7770** — sin VPN y sin abrir puertos en el router.
En la misma red (el showcase, 3 PCs con un router propio) no hace falta: se conectan a la IP
local del host, que la sala y la pausa muestran.

**La conexión es una pantalla del prefab de menús** (`UI_ConnectPanel`, desde el 26 de octubre
de 2026, para el showcase): Jugar → Crear partida, o la dirección (IP, o dirección:puerto la de
playit) y Conectar. **Cualquier build puede crear la partida** (se fue el candado de "solo el
editor o `-host`"). Lo que se abría con ESC es el **menú de pausa** (`UI_PauseMenu`, ESC o
Start): Continuar, Ajustes, Desatascarse, Salir. Los menús van por el Input System nuevo y
responden al control: quién recibe "volver" y Start y dónde está la selección lo decide
`MenuInput` (cada pantalla es un `IMenuScreen`). El recuadro viejo (`ConnectionHUD`, IMGUI)
queda solo en `Test_Network`, que no tiene el prefab de menús.

**La lista de partidas** (etapa 2 del showcase): la pantalla de conexión busca sola las partidas
abiertas en esa PC y en la red (`LanDiscovery`, UDP en el puerto **7771**, aparte del de la
partida) y las muestra como botones; la PC con la partida la anuncia. Por internet sigue la
dirección de playit escrita a mano. **Con control, A sobre un campo de texto abre el teclado en
pantalla** (`UI_OnScreenKeyboard`: X borra, Y espacio, Start acepta, B cancela); por eso los
campos ya no se ponen a escribir solos al elegirlos (`shouldActivateOnSelect` apagado).

---

## Git

Repo: `.../Proyectos Gustavo/GasTest/GasTest` (la carpeta INTERNA; la de afuera no es el
repo). Remoto: `https://github.com/Gustavo-FCFM/GasTest.git`, rama `main`.

**Al empezar a trabajar en una máquina, mirar arriba de `PENDIENTES.md`:** si hay una
sección "★ EN LA MÁQUINA DE CASA" (u otra máquina) con casillas sin marcar, son acciones
de una sola vez para esa máquina. Hacerlas primero y confirmarlas.

Al cambiar de máquina, sincronizar con **fetch + reset**, no con `pull`:

```bash
git fetch origin && git checkout main && git reset --hard origin/main
```

(La máquina de casa tuvo historia divergente en julio de 2026; un `pull` normal recrea
ese merge conflictivo. Lo descartado quedó en la rama `backup-merge-2026-07-15`.)
