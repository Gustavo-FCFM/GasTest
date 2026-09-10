# Pendientes — actualizado el 10 de septiembre de 2026

Revisión completa contra el estado real del proyecto: **la mayoría de las tareas de
editor de la lista anterior ya estaban hechas**. Acá quedan solo las que verifiqué que
siguen abiertas, más el plan.

El proyecto compila limpio: 0 errores y 0 warnings en código propio (los 312 warnings
del log son todos de packs importados).

## Cómo retomar en la otra máquina

```bash
cd ".../Proyectos Gustavo/GasTest/GasTest"
git fetch origin && git checkout main && git reset --hard origin/main
```

Con **fetch + reset**, no con `pull` — la máquina de casa tuvo historia divergente en
julio y un pull normal recrea ese merge conflictivo. Ver `CLAUDE.md`.

---

# 0. Lo que ya está hecho (verificado, no hace falta volver a tocarlo)

Del documento anterior, ya están resueltos:

- **Cámara**: `Player Camera.prefab` está taggeado `MainCamera`.
- **Salto y caída**: existen el parámetro `VerticalSpeed`, el estado `Fall` y las
  transiciones; `Movement → Jump` ya tiene `Has Exit Time` destildado.
- **Escudo del Paladín**: `PLACEHOLDER_HoldLoop` ya no está en el Base Layer (queda una
  sola copia, la de UpperBody) y `PLACEHOLDER_HoldImpact` ya existe.
- **Salto por habilidad**: están los tres estados `PLACEHOLDER_AirStart/AirLoop/AirLand`.
- **Nivel 3**: `OpenSubclassesOnMaxLevel` está en 0.
- **Alas del Ángel**: `DemoAnimationSelector` ya no está en ningún prefab y
  `GE_AvengingAngelTag.TargetVFX` ya tiene el prefab asignado.
- **Molinete**: `Usable While Channeling` marcado en `GA_Frenzy` y `GA_BarbarianLeap`.
- **Golpe final**: los tres `Charge*Animation` asignados (partiste el clip) y el
  `HitboxOffsetY` subido a 1.
- **Hacha arrojadiza**: `SpawnOffset.y` en 1.5.
- **Daño mágico**: `GA_SwordAttack` ya usa `GE_Class_Damage`.
- **Barras de vida de los enemigos**: los once prefabs migrados a `UI_WorldHealthbar`.
  `HealthBarNpc.cs` y todo su paso del menú ya no existen.
- **`GA_BossAura.Radius`**: subido de 2 a 9 (lo cambié yo en esta sesión).

---

# 1. Tareas de editor que SIGUEN pendientes

Son siete, y ninguna es urgente.

## Molinete del bárbaro — el VFX

- [ ] `GA_WhirlwindAttack.VisualPrefab` sigue apuntando al prefab **`Red`** (el círculo
      rojo de prueba). Va el tornado, con **`VFX_AreaVisual`** en la raíz del prefab y su
      `RadiusAtScaleOne` medido

Sin `VFX_AreaVisual` el efecto no se escala con el radio real de la habilidad: se ve del
mismo tamaño aunque el área cambie.

## Hacha arrojadiza — el blur

- [ ] Material del spin blur en `PF_ExampleProjectile` →
      `AssetsExtra/Simple Spin Blur/Materials/Spin Blur Material.mat`

## Rogue — el giro del segundo golpe

- [ ] `HumanM@Attack1H01_L` → Animation → **Root Transform Rotation → Offset**

**No lo lleves a cero**: algo de torsión es correcta para una puñalada con la izquierda.
El combo alterna derecha-izquierda a propósito (las cuatro clases del rogue llevan dos
dagas). Probá con la mitad de lo que tiene.

## El señuelo del Ilusionista tiene el avatar viejo

- [ ] Cambiar el modelo dentro de `GameplayAbilities/Prefabs/Summons/Entity_PlayerCopy.prefab`

Usa `AssetsExtra/RPG Tiny Hero Duo/Prefab/MaleCharacterPBR`, el avatar anterior al cambio
a Kevin Iglesias. **Es el único asset del proyecto que quedó atrás** — lo verifiqué contra
todos los prefabs y escenas.

El script ya asume que el avatar es compartido (su comentario lo dice: *"solo cambian arma
y animación por clase"*), así que alcanza con cambiar el hijo visual. La raíz no se toca:
ahí viven el `NetworkObject`, el `NetworkTransform`, el ASC, el collider y el script.

Tres cosas que tienen que sobrevivir al cambio:

1. **`WalkAnimator`** repuntado al `Animator` del modelo nuevo, con el mismo Avatar
   Humanoid del jugador (si no, el `ClassAnimatorOverride` está hecho para otro esqueleto).
2. **`Socket_MainHand` y `Socket_OffHand`** con esos nombres EXACTOS, colgando de los
   huesos de las manos. El script los busca por nombre en toda la jerarquía, no por
   referencia.
3. El `Animator` nuevo necesita un float **`Speed`** — es con lo que la copia mezcla
   caminar e idle.

## Limpieza de nombres

- [ ] Renombrar `GE_Reckless 1`, `GE_Reckless 2`, `GE_Reckless 3` y `GA_ConeReckless 1`

Los cuatro están **en uso**, no son huérfanos. Renombrarlos es seguro: Unity mantiene el
GUID y las referencias no se rompen. Ese sufijo ya te hizo dudar una vez sobre cuál
asset estabas editando.

## Recordatorio de subida de nivel *(opcional)*

- [ ] Cablear **`LevelUpNotification`** del `UI_PlayerHUD`, que sigue en `None`

Sirve como recordatorio fijo; el anuncio del centro se desvanece y si estabas peleando
te lo perdés.

## Speed Multiplier de la animación de acción

- [ ] Los dos `PlaceHolder_Action` tienen `ActionSpeedMult` asignado pero **la casilla
      desactivada**

No rompe nada (está igual en las dos capas), pero el ritmo de ataque no acelera la
animación: un personaje con mucha velocidad de ataque pega más seguido pero el swing se
ve igual de lento.

---

# 2. La sala nueva — YA ESTÁ MONTADA

El lobby viejo (`UI_LobbyMenu` + `UI_LobbyRoster`) se cambió por **un solo panel**:
`UI_LobbyPanel`. **La escena ya quedó armada y guardada**, no hay nada que cablear:

- `LobbyManager` está en el mismo GameObject que `NetworkGameManager` (comparte su
  `NetworkObject`, igual que `MercenariesGameMode`).
- `UI_LobbyPanel` está en su propio GameObject, con las tres clases base ya asignadas
  en `SelectableClasses` (Barbarian, Rogue, Paladin — las mismas del jugador).
- El menú viejo y el marcador de sala se sacaron de la escena.

La escena ya está armada y guardada; el generador que la creaba se borró (ver la sección
del menú, más abajo).

## Cómo se usa

El panel aparece **solo con conexión**: antes de iniciar el host o de conectarte, lo
único en pantalla es el recuadro de red.

1. Entrás y quedás de **espectador** automáticamente, sin tocar nada.
2. Escribís tu nombre arriba (Enter para confirmarlo).
3. Tocás **Unirte** en un lugar libre de un equipo.
4. Tocás **tu propio ícono** para abrir la grilla de clases (el fondo la cierra sin
   elegir).
5. **Confirmar** te pone en verde. Volver a tocarlo te apaga.
6. El **host** aprieta **Start** cuando quiera — el botón solo lo ve él, se pone verde
   cuando todos están listos, y la línea de estado le dice cuántos faltan.

Para salirte de un equipo, **Espectador** en la última fila de la columna de la derecha.

**El personaje aparece recién con el Start**, no al confirmar. Antes el que confirmaba
primero se quedaba dando vueltas por el mapa mientras los demás elegían.

Con `MinPlayersToStart: 1` lo podés probar solo.

## ESC y el recuadro de red

El recuadro de conexión se **minimiza solo** al conectarse (si no, se queda encima de la
IP del panel) y **ESC lo abre y lo cierra en cualquier momento**, también en plena
partida: ahí es donde está el botón de Desconectar. Mientras está abierto suelta el mouse
y apaga el mapa de input de juego, así la cámara no gira mientras buscás el botón.

Eso lo hace **`UICursor`** (`Scripts/UI/`), que es **el único dueño del cursor y del modo
de input**. Antes cada menú los fijaba por su cuenta y ganaba el último en cerrarse:
cerrar el menú de clases con el recuadro de red abierto te volvía a tragar el mouse.
Ahora cada menú los pide y los suelta, y quedan libres si los pide aunque sea uno.

La **rueda de habilidades es la excepción declarada**: pide el cursor con
`blockGameplayInput: false`, porque elige la opción con el movimiento del jugador.

Si agregás un menú nuevo, el patrón es:

```csharp
void OnEnable()  => UICursor.Request(this);
void OnDisable() => UICursor.Release(this);
```

## El menú Mercenarios — quedó en un solo ítem

Los ocho pasos de armado ya cumplieron su función: la arena, los prefabs, los enemigos y
el catálogo están hechos y guardados en la escena. Se borraron los tres archivos que los
contenían — `MercSetupTools.cs`, `MercArenaBuilder.cs` y `MercLayoutTools.cs`.

Lo único que queda en el menú es **`Actualizar los registros de red (habilidades y
efectos)`**, en `MercRegistryTools.cs`. Hay que correrlo **cada vez que creás un `GA_*` o
un `GE_*` nuevo**: FishNet manda el índice dentro de esas dos listas de `Resources`, así
que una habilidad que falte tiene su VFX visible SOLO en el host. Antes había que
acordarse de hacer click derecho ▸ *Auto-Fill From Project* sobre cada uno de los dos
assets por separado.

Ojo con una cosa: el índice tiene que significar lo mismo en todos los peers, así que
después de rellenarlos **hay que repartir el mismo build a todos**.

### Lo que se perdió con eso

Dos cosas que sí funcionaban, por si algún día hacen falta:

- **`3 · Regenerar la arena en la escena actual`** — el generador de la arena entera,
  incluidas las rejas de las salas seguras (es el que corriste cuando los personajes se
  escapaban de la jaula) y los espejados de los pasos 4 y 5.
- **`7 · Convertir enemigos de la jam a red`** — de los siete `Enemy_*` de `48toPlay`
  solo hay tres convertidos; faltan `DamageBoss`, `IceBoss`, `IceMage` y `RockMage`.

Vuelven con un comando. El último commit que los tenía es **`6143570`**:

```bash
git checkout 6143570 -- "Assets/Scripts/GameMode/Editor/MercSetupTools.cs" "Assets/Scripts/GameMode/Editor/MercArenaBuilder.cs" "Assets/Scripts/GameMode/Editor/MercLayoutTools.cs"
```

Los tres van juntos: se llaman entre ellos (`MercArenaBuilder` usa los materiales y los
buscadores de prefab de `MercSetupTools`, y `MercLayoutTools` usa a los dos).

**Una trampa si los restaurás.** Ese `MercSetupTools` de `6143570` es de ANTES de la sala
nueva: su paso `2 · Crear la escena de la arena` instala el `UI_LobbyMenu` viejo, no el
`LobbyManager` + `UI_LobbyPanel`. La versión corregida nunca llegó a commitearse, así que
no está en el árbol de ningún commit — queda acá, para pegarla a mano dentro de
`CreateNetworkStack()` reemplazando el bloque del "menú de entrada":

```csharp
managerGo.AddComponent<MercenariesGameMode>();

// La SALA vive en el mismo NetworkObject que el modo: es un objeto DE ESCENA, y así
// se spawnea con los otros dos sin cablear nada.
managerGo.AddComponent<LobbyManager>();

// ... (el bloque del ConnectionHUD queda igual) ...

// El panel se dibuja por código, así que alcanza con el componente pelado. Las
// clases elegibles se heredan del menú viejo, para no arrastrarlas una por una.
GameObject lobbyGo = new GameObject("UI_LobbyPanel");
UI_LobbyPanel lobbyPanel = lobbyGo.AddComponent<UI_LobbyPanel>();

GameObject legacyMenu = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath);
UI_LobbyMenu legacy   = legacyMenu != null ? legacyMenu.GetComponent<UI_LobbyMenu>() : null;
if (legacy != null && legacy.SelectableClasses != null)
    lobbyPanel.SelectableClasses = legacy.SelectableClasses;
else
    Debug.LogWarning("[Mercenarios] UI_LobbyPanel quedó sin clases elegibles — asigná " +
                     "SelectableClasses a mano o no vas a poder elegir clase en la sala.");
```

El paso 3 (regenerar la arena y las rejas) y los pasos 4-7 no tienen ese problema: no
tocan la sala.


Las medidas del panel están todas en el inspector del componente: `PanelSize`,
`ColumnWidth`, `RowHeight` y los cinco colores. Y no tiene scroll: con nueve jugadores
las columnas de tres entran justas.

`UI_LobbyMenu.cs` y su prefab ya no los usa nadie: eran la lista de clases que leía el
generador de la arena, y ese generador se borró. Se pueden borrar los dos — pero antes
copiá las tres clases (`Class_Barbarian`, `Class_Rogue`, `Class_Paladin`) del prefab, que
es el único lugar del proyecto donde están juntas además del panel y del Player.

---

# 3. Balance pendiente (decisiones tuyas)

## El daño mágico — RESUELTO

El tipo de daño lo decide **el atributo del que escala el modificador**: `Attack` da daño
físico (paga armadura) y `MagicDamage` da mágico (ignora armadura, vale doble contra
escudos).

Quedó decidido: **el Paladín usa su daño mágico solo en el Smite**, que es la habilidad
que lo tiene por diseño. El resto de su kit es físico. No hay nada que cambiar.

## El jefe — RESUELTO (ya hacía lo que querías)

`GA_BossAbility` ya encadena un aura que **buffea y cura** a sus aliados (`GA_BossAura`,
con `GE_BossBuffs` + `GE_BossHeal`) y después una bola de fuego. Lo que lo rompía era la
geometría: el aura tiene radio 9 y el jefe peleaba a `KeepDistance: 11` — **fuera de su
propia aura**. Quedó en 5. Y los rangos se ordenaron: detección 20 (= alcance), correa 28.

Si querés separar más las dos fases, el `DelayAfter` del paso del aura en el
`GA_ComboSequence` hace que cure, espere, y recién ahí ataque.

## Los monstruos — el DPS no tenía carril

El bárbaro tiene cuatro fuentes de área y el pícaro es todo objetivo único, así que
contra un campamento el tanque ganaba siempre. Pero el problema de fondo no era de
habilidades: **un jefe valía 75 y un fantasma 15** — cinco fantasmas eran un jefe, así
que barrer basura con área era la estrategia dominante para todos.

Se le dio un carril a cada uno moviendo experiencia, no habilidades:

| | antes | ahora |
|---|---|---|
| Fantasma | 15 | **10** |
| Mago | 25 | **40** |
| Jefe | 75 | **175** |

Y el fantasma pasó de 23 a **30** de vida, para que matarlo cueste *algo* y el área deje
de ser gratis. Si con esto el bárbaro sigue dominando, ahí sí es cosa de habilidades.

# 4. Plan hasta noviembre

El riesgo con tres meses y trabajando solo **no es que falten cosas: es que todo quede
al 80%**. Este orden es por dependencias y riesgo, no por ganas.

## 1º · Lobby — TERMINADO ✅

Código y montaje, los dos. Ver la sección 2.

## 2º · Bots y cámara espectador — CÓDIGO TERMINADO, falta probar

No estaban en el plan: salieron de que para probar el modo espectador hacen falta otros
jugadores, y juntar nueve personas cada vez no es viable.

**Los bots** (`GameMode/BotController.cs`) son jugadores de verdad: mismo prefab, mismo
ASC, mismas habilidades, spawneados sin dueño para que el servidor los maneje. Piden sus
habilidades por `ServerActivateAbility`, el MISMO punto de entrada que un jugador, así que
pagan cooldowns, energía y tags igual que todos. Se agregan desde la sala con el `+` de
cada lugar libre — solo lo ve el host.

Tres roles, deducidos de la clase base (`MainBaseClasses`: 0 Bárbaro, 1 Pícaro,
2 Paladín), así que una subclase nueva hereda el rol de su rama sin configurar nada:

- **Bárbaro** — encima y sin guardarse nada.
- **Pícaro** — todo el daño que pueda; se retira a su base a curarse bajo el 35 % de vida
  y vuelve al 75 %.
- **Paladín** — se pone ENTRE su compañero y quien lo ataca, y pega desde ahí (sus ataques
  son los que curan).

Rodean en vez de quedarse de frente, y se despegan mientras el ataque básico está en
cooldown. Al llegar a nivel 3 eligen subclase al azar.

**La cámara espectador** (`GameMode/SpectatorCamera.cs`) se enciende sola cuando tu fila
de la sala dice Spectator y la partida arrancó. Vuelo libre WASD + Espacio/Ctrl + Shift,
clic izquierdo/derecho para seguir jugadores, `F` para volver a libre, `H` para el panel
del observado y `M` para el marcador.

### Lo que falta verificar

Lo primero al retomar.

- ~~La cámara espectador~~ — PROBADA Y APROBADA.
- **Que ya no se salgan del mapa.** Ver abajo — es el problema que más vueltas dio.
- El **dash y el blink** de los pícaros.
- Los **cuatro slots de habilidad**. Las clases base usan LMB, RMB, Q y Shift; el bot solo
  probaba Q/E/R, así que **nunca** tiraron el hacha ni saltaron ni dashearon. Ahora se
  prueban los cuatro, con el arrojadizo de lejos y el cierre de distancia si está muy lejos.
- ~~Los tótems del Chamán~~ — ANDAN. Eran de RUEDA (no se activan con Activate, hay que
  elegir una opción del menú circular). Se les puso un ritmo propio (`SummonInterval`,
  10 s) porque los usaban sin parar y llenaban el mapa.
- El **salto del Bárbaro** y la **auto-revivida del Inmortal**.
- Que **no entren a las salas seguras ajenas**, y que la expulsión no se vea brusca
  (`EjectMargin` en cada `MercTeamBase`).

### El salto: no colisionaban mal, no APUNTABAN

Con los números del Bárbaro —`JumpVelocity 15`, `ForwardForce 15`— el salto vuela 3
segundos y recorre **46 metros**. La arena tiene 42 de radio: un solo salto la cruzaba
entera y aterrizaba afuera.

Perseguí eso como si fuera colisión (tres intentos, ver abajo) cuando el problema era
otro: **un jugador apunta el salto y cae donde quiso**, y el bot solo se impulsaba hacia
adelante con toda la fuerza. Ahora resuelve el tiro: mide el tiempo de vuelo, apunta al
objetivo, y recorta a lo que la habilidad permite Y a un punto donde de verdad se pueda
estar parado. Si no hay ninguno, salta en el lugar.

### El problema de salirse del mapa

Costó tres intentos y vale anotar por qué, porque el patrón se repite en este proyecto.

Los bots saltaban a través del muro y caían al vacío. Los dos primeros arreglos fallaron:

1. Escribir `transform.position` no colisiona con nada — obvio en retrospectiva.
2. Pasar a `CharacterController.Move()` **tampoco alcanzó**. El log lo mostró: un bot
   terminó a 64 m del centro y 12 m bajo el piso, en pleno salto. La matriz de colisiones
   estaba bien y la referencia también; el problema es que **el CharacterController tiene
   estado que otras partes del juego manipulan** (el dash le toca `excludeLayers`,
   `TeleportTo` lo apaga y lo prende). No es algo en lo que un script externo pueda confiar.

Lo que quedó: un **`Physics.SphereCast` propio** en `BotController.MoveWithCollision`, que
no depende del estado de nadie. El aterrizaje tampoco pregunta por el CharacterController
—un rayo corto hacia abajo—, y hay dos topes: 2,5 s de vuelo y 12 m de caída.

Además hay una **red de contención** que corre cuatro veces por segundo: si un bot está a
más de 3 m del NavMesh o por encima del techo, vuelve a su base. El invariante es el
NavMesh porque todo lo transitable —arena, rampas, pasillos y bases— está horneado.

**Si vuelve a pasar**, el log dice la posición exacta y si estaba saltando. Y queda un
sospechoso sin descartar: `MercArenaBounds.OpenTowardBases` deja **tres huecos de 30°** en
el anillo invisible para poder llegar a las bases — un cuarto del perímetro sin pared. Si
el log dice `saltando o dasheando: False`, se están yendo caminando por ahí y lo que hay
que cerrar es eso, no el bot.

### Lo que quedó sabido y sin hacer

- **Las clases base solo usan cuatro slots** (LMB, RMB, Q y Shift): E y R están vacíos.
  Las subclases sí los usan. Si querés que los bots muestren un kit completo desde el
  nivel 1, ahí falta contenido — son habilidades que hay que diseñar, no código.

- **Las perillas del bot no salen en el inspector.** El componente se agrega en runtime,
  así que los valores son los defaults del código (`BotController.cs`). Si vas a iterar
  mucho el comportamiento, conviene moverlas a un ScriptableObject cableado en el
  `NetworkGameManager`.
- **Los señuelos del Ilusionista se acumulan.** No tienen límite de tiempo (es el diseño),
  pero un bot activa la habilidad apenas sale de cooldown y la arena se llena. Tres
  caminos: dejarlo, ponerle vida útil a la copia (cambia el balance para todos), o que los
  bots la usen con criterio (no toca el balance — es lo que yo haría).
- **`Entity_PlayerCopy.prefab` usa el avatar VIEJO** (`RPG Tiny Hero Duo/MaleCharacterPBR`).
  Es el único asset del proyecto que quedó atrás. Ver la sección 1.
- **Las salas seguras ajenas están cerradas con PARED, no solo con expulsión.**
  `MercSafeRoomBarrier` arma cuatro paredes invisibles alrededor del área. Un collider no
  se puede filtrar por equipo desde el editor —las capas son globales y los equipos se
  deciden en runtime—, así que la pared es UNA sola, sólida para todos, y a cada personaje
  del equipo dueño se le apaga el par con `Physics.IgnoreCollision`. El que no es de casa
  nunca recibe ese permiso y choca.

  Los BOTS no chocan con ella caminando (un NavMeshAgent navega el NavMesh, no mira
  colliders): para ellos la regla vive en `BotController.AvoidEnemySafeRooms`. La pared
  frena a las PERSONAS y a cualquiera que llegue saltando o dasheando.

  Sigue estando además la expulsión (`EjectEnemies` en cada `MercTeamBase`), como red por
  si alguien igual se cuela.
- **El `DeathZone` no agarraba a los bots.** Tenía `if (!player.IsOwner) return;` y un bot
  no tiene dueño: caerse al vacío los dejaba cayendo para siempre. Ahora, para un bot, la
  guardia es estar en el servidor.
- **Ninguna habilidad usa ya `MovesThroughOwner`.** El teletransporte, el dash y el salto
  tienen los tres camino server-side. La propiedad queda en `GameplayAbility` por si
  escribís una nueva que mueva al dueño de un modo que el servidor no pueda reproducir.

## 3º · Sonido

El mayor salto de calidad percibida por hora invertida. Un juego mudo se lee como
prototipo aunque todo lo demás esté bien.

Tiene **cola larga**: son 67 habilidades. No las sonorices todas — elegí las ~15 que
suenan siempre (ataques básicos, pasos, golpe recibido, muerte, entrega del objetivo).
La música de batalla la está haciendo un amigo: tené los hooks listos para cuando llegue.

**Solo se puede hacer en la máquina de casa** — la del trabajo no tiene con qué trabajar
audio. Si estás en el trabajo, agarrá otra cosa de esta lista.

## 4º · Pantalla de inicio y ajustes

Necesaria pero de bajo riesgo: manejo de escenas y un panel de opciones. Se puede dejar
para el final sin que nada dependa de ella.

## 5º · Conexión — respondida, pero no la haría ahora

Hoy **solo vos podés hostear**, y es a propósito: `ConnectionHUD.HostOnlyInEditor` está
en `true`, así que las builds que repartís son solo cliente.

¿Pueden hostear ellos? Sí: destildás ese flag y **cada host levanta su propio túnel de
playit.gg** y comparte la dirección que le genere. Funciona, pero cada uno tiene que
instalar y configurar un túnel — para una demo con amigos eso es peor que hostear vos.

La solución de verdad (relay o servicio de lobbies) son semanas. **Dejalo como está y
revisalo después de la demo.**

## Lo que NO haría: el mapa de FFA

Un segundo modo trae su propio lobby, sus reglas, su balance y su HUD. Es exactamente lo
que se come una demo.

Si querés variedad, **un segundo mapa para Mercenarios cuesta una fracción** y da la
misma sensación de contenido. Eso sí, el generador de arena está borrado: habría que
restaurarlo (ver la sección 2) o armar el segundo mapa a mano.

---

# 5. Animaciones que siguen viéndose raras

No las dejes ahí. En la sesión de septiembre perseguimos cuatro causas distintas y al
final **casi todo era una sola**: confundir el yaw del cuerpo con el punto de mira. Es
muy posible que lo que queda también tenga un origen común.

El método que funcionó, para aplicarlo a cada caso:

1. **¿Es el transform o la pose?** Poné el peso de la capa UpperBody en 0 durante Play.
   Si el problema desaparece, es la capa/pose; si sigue, es la rotación del transform.
2. **¿Es el clip?** Miralo en la vista previa del FBX, sin el juego de por medio. Si ahí
   ya se ve torcido, el clip viene así.
3. **Si es el clip:** `Root Transform Rotation → Offset` te da respuesta visual
   inmediata, sin teorías.

Cuando lo retomes, anotá **cuáles se ven mal y con qué clase**, y vamos una por una.

**Un caso menos:** los nameplates que se veían como carteles fijos ya están arreglados, y
no era el tag de la cámara. `UI_WorldHealthbar` cacheaba `Camera.main` y solo la volvía a
resolver si era `null` — pero al spawnear, `PlayerController` **apaga** la cámara del
lobby, y un componente apagado no es `null`. Se quedaban orientándose hacia una cámara
desactivada para siempre. Ahora usa el mismo criterio que `PlayerController.MainCamera`.
