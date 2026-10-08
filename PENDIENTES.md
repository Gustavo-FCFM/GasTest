# Pendientes — actualizado el 8 de octubre de 2026

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

# ★ EN LA MÁQUINA DE CASA: borrar dos carpetas viejas (una sola vez) — HECHO (8 de octubre)

**Para Claude, si lee esto en la máquina de casa: hazlo tú antes de cualquier otra
cosa, con Unity CERRADO, y confírmaselo a Gustavo.**

El 25 de septiembre se borraron del proyecto `Assets/FishNet` (carpetas vacías, restos
del FishNet viejo antes de moverlo a `AssetsExtra`) y los ejemplos de TextMesh Pro. Pero
en la máquina de casa **las carpetas siguen existiendo**: el `reset --hard` no borra una
carpeta que tiene archivos ignorados adentro (ahí quedaron `.csproj` y `.sln` viejos).
Unity las vio, les creó `.meta`, y se colaron en el commit `d8f0fe4` del 26.

Ya se sacaron del repo otra vez y ahora el `.gitignore` las ignora, así que no se van a
volver a colar. Pero hay que borrarlas del disco para que Unity deje de verlas:

```bash
cd ".../Proyectos Gustavo/GasTest/GasTest"
git fetch origin && git reset --hard origin/main
rm -rf "Assets/FishNet" "Assets/FishNet.meta" "Assets/TextMesh Pro/Examples & Extras" "Assets/TextMesh Pro/Examples & Extras.meta"
```

- [x] Hecho en la máquina de casa (el trabajo ya lo hizo esta máquina el 28).
      **8 de octubre: Gustavo borró `Assets/FishNet` en la casa.** Si Unity la vuelve a
      mostrar ahí, borrarla otra vez con Unity cerrado.
      **29 de septiembre: a medias.** `TextMesh Pro/Examples & Extras` ya no está;
      `Assets/FishNet` sigue (solo `.csproj`/`.sln` viejos y sus `.meta`, ignorados por git).
      Falta borrar esa carpeta con Unity cerrado.

---

# ★ Inspector de habilidades nuevo — HECHO, falta probar (7 de octubre)

Pedido de Gustavo: cada GA ordenado por secciones plegables, los daños/efectos y los VFX
en listas con condiciones, sin perder lo ya cargado, y ajustar las formas desde la escena.
Compila limpio (juego y editor).

**Una vez, con Unity abierto (después de que recompile):**

- [x] `Mercenarios ▸ Guardar las habilidades en el formato nuevo`. Escribe en disco los
      assets de habilidad (incluidos los 9 de `48toPlay`) con las listas nuevas. Sin esto
      igual funcionan —cada uno se pasa solo al cargarse, en el editor y en la build—, pero
      se irían guardando de a uno a medida que se tocan.
- [x] Mirar en git que los `.asset` cambiaron como se espera, y subir. *(7 de octubre de 2026: corrido y subido; ningún asset conserva datos en los campos viejos.)*

**Qué cambió:**

- **Secciones** en todos los GA, siempre con el mismo nombre y en el mismo orden: General ·
  Costo y cooldown · Reglas de activación · Objetivo · Forma y alcance · Movimiento ·
  Tiempos · (las propias de cada una: Proyectil, La marca, Tótems...) · Efectos (GE) · VFX ·
  Animación · Sonido · Avanzado. Se pliegan con un clic y se recuerda cuáles; buscador
  arriba y "Abrir todo / Cerrar todo". Arriba de todo, una tarjeta con ícono, cooldown,
  cargas y cuántos efectos y VFX tiene.
- **Efectos (GE)**: UNA lista por habilidad (`GameplayAbility.Effects`). Cada entrada:
  cuándo (Al golpear / Al primer golpe / Al activarse / Al matar) · a quién (Enemigos /
  Aliados / El lanzador / Todos) · el GE · condición opcional (solo si el objetivo tiene
  un tag). Reemplaza a `DamageEffect`, `AdditionalEffects`, `TargetEffects`,
  `FirstHitEffects`, `EffectsToApply`, `AllyEffects`, `BuffEffect`, `CrowdControlEffect`,
  `InstantDamageEffect`, `DurationEffect`, `SelfEffects`... El orden de la lista es el
  orden en que se aplican.
- **VFX**: UNA lista (`GameplayAbility.Visuals`). Cada entrada: cuándo (Al lanzar / Al
  golpear / En el impacto), prefab, espera, offset, rotación, escala, si lo sigue, cuánto
  dura y "calzar con el área". Reemplaza a `HitVFX`, `ImpactVFX`, `VisualPrefab`,
  `FlashVFX`, `SpawnVFX`, `TornadoVFXPrefab` y a la vieja Visuals Sequence (que eran todas
  "Al lanzar").
- Lo que nunca se aplicaría se marca en **rojo** con el motivo (un "al golpear" en un buff
  propio, un "al activarse" para enemigos), y sale en los avisos de arriba.
- **Campos que se esconden** cuando no aplican, y conservan su valor: Max Range sin
  retícula, Follow Owner con retícula, los clips de apuntar sin Aim Before Throw, el
  retroceso con Recoil Speed en 0, Self If No Target apuntando a enemigos, TargetLayer en
  las que no buscan a nadie...
- **Tótems del Chamán**: una lista de opciones (nombre, descripción, ícono, prefab,
  cooldown) en vez de cinco listas en paralelo. Sirve igual para la rueda del Mago.
- **Golpe final** y **ataque cargado**: lo de "mientras carga" es una lista. **Orden de
  detenerse**: el castigo (`PunishEffects`) es una lista.
- **Vista previa en escena**: elegir un GA en el Project lo dibuja sobre el muñeco de
  prueba (`AbilityPreview`, ahora con silueta de 1.8 m y flecha hacia adelante) y se
  ajusta ARRASTRANDO las manijas: alcance, radio, ángulo, cajas de golpe, desde dónde sale
  el golpe, de qué mano sale el proyectil, a qué altura dispara. Con Ctrl+Z. Si no hay
  muñeco en la escena, el inspector tiene un botón para ponerlo.

**Lo que sale gratis:** cualquier GA acepta efectos "al activarse" (un buff propio al
lanzar) y, las que golpean, "al primer golpe", "al matar" (la invisibilidad del Marcado
para morir ahora es eso) y VFX "al golpear"; las de área y salto aceptan VFX "en el
impacto" del tamaño del área. Los de objetivo único (Enemigo jurado, Orden de detenerse,
Resurrección, Intercepción) aplican también sus efectos "al golpear" al elegido.

**Qué probar:**

- [x] Que todo pegue igual que antes: daños, aturdidos, la estela que cura del Castigo
      divino y del arco de luz, la Zona de verdad, la Luz del amanecer, la bandera del
      Comandante, los tótems de la Furia elemental.
- [x] VFX de golpe e impacto en las DOS pantallas (cambió la RPC de "al golpear"; Unity
      tiene que recompilar).
- [x] Rueda de tótems: nombres, descripciones y cooldowns como estaban.
- [ ] Proyectil devuelto por el parry del Guardián: sigue haciendo el daño que traía.
- [ ] Bots: siguen usando sus buffs (Comida de emergencia, Emboscada sombría) y sus aturdidos.

**Detalles que cambian (a propósito):**

- El sonido de impacto de un proyectil suena aunque no tenga VFX de impacto.
- La Ira inmortal le pega una vez a cada enemigo (antes, una por collider).
- La Furia elemental con Tick Rate 0 ya no se cuelga (mínimo 0.05 s).
- Marcado para morir: un solo sonido de impacto, en el centro (antes uno por enemigo).
- Conos y líneas eligen "el primer golpe" por cercanía (para "al primer golpe").

**Lo mismo para GE, clases y ASDef (7 de octubre, tarde):** los tres usan ahora la misma base
de inspector por secciones (`SectionedInspector`), sin renombrar ningún campo (los assets no
cambian). GE: tarjeta con duración, acumulaciones, tags y la fórmula de cada modificador
(`− 10 + 1.2 × Attack (del que lo aplica)`); lo que no aplica se esconde (el período en un
instantáneo, el desplazamiento si no desplaza...). Clases: tarjeta con el rol en su color,
los stats de partida y la fila del kit con los íconos de cada botón. ASDef: stats en una
línea y qué clases lo usan. En los cuatro, "¿Quién lo usa?" abajo (y si está en el registro
de red).

**Monje:** los puñetazos ahora son `GA_LineAttack` desde los hombros, el desarme no bloquea
sus golpes, y tiene íconos (habilidades, `GE_Ki`, `GE_MartialArts`, `GE_PatientDefense` y la
clase). Sin usar todavía: `4_attacks_icon`, `Stunning_Strikes` y los de las subclases.

**Más adelante:** cuando todos los assets estén guardados en el formato nuevo y subidos,
se pueden borrar los bloques `DATOS VIEJOS` del final de cada GA.

---

# ★ Fuego del Monje en rojo y su quemadura aparte — HECHO Y PROBADO ✅ (8 de octubre, noche)

Pedido de Gustavo: la quemadura del Monje (`GE_DragonBurn`) usaba el MISMO VFX que la del Clérigo
(`Radiant Aura Burn`, el "aura burn"), y todo el fuego del Monje tiene que ser muy rojo.

- [x] `Mercenarios ▸ Fuego rojo del Monje y su quemadura aparte (una sola vez)` y después
      **borrar `Assets/Scripts/Editor/MercMonkRedFireSetup.cs`**. *(Corrida por Gustavo; revisados los assets y borrada la herramienta.)* Hace:
  - `Art/VFX/VFX_DragonBurnAura`: copia de `Radiant Aura Burn` en rojo; `GE_DragonBurn` la usa.
    `GE_RadiantBurn` y `GE_HolyFireBurn` (Clérigo) siguen con la suya.
  - `VFX_FireZone` (las zonas del Dragón rojo): las fogatas pasan a `Magic fire pro red` de Hovl
    y además en rojo intenso.
  - Orbes del Maestro elemental: proyectil propio `Prefabs/Projectiles/PF_DragonOrb` (copia de
    `PF_Fireball` en rojo; `PF_Fireball` no se toca, lo usan otras bolas de fuego) y golpe propio
    `Art/VFX/VFX_DragonOrbHit` (copia del "Holy hit" en rojo). La Patada del viento sigue con el
    suyo: no es fuego.
  - "Rojo intenso": cada color de las partículas, luces y estelas pasa al tono rojo con mucha
    saturación (hasta el centro blanco del fuego), conservando brillo y transparencia. Los
    materiales no se tocan.
- [x] **El orbe es un objeto de red** *(quedó en DefaultPrefabObjects; PROBADO ✅ en un cliente: los orbes se ven rojos)*: la herramienta revisa que haya quedado en
      `DefaultPrefabObjects` y avisa en la consola si no. Si avisa: `Tools ▸ Fish-Networking ▸
      Utility ▸ Refresh Default Prefabs`. Probar en un cliente que los orbes se ven.
- [ ] Mirar: la quemadura del Monje roja y la del Clérigo como siempre; las zonas, los orbes y su
      golpe bien rojos. Si algo queda "demasiado" rojo o apagado, se ajusta a mano en su prefab.

---

# ★ Quemadura santa con zonas de fuego sagrado — HECHO Y PROBADO ✅ (8 de octubre, noche: "funciona")

Pedido de Gustavo: la definitiva del Clérigo de la Luz deja 3 zonas de fuego como las del Aliento
del dragón rojo, pero sagrado y amarillo. Compila limpio (juego y editor).

**Una vez, con Unity abierto (después de que recompile):**

- [x] `Mercenarios ▸ Zonas de fuego sagrado de la Quemadura santa (una sola vez)` y después
      **borrar `Assets/Scripts/Editor/MercHolyFireZonesSetup.cs`**. *(Corrida por Gustavo, que
      subió la fila a 5 zonas; herramienta borrada.)* Crea:
  - `Art/VFX/VFX_HolyFireZone`: el `VFX_FireZone` con las fogatas amarillas de Hovl
    (`Magic fire pro yellow`) en lugar de las naranjas.
  - `GE_HolyFireBurn` (`Effects/Damage`): la quemadura de la Luz, pero de 3 s y acumulable, una
    por tick (⚙ hasta 5). `GE_RadiantBurn` no se toca: la usan la Luz del amanecer y otras.
  - `GA_HolyFireZone` (`Cleric/LightDomain`): 5 s, un tick por segundo. A los enemigos les suma
    una quemadura sagrada; **a los aliados (y al Clérigo) les renueva la curación de la Luz**
    (`GE_RadiantHeal`), lo mismo que hace el cono. Si no la querés, es una entrada de su lista de
    efectos. No carga la definitiva.
  - En `GA_HolyFire`, sección nueva "Zonas donde apunta": 3 zonas en FILA, de costado a la mira y
    centradas donde apuntás (⚙ 5 m entre centros: se tocan), hasta 10 m (el alcance del cono).
  - Actualiza los registros de red.

- [x] **Rellenar el cono** (pedido de Gustavo, al ver la fila en el gizmo): `Zone Layout` nuevo en
      la sección "Zonas donde apunta". En `GA_HolyFire` ponerle **`Rellenar el cono`** y
      **`Zone Spacing` ⚙ 4.3** (≈ 1.7 × el radio: sin huecos). Con el cono de 10 m y 120° salen
      6 zonas: 4 en el arco de afuera y 2 cerca del Clérigo. El gizmo dibuja exactamente las que
      van a salir; más separación = menos zonas. En este modo `Zone Count` y `Zone Max Range` se
      esconden (no se usan): el cono manda, y sale hacia donde mira el cuerpo, como el golpe.

- [x] **Lo mismo para el Aliento de dragón rojo** (pedido de Gustavo): `GA_RedDragonBreath` tiene
      ahora la misma sección ("Zona por tick": `Zone Layout`, `Zone Count`, `Zone Spacing`). Por
      defecto sigue igual (una zona donde mira). Para rellenar: **`Rellenar el cono`** y
      **`Zone Spacing` ⚙ 3**: con su cono de 6 m y 120° y la zona de 2.5 m salen 3 por tick (a
      3.5 m, repartidas en la apertura), o sea hasta 9 vivas en los 3 s. Mirando siempre al mismo
      lado se encienden encima (la quemadura topa en 5); barriendo con la mira, pinta el piso.
      Si se ve pesado (cada zona son 7 fogatas), subir la separación o achicar el radio de
      `GA_DragonFireZone`.

Por dentro: los dos (`GA_ConeAttack.ImpactZone` y `GA_ChanneledCone.TickZone`) reparten con lo
mismo, en `GA_ContinuousAoE`: `EZoneLayout`, `DeployConeZones` y `ConeFillPoints` (el gizmo usa
los mismos puntos). Salen con el primer impacto; la fila, con la mira del instante.
Las piezas para soltar zonas desde otra habilidad quedaron en `GA_ContinuousAoE` (`Deployable`,
`ClampAimPoint`, `TryGroundPoint`) y el Aliento del dragón rojo usa las mismas.

**Qué probar:**

- [x] La Quemadura santa rellena el cono de zonas amarillas (o, en fila, donde apuntás).
- [x] Un enemigo adentro suma quemaduras sagradas (hasta 5); un aliado adentro se sigue curando.
- [ ] En un cliente: las zonas caen donde mira él y se ven en las dos ventanas.
- [x] ¿Se ve amarillo el fuego? (si las fogatas amarillas de Hovl no alcanzan, se le cambia el color).

---

# ★ Después de probar el Maestro elemental y el Shinobi — HECHO, falta probar (8 de octubre, tarde)

Pedidos de Gustavo. Compila limpio (juego y editor); Unity ya recompiló el código con el codegen
de FishNet (hay RPC nuevas en `PlayerController`).

**Una vez, con Unity abierto:**

- [x] `Mercenarios ▸ Aliento del dragón, humo del Shinobi y Emboscada (una sola vez)` y después
      **borrar `Assets/Scripts/Editor/MercBreathAndStealthSetup.cs`**. Hace todo lo de assets de
      abajo y actualiza los registros de red. *(Corrida por Gustavo; revisados los assets y
      borrada la herramienta.)*
- [x] **El fuego de la zona se veía magenta** (y las chispas). El paquete *Procedural fire* de
      Hovl viene para el pipeline viejo: `FireSphere2`, `SparkSphere` y `Trail67` usaban el
      `.shader` de superficie, que URP no dibuja. Gustavo los pasó a `Shader Graphs/FireSphere`
      (lo que pide la imagen del paquete); **ya se ve el fuego**. `Smoke26` sigue con un shader
      viejo, pero no se usa.

**Qué cambió:**

- **Wall jump: al aterrizar seguía resbalando.** El impulso del salto desde la pared (y lo que
  el Monje corría a lo largo de ella) ahora se corta al tocar el piso. El de un dash no cambia.
- **Defensa de agua apilada sin tope con la Paz mental.** `GE_WaterDefense`, `GE_PeaceOfMind`,
  `GE_ShadowCloak` y `GE_SilentDeathUntouchable` pasan a Refresh: uno solo, volver a lanzarlo
  reinicia la duración. *(Herramienta.)*
- **Aliento de dragón rojo, ahora sostenido:** con Ki, E echa fuego en cono (⚙ 6 m, 60°) durante
  3 s; un golpe por segundo (3 golpes, daño mágico) y en cada uno **quema el piso donde apunta
  la mira** (⚙ hasta 6 m). Mientras sale se puede mover y girar (el cono sigue a la mira), pero
  no usar otras habilidades; aturdido o silenciado se corta. Animación de prueba: CastingEnter /
  CastingIdle / CastingExit de Kevin Iglesias. **No tiene VFX del fuego del cono** (no hay un
  lanzallamas en los packs): se ven las zonas que va dejando; si aparece uno, va en la lista VFX
  de `GA_RedDragonBreath` "al lanzar" con Destroy Time 0 (dura lo que el aliento).
- **La zona de fuego (quemadura del piso):** dura 5 s y cada segundo le pone una acumulación de
  quemadura a quien la pisa. `GE_DragonBurn` pasa a acumularse (⚙ hasta 5); el Aliento de fuego
  sin Ki también suma de a una. *(Herramienta.)*
- **Asesino: crítico mejorado más rápido estando invisible.** Mientras está invisible, la
  ventana por objetivo (6 s) y la reutilización (2 s) corren ⚙ 3 veces más rápido.
  `Invisible Recovery Multiplier` en el `FirstStrikeCritModifier` de `Assassin_Behaviour`.
- **Asesino: ventana de 6 s al salir de la invisibilidad.** Si mata algo en ese rato (jugador,
  NPC, cualquier cosa con vida), la Emboscada sombría vuelve a estar lista. Mientras dura la
  ventana tiene un buff con el ícono de la Emboscada (`GE_AmbushKillWindow`). El golpe que mata
  desde la invisibilidad también cuenta. Un reinicio por cada salida. *(Herramienta: agrega
  `AmbushKillReset` a `Assassin_Behaviour`.)*
- **Shinobi: humo.** El mismo humo de la Emboscada del Asesino al ocultarse (Manto), al salir y
  al llegar del Paso de las sombras, y **en cada salto mientras está oculto** (del piso o de una
  pared). El humo de los saltos lo ven todos, también los enemigos: delata por dónde va. Si no
  lo querés así, se puede hacer que solo lo vean los aliados. *(Herramienta.)*

**Piezas nuevas de código:**

- `GA_ChanneledCone` (genérica): cono sostenido con ticks, que puede soltar una zona por tick
  (`TickZone`, un `GA_ContinuousAoE` que se reusa tal cual con `DeployZoneAt`).
- `PlayerController` → "MIRA EN VIVO": el servidor le pide al dueño que le mande la mira mientras
  dura un canalizado (en el host no hace falta).
- `GameplayEffect.JumpVFX`: un VFX en cada salto mientras dura el efecto (sección VFX).
- `AbilitySystemComponent.OnKilledTarget`: el que mató se entera (server-side).
- `AmbushKillReset` (pasiva del Asesino).

**Qué probar:**

- [ ] Wall jump con Pícaro y Monje: al aterrizar se frena ahí, sin resbalar.
- [ ] Paz mental + Defensa de agua varias veces: un solo buff en la barra, que se reinicia.
- [x] Aliento de dragón rojo: 3 golpes en 3 s, una zona por golpe donde mirás; girando, el cono y
      las zonas siguen la mira; cada zona dura 5 s y suma una quemadura por segundo (hasta 5).
      En un cliente (no el host) también: que las zonas caigan donde mira ÉL. **PROBADO ✅ en el
      cliente (8 de octubre).**
- [ ] Aturdido o silenciado a mitad del aliento: se corta (barra roja) y puede volver a usar todo.
- [ ] Asesino: invisible, el aviso del crítico mejorado vuelve antes; salir de la invisibilidad
      y matar a alguien (o un NPC) en 6 s → la Emboscada lista; pasados los 6 s, no.
- [ ] Shinobi: humo al ocultarse, en las dos puntas del Paso de las sombras y en cada salto
      oculto (piso y pared), en las dos ventanas.

---

# ★ Paredes (Pícaro y Monje), Ki, Ráfaga y escudos — HECHO, falta probar (8 de octubre)

Compila limpio (juego y editor, con el Roslyn de Unity; el codegen de FishNet lo corre Unity
al recompilar: hay dos RPC que cambiaron).

**Una vez, con Unity abierto (después de que recompile):**

- [x] `Mercenarios ▸ Paredes, Ki, Ráfaga y costo de los escudos (una sola vez)` y después
      **borrar `Assets/Scripts/Editor/MercWallAndShieldSetup.cs`**. *(8 de octubre: corrida y
      borrada; revisados los assets.)* Hizo todo esto:
  - Crea `Attributes/Rogue/WallMove_Rogue` y `Attributes/Monk/WallMove_Monk` y se los pone
    a `Class_Rogue`, `Class_Monk` y a todas sus subclases (si ya existen, no les toca los números).
  - `GA_Ki`: 2 cargas (antes 3).
  - `GA_FlurryOfBlows`: 4 puñetazos (der., izq., der., izq.) después de las 4 acumulaciones;
    antes eran 2. El combo entero se comprime al ritmo de ataque, así que sale más rápido.
  - `GE_Cost_Shield` nuevo (`Effects/Costs`, −5 de energía) como costo de los tres escudos
    (`GA_PaladinShieldBlock`, `GA_FighterShieldBlock`, `GA_MonkBlock`: los usan también sus
    subclases), y el mínimo para levantarlos baja de 10 a 5: con 5 o menos no se levanta.
  - Actualiza los registros de red.

**Cómo funciona (decisiones de Gustavo, 8 de octubre):**

- Se engancha solo **en el aire**, yendo con el WASD contra una pared casi vertical. No a menos
  de 0.8 m del piso (para no pegarse al saltar al lado de una pared).
- **Pícaro (pegarse):** se frena y queda pegado 1 s; después resbala 0.5 s y se suelta.
- **Monje (correr):** WASD lo lleva a lo largo de la pared (solo de costado), bajando de a poco
  hasta tocar el piso. Si la pared se acaba, cae con el impulso que traía. Se ven las piernas
  corriendo y el modelo inclinado 20° hacia afuera (en todas las pantallas).
- **Espacio** salta hacia AFUERA de la pared (7 m/s) y hacia arriba (7 m/s). Un cuarto de
  segundo el WASD no puede empujar de vuelta contra ella. **Empujar hacia afuera** lo suelta.
  **El salto se desvía hacia donde mira la cámara, hasta 30° a cada lado** (pedido de Gustavo,
  8 de octubre): nunca sale derecho a donde mira. `Jump Aim Max Angle` en los perfiles.
  - [ ] Probar: pegado, mirar a la izquierda o la derecha y saltar → sale torcido hacia ese lado.
- **Saltos encadenados sin límite**, pero a la pared de la que se acaba de soltar no se vuelve
  a pegar hasta tocar el piso.
- Pegado **se pueden usar las habilidades**; las de movimiento (dash, Blink, la patada) salen
  desde la pared y lo sueltan. **Con la bolsa del objetivo también se puede.**
- Lo sueltan: aturdido, enraizado, repelido, volando, morir, cambiar de clase.
- No cuentan como pared: las paredes invisibles (Ignore Raycast, los límites de la arena), los
  personajes, tótems y NPCs (todo lo que tiene ASC), lo que tiene Rigidbody y los triggers.
- Los números están en los perfiles `WallMove_*` (inspector con secciones), y la clase muestra
  en su tarjeta si se pega o corre. Los bots no lo usan.

Por dentro: sección "MOVIMIENTO EN PAREDES" de `PlayerController` (todo en el dueño, como el
dash); `WallMovementProfile` + `CharacterClassDefinition.WallMovement` (sección Movimiento). La
inclinación viaja en la RPC de locomoción (dos bytes más) y se aplica al modelo en
`LateUpdate`, junto con el giro del molinete.

**Qué probar:**

- [ ] Pícaro: saltar contra una pared → se pega 1 s, resbala y se suelta; Espacio pegado →
      salta hacia afuera; entre dos paredes enfrentadas, subir saltando de una a otra; que no
      se vuelva a pegar a la misma sin tocar el piso.
- [ ] Monje: correr a lo largo de una pared, que baje de a poco y termine en el piso; que la
      pared se acabe a mitad de camino; saltar hacia afuera; la patada desde la pared.
- [ ] Que NO se pegue a: los límites invisibles de la arena, otros jugadores, tótems, la barrera
      de un escudo.
- [ ] Atacar pegado (dagas, puños, el escudo); aturdido o empujado pegado → se cae.
- [ ] Con dos ventanas: que el otro vea la pose (Monje corriendo inclinado, Pícaro pegado).
- [ ] La Ráfaga con 4 golpes (¿se ve bien a esa velocidad?) y el Ki con 2 cargas.
- [ ] Escudos: levantar cobra 5 de energía; con 5 o menos no se levanta.
- [-] Animaciones de pared propias: no hay en el pack de Kevin Iglesias (se usan caer y correr);
      del artista si las hace.

---

# ★ Samurái (subclase del Monje) — HECHO Y PROBADO ✅ (8 de octubre), salvo la franja del Corte final

Diseño de Gustavo (ver "Subclases del Monje — LO DECIDIDO" más abajo); los números ⚙ los puso
Claude. Compila limpio (juego y editor).

**Una vez, con Unity abierto (después de que recompile):**

- [x] *(Corrida por Gustavo; herramienta borrada.)* `Mercenarios ▸ Crear el Samurái (subclase del Monje, una sola vez)` y después **borrar
      `Assets/Scripts/Editor/MercSamuraiSetup.cs`**. Crea todo en `GameplayAbilities/Monk/Samurai`,
      los GE en `Effects/`, `ASDef_Samurai` y `Class_Samurai` en `Attributes/Monk`, la cuelga de
      `Class_Monk` y actualiza los registros. Busca los íconos por nombre (`Samurai_Attack`,
      `Samurai_Slash`, `4_attacks_icon`, `Final_slash_monk`, `Class_Monk_Samurai_Icon`).

**El kit:**

| Ranura | Qué hace | Pieza |
|---|---|---|
| Stats | Los del Monje a nivel 3: 180 de vida, 6 de ataque. Rol: Daño. La espada del Guerrero en la mano (la "katana") | `ASDef_Samurai`, `Class_Samurai` |
| Clic izq. | Dos cortes en arco (⚙ 3 m, 120°), daño de clase y una herida cada uno (`GE_Wounds` del Pícaro, hasta 10). Con Ki, la Ráfaga: las 4 acumulaciones y 4 cortes | `GA_SamuraiAttack` → `GA_SamuraiStrikes` / `GA_SamuraiFlurry` (+ `GA_SamuraiFlurryStacks`), cortes `GA_SamuraiSlash` con los clips del combo del Guerrero |
| Clic der. | El bloqueo y la Defensa paciente del Monje, pero cada golpe que frenan le pone una herida al atacante (también a distancia y en el parry) | `SamuraiBehaviours` (copia de `MonkBehaviours`): `Entity_ShieldBarrier.OnBlockAttackerEffect` = `GE_Wounds` |
| Pasiva extra — Afilar | Cada acumulación de Artes marciales también da +1 de ataque | `GE_MartialArtsSharpen` en su `MartialArtsPassive` |
| Q | Ki, como el Monje | `GA_Ki` |
| Shift | La Patada voladora y la del dragón, y las dos ponen una herida (pedido de Gustavo, 8 de octubre) | `GA_SamuraiKick` → `GA_SamuraiFlyingKick` / `GA_SamuraiDragonKick` |
| E — Corte giratorio | Área alrededor (⚙ 3.5 m, 1.5 × ataque) con heridas; ⚙ 8 s. Con Ki, **Cortes devastadores**: 1 corte + 1 por acumulación; con acumulaciones, CADA corte lo cura igual a su ataque de antes de gastarlas (6 + 1 por acumulación); al terminar las gasta | `GA_SamuraiSpin` → `GA_SpinningSlash` / `GA_DevastatingCuts` (`GA_InstantAoE` con `ExtraHitsPerStack`) + `GE_SpinningSlashDamage`, `GE_SamuraiCutHeal` |
| R — Corte final | Mantener R muestra una franja en el piso (⚙ 12 m, recortada en la primera pared) y **marca con un contorno rojo a los enemigos que va a alcanzar**; soltar la recorre a ⚙ 30 m/s golpeando a todos (⚙ 2 × ataque), les pone una marca de ⚙ 4 s (**con la marca, al 5 % de vida mueren**) y una herida por acumulación; al terminar las gasta | `GA_FinalCut` (`GA_RushAttack` con `AimFirst`) + `GE_FinalCutDamage`, `GE_FinalCutMark` |

**Piezas nuevas de código (sirven para las tres subclases):**

- **Acumulaciones** (sección nueva en cada GA): `StacksTag` (qué cuenta: `Status_MartialArts`, tag
  nuevo al final del enum que da cada acumulación) y `ConsumeStacks` (no / al activarse / al
  terminar). Cada entrada de Efectos tiene "Con las acumulaciones": solo con acumulaciones, una
  vez por acumulación, o duración por acumulación. Sin acumulaciones, lo que escala no se aplica.
  Un combo le pasa lo que leyó a sus pasos (para la Ráfaga aturdidora del Maestro elemental).
- `GA_InstantAoE.ExtraHitsPerStack` + `RepeatInterval`: golpes extra por acumulación.
- `GameplayEffect.ExecuteBelowHealth`: con el efecto encima, al quedar en ese % de vida muere (la
  baja es de quien lo puso). No mata a un inmune ni a un inmortal.
- `Entity_ShieldBarrier.OnBlockAttackerEffect`: un GE al atacante por cada golpe frenado.
- `GA_RushAttack.AimFirst` + `ILineTargetAbility`: la franja de apuntar en `UI_GroundTargetIndicator`
  y el contorno de los objetivos (`CharacterOutline`, sacado de `DeadAllyHighlighter`, que ahora lo usa).

**Qué probar:**

- [x] **PROBADO ✅ (8 de octubre, "está bien").** Los cortes ponen heridas; bloquear hiere al
      atacante; Afilar; los Cortes devastadores con y sin acumulaciones; los cortes extra en red.
- [ ] **R: la franja NO se veía** (8 de octubre). No era la franja: el marcador del piso
      (`UI_GroundTargetIndicator`) no estaba puesto en NINGUNA escena ni prefab, así que ninguna
      habilidad de zona mostró nunca nada. Ahora se crea solo y dibuja una caja rellena con borde
      (la franja) o un círculo relleno con borde (las zonas). Probar: la franja sigue la mira, se
      corta en las paredes, marca a los enemigos de adentro (y no a un invisible); al soltar los
      atraviesa a todos. Un enemigo con la marca que baja al 5 % muere y la baja es del Samurái.
- [ ] De paso, ahora SÍ se ve el círculo de las demás habilidades de zona: Marcado para morir del
      Asesino, Salto heroico del Comandante, las áreas "en la retícula".
- [ ] Las patadas del Samurái ponen herida.
- [-] ⚙ Animaciones: el corte giratorio usa el clip del molinete (`MagicAttackOmni01`) y el Corte
      final el corte del Guerrero; cambiarlos si hay algo mejor.

---

# ★ Maestro elemental y Shinobi (subclases del Monje) — CREADOS, falta probar (8 de octubre)

Los creó una herramienta corrida en batch con Unity cerrado (ya borrada); compila con Unity, con el
codegen de FishNet. Diseño de Gustavo (ver "Subclases del Monje — LO DECIDIDO"); números ⚙ de
Claude. Las tres subclases ya cuelgan de `Class_Monk`.

**Maestro elemental** (`Class_ElementalMaster`: 180 de vida, 6 de ataque, ⚙ 5 de daño mágico, rol Daño):

| Ranura | Qué hace | Pieza |
|---|---|---|
| Clic izq. | Cada puñetazo lanza además un ORBE mágico: un proyectil recto (la bola de fuego), ⚙ 10 m, 1 × daño mágico. Los orbes no tienen animación propia: anima el puño | `GA_ElementalAttack` → `GA_ElementalStrikes` (puño, orbe, puño, orbe) con `GA_ElementalOrbRight/Left` |
| Clic izq. con Ki | **Ráfaga aturdidora**: gasta las acumulaciones, le vuelven las 4 de la Ráfaga, y cada puñetazo aturde 0.5 s por cada acumulación gastada (0 = no aturde). 4 puñetazos con sus orbes | `GA_StunningFlurry` (Acumulaciones: gastar al activarse) + `GA_StunningPunchRight/Left` (aturdido "duración por acumulación") |
| Clic der. con Ki | **Defensa de agua**: la Defensa paciente, y sus primeros ⚙ 2 s el daño que recibe lo CURA | `GA_ElementalGuard` → `GA_WaterDefense` + `GE_WaterDefense` (tag nuevo `Status_DamageToHeal`) |
| Shift con Ki | **Patada del viento**: atraviesa, repele ⚙ 4 m y aturde a todos los que toca | `GA_ElementalKick` → `GA_WindKick` + `GE_WindKickPush` |
| E — Aliento de fuego | Cono (⚙ 5 m, 70°) de daño mágico que quema ⚙ 3 s; ⚙ 8 s. Con Ki, **Aliento de dragón rojo** (cambiado el 8 de octubre a la tarde, ver arriba): 3 s de fuego sostenido en cono, un golpe por segundo, y cada golpe deja una zona de fuego donde apunta la mira (5 s, una quemadura por segundo) | `GA_ElementalBreath` → `GA_FireBreath` / `GA_RedDragonBreath` (`GA_ChanneledCone`) con la zona `GA_DragonFireZone` (`GA_ContinuousAoE`) + `GE_DragonBurn`. **VFX nuevo `Art/VFX/VFX_FireZone`**: 7 fogatas de Hovl en un círculo, se estira al tamaño de la zona |
| R — Paz mental | ⚙ 6 s: +30 % de resistencia al daño y TODO sale con Ki sin gastarlo | `GA_PeaceOfMind` + `GE_PeaceOfMind` (tag nuevo `Status_PeaceOfMind`); las variantes con Ki tienen `Also Active With Tag` |

**Shinobi** (`Class_Shinobi`: 180 de vida, 6 de ataque, la daga del Pícaro en la mano, rol Daño):

| Ranura | Qué hace | Pieza |
|---|---|---|
| Clic izq. | Dos KUNAIS (el lanzamiento de dagas del Pícaro, sin apuntar), ⚙ 15 m; **crítico por la espalda** (el `BackstabDamageModifier` del Pícaro). Con Ki, la Ráfaga: 4 kunais, y el primero gasta las acumulaciones: **Artes oscuras**, 5 % de la vida faltante por cada una | `GA_ShinobiAttack` → `GA_ShinobiStrikes` / `GA_ShinobiFlurry`; `GA_Kunai`, `GA_KunaiDarkArts` + `GE_DarkArtsDamage` |
| Clic der. | El bloqueo y la Defensa paciente **esquivan cualquier proyectil**: lo atraviesan sin hacerle nada (y la barrera no los para); el cuerpo a cuerpo lo frenan como siempre | `GA_ShinobiGuard` → `GA_ShinobiBlock` / `GA_ShinobiPatientDefense` + `GE_ShinobiBlock`, `GE_ShinobiPatientDefense` (tag nuevo `Status_DodgeProjectiles`) |
| Shift con Ki | La Patada del dragón, y su golpe gasta las acumulaciones (Artes oscuras) | `GA_ShinobiKick` → `GA_ShinobiDragonKick` |
| E — Manto de oscuridad | 2 cargas (⚙ 10 s): invisible ⚙ 5 s, más rápido, el próximo golpe es crítico (el de la Emboscada del Asesino). Con Ki, **Paso de las sombras**: además se teletransporta, el Destello del Clérigo con el DOBLE de alcance; mirando una pared aparece contra ella (si cabe) y queda enganchado. Humo al ocultarse, en las dos puntas del Paso y en cada salto oculto (8 de octubre a la tarde) | `GA_ShinobiCloak` → `GA_ShadowCloak` / `GA_ShadowStep` (`GA_Teleport` con `AllowWallLanding`) + `GE_ShadowCloak` (`JumpVFX`) |
| R — Muerte silenciosa | Elige al enemigo de la mira (⚙ 15 m), aparece a su espalda: su daño + 30 % de su vida faltante. Intocable (inmune e imparable) toda la cadena; a los 0.5 s salta al siguiente a ⚙ 10 m (primero JUGADORES, el que más vida le falta), hasta que no quede ninguno sin golpear | `GA_SilentDeath` (genérico nuevo **`GA_ChainStrike`**) + `GE_SilentDeathDamage`, `GE_SilentDeathUntouchable` |

**Piezas nuevas de código:** `GA_ChainStrike`; `GA_TagSwitch.TagVariant.AlsoActiveWithTag` (la
variante sale con ese tag sin gastar nada) y el switch se APUNTA si la variante de ahora se apunta;
`Status_DamageToHeal` en el pipeline de daño; `Status_DodgeProjectiles` en `GC_Projectile`;
`GA_Teleport.AllowWallLanding` + el enganche a la pared al llegar (`PlayerController.TeleportTo`).
Los tres tags nuevos, al final del enum.

**Qué probar:**

- [ ] Maestro elemental: los orbes salen con cada puñetazo y pegan de lejos (daño mágico).
- [ ] Ráfaga aturdidora con 0, 2 y 4 acumulaciones: no aturde / 1 s / 2 s; después tiene 4.
- [ ] Defensa de agua: los primeros 2 s los golpes curan (número verde); después frena como siempre.
- [ ] Patada del viento: atraviesa a varios, los empuja y los aturde.
- [ ] Aliento de fuego: el cono quema. Con Ki: ver el Aliento de dragón rojo sostenido arriba
      (¿se ve bien el VFX de la zona y del tamaño correcto?).
- [ ] Paz mental: 6 s en los que clic izq./der., Shift y E salen con Ki sin gastar el Ki.
- [ ] Shinobi: kunais por la espalda hacen crítico (número amarillo). Con Ki, el primero de la
      Ráfaga hace más daño con más acumulaciones.
- [ ] Bloqueo / Defensa paciente: un hacha, una daga o un arco lo atraviesan sin dañarlo (y
      siguen); un golpe cuerpo a cuerpo se frena normal.
- [ ] Manto: invisible, rápido, el primer golpe crítico; 2 cargas. Con Ki, Paso de las sombras:
      el doble de lejos; mirando una pared, aparece pegado a ella.
- [ ] Muerte silenciosa con 3+ enemigos: salta de uno a otro, primero jugadores y el más herido,
      a cada uno una vez; nadie le hace daño mientras dura.
- [ ] En red (dos ventanas): los orbes y kunais se ven, los saltos de la Muerte silenciosa, la zona
      de fuego.
- [-] ⚙ Animaciones: el Aliento usa `MagicAttackDirect1H01_R` y la Muerte silenciosa el golpe del
      cono de dagas; los kunais, el lanzamiento de dagas (el mismo clip para los dos).

---

# ★ Menús de clase más compactos e ícono de clase en las barras — CÓDIGO HECHO, falta probar (8 de octubre)

Pedido de Gustavo: con más clases todo se estiraba. Compila limpio. No toca prefabs ni assets:
todo se arma por código.

- **Sala (pantalla principal): clases agrupadas por rol.** Arriba del selector, una pestaña
  por rol del color del rol —`Tanks`, `Damage`, `Supports`, y `Other` si alguna clase no tiene
  rol— con cuántas clases tiene; debajo, solo las del rol abierto. Se abre en la pestaña de tu
  clase. Una clase BASE no tiene rol (no tiene definitiva): aparece con el de la mayoría de sus
  subclases (`CharacterClassDefinition.DisplayRole`). Hoy: Bárbaro y Guerrero en Tanks, Pícaro
  en Damage, Paladín y Clérigo en Supports.
- [ ] **El Monje no tiene subclases todavía, así que sale en `Other`: ponerle `Menu Role` =
      Damage en `Class_Monk` (sección Identidad).** Lo mismo con cada clase base nueva hasta que
      tenga subclases. El inspector de la clase avisa cuando falta.
- **Menú de clases en partida (C / V): tarjetas en grilla.** Desde 5 clases, dos filas; con
  menos (las subclases), una. Tarjetas, nombre, ícono y descripción más chicos (la descripción
  se achica sola si no entra). Los tamaños se ajustan en `UI_ClassMenu` del prefab
  `Player Camera` (sección "Tarjetas (grilla)").
- **Ícono de la clase al lado de la barra de vida** de aliados y enemigos (jugadores y bots),
  a la DERECHA de la barra (a la izquierda, arriba, van los buffs). Cambia solo al cambiar de
  clase o evolucionar. Tamaño y posición en `UI_WorldHealthbar` del prefab `Player`
  (`Class Icon Size`, `Class Icon Offset`; `Show Class Icon` lo apaga).

**Qué probar:**

- [ ] La sala: las pestañas cambian las clases, el recuadro no salta de tamaño, elegir y
      confirmar sigue funcionando (también eligiéndole clase a un bot).
- [ ] El menú C con 6 clases en dos filas de 3; el V con las 3 subclases en una fila; las
      teclas 1–6 y el control siguen eligiendo la tarjeta correcta.
- [ ] El ícono al lado de la barra: que se vea en aliados y enemigos, que cambie al cambiar de
      clase, y que no choque con los buffs.

---

# ★ QUÉ SIGUE (rumbo al showcase de diciembre, ~9 semanas)

En orden de lo que más mueve la demo:

**Regla desde el 5 de octubre: no se hacen modelos ni VFX nuevos.** Ya se están haciendo
los modelos del juego y el artista hará los VFX: hasta entonces se reusan los modelos que
hay (espada, bastón como lanza) y los VFX gratis. Los pendientes de ese tipo quedan
marcados "Pospuesto".

**Estado al 5 de octubre:** Clérigo completo y probado. Guerrero: kit base, Maestro de
batalla y Comandante hechos y probados (falta el aturdido al apuntar, el jueves 9).
**Guardián: hecho y probado ✅** (5 de octubre). **El Guerrero queda completo** (kit base y las 3 subclases).
**6 de octubre:** barra de carga por etapas del ataque cargado (`UI_ChargeBar`), repeler / atraer y vuelo libre: hechos y probados. **Monje: kit base hecho y probado (8 de octubre). Barra de canalizar (`UI_CastBar`): hecha.** Queda la mascota con IA en red.

1. **Clases nuevas — DECIDIDO (28 de septiembre): Clérigo + Guerrero completos**, en ese
   orden. Detalle y estimación en la sección 6.

   **Clérigo — kit base: HECHO Y PROBADO ✅ (28 de septiembre, commit `2786174`).**
   Números como los dejaste en el editor:

   | Ranura | Habilidad | Pieza |
   |---|---|---|
   | Pasiva | Bendición: los aliados que cura reciben +10 % de resistencia 5 s | `ClericBlessingPassive` (nueva) |
   | Clic izq. | Arco de luz: atraviesa; 1.0× daño mágico a enemigos (`GE_Class_Magic_Damage`), 2.0× curación a aliados (`GE_SmiteHealing`); vuela hasta chocar (`LifeTime` 0) | `GA_ProjectileShoot` |
   | Clic der. | Curación al aliado de la mira (o a uno mismo), 2.0× (`GE_SmiteHealing`), 4 cargas de 3 s | `GA_Target` |
   | Q | Guiding Bolt (Luz guía): rayo muy rápido (100) que atraviesa, 2.0× y +20 % de daño recibido 5 s (`GE_Bane`) | `GA_ProjectileShoot` |
   | Shift | Destello: estilo Misty Step, la mira es la guía. Mirando un piso, ahí; mirando una pared o una plataforma por abajo, al piso de ENCIMA (o si no, al de abajo); mirando al aire, al piso más cercano bajo ese punto. Caminando de lado o hacia atrás, en esa dirección lo más lejos posible, mirando al frente. Siempre sobre piso; no cruza paredes invisibles, rejas ni muros | `GA_Teleport` (nueva, con `DirectionalTeleport`) |

   Estadísticas de partida: 80 vida, 4 defensa, 6 ataque, 8 daño mágico, 0.8 s entre
   ataques (29 de septiembre; antes 0.9). La curación se llama ahora `GA_HealingWord`
   (antes `GA_ClericHeal`, mismo asset). Guiding Bolt y el Destello no se pueden usar
   aturdido ni silenciado (el Destello tampoco enraizado).

   - [x] **Correr `Mercenarios ▸ Actualizar los registros de red`** (hecho: los registros se rehicieron varias veces desde entonces): el registro de
         efectos todavía apunta a `GE_GuidingLightDamage`, que se borró (Guiding Bolt
         ahora usa el daño mágico común). `GE_Bane` es el viejo `GE_GuidingLightMark`
         renombrado: ese sigue bien. El bot elige su rol por `EClassRole` (el Clérigo juega de Support). Ya tiene
   íconos, `AOC_Cleric` y el bastón (`Quarterstaff 1`) en la mano derecha.

   ⚠ **`GE_SmiteHealing` ahora escala con daño mágico** (antes con ataque) y lo comparte
   el Castigo divino del Paladín (`GA_SmiteBeam` y `GA_SmiteBeamConquest`). Hoy da igual
   porque el Paladín tiene 8 y 8, pero si algún día sube uno solo de los dos, la curación
   de su rayo cambia con él. Si querés separarlos: duplicar el GE para el Clérigo.

   **El libro en la mano izquierda — QUITADO por ahora (29 de septiembre).** La pose fija
   dejaba la mano siempre hacia abajo y se veía raro. El código quedó (campo `Off Hand
   Pose` de cada clase y la capa `OffHandPose` de `AC_Player`) por si el artista hace
   una pose que sirva; `Human_MageBook.prefab` se borró.

   **Arte (animaciones, esqueletos, VFX y sonido): lo toma el artista contratado.**
   Lo de arte de esta lista queda para él.

   - [x] El `FlashVFX` del Destello (artista).
   - [x] La Bendición también cuando el Clérigo se cura a sí mismo (29 de septiembre):
         aviso nuevo `ASC.OnHealedSelf`, aparte de `OnHealedAlly` para que curarse NO
         cargue la definitiva. **PROBADO ✅ (5 de octubre)** (clic derecho sin nadie en la
         mira, herido: tiene que aparecer `GE_Blessing` 5 s).
   - [x] El VFX de la curación sigue al curado (29 de septiembre): `GA_Target` tiene
         `Attach Impact VFX`, `Impact VFX Offset` y `Impact VFX Lifetime`, y la red manda
         al OBJETIVO (`NetworkASC.ServerPlayAbilityVFXOn`) en vez de un punto. Vale para
         todo `GA_Target`. **PROBADO ✅ (5 de octubre)** (dejar que Unity recompile:
         hay una RPC nueva). Para el aura `Healing` de Hovl, probar Offset (0, 0, 0).

   **Subclases del Clérigo.** Cada una es su propio `Class_*.asset` con kit completo,
   colgado de `Class_Cleric` en `AvailableSubclasses`, igual que los juramentos del
   Paladín. Armas nuevas (hoz, martillo) y colores de los hechizos: del artista;
   mientras, el bastón. Los números los da Gustavo (decidido el 30 de septiembre).

   **Dominio de la vida — HECHO Y PROBADO ✅ (30 de septiembre, commit `e81d9d1`).**

   | Ranura | Habilidad | Pieza |
   |---|---|---|
   | E | Preservar vida: el aliado de la mira (10 m) no baja de 1 de vida por 3 s. No se lanza sobre uno mismo | `GA_PreserveLife` (`GA_Target`) + `GE_PreserveLife` (`Status_Immortal`) |
   | Pasiva extra | Médico bendecido: curación × (1 + 0.3 × vida faltante del curado), hasta +30 % | `BlessedHealerPassive` sobre `IHealModifier` (nuevo: el gemelo de `IDamageModifier` para curaciones) |
   | R | Resurrección: revive al aliado muerto de la mira (10 m) donde cayó, con TODA la vida y sus cooldowns como estaban; su vuelta a la base se cancela. Ventana: los 5 s de reaparición | `GA_Resurrection` (busca el CUERPO: con ragdoll el muerto no tiene cápsula y `GA_Target` no lo ve) + `NetworkASC.ServerResurrect` + `NetworkGameManager.CancelRespawn` |
   | Visual | Contorno de los aliados muertos, solo en su pantalla: VERDE si la R está lista y el cuerpo a 10 m, ROJO si no. Sigue al ragdoll | `DeadAllyHighlighter` + shader `Mercenaries/Outline` (`Assets/Shaders/Resources`) |

   Estadísticas (nivel 3): 180 vida, 4 defensa, 8 ataque, 10 daño mágico, 0.8 s entre
   ataques; el kit base igual. Los cooldowns de E y R están en 0 en la habilidad:
   mandan `GE_Cooldown_Extra` y `GE_Cooldown_Ultimate`, para ajustarlos ahí.

   **Dominio del orden — HECHO (30 de septiembre, herramienta corrida y funcionando).** Números de Gustavo; los que no vinieron los puso Claude (marcados ⚙).

   | Ranura | Habilidad | Pieza |
   |---|---|---|
   | Clic izq. (base, Vida y Orden) | **Combo nuevo**: bastonazo en cono (físico, `GE_Class_Damage`) + el arco de luz de siempre (mágico) | `GA_ClericStaffCombo` = `GA_ClericStaffSwing` (copia del cono del Paladín) + `GA_ClericLightArc` |
   | Clic izq. (Orden) | El mismo combo, pero el arco aturde 1 s al PRIMER enemigo que toca. ⚙ El mismo enemigo no se vuelve a aturdir antes de 3 s: sin eso, con ataques cada 0.8 s quedaba aturdido para siempre | `GA_OrderStaffCombo` → `GA_OrderLightArc` (`FirstHitEffects` = `GE_OrderStun`, `FirstHitCooldownPerTarget` 3; 0 = sin límite) |
   | E | Aturdir: marca a un enemigo a 10 m; si le pega a un aliado (o al Clérigo), `GE_Stun` y la marca se consume. ⚙ Marca de 4 s | `GA_CommandHalt` (nuevo, como el Enemigo jurado pero escuchando los golpes que DA) + `GE_CommandHaltMark` |
   | Pasiva extra | Heroísmo: al curar a un aliado, además de la resistencia al daño de la Bendición, +30 % de resistencia al control por 6 s | `GE_Heroism` (copia de `GE_Blessing` + `CCResistance`) en lugar de la Bendición, en `ClericOrderBehaviours` |
   | R | Zona de verdad: 6 m, 5 s, a 10 m. TODOS adentro (los tres equipos, el Clérigo incluido) quedan silenciados y desarmados; los aliados se curan. ⚙ 0.2 × daño mágico por tick de 0.5 s (unos 20) | `GA_ZoneOfTruth` (`GA_ContinuousAoE` con `Targets` All y la lista nueva `AllyEffects`) + `GE_ZoneOfTruth` + `GE_ZoneOfTruthHeal` |

   Estadísticas: iguales a las de la Vida (las tres subclases comparten). Cooldowns de E y
   R en 0: mandan `GE_Cooldown_Extra` y `GE_Cooldown_Ultimate`.

   Piezas nuevas de código: tag **`State_Disarmed`** (al final del enum; bloquea el
   ataque básico de CUALQUIER clase, marcado con `IsBasicAttack` al equipar; lo va a
   reusar el Guerrero), `FirstHitEffects` en `GA_ProjectileShoot`, `AllyEffects` en
   `GA_ContinuousAoE` y `GA_CommandHalt`. **La entrega de la carga pasó de 3 a 6 s**
   (`MercObjective.DeliverSeconds`): más que los 5 s de la Zona de verdad.

   - [x] Correr `Mercenarios ▸ Crear el Dominio del orden y el combo del Clérigo (una sola
         vez)` y borrar `Assets/Scripts/Editor/MercClericOrderSetup.cs`. Cambia el clic
         izquierdo de `Class_Cleric` y `Class_LifeDomainCleric` por el combo, crea todo
         lo del Orden, lo cuelga de `Class_Cleric` y actualiza los registros.
   - [x] El ícono de la Zona de verdad y su `VisualPrefab` (`Art/VFX/Zone of Truth`), y el
         ícono del desarme en `GE_ZoneOfTruth`. Pendiente: el clip del bastonazo es el del
         cono del Paladín; cambiarlo si el artista hace uno.
   - [x] **PROBADO ✅ (5 de octubre).** Probar a fondo en red: el combo pega con el bastón y después lanza el arco; el Orden aturde
         solo al primero que toca el arco; Aturdir aturde al marcado cuando le pega a un
         aliado y no cuando le pega a un enemigo suyo; Heroísmo aparece al curar; en la
         Zona nadie puede atacar (tampoco el Clérigo ni sus aliados) y los aliados se
         curan; la entrega tarda 6 s.

   **Dominio de la luz — HECHO (30 de septiembre, herramienta corrida y funcionando),
   FALTA PROBAR.** Idea de Gustavo: sus hechizos pasan de instantáneos a CON EL TIEMPO. Los
   números que no vinieron los puso Claude (marcados ⚙).

   | Ranura | Habilidad | Pieza |
   |---|---|---|
   | Los GE | Quemadura: 1 × daño mágico cada 1 s por 5 s. Curación: 2 × daño mágico cada 1 s por 5 s. Refresh: reaplicarlos renueva la duración SIN cortar el ritmo de 1 s (Refresh solo toca la duración, el tick sigue su cuenta), así que pegar más seguido no suma ticks | `GE_RadiantBurn`, `GE_RadiantHeal` |
   | Clic izq. | El combo, con un arco que quema a los enemigos y cura con el tiempo a los aliados | `GA_RadiantStaffCombo` → `GA_RadiantLightArc` |
   | Clic der. | Healing Word que cura con el tiempo. Ojo: gastar dos cargas seguidas en el mismo aliado solo renueva la curación, no la suma | `GA_RadiantHealingWord` |
   | Q | Guiding Bolt que quema (conserva su marca Bane) | `GA_RadiantGuidingBolt` |
   | E | Luz del amanecer: zona que cada 1 s quema a los enemigos y cura a los aliados que están adentro. ⚙ 6 m, 4 s, a 15 m | `GA_DawnLight` (`GA_ContinuousAoE`; `AllyEffects` ahora corre aparte de `Targets`) |
   | Pasiva extra | Faro de esperanza, cada 1 s. Aliados (y él): +15 % de curación recibida y 0.5 × daño mágico de curación. Enemigos: revelados aunque estén invisibles. ⚙ 12 m | El aura por anillos del Paladín (`PaladinAuraPassive`) en `ClericLightBehaviours` + `GE_BeaconOfHope` (atributo nuevo `HealingReceived`), `GE_BeaconHeal`, `GE_Revealed` (tag nuevo `State_AlwaysVisible`) |
   | R | Quemadura santa: cono grande que quema a los enemigos y cura a los aliados (los mismos GE), y marca a los enemigos alcanzados: los aliados que los golpean se curan, como con el Enemigo jurado. ⚙ 10 m, 120°, marca de 5 s; ⚙ 0.5 × daño mágico por golpe, como mucho cada 0.2 s por aliado | `GA_HolyFire` (nuevo: un `GA_ConeAttack` con el gancho nuevo `OnEnemyHit`) + `GE_HolyFireHeal` |

   Estadísticas: las mismas de la Vida y el Orden. Cooldowns de E y R en 0: mandan
   `GE_Cooldown_Extra` y `GE_Cooldown_Ultimate`. El Destello, igual.

   Piezas nuevas de código: atributo **`HealingReceived`** y tag **`State_AlwaysVisible`**
   (los dos al FINAL de sus enums); `ASC.IsHiddenFromEnemies` (invisible y sin revelar):
   lo preguntan el modelo, la barra de vida, los números de daño y los NPCs, en vez del
   tag a secas. El revelado NO saca la invisibilidad: sus aliados lo siguen viendo
   fantasma y al salir del aura vuelve a desaparecer.

   - [x] Correr `Mercenarios ▸ Crear el Dominio de la luz del Clérigo (una sola vez)` y
         borrar `Assets/Scripts/Editor/MercClericLightSetup.cs`. Crea los GE, las copias
         del kit, la E, la R, `ClericLightBehaviours` (Bendición + Faro), la subclase, la
         cuelga de `Class_Cleric` y actualiza los registros.
   - [-] Pospuesto (VFX gratis por ahora): el `VisualPrefab` de la Luz del amanecer, el `HitVFX` de la
         Quemadura santa, y el VFX del aura si querés que se vea.
   - [x] **PROBADO ✅ (5 de octubre).** **Probar la subclase: el DAÑO y los ÍCONOS.** Daño: cuánto quema y cuánto cura
         de verdad cada cosa (el arco, Guiding Bolt, la Luz del amanecer, la Quemadura santa,
         la curación del Faro) contra los números de arriba, y si la curación con el tiempo
         rinde de más (con 10 de daño mágico, un Healing Word cura 100 en 5 s contra 20 del
         normal). Íconos: que cada habilidad y cada efecto (quemadura, curación, Faro,
         revelado) muestre el suyo en la barra y en los efectos activos.
   - [x] **PROBADO ✅.** Probar: la quemadura y la curación tickean cada 1 s aunque se peguen más seguido;
         la Luz del amanecer quema y cura en la zona; con el Faro, un Pícaro invisible
         cerca se ve (y al alejarse vuelve a desaparecer) y las curaciones rinden un
         15 % más; la Quemadura santa cura a los aliados que golpean a los quemados.

   **VFX de efectos y de impacto — HECHO, FALTA PROBAR (30 de septiembre, tarde).**
   - **`TargetVFXLoop`** (casilla nueva en cada GE, prendida por defecto): las partículas
     del Target VFX se repiten mientras dure el efecto aunque el prefab sea de un solo
     disparo, y al terminar se desvanecen en vez de cortarse. Ojo: quedó prendida también
     en los GE que ya tenían VFX (auras, `GE_DivineProtection`, `GE_FinalBlowShield`,
     `GE_PreserveLife`, `GE_Heroism`, `GE_Inmortal_Rage`, `GE_HeroicInterventionBuff`,
     `GE_DivineSmiteCharge`, `GE_RadiantHeal`): si alguno era un destello de una sola vez,
     apagársela.
   - **`VFX_EyeReveal`** (`Art/VFX`): el ojo con halo y chispas encima del revelado por el
     Faro. Ya puesto; la herramienta que lo armó se borró.
   - **El VFX del aturdido** (`GE_Stun` y `GE_OrderStun`) sube a y = 1: quedaba muy abajo.
   - **El círculo de la Zona de verdad salía parado**: el prefab acuesta el círculo girando
     su raíz −90° en X, y las zonas lo creaban con rotación cero. `GA_ContinuousAoE` (y
     `GA_InstantAoE`, por las dudas) ahora respetan la rotación del prefab. La Luz del
     amanecer no cambia (su prefab no tiene rotación).
   - **`TargetImpactAbility`** (`Scripts/GAS`): la base común del VFX de impacto sobre un
     personaje (Attach, Offset, Lifetime). La heredan `GA_Target`, `GA_SwornEnemy`,
     `GA_CommandHalt`, `GA_MarkedForDeath`, `GA_Resurrection` y `GA_HeroicInterception`;
     se borró la copia que tenía cada una. Enemigo jurado, Aturdir y la Marca ahora pegan el
     VFX al objetivo (antes quieto 2 s); la Resurrección queda igual (pies, 3 s); la
     Intercepción sigue en el punto de aterrizaje (ahí solo cuenta Lifetime).
   - [x] **PROBADO ✅.** Probar: Healing Word de la Luz (el VFX dura los 5 s y se apaga suave); Enemigo
         jurado o Aturdir sobre alguien que se mueve; Resurrección; el círculo de la Zona
         de verdad acostado en el piso; las estrellas del aturdido a la altura de la cabeza.

   **Limpieza de assets (revisión del 30 de septiembre) — HECHA el 8 de octubre, falta verla:**
   - [x] `GA_DivineSmiteVengeane`: **VFX doble**. Su Visuals Sequence pone el aura `Buff`
         y `GE_DivineSmiteCharge` pone el mismo prefab. Borrar el de la secuencia.
   - [x] `GA_Frenzy`: pasar `RageBuff` (el elemento que termina con `Status_Frenzy`) al
         Target VFX de `GE_Frenzy`. El del escudo (termina al gastarse) se queda.
   - [x] `GA_InvencibleConqueror`: pasar `RageBuff` al Target VFX de
         `GE_InvencibleConquerorTag`.
   - [x] `GA_AvengingAngel`: dos VFX por dos vías (`RageBuff` en la secuencia, las alas en
         `GE_AvengingAngelTag`). Funciona; un GE admite UN Target VFX, así que pasarlo todo
         al GE pide juntarlos en un prefab.
   - [x] `GA_RadiantHealingWord`: su Impact VFX (`Healing buff`) y el de `GE_RadiantHeal`
         (`Healing buff Burn`) salen juntos. Si se ven encimados, sacarle el Impact VFX.
   - [x] 10 GE viven en `GameplayAbilities/` y no en `Effects/` (Venganza: `GE_AvengingAngelTag`,
         `GE_AvenginAngelBuffs` —con error de tipeo—, `GE_SwornEnemy`, `GE_SwornEnemyHeal`;
         Conquista: `GE_InvencibleConquerorTag`, `GE_ShieldOfFaith`; Pirata: `GE_WinGamble`,
         `GE_LoseGamble`, `GE_WinHeal`, `GE_LoseHeal`). Moverlos desde Unity no rompe nada.

   **Cómo quedó (8 de octubre, con Unity cerrado):**
   - El Castigo divino de la Venganza ya lo habías limpiado vos.
   - Frenesí e Invencible conquistador: el `RageBuff` salió de la lista de VFX de la habilidad
     y es el Target VFX de `GE_Frenzy` y `GE_InvencibleConquerorTag`. El escudo del Frenesí
     (termina al gastarse) sigue en la habilidad.
   - Ángel vengador: prefab nuevo **`Art/VFX/VFX_AvengingAngel`** = las alas (con su
     altura 0.5 y escala 0.6 de antes) + el `RageBuff`, como tu `VFX_FrenzyBuff`. Es el
     Target VFX de `GE_AvengingAngelTag`; la habilidad ya no tiene VFX propios. Se pierde
     el retraso de 0.2 s que tenía el aura. Al terminar el efecto, lo que NO es partícula
     (las alas) se apaga en el acto y las partículas se desvanecen (cambio en
     `NetworkASC.FadeOutEffectVfx`, sirve para cualquier VFX combinado).
   - Healing Word radiante: se queda como está. El de la habilidad es el destello de 1 s al
     curar (eso va en la lista de la habilidad) y el del GE dura la curación; no son el mismo.
   - Los GE se movieron con `git mv` (mismo GUID, no se rompe ninguna referencia): a
     `Effects/Buffs` todos menos `GE_SwornEnemy`, que va a `Effects/Debuffs`. También
     `GE_Fly`, que estaba suelto en `GameplayAbilities/`. `GE_AvenginAngelBuffs` se llama
     ahora **`GE_AvengingAngelBuffs`**.
   - [x] **Visto ✅ (8 de octubre).** Ver en juego el Frenesí, el Invencible conquistador y el Ángel vengador (las alas
         a la altura de antes, y que se vayan al terminar).

   **Arreglos del 30 de septiembre, PROBADOS ✅:**
   - **Los lanzamientos no se veían en las otras pantallas**: el hacha del Bárbaro, las
     dagas del Pícaro y el arco y la Guiding Bolt del Clérigo. Cobran al SOLTAR, así que
     al salir de `Activate()` el servidor creía que la habilidad se había plantado y no
     mandaba la animación. Marca nueva `StartedThisActivation` en `GameplayAbility` (la
     pone `GA_ProjectileShoot`).

   **VFX de la curación del aura del Paladín — HECHO Y PROBADO ✅ (30 de
   septiembre).** Cada vez que el aura cura a un aliado (golpeando o bloqueando), le
   sale un destello encima, en todas las pantallas. Solo si de verdad le subió la vida.
   - [x] Poner el VFX en `PaladinAuraPassive ▸ Heal VFX` de los CUATRO prefabs:
         `PaladinBehaviours`, `OathOfDevotionBehaviours`, `OathOfVengeanceBehaviours` y
         `OathOfConquestBehaviours`. Ajustes al lado: `Heal VFX Offset` (0, 1, 0 = pecho),
         `Attach Heal VFX`, `Heal VFX Lifetime`.
   - [x] Probar en red: el otro jugador ve el destello sobre los curados.
   **Guerrero (Fighter) — KIT BASE HECHO Y PROBADO ✅ (1 de octubre).** Los assets los
   creó una herramienta corrida en batch con Unity cerrado (ya borrada). Números de
   Gustavo; los que no vinieron, marcados ⚙.

   | Ranura | Qué hace | Pieza |
   |---|---|---|
   | Stats | 100 vida, 8 ataque, 6 armadura, 1 s entre ataques, velocidad 6 (la del Bárbaro), 100 de energía | `ASDef_Fighter` (copia del Paladín, sin daño mágico) |
   | Clic izq. | Combo ALTERNADO de dos conos de 180° y 2 m, daño de clase y "Stones hit": derecha→izquierda (`Attack1H01_R`) e izquierda→derecha (`Attack1H02_R`) | `GA_FighterPrimaryCombo` → `GA_FighterSlashRight` / `GA_FighterSlashLeft` |
   | Clic der. | El escudo del Paladín + **parry**: ⚙ los primeros 0.3 s al levantarlo, un golpe que la barrera frena se frena ENTERO y no gasta energía; si el atacante está a ⚙ 3.5 m o menos, queda aturdido (`GE_ParryStun`, 1 s) | `GA_FighterShieldBlock` + `Entity_ShieldBarrier` (`ParryWindow`, `ParryMeleeRange`, `ParryStunEffect`) |
   | Q | Postura: en defensiva pasa a ofensiva y viceversa, 1 s de reutilización. Ofensiva +2 ataque; defensiva +2 armadura. Empieza en defensiva. El botón muestra la postura ACTUAL | `GA_FighterStance` (`GA_TagSwitch`) + `GE_StanceDefensive` / `GE_StanceOffensive` |
   | Shift | Carga según la postura. Ofensiva: atraviesa y daña a todos. Defensiva: frena frente al primero, le pega y lo aturde (`GE_Stun`). ⚙ 5 m a 35 de velocidad (los del Ángel vengador); su cooldown es el de `GE_Cooldown_MoveAbility` | `GA_FighterCharge` (`GA_TagSwitch`) → `GA_FighterChargeOffensive` / `GA_FighterChargeDefensive` |
   | Pasiva | Segundo aliento. Fuera de combate (3 s sin que le baje la vida): cada 1 s, 5 % de la vida que le FALTA (como Sett del LoL). Bajo el 30 %: el 80 % de su vida máxima de ese momento, en 5 curaciones durante 5 s (la primera en el acto); ⚙ puede volver a saltar a los 60 s | `OutOfCombatRegen` + `EmergencyHeal` en `FighterBehaviours`. Modo de la regeneración: % de lo que falta, % de la máxima o fijo |

   Piezas nuevas de código:
   - **`ReplacesGroup`** en los GE: el último de un `EffectGroup` reemplaza a los otros sin
     importar Priority. Las posturas comparten grupo (`Status_Stance`), así que entrar a una
     saca la otra en el acto: nunca están las dos. Por eso NO hace falta que duren 0.5 s:
     duran "para siempre" (99999 s, como `GE_EnergyRegen`) y son Hidden, para que la muerte
     no las borre.
   - Tags nuevos (al final del enum): `Stance_Defensive`, `Stance_Offensive`, `Status_Stance`.
   - **`CurrentIcon`**: el HUD revisa el ícono cada frame. `GA_TagSwitch` con
     `ShowVariantIcon` muestra el de la variante activa (`Icon` por variante y
     `DefaultIcon` para cambiarlos). El Castigo divino del Paladín no cambia (apagado).
   - **`GA_Dash`**: `StopAtFirstEnemy` (se acorta para frenar frente al primero),
     `FirstHitEffects` y `StopShortDistance`.
   - **La barrera sigue la mira** (`FollowAimPitch`, `PitchPivotHeight`, `PitchFactor`):
     sube y baja con el mismo ángulo que el torso (`UpperBodyAim.CurrentPitch`), así se ve
     igual en todas las pantallas y el servidor bloquea con esa geometría. Vale también
     para el Paladín.

   - [x] **Probar el Guerrero** (PROBADO ✅; Gustavo ajustó valores e íconos): el combo alterna los dos cortes; el escudo apunta arriba y
         abajo con la mira (también el del Paladín); parry contra un melee (aturde, no gasta
         energía) y contra un proyectil (frena, no gasta); Q cambia la postura, el ícono y
         la armadura/ataque (mirar los stats); el Shift según la postura; la regeneración
         fuera de combate y la de emergencia bajo el 30 %; que al morir conserve la postura.
   - [x] **Carga defensiva con el escudo arriba** (1 de octubre, PROBADO ✅):
         casilla nueva `Usable While Holding` en las habilidades (MARCARLA en
         `GA_FighterChargeDefensive`). El `GA_TagSwitch` la resuelve por variante: la ofensiva
         sigue bloqueada. El escudo sigue arriba durante y después de la carga (se baja
         soltando el botón) y la carga no se anima mientras se sostiene. Probar: cargar con
         el escudo, soltar el clic derecho después de la carga (tiene que bajar), soltarlo
         EN MEDIO de la carga, y en red (que no vuelva el escudo eterno).
   - [-] Pospuesto (modelos y VFX del juego en camino): el ajuste de la espada en la mano (`MainHandRotationOffset` en
         `Class_Fighter`: se copió en cero del martillo del Paladín) y los VFX.

   **Subclases del Guerrero — lo decidido (1 de octubre):**
   - **Comandante**: código hecho, ver su sección abajo.
   - **Guardián**: hecho y probado, ver su sección abajo (después del Comandante).
   - Pendiente aparte: migrar el Golpe final del Inmortal a `GA_ChargedAttack` (apuntar
     los lanzamientos ya se resolvió con `AimBeforeThrow`).

   **Maestro de batalla — PROBADO ✅ (1 de octubre).** El mandoble siempre equipado. En
   postura OFENSIVA: a dos manos, escudo a la espalda, un tajo de mandoble y ataque cargado.
   En DEFENSIVA: escudo en la mano y el arma a una mano (el kit base). Números de Claude
   (⚙), Gustavo los ajusta.

   | Ranura | Qué hace | Pieza |
   |---|---|---|
   | Clic izq. | Defensiva: el combo del Guerrero. Ofensiva: UN tajo de mandoble (`Attack2H01`), ⚙ 1.3 × ataque, 2.5 m, 180° | `GA_BattleMasterPrimary` (`GA_TagSwitch`) → `GA_GreatswordSlash` |
   | Clic der. | Defensiva: el escudo con parry. Ofensiva: **ataque cargado** como la Q de Sion, con la pose del Golpe final del Inmortal: tocar = golpe rápido; ⚙ a los 0.6 s la segunda etapa y a los 1.2 s la tercera (aturde 1 s); se suelta solo a los 1.5 s. ⚙ 1.2 / 2 / 3 × ataque, 2.5 m, 120°; ralentiza un 40 % mientras carga; un aturdido corta la carga sin golpe; 3 s de cooldown, que el botón muestra solo en ofensiva | `GA_BattleMasterGuard` (`GA_HoldTagSwitch`) → `GA_GreatswordChargedStrike` (`GA_ChargedAttack`) + `GA_ChargedStrikeStage1/2/3` |
   | E | Desarme: el enemigo de enfrente (⚙ 3 m) no puede usar sus acciones de ARMA ⚙ 2 s (la resistencia al control lo acorta); 25 s de cooldown | `GA_Disarm` (`GA_Target`) + `GE_Disarmed` (`State_Disarmed`) |
   | Pasiva extra | Punto débil: al dañar a un enemigo lo marca ⚙ 3 s, ⚙ +10 % de daño recibido (+20 % con Romper límites). Las dos marcas no se acumulan | `WeakPointPassive` en `BattleMasterBehaviours` + `GE_WeakPoint` / `GE_WeakPointEmpowered` |
   | R | Romper límites: ⚙ 8 s Imparable (inmune al control, limpia los debuffs) y ⚙ +30 % de ataque; Punto débil potenciado | `GA_LimitBreak` + `GE_LimitBreak` (`Status_Unstoppable` + `Status_BreakLimits`) |
   | Stats | ⚙ 200 vida, 10 ataque, el resto como el Guerrero, nivel 3. Rol: Daño | `ASDef_BattleMasterFighter` |

   Piezas nuevas de código (detalle en la guía y la arquitectura):
   - **`GA_ChargedAttack`** (genérico): mantener carga, soltar dispara la etapa alcanzada;
     cada etapa es OTRA habilidad (cono, línea, proyectil).
   - **`GA_HoldTagSwitch`**: un `GA_TagSwitch` entre habilidades de MANTENER (escudo /
     carga). Le pasa a la variante el soltar, los clips y el cooldown que muestra el HUD
     (`CooldownEffectForDisplay`). Cada variante paga su propio cooldown.
   - **`ReportEndAs`**: una variante de un switch avisa su fin con el nombre del switch.
     Sin esto, un mantenido dentro de un switch que el servidor corta solo (sin energía, la
     carga al tope) dejaba trabado al dueño.
   - **La postura a la vista**, todo según un tag y sin RPC: `StowOffHandTag` cuelga el
     escudo del hueso del pecho; `StanceAnimatorOverride` + `StanceAnimatorTag` cambian los
     clips de la clase por los de otro AOC (el Maestro en ofensiva usa
     `AOC_Paladin_2Handed`). La pose del brazo izquierdo (`OffHandPose`) ya no la usa; su
     peso ahora se repone cada frame (cada cambio de clip del Animator lo reiniciaba).
   - **Bloqueos por tipo de acción**: cada habilidad dice qué la bloquea en
     `ActivationBlockedTags`. Desarmado = las de ARMA (golpear o lanzar, no el escudo),
     Silencio = las de MAGIA o fantasía, Enraizado = las de MOVIMIENTO, Aturdido = todas;
     las que mezclan dos tipos llevan los dos. Las de salir de apuros (Comida de emergencia,
     Protección divina) solo el aturdido. El desarme ya no bloquea "el básico" por código.
   - Tags nuevos (al final): `Status_BreakLimits`, `Status_WeakPoint`.

   - [x] **Volver a correr `Mercenarios ▸ Aplicar los bloqueos por tipo de acción (una
         sola vez)` y BORRAR `Assets/Scripts/Editor/MercActionTagsSetup.cs`.** Se corrió
         antes de dos ajustes de Gustavo y en los assets todavía quedaron así: el Blink con
         enraizado + silencio (tiene que ser solo enraizado) y la Protección divina con
         silencio (tiene que ser solo el aturdido). Correrla de nuevo no rompe nada.
   - [-] Pospuesto (modelos y VFX del juego en camino): el modelo del mandoble (no hay uno: queda la espada); los VFX de las
         etapas de carga; si al caminar en ofensiva se ve a una mano, agregarle a
         `AOC_Paladin_2Handed` los clips 2H de caminar y correr (hoy solo cambia el de quieto).

   **Comandante — HECHO Y PROBADO ✅ (5 de octubre), salvo el aturdido al apuntar.** La lanza (el bastón del Clérigo)
   siempre equipada. En OFENSIVA: a dos manos (pose de lanza de Kevin), escudo a la
   espalda, estocada y lanzamiento. En DEFENSIVA: el kit base. Números de Claude (⚙),
   Gustavo los ajusta.

   | Ranura | Qué hace | Pieza |
   |---|---|---|
   | Clic izq. | Defensiva: el combo del Guerrero. Ofensiva: estocada de lanza (`AttackPolearm01`), ⚙ 1.2 × ataque, 3.5 m de largo | `GA_CommanderPrimary` (`GA_TagSwitch`) → `GA_SpearThrust` (`GA_LineAttack`) |
   | Clic der. | Defensiva: el escudo. Ofensiva: lanzamiento APUNTADO (ver abajo): atraviesa y aturde 1 s a cada enemigo que toca, ⚙ 1.5 × ataque; ⚙ 8 s de cooldown | `GA_CommanderGuard` (`GA_HoldTagSwitch`) → `GA_SpearThrow` (`GA_ProjectileShoot` con `AimBeforeThrow`) |
   | Q | La postura, pero SOLO da el tag: los +2 los da el aura | `GA_CommanderStance` → `GE_CommanderStanceDefensive/Offensive` (ocultos) |
   | Pasiva extra | Inspiración de batalla: el aura del Paladín con un anillo por postura (⚙ 8 m, a él y a los aliados): +2 de armadura en defensiva, +2 de ataque en ofensiva | `PaladinAuraPassive` en `CommanderBehaviours` + `GE_CommanderAuraDefensive/Offensive` |
   | E | Voz de mando: ⚙ 6 s con `Status_CommandingVoice`, que prende otro anillo del aura: ⚙ +20 % de velocidad de movimiento y de ataque. ⚙ 20 s de cooldown. Animación: el grito del Bárbaro | `GA_CommandingVoice` (`GA_SelfBuff`) + `GE_CommandingVoice` / `GE_CommandingVoiceBuff` |
   | R | Salto heroico: se apunta con el marcador (⚙ 15 m), despega, aparece en el destino a los ⚙ 0.35 s, golpea ⚙ 2 × ataque en ⚙ 3 m y clava la bandera: ⚙ 8 s, ⚙ 6 m, daña ⚙ 0.3 × ataque por segundo a los enemigos y da ⚙ +20 % de daño y +3 de armadura a los aliados | `GA_HeroicLeap` (nuevo, hereda de `GA_Teleport`) + `GA_CommanderBanner` (`GA_ContinuousAoE`) + `PF_CommanderBanner` |
   | Stats | ⚙ como el Maestro de batalla con 8 de ataque. Rol: Tanque (la definitiva se carga con el daño recibido) | `ASDef_CommanderFighter` |

   Código nuevo: `GA_HeroicLeap`; `GA_Teleport.ExecuteTeleport` (el viaje, aparte de
   buscar el destino seguro); `GA_ContinuousAoE.ActivateAt(punto)` (una zona que nace
   donde otra habilidad diga, sin animación); tag `Status_CommandingVoice` (al final).

   **Lanzamientos apuntados (1 de octubre, pedido de Gustavo tras probar la lanza).**
   Casilla nueva `Aim Before Throw` en `GA_ProjectileShoot`, prendida en el hacha del
   Bárbaro (y sus 3 subclases), las dagas del Pícaro, del Asesino y del Ilusionista, y la
   lanza del Comandante. Los castigos, el Guiding Bolt y el arco del combo del Clérigo NO
   la tienen: siguen saliendo al apretar.
   - Mantener: el arma atrás (`Aim Hold Clip`: `ThrowWeapon01_R - Hold` /
     `ThrowSpear01_R - Hold`), la cámara se acerca a la mira (`Aim Cam Offset` en la
     cámara, sensibilidad ×0.75) y el modelo del jugador se le esconde (sigue la sombra).
   - Soltar: sale YA. Se reproduce el lanzamiento desde la pose (`Aim Release Clip`: tomas
     nuevas `HumanM@ThrowWeapon01_R - Release` y `HumanM@ThrowSpear01_R - Release`, que la
     herramienta armó buscando en el clip entero el cuadro más parecido a la pose de
     apuntar) y el proyectil sale cuando el clip suelta: el hacha y las dagas a los 0.25 s
     (el evento del clip original, corrido); la lanza a los ⚙ 0.25 s (`Aim Release
     Delay`, su clip no trae evento). El dueño lo ve en el acto (predicción) y la cámara
     y el modelo vuelven.
   - Un aturdido o la muerte mientras apunta lo cortan sin lanzar y sin cobrar. No se
     suelta solo (corte de seguridad a los 30 s).
   - Se reemplazó la versión anterior (envoltorios `GA_ChargedAttack` "...Aim", que se
     borraron) y `GA_ChargedAttack` volvió a como estaba.
   - Por dentro: `IHoldAbility.UsesHoldInput` + `HoldInput.IsHold` (un
     `GA_ProjectileShoot` es mantenido solo con la casilla);
     `GameplayAbility.AimsCameraWhileHeld` y `PredictOwnerReleaseVisuals`;
     `NetworkASC.ServerBroadcastStepAnimationToOthers` (el clip de soltar a los demás, sin
     repetírselo al dueño).

   - [x] Correr `Mercenarios ▸ Crear el Comandante` (corrida y borrada) y la herramienta
         de los lanzamientos apuntados (corrida en batch y borrada).
   - [x] Íconos del Comandante y del Maestro de batalla (Gustavo, 5 de octubre).
   - [x] **Probado (5 de octubre):** el lanzamiento apuntado ("me gustó como está"), las
         auras cambian con la postura, la Voz de mando. La lanza en la mano volvió a
         (0, 0, 0): el agarre no era el problema.
   - [x] **La cámara se acerca recién al mantener 1 s** (5 de octubre, pedido de Gustavo):
         un lanzamiento rápido no mueve la cámara ni esconde el modelo; mantener = apuntar.
         `Aim Delay` en la cámara (junto a `Aim Cam Offset`, que sirve para ajustar cuánto
         se acerca). **Compila; falta probarlo.**
   - [x] El Guiding Bolt tiene `Aim Before Throw` (lo prendió Gustavo) sin pose de
         apuntar: sin `Aim Hold Clip` el personaje sigue con su animación normal (antes
         habría entrado al bucle de mantener con el clip de otra habilidad). Solo apunta la
         cámara.
   - [ ] **Jueves 9:** probar el aturdido mientras se apunta (no lanza ni cobra), con
         otro jugador o el control; y los bots con los lanzamientos.
   - [-] Pospuesto: el evento de impacto de la estocada (al 40 % a ojo), el momento en que
         suelta la lanza, la bandera. Se usa lo que hay hasta que lleguen los modelos y
         VFX del juego.

   **Guardián — HECHO Y PROBADO ✅ (5 de octubre).**
   Tanque. En ofensiva, el kit del Guerrero sin cambios; lo suyo está en la defensiva y en
   sus tres habilidades. Números de Claude (⚙), Gustavo los ajusta.

   | Pieza | Qué hace | Cómo |
   |---|---|---|
   | Postura | Defensiva: +3 de armadura (no +2). Ofensiva: la del Guerrero (+2 de ataque). Cambiar de postura NO toca la energía ni el escudo: solo el tag y el stat (Gustavo) | `GA_GuardianStance` → `GA_GuardianEnterDefensiveStance` + `GE_GuardianStanceDefensive` |
   | Escudo grande | El escudo de siempre a ⚙ ×1.3 (`Off Hand Scale` en la clase, sin modelo nuevo) y la BARRERA (lo que de verdad frena) ⚙ 1.5 veces más ancha y 1.4 más alta, para cubrirlo a él y a los aliados detrás. 400 de energía base (Gustavo) | `GuardianBehaviours` (copia de `FighterBehaviours`) con la barrera escalada; `ASDef_GuardianFighter` |
   | Devolver daño | Al frenar un golpe CUERPO A CUERPO (a 3.5 m o menos), el atacante recibe TODO lo frenado como daño físico normal (pasa por su armadura). Lo devuelto no se vuelve a devolver | `Entity_ShieldBarrier.ReflectMeleeFraction` = 1 (los demás escudos en 0) |
   | Parry devuelve proyectiles | En la ventana del parry, el proyectil cambia de dueño y sale hacia donde apunta el Guardián, con el daño que traía. Una vez por proyectil | `Entity_ShieldBarrier.ReflectProjectilesOnParry` + `GC_Projectile.Reflect` |
   | E — Ejecución | Golpe hacia adelante (cono de 90° (Gustavo), 2.5 m): 1 × ataque + 15 % de la vida que le falta al enemigo, sin tope. Si lo mata: se reinicia el cooldown y se cura ⚙ 20 % de la vida máxima del muerto. ⚙ 12 s | `GA_Execute` (hereda de `GA_ConeAttack`) + `GE_ExecuteDamage` |
   | Pasiva — Venganza | Resistencia al daño y al control: 3 % por cada 5 % de vida que falta, 30 % a la mitad de la vida. Se ve en la barra como un buff con su número | `VengeancePassive` + `GE_Vengeance` (10 acumulaciones de 3 %) |
   | R — Avatar | ⚙ 8 s: Imparable, un escudo igual a su vida máxima (lo que sobre se va al terminar), el modelo crece ×1.5 en todas las pantallas y sus golpes cuerpo a cuerpo llegan ×1.5 más lejos | `GA_Avatar` (`GA_SelfBuff`) + `GE_Avatar`; `GrowTag`/`GrowScale` en la clase; atributo nuevo `MeleeRangeBonus` (conos y líneas) |
   | Stats | ⚙ 220 de vida, 8 de ataque, 6 de armadura (9 en defensiva), 400 de energía. Rol: Tanque | `ASDef_GuardianFighter` |

   Código nuevo: `GA_Execute`, `VengeancePassive`; en `Entity_ShieldBarrier` devolver daño
   y proyectiles; `GC_Projectile.Reflect`; `CharacterClassDefinition.OffHandScale`,
   `GrowTag`, `GrowScale`; `EAttributeType.MeleeRangeBonus` y `Status_Avatar` (al final).

   - [x] Correr `Mercenarios ▸ Crear el Guardián (una sola vez)` (corrida y borrada:
         `Assets/Scripts/Editor/MercGuardianSetup.cs`).
   - [x] **La cámara acompaña al Avatar** (5 de octubre, pedido de Gustavo): mientras el
         modelo crece, el punto que mira sube con él y la cámara se aleja un poco
         (`Grow Distance Factor` en la cámara, ⚙ 0.6: creciendo ×1.5 se aleja ×1.3).
         **PROBADO ✅.**
   - [x] **Los escudos cargan la definitiva por golpe bloqueado** (5 de octubre, pedido de
         Gustavo): 1 s menos por cada GOLPE que frena la barrera (la cantidad, no el daño),
         también lo que frena por un aliado, los proyectiles y el parry. Un mismo golpe
         frenado para varios a la vez cuenta una vez. Vale para todos los escudos (Paladín,
         Guerrero y sus subclases; el Monje cuando llegue), sin importar el rol:
         `Ultimate Seconds Per Block` en `Entity_ShieldBarrier`. Ojo: cada tick de un área
         (el molinete) es un golpe. **PROBADO ✅.**
   - [x] **Revisado por Gustavo (5 de octubre).** En el editor: los íconos (habilidades, clase, Venganza, Avatar); mirar la barrera
         grande en `GuardianBehaviours` (si tapa demasiado o queda corta, su escala) y el
         escudo ×1.3 en la mano.
   - [x] **PROBADO ✅ (5 de octubre).** Probar: el escudo más grande cubre a un aliado detrás; devolver daño a un melee (y
         que no rebote entre dos Guardianes); el parry con un hacha o una daga (que salga
         hacia la mira y dañe al equipo contrario); la Ejecución al matar (cooldown y
         curación); la Venganza bajando de vida; el Avatar (tamaño en la otra pantalla, el
         escudo, el alcance). La energía tarda más en llenarse (la regeneración es la misma
         para 400).

   **Monje — kit base: HECHO Y PROBADO ✅ (8 de octubre).** Los clips de los puños ya
   tienen su `AnimationEvent_HitFrame`.
   Daño, a puño limpio. Decisiones de Gustavo (6 de octubre); los números que no vinieron
   los puso Claude (⚙).

   | Ranura | Habilidad | Pieza |
   |---|---|---|
   | Pasiva | Artes marciales: cada ataque que pega (uno por ataque, no por enemigo) suma 5 % de velocidad de ataque, hasta 4 acumulaciones, 8 s. ⚙ También 5 % de movimiento (lo decía el documento) | `MartialArtsPassive` (nueva) + `GE_MartialArts` (Stack 4, AtkSpeed × 0.95, MovSpeed × 1.05) |
   | Q | Ki: 2 cargas (8 de octubre; antes 3) (⚙ 8 s cada una). Deja el Ki preparado (⚙ 10 s): la PRÓXIMA acción sale con Ki y lo gasta. Con Ki ya preparado no se gasta otra carga | `GA_Ki` (`GA_SelfBuff`, MaxCharges 2) + `GE_Ki` (tag nuevo `Status_Ki`) |
   | Clic izq. | Dos puñetazos (`AttackPunch01_R` y `_L`, cono de 2 m y ⚙ 90°), 1 s entre ataques. Con Ki, **Ráfaga de golpes**: primero se pone las 4 acumulaciones de una y después pega | `GA_MonkAttack` (`GA_TagSwitch`, ConsumeTag) → `GA_MonkStrikes` / `GA_FlurryOfBlows` (combo: `GA_FlurryStacks` + los dos puños) |
   | Clic der. | Bloqueo frontal, más chico que los escudos: ⚙ frena el 40 % por energía según el daño frenado. Con Ki, **Defensa paciente**: 6 s de una cápsula alrededor que frena el 40 % desde cualquier lado con la misma energía, SIN mantener el botón (puede atacar a la vez) | `GA_MonkGuard` (`GA_HoldTagSwitch`: ahora acepta una variante que no es de mantener y consume el tag) → `GA_MonkBlock` / `GA_PatientDefense` + `GE_PatientDefense` (tag nuevo `Status_PatientDefense`). La cápsula es una segunda barrera en `MonkBehaviours` con la casilla nueva `Omnidirectional` y una esfera con el material del escudo |
   | Shift | **Patada voladora**: embestida larga (20 m a 20 m/s, valores de Gustavo); izquierda/derecha la **corren de costado sin girarla** (`StrafeSpeed` ⚙ 5 m/s), sale con el **ángulo vertical de la mira** como el dash (`AimVertical`, tope `MaxPitch` ⚙ 45°: mirando arriba alcanza al que está en el aire), **volver a apretar Shift la corta** (por el precipicio), choca con paredes y frena en el primer enemigo. Con Ki, **Patada del dragón**: 20 m a 20 m/s y aturde al primero | `GA_MonkKick` (`GA_TagSwitch`) → `GA_FlyingKick` / `GA_DragonKick`: genérico nuevo **`GA_RushAttack`** (Create ▸ GAS ▸ Generics ▸ Rush Attack), como la carga de Reinhardt |

   Estadísticas (`ASDef_Monk`): 80 de vida (+50 por nivel), 4 de ataque (+1 por nivel), 4 de
   armadura, 50 de energía, 1 s entre ataques, la velocidad del Pícaro (6.5). Sin arma. Por
   ahora usa el AOC del Pícaro.

   - [x] (Hecho el 7 de octubre: assets creados y herramienta borrada.) **Correr `Mercenarios ▸ Crear el Monje (kit base, una sola vez)`** (con Unity
         abierto, o en batch con Unity cerrado: `-executeMethod MercMonkSetup.Create`) y
         borrar `Assets/Scripts/Editor/MercMonkSetup.cs`. Crea todo en
         `GameplayAbilities/Monk` y `Attributes/Monk`, lo agrega al jugador y a la sala, y
         actualiza los registros. Busca íconos por nombre (`Class_Monk_Icon`, `Ki_Icon`,
         `Martial_Arts_Icon`, `Patient_Defense_Icon`, `Strikes_Icon`, `Flurry_Of_Blows_Icon`,
         `Monk_Block_Icon`, `Flying_Kick_Icon`, `Dragon_Kick_Icon`); los que no estén, a mano.
   - [x] **PROBADO ✅ (8 de octubre).** Probar: los puños y las acumulaciones (íconos con su número); Ki → clic izq. sale
         con las 4 de una; Ki → clic der. prende la cápsula 6 s y se puede atacar dentro;
         la cápsula frena desde la espalda y gasta energía; Ki → Shift aturde; la Patada
         voladora se tuerce con A/D y se corta con Shift; choca con una pared y termina.
   - [ ] Los clips: la pose del bloqueo es la del escudo del Guerrero (no hay una de guardia
         a puño limpio); la patada es `AttackKick01_R` suelta (si se quiere una pose
         sostenida mientras vuela, va en `Rush Loop Clip` de la patada).
   - [x] **PROBADO ✅ (8 de octubre).** Revisar con dos jugadores: que los demás vean los puñetazos de la Ráfaga (un combo
         dentro de un `GA_TagSwitch` es nuevo: el switch ahora deja que el combo mande sus
         animaciones).
   - Lo que cambió del documento: la Ráfaga ya no son "2 golpes extra por ataque" sino las 4
     acumulaciones de una; la Defensa paciente no es "bloquear todo" sino la cápsula al 40 %.
   - Subclases: lo decidido el 8 de octubre está abajo ("Subclases del Monje").

   **7 de octubre, después de probar el Monje:**
   - Gustavo subió la velocidad de ataque y dejó las dos patadas en 20 m a 20 m/s.
   - `GA_RushAttack`: el giro (`TurnRate`) se reemplazó por **`StrafeSpeed`** (izquierda/derecha
     corren de costado, la patada sigue mirando y avanzando al mismo lado) y la casilla
     **`AimVertical`** (+ `MaxPitch`): sale con el ángulo de la mira. Prendida en las dos patadas.
   - [x] **PROBADO ✅ (8 de octubre).** Probar: patada mirando a alguien en el aire (sube y le pega); A/D durante la patada
         (se corre sin girar); mirando al piso (que no se trabe); cómo cae al terminar arriba.
   - **Maestro de batalla:** el clic izquierdo hacía siempre el tajo a dos manos. En el commit
     del Comandante (`f1aa49d`, 1 de octubre) la ranura de `Class_BattleMasterFighter` había
     quedado en `GA_GreatswordSlash` en vez de la elección por postura (`GA_BattleMasterPrimary`).
     Arreglado por Gustavo; revisadas las demás clases y subclases: ninguna más tenía eso.

   **Subclases del Monje — LO DECIDIDO (8 de octubre). Las tres hechas: Samurái probado, Maestro
   elemental y Shinobi falta probar (ver arriba, "★ Samurái" y "★ Maestro elemental y Shinobi").**
   Diseño de Gustavo; los
   números que no vinieron los pone Claude (⚙). Orden: **Samurái → Maestro elemental →
   Shinobi** (de la que más reusa a la más arriesgada), después de las paredes. Estimación:
   ~4 semanas las tres.

   **Primero, una pieza que usan las tres (1–2 días):** gastar las acumulaciones de Artes
   marciales desde la lista de efectos (`AbilityEffect`). Cada entrada podrá escalar con las
   acumulaciones de un GE —la duración, la cantidad o las repeticiones— y gastarlas (una vez
   por golpe).

   **La regla (Gustavo, 8 de octubre): lo que gasta acumulaciones, SIN ellas no hace nada
   extra; cada acumulación suma.** Premia al que las junta. Con 0 acumulaciones el efecto
   que escala no se aplica (0 s de aturdido, 0 % de daño extra, ningún corte extra).

   *Samurái:*

   | Pieza | Qué hace | Cómo |
   |---|---|---|
   | Arma | Katana: más alcance, ataques en arco | La espada del Guerrero (sin modelos nuevos) |
   | Clic izq. | Los 2 golpes (4 con la Ráfaga), en arco; cada golpe pone una herida | Conos + `GE_Wounds` del Pícaro (DoT, ya tiene tope de 10) en sus Effects |
   | Clic der. | Bloqueo: por cada ataque que frena, una herida al ATACANTE (hasta 10). Reemplaza al "devuelve un golpe" del documento | La barrera aplica un GE al atacante por golpe frenado (nuevo, chico) |
   | Pasiva extra — Afilar | Las acumulaciones también suben el ataque: +1 cada una (6 + 1 por acumulación) | Otro GE de acumulaciones con +1 de ataque; `MartialArtsPassive` ya acepta el GE. Sin código |
   | E — Corte giratorio | Área alrededor que pone heridas. Con Ki (Cortes devastadores): 1 corte + 1 por acumulación (0 → 1 corte y no se cura; 4 → 5 cortes). Con acumulaciones, CADA corte lo cura igual a su ataque de antes de gastarlas (6 + 1 por acumulación: con 4, 5 curaciones de 10). Las gasta todas | `GA_InstantAoE` + la pieza compartida (repetir por acumulación y gastar) |
   | R — Corte final | Se apunta un rectángulo (un ataque en línea con desplazamiento); **mientras apunta, se marca a quién va a alcanzar**. Viaja hasta el final golpeando a todos y les pone un GE (⚙ 4 s): si su vida baja al 5 %, muere. Heridas según las acumulaciones que tenga | `GA_RushAttack` sin frenar en el primero + marcador rectangular y resaltado de objetivos (nuevo) + "ejecutar al 5 %" en el pipeline de daño (nuevo) |

   *Maestro elemental:*

   | Pieza | Qué hace | Cómo |
   |---|---|---|
   | Clic izq. | Cada puñetazo es un combo: el golpe + un ORBE mágico (un lanzamiento sin caída, con más alcance que el golpe). Un combo de combos. Reemplaza a los 4 orbes que giraban (como arma se pegaban a las manos) | `GA_ComboSequence` cuyos pasos son `GA_ComboSequence` (golpe + `GA_ProjectileShoot` mágico, con un VFX que ya exista). Verificar que anide bien en red |
   | Ráfaga aturdidora (Sobrecarga) | Con Ki, la Ráfaga gasta las acumulaciones que tiene y aturde 0.5 s por cada una: 0 → no aturde, 1 → 0.5 s, 2 → 1 s, 3 → 1.5 s, 4 → 2 s. Después le vuelven las 4 de la Ráfaga | Pieza compartida (duración por acumulación + gastar) ANTES de `GA_FlurryStacks` |
   | Defensa de agua (Sobrecarga) | Los primeros segundos de la Defensa paciente convierten el daño en curación | Nuevo |
   | Patada del viento (Sobrecarga) | La Patada del dragón atraviesa, repele y aturde | `GA_RushAttack` sin `StopAtFirstEnemy` + GE con desplazamiento + aturdido: ya existe todo |
   | E — Aliento de fuego | Cono amplio que quema. Con Ki (Aliento de dragón rojo): se mantiene y quema el piso donde apunta; quien lo pisa se prende | Cono + quemadura; mantenido como el molinete; la zona con `GA_ContinuousAoE.ActivateAt`. **El VFX de la zona lo arma Claude** (pedido de Gustavo): un prefab nuevo con el fuego de Hovl (`Procedural fire`), que se ajusta al tamaño del área |
   | R — Paz mental | Resistente al daño y todo sale con Ki sin gastarlo | GE de resistencia + un tag que hace salir las variantes con Ki sin gastar el Ki (nuevo, chico) |

   *Shinobi:*

   | Pieza | Qué hace | Cómo |
   |---|---|---|
   | Clic izq. | Kunais: proyectiles, la misma secuencia de 2 (4 con la Ráfaga); crítico por la espalda | `GA_ProjectileShoot` en el combo; el backstab del Pícaro (`BackstabDamageModifier`) en su prefab de pasivas |
   | Clic der. | El bloqueo y la Defensa paciente esquivan CUALQUIER proyectil: no le hacen daño | Nuevo: los proyectiles lo atraviesan mientras tiene el tag |
   | Pasiva extra — Artes oscuras | Las habilidades con Ki gastan las acumulaciones: 5 % de la vida faltante del objetivo por acumulación (0 → nada, 4 → 20 %) | Pieza compartida + la escala con vida faltante que ya tiene el `Modifier` |
   | E — Manto de oscuridad | Invisible y mucho más rápido; el próximo golpe es crítico; 2 cargas. Con Ki (Paso de las sombras): además se teletransporta | Invisibilidad del Asesino, `Status_GuaranteedCrit`, cargas. El teletransporte es el Destello del Clérigo con el DOBLE de alcance, que también puede apuntar a una PARED (queda enganchado, por las paredes), revisando primero que la cápsula quepa |
   | R — Muerte silenciosa | Elige un enemigo: le hace 30 % de su vida faltante + su daño normal y queda intocable encima de él 0.5 s. Busca otro enemigo cerca (primero JUGADORES, el que más vida le falta), se teletransporta a él y repite, hasta que no quede ninguno sin golpear (a cada uno, una vez) | Nuevo: el servidor elige el siguiente y le manda el teletransporte al dueño; un tag de intocable |

   Falta definir: a qué distancia busca al siguiente la Muerte silenciosa (⚙ 10 m), y los
   números de daño de cada cosa (Claude los pone ⚙ y Gustavo los ajusta).

   **Barra de canalizar (6 de octubre), CÓDIGO HECHO, falta probar.** `UI_CastBar`, bajo la
   barra de carga, solo para el que la usa, como la del WoW: amarilla y se LLENA en una
   carga (el **Golpe final** del Inmortal), verde y se VACÍA en un canalizado (el
   **molinete** del Berserker), roja con "Interrupted" si la cortan. Una habilidad la pide
   con `ShowCastBar(duración, channel)` / `HideCastBar(interrumpida)`.
   - [ ] Probar con el Golpe final (y cortarlo con un aturdido) y con el molinete.

   **El molinete solo se veía en la pantalla del que lo usaba (8 de octubre) — ARREGLADO Y
   PROBADO ✅.** Después de `Activate()` el servidor manda a los demás el clip suelto de la
   habilidad; con el molinete llegaba justo DESPUÉS de la pose sostenida (el canalizado) y
   la pisaba. Al dueño no le pasaba porque ese RPC se lo saltea. Ahora `GA_Whirlwind` dice
   que manda su propia animación (`BroadcastsOwnAnimation`) cuando tiene clip de bucle.
   El **Golpe final** tenía lo mismo y uno más: el mandoble del final solo se veía en el
   host. Ahora el dueño no lo anticipa al apretar, nadie lo recibe al activar, y al
   terminar la carga va a TODAS las pantallas (RPC nueva
   `ServerPlayAbilityAnimationOnAll`; Unity tiene que recompilar).
   - [x] **PROBADO ✅ (8 de octubre).** Con dos ventanas: el molinete gira y sostiene la pose en la del otro; el Golpe final
         levanta el arma, la sostiene y baja el mandoble, en las dos (también cortado con un
         aturdido: baja el arma sin mandoble).
   - [ ] Canalizar como MECÁNICA genérica (un tiempo de lanzamiento antes de que salga
         cualquier habilidad, que se corta si te aturden): no está; la barra ya sirve para
         cuando se haga.

2. **El mapa del cementerio — GREYBOX ARMADO (29 de septiembre), falta jugarlo.** Escena
   nueva `Scenes/Mercenaries_Graveyard.unity` (copia de la del modo, con sala, red y menús;
   la vieja no se tocó). Ya la estás editando a mano. Detalle, rutas
   medidas y lo que falta en la sección 7.
3. **Sonido**: el sistema está; faltan los clips (y volver a prender la sección de
   sonido de Ajustes).
4. **Animaciones que se ven raras** (sección 5).
5. **Quedarse sin control al spawnear**: juntar los logs `[Unstuck]` cuando pase.

---

# Después de la prueba de 9 — arreglos del 26 y 28 de septiembre, TODO PROBADO ✅

**Lo que arreglaste en casa (commit `d8f0fe4`):** jefes y tótems que sobrevivían de una
partida a la siguiente (ahora la vuelta a la sala barre todo lo que no es de escena), el
escudo eterno del Paladín (red de seguridad de 20 s con aviso en consola), la entrega que
no se cancelaba con daño sin atacante (segundo detector mirando la vida), el botón
**Unstuck** del recuadro de red, y el cartel "+75 HP" del botiquín.

**Lo que se corrigió encima (28 de septiembre):**
- **Unstuck ya no es un atajo.** Antes te mandaba a la base cuando quisieras (con la bolsa
  quedabas junto a la entrega) y te quitaba un aturdido de verdad. Ahora el servidor solo
  lo acepta si llevas **5 s quieto**, **nadie te pegó en 5 s**, no tienes un control
  **legítimo** encima, no estás muerto y pasó su **cooldown de 60 s**. Y en vez de
  arreglar el personaje trabado, te da **uno nuevo del prefab**, en tu base, con tu clase
  y tu carga de definitiva. Si llevabas la bolsa, se cae donde estabas. Antes de
  reemplazarte deja en la consola qué encontró trabado (mantenidos abiertos, tags de
  control pegados sin efecto, `isAttacking`) — cada uso es una pista para el bug de
  quedarse sin control al spawnear.
- **La entrega ya no se cancela sola** cuando al portador se le acaba un bono de vida
  máxima (la vida se recorta al nuevo máximo y "bajaba" sin que nadie le pegue).

- **El "escudo eterno" era esto:** subir el escudo corta el básico que venía corriendo, y
  el aviso de "terminó" de ese básico le borraba el escudo al DUEÑO (no decía cuál
  habilidad terminó). El clic izquierdo sostenido volvía a pegar con el escudo arriba en
  el servidor, y al soltar el clic derecho ya no se avisaba nada. Ahora los avisos de fin
  dicen cuál habilidad terminó (`FinishAttack(GameplayAbility)`), y el servidor rechaza
  cualquier activación mientras haya un mantenido arriba. La red de 20 s de
  `GA_ShieldBlock` se queda como segunda red.
- **El menú de clases ya no dice "You got hit" al elegir:** equipar reiniciaba la vida con
  el máximo nuevo mientras el menú seguía abierto y lo tomaba como golpe. Ahora cierra
  antes de equipar, y también ignora el recorte al máximo.
- **Seguro de la preparación:** quien quede fuera de la planta de su sala (con 1.5 m de
  margen, `WarmupEscapeMargin`) vuelve a su punto de aparición. Tapaba la Intercepción
  heroica sobre un aliado pegado a los barrotes, y cualquier otro teletransporte futuro.

- [x] Unstuck: probado, funciona (28 de septiembre).
- [x] La entrega no se cancela al cambiar la vida máxima (28 de septiembre).
- [x] Paladín con los dos clics a la vez, el menú de clases sin "You got hit" y el seguro
      de la preparación: probados (28 de septiembre).
- **Clic izquierdo + clic derecho enseguida seguía pegando:** el ataque principal del
  Paladín es un switch (`GA_TagSwitch`) que clona el martillo o el golpe con Castigo, y
  la copia nacía SIN la marca de interrumpible: el escudo la cortaba pero el golpe
  llegaba igual. Ahora la variante hereda `IsInterruptible` del switch (como ya heredaba
  la velocidad de ataque). Cortado antes del evento de impacto del clip, no pega; el
  cooldown ya pagado no se devuelve.
- [x] Paladín: clic izquierdo e inmediatamente clic derecho → no pega, sube el escudo.
      Con Castigo divino cargado, lo mismo (la estela tampoco sale).
- [ ] Cuando alguien se atasque de verdad: guardar el `[Unstuck]` de la consola (host y
      cliente) y traerlo a la siguiente sesión.

**Pendiente de decidir:** la escena trae por defecto tu dirección de playit.gg
(`ssh-buried.tun.ply.gg:60625`). Cómodo para tus amigos, pero para probar en tu PC con dos
ventanas hay que escribir `127.0.0.1` / `7770` a mano, y si el repo de GitHub es público
la dirección queda a la vista.

---

# La prueba de 9 jugadores (viernes 25 de septiembre) — HECHA

Lo que salió de la prueba está arriba, en "Después de la prueba de 9". Los puntos de
"Qué observar" que nadie comentó siguen abiertos para la próxima prueba con gente.

Es una **prueba, no la demo** (la demo es la del showcase, en diciembre): lo que vale
de esta noche es lo que la gente diga y lo que se vea en la grabación.

## Antes de que lleguen

- [x] **Build nueva desde `main`** (`2f636b3` o más nuevo). Trae todo lo del 24 y 25:
      la entrega de 3 s, la bolsa que no entra a la base, los tótems rompibles con barra
      de vida, el Blink que gira la cámara, toda la retroalimentación visual nueva
      (sección 8) con los textos de la partida en inglés, los multiplicadores que ahora
      se multiplican (tope de ataque 0.3 s, sección 3) y la limpieza del proyecto
      (sección 9). Ya revisado en el prefab:
      `StartSubclassWithFullUltimate` apagado (la definitiva arranca en 0) y la carga
      por rol en 0.15 / 30 / 0.2.
- [x] **Ensayar el host con `-host`**, si todavía no lo hiciste: 10 minutos con una
      segunda máquina (o una segunda copia de la build conectándose a `127.0.0.1`).
      Nunca hosteamos desde una build; que no sea la primera vez con 8 personas
      esperando. Detalles abajo, en la sección 2.
- [x] **playit.gg corriendo** y la dirección + puerto (7770 UDP) listos para pegar.
- [x] **Rendimiento del host**: servidor + tu espectador + la grabación en la misma
      máquina. Si el host tartamudea, tartamudean todos: si hace falta, baja la calidad
      de la grabación antes que otra cosa.
- [x] Bots: con 9 personas no hacen falta. Si falta alguien, llena ese lugar con uno.

**Las reglas en 30 segundos**, para pegar en el grupo antes de empezar:

> Somos 3 equipos de 3. Gana quien entregue 2 veces la caja dorada que aparece en el
> centro. Se levanta con solo acercarte; cargándola vas más lento y la R la suelta.
> Para entregar llévala a la plataforma de afuera de tu base y **quédate 3 segundos**:
> si te pegan o te sales, vuelve a empezar. Con la caja no puedes entrar a tu base. Al
> llegar a nivel 3 eliges subclase con la V (mejor en tu base). La clase solo se cambia
> dentro de tu base.

## Qué observar durante las partidas

Son las perillas y dudas que quedaron abiertas y que solo se contestan jugando:

- [x] **Subida a 6 s (30 de septiembre, `MercObjective.DeliverSeconds`).** **La entrega de 3 s**: ¿los rivales llegan a cortarla? ¿3 s es poco o mucho?
      ¿Una herida o un veneno encima reiniciándola se siente justo o frustrante? De
      paso: salir de la zona a mitad vuelve la barra a 0, y en un cliente la barra
      avanza suave, no a saltos.
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
- [ ] **La retroalimentación nueva con 9** (sección 8, probada solo de a pocos):
      ¿los números de daño se leen o ensucian la pelea grande? ¿El registro de bajas
      tapa algo? ¿La viñeta roja molesta? ¿La pantalla de muerte dice lo que hace falta?
      ¿Alguien no entendió algún aviso por estar en inglés?
- [ ] **El Chamán y la velocidad de ataque**: los bonos ahora se multiplican y el tope es
      0.3 s. ¿El Chamán con Enfurecer + Tigre se siente bien, o quedó lento? ¿El
      Berserker sigue rápido? (ver sección 3, velocidad de ataque)
- [ ] **Balance**: qué clase o subclase dominó, cuál no eligió nadie, qué se sintió
      injusto.

## Después

- [x] Anotar lo que dijeron **esa misma noche**, mientras está fresco (aunque sea en
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

Lo que falta hacer antes (ensayar el host, la IP de playit.gg, el rendimiento) está
arriba, en **★ HOY EN LA NOCHE → Antes de que lleguen**.

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

## Velocidad de ataque: los bonos ahora se MULTIPLICAN — HECHO Y PROBADO ✅ (25 sept.)

`AtkSpeed` son **segundos entre ataques** (menos = más rápido). Antes los multiplicadores
se **sumaban** (valor = base × (1 + suma de los −%)): dos bonos de −50 % daban cero y
siempre caían en el tope, y la bolsa (×0.75) + `GE_Slow` (×0.3) dejaba al portador casi
quieto (×0.05).

**Ahora se multiplican** (`AttributeValue.Multipliers`: la lista de multiplicadores
activos, se agrega al aplicar el efecto, se saca al quitarlo, y el valor usa el
producto). Vale para TODOS los atributos. Y el tope subió de 0.2 s a **0.3 s**
(`AbilitySystemComponent.MinAttackInterval`, 3.3 ataques por segundo).

| Base | Con | Antes (sumando) | Ahora (multiplicando, tope 0.3) |
|---|---|---|---|
| Chamán 1.2 s | Enfurecer (×0.5) | 0.6 s | 0.6 s |
| Chamán 1.2 s | Enfurecer + Tigre normal (×0.9) | 0.48 s | 0.54 s |
| Chamán 1.2 s | Enfurecer + Tigre potenciado (tu ×0.8) | 0.36 s | 0.48 s |
| Chamán 1.2 s | Enfurecer + Tigre potenciado (el ×0.5 original) | 0.2 s (tope viejo) | 0.3 s (justo el tope) |
| Berserker 0.5 s | Enfurecer (×0.5) | 0.25 s | **0.3 s** (el tope lo frena un poco) |

Portador de la bolsa (×0.75) con `GE_Slow` (×0.3): antes ×0.05, ahora ×0.225.

- [x] Probar: el Chamán con Enfurecer + Tigre ya no ataca al tope, el Berserker sigue
      sintiéndose rápido con 0.3 s, y un portador ralentizado todavía se mueve.
- [ ] Decidir los valores del Tigre: tu 0.9 / 0.8 era para frenar la suma. Multiplicando,
      el potenciado deja al Chamán en 0.48 s; si se siente lento, volver al 0.8 / 0.5
      original (0.3 s, justo el tope).

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
- [x] **Decidido (25 de septiembre): se quedan donde están.** Te gustó cómo quedaron.

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
- [x] **Se quitó el `NetworkAnimator` del prefab del jugador** (25 de septiembre, con la
      herramienta de limpieza). No volver a ponerlo: ver `CLAUDE.md`.

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
- [x] **Decidido (25 de septiembre): el rayo del Paladín NO se interrumpe.** Castigo
      divino funciona como un switch del siguiente ataque; cortarlo no tiene sentido.

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
- [x] **Decidido (25 de septiembre): `GhostAlpha` se queda en 0.35.** `GhostForAllies`
      sigue apagado; prenderlo haría que tu equipo sepa de un vistazo que el enemigo no te ve.

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

- [x] **Desarmar** — Guerrero (Maestro de batalla), Clérigo (Zona de verdad). Hecho: `State_Disarmed` bloquea las acciones de arma (1 de octubre)
- [x] **Repeler / atraer** — Monje (Patada del viento), Explorador (trampa explosiva). Hecho y probado (6 de octubre): sección "Desplazamiento" de cada GE (`KnockbackDistance`, dirección, duración, elevación, `PullStopDistance`). Es CC: el Imparable no se mueve, la resistencia al control recorta la distancia, el escudo no lo frena. Falta: que la trampa empuje desde ella y no desde el Explorador
- [x] **Revelar invisibles** — Clérigo (Faro de esperanza), Explorador (Búho). Hecho: `State_AlwaysVisible`
- [x] **Vuelo libre** — Mago, quizá Muerte silenciosa del Shinobi. Hecho y probado (6 de octubre): `GA_Flight` (Create ▸ GAS ▸ Generics ▸ Flight) + un GE que dé `Status_Flying`. Despega con impulso; WASD en 3D hacia la cámara (×ForwardBoost solo hacia adelante), Espacio sube, **Ctrl baja (Ctrl ya no ataca; en control, L3)**; no aterriza; sin techo propio (el del mapa). Quieto en el aire cae despacio (`IdleSinkSpeed`). Daño de otro o enraizado lo terminan; al terminar en el aire, o aturdido, cae lento. Los bots no lo usan. Prueba: `GA_Flight` y `GE_Fly` quedaron en `GameplayAbilities/` (mover `GE_Fly` a `Effects/` desde Unity)
- [ ] **Mascota con IA en red** — Explorador; sirve para futuros summons

## Para la demo: Clérigo + Guerrero (~4–4.5 semanas)

- Las dos más baratas.
- Tanque y Soporte quedan completos (2 clases cada uno): nadie repite forzosamente ahí.
- Con la demo en diciembre, caben también el mapa del foso (sección 7, 1–2 semanas) y
  unas 3–4 semanas de pulido visual.
- Daño sigue siendo solo Pícaro; si quieres variedad, el siguiente es el Explorador.

**Alternativa:** solo los **kits base** de varias clases (~3–5 días cada uno, porque lo
nuevo vive casi todo en las subclases). Contra: al llegar a nivel 3 no hay qué elegir.

- [x] Decidir: Clérigo + Guerrero completos (28 de septiembre). Clérigo: kit base hecho;
      Clérigo completo; Guerrero: kit base, Maestro de batalla, Comandante y Guardián probados (5 de octubre): Guerrero completo.
- [ ] Antes del Mago: definir la extra de Filo danzante (el vuelo libre ya está, 6 de octubre)

---

# 7. El mapa se siente chico — entrega con espera y mapa del foso

**El diagnóstico (24 de septiembre).** Las bases están a 47 m del centro; cargando se va
a 4.5 m/s, o sea ~10 s caminando. Pero el salto del Bárbaro (`JumpVelocity 15`,
`ForwardForce 15`, gravedad 9.8) vuela ~3 s a 15 m/s: **~45 m en papel, casi todo el
viaje de un salto**. El Pícaro suma 2 dashes de 6 m y el Blink. Y la bolsa solo bloquea la
R, no el Shift. Por eso agrandar el mapa sin tocar las reglas no alcanza.

## Entregar toma 3 segundos — HECHO Y PROBADO CON BOTS ✅

Inspirado en el cobro de The Finals. `MercObjective.DeliverSeconds` (3 s; 0 = entrega
instantánea como antes): hay que quedarse en la zona de entrega. **Salir de la zona o
recibir daño de otro personaje reinicia la cuenta.** El daño es el mismo que carga la
definitiva del tanque (`OnDamageEndured`): lo que frena un escudo cuenta y corta la
entrega; lo que se bloquea del todo con el escudo direccional, no.

Todos ven "ENTREGANDO 1.8s" y una barra del color del equipo bajo el marcador del
Objetivo; tu equipo ve "ENTREGANDO" en el marcador de la entrega.

- [x] Probado con bots: anota a los 3 s y el golpe reinicia (24 de septiembre)

Lo que falta ver ya es con gente: está en **★ HOY EN LA NOCHE → Qué observar**.

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
- [x] Greybox de UN sector (panteón, atrio, laberinto, torre, catacumbas, capilla y
      cripta), prefab `Prefabs/Map/Graveyard_Sector` rotado 3 veces (29 de septiembre)
- [x] NavMesh: rampas macizas y escalones de lápidas, nada de escaleras de mano
- [x] La fosa con un **NavMesh Link de un solo sentido** (los bots solo bajan), y otro
      para bajar del techo de nichos al pre-atrio
- [x] `MercArenaBounds.CeilingHeight` a 25 m, y tapas invisibles hasta 26 m sobre todas
      las paredes de afuera (el salto del Bárbaro las pasaba)
- [x] Spawns, campamentos (jefe en la capilla, magos arriba de cada torre, fantasmas en
      el campo de tumbas y en la explanada), botiquines y `ObjectiveSpawnPoint` en la cripta
- [ ] **Abrir la escena en el editor, darle Play y recorrerla** (ver abajo qué mirar)
- [ ] Probar con bots cuánto tardan en volver por cada ruta, y jugarlo, ANTES de decorar
- [ ] Decoración al final: nichos, cruces, velas, pétalos

### Lo que quedó armado (29 de septiembre)

**Después lo retocaste a mano** (29 de septiembre): sacaste algunos muros que no te
gustaron y agrandaste la aparición de enemigos del centro. Te gustó como quedó; desde
acá se edita solo a mano. Las rutas de abajo se midieron ANTES de esos cambios: con
menos muros probablemente bajaron.

La armó `Scripts/Editor/MercGraveyardBuilder.cs`, una sola vez: copió `Mercenaries_Gamemode`
a **`Mercenaries_Graveyard`** (con sala, red, HUD, menús y espectador), armó el sector
como prefab, reusó las bases, los 9 campamentos (más uno), los 9 botiquines y el punto
del Objetivo, y horneó el NavMesh. **Desde ahí se edita a mano**: la herramienta se
borró el 29 de septiembre, sin commitear (volver a correrla pisaba los cambios): las
mallas de los pisos con hoyos quedan como están en `Art/Models/Graveyard`.

- **Escena en Build Settings DESACTIVADA**: la build sigue abriendo el mapa viejo. Para
  probar el cementerio en una build, ponerla primera y activarla.
- **Pisos con hoyos**: el piso de cada sector es una malla en anillos (coordenadas
  polares), así los hoyos de las escaleras cortan exacto y el sector gira sin costuras.
  Todo hoyo cae sobre algo caminable (la cripta o una catacumba).
- **La torre** mide 12×12: con 10×10 los tramos de escalera empalmaban de costado con el
  descanso y el NavMesh se comía la unión (los bots no subían).
- La capilla **no tiene techo** (se ve desde el menú y el espectador). Sus puertas: una
  ancha hacia la avenida y una angosta hacia cada torre. La rampa de la cripta sale
  hacia la torre, no hacia la base.
- La cámara del menú y la del espectador arrancan más alto y más lejos.
- **18 botiquines** (29 de septiembre): los 9 del piso, más uno arriba de cada torre y
  dos en el techo de nichos junto al muro de cada sector (a los dos lados de la avenida).
  Se pusieron sobre la escena ya editada, sin rehacer el mapa. Para sumar o sacar más,
  a mano (duplicar uno: Ctrl+D y moverlo; FishNet le da su identificador de red solo).

**Rutas medidas con el NavMesh** (a 4.5 m/s, la velocidad cargando la bolsa; el informe
lo deja el modo batch en `routes.txt`):

| Ruta | Distancia | Cargando |
|---|---|---|
| Cripta → entrega, lo más corto | 76 m | ~17 s |
| A pie: capilla y avenida | 80 m | ~18 s |
| Catacumba: torre y pasillos | 100 m | ~22 s |
| Techos: torre, puente y techos de nichos | 137 m | ~30 s |
| Base → cripta caminando | 90 m | (sin bolsa, ~15 s) |
| Base → cripta por la fosa | 106 m | (sin bolsa, ~18 s) |

Tres cosas para decidir jugándolo:
- [x] **Rutas revisadas por Gustavo (5 de octubre): están bien.** **La ruta a pie queda en ~18 s**, un poco debajo de los 20–25 s del diseño. Palanca
      barata: cerrar la puerta ancha de la capilla (salir solo por las de las torres
      suma ~15 m).
- [x] **La fosa NO es más rápida que caminar** (106 m contra 90): las dos van derecho del
      atrio al centro, así que la distancia es casi la misma. Hoy su ventaja es ir
      tapado, sin pasar por el laberinto ni por el jefe. Si tiene que ser un atajo, hay
      que alargar la de arriba (lo de la capilla ayuda).
- [x] **La ruta por los techos es la más lenta** (~30 s): subir la torre cuesta. Es la
      alta y expuesta; ver si alguien la usa.

**Qué mirar al recorrerla:** que la cámara no se meta en el techo de las catacumbas,
que los magos no se caigan de la torre, que el jefe tenga lugar en la capilla, que desde
el pre-atrio se lean las dos entradas al atrio, y que el muñeco de práctica
(`HumanDummy_F Red`, movido a la explanada del equipo 1) no estorbe.

---

# 8. Más retroalimentación visual — LAS 7 HECHAS Y PROBADAS ✅ (25 de septiembre)

Hechas el mismo día, probadas de a pocos (falta verlas con 9: ver "Qué observar" arriba).
Dónde vive cada una, por si hay que ajustarla (todas se arman solas, sin cablear, y sus
perillas aparecen en el Inspector durante Play):

- `UI_ScreenFeedback`: viñeta de poca vida, pantalla de muerte, avisos cortos.
- `UI_KillFeed`: registro de bajas. Lo alimenta `PlayerController.ServerAnnounceDeath`.
- `UI_UltimateSlot`: el latido y el aviso de la definitiva lista.
- `MercObjective` → "Columna de luz": la columna de la entrega (`Sprites/Default`).
- `UI_DamageNumbers`: los números. Los manda `NetworkASC.ServerShowCombatNumber`; las
  curaciones se juntan cada `HealNumberBatchSeconds` (0.35 s).
- `UI_CombatFeedback.HitCriticalColor`: la X amarilla.
- `UI_PlayerHUD`: las habilidades se esconden mientras estás muerto.

Además, **todo lo que se ve dentro de la partida pasó a inglés** (regla en `CLAUDE.md`).
Después también pasaron a inglés los menús (inicio, ajustes, red, sala), y la sección de sonido de Ajustes quedó oculta hasta que haya clips (`UI_SettingsPanel.ShowSoundSection`).

La lista original:

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

---

# 9. Depuración del código y los prefabs (25 de septiembre)

**Qué se revisó:** los 134 scripts del proyecto (referencias en prefabs, escenas y
assets, y en el código), todos los componentes de los prefabs y de la escena del modo
(scripts rotos, desactivados o repetidos), y los métodos y tipos que nadie usa.

**Resultado: el proyecto está limpio.** Ningún script de juego sin usar, ningún
componente roto ni desactivado en los prefabs, ningún tipo huérfano.

**Lo que se quitó:**
- Código: `ASC.GetCooldownRemainingNormalized` (su comentario decía que lo usaban las
  ranuras del HUD; ya no) y `BotController.AllyInTrouble` (la lógica de la definitiva
  del Paladín bot ya usa `AllyDangerHealth`).
- Con la herramienta de un solo uso `Mercenarios ▸ Limpieza del 25 de septiembre`:
  - [x] Corrida y borrada (25 de septiembre).
  - El `NetworkAnimator` inerte del prefab del jugador.
  - `SampleScene` (la de ejemplo de Unity, con scripts rotos; ya estaba fuera de la build).
  - `TextMesh Pro/Examples & Extras` (~6 MB de ejemplos que nada usa).
  - `Assets/FishNet`: carpetas vacías, restos de los `.csproj` borrados.
  - El prefab de `AbilityPreview` pasa de `Scripts/Editor` a `Prefabs/Tools`.

**Lo que se dejó a propósito:**
- Las herramientas del menú `Mercenarios ▸` (paredes, espectador, menú principal,
  botiquines, audio, registros). **Actualización del 29 de septiembre:** el cementerio se
  armó copiando la escena, así que no hicieron falta. Se borraron las de paredes,
  espectador, menú principal y botiquines (ya aplicadas en las dos escenas), y la que
  armó el cementerio. Quedan
  registros (se usa siempre) y audio (**todavía no se corrió**: falta crear
  `AudioLibrary` y el `AudioListener`, en las dos escenas).
- El `Rigidbody` cinemático de la raíz del jugador: sirve para los triggers, y el
  ragdoll ya lo excluye.
- `ASC.GetTagCount`: nadie lo usa hoy, pero es el accesor natural de los tags con
  conteo.
- `Test_Network.unity`: la escena de pruebas de clases.

**Para juntar más adelante** (no urgente, ninguno es un error):
- Los cuatro overlays que se arman solos (`UI_CombatFeedback`, `UI_ScreenFeedback`,
  `UI_KillFeed`, `UI_DamageNumbers`) repiten el mismo `Get()`/`Awake` de singleton:
  podrían heredar de una base común.
- Varios scripts resuelven la cámara con el mismo cuidado ("una cámara apagada no es
  null"): podrían usar todos `PlayerController.MainCamera`.
- Los packs de `AssetsExtra` traen escenas y materiales de demostración (por ejemplo,
  las demos de Kevin Iglesias). Son de los packs, así que no los toqué.
