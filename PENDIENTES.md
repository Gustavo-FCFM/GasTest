# Pendientes — actualizado el 7 de septiembre de 2026

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

Son seis, y ninguna es urgente.

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

Vuelven con un comando, apuntando al commit que los tenga:

```bash
git checkout <commit> -- Assets/Scripts/GameMode/Editor/MercSetupTools.cs Assets/Scripts/GameMode/Editor/MercArenaBuilder.cs Assets/Scripts/GameMode/Editor/MercLayoutTools.cs
```

Los tres van juntos: se llaman entre ellos (`MercArenaBuilder` usa los materiales y los
buscadores de prefab de `MercSetupTools`, y `MercLayoutTools` usa los dos).

## Qué queda por ajustar

Las medidas del panel están todas en el inspector del componente: `PanelSize`,
`ColumnWidth`, `RowHeight` y los cinco colores. Y no tiene scroll: con nueve jugadores
las columnas de tres entran justas.

`UI_LobbyMenu.cs` y su prefab ya no los usa nadie: eran la lista de clases que leía el
generador de la arena, y ese generador se borró. Se pueden borrar los dos — pero antes
copiá las tres clases (`Class_Barbarian`, `Class_Rogue`, `Class_Paladin`) del prefab, que
es el único lugar del proyecto donde están juntas además del panel y del Player.

---

# 3. Balance pendiente (decisiones tuyas)

## El daño mágico cambió de raíz

El tipo de daño ahora lo decide **el atributo del que escala el modificador**:

- escala de `Attack` → daño **físico** (paga armadura)
- escala de `MagicDamage` → daño **mágico** (ignora armadura, vale doble contra escudos)

Antes el `MagicDamage` del atacante se sumaba automáticamente a cada golpe. Estas cinco
clases **perdieron 8-10 de daño por golpe** y pegan solo su físico hasta que les armes
habilidades con efectos mágicos:

| Clase | MagicDamage |
|---|---|
| `ASDef_Inmortal` | 8 |
| `ASDef_Paladin` | 8 |
| `ASDef_OathOfConquestPaladin` | 10 |
| `ASDef_OathOfDevotionPaladin` | 10 |
| `ASDef_OathOfVengeancePaladin` | 10 |

El Paladín es el que más lo siente. Hay que decidir **qué habilidades suyas son mágicas**
y ponerles un efecto que escale de `MagicDamage`, como ya hace `GE_SmiteDamage`.

## Los rangos del jefe no calzan

`Net_Boss` tiene `AttackRange: 20` pero `DetectionRadius: 16`: **nunca puede usar su
alcance completo**, te ataca recién cuando ya estás cuatro metros adentro. Y
`LeashRadius: 20` es igual al alcance, así que suelta la correa justo en el borde desde
donde todavía podría pegarte.

Lo que tiene sentido es detección ≥ alcance, y correa bastante mayor que las dos.
`KeepDistance: 11` parece razonable si el alcance real termina siendo 16-20.

## Los fantasmas siguen siendo sacos de experiencia

Hacen 5 de daño contra 120 de vida: un 4 % por golpe, casi 40 segundos para matarte
estando quieto. Si querés que se sientan una amenaza, lo que más rinde es el **ataque**
en `ASDef_WaveEnemy` (probá 12-15), y después la vida (5 es muy poco: cualquier ataque
los borra).

---

# 4. Plan hasta noviembre

El riesgo con tres meses y trabajando solo **no es que falten cosas: es que todo quede
al 80%**. Este orden es por dependencias y riesgo, no por ganas.

## 1º · Lobby — TERMINADO ✅

Código y montaje, los dos. Ver la sección 2. Lo que quedó:

- **`LobbyManager`** (`Scripts/Network/`): la sala compartida en una `SyncList`, con
  autoridad de servidor. Valida nombre repetido y cupo por equipo, maneja el estado
  "listo", saca a quien se desconecta, y guarda el **arranque del host** (`MatchStarted`)
  en un `SyncVar`.
- **`UI_LobbyPanel`** (`Scripts/GameMode/UI/`): el panel entero dibujado por código — IP,
  nombre, tres columnas de equipo, franja de espectadores, grilla de clases y los botones
  de Confirmar y Start.
- **`NetworkGameManager`**: mete cada conexión a la escena apenas carga. Sin eso el
  `NetworkObject` DE ESCENA de la sala no se spawneaba del lado del cliente y el panel
  se quedaba mudo — era el candado que trabó todo un día.
- **`MercenariesGameMode`**: la preparación arranca con el Start del host, no sola.
- **`UICursor`** (`Scripts/UI/`): el árbitro del cursor y del modo de input, que salió de
  querer poder desconectarse con ESC en cualquier momento. Ver la sección 2.

Sin `LobbyManager` en la escena todo se comporta como antes, así que `Test_Network` no
cambió en nada.

## 2º · Sonido

El mayor salto de calidad percibida por hora invertida. Un juego mudo se lee como
prototipo aunque todo lo demás esté bien.

Tiene **cola larga**: son 67 habilidades. No las sonorices todas — elegí las ~15 que
suenan siempre (ataques básicos, pasos, golpe recibido, muerte, entrega del objetivo).
La música de batalla la está haciendo un amigo: tené los hooks listos para cuando llegue.

## 3º · Cámara espectador

Barata, y es **la herramienta para enseñar el juego**. La querés en octubre, no en
noviembre: el material se graba, se mira y se vuelve a grabar. Como bonus, es el mejor
debugger para mirar peleas desde afuera.

Cámara libre, sin UI. Ahora además tiene con qué engancharse: el lobby ya marca quién
entra como espectador.

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
