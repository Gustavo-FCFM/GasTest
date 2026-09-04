# Pendientes — sesión del 2 al 4 de septiembre de 2026

Todo lo de esta sesión está **commiteado y pusheado**. Lo que queda son tareas de
EDITOR (que las hacés vos) más el plan hasta la demo de noviembre.

## Cómo retomar en la otra máquina

```bash
cd ".../Proyectos Gustavo/GasTest/GasTest"
git fetch origin && git checkout main && git reset --hard origin/main
```

Con **fetch + reset**, no con `pull` — la máquina de casa tuvo historia divergente en
julio y un pull normal recrea ese merge conflictivo. Ver `CLAUDE.md`.

---

# 1. Tareas de editor pendientes

Ordenadas por cuánto molestan hoy. Las de "rápido" son un casillero o un número.

## Cámara — arregla las barras de vida (rápido, alto impacto)

`Player Camera.prefab` está **`Untagged`**, y el único objeto con tag `MainCamera` en
`Mercenaries_Gamemode` es `Camara_Lobby`.

Como todo el código resuelve la cámara con `Camera.main`, los nameplates se orientan
hacia la cámara del lobby (fija) en vez de hacia la tuya: por eso se ven como carteles
estáticos que se leen bien desde un ángulo y mal desde otro.

- [ ] Taggear `Player Camera.prefab` como **`MainCamera`**

Es seguro con varios jugadores: la cámara se instancia **solo para el dueño**
(`if (base.IsOwner)` en `PlayerController`), así que en cada máquina hay una sola.

De paso fijate si se acomoda algo más: `GetWASDInputVector`, `FaceCameraForward` y
`GetAimPoint` salen de la misma `Camera.main`.

## Salto y caída

- [ ] En `AC_Player`, parámetro nuevo **`VerticalSpeed`** (Float)
- [ ] Estado **`Fall`** con `HumanM@Fall01`, en **Loop**
- [ ] `Movement → Fall`: `IsJumping` + `VerticalSpeed < -0.1` · Exit Time OFF · dur 0.15
- [ ] `Jump → Fall`: `IsJumping` · Exit Time **ON 0.9** · dur 0.15
- [ ] `Fall → Movement`: `IsJumping` = false · Exit Time OFF · dur 0.15
- [ ] **Destildar `Has Exit Time` en `Movement → Jump`** (hoy está en 0.8 y retrasa la
      animación de salto casi un segundo)

**NO pongas `Any State → Fall`.** Le roba el bucle aéreo al salto del bárbaro. El umbral
en `-0.1` y no en `0` evita el parpadeo en el punto más alto del salto.

## Escudo del Paladín

- [ ] **Borrar `PLACEHOLDER_HoldLoop` del Base Layer** — es lo que impide caminar
      bloqueando. El de UpperBody ya está bien configurado y no se toca.
- [ ] Crear **`PLACEHOLDER_HoldImpact`** en UpperBody (el `ShieldHitClip` ya está
      asignado en `GA_PaladinShieldBlock` y hoy no se ve nunca):
  - `Any State → HoldImpact`: `ActionID == 97` + `HoldImpactTrigger` · Exit Time OFF · 0.05
  - `HoldImpact → HoldLoop`: `IsHolding` = true · Exit Time **ON 0.9** · 0.1
  - `HoldImpact → Empty`: `IsHolding` = false · Exit Time OFF · 0.1

`RaiseClip` y `LowerClip` **dejalos vacíos**: el pack no trae esos gestos y son
opcionales. El blend de 0.1 s ya se lee como levantar el escudo.

## Nivel 3 / subclases

- [ ] En `Player Camera.prefab`, destildar **`OpenSubclassesOnMaxLevel`** del
      `UI_ClassMenu` (está serializado en `1`; el default del código ya es `false` pero
      el valor guardado le gana)
- [ ] *(Opcional)* Cablear **`LevelUpNotification`** del `UI_PlayerHUD`, que está en
      `None`. Sirve como recordatorio fijo; el anuncio del centro se desvanece y si
      estabas peleando te lo perdés.

## Alas del Ángel Vengador

- [ ] Sacar **`DemoAnimationSelector`** del prefab `AngelWings`
- [ ] En `AngelWings_Controller`, poner el valor por defecto de **`Mode` en `2`** (es el
      que corresponde a `Fly`; en `0` va a `Idle` y las alas se quedan quietas)
- [ ] Asignar el prefab a **`GE_AvengingAngelTag.TargetVFX`**
- [ ] `TargetVFXOffset`: Y ≈ 1.3-1.5, Z en negativo (que salgan de la espalda)

El prefab **no tiene colliders**, así que no hay que limpiarlo.

## Molinete del bárbaro

- [ ] Marcar **`Usable While Channeling`** en `GA_Frenzy` y `GA_BarbarianLeap` — son las
      dos únicas habilidades que deben poder usarse durante el molinete
- [ ] Tornado en `GA_WhirlwindAttack.VisualPrefab` (hoy está el círculo rojo), con
      **`VFX_AreaVisual`** en la raíz del prefab y su `RadiusAtScaleOne` medido

## Golpe final (Inmortal)

- [ ] **`HitboxOffsetY` ≈ 1.2** — hoy está en 0 y la caja nace a ras del suelo, así que
      el golpe apenas llega a la cintura
- [ ] Partir `HumanM@Attack2H02` en tres y asignar los `Charge*Animation`:
  - `_ChargeStart` (hasta el punto más alto)
  - `_ChargeLoop` (4-8 frames **en** el punto más alto, **Loop Time ON**)
  - `_Strike` (del punto más alto al final) → va en el `AnimationClip` de la habilidad

Si dejás el clip entero como `AnimationClip`, al terminar la carga levanta el arma otra
vez antes de pegar: se ve el gesto dos veces.

## Hacha arrojadiza

- [ ] `SpawnOffset.y` ≈ **1.4** (hoy sale de la panza)
- [ ] Material del spin blur en `PF_ExampleProjectile` →
      `AssetsExtra/Simple Spin Blur/Materials/Spin Blur Material.mat`

## Rogue

- [ ] `HumanM@Attack1H01_L` → Animation → **Root Transform Rotation → Offset**, para
      reducir el giro a la izquierda del segundo golpe del combo

**No lo lleves a cero**: algo de torsión es correcta para una puñalada con la izquierda.
El combo alterna derecha-izquierda a propósito (las cuatro clases del rogue llevan dos
dagas). Probá con la mitad de lo que tiene.

## Limpieza de nombres

- [ ] Renombrar `GE_Reckless 1`, `GE_Reckless 2`, `GE_Reckless 3` y `GA_ConeReckless 1`

Los cuatro están **en uso**, no son huérfanos. Renombrarlos es seguro: Unity mantiene el
GUID y las referencias no se rompen. Ese sufijo ya te hizo dudar una vez sobre cuál
asset estabas editando.

---

# 2. Balance pendiente (decisiones tuyas, no tareas mecánicas)

## El daño mágico cambió de raíz

Antes el `MagicDamage` del atacante se sumaba **automáticamente a cada golpe**. Ahora el
tipo de daño lo decide **el atributo del que escala el modificador**:

- escala de `Attack` → daño **físico** (paga armadura)
- escala de `MagicDamage` → daño **mágico** (ignora armadura, vale doble contra escudos)

Consecuencia: estas cinco clases **perdían 8-10 de daño en cada golpe** y ahora pegan
solo su físico, hasta que les armes habilidades con efectos mágicos.

| Clase | MagicDamage |
|---|---|
| `ASDef_Inmortal` | 8 |
| `ASDef_Paladin` | 8 |
| `ASDef_OathOfConquestPaladin` | 10 |
| `ASDef_OathOfDevotionPaladin` | 10 |
| `ASDef_OathOfVengeancePaladin` | 10 |

El Paladín es el que más lo siente. Hay que decidir qué habilidades suyas son mágicas y
ponerles un efecto que escale de `MagicDamage` (como ya hace `GE_SmiteDamage`).

- [ ] **`GA_SwordAttack` usa `GE_Class_Magic_Damage`** — era temporal para probar el daño
      mágico. Si es un espadazo normal, cambialo a `GE_Class_Damage`.

## Otros

- [ ] El `Speed Multiplier` de los dos `PlaceHolder_Action` tiene `ActionSpeedMult`
      asignado pero **la casilla desactivada**. No rompe nada (está igual en las dos
      capas), pero el ritmo de ataque no acelera la animación.
- [ ] `GA_BossAura.Radius` está en 2; debería ser 8-10.
- [ ] Revisar `DetectionRadius` / `AttackRange` / `LeashRadius` del jefe, que no calzan.

---

# 3. Plan hasta noviembre

El riesgo con tres meses y trabajando solo **no es que falten cosas: es que todo quede
al 80%**. Este orden es por dependencias y riesgo, no por ganas.

## 1º · Lobby

No por lo visual: es **lo que se rompe con nueve personas de verdad**. Hoy no sabés si
un nombre está repetido, cuántos hay por equipo, ni si alguien no eligió clase. Sin eso
no podés hacer un playtest decente, y sin playtests no podés evaluar nada de lo demás.

Además el **pool de espectadores** vive acá, así que resuelve dos cosas de una.

Ya existe `UI_LobbyMenu`; esto es extenderlo. Lo que falta:

- jugadores agrupados por equipo, con su clase elegida
- un `?` para quien no eligió todavía
- aviso de nombre repetido
- gate de "todos listos" para poder empezar
- lista de espectadores (no bloquean el inicio)

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

Cámara libre, sin UI.

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

# 4. Animaciones que siguen viéndose raras

No las dejes ahí. En esta sesión perseguimos cuatro causas distintas y al final **casi
todo era una sola**: confundir el yaw del cuerpo con el punto de mira. Es muy posible
que lo que queda también tenga un origen común.

El método que funcionó, para aplicarlo a cada caso:

1. **¿Es el transform o la pose?** Poné el peso de la capa UpperBody en 0 durante Play.
   Si el problema desaparece, es la capa/pose; si sigue, es la rotación del transform.
2. **¿Es el clip?** Miralo en la vista previa del FBX, sin el juego de por medio. Si ahí
   ya se ve torcido, el clip viene así.
3. **Si es el clip:** `Root Transform Rotation → Offset` te da respuesta visual
   inmediata, sin teorías.

Cuando lo retomes, anotá **cuáles se ven mal y con qué clase**, y vamos una por una.
