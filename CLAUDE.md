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

**Para verificar que compila:** Unity en batch mode, con el editor CERRADO (no entran
dos instancias sobre el mismo proyecto):

```bash
"C:/Program Files/Unity/Hub/Editor/6000.0.59f2/Editor/Unity.exe" -batchmode -quit -nographics -projectPath "C:/Users/MDyR/Desktop/Proyectos Gustavo/GasTest/GasTest" -logFile compile.log
```

Después buscar `error CS` en el log.

---

## Mapa del proyecto

| Dónde | Qué |
|---|---|
| `Assets/Scripts/GAS/` | El motor de habilidades: ASC, efectos, habilidades, atributos, tags. |
| `Assets/Scripts/Network/` | Capa de red: `NetworkAbilitySystemComponent`, `NetworkGameManager`, `ConnectionHUD`. |
| `Assets/Scripts/Player/` | `PlayerController` (movimiento, input, animación) y el prefab del jugador. |
| `Assets/Scripts/UI/` | HUD de clase, menús de clase y de entrada. |
| `Assets/Scripts/GameMode/` | **El modo Mercenarios.** Ver su `LEEME_ModoMercenarios.md`. |
| `Assets/Attributes/` | Clases jugables (`Class_*.asset`) y sus stats base (`ASDef_*.asset`). |
| `Assets/GameplayAbilities/` | Los assets de habilidades y efectos, por clase. |
| `Assets/48toPlay/` | Restos de la game jam: los fantasmas y sus stats. Se reusan como NPCs. |
| `Assets/AssetsExtra/` | Packs comprados: FishNet, animaciones de Kevin Iglesias, Medieval Cute Series, VFX. |
| `DesignDocuments/` | Diseño en `.docx`: arquitectura del GAS, guía de habilidades, las 8 clases, glosario. |

**Escenas:**
- `Assets/Scenes/Test_Network.unity` — pruebas rápidas de clases y habilidades.
- `Assets/Scenes/Mercenaries_Gamemode.unity` — **el modo de juego**, la arena 3c3c3.

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
