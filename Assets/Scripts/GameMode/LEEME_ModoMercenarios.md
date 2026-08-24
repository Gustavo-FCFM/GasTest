# Modo Mercenarios — documentación completa

Modo principal **3c3c3 PvEvP**: tres equipos de tres, NPCs por todo el escenario, un
Objetivo en el centro y **dos entregas para ganar**.

Todo vive en `Assets/Scripts/GameMode/`. La escena de la arena es **nueva y aparte**
(`Assets/Scenes/Arena_Mercenaries.unity`): `Test_Network.unity` queda intacta para tus
pruebas rápidas de clases.

---

# 1. Las reglas, en criollo

**Antes de entrar.** Te conectás, aparece el menú de siempre (nombre + equipo 1/2/3 +
clase) y recién cuando confirmás nace tu personaje, dentro de la sala segura de tu equipo.

**Preparación (30 s).** Los tres equipos están en su base. Es el momento de acomodar
composición: la sala segura es el **único lugar donde se puede cambiar de clase**.

**Partida (hasta 15 min).**

- Al minuto 1 aparece **el Objetivo** en el centro del mapa (una caja dorada con luz
  propia, hasta que consigas un modelo de bolsa).
- Se levanta **con solo acercarse**. Quien lo lleva se mueve un 25 % más lento y **pierde
  la definitiva**: ese botón pasa a soltar el Objetivo.
- Hay que llevarlo a la **plataforma de entrega de tu base**, que está justo afuera de la
  puerta de tu sala segura.
- Cada entrega = 1 punto. **Con 2 puntos se gana** y la partida termina (y se reinicia
  sola a los 15 s, cómodo para una demo con gente mirando).
- Después de cada entrega, el siguiente Objetivo aparece a los 30 s.
- Si el que lo lleva muere, el Objetivo se le cae ahí mismo y **nadie lo puede levantar
  por 3 segundos**.

**La sala segura.** Adentro, y solo si es la de TU equipo: la vida se te topea al máximo,
**no te pueden hacer daño**, y podés cambiar de clase. No protege a los enemigos que
entren: en la base ajena sos carne.

**Los NPCs.** Fantasmas repartidos en campamentos, sobre todo alrededor del centro.
Detectan poco (9 m) y vuelven a su puesto si te alejás; están para dar experiencia y para
que el mapa no se sienta vacío. Son neutrales: los tres equipos pueden matarlos y ellos
atacan a todos (menos al que esté en su sala segura).

**El nivel es del EQUIPO, no tuyo.** Todo lo que gana cualquiera de los tres cae en una
sola bolsa de experiencia, y de ahí sale el nivel de los tres. Consecuencia buscada:
**cambiar de clase no te cuesta progreso** — te reinicia el personaje a nivel 1 y medio
segundo después el servidor te devuelve el nivel del equipo.

**Reaparición.** 5 segundos, en tu propia base.

---

# 2. Qué tenés que armar

## Paso a paso

Menú nuevo en la barra de Unity: **Mercenarios**.

| # | Menú | Qué hace |
|---|---|---|
| 1 | `1 · Crear prefabs de la demo` | Crea `Assets/GameMode/Prefabs/`: el **Objetivo** (caja dorada + luz + `MercObjective`) y el **fantasma EN RED** (agarra `Enemy_Ghost` de la game jam, le saca `EnemyAI` y `NPC_WaveEnemy` —que eran de un jugador— y le pone `NetworkObject`, `NetworkTransform`, `NetworkAbilitySystemComponent` y `MercEnemyAI`, conservando su efecto de daño). |
| 2 | `2 · Crear la escena de la arena` | Crea **`Assets/Scenes/Arena_Mercenaries.unity`** de cero y la deja jugable: NetworkManager, NetworkGameManager + modo de juego, ConnectionHUD, menú de entrada, cámara de lobby, luz, la arena entera, los campamentos, **el NavMesh horneado** y el HUD. La agrega a Build Settings. |
| 3 | `3 · Regenerar la arena en la escena actual` | Rehace solo la geometría. **Ojo: borra la arena entera**, así que si ya la editaste a mano, no lo uses. |
| 4 | `4 · Acomodar las bases en simetría de 3` | Pone las tres bases a 120° exactos y mirando al centro, **sin tocar el resto de la escena**. La del equipo 1 manda: se respeta dónde la pusiste y las otras se acomodan a partir de esa. |
| 5 | `5 · Espejar la selección a los otros 2 sectores` | Decorás **un tercio** de la arena a mano y esto lo copia girado 120° y 240°. Ctrl+Z deshace todo de una. |
| 6 | `6 · Revisar el montaje` | Informe en la consola de qué está bien y qué falta (prefabs, bases, simetría, NavMesh, HUD, rejas…). |

### Cómo decorar con el pack medieval

El pack (`AssetsExtra/Medieval Cute Series/Prefabs`) tiene justo lo que hace falta:
`Tower_1/2` y `Wall_1/2/3` para el perímetro, `Pillar_1/2` para reemplazar mis columnas,
`Tent_1/2` y `Sztandar` (estandarte) para las bases, `Barier_2` y `Archer Shield 1` como
coberturas, `Fence_1/2` para las rejas, y rocas, arbustos y árboles para rellenar.

El método es siempre el mismo: **poné todo en un solo sector** (el tercio que va de una base
a la siguiente), acomodalo hasta que te guste, seleccionalo entero y apretá
`5 · Espejar la selección`. Los otros dos tercios aparecen idénticos. Así el mapa queda
hecho a mano pero sigue siendo justo para los tres equipos — que es lo que un 3c3c3 necesita.

Dos detalles: las copias mantienen el vínculo con el prefab (cambiás el original y cambian
las tres), y si algún adorno tiene collider grande, volvé a hornear el NavMesh después.

Te pide guardar lo que tengas abierto antes de cambiar de escena, y si `Arena_Mercenaries`
ya existe te pregunta antes de reemplazarla.

## Lo que la herramienta NO puede hacer por vos

1. **Elegir el ritmo.** Los tiempos por defecto son los de la sección 4. Para una demo con
   público en vivo, `FirstObjectiveDelay` en 60 s puede ser mucho: bajalo a 20-30 s.
2. **El `MaxLevel` del jugador tiene que coincidir con `MaxTeamLevel`.** Los dos están en
   3 hoy (el `AbilitySystemComponent` del prefab `Player` y el modo de juego). Si subís
   uno, subí el otro.
3. **Balancear los NPCs.** El fantasma hereda los stats de la jam. Miralo en el prefab
   nuevo (`Enemy_Ghost_Networked`): vida, `FallbackDamage`, `DetectionRadius`,
   `AttackCooldown`.
4. **Build Settings para la demo.** La escena queda agregada pero al final de la lista;
   movela al **primer lugar** antes de compilar la build que les pasás a tus amigos, o
   van a arrancar en otra escena.
5. **Las clases del lobby.** Ya están las tres (Bárbaro / Pícaro / Paladín = tanque / daño
   / soporte) en el prefab `UI_LobbyMenu`. Si sumás una cuarta, agregala ahí.
6. **Arte.** Todo es primitivas y colores planos. Cuando tengas modelos, reemplazá los
   hijos visuales: la lógica no mira los meshes en ningún lado.

## Para probarlo vos solo

Play en el editor → `Iniciar Host` → nombre, equipo, clase. Con un solo jugador la partida
igual corre entera (preparación, Objetivo, entrega, victoria). Para probar de verdad los
tres equipos hacen falta build + editor, como venías haciendo.

---

# 3. Cómo funciona por dentro

## Quién manda

**El servidor decide todo; los clientes solo dibujan.** Ningún cliente puede puntuar,
levantar el Objetivo ni darse experiencia: manda un input, el servidor resuelve.

```
Cliente (input)                Servidor (verdad)              Todos los clientes (vista)
──────────────                 ─────────────────              ─────────────────────────
camina hasta el Objetivo  →    MercObjective lo detecta   →   SyncVar: quién lo lleva
                               por cercanía                   → cada uno lo dibuja
                                                                pegado a ese personaje

toca el botón de la ulti  →    ServerRequestDrop:         →   SyncVar: está en el piso
(con el Objetivo encima)       ¿sos vos el que lo lleva?

mata un fantasma          →    NetworkASC.AwardKill       →   SyncList: nivel del equipo
                               → bolsa del equipo             → el marcador se actualiza
```

## Las piezas

| Archivo | Qué es | Dónde vive |
|---|---|---|
| `MercenariesGameMode.cs` | El cerebro: fases, puntaje, aparición del Objetivo, experiencia compartida, wipes y avisos. | En la escena, sobre el mismo objeto que `NetworkGameManager` (comparte su `NetworkObject`). |
| `MercTeamBase.cs` | Una base: sala segura, punto de entrega y puntos de aparición. | Un objeto por base en la escena. |
| `MercObjective.cs` | El Objetivo: recolección, ralentización, caída, entrega. | Prefab de red que el modo spawnea y despawnea. |
| `MercEnemyAI.cs` | Los fantasmas en red: percepción, persecución, correa y golpe. | Prefab de red. |
| `MercEnemySpawner.cs` | Un campamento: mantiene N vivos y los repone. | Objetos sueltos en la escena (no son de red). |
| `UI/UI_MercenariesHUD.cs` | El marcador de arriba (color, nivel, barra de entregas). | `HUD_Mercenarios` en la escena. |
| `UI/UI_MatchAnnouncer.cs` | Los avisos grandes del centro. | ídem |
| `UI/UI_ObjectiveMarker.cs` | El rombo con la distancia que señala el Objetivo. | ídem |
| `UI/MercUIFactory.cs` | Fabriquita de recuadros y textos que usan los tres de arriba. | — |
| `Editor/MercSetupTools.cs` | Los tres menús: prefabs, escena y cableado. | — |
| `Editor/MercArenaBuilder.cs` | La FORMA de la arena: piso, muro, meseta, tablados, rampas, bases y campamentos. Separado de los menús para poder iterar el diseño sin tocar la plomería. | — |

**Todo el HUD se dibuja por código.** No hay prefab de UI que cablear: cada componente se
arma solo y se crea su propio Canvas. Es a propósito — así no se rompe cuando tocás un
prefab, y no hay treinta referencias que se pueden quedar en `None`.

## El plano de la arena

Coliseo redondo de 42 m de radio con **simetría de 3**: todo lo que existe para un equipo
existe igual para los otros dos, girado 120°. Nadie tiene mejor camino que nadie.

```
                        Base Equipo 2 (verde)
                     ┌──────────────────────┐
                     │  ▸▸▸ sala segura ▸▸▸ │   vida topeada, inmune,
                     └────────┬─────────────┘   cambio de clase
                         (entrega)
                              │ carril
                    ╱         │         ╲
              tablado      ╔══╧══╗      tablado      ← madera, 2,5 m
              (2,5 m) ═════╣ ★★★ ╠═════ (2,5 m)        puentes al centro
                    ╲      ║MESETA║      ╱
                           ╚══╤══╝                    ← piedra, 5 m
   Base Equipo 3         carril│carril      Base Equipo 1
      (azul) ───────────────────────────────── (rojo)
```

**Tres alturas, y ahí está la gracia:**

| Nivel | Qué es | Material |
|---|---|---|
| **0 · la arena** | El piso de arena. Donde caés si te tirás de cualquier lado. | arena clara |
| **1 · los tablados** (2,5 m) | Tres plataformas redondas, una entre cada par de bases. Terreno neutral. | madera |
| **2 · la meseta** (5 m) | El centro. Ahí aparece el Objetivo, y quien la tiene ve todo. | piedra |

Que el nivel 1 sea **siempre madera** y el 2 **siempre piedra** no es decoración: es cómo
se lee la altura de un vistazo mientras corrés.

**Dos formas de llegar al Objetivo**, y ese es el juego:

- **El carril** (directo, abajo). Salís de tu base, cruzás tu camino de piedra —marcado con
  el color de tu equipo—, subís la rampa ancha y estás arriba. Rápido y a la vista de todos.
- **La vuelta alta** (flanco). Rampa lateral a un tablado, y del tablado un puente de madera
  a la meseta. Más largo, pero llegás por arriba y por el costado.

Cada tablado toca a **dos** bases (una rampa para cada una), así que también es el camino
natural para ir a molestar al vecino sin pasar por el medio.

Las salas miden 12×12 con **una sola puerta**, mirando al centro. La plataforma de entrega
está **afuera de esa puerta**: si estuviera adentro, pisar la base sería punto asegurado
(ahí sos invulnerable) y el último tramo —el más emocionante— no se podría disputar.

Bajar el Objetivo desde la meseta es a propósito lo más tenso de la partida: estás lento,
no tenés definitiva, y tenés que elegir por qué rampa salir con dos equipos mirándote.

### Las medidas, si querés tocarlas

Están todas juntas arriba de [`Editor/MercArenaBuilder.cs`](Editor/MercArenaBuilder.cs) —
radio de la arena, altura de la meseta, distancia de las bases, ancho de los carriles. La
geometría se escribe **una sola vez** en el marco de un sector (origen en el centro, +Z
hacia la base) y se instancia tres veces girada 120°: no hay tres copias del código, hay un
sector y tres rotaciones. Cambiás un número y las tres bases cambian juntas.

## La línea de tiempo de una partida

```
0:00 ─── preparación ───► 0:30 ─── partida ─────────────────────────► 15:00
       (todos en su base)      │                                    (o antes,
       cambio de clase libre   │ 1:30 aparece el Objetivo            al 2° punto)
                               │      ▼
                               │      entrega → +1 punto → +30 s → vuelve a aparecer
                               │
                               └─ experiencia pasiva corriendo todo el tiempo
                                  peleando:   nivel 2 a los ~2 min · nivel 3 a los ~5
                                  sin pelear: nivel 2 a los  4 min · nivel 3 a los 10
```

(Los minutos son desde que ARRANCA la partida, o sea 30 s después de entrar.)

## La experiencia compartida (la parte más importante)

Cada equipo tiene **una bolsa**. Entra experiencia de tres fuentes:

| Fuente | Cuánto |
|---|---|
| Pasiva (todo el tiempo, para todos) | se calcula sola: lo justo para llegar a nivel 3 en el minuto 10 |
| Matar un NPC | 15 |
| Derribar a un jugador | 40 |

Cuando la bolsa cruza un umbral (100 para el nivel 2, 250 acumulados para el 3), el
servidor le sube el nivel **a los tres jugadores** con
`AbilitySystemComponent.SetLevelTo()`, que aplica el crecimiento de stats de la clase
nivel por nivel — el mismo camino que usaba la experiencia individual.

Dos veces por segundo, `ServerSyncPlayerLevels()` repasa a todos y les deja el nivel de su
equipo. Eso es lo que hace que **cambiar de clase salga gratis**: `EquipCharacterClass`
con `resetProgress` te deja en nivel 1, y en el tick siguiente volvés al nivel del equipo.
De paso, esa función publica la bolsa en tus atributos `Exp`/`MaxExp`, así la barra de
experiencia de tu HUD personal muestra el progreso **del equipo**.

La experiencia pasiva **no es un número suelto**: sale de
`MinutesToGuaranteedMaxLevel`. O sea que el campo que tocás no es "cuánta exp por
segundo" sino "en qué minuto quiero que llegue a nivel máximo el equipo que no hizo nada",
que es la pregunta de diseño de verdad.

## Qué viaja por la red

| Dato | Cómo | Por qué así |
|---|---|---|
| Puntaje, nivel y progreso de los 3 equipos | `SyncList` | El HUD los lee directo, sin RPCs. |
| Reloj de la fase | `SyncVar` cada 0,5 s | El cliente lo descuenta solo entre paquetes, así corre suave sin gastar red. |
| Avisos ("¡entregaron!") | `ObserversRpc` con enum + números | El TEXTO lo arma cada cliente. Cambiar la redacción o traducir el juego no toca la red. |
| Posición del Objetivo en el piso | `SyncVar` | Se mueve muy poco. |
| Quién lleva el Objetivo | `SyncVar` con el ObjectId | Cada cliente lo pega LOCALMENTE a ese personaje: se ve perfectamente suave porque usa la posición ya interpolada de ese jugador, y no gasta red. |
| Estado de la sala segura | tags (`NetTags`) | `Status_Immunity` + `Status_SafeZone` viajan por el canal de tags que ya existía. |

---

# 4. El ritmo (Inspector del `MercenariesGameMode`)

| Campo | Por defecto | Para qué |
|---|---|---|
| `WarmupSeconds` | 30 | Preparación con los equipos en su base. |
| `MatchDurationSeconds` | 900 (15 min) | Tope; si se acaba, gana el que va arriba. |
| `PointsToWin` | 2 | Entregas para ganar. El marcador se parte solo en esa cantidad de trozos. |
| `FirstObjectiveDelay` | 60 | Del arranque a la primera aparición. **Bajalo para la demo.** |
| `ObjectiveRespawnDelay` | 30 | De una entrega a la siguiente aparición. |
| `RespawnSeconds` | 5 | Reaparición del jugador en su base. |
| `AutoRestart` / `RestartDelay` | sí / 15 | Reinicia sola al terminar. |
| `MaxTeamLevel` | 3 | Tiene que coincidir con el `MaxLevel` del ASC del jugador. |
| `XpPerLevel` | 100, 150 | Experiencia de 1→2 y de 2→3. |
| `XpPerNpcKill` | 15 | |
| `XpPerPlayerTakedown` | 40 | Vale más que un NPC a propósito. |
| `MinutesToGuaranteedMaxLevel` | 10 | La red de seguridad: sin pelear, nivel 3 en el minuto 10. |
| `PassiveXpPerSecondOverride` | 0 (auto) | Solo si querés fijar la pasiva a mano. |
| `ClassChangeOnlyInSafeRoom` | sí | Apagalo y se puede cambiar de clase en cualquier lado. |
| `TeamColors` | rojo / verde / azul | Los usan el marcador, los avisos y el rombo del Objetivo. |

Con estos números, un equipo que farmea y pelea llega a nivel 3 alrededor del **minuto 5**
(necesita ~125 de experiencia extra sobre la pasiva: unas 8 bajas de NPC más un derribo).

---

# 4 bis. Panel de control: dónde se ajusta cada cosa

Todo lo tocable del modo, con lo que vale hoy y en qué archivo vive.

## Experiencia y niveles

| Qué | Dónde | Hoy |
|---|---|---|
| Cuánta experiencia cuesta cada nivel | `MercenariesGameMode` → `XpPerLevel` | 100 (1→2), 150 (2→3) |
| Cuánto da un NPC / un jugador | `MercenariesGameMode` → `XpPerNpcKill`, `XpPerPlayerTakedown` | 15 / 40 |
| La red de seguridad (pasiva) | `MercenariesGameMode` → `MinutesToGuaranteedMaxLevel` | 10 min |
| Nivel máximo | `MercenariesGameMode` → `MaxTeamLevel` **y** el `MaxLevel` del `AbilitySystemComponent` del prefab `Player` | 3 en los dos |
| **Qué gana el personaje al subir** | El asset de cada clase → `StatGrowthPerLevel` | Bárbaro: **+70 vida máx** y **+1 ataque** por nivel |

Ojo con la última fila: el modo decide *cuándo* subís de nivel, pero *qué te da* subir es
de la clase. Si el nivel 3 se siente flojo, se toca ahí (`Assets/Attributes/…/Class_X.asset`),
no en el modo.

El `MaxExp` que aparece en `StatGrowthPerLevel` ya no hace nada: con la bolsa compartida, la
experiencia que se muestra la escribe el modo de juego cada medio segundo.

## Los fantasmas

| Qué | Dónde | Hoy |
|---|---|---|
| Vida, ataque y velocidad | `Assets/48toPlay/ASDef_WaveEnemy.asset` | **5 de vida**, **5 de ataque**, 3,5 de velocidad |
| Cómo pega | `Assets/48toPlay/GE_EnemyDamage.asset` | daño = 1 × el ataque del fantasma → **5 por golpe** |
| Cada cuánto pega | prefab `Enemy_Ghost_Networked` → `MercEnemyAI.AttackCooldown` | 1,6 s → ~3 de daño por segundo |
| A qué distancia te ve | `MercEnemyAI.DetectionRadius` | 9 m |
| Hasta dónde te persigue | `MercEnemyAI.LeashRadius` | 18 m desde su puesto |
| Cuántos hay y cada cuánto vuelven | cada `MercEnemySpawner` → `Count`, `RespawnSeconds` | 2-3 por campamento, 20 s |

**Para dimensionarlo:** un Bárbaro arranca con **120 de vida**. O sea que un fantasma le
saca un 4 % por golpe y tarda casi 40 segundos en matarlo estando quieto. Hoy son
prácticamente sacos de experiencia — si querés que se sientan una amenaza real, el número
que más rinde es el **ataque** en `ASDef_WaveEnemy` (probá 12-15), y después la vida (5 es
muy poco: cualquier ataque los borra).

## El Objetivo

| Qué | Dónde | Hoy |
|---|---|---|
| A qué distancia se levanta | prefab `Objective_GoldBag` → `MercObjective.PickupRadius` | 2,2 m |
| Cuánto queda bloqueado al caerse | `PickupLockSeconds` | 3 s |
| Cuánto te frena llevarlo | `CarrySlowPercent` | 25 % |
| Dónde se ve mientras lo cargás | `CarryOffset` | 2,1 m sobre la cabeza |
| Radio de la entrega | cada `MercTeamBase` → `DeliveryRadius` | 3,5 m |

## Escenario y reglas de la arena

| Qué | Dónde |
|---|---|
| Medidas de la arena (radio, alturas, distancias) | `Editor/MercArenaBuilder.cs`, las constantes de arriba |
| Tamaño de la sala segura | cada `MercTeamBase` → `SafeRoomSize` |
| Techo y paredes invisibles | `MercArenaBounds` (ver abajo) |
| Rejas de la preparación | `MercGate` (ver abajo) |

---

# 4 ter. Que nadie se escape: límites y rejas

Dos componentes nuevos, los dos se sueltan en la escena y no piden cablear nada.

## `MercArenaBounds` — el techo invisible

Un GameObject vacío **en el centro de la arena** con este componente. Al arrancar la partida
arma solo un anillo de paredes invisibles y un techo, para que nadie se escape con un dash,
un salto o la caída de pluma.

- **Deja huecos hacia cada base**, calculados a partir de dónde estén las `MercTeamBase`. Si
  moviste las salas afuera del muro (como hiciste), igual se puede entrar y salir; si movés
  una base, el hueco la sigue.
- Se construye **en runtime** a propósito: si estas paredes existieran al hornear el NavMesh,
  los NPCs las verían como obstáculos y el mallado se comería el borde de la arena. En el
  editor las ves como gizmo al seleccionar el objeto (naranja = pared, verde = hueco).
- `Radius` un poco mayor que el muro visible, `CeilingHeight` 20 m, y `CeilingMargin` para
  que el techo también tape las salas de afuera.

## `MercGate` — la reja de preparación

En el hueco de cada puerta: cerrada durante los 30 s de preparación, se levanta sola cuando
arranca la partida. **No es un objeto de red**: la fase ya viaja sincronizada, así que cada
máquina mira ese estado y mueve su propia reja — cero tráfico y cero desincronización.

1. Poné el modelo de reja en el hueco, **en la posición cerrada** (esa es la que el script
   toma como punto de partida).
2. Agregale `MercGate`. Si el modelo tiene que subir pero el pivote está en el piso, poné
   la parte que se mueve en `MovingPart`.
3. Si todavía no tenés modelo: clic derecho en el componente → **Generar reja de barrotes**,
   y después la reemplazás por `Fence_1` o `Barier_2` del pack medieval.

Sin modo de juego en la escena la reja se queda abierta, así una escena de pruebas nunca te
deja encerrado.

---

# 5. Dónde se toca cada regla

- **Sala segura** → `MercTeamBase.TickSafeRoom()`. Cada 0,2 s hace un `OverlapBox` y
  reparte `Status_Immunity` + `Status_SafeZone`. Usa un barrido y no `OnTriggerEnter` a
  propósito: así también agarra a quien **aparezca ya adentro** (spawn y respawn), que es
  justo el caso que un trigger se pierde.
- **Cambiar de clase** → `UI_ClassMenu.Open()`. La tecla **C** solo abre el menú si tenés
  `Status_SafeZone`; si no, avisa por el cartel del centro. La tecla **V** (subclases) no
  tiene restricción: eso es progresión, no cambio de personaje.
- **El botón de la ulti suelta el Objetivo** → `PlayerController.HandleAbilityInput()`. La
  bifurcación está **antes** de `CheckAbilityButton`; si la habilidad se evaluara igual,
  el mismo clic soltaría la bolsa Y tiraría la definitiva.
- **Experiencia por baja** → `NetworkAbilitySystemComponent.AwardKillExperience()`. Es el
  único punto del core que sabe quién dio el golpe final (`LastAttacker`). Si hay modo de
  juego, la manda a la bolsa del equipo; si no, sigue funcionando como antes (individual),
  así tu escena de pruebas no cambia en nada.
- **Aparición y reaparición** → `NetworkGameManager` le pregunta al modo por el punto de
  la base del equipo, y usa su `RespawnSeconds`. Sin modo de juego, reparto redondo de
  siempre.
- **Enemigos** → `MercEnemyAI`. `DetectionRadius` 9 m, correa de 18 m a su puesto, ignoran
  a quien esté en una sala segura. Son equipo 0 (neutral) = hostiles a los tres equipos.
- **Efectos creados en código**: el de "cargar el Objetivo" (ralentización + tag) y el
  golpe de los NPCs sin `GameplayEffect` asignado se fabrican en runtime con
  `ScriptableObject.CreateInstance`, marcados `Hidden` para no tener que estar en el
  `GameplayEffectRegistry`. Si preferís verlos como assets, asignalos en el Inspector
  (`MercObjective.CarryEffect`, `MercEnemyAI.DamageEffect`) y esos mandan.

## Tags nuevos

Se agregaron **al final** de `EGameplayTag` (como manda la regla del enum, para no correr
los índices de los assets ya guardados):

- `Status_Carrying_Objective` — lleva el Objetivo.
- `Status_SafeZone` — está en la sala segura de su equipo.

---

# 6. Pendientes conocidos

- **Los NPCs no roban el Objetivo.** El documento de diseño dice que intentan llevarlo de
  vuelta al centro; hoy solo pelean. Es un agregado chico sobre `MercEnemyAI`.
- **Nadie está encerrado durante la preparación.** Podés salir de la base antes de que
  arranque. Se arregla con una pared que se apague al empezar (o chequeando
  `State == Warmup`).
- **Enemigos a distancia**: `MercEnemyAI.AbilityToUse` ya está listo para el mago de la
  jam, pero ningún campamento lo usa todavía.
- **Marcadores de compañeros** (los rombitos con el nombre de cada aliado, como en las
  capturas de referencia): el marcador del Objetivo ya sirve de molde.
- **Sin sonido**: los avisos son solo texto.
- **Sin pantalla de fin de partida**: hoy el resultado sale en el reloj del marcador y en
  el cartel del centro, y a los 15 s arranca otra.
