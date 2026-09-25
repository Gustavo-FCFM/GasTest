# Pendientes — actualizado el 25 de septiembre de 2026

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

# ★ HOY EN LA NOCHE: la prueba de 9 jugadores (viernes 25 de septiembre)

Es una **prueba, no la demo** (la demo es la del showcase, en diciembre): lo que vale
de esta noche es lo que la gente diga y lo que se vea en la grabación.

## Antes de que lleguen

- [ ] **Build nueva desde `main`** (`3e83c30` o más nuevo): trae la entrega de 3 s y la
      regla de que con la bolsa no se entra a la base. Ya revisado en el prefab:
      `StartSubclassWithFullUltimate` apagado (la definitiva arranca en 0) y la carga
      por rol en 0.15 / 30 / 0.2.
- [ ] **Ensayar el host con `-host`**, si todavía no lo hiciste: 10 minutos con una
      segunda máquina (o una segunda copia de la build conectándose a `127.0.0.1`).
      Nunca hosteamos desde una build; que no sea la primera vez con 8 personas
      esperando. Detalles abajo, en la sección 2.
- [ ] **playit.gg corriendo** y la dirección + puerto (7770 UDP) listos para pegar.
- [ ] **Rendimiento del host**: servidor + tu espectador + la grabación en la misma
      máquina. Si el host tartamudea, tartamudean todos: si hace falta, baja la calidad
      de la grabación antes que otra cosa.
- [ ] Bots: con 9 personas no hacen falta. Si falta alguien, llena ese lugar con uno.

**Las reglas en 30 segundos**, para pegar en el grupo antes de empezar:

> Somos 3 equipos de 3. Gana quien entregue 2 veces la caja dorada que aparece en el
> centro. Se levanta con solo acercarte; cargándola vas más lento y la R la suelta.
> Para entregar llévala a la plataforma de afuera de tu base y **quédate 3 segundos**:
> si te pegan o te sales, vuelve a empezar. Con la caja no puedes entrar a tu base. Al
> llegar a nivel 3 eliges subclase con la V (mejor en tu base). La clase solo se cambia
> dentro de tu base.

## Qué observar durante las partidas

Son las perillas y dudas que quedaron abiertas y que solo se contestan jugando:

- [ ] **La entrega de 3 s**: ¿los rivales llegan a cortarla? ¿3 s es poco o mucho?
      ¿Una herida o un veneno encima reiniciándola se siente justo o frustrante?
- [ ] **El mapa**: con la entrega de 3 s, ¿se sigue sintiendo chico? Si sí, la regla
      siguiente es que cargar la bolsa bloquee el Shift (sección 7).
- [ ] **La definitiva por rol**: ¿llega a tiempo para usarla en la partida? ¿Qué rol la
      carga demasiado rápido o demasiado lento?
- [ ] **La retroalimentación del combate**: la X, la calavera y el círculo de daño
      (`DamageRingRadius` 190). ¿Se entienden? ¿El círculo molesta?
- [ ] **El Asesino invisible** (`GhostAlpha` 0.35): ¿el que lo usa entiende que es
      invisible?
- [ ] **Apuntar al objetivo** (Blink, Enemigo jurado, Intercepción): ¿demasiado exacto
      contra alguien que se mueve?
- [ ] **Botiquines**: ¿se usan? ¿están donde pasa la gente?
- [ ] **La red con 9**: animaciones de los demás (ataques, lanzamientos, torso), lag,
      desconexiones, y los FPS del host.
- [ ] **Blink** (nuevo, sin probar): al aparecer en la espalda, la cámara gira hacia el
      enemigo y le puedes seguir pegando. ¿Marea o se siente bien?
- [ ] **Tótems del Chamán** (nuevo, sin probar): ahora tienen collider y barra de vida
      (roja para los rivales, verde para los aliados) y se pueden romper. Romper uno da
      15 de experiencia al equipo. Los de la definitiva son indestructibles
      (inmunidad real: su barra se queda llena). El equipo del tótem viaja por la red.
- [ ] **Balance**: qué clase o subclase dominó, cuál no eligió nadie, qué se sintió
      injusto.

## Después

- [ ] Anotar lo que dijeron **esa misma noche**, mientras está fresco (aunque sea en
      desorden), y guardar la grabación. En la siguiente sesión lo ordenamos y decidimos
      qué entra antes de la demo.

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

# 1. Tareas de editor — NO QUEDA NINGUNA

Revisadas contigo el 11 de septiembre. Todo lo de la lista anterior está hecho o decidido:

- **Tornado del molinete**: `Red Tornado.prefab` ya tiene `VFX_AreaVisual`
  (`RadiusAtScaleOne 4`, que con `Radius 4` del Whirlwind da escala 1 — si el tornado se
  ve más chico o más grande que el área real, ese número es el que se ajusta).
- **Hacha**: el hijo `WeaponTrail` de `2HandedAxe.prefab` se borró. El blur queda solo en
  el hacha arrojada (Spin Blur de `PF_ExampleProjectile`).
- **Rogue, giro del segundo golpe**: abandonado — se queda como está.
- **`LevelUpNotification` se queda en `None`.** El aviso por equipo que ya existe alcanza.
- **`GE_Reckless 1/2/3` y `GA_ConeReckless 1` no se renombran.** Son tres efectos
  distintos del combo, no copias accidentales.
- Ya hechas antes: el señuelo del Ilusionista tiene el avatar nuevo, y la casilla de
  `ActionSpeedMult` está marcada (con más velocidad de ataque el swing se ve más rápido).

---

# 1b. Elegir subclase: en el piso, y se interrumpe si te pegan — HECHO

Antes el menú de subclases (tecla V) se abría donde sea y en cualquier estado: si lo
abrías en pleno salto el personaje quedaba **suspendido en el aire** con el menú abierto,
porque `Update` salía antes de aplicar la gravedad.

Lo que quedó (`UI_ClassMenu` + `PlayerController.SettleWithoutInput`):

1. **El menú solo se abre con los pies en el piso** ("Aterrizá primero" si estás en el
   aire). Elegir en el aire cambiaba el Animator a mitad del salto y la animación quedaba
   trabada. Y si igual queda abierto sin input, la gravedad sigue (`SettleWithoutInput`).
2. **Recibir daño cierra el menú sin elegir** (`CloseOnDamage`, prendido por defecto).
   Mira la vida por `OnAttributeChangedCallback`, así funciona igual en el host y en un
   cliente remoto; una curación no lo cierra. Aviso: *"Te pegaron: la elección se canceló.
   Volvé a tu base y apretá V"*.
3. **Abrirlo fuera de la base avisa, no bloquea**: *"Estás fuera de tu base: si te pegan,
   el menú se cierra"*. La subclase se sigue pudiendo elegir afuera, con ese riesgo.

Probado.

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
4. Tocás **tu propio ícono** para abrir el selector de clases. Ahí, **pasar el mouse**
   por un ícono muestra su NOMBRE y **tocarlo** la selecciona y muestra su DESCRIPCIÓN
   — tocar NO la aplica: eso lo hace el botón **Confirm** de abajo. El fondo cierra el
   selector sin elegir nada.
5. **Confirmar** (el del panel, no el del selector) te pone en verde. Volver a tocarlo
   te apaga.
6. El **host** aprieta **Start** cuando quiera — el botón solo lo ve él, se pone verde
   cuando todos están listos, y la línea de estado le dice cuántos faltan.

Para salirte de un equipo, **Espectador** en la última fila de la columna de la derecha.

**El personaje aparece recién con el Start**, no al confirmar. Antes el que confirmaba
primero se quedaba dando vueltas por el mapa mientras los demás elegían.

Con `MinPlayersToStart: 1` lo podés probar solo.

## La prueba de 9 jugadores (viernes 25, en la noche) — quién hostea

**Hostear desde una build ya se puede**, y sin repartir una build que cualquiera pueda
hostear: la MISMA build abierta con el argumento `-host` muestra el botón.

```
Mercenaries.exe -host
```

(Un acceso directo con ` -host` pegado al final del destino alcanza.) Para todos los
demás, esa build sigue siendo solo-cliente: el botón no existe. Antes el candado era
`ConnectionHUD.HostOnlyInEditor`, que solo dejaba hostear dentro del editor.

**De espectador no ocupás lugar.** En la sala, "Espectador" es una fila aparte: no cuenta
para el cupo de los equipos, no frena el arranque y **no te spawnea personaje** (el
personaje nace recién al confirmar equipo y clase). Así que 9 jugadores son 3c3c3 exactos
y vos mirás desde afuera. El botón de **Start lo aprieta el host**, o sea vos, aunque
seas espectador.

Los controles de la cámara de espectador están en `SpectatorCamera`: WASD para moverse,
Espacio sube, Ctrl baja, Shift para ir rápido, clic izquierdo/derecho para saltar de
jugador en jugador, **F** vuelve a la cámara libre, **H** muestra el nombre y la vida de
quien mirás, **M** el marcador.

- [ ] **Ensayarlo antes de la prueba**, aunque sea con dos máquinas: nunca hosteamos desde
      una build. El código del host es el mismo, pero es la primera vez que corre sin
      editor, y no es el día de averiguarlo.
- [ ] Acordate de la IP: los que se conectan necesitan tu dirección de playit.gg
      (puerto 7770 UDP), no `127.0.0.1`.
- [ ] Mirá el rendimiento: esa máquina va a llevar el servidor, tu cliente de
      espectador y la grabación al mismo tiempo.

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

`UI_LobbyMenu.cs` y su prefab **siguen en uso en `Test_Network.unity`**: son lo único
con lo que se entra a la partida en esa escena (no tiene `LobbyManager` ni
`UI_LobbyPanel`, solo el `NetworkGameManager`). Se borran el día que se jubile esa
escena, o cuando se la modernice con la sala nueva.

No hay nada que rescatar del prefab: las tres clases que guardaba
(`Class_Barbarian`, `Class_Rogue`, `Class_Paladin`) ya están idénticas en el
`UI_LobbyPanel` de la escena del modo.

---

# 3. Balance pendiente (decisiones tuyas)

## Carga de la definitiva por rol — PROBADO ✅

Antes, la definitiva cargaba solo con los golpes (1 s por golpe de los 180 del cooldown)
y con el tiempo: tardaba demasiado. Ahora además carga por **hacer el rol** de la clase:

| Rol | Carga al… | Perilla (en el PlayerController del prefab) |
|---|---|---|
| Tanque (Bárbaro y sus subclases) | aguantar daño de un enemigo | `TankChargePerDamage` = 0.15 s por punto |
| Daño (Pícaro y sus subclases) | matar a un personaje enemigo (jugador o bot) | `DamageChargePerKill` = 30 s por baja |
| Soporte (Paladín y sus subclases) | curar a un aliado | `SupportChargePerHeal` = 0.2 s por punto |

El rol es un campo nuevo de la clase, **`Role`** en el `CharacterClassDefinition`. Ya lo
cargué en las 9 subclases; las 3 clases base quedan en `None` (no tienen definitiva, así
que no cargan nada). Al crear una clase nueva, elegirle el rol ahí.

Lo que **no** cuenta, a propósito: curarse a uno mismo, curar de más a alguien que ya
estaba lleno, el daño de una zona del mapa, la vida de un botiquín o de la base, y matar
monstruos.

**Dos reglas nuevas al cambiar de clase:**

- **La subclase arranca en 0.** Antes arrancaba llena sin querer: equipar una clase
  borra todos los efectos, el cooldown de la definitiva incluido, y sin cooldown puesto
  la definitiva está lista.
- **Cambiar de clase a mitad de partida conserva la MITAD** de la carga que tenías, y
  con esa mitad arranca la próxima subclase. Así no se puede cargar la definitiva con
  una clase y gastarla con otra. La perilla es `UltimateKeptOnClassChange` (0.5).

**Para probar rápido**: `StartSubclassWithFullUltimate` en el PlayerController hace que
la subclase arranque con la definitiva lista. Apagarlo para jugar en serio.

- [x] Probado: la carga por rol funciona.
- [x] Soporte: curar a un aliado herido (sube) y a uno lleno (no sube).
- [x] Daño: matar a un bot (sube 30 s de golpe) y a un monstruo (no sube).
- [x] Elegir subclase: la definitiva tiene que arrancar vacía.
- [x] Cargar un poco, volver a la base, cambiar de clase y elegir otra subclase: tiene
      que arrancar con la mitad.
- Los tres números quedaron como estaban. Si en una partida larga la definitiva se
  siente lenta o regalada, son `TankChargePerDamage`, `DamageChargePerKill` y
  `SupportChargePerHeal` en el `PlayerController` del prefab.

Un detalle que queda afuera: las bajas que hace una **invocación** (las copias del
Ilusionista, los tótems) no le cuentan a su dueño, porque el que pegó para el juego es
la invocación. Si se nota, se arregla anotando al dueño como atacante.

## Botiquines del mapa — PUESTOS, falta acomodarlos y jugarlos

Los de Overwatch / Marvel Rivals: una cruz verde que flota, te da **75 de vida** al
pasarle por encima y desaparece 20 segundos, con un disco en el piso que se llena como
una gráfica de pastel mientras vuelve. Existen para que un tanque o un DPS se recuperen
sin caminar hasta la base, y para dar puntos del mapa por los que vale la pena pelear.

`GameMode/HealthPack.cs` y `HealthPackVisual.cs` (en `GameMode/`, no en `Mercenaries/`:
no saben nada de equipos ni de objetivo, sirven a cualquier modo).

**Por la red viaja un solo número**: el tick en que vuelve a estar listo. De ahí sale
todo lo demás en cada cliente —si mostrar la cruz o la cuenta regresiva— sin más
tráfico. El servidor es el único que cura.

Para instalarlos:

- [x] **`Mercenarios ▸ Instalar botiquines`** con la arena abierta. Pone 9: seis en un
      anillo alrededor de la meseta y tres más afuera, sobre los carriles. Cada uno se
      apoya en el piso con un rayo y se corrige al NavMesh; los que caen en una sala
      segura se saltean.
- [x] **Volver a correr el mismo ítem del menú** (23 de septiembre): ya aparecen los
      nueve. La primera vez salía uno solo porque crear nueve NetworkObject de un saque
      por código dejó a ocho con `SceneId 0`, y para FishNet un NetworkObject sin SceneId
      no es un objeto de escena — no se spawnea. Ahora la herramienta se lo asigna a
      todos, y volver a correrla sobre una escena que ya los tiene los repara.
- [ ] **Moverlos a mano.** Son un punto de partida: el buen sitio para un botiquín se
      descubre jugando (detrás de una cobertura, en el desvío de un carril), no
      calculando ángulos.

Perillas por botiquín: `HealAmount` (75), `RespawnSeconds` (20), `PickupRadius` (1.6),
`OnlyIfHurt`, `Tint`, y `CrossVisual` si querés un modelo en vez de la cruz de barras
que se arma por código. El sonido sale de `AudioLibrary.HealthPack` salvo que el
botiquín traiga el suyo.

Qué mirar al probar: que 75 sea la cantidad justa (un tercio de un tanque, casi toda la
vida de un pícaro), y que 20 segundos no los vuelva un chorro infinito de vida en la
pelea del centro. Si el centro se vuelve inmortal, lo primero que subiría es el
respawn, no lo que curan.

## Armadura y ritmo de ataque — CARGADO Y PROBADO (21 de septiembre)

El sistema de números es D&D con una variable más: el **tiempo**. Cada clase se define por
tres números —dado, intervalo y clase de armadura— y todo lo demás se deriva:

- **Vida** = dado × 10; por nivel, (dado/2 + 1) × 10.
- **Golpe** = dado (el ataque sube +1 por nivel, como la competencia).
- **Armadura** = CA − 10, y mitiga `Def / (Def + 10)` **del daño físico** (la magia la
  ignora: es el contrapeso natural de las clases blindadas). Antes era una resta fija,
  que con golpes de 6 no convivía.
- **Intervalo** = la palanca que D&D no tiene: DPS = golpe ÷ intervalo.

| | Vida | Golpe | Cada | DPS | CA → Def | Mitigación | Vida efectiva |
|---|---|---|---|---|---|---|---|
| Bárbaro | 120 | 12 | 1.2 s | 10 | 15 → 5 | 33 % | 180 |
| Pícaro | 80 | 7 | 0.75 s | 9.3 | 13 → 3 | 23 % | 104 |
| Paladín | 100 | 8 | 1.0 s | 8 | 18 → 8 | 44 % | 179 |

Las subclases llevan sus valores de **nivel 3** en su `ASDef` (vida = base + 2 niveles,
golpe = base + 2) porque al evolucionar se conserva el nivel y no se vuelve a aplicar el
crecimiento. Las que tienen un golpe propio a propósito se dejaron: Berserker 7 a 0.5 s,
Conquista a 1.2 s, Venganza a 0.8 s.

**El benchmark para balancear**: `TTK = vida efectiva del rival ÷ mi DPS`. Con básicos,
dos iguales tardan ~18 s; el bárbaro o el pícaro matan a un pícaro en ~10. Las
habilidades son las que acortan eso. Si se siente lento, la palanca es `ArmorConstant`
(AbilitySystemComponent), no los dados.

Pendiente de decidir jugando:
- Los NPCs no tienen armadura y ahora pegan un 23–44 % menos *de facto* a los jugadores.
  Si el centro se siente blando, subirles el ataque ~30 % (fantasma 12 → 16, jefe 50 → 65;
  el mago es mágico, no hace falta).
- Con +70 de vida y +1 de golpe por nivel, los duelos a nivel 3 duran casi el doble que a
  nivel 1. Si eso se siente esponjoso: golpe +3/+2/+2 por nivel en vez de +1.
- La cura del paladín escala con su golpe: si "cura poco" se vuelve "no sirve", la salida
  es un componente fijo por golpe, no subirle el dado.

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

**Los bots** (`GameMode/Mercenaries/BotController.cs`) son jugadores de verdad: mismo prefab, mismo
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

**La cámara espectador** (`Player/SpectatorCamera.cs`) se enciende sola cuando tu fila
de la sala dice Spectator y la partida arrancó. Vuelo libre WASD + Espacio/Ctrl + Shift,
clic izquierdo/derecho para seguir jugadores, `F` para volver a libre, `H` para el panel
del observado y `M` para el marcador.

### Lo que falta verificar

Lo primero al retomar.

- ~~La cámara espectador~~ — PROBADA Y APROBADA.
- ~~Que ya no se salgan del mapa~~ — PROBADO (21 de septiembre), junto con los paladines
  y el balance de NPCs.
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
- **El `unhandled PacketId of 0` de FishNet — RESUELTO.** No era de red: `GA_IceAoE` tenía
  `TickInterval 0` y `GA_ContinuousAoE` lo tomaba como "cada frame, para siempre" (el
  jefe mataba de un golpe a un Berserker de 260 y la red se partía en `Split`). El asset
  pasó a `GA_InstantAoE` y el continuo ahora trata un tick en 0 como una sola aplicación.

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

## 3º · Sonido — CÓDIGO TERMINADO, faltan los clips

El mayor salto de calidad percibida por hora invertida. Un juego mudo se lee como
prototipo aunque todo lo demás esté bien.

Todo el sistema está escrito y compila **sin un solo archivo de audio**: cada sonido es
un `SfxCue` que, vacío, es silencio. Ver el LEEME del modo, sección "El sonido". Lo que
queda es de **la máquina de casa** (la del trabajo no tiene con qué trabajar audio):

1. **`Mercenarios ▸ Crear la biblioteca de audio`** con la arena abierta: crea
   `Resources/AudioLibrary.asset` y le pone el `AudioListener` a `Camara_Lobby` (sin
   eso el menú y la sala no suenan).
2. **Descargar y cargar**, por prioridad. Formato: WAV mono 44.1 kHz para los efectos
   cortos (mono es obligatorio para que el 3D funcione), OGG para música y loops.
   Fuentes: Sonniss GDC bundles, Kenney (CC0), freesound.org con filtro CC0, y en el
   Asset Store "RPG Essentials Sound Effects" / "Free Casual Game SFX".
   - **Lo que suena siempre** (en `AudioLibrary`): pasos ×3–4, salto, aterrizaje, golpe
     recibido ×2, muerte, subir de nivel, clic de UI.
   - **Habilidades por familia** (en cada `GA_*`: `CastSound` al lanzar, `ImpactSound`
     al impactar): whoosh liviano (dagas) y pesado (hacha/martillo), espada, fuego,
     hielo, sagrado, sombra, salto/caída, dash, molinete, cañón, tótem, "poof" mágico.
     No hace falta una por habilidad: ~10 familias cubren las 77.
   - **Efectos con duración** (en cada `GE_*`: `TargetSound`): quemadura, stun,
     escudo, sigilo.
   - **Partida** (en `AudioLibrary`): inicio, cuenta atrás, Objetivo aparece / tomado /
     cae / entregado, aniquilación, nivel de equipo, victoria, derrota. Un loop de
     viento para el ambiente.
   - **Música**: `MenuMusic` y `BattleMusic`. La de batalla la está haciendo un amigo:
     cuando llegue, se arrastra y listo.
3. Jugar con bots y ajustar volúmenes por cue (`Volume` en cada `SfxCue`) y las
   distancias (`MaxDistance`: un paso 20 m, un grito 40, una explosión 60).

### El ritmo de la sacudida se separó del sonido — HECHO Y PROBADO ✅

La reacción se sentía como un temblor continuo: con la velocidad de ataque de varias
clases, cada golpe pasaba el filtro y el personaje no paraba de sacudirse.

El respiro estaba en `AudioLibrary.HurtCooldown` (0,45 s) y gobernaba **las dos cosas**,
así que subirlo para que dejara de temblar también espaciaba el "ay" de cada golpe. Ahora
son dos ritmos independientes:

| Perilla | Dónde | Qué controla |
|---|---|---|
| `HitReactionCooldown` | `PlayerController`, en el prefab del Player | La **sacudida**. **1 s** |
| `HurtCooldown` | `AudioLibrary` | El **sonido**. 0,45 s |

La de la animación vive en el prefab a propósito: se puede tocar **sin** crear el asset de
audio, que todavía no existe. En 0 reacciona a todos los golpes.

El reloj se marca DESPUÉS de los descartes, no antes: un golpe que no llegó a animarse
(escudo arriba, molinete en curso) no consume el respiro y no se come la reacción del
golpe siguiente, que es el que sí tenía que verse.

- [x] Número elegido jugando: **1 s**. Quedó también como default del código, para que
      el prefab y el script no digan cosas distintas.

### Reacción de golpe, aturdido, muerte y ragdoll — HECHOS Y PROBADOS ✅

Todo instalado en el Animator y en el prefab del jugador (22 de septiembre):

- **Reacción de golpe**: `HitTrigger` → `PLACEHOLDER_Hit` en la capa UpperBody; cada AOC
  pone el `CombatDamage` de su postura. Los estados con etiqueta **`Loop`** (escudo
  arriba, vuelo del salto, aturdido, molinete) no se cortan por un golpe — lo decide el
  código, no el Animator (`HitBlockedByStateTag`).
- **Aturdido**: bool `IsStunned` leído del tag en todas las copias → `PLACEHOLDER_Stun`
  en bucle.
- **Muerte con ragdoll**: el prefab tiene el Ragdoll Wizard corrido y `RagdollController`
  en la raíz. El cuerpo sale despedido en dirección contraria a quien lo mató, y **la
  cámara lo sigue** mientras cae (`CameraFollowsRagdoll` en el PlayerController, apagable).
  La animación de muerte (`IsDead` + `PLACEHOLDER_Death` con un clip al azar de
  `DeathClips`) queda de respaldo para un prefab sin ragdoll.
- **Morir limpia buffs y debuffs; revivir devuelve el kit** (cooldown a cero, cargas
  llenas) menos la R. El Inmortal también lo recibe al levantarse con su definitiva.

### Inclinar el torso hacia la mira — PROBADO ✅ (se inclina en las dos ventanas)

`Player/UpperBodyAim.cs`: mirando al cielo el personaje se arquea hacia atrás, mirando
al piso se encorva. El yaw no se toca (de eso ya se encarga `FaceCameraForward`). Se
aplica en `LateUpdate`, encima de la pose que haya animado el Animator, así funciona
igual corriendo, atacando o con el escudo arriba.

- [x] En `AC_Player`, parámetro **Float** llamado **`AimPitch`**.
- [x] En el prefab del jugador, `UpperBodyAim` en la **raíz**.
- [x] **Probarlo con dos ventanas**: que el OTRO personaje también se incline al apuntar
      arriba o abajo, no solo el propio.

**Cómo viaja por la red** (cambió): el dueño manda el ángulo por un RPC no confiable a
~15 por segundo (un byte), y los demás lo suavizan. El plan era que viajara gratis en el
parámetro del Animator, por el `NetworkAnimator` del prefab — **está inerte**: ver abajo.
El parámetro `AimPitch` se sigue escribiendo igual, pero solo para poder mirar el valor
en la ventana del Animator mientras se prueba.

Perillas: `Weights` (cuánto del ángulo lleva cada hueso — repartido se ve natural, todo
en uno se ve quebrado), `MaxUp` 50° / `MaxDown` 40°, `Smooth` 0.08 s, y `SendRate` 15.

No se aplica con el personaje muerto ni aturdido.

### El NetworkAnimator estaba inerte y la locomoción no viajaba — ARREGLADO Y PROBADO ✅

Confirmado con dos ventanas el 22 de septiembre: los demás jugadores **se deslizaban en
idle** en vez de caminar. Las animaciones de habilidad, golpe, stun y muerte sí se veían
—esas van por las RPC del `NetworkASC`—, pero caminar, correr, saltar y caer no.

La causa: el `NetworkAnimator` del prefab tiene su campo **Animator vacío**, y el
Animator del personaje vive en un hijo (el modelo). FishNet, con el campo vacío, prueba
un `GetComponent` en **su propio** GameObject, no lo encuentra y se da por vencido — sin
un solo warning, porque lo tiene comentado en su código. Y `UpdateAnimations` corre solo
en el dueño, así que nadie más escribía esos parámetros. Lo más probable es que la
referencia se haya perdido al cambiar el modelo, igual que el `WalkAnimator` del clon del
ilusionista.

**Cómo quedó**: los cinco parámetros de caminar (`Speed`, `MoveX`, `MoveY`, `IsJumping`,
`VerticalSpeed`) los manda ahora quien manda sobre el personaje —el dueño si es un
jugador, el servidor si es un bot— por RPC no confiable a 15 por segundo, y solo cuando
cambian: parado no gasta un paquete. El resto los recibe y los aplica con el mismo
suavizado que usa el dueño.

**Por qué no revivimos el NetworkAnimator**, que es literalmente para esto: asignarle el
Animator prende de golpe la sincronización de TODOS los parámetros, encima de las RPC
que ya mandan las animaciones de habilidad, golpe, stun y muerte — habría que ir
marcando a mano cuáles ignorar, y cada parámetro nuevo sería una trampa nueva. Mandar
cinco floats nosotros es más barato y no toca nada de lo que ya funciona.

**De paso**: la velocidad de ataque (`AttackSpeedMult`) tampoco se aplicaba en las copias
ajenas, así que los ataques de los demás se veían siempre a velocidad 1, ignorando sus
buffs. Eso no necesita red —el atributo ya está sincronizado— y ahora cada copia lo lee
de su propio ASC.

- [x] Probado con dos ventanas: ahora caminan bien.
- [ ] Si querés, sacar el `NetworkAnimator` del prefab del jugador: no lo usa nadie y ya
      nos costó un día. Dejarlo tampoco hace daño — está inerte.

### Dos animaciones que mentían, las dos por la misma línea — PROBADO ✅

Salieron de probar el Pícaro entre dos ventanas:

1. **El primer golpe del combo no se veía en la pantalla del otro**, solo el segundo.
2. **El Golpe mortal (Q) hacía su animación apuntando a la nada**, sin hacer nada.

Las dos venían de que `ServerActivateAbility` mandaba la animación de la habilidad a
los observadores **siempre**, pasara lo que pasara dentro de `Activate()`:

- En un combo, cada paso ya manda la suya. La del padre llegaba un frame después de la
  del primer paso y la pisaba. Ahora un combo dice `BroadcastsOwnAnimation` y el
  servidor no manda nada encima.
- El Golpe mortal sin nadie a tiro sale de `Activate()` sin comprometer cooldown ni
  costo — pero igual se mandaba la animación. Ahora toda habilidad marca
  `CommittedThisActivation` al comprometerse, y sin eso no se anima: animar algo que no
  pasó le miente al que mira y encima delata la posición del que falló.

Y del lado del DUEÑO había una segunda fuente, aparte: el cliente remoto **anticipa** la
animación al apretar, antes de que el servidor conteste. Ahora pregunta primero
(`CanPredictActivation`), y las de objetivo único contestan que no cuando no hay nadie a
tiro. Quedaron cubiertas las cuatro: Golpe mortal, Intercepción heroica, las de
`GA_Target` y Enemigo jurado (esa ya lo hacía por su `CanActivate`).

- [x] El combo del Pícaro entre dos ventanas: se ven los dos golpes.
- [x] Probar la Q del Pícaro apuntando a la nada: no tiene que animar nada, ni en tu
      pantalla ni en la del otro. Y apuntando a alguien, igual que siempre.
- [x] De paso, la Intercepción heroica del Paladín sin aliado a la vista.

### El hacha quedaba en la mano mientras el proyectil ya volaba — PROBADO ✅

**Solo le pasaba a los que se CONECTAN**, no al host, y esa es toda la pista.

El arma se esconde de la mano en el frame exacto en que la animación la suelta — eso lo
decide el servidor con su propia cuenta. Pero el dueño REMOTO anticipa su animación al
apretar, antes de que el servidor se entere. Entonces: él ve el lanzamiento, el pedido
viaja, el servidor cuenta hasta el frame de soltar, y el aviso de "escondé el hacha"
vuelve **dos viajes tarde**. En el medio, el hacha seguía en la mano mientras el
proyectil ya estaba en el aire: dos hachas al mismo tiempo. En el host no se ve nunca,
porque ahí el viaje es cero.

**Cómo quedó**: el dueño hace la misma cuenta de su lado y esconde el arma en su propio
frame de soltar (`PredictWeaponHide`), con los mismos números que usa el servidor
(`GA_ProjectileShoot.ResolveThrowTiming`, que ahora lo calcula una sola vez para las dos
puntas). Y el aviso del servidor ya no se le manda al dueño, para que no se peleen.

La rutina del dueño **siempre** devuelve el arma a la mano, aunque el servidor rechace
la habilidad: no hay forma de quedarse sin arma.

- [x] Probar el hacha del Bárbaro desde un cliente conectado (no el host): que salga de
      la mano en el mismo frame en que la animación la lanza.
- [x] Lo mismo con las dagas del Pícaro, el Asesino y el Ilusionista (`HideWeaponWhileFlying`).

Queda una diferencia que NO se arregla así: el proyectil en sí sigue apareciendo dos
viajes después de tu animación, porque lo spawnea el servidor. Con el arma ya escondida
no se nota (ves salir el hacha y el proyectil aparece un pelo más adelante), pero si con
mucha latencia sigue molestando, el paso siguiente sería que el dueño dibuje un
proyectil suyo, solo visual, y el de red lo releve al llegar.

### Sensación del ataque básico — HECHO Y PROBADO ✅

- **Sostenido**: con el LMB o el gatillo apretado, el ataque principal se repite solo
  cada vez que vuelve a estar disponible (`AutoRepeatPrimaryAttack`, apagable).
- **Cancelable**: cualquier otra habilidad corta el básico a mitad del swing. Lo que ya
  pegó, pegó; los `HitFrame` que faltaban no salen; el cooldown ya pagado no se
  devuelve (no hay animation cancel gratis). Un enemigo recibe un solo golpe por swing
  aunque el clip tenga varios eventos.

### Los lanzamientos también se cancelan — PROBADO ✅

Mismo mecanismo (`IsInterruptible` + `CancelSerial`), ahora marcable **en el asset** en
vez de solo en el slot `PrimaryAttack`. Ya está puesto en el hacha del Bárbaro y en las
dagas del Pícaro, el Asesino y el Ilusionista.

Hay dos momentos y significan cosas distintas:

- **Antes de soltar** → el proyectil no sale. Es la finta. El cooldown ya pagado no se
  devuelve: cancelar cuesta, igual que en el básico.
- **Después de soltar** → el proyectil ya está en el aire y se queda. Lo único que se
  saltea es el remate de la animación, que es lo que hoy te obliga a esperar. Ahí está
  la ganancia de ritmo: tirás y encadenás sin que el brazo termine de volver.

El arma vuelve a la mano al instante en los dos casos, también en la pantalla del que
canceló (su predicción se corta con `CancelWeaponHide`).

**El permiso se mira en DOS lados**, y por eso la primera versión no funcionaba: el
servidor corta la habilidad, pero antes el CLIENTE tiene que dejar leer el botón de la
habilidad nueva mientras hay una corriendo (`_attackInterruptible` en
`HandleAbilityInput`). Eso último solo se activaba para el slot del ataque principal, así
que el pedido no salía nunca del cliente y marcar o desmarcar la casilla daba igual. Si
agregás otra habilidad interrumpible y "no pasa nada", mirá ahí primero.

- [x] Probado: se puede interrumpir el lanzamiento.
- [x] Probado: el básico corta un lanzamiento y el combo se sigue encadenando.
- [x] Probar el encadenado: tirar y meter el dash apenas sale el proyectil.
- [x] Que el arma no se quede escondida ni aparezca dos veces en ninguno de los dos.
- [x] **El cooldown ahora empieza al SOLTAR**, no al apretar (ver abajo). Probar que
      fintar no deja la habilidad en cooldown, y que mantener o repetir el botón del
      hacha NO reinicia el lanzamiento.
- [ ] Decidir si el **rayo del Paladín** (`GA_SmiteBeam` y el de Conquista) también
      debería cancelarse. Es marcar la casilla en esos dos assets, pero cambia cómo se
      siente la clase y esa es tu decisión, no mía.

**El cooldown de un lanzamiento empieza al SOLTAR**, no al apretar el botón. Fintar
dejaba la habilidad en cooldown varios segundos por un hacha que nunca salió: se pagaba
la intención, no el hacha. Ahora se paga lo que salió.

No se puede abusar, y el motivo es estructural: **para cancelar hay que activar OTRA
habilidad**, y esa tiene su propio costo. No existe un botón de "cancelar" suelto. Y
repetir el botón del lanzamiento no reinicia nada, porque una habilidad no se corta a sí
misma (`CheckAbilityButton`). Lo peor que se puede hacer es amagar gastando el cooldown
de otra cosa, que es mal negocio.

Efecto en el balance, que conviene mirar jugando: entre dos hachas ahora pasa (tiempo de
soltar + cooldown) en vez de solo el cooldown — unos 0,7 s más en el hacha del Bárbaro.
Si se siente lenta, se baja su `CooldownDuration`. Y el ícono del HUD se queda encendido
durante el envión, que es lo correcto: todavía no gastaste nada.

**El ataque básico TAMBIÉN corta** (desde el 23 de septiembre): apretar LMB con un
lanzamiento en curso lo cancela y empieza el swing. Lo único que no puede cortar es a sí
mismo — el cliente no deja ni leer el botón mientras corre su propio combo, que si no
apretar LMB lo mataría en vez de encadenarlo (`_runningSlot` en `HandleAbilityInput`).

## 4º · Pantalla de inicio y ajustes — TERMINADO Y PROBADO ✅

Está en `Scripts/Settings/ y Scripts/UI/` (ver el LEEME del modo, sección "El menú principal y los
Ajustes"). **El menú es un panel sobre la arena, no otra escena**: de fondo se ve el
mapa desde la cámara de la sala girando encima de la meseta, con blur. Jugar lo
esconde y queda el recuadro de red de siempre; desde ese recuadro (ESC) están
**Ajustes** y **Menú principal**. En el menú, ESC abre los Ajustes directo.

Probado: menú → Jugar → hostear → Menú principal → Jugar → hostear otra vez.

Perillas, por si querés tocarlas:

- **La órbita**: en `Camara_Lobby` → `MenuOrbitCamera` (`Height 22`, `Radius 18`,
  `DegreesPerSecond 3`). Sigue girando en la sala de espera y se queda quieta al
  arrancar la partida.
- **El blur**: `BlurEnabled` y sus tres números en el mismo componente. Si no se ve, el
  renderer de URP no tiene post-procesado (`PC_Renderer` → Post Process Data).
- **El velo del menú**: `UI_MainMenu.BackdropColor` (transparente para ver el mapa limpio).
- **Probar la arena sin pasar por el menú**: `UI_MainMenu.ShowOnStart` apagado.
- **La resolución y el modo de pantalla** solo se ven en una build (en el editor
  `Screen.SetResolution` no hace nada).
- **Música y efectos** todavía no suenan: son los ganchos para el 3º
  (`GameSettings.MusicVolume` / `SfxVolume` + `OnChanged`). El general sí
  (`AudioListener.volume`).

### Lo que se arregló de paso: hostear dos veces en el mismo Play

Desconectar → Iniciar Host nunca había andado bien; el menú solo lo hizo visible. Al
parar el servidor, FishNet destruye los personajes y el Objetivo pero **no toca el estado
de los managers de escena**: `LobbyManager` seguía con `MatchStarted = true` y las filas
de la sala vieja, y el modo con puntajes y jugadores. Al hostear de nuevo la preparación
arrancaba sola, sin sala, y nadie spawneaba.

Ahora los tres (`LobbyManager`, `NetworkGameManager`, `MercenariesGameMode`) reinician
su sesión en `OnStartServer`. **Si agregás un manager de escena con estado de partida,
que también lo limpie ahí** — está como regla en `CLAUDE.md`.

### Los botones que se muestran siguen al control elegido — HECHO Y PROBADO ✅

En Ajustes, arriba de todo: **Controles ▸ ¿Qué estás usando?** con *Teclado y mouse*,
*Control de Xbox* y *Control de PlayStation*. Lo que se elija ahí decide cada botón que
el juego **dibuja**:

- Los slots del HUD (`Q E R Shift LMB RMB` → `RB LB Y B RT LT` → `R1 L1 TRI CIR R2 L2`).
- El aviso de subir de nivel: "Presiona **V** / **VIEW** / **CREATE** para elegir una
  Subclase", y el de "te pegaron, volvé a tu base y apretá …".
- Los números `[1] [2] [3]` de las tarjetas de clase y de la lista de subclases: son una
  ayuda de teclado, así que con control **se esconden** (ahí se elige con el stick y el
  botón de confirmar).

**No cambia ningún binding.** El juego sigue escuchando teclado Y control a la vez, como
siempre: si ponés "Xbox" y agarrás el teclado, la Q funciona igual, solo que el HUD dice
RB. Es a propósito — adivinar el dispositivo cada frame hace parpadear el HUD cuando
apoyás la mano en el teclado sin querer.

Todo sale de **una sola tabla**, `UI/InputGlyphs.cs`. Si cambiás un binding en
`InputSystem_Actions`, ese archivo es el único que hay que tocar.


**Los símbolos de PlayStation van como texto** (`TRI`, `CIR`, `X`) y no como △ ○ ✕: la
fuente TMP del proyecto no los tiene y saldrían como cuadraditos, el mismo problema que
tuvimos con las flechas ◀ ▶. Si algún día querés los botones dibujados de verdad, se
hace un *sprite asset* de TMP y se cambia solo esa tabla (TMP los mete en línea con
`<sprite name="...">`): el resto del juego lo hereda.

## Paquetes: qué se sacó y qué queda

Se sacaron del `manifest.json` (22 de septiembre): **Vivox** (el error HTTP 400 al dar
Play, chat de voz que nunca se usó), **Multiplayer Center** y su **quickstart** (el
asistente de recomendaciones que dejó `UserChoices.choices`). Ninguna línea del proyecto
los usaba.

**El Multiplayer Play Mode se queda**: es el que abre las ventanas extra del editor para
probar de a varios, y su única dependencia dura es newtonsoft-json.

Quedan dos que tampoco usa nadie pero no molestan: `multiplayer.widgets` (es el que
regenera la carpeta `Assets/Multiplayer Widgets` cada vez que se la borra) y
`services.multiplayer`, del que depende. Si la carpeta reaparece y estorba, se sacan los
dos juntos.

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

### Los tiros de cerca salían torcidos — ARREGLADO Y PROBADO ✅

Lanzando el hacha o disparando a algo que tenías pegado, el proyectil no salía de
frente: se iba al costado o para arriba, con un ángulo raro.

**Dos causas distintas, las dos arregladas:**

**1. La retícula sale de la CÁMARA y el proyectil de la MANO.** La cámara va atrás y al
hombro; el arma, adelante y al centro. Esas dos líneas convergen bien de lejos, pero
cuanto más cerca está el objetivo, más se abren — a dos metros, apuntarle al pecho a
alguien hacía salir el hacha unos veinte grados al costado. Pegaba, pero se veía mal.

Ahora no se apunta al punto crudo sino a un punto del **mismo rayo** pero a una
distancia mínima de la mano (`MinConvergeDistance`, 4 m). La dirección queda casi
paralela a la retícula, que es lo que el jugador espera ver, y como el objetivo está
sobre esa misma línea, le pega igual. Es el arreglo de siempre en tercera persona.

**2. El rayo de la mira chocaba con lo que hubiera ENTRE la cámara y vos.** La cámara va
varios metros atrás, así que el rayo atraviesa todo ese tramo primero. Un compañero
parado detrás tuyo —cosa de todos los partidos en un 3c3c3— o una pared contra la que la
cámara se apoya daban un punto de mira **detrás** del jugador: el personaje giraba al
revés y lo que lanzabas salía para atrás. Ahora se descarta todo lo que esté más cerca
que el propio personaje.

Ese segundo arreglo es de `GetAimPoint`, así que vale para TODO lo que use la mira:
girar el cuerpo, las áreas en el piso, el dash, las de objetivo único.

- [x] Probado: los tiros salen derechos.
- [x] La pistola del Pirata (usa el mismo arreglo) y las dagas del Pícaro.
- [x] Con un compañero parado justo detrás tuyo, apuntar y lanzar. Ese era el caso del
      tiro que salía para atrás.
- [ ] Si de cerca ahora se siente que el tiro "no obedece" (pasa al lado del que tenías
      pegado), bajá `MinConvergeDistance` en el asset. Más alto = más derecho; más bajo =
      más literal.

### Apuntar a un objetivo elegía al más cercano — ARREGLADO Y PROBADO ✅

Con el Golpe mortal (Q del Pícaro), apuntando a un enemigo del fondo entre otros dos,
siempre agarraba al que tenías más cerca aunque no estuviera en la retícula.

**Cómo elige** (`GameplayAbility.FindBestTargetInAim`, la misma para Golpe mortal,
Intercepción heroica, Enemigo jurado y las de `GA_Target`): junta a todos los candidatos
dentro del alcance, descarta a los que están fuera de un cono, y de los que quedan se
queda con **el más centrado en la mira**. Nunca fue "el más cercano" a propósito.

**Por qué salía el más cercano igual**, dos errores que se sumaban:

1. **El ángulo se medía desde los PIES del personaje**, no desde la cámara. La cámara
   está detrás y al hombro, así que la línea cámara→retícula y la línea
   personaje→retícula son distintas, y cuanto más cerca está el objetivo más se abren.
   A corta distancia esa diferencia se comía el cono entero.
2. **El punto de mira se pedía con el alcance de la habilidad** (10 metros). Pero el rayo
   sale de la cámara, que ya está unos 5 metros detrás: llegaba apenas más allá del
   personaje y, si no chocaba con nada, devolvía un punto casi encima de él. La
   dirección salía de ahí y era ruleta.

**Cómo quedó**: el ángulo se mide **desde la cámara, sobre su propio rayo** — o sea,
contra lo que el jugador ve en la retícula — y el punto se pide a 200 metros para que la
dirección sea estable. El alcance se sigue midiendo desde el personaje, que es lo
correcto. Y se apunta al **torso** del candidato y no a sus pies, que desde una cámara
que mira un poco hacia abajo quedan lejos de la retícula.

Para que el servidor resuelva con el mismo rayo que vio el dueño, ahora viaja también el
ORIGEN de la mira con el pedido de activación (`NetworkAimOrigin`), no solo el punto. Un
bot, que no tiene cámara, usa los ojos de su propio personaje.

- [x] Probado con el Golpe mortal: ahora agarra al de la retícula.
- [x] Enemigo jurado e Intercepción heroica probados
      (el de 20 metros de alcance, el más largo, era el que más preocupaba).
- [ ] Si ahora se siente DEMASIADO exacto —cuesta agarrar a alguien en movimiento—, el
      `SelectionAngle` de cada asset es la perilla: 25° en el Golpe mortal, 30° en el
      resto. Ese número ahora significa de verdad "grados desde la retícula".

**Lo que NO hace**: comprobar que haya línea de visión. Apuntando a un enemigo detrás de
una pared, si entra en el cono, lo va a elegir igual — y el Golpe mortal te teletransporta
ahí. Si aparece en la prueba del jueves, se arregla con un raycast.

### Invisible se NOTA en tu propia pantalla — HECHO Y PROBADO ✅

Estando invisible (Emboscada sombría del Asesino) tu modelo se ve **fantasma**: pasa a
semitransparente y deja de proyectar sombra. El ícono del buff en el HUD se pierde en
medio de una pelea; el propio cuerpo no.

Quién ve qué, todo en `Player/PlayerVisibility.cs` y sin nada nuevo por la red — el tag
ya viaja, y cada pantalla decide sola según la afiliación:

| Quién mira | Qué ve |
|---|---|
| Un enemigo | Nada (Renderers apagados, como siempre) |
| Vos mismo | Fantasma |
| Un aliado | Normal, o fantasma si prendés `GhostForAllies` |

Perillas en el prefab del Player: `GhostAlpha` (0.35) y `GhostForAllies` (apagado). Lo
de los aliados lo dejé apagado porque pediste que se vea para vos; prenderlo hace que tu
equipo sepa de un vistazo que el enemigo no te ve, y vale la pena probarlo.

Solo se vuelven fantasma las MALLAS. Las partículas y las estelas quedan como están:
sus materiales son de efecto y convertirlos a transparente los rompe.

**El material fantasma se arma sobre URP/Lit**, no sobre el original. El cuerpo del
personaje usa un material del pack de Kevin Iglesias con un shader HEREDADO de Unity: no
tiene `_BaseColor` ni `_Surface`, y bajarle el alfa no hace nada. En la primera versión
eso hacía que solo las armas se volvieran fantasma. Copiando la textura y el color sobre
URP/Lit funciona igual venga del pack que venga.

- [x] Probado con el Asesino: el personaje entero se ve fantasma.
- [ ] Perillas por si querés afinarlo jugando: `GhostAlpha` (0.35) y `GhostForAllies`
      (apagado). Prenderlo hace que tu equipo sepa de un vistazo que el enemigo no te ve.

### Respuesta visual del combate — PROBADO ✅

Tres avisos, sin nada que cablear: `UI/UI_CombatFeedback.cs` se dibuja solo y quien lo
necesita lo pide con `UI_CombatFeedback.Get()`.

**1. La X de golpe.** Cada vez que le pegás a algo —NPC o personaje— aparece una X roja
en la retícula y se apaga. La opacidad es **la vida que le FALTA al golpeado**: le pegás
a alguien entero y la X apenas se insinúa; lo dejás al 10% y sale casi sólida. De un
vistazo sabés si le estás haciendo cosquillas o si está por caer.

Con una salvedad sobre lo que pediste: le puse un **piso de opacidad** (`HitMinAlpha`,
0.25). Al pie de la letra, pegarle a alguien con la vida llena daría un 10% de opacidad,
que en pantalla y en movimiento no se ve — y entonces la X fallaría justo en lo que
tiene que hacer, que es confirmarte que el golpe entró. Ponelo en 0 si lo querés literal.

**2. La calavera.** Solo al matar a un PERSONAJE (jugador o bot), no a un monstruo del
mapa: si cada bicho tirara calavera, dejaría de significar algo. Aparece a plena
opacidad y se apaga rápido. Se dibuja por código; si le asignás un sprite en `KillIcon`,
usa ese.

**3. El arco de daño recibido.** Un círculo grande alrededor de la mira, siempre
invisible, que se enciende del lado por donde vino el golpe: arriba si te pegaron de
frente, abajo si fue por la espalda, y los costados en su ángulo real. El ángulo se mide
contra **hacia dónde estás mirando**, no contra el cuerpo — lo que tenés que corregir es
la cámara.

Sin esto, morir por la espalda se siente injusto: no llegabas a enterarte de que te
estaban pegando.

**Todo va por TargetRpc a cada dueño**, no por ObserversRpc: es información privada de
cada jugador, y mandarla a todos gastaría red y le contaría a los demás que alguien está
peleando. Los ticks de veneno no disparan nada, para no llenar la pantalla.

**Para tocarle las medidas desde el Inspector**: creá un GameObject vacío en la escena de
la arena y agregale el componente `UI_CombatFeedback`. `Get()` usa el de la escena si hay
uno, y recién si no hay lo crea. Con el componente puesto, cambiar las medidas **mientras
jugás** las aplica al momento — no hay que salir y volver a entrar.

La X son **cuatro puntas con un hueco en el medio**, no una equis maciza: el hueco deja
ver la retícula justo cuando más la estás mirando. `HitMarkLength` alarga las puntas,
`HitMarkGap` agranda el hueco, `HitMarkThickness` las engrosa.

- [x] La X ya se ve bien de tamaño. Falta mirar, jugando, si la opacidad según la vida
      del golpeado se lee (que vaya poniéndose más sólida a medida que baja).
- [x] Probar la calavera matando a un bot, y que NO salga al matar un monstruo.
- [x] Probar el arco con un bot pegándote de frente y por la espalda.
- [ ] Mirar si el círculo (`DamageRingRadius`, 190) queda donde molesta o donde se ve.
      Es la perilla que más se va a querer tocar.

### El robo de vida se quedaba pegado al cambiar de clase — ARREGLADO Y PROBADO ✅

Cambiar de Bárbaro a Pícaro dejaba al Pícaro **curándose al pegar**. No hacía falta morir.

Los `AttributeSet` del Bárbaro y sus tres subclases traen `LifeSteal 0.3` como stat **base
de la clase** (no es un buff, por eso no lo limpiaba ni morir ni `RemoveAllActiveEffects`).

En un **host el ASC es UNO SOLO**: servidor y cliente comparten objeto. Y
`OnNetStatChanged` no tenía el guard que sí tiene su gemelo `OnNetTagsChanged`:

```csharp
// TAGS
if (asServer || IsServerInitialized || _asc == null) return;
// STATS  ← le faltaba
if (asServer || _asc == null) return;
```

Sin ese chequeo, la copia **cliente** del callback vuelve a escribir en el ASC autoritativo
un valor que el servidor ya cambió — y como `SetCurrentAttributeValue` **crea** el atributo
si no existe, resucitaba atributos recién borrados. `InitializeAttributes` vaciaba bien el
diccionario; el eco de `NetStats` le volvía a meter el 0.3 del Bárbaro, y como el daño se
resuelve en el servidor, el Pícaro seguía robando vida.

La otra mitad era de los **clientes remotos**: `NetStats` solo se ESCRIBE, no sabe decir
"este atributo ya no existe", así que las otras pantallas se quedaban con los stats de la
clase vieja. `InitializeAttributes` ahora avisa de todo al reconstruir — en cero los que
desaparecieron, con su valor los nuevos (que tampoco viajaban: recién construidos su
`CurrentValue` ya es el correcto y `RecalculateAllAttributes` no veía ningún cambio).

**Vale para cualquier stat que una clase declare y la siguiente no**, no solo el robo de
vida. Con el Paladín pasaba lo mismo al revés.

- [x] Probado: el Pícaro ya no se cura por ningún motivo al venir del Bárbaro.

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

---

# 6. Las clases que faltan — plan y estimación (24 de septiembre)

Estimación al ritmo que llevamos, **solo código y configuración** (animaciones, VFX y
balance van aparte, y con el Bárbaro tomaron sus semanas). Referencia del historial: el
Paladín con sus 3 subclases tardó ~12 días (11–22 de agosto); el Pícaro ~3.5 semanas,
con arreglos de red mezclados.

**Las 5: 13 a 15 semanas → enero de 2027.** A la demo del showcase (diciembre) le quedan
~10: no caben todas.

Ordenadas de menos a más difícil:

| # | Clase | Rol | Estimación | Lo nuevo que pide (lo demás se reusa) |
|---|---|---|---|---|
| 1 | Clérigo | Soporte | 1.5–2 sem | Resurrección (cuerpo de aliado, se mete con el respawn), revelar invisibles, desarmar |
| 2 | Guerrero | Tanque | 2–2.5 sem | Parry (ventana exacta en el servidor, con lag), devolver proyectiles, desarmar, arma según postura |
| 3 | Explorador | Daño | ~3 sem | Mascota con IA en red, ver a través de paredes (solo tu equipo), repeler, zoom del arco, "menos curación recibida" |
| 4 | Monje | Daño | 3–3.5 sem | Ki (variantes "con Ki" en casi todo el kit), 4 orbes, aliento mantenido, daño→curación, repeler, cadena de Muerte silenciosa. Muchas animaciones de puños |
| 5 | Mago | Daño | 4+ sem | ~12 hechizos, vuelo libre en red, interacciones entre hechizos, lanzar desde el aliado. **Bloqueado:** extra de Filo danzante SIN DEFINIR |

Qué se reusa, en corto:
- **Clérigo:** estela de Castigo divino, elegir aliado de la Intercepción,
  `Status_Immortal`, `ContinuousAoE`, cono, `State_Silenced`.
- **Guerrero:** `GA_ShieldBlock`, `GA_Dash`, `GA_TagSwitch`, `LeapAbility`, marcas
  del Enemigo jurado, `ChargedAbility`, `Status_Unstoppable`.
- **Explorador:** `ChargedAbility`, `LeapAbility`, Cañones del Pirata,
  `RadialMenuAbility`, `EClassRole` para Presa.

## Sistemas compartidos — hacerlos una vez

- [ ] **Desarmar** — Guerrero (Maestro de batalla), Clérigo (Zona de verdad)
- [ ] **Repeler** — Monje (Patada del viento), Explorador (trampa explosiva)
- [ ] **Revelar invisibles** — Clérigo (Faro de esperanza), Explorador (Búho)
- [ ] **Vuelo libre** — Mago, quizá Muerte silenciosa del Shinobi
- [ ] **Mascota con IA en red** — Explorador; sirve para futuros summons

## Para la demo: Clérigo + Guerrero (~4–4.5 semanas)

- Las dos más baratas.
- Tanque y Soporte quedan completos (2 clases cada uno): nadie repite forzosamente ahí.
- Con la demo en diciembre, caben también el mapa del foso (sección 7, 1–2 semanas) y
  unas 3–4 semanas de pulido visual.
- Daño sigue siendo solo Pícaro; si quieres variedad, el siguiente es el Explorador.

**Alternativa:** solo los **kits base** de varias clases (~3–5 días cada uno, porque lo
nuevo vive casi todo en las subclases). Contra: al llegar a nivel 3 no hay qué elegir.

- [ ] Decidir: Clérigo + Guerrero completos, o kits base de varias
- [ ] Antes del Mago: definir la extra de Filo danzante y el vuelo libre

---

# 7. El mapa se siente chico — entrega con espera y mapa del foso

**El diagnóstico (24 de septiembre).** Las bases están a 47 m del centro; cargando se va
a 4.5 m/s, o sea ~10 s caminando. Pero el salto del Bárbaro (`JumpVelocity 15`,
`ForwardForce 15`, gravedad 9.8) vuela ~3 s a 15 m/s: **~45 m en papel, casi todo el
viaje de un salto**. El Pícaro suma 2 dashes de 6 m y el Blink. Y la bolsa solo bloquea la
R, no el Shift. Por eso agrandar el mapa sin tocar las reglas no alcanza.

## Entregar toma 3 segundos — HECHO, falta probar

Inspirado en el cobro de The Finals. `MercObjective.DeliverSeconds` (3 s; 0 = entrega
instantánea como antes): hay que quedarse en la zona de entrega. **Salir de la zona o
recibir daño de otro personaje reinicia la cuenta.** El daño es el mismo que carga la
definitiva del tanque (`OnDamageEndured`): lo que frena un escudo cuenta y corta la
entrega; lo que se bloquea del todo con el escudo direccional, no.

Todos ven "ENTREGANDO 1.8s" y una barra del color del equipo bajo el marcador del
Objetivo; tu equipo ve "ENTREGANDO" en el marcador de la entrega.

- [x] Probado con bots: anota a los 3 s y el golpe reinicia (24 de septiembre)
- [ ] Salir de la zona a mitad: la barra vuelve a 0
- [ ] Un veneno/herida encima: cada tick reinicia (¿se siente justo o frustrante?)
- [ ] En un cliente la barra avanza suave, no a saltos
- [ ] En la prueba de 9: ¿los rivales llegan a cortarla? ¿3 s es poco o mucho?

## Con la bolsa no se entra a la base propia — PROBADO ✅

Si no, bastaba esconderse en la sala (donde eres intocable) con la bolsa hasta que se
acabe el reloj. Tres capas:
- **La pared** (`MercSafeRoomBarrier`): al de casa que carga la bolsa se le quita el
  permiso de atravesarla, y lo recupera al soltarla o entregarla.
- **La base** (`MercTeamBase.EjectObjectiveCarrier`, prendido): si igual se mete, lo
  saca por la cara más cercana, como a un enemigo. Es la regla de verdad, del servidor.
- **Los bots** (`AvoidEnemySafeRooms`): con la bolsa no ponen rumbo a su sala, así un
  Pícaro herido no queda rebotando en la puerta.

- [x] Con la bolsa, caminar hacia la puerta de tu sala: la pared te frena
- [x] Soltar la bolsa (R) en la puerta: puedes volver a entrar
- [x] Agarrarla estando parado junto a la puerta desde adentro: te saca afuera
- [x] Un Pícaro bot herido con la bolsa: va a entregar en vez de irse a curar

**Si aun así se siente rápido**, la otra regla que quedó en la mesa: cargar la bolsa
bloquea la habilidad de movimiento (como ya bloquea la R), o usarla te hace soltar la
bolsa.

## El mapa del foso — para la demo de diciembre

El bosquejo que te gustó, para después:

- **Tamaño:** bases a ~80 m (hoy 47) y arena de ~63 m de radio (hoy 43): 2.1× el área.
  Cargando, ~18–25 s de vuelta según la ruta.
- **El Objetivo en un foso a −6 m** en vez de en la meseta: quien lo agarra tiene que
  salir mientras le disparan desde arriba.
- **Rampas del foso hacia las torres, no hacia las bases**: ninguna ruta es recta.
- **3 torres a +8 m entre las bases**, con los campamentos de NPCs, unidas por **puentes
  que pasan justo encima de la ruta directa de cada equipo**.
- **3 rutas de vuelta por equipo:** suelo (abierta, media), alta (torres y pasarelas,
  rápida y expuesta) y túnel (cubierto, casi directo, con techo: no se puede saltar).
- Se arma **un sector de 120° y se rota 3 veces**, como la decoración.

Lo que cuesta o hay que cuidar:
- [ ] NavMesh con niveles: NavMesh Links para bordes y saltos; los bots ya batallaban
      saltando
- [ ] Subir `MercArenaBounds.CeilingHeight` (20 m): el salto del Bárbaro sube ~11.5 m, y
      desde una torre de +8 m llega a ~19.5 m
- [ ] Túneles anchos: la cámara en tercera persona sufre en lo estrecho
- [ ] Mover bases, campamentos, botiquines y plataformas de entrega
- [ ] Greybox con cubos y probar con bots ANTES de decorar
- Estimación: 1–2 semanas de editor. Compite con Clérigo + Guerrero (sección 6).

## El Cementerio de los Tres Panteones — el mapa para la demo (bosquejo del 24 de sept.)

Es el mapa del foso de arriba, con tema de cementerio, pasillos y pasadizos. **Este es el
plan**; lo de arriba queda como el origen de la idea.

**Las zonas, del centro hacia afuera:**
- **Capilla y cripta.** El Objetivo aparece **en la cripta, a −6 m**, bajo la capilla. En
  la nave de la capilla va **el jefe**: el centro es la pelea grande. Las escaleras de la
  cripta salen hacia las **torres**, no hacia las bases.
- **Explanada.** Anillo abierto alrededor de la capilla, con tumbas grandes de cobertura.
  El único lugar con vistas largas.
- **Laberinto de nichos.** Muros de gavetas de 4 m (estilo panteón mexicano). Por sector:
  una **avenida** central ancha, cortada por una fuente para que no sea una línea de tiro;
  **dos pasillos laterales** que serpentean hasta las torres; y **dos mausoleos con
  pasadizo** (entras por un lado, sales al pasillo de al lado) para flanquear.
- **Techos de los nichos (+4 m).** Se suben por escalones de lápidas: ruta alta y expuesta.
- **Torres campanario (+8 m)** entre cada par de panteones: **magos** arriba y **campos de
  tumbas con fantasmas** alrededor.
- **Catacumbas (−6 m).** Túneles de la cripta al pie de cada torre (escalera por dentro).
  Con techo: **el Bárbaro no puede saltar ahí**.
- **La fosa.** Hoyo al costado del atrio de cada panteón (no adentro: que nadie se caiga
  entregando) que cae a una rama de la catacumba hacia la cripta. **Solo de bajada**: tu
  equipo llega rápido a la cripta, pero no sirve para volver con la bolsa.
- **Panteón y atrio.** El panteón es la sala segura (14×14, una puerta). El atrio de
  entrega, afuera, tiene **dos entradas** y algún techo bajo cerca, para que los rivales
  tengan varios ángulos para cortar los 3 s.

**Rutas de vuelta con la bolsa (por equipo):** a pie (capilla → explanada → avenida:
directa y expuesta), por los techos (túnel → torre → techos → atrio: te ven todos) y por
catacumbas (túnel → torre → pasillo lateral → atrio: cubierta un tramo, la más larga).
Todas **20–25 s cargando**, más los 3 s de entrega (hoy ~10 s).

| Elemento | Medida | Por qué |
|---|---|---|
| Centro → panteón | ~75 m | 20–25 s cargando |
| Radio del muro del cementerio | ~60 m | 2× el área de hoy |
| Pasillos | 5–6 m de ancho | Cámara en tercera persona + dos peleando |
| Avenida | ~8 m | Que no la tape un solo jugador |
| Muros de nichos | 4 m | Tapan la vista; el salto los pasa (sube ~11.5 m) |
| Catacumbas | ≥ 4.5 m de alto, 5 m de ancho | Que la cámara no se meta en el techo |
| Torres | +8 m, escalera por dentro | Altura para los magos y para quien la tome |
| Techo de la arena | 20 → ~25 m | Desde una torre el salto llega a ~19.5 m |
| Vistas más largas | ~30–35 m | Las 3 clases de hoy son casi todas cuerpo a cuerpo |

**Tema que además guía:** toque de Día de Muertos. **Caminos de pétalos de cempasúchil**
marcando las rutas a cada atrio (como guían a las almas a casa), velas como luz, y las
campanas sonando cuando aparece el Objetivo (cuando haya sonido).

**Cómo construirlo:**
- [ ] Greybox de UN sector con cubos (panteón, atrio, laberinto, media torre, tramo de
      catacumba), hacerlo prefab y rotarlo 3 veces
- [ ] NavMesh: rampas y escalones de lápidas, nada de escaleras de mano (no hay trepar)
- [ ] La fosa con un **NavMesh Link con Bidirectional apagado** (los bots solo bajan)
- [ ] Subir `MercArenaBounds.CeilingHeight` a ~25 m
- [ ] Mover spawns, campamentos (fantasmas en tumbas, magos en torres, jefe en la
      capilla), botiquines y el `ObjectiveSpawnPoint` a la cripta
- [ ] Probar con bots cuánto tardan en volver por cada ruta, y jugarlo, ANTES de decorar
- [ ] Decoración al final: nichos, cruces, velas, pétalos

---

# 8. Más retroalimentación visual (25 de septiembre)

Ya existen: la X del golpe, la calavera, el círculo de daño, los nameplates con buffs, el
anunciador de partida y el crítico en el pipeline de daño (`ctx.IsCrit`). Lo que NO hay y
se puede hacer barato, de más a menos valor para el showcase:

**Muy baratas (< 1 h cada una):**
- [x] **Viñeta roja con poca vida**: el borde de la pantalla late en rojo por debajo de
      ~30 %. Solo lee tu vida local, sin red.
- [x] **Pantalla de muerte + cuenta regresiva**: "Te eliminó *Nombre* (Rogue) ·
      Reapareces en 3…". El asesino ya se conoce en el servidor (`LastAttacker`).
- [x] **La definitiva lista late**: la ranura de la R pulsa y brilla al estar cargada, y
      un "¡Definitiva lista!" corto. `UI_UltimateSlot` ya sabe cuándo está lista.
- [x] **Columna de luz en la entrega**: del color del equipo, visible desde todo el mapa,
      mientras alguien entrega. Usa el progreso que ya viaja por red.

**Baratas (1–2 h):**
- [x] **Registro de bajas** en la esquina ("*Gus* [hacha] *Pedro*", con colores de
      equipo). Probablemente el que más aporta al showcase: quien solo mira entiende.
- [x] **Números de daño flotantes**: rojos normales, amarillos y grandes los críticos,
      verdes las curaciones (el Paladín es el que menos retroalimentación tiene hoy).
      Ampliar `ServerReportDamage` con cantidad, posición y crítico.
- [x] **X de crítico distinta** (amarilla): casi gratis con los números, viaja el mismo
      dato.
