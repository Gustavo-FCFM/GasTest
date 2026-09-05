# Pendientes — actualizado el 5 de septiembre de 2026

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
- **Barras de vida de los enemigos**: los once prefabs migrados a `UI_WorldHealthbar`
  (corriste `Mercenarios ▸ 9`). `HealthBarNpc.cs` quedó sin usar en todo el proyecto —
  se puede borrar.
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

# 2. Montaje del lobby nuevo

El código del lobby está terminado y compilando (ver la sección 3). Para probarlo:

- [ ] `LobbyManager` en el mismo GameObject que `NetworkGameManager` (comparte su
      `NetworkObject`, igual que `MercenariesGameMode`)
- [ ] `UI_LobbyRoster` en cualquier GameObject de la escena del lobby
- [ ] *(Opcional)* Cablear `SpectatorToggle` y `WarningText` en `UI_LobbyMenu` — sin
      ellos funciona igual, solo que sin opción de espectador y con el aviso de nombre
      repetido yendo a consola

Con `MinPlayersToStart: 1` lo podés probar solo: confirmás y arranca la preparación.

El panel se dibuja por código, así que no hay prefab de UI que armar — pero las
posiciones y tamaños seguro quieran un ajuste cuando lo veas en pantalla (están en los
campos del componente).

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

## 1º · Lobby — CÓDIGO TERMINADO ✅

Ya está, falta solo el montaje de la sección 2. Lo que quedó implementado:

- **`LobbyManager`** (`Scripts/Network/`): la sala compartida en una `SyncList`, con
  autoridad de servidor. Valida nombre repetido y cupo por equipo, maneja el estado
  "listo" y saca a quien se desconecta.
- **`UI_LobbyRoster`** (`Scripts/GameMode/UI/`): tres columnas por equipo con el cupo,
  franja de espectadores, y una línea de estado que dice a quién se está esperando.
- **`UI_LobbyMenu`**: te anota en la sala apenas abrís el menú (los demás te ven con `?`
  mientras elegís), avisa del nombre repetido en vivo, y contempla al espectador.
- **`MercenariesGameMode`**: la preparación no corre mientras falte gente por confirmar.

Sin `LobbyManager` en la escena todo se comporta como antes, así que `Test_Network` no
cambió en nada.

**Lo que sigue faltando del lobby**, cuando lo pruebes con gente: nada bloqueante, pero
el panel no tiene scroll — con nueve jugadores las columnas de tres entran justas.

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
misma sensación de contenido. Ya tenés el generador de arena.

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
