# Pendientes — actualizado el 28 de octubre de 2026

Revisión completa contra el estado real del proyecto: **la mayoría de las tareas de
editor de la lista anterior ya estaban hechas**. Acá quedan solo las que verifiqué que
siguen abiertas, más el plan.

El proyecto compila limpio: 0 errores y 0 warnings en código propio (los 312 warnings
del log son todos de packs importados).

## ★ Al retomar: lo abierto, en orden (22 de octubre)

0. **El showcase se adelantó al 15 de noviembre: PLAN en curso (26 de octubre)** — ver "★ Showcase del 15 de
   noviembre". **Etapa 1** (los menús con control, la conexión en el menú y la pausa) y **etapa 2** (la lista
   de partidas en la red, el teclado en pantalla y los botones con el elegido más oscuro): HECHAS, falta
   probar (las herramientas ya se corrieron y se borraron). Documentos al día (26 de octubre:
   `Red_Arquitectura.docx`, el LEEME del modo y CLAUDE.md). **Etapa 3 (tres ventanas en una PC, cada una con su control): HECHA (28 de octubre; la herramienta del
   cartel ya se corrió y se borró), falta hacer una build y probar con los controles: ver "Etapas" ▸ 3.**
1. **Subclases del Mago: PLAN en curso (22 de octubre)** — ver "★ Subclases del Mago"; plan CERRADO;
   **etapa 1 (Escuela de evocación + cambios al kit base): HECHA Y PROBADA ✅**; **etapa 2 (Escuela de abjuración): HECHA Y PROBADA ✅**; **etapa 3 (Filo danzante): HECHA Y PROBADA ✅**. Documentos al día (23 de octubre). Orden: Evocación, Abjuración, Filo danzante.
   **Misiles de a 3 (22 de octubre): HECHO Y PROBADO ✅** (se quedó en ráfaga y no en combo: un solo gesto, 0.2 s fijos). Cada ataque es una RÁFAGA de 3 misiles, 0.2 s entre
   cada uno, de 1/4 del daño mágico cada uno (`GE_ArcaneMissileDamage`), y cada uno deja su punto; la
   Vulnerabilidad arcana bajó a +1 por punto (cada misil suma el suyo). Probado: salen los 3 siguiendo la mira; con 5 puntos, cada misil pega 1/4 + 5; un aturdido, morir
   u otra habilidad cortan los que faltan; el ritmo de ataque al mantener no cambia; la definitiva
   carga lo mismo que antes por ataque.
   **El escudo a la vista (22 de octubre, pedido de Gustavo): HECHO Y PROBADO ✅ (23 de octubre).** La barra de arriba de la
   cabeza (Nameplate, pieza "ShieldFill", ya puesta por la herramienta y borrada) muestra el escudo como
   VIDA EXTRA: un tramo celeste justo después de la vida, que no la achica; si vida + escudo pasa de la
   máxima, sale por la derecha de la barra (hasta el doble). La burbuja NO es un componente (Gustavo no
   lo quiso: los VFX nacen de los GE): la pasiva de la Abjuración le pone `GE_ArcaneShield` (Buffs: ícono
   + "Magic shield blue" ×0.8 en su TargetVFX, provisional) a quien tenga escudo suyo, y se lo quita
   cuando se le acaba. Probar: el Mago al pegar y el aliado proyectado, con su tramo y su burbuja.
   Arreglado el 22 de octubre: el tramo no se veía (la copia del relleno de un Slider sale con las anclas
   en 0, sin alto); ahora copia en vivo la forma del relleno de vida. Y se apagó el "blur de giro" de las
   armas lanzadas (hacha, dagas, kunais, lanza: `PF_ExampleProjectile` → "Blur: material" vacío), que
   tenía el material de trail de Vefects y tiraba "doesn't have a color property _Color".
2. **Los bots del Mago (etapa 4): HECHOS Y PROBADOS ✅ (23 de octubre).** Ver "★ El
   Mago" ▸ Etapa 4. Las etapas 1, 2 y 3 están hechas y probadas ✅ (el kit entero). Quedó por
   confirmar que la Tumba cura y que la Flecha helada vuela más rápido.
   **Bots revisados el 23 de octubre (pedido de Gustavo): HECHO Y PROBADO ✅ (23 de octubre).** El rol de la
   Abjuración lo pasó Gustavo a **Daño** (el rol lo fija la clase): sus bots pelean de lejos como el Mago.
   Arreglado en `BotController`:
   · **El hacha apuntada para siempre:** un lanzamiento cobra al soltar, así que mientras apuntaba seguía
     "disponible" y el bot lo reactivaba, reiniciándole la cuenta para soltar (hasta el corte de 30 s). Ya
     no se aprieta un mantenido mientras haya otro abierto. Y cuánto se sostiene depende de qué es
     (`HoldSecondsFor`): un lanzamiento apuntado, lo que canaliza + 0.6 s (⚙ "Apuntar un lanzamiento");
     un ataque cargado, hasta su última etapa (el arco, la Mordedura de hielo que aturde); el resto, 1.6 s.
   · **Habilidades sobre un aliado** (la Proyección arcana, la curación apuntada del Clérigo): se apuntaban
     al ENEMIGO y no encontraban al aliado. Ahora se apuntan al aliado (`ChooseAllyFor`: el más herido; la
     Proyección, el más cercano sin proyectar), y la mira se escribe antes de preguntar si se puede.
   · **La Proyección** se pone una vez y no se relanza mientras dure (`ArcaneProtectionPassive.HasProjectionFromE`).
   · **La Sobrecarga de hechizos** (la R de la Evocación, un vuelo): los bots no usaban nada que vuele.
     Ahora la usan en plena pelea SIN volar: `GA_Flight` le cambia a un bot el efecto del vuelo por una copia
     sin `Status_Flying` (`GameplayEffect.CreateCopyWithoutTags`) y se queda con la resistencia, el maná y las Q.
     **Desde el 29 de octubre los bots SÍ vuelan** (ver punto 3, "El Mago bot vuela"): la copia sin vuelo se fue.
   · **Dashes derechos:** apuntaban al pecho desde los pies y salían en diagonal hacia arriba (el corte
     avanzando del Filo, el del Pícaro).
   · **Definitivas:** un buff del que pega de lejos (la Protección máxima, la Ira primal) va en plena pelea a
     su distancia; una de área de un asesino (el Baile final), con 2 o más juntos o para rematar.
   · Un Soporte cuyo básico pega de lejos se quedaría a distancia de tiro (`FightsFromAfar`), para el futuro.
   · **Los invisibles** (23 de octubre, segunda vuelta): los bots los perseguían como si nada. Ahora no ven a un
     invisible salvo que lo revelen a su equipo (`CanPerceive`: la Marca del cazador, el Faro), como los NPCs;
     si su objetivo se vuelve invisible, lo pierden.
   · **La Patada voladora del Monje y el Corte final del Samurái** (`GA_RushAttack`) y **Muerte silenciosa** del
     Shinobi (`GA_ChainStrike`): los bots no los usaban (MovesThroughOwner). Ahora la embestida de un bot es un
     dash derecho de su cerebro (`NetworkASC.ServerStartRush` / `ServerStopRush` → `BotController.ServerDash` /
     `ServerStopDash`) y la cadena usa su teletransporte. El Shift del Monje (un switch de patadas) cuenta como
     movimiento: para acercarse, solo si llega.
   · **El Destello del Clérigo** (`GA_Teleport`) caía en "cuerpo a cuerpo" y lo teletransportaba encima del que
     le pegaba. Ahora es movimiento y va a donde conviene (`TeleportAim`): lejos del enemigo en apuros, al aliado
     herido para socorrerlo.
   Probado con bots: Bárbaros que tiren el hacha al rato de apuntar (no quietos con el arma atrás); un Clérigo
   que cure al aliado herido; una Abjuración que proyecte una vez a un aliado y pelee de lejos; una Evocación
   que use la R (resistencia y maná, sin volar) y la Mordedura cargada; un Filo que corte de cerca y use el
   corte avanzando derecho; Pícaros con el dash derecho; un Asesino o un Shinobi invisible al que el bot NO
   persigue; un Monje que entra con la Patada voladora; un Samurái y un Shinobi que usan su definitiva; un
   Clérigo que se escapa con el Destello cuando está en apuros.
   **Tercera vuelta (23 de octubre, pedido de Gustavo): HECHA Y PROBADA ✅ (23 de octubre).**
   · **Los inmunes** (la Tumba helada, la Protección divina, Muerte silenciosa, el que está en su sala): los bots
     les gastaban habilidades. Ahora prefieren a cualquier otro enemigo a la vista (un inmune pesa como si
     estuviera 60 m más lejos) y no lo cuentan para un área; si es el único, no gastan nada CONTRA él
     (`UsefulAgainstImmune`): solo el básico y lo que no va contra él (protegerse o curarse, escapar, curar a un
     aliado, la definitiva de un soporte, elegir hechizo o compañero).
   · **El compañero del Maestro de bestias** (`DesiredCompanion`): antes lo cambiaba al azar cada tanto. Ahora,
     según la pelea: el Búho si se le hizo invisible un enemigo hace poco (8 s), el Oso en apuros o peleando
     contra un NPC, y si no el inicial (el Lobo). Lo lee de cada prefab (`Entity_Companion`: caza invisibles, se
     pone delante, atrae fantasmas), no de los nombres. Se queda con cada uno 10 s (⚙ "Mantener el compañero");
     si se le muere, cambia enseguida por otro que no esté esperando para volver.
   Probado con bots: un Mago en la Tumba o un Paladín con la Protección divina al que el bot deja de tirarle
   habilidades (y se va con otro si hay); un Maestro de bestias que pelea con el Lobo, saca al Oso contra los
   fantasmas o con poca vida, al Búho si un Asesino o Shinobi se le hace invisible, y otro si se le muere el que tiene.
3. **Probar lo de antes:** el parry del Guardián en un cliente; los bots con el Guerrero (los del
   Explorador y el Monje, probados ✅ el 23 de octubre en las vueltas de bots). (`GA_SpawnSummon` con variantes de las plantillas ya quedó probado: el Muro
   y la Tormenta del Mago.)
   **Bots con las clases nuevas, revisados el 28 de octubre (pedido de Gustavo): HECHO, falta probar.** Se cruzaron
   las 32 clases con `BotController`; seis habilidades no se usaban bien. Arreglado:
   · **Resurrección** (Clérigo de la vida, R): nunca revivía (apuntaba al enemigo). Ahora, con la R lista y un
     aliado muerto a 25 m o menos ("Buscar cuerpos hasta"), camina hasta el cuerpo y lo revive.
   · **Presa** (Cazador, E): solo cuerpo a cuerpo, y el Cazador pelea de lejos. Ahora sobre el jugador que
     pelea, apenas lo ve y está a tiro (30 m).
   · **Trampas** (Trampero, E): casi nunca, y a los pies del enemigo. Ahora entre él y el enemigo que se le
     acerca (a mitad de camino), y una detrás al retirarse a curarse.
   · **Ejecución** (Guardián, E): esperaba dos enemigos juntos. Ahora cuerpo a cuerpo contra uno con poca
     vida (el mismo umbral del Asesino para rematar).
   · **Aturdir** (Clérigo del orden, E) y **Enemigo jurado** (Paladín de la venganza, E): solo trabados cuerpo
     a cuerpo. Ahora sobre el enemigo que le pega a un aliado (o a él), hasta 10 m.
   · **Furia elemental** (Chamán, R): salía con el Chamán lejos de la pelea. Ahora además metido en ella.
   - [ ] Un Clérigo de la vida bot revive a un compañero caído cerca (va hasta el cuerpo).
   - [ ] Un Cazador bot marca su Presa de lejos; un Trampero bot pone trampas entre él y quien se acerca, y
         una detrás al huir.
   - [ ] Un Guardián bot usa la Ejecución sobre uno con poca vida.
   - [ ] Un Clérigo del orden y un Paladín de la venganza bots marcan al que le pega a un compañero.
   - [ ] Un Chamán bot suelta la Furia elemental dentro de la pelea, no de lejos.
   **El Shinobi bot pelea a distancia (29 de octubre, pedido de Gustavo): HECHO, falta probar.** Su básico es un combo de
   kunais (`GA_ShinobiStrikes`), y `BotController.Classify` tomaba todo combo como cuerpo a cuerpo: lo ponía por la
   espalda y solo tiraba a 3 m. Ahora un combo con TODOS sus pasos a distancia cuenta como a distancia (hoy solo el
   del Shinobi): pelea como el Mago, a ~11 m rodeando, tira hasta 25 m si lo ve y se retira a curarse con poca vida.
   - [ ] Un Shinobi bot tira las kunais de lejos, rodeando, y no se pega al enemigo.
   **El Mago bot vuela (29 de octubre, pedido de Gustavo): HECHO, falta probar.** Antes no volaba nunca (el vuelo lo
   mueve el dueño, y un bot no tiene). Ahora lo mueve su cerebro (`BotController` ▸ VUELO, `ServerStartFlight`) con
   los mismos números del `GA_Flight`, a **8 m** sobre donde va (⚙ "Altura de vuelo (m)"), sin pasar el techo de
   la arena. Despega (si no tiene techo encima):
   · **para escapar:** volviendo a curarse; cerca de su base aterriza AFUERA (10 m) y entra caminando;
   · **para pelear desde arriba:** con el enemigo a tiro y a la vista, y le sigue tirando hechizos desde el aire;
   · **para cruzar el mapa:** sin nadie a quien pegarle y con el destino a más de 20 m (⚙ "Vuela para ir a más de
     (m)"), o persiguiendo al que lleva el Objetivo. Al llegar sin nadie cerca, aterriza.
   Al terminar el vuelo (o si le pegan) cae lento como un jugador; el Terremoto lanzado volando lo baja en picada.
   La Sobrecarga de la Evocación ahora también levita de verdad. En el aire se ve en la pose de salto/caída.
   - [ ] Un Mago bot despega al ver a un enemigo, se queda arriba rodeándolo y le tira hechizos; al acabarse el
         vuelo baja y sigue peleando.
   - [ ] Con poca vida vuela hacia su base, baja afuera y entra caminando a la sala segura.
   - [ ] Sin enemigos, cruza el mapa volando hacia el centro o el Objetivo y aterriza al llegar.
   - [ ] Nunca se queda trabado arriba de un techo ni fuera del mapa (si pasa: dónde y haciendo qué).
   - [ ] Una Evocación bot con la Sobrecarga levita y pelea desde arriba.
   **Aterrizar a propósito (29 de octubre, pedido de Gustavo: "si siempre está volando se le complica cambiar de
   clase"): HECHO, falta probar.**
   · **Shift volando = aterrizar:** corta el vuelo del Shift aunque esté en cooldown (el cooldown sigue) y cae lento.
     La levitación de la Sobrecarga no la corta el Shift. (`PlayerController.HandleAbilityInput` →
     `NetworkASC.ServerRequestLand` → `GA_Flight.EndFlight`.)
   · **Cambiar de clase (C / cruceta arriba) o subclase (V / View) en el aire:** corta TODO vuelo (también la
     Sobrecarga), avisa "Bajando: el menú se abre al tocar el piso" y lo abre solo al aterrizar (8 s de espera como
     mucho). Lo mismo en un salto. Las reglas siguen: base solo en la sala segura, subclase solo al nivel máximo.
   - [ ] Volando, Shift: cae lento; el Vuelo queda en cooldown. Con la Sobrecarga, Shift no la corta.
   - [ ] Volando sobre la sala segura, C: baja y el menú de clases se abre solo al tocar el piso.
   - [ ] Al nivel máximo y volando, V: baja y se abre el de subclases.
4. **Cuando alguien se atasque:** traer `Logs/Unstuck.log`.
5. **Decisiones de Gustavo, sin prisa:** más prefabs (variantes de tótems y enemigos, la UI que
   todavía arma el código), acomodar los botiquines, la puntería si se siente mal.
6. **Después del Mago:** las animaciones raras (sección 5, con la pose del bloqueo del Monje) y
   el sonido (faltan los clips).
7. **Íconos del Mago: PUESTOS (28 de octubre).** Gustavo los dejó en `Art/Icons/Abilities/Wizard` y `Classes`; ya están
   en sus GA, GE, opciones de los libros y las cuatro clases. La Q (Poder del hechizo) ya tiene el suyo. Siguen
   con uno prestado: los Cortes, el Corte y la Danza de filos ("Slash attack") y la opción "Cortes" del libro del Filo.
   **Encargo de íconos ACTUALIZADO (28 de octubre):** `DesignDocuments/Encargo_de_Iconos` (solo local, excluido de git)
   suma el Monje, el Mago y el Explorador con sus subclases (293 íconos; las 5 clases viejas no cambiaron, revisado
   píxel a píxel); el zip del 7 de octubre quedó como `Encargo_de_Iconos_7_de_octubre.zip`. Dudas para Gustavo:
   - [ ] La Tormenta eléctrica: el botón de la Q con rayo y la de la Evocación usan `Barbarian/Tornado_Icon`; la
         del kit base, `Wizard/Storm.png`. ¿Cuál va?
   - [ ] El fuego de la rueda del libro usa `48toPlay/Fire.png` (fuera de `Art/Icons`).
   - Prestados de otra clase: Trampas (la Presa), Manto del Shinobi (Ocultarse del Pícaro), Ráfaga de kunais,
     katana del Samurái y lanza del Trampero (del Guerrero), Danza defensiva (Paladín), Imparable (dash del Pícaro).
8. **Las demos de FishNet, BORRADAS Y PROBADAS ✅ (28 de octubre).** El `NetworkManager` de las tres escenas era el prefab de
   `FishNet/Demos/Prefabs`: pasó a `Assets/Prefabs/Network/` (con su `NetworkHudCanvas`, el script
   `NetworkHudCanvases.cs` y la textura `1x1 Pixel`), con los mismos `.meta`, así que las escenas siguen igual.
   El resto se borró (4 avisos menos; quedan ~150, todos de FishNet). Compila sin errores en el editor.
   - [x] Entrar a Play en `Mercenaries_Gamemode` y `Test_Network`: la partida arranca y conecta como siempre.
     (`Mercenaries_Graveyard` ya arrancó y conectó bien el 28 de octubre, en la pasada con control.) **Probado ✅ (28 de octubre).**
9. **Pasada completa SOLO con control, en el editor (28 de octubre, pedido de Gustavo para jugar contra bots el
   viernes): HECHA ✅.** Con un control virtual (el CLI `unity`): menú → Crear partida → unirse → elegir clase →
   3 bots con "+" → Confirmar → Empezar → cambiar de clase (cruceta arriba) → la rueda del libro (LT + stick) →
   subclase (View) → pausa (Start, B cierra). Todo anduvo. Arreglado en la sala (`UI_LobbyPanel`):
   · **El selector de clase no llegaba a Confirmar:** el stick bajaba a los "Unirse" de la sala de atrás y
     `MenuInput` lo devolvía al ícono. Ahora tiene navegación propia (pestañas, íconos, Confirmar).
   · **El ícono bajo el stick se agranda** (⚙ "Ícono bajo el stick (×)", 1.15): antes solo cambiaba el nombre.
   · **Después de un "+" la selección pasa al "+" siguiente** del equipo (o al "x" si se llenó); antes caía en Confirmar.
   · **"Aprieta A en tu lugar para elegir clase"** con control, en vez de "Haz clic".
   **Decidido (28 de octubre):** la V / View abre las subclases SOLO con el nivel máximo (antes, en cualquier nivel;
   avisa "Llega al nivel 3"; ⚙ "PRUEBAS: subclase en cualquier nivel" en `UI_ClassMenu`), y la definitiva de la
   subclase arranca en 0: el Player tenía prendido "PRUEBAS: definitiva llena al evolucionar" y se apagó. Probado ✅.
   - [ ] En una build, con un control de verdad: la misma pasada.
   **Después de probar con control (28 de octubre, noche):** la rueda probada ✅ y pasada al stick DERECHO (el
   izquierdo camina); el teclado en pantalla y los Ajustes ya no se traban (se enlazan por filas, y `MenuInput` no
   deja que el stick salga de la pantalla de arriba); un marco blanco muestra lo elegido en todos los menús, y el
   teclado quedó más separado (la herramienta ya se corrió y se borró). **Todo probado ✅ (28 de octubre).** Ver
   "★ Showcase" ▸ etapas 2 y 3.

## Cómo retomar en la otra máquina

```bash
cd ".../Proyectos Gustavo/GasTest/GasTest"
git fetch origin && git checkout main && git reset --hard origin/main
```

Con **fetch + reset**, no con `pull` — la máquina de casa tuvo historia divergente en
julio y un pull normal recrea ese merge conflictivo. Ver `CLAUDE.md`.

---

# ★ Showcase del 15 de noviembre — PLAN (26 de octubre)

El showcase se adelantó al **15 de noviembre** (antes era en diciembre). Pedido de Gustavo: todos los menús
funcionando completos con control; la conexión dentro de los menús y con su mismo estilo (hoy es el recuadro
gris de la esquina, dibujado aparte y sin control); y poder jugar con 3 instancias en una misma PC o en 3 PCs.

**Decidido con Gustavo (26 de octubre):**
- En la PC del showcase juegan **varias personas a la vez**: cada ventana con su control (o una con teclado y
  mouse), cada una en su monitor.
- Con control se entra eligiendo de una **lista de partidas** que el juego busca solo (en la misma PC o en la
  red). Para una IP de playit o para el nombre, un **teclado en pantalla**. Recuerda la última dirección.
- **Cualquiera puede hostear** desde una build: se va el candado de "solo el editor o `-host`".

**Lo de la red, respondido (no hace falta código):**
- 3 instancias en una PC sin el editor: una crea la partida y las otras se conectan a `127.0.0.1`.
- 3 PCs en la misma red: a la IP local del host (192.168.x.x), puerto 7770, sin playit. El Wi-Fi de los
  eventos suele aislar los equipos entre sí: llevar un router propio (mejor con cable). La primera vez que el
  host abre una partida, el firewall de Windows pregunta: darle **Permitir** (si se cancela, nadie entra).
  Por internet, playit.gg como siempre.

## Etapas

1. **Los menús con control y la conexión en el menú — HECHA, falta probar (26 de octubre).**
   - Los menús pasan al Input System nuevo (`InputSystemUIInputModule`: stick, cruceta, A y B) y una sola
     pieza decide quién responde a "volver" (B / ESC) y a Start (`MenuInput`, en `Menus_UI`).
   - Jugar → una pantalla de conexión en `Menus_UI` (`UI_ConnectPanel`): Crear partida, la dirección (IP o
     IP:puerto, la recuerda), Conectar, Volver y el estado ("Conectando…", "No se pudo conectar").
   - El recuadro de ESC pasa a un **menú de pausa** (`UI_PauseMenu`) que abren ESC o Start: Continuar,
     Ajustes, Desatascarse y Menú principal. La partida no se detiene.
   - La sala, navegable con control: arranca con algo elegido y no lo pierde cuando los botones cambian.
   - La sala muestra la IP del host en la red (la que hay que darles a los demás).
   - `ConnectionHUD` queda solo para la escena de pruebas (`Test_Network`, que no tiene `Menus_UI`).
   **Antes de probar:** correr `Mercenarios ▸ Una vez ▸ Menús con control: conexión y pausa (showcase 1)` en
   `Mercenaries_Gamemode` y guardar; después en `Mercenaries_Graveyard` y guardar. Arma en `Menus_UI` la
   pantalla de conexión, la pausa y `MenuInput`; pasa el EventSystem de la escena al Input System nuevo; y saca
   el recuadro de red (antes copia su dirección de playit a la "Dirección por defecto" de la conexión).
   **Corrida en las dos escenas y borrada (26 de octubre).**
   **Botón Salir en la sala (26 de octubre, Gustavo: "el jugador no tiene manera de salirse del lobby"):** ESC o
   Start abren la pausa, pero en la sala no había nada a la vista. Ahora hay un botón Salir arriba a la izquierda
   (enfrente de Empezar) que vuelve al menú principal; siendo host dice "Cerrar sala". Se pone con
   `Mercenarios ▸ Una vez ▸ Botón Salir en la sala (showcase 1b)` (solo el prefab: una vez alcanza). **Corrida y borrada (26 de octubre).**
   Las piezas nuevas se acomodan a mano como las demás.
   **Salir siendo host pregunta antes (26 de octubre, Gustavo dijo que sí):** siendo host, Salir cierra la partida
   para todos, y en el showcase lo puede apretar sin querer alguien que no conoce el juego. Ahora el host lo aprieta
   dos veces: la primera cambia el texto a "¿Seguro? Se cierra para todos" (pausa) o "¿Cerrar para todos?"
   (sala) y la segunda sale. La pregunta se retira sola a los 4 s ("Tiempo para confirmar", en el inspector de
   cada uno) o si la selección se va a otro botón. Los demás salen de una. Es `LeaveConfirm`, que usan los dos.
   **Avisos de la consola, revisados con Unity cerrado (26 de octubre, pedido de Gustavo).** Compilación completa
   en batch: 0 errores y 0 warnings (incluido el codegen de FishNet). Lo que había:
   · `HealthPack.OnValidate` tapaba al de `NetworkBehaviour` (CS0114): el de FishNet, que en el editor registra
     el componente en su NetworkObject, no corría nunca para los botiquines. Ahora es `override` y llama al base.
   · `HealthPackVisual`: `enableWordWrapping` (obsoleto) → `textWrappingMode`.
   · El menú "Upgrade from Mirror" de FishNet usaba la API vieja de defines (2 × CS0618): cambiado a
     `NamedBuildTarget` (código del paquete, marcado con un comentario).
   · "There are no audio listeners in the scene": la cámara de la sala no tenía oído. Se le puso un
     `AudioListener` (en las dos escenas). Y `PlayerController` ahora la encuentra por su `MenuOrbitCamera`: perdió
     la etiqueta MainCamera el 2 de septiembre y desde entonces `Camera.main` daba null al aparecer el personaje,
     así que NO se apagaba: quedaba prendida debajo de la tuya toda la partida (el mapa se dibujaba dos veces).
   · **`Mercenaries_Gamemode` no había quedado guardada con la herramienta** (seguía con el EventSystem viejo y
     el recuadro de red). Se le hizo a mano lo mismo que la herramienta hizo en Graveyard; Unity abre las dos
     escenas bien (verificado en batch).
   - [ ] Al jugar, la consola sin avisos: ni "no audio listeners" en el menú, ni "2 audio listeners" en partida.
   - [ ] Al aparecer tu personaje, `Camara_Lobby` se apaga en la jerarquía; al terminar la partida o salir, vuelve.
   **Probar, con un control conectado:**
   - [ ] Menú principal: el stick y la cruceta recorren Jugar, Ajustes y Salir; A entra; Start o ESC abren los
         Ajustes y B (o ESC) los cierra.
   - [ ] Jugar → la pantalla de conexión. Crear partida → aparece la sala.
   - [ ] La sala con control: arranca en un "Unirte"; A te une y la selección pasa a tu ícono; A abre el
         selector de clase, el stick recorre las clases (sale el nombre), A elige una y abajo está Confirmar; B
         cierra el selector. Con la clase puesta, la selección pasa a Confirmar; siendo host y ya listo, a Start.
   - [ ] Start o ESC en la sala abren la pausa. Ajustes desde la pausa, y B vuelve a la pausa.
   - [ ] Salir en la sala vuelve al menú principal (con control se llega con el stick). Siendo host dice "Cerrar
         sala", y los clientes vuelven a la pantalla de conexión con "Se cortó la conexión".
   - [ ] Siendo host, Salir (en la sala y en la pausa) pregunta primero: el texto cambia a la pregunta y recién la
         segunda vez sale. Esperar 4 s, o moverse a otro botón con el stick, vuelve al texto normal. Un cliente sale
         con un solo apretón. En la sala, que "¿Cerrar para todos?" quepa en el botón (si no, agrandarlo a mano).
   - [ ] Jugando: Start o ESC abren la pausa (la partida sigue). Desatascarse funciona. Salir vuelve al menú
         principal (siendo host, el botón dice "Cerrar la partida y salir").
   - [ ] Otra ventana (una build) → Jugar → escribir `127.0.0.1` → Conectar → entra a la sala. Con una dirección
         mala dice "No se pudo conectar a …" (tarda unos segundos), y Volver mientras conecta lo cancela.
   - [ ] Con el cliente en la sala, cerrar la partida en el host: el cliente vuelve a la pantalla de conexión con
         "Se cortó la conexión".
   - [ ] Dos ventanas en la misma PC que crean partida: la segunda dice "No se pudo crear la partida (¿ya hay
         una abierta en esta PC?)" y no se mete en la de la otra.
   - [ ] Conectar a una dirección que no responde (la de playit con el túnel apagado), Volver mientras dice
         "Conectando…" (o esperar el error) y Crear partida: se crea bien. Antes decía "¿ya hay una abierta en
         esta PC?": el intento a medias seguía vivo (Volver no lo cortaba) y al cerrarse tiraba abajo la partida
         nueva. Arreglado el 26 de octubre (`UI_MainMenu.StopNetworking` corta también lo que se está conectando).
   - [ ] La sala y la pausa muestran, en el host, su IP de la red (192.168.x.x); en un cliente, a dónde se
         conectó.
   - [ ] Con mouse y teclado todo sigue como antes (escribir la dirección, Enter conecta).
   Ojo: con dos ventanas en la misma PC, hoy un mismo control mueve las dos. Eso es la etapa 3.
2. **Buscar partidas y el teclado en pantalla — HECHA, falta probar (26 de octubre).**
   - La pantalla de conexión busca sola las partidas abiertas en esta PC y en la red, y las muestra debajo de
     la línea de estado ("Partidas en la red"). Cada una es un botón: "Sala de <host>", los jugadores, si
     están en la sala o jugando, y dónde está (esta PC o su IP). Apretarla conecta, sin escribir nada.
     Va por UDP en su propio puerto, el **7771** (`LanDiscovery`; "Puerto para buscar partidas" en
     `UI_ConnectPanel`), aparte del de la partida. La PC con la partida abierta la anuncia sola.
     · Si el host no cambió su nombre ("Jugador"), sale el nombre de su PC.
     · Una partida de otra versión del juego aparece, pero no se puede elegir.
     · Entrar desde la lista NO cambia la dirección guardada (no pisa la de playit).
     · Solo en la misma red: por internet sigue la dirección de playit, escrita a mano.
   - **El teclado en pantalla** (`UI_OnScreenKeyboard`, en `Menus_UI ▸ Keyboard`): con el control, A sobre la
     dirección o sobre tu nombre en la sala lo abre con lo que ya tenía. El stick recorre las teclas y A
     aprieta; X borra, Y pone un espacio, Start (o Listo) acepta y B cancela. Mayús vale para una letra (vacío
     arranca prendida, para la inicial). Cada tecla es un `UI_KeyboardKey`: para agregar una, se duplica otra
     y se le cambia la "Letra".
   - Los campos de texto ya no se ponen a escribir solos cuando el stick pasa por encima (antes, además, A
     sobre la dirección conectaba). Con el mouse, igual que siempre: clic y se escribe.
   - **Todos los botones de los menús: el elegido, el MISMO color más oscuro (26 de octubre, con Gustavo).**
     Antes el tinte de "elegido" multiplicaba al color de la imagen y el resultado dependía del color
     ("Ajustes" elegido, casi negro). Ahora el color de cada botón va en el Button ("Normal Color" y los demás
     estados) y su imagen queda blanca (`ButtonTint`). **Para cambiarle el color a un botón a mano: "Normal
     Color" del Button, no la imagen.** Desde código: `ButtonTint.Paint` (la sala ya lo usa para Confirmar,
     Empezar, las pestañas y los íconos de clase).
     · Primera versión: el elegido iba hacia el blanco. A Gustavo no le gustó ("no me gustan los colores
       pálidos": el naranja quedaba durazno). Ahora solo baja el BRILLO, con el mismo tono y la misma
       intensidad: el naranja pasa a un ámbar más hondo, el gris oscuro a casi negro. Cuánto: `SelectedDarken`
       y `PressedDarken` en `ButtonTint` (0.12 y 0.22 del brillo); un botón suelto se ajusta a mano en sus
       "Highlighted/Selected Color".
     **Antes de probar:** correr `Mercenarios ▸ Una vez ▸ Botones: el elegido más oscuro (showcase)` (solo los
     prefabs: el de menús y la tarjeta de clase). Recalcula los colores de todos desde su "Normal Color". (La
     primera, "el elegido más claro", ya se corrió y se borró.) **Corrida y borrada (26 de octubre).**
     - [ ] Los botones se ven igual que antes sin elegir; con el stick, el elegido se ve del mismo color pero
           más oscuro, nada pálido (en el menú, la conexión, la sala, los ajustes, la pausa, el teclado). Pasar
           el mouse hace lo mismo.
     - [ ] En la sala: Confirmar y Empezar cambian a verde al estar listos, las pestañas de rol se ven de su
           color (la abierta más fuerte) y los íconos de clase se apagan/encienden como antes.
     - [ ] Las tarjetas del menú de clases y de subir de nivel se ven como antes.
     - [ ] Un botón apagado (Confirmar sin clase) se ve oscuro y medio transparente, como antes.
   **Antes de probar:** correr `Mercenarios ▸ Una vez ▸ Lista de partidas y teclado en pantalla (showcase 2)`
   (solo el prefab: una vez alcanza). Arma la lista en `Connect` y el teclado (`Keyboard`). **Corrida y borrada (26 de octubre).**
   **Probar:**
   - [ ] Una ventana crea la partida; otra en la misma PC (una build) → Jugar: la partida aparece sola en la
         lista, con "esta PC". Apretarla entra a la sala.
   - [ ] Desde otra PC de la misma red aparece con la IP del host. Si no aparece: el firewall del host (si
         pregunta, "Permitir") o un Wi-Fi que aísla los equipos (ahí tampoco se podría jugar).
   - [ ] Al arrancar la partida dice "Jugando"; al cerrarla, desaparece de la lista en unos 4 segundos.
   - [ ] La dirección escrita a mano sigue andando y se recuerda; entrar desde la lista no la cambia.
   - [ ] Con control: A sobre la dirección abre el teclado con lo escrito. El A que lo abre no escribe nada.
         Escribir una IP con ":" y puerto; X borra, Y espacio; Start acepta (queda en el campo) y B cancela
         (queda como estaba). Al cerrarse, la selección vuelve al campo.
   - [ ] En la sala, A sobre tu nombre abre el teclado ("Tu nombre"); al aceptar, el nombre cambia para todos.
   - [ ] Con mouse y teclado: clic en el campo y se escribe como antes; Enter conecta.
   **Lo que encontró Gustavo al probar el teclado (28 de octubre):** "a veces no puedo pasarme de unas letras a otras"
   y "está muy pegado".
   · **Trabado: ARREGLADO.** La navegación automática de Unity elige entre TODOS los botones de la escena: desde
     algunas teclas saltaba a uno de la pantalla de atrás (la conexión, la sala), `MenuInput` devolvía la selección a
     la tecla y el movimiento se perdía. Ahora, al abrirse, cada tecla se enlaza con sus vecinas
     (`UI_OnScreenKeyboard.LinkKeys`, navegación Explicit): izquierda y derecha recorren la fila y de una punta pasan
     a la otra; arriba y abajo, la tecla más cercana de la fila de al lado. Sale de dónde están las teclas, así que
     se pueden seguir moviendo a mano.
   **Segunda vuelta (28 de octubre, noche, con captura) — HECHA Y PROBADA ✅:** "muy pegado a donde escribes", "la
   selección no es muy clara" y "en Ajustes no puedo bajar más allá de la sensibilidad con control".
   · **Muy pegado:** había 8 px entre teclas y la fila de los números se montaba 10 px sobre el recuadro de lo
     escrito (`Keys` arrancaba en −146 y el recuadro termina en −156). Lo arregla la herramienta de abajo: 16 px
     entre teclas y entre filas, la primera fila 24 px debajo del recuadro (Y −180), las filas de 880 de ancho y el
     panel de 940 × 590 (la ayuda baja sola). Después se ajusta a mano.
   · **Lo elegido, más claro: un MARCO** (`UI_SelectionFrame` + `UI_RectFrame`, en `Menus_UI ▸ SelectionFrame`): uno
     para TODOS los menús, blanco de 4 px a 4 px de lo elegido, que se desliza de un control al siguiente y late
     suave. Solo con el control (con el mouse no se ve). El color y el grosor, en el marco (`Frame`); el margen,
     los tiempos y el latido, en `SelectionFrame`. El "mismo color más oscuro" de los botones (ButtonTint) sigue.
   · **Ajustes: ARREGLADO, la misma causa del teclado.** Desde la barra de sensibilidad, el "abajo" de la
     automática era un botón del menú de atrás (el principal o la pausa) y `MenuInput` lo devolvía. Además, aun
     sin eso, se salteaba Invertir Y (su casilla está a la derecha). Ahora el panel se enlaza por filas al abrirse
     (`MenuNavigation.LinkRows`, lo mismo que el teclado, que pasó a usarla): Idioma → Control → Sensibilidad →
     Invertir Y → Pantalla → Resolución → VSync → Restablecer/Cerrar; en la barra, izquierda/derecha cambian el valor.
   · **Para las demás pantallas** (la sala, la pausa, la conexión), `MenuInput`: si la automática salta a un botón
     de la pantalla de atrás, la selección pasa al de la pantalla de arriba que mejor queda en esa dirección (la
     misma cuenta de Unity, solo entre los suyos); antes volvía a donde estaba y el stick se trababa.
   **Antes de probar:** correr `Mercenarios ▸ Una vez ▸ Teclado más separado y marco de lo elegido (control)` (solo
   el prefab de menús: una vez alcanza). **Corrida y borrada (28 de octubre).** Quedó: las teclas de 880 × 344 en Y −180,
   16 px entre teclas y filas, el panel de 940 × 590, y `SelectionFrame` (canvas en 500) con su marco blanco de 4 px.
   - [x] El teclado se ve separado y la fila de los números ya no toca el recuadro de lo escrito. **Probado ✅ (28 de octubre).**
   - [x] El stick pasa por todas las teclas sin trabarse, abierto desde la conexión y desde la sala; en la punta
         de una fila pasa a la otra punta. **Probado ✅ (28 de octubre).**
   - [x] Con el control, el marco blanco rodea lo elegido en todos los menús (inicio, conexión, sala, Ajustes, pausa,
         teclado) y se desliza al moverse; con el mouse no aparece. Que no tape nada importante. **Probado ✅ (28 de octubre).**
   - [x] Ajustes con el control, abierto desde el inicio y desde la pausa: baja por todas las filas hasta Cerrar,
         pasando por Invertir Y; en la barra, izquierda/derecha cambian la sensibilidad. **Probado ✅ (28 de octubre).**
   - [x] La sala y la pausa con el control: el stick no se traba en ningún botón. **Probado ✅ (28 de octubre).**
3. **Tres ventanas en una PC — HECHA, falta correr la herramienta y probar (28 de octubre).**
   Lo que se quería: en la PC del showcase juegan varias personas a la vez, cada una en su monitor con su
   ventana del juego (una build), cada ventana con SU control, o una con teclado y mouse. Antes, con dos
   ventanas en la misma PC, un mismo control movía las dos.
   **Decidido con Gustavo (28 de octubre):** el control se toma apretando A (no por número: el número cambia
   al desenchufar); en el showcase va una ventana de teclado y mouse y las demás con control; el espectador
   con control entra en esta etapa.
   **Cómo quedó:**
   - **`LocalSeat` (el puesto de la ventana)** lee los argumentos de la build: `-pad` (un control), `-kbm`
     (teclado y mouse), `-mute` (sin sonido); sin nada, todos los dispositivos, como antes. En el editor
     lee los tags de MPPM: `kbm`, `pad` (pide A), `pad0`/`pad1`/`pad2` (ese control por número) y `mute`
     (lo que antes hacía `PlayerInputProvider`, solo en el editor, pasó acá).
   - Con `-pad`, la ventana muestra **"Aprieta A en tu control"** (`UI_PadClaim`, en `Menus_UI ▸ PadClaim`)
     y se queda con el control que aprieta A mientras ELLA tiene el foco; las de atrás también ven esa A,
     pero no la toman. Después dice "¡Listo!" un momento. Si el control se desconecta, vuelve el cartel;
     si reconectan el mismo, lo retoma solo.
   - Las tres copias del asset de input (jugador, menús, espectador) se anotan en `LocalSeat` y escuchan
     solo los dispositivos de su ventana. Esperando la A, ninguno: la ventana no responde a nada.
   - Lo que leía dispositivos por fuera del asset ahora pregunta al puesto: las teclas 1/2/3 de los menús
     de clase y subclase, la rueda (en una ventana de control apunta siempre con el stick) y los atajos del
     giro y del bajar.
   - **El cursor:** una ventana de control nunca lo atrapa (`UICursor`); si no, al hacerle clic para tomar
     el control le robaba el mouse a la de teclado.
   - **Los botones que se dibujan** (`InputGlyphs`) siguen al puesto: la ventana de control muestra RB y la
     de teclado Q, aunque las tres compartan los Ajustes (son los mismos PlayerPrefs). La opción de control
     de Ajustes no cambia nada en una ventana con puesto.
   - **`-mute`:** `GameSettings` pone el volumen en 0 en esa ventana, diga lo que diga Ajustes.
   - **El espectador con control** (`SpectatorCamera`): ya no usa el input viejo, usa el mapa nuevo
     "Spectator" del asset (InputSystem_Actions): stick izquierdo para moverse, el derecho para mirar,
     RT/LT subir y bajar, L3 rápido, RB/LB siguiente y anterior jugador, Y cámara libre, X la info, View el
     marcador (con teclado, lo de siempre: WASD, Espacio/Ctrl, Shift, clics, F, H, M). Las teclas están en
     el asset, no en el inspector (se fueron los campos "Tecla: …"); la ayuda de abajo sale de
     `InputGlyphs`. Nuevo campo "Giro con control (°/s)", 120 como la cámara del jugador.
   - **`Showcase.bat`:** cada build de Windows lo deja al lado del .exe (`MercShowcaseBuildBat`, corre solo).
     Abre tres ventanas, una por monitor (`-monitor 1/2/3`): la 1 con `-kbm` (es la que suena) y la 2 y la 3
     con `-pad -mute`. Cada una deja su log al lado (`Showcase_1.log`, …).
   **Antes de probar:** correr `Mercenarios ▸ Una vez ▸ Cartel Aprieta A (showcase 3)` (solo el prefab de
   menús: una vez alcanza). **Corrida y borrada (28 de octubre).** Y hacer una build nueva (para el .bat y lo demás).
   **En el showcase, el orden:** abrir `Showcase.bat`; clic en la ventana 2 y A en su control; clic en la 3 y
   A en el otro; **al final, clic en la ventana 1** (la de teclado: el teclado solo le llega a la ventana que
   tiene el foco). La pantalla completa la ponen los Ajustes (son los mismos para las tres).
   **Probar (con 2 controles):**
   - [ ] En el editor, sin tags: todo como antes (teclado y control a la vez).
   - [ ] Las tres ventanas con el `.bat`: cada una en su monitor; la 2 y la 3 muestran "Aprieta A en tu
         control" (sin el foco, "Haz clic en esta ventana…"). Al apretar A: "¡Listo!" y se va.
   - [ ] Cada control mueve solo su ventana (menús, sala, teclado en pantalla y partida), aunque la ventana
         no tenga el foco. El teclado y el mouse, solo la 1. Ninguna ventana de control atrapa el mouse.
   - [ ] La ventana 1 crea la partida; la 2 y la 3 la eligen de la lista con su control y entran. Una
         partida entera.
   - [ ] El HUD de las ventanas de control muestra RB/LB/RT…; el de la 1, Q/E/R.
   - [ ] Solo suena la ventana 1.
   - [ ] Desconectar un control: su ventana vuelve a pedir A ("Se desconectó el control…"); al reconectarlo,
         lo retoma sola.
   - [ ] Entrar de espectador en una ventana de control: se mueve, mira, sube/baja y sigue jugadores con
         el control, y la ayuda muestra los botones del control. En la de teclado, lo de siempre.
   - [ ] En la rueda de una ventana de control, se apunta con el stick aunque el mouse ande por ahí.
   **Probar las habilidades con control (28 de octubre, pedido de Gustavo).** En el código ya andan (ninguna lee
   teclado o mouse directo: todo va por las acciones del asset), pero nadie lo jugó entero con control:
   - [x] La rueda de cada clase que la tenga: se abre con su botón, el stick elige, el centro cancela, al soltar
         sale lo elegido; mientras está abierta el personaje no camina. **Probado ✅ (28 de octubre).**
   **La rueda con el stick DERECHO (28 de octubre, pedido de Gustavo al probarla) — PROBADA ✅.** Antes se elegía con el
   izquierdo y el personaje no caminaba. Ahora se elige con el derecho (la acción Look; la cámara no gira mientras
   la rueda está abierta) y el izquierdo sigue moviendo al personaje, como el WASD mientras se elige con el mouse.
   Soltar el stick (el centro) sigue siendo cancelar. En una ventana con teclado y control (el editor sin tags) se
   apunta con el último que se movió. Y al cerrar la rueda con el stick todavía inclinado, la cámara no gira hasta
   que vuelve al centro (antes giraba de golpe hacia la opción elegida). `UI_RadialMenu.TrackAimDevice`,
   `ThirdPersonOrbitCam` (`_waitStickCenter`); se fue `PlayerInputProvider.MoveIsGamepad`.
   - [x] Con control: la rueda se elige con el stick derecho mientras el izquierdo camina; al soltar el botón
         sale la opción, y con el stick suelto, nada. Al cerrarla, la cámara no pega un giro. **Probado ✅ (28 de octubre).**
   - [x] Con mouse y teclado: la rueda como siempre (el mouse elige, WASD camina). **Probado ✅ (28 de octubre).**
   - [x] Una habilidad de mantener (canalizar o apuntar un lanzamiento) con su gatillo o botón. **Probado ✅ (29 de octubre, Gustavo: "se mueven bien").**
   - [x] Una que se pone en el piso (el Muro de fuego), girándola con la cruceta izquierda/derecha. **Probado ✅ (29 de octubre).**
   - [x] Cambiar de clase (cruceta arriba) y elegir subclase (View): el stick recorre las tarjetas y A confirma. **Probado ✅ (28 de octubre, en el editor).**
   - [ ] El HUD muestra los botones del control (RT, LT, RB, LB, Y, B).
4. **Documentos** (arquitectura de red, guía, CLAUDE.md, el LEEME del modo). Lo de las etapas 1 y 2 ya está en
   `Red_Arquitectura.docx`, el LEEME y CLAUDE.md (26 de octubre). La etapa 3 también (28 de octubre: CLAUDE.md, el LEEME
   y "Cómo probar" de `Red_Arquitectura.docx`).

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

# ★ Subclases del Mago — PLAN (22 de octubre; aún no se arman)

Decidido con Gustavo el 22 de octubre. Orden: **Evocación → Abjuración → Filo danzante**. Cada una
es su `Class_*` colgada de `Class_Wizard` (como los juramentos del Paladín), con E, R y pasiva extra,
y conserva la Vulnerabilidad arcana. Stats de nivel 3 (lo mismo que da la progresión del base:
+40 vida, +50 maná, +1 ataque y +1 daño mágico por nivel): 140 de vida, 200 de maná, 10 de ataque,
12 de daño mágico, 2 de defensa, velocidad 6 y el tiempo entre ataques del base (hoy 0.6 s). El
Filo danzante NO tiene más resistencia. Los visuales de cada subclase ("el libro brilla") son del
artista.

**Cambios al KIT BASE que salieron de esta charla (van en la etapa 1):**
- Muro de fuego, Levantamiento y Tormenta eléctrica **canalizan 2 s** (la Tumba no; la recarga sigue
  con sus 3 s). Ver pregunta abierta C.
- La Tormenta base **no tira rayos**: eso es de la Evocación (ver la corrección en "★ El Mago").
- La Vulnerabilidad arcana dura **15 s** (antes 6), para todos los Magos.
- El muro daña hasta 2 m de alto pero sus llamas son las de la zona del dragón (bajas): una copia
  más alta de las llamas para el muro.

## Evocación
| Ranura | Qué hace |
|---|---|
| E — Memoria perfecta | Tu PRÓXIMA Q (menos la recarga de maná) sale al instante (sin los 2 s de canalizar) y sin poner cooldown. La marca dura 10 s o hasta usarla; cooldown 15 s |
| Pasiva — Hechizos empoderados | Gran bola de fuego: la que cruza TU Muro de fuego crece: explosión ×1.5 de tamaño y de daño, quemadura +2 s (y se ve más grande). Mordedura de hielo: la Flecha helada se carga más (3 s en total) y la cargada aturde 1 s +0.5 s por punto arcano. Parte montañas: el Terremoto suelta 2 grietas más a ±30°; cada enemigo recibe un solo golpe por lanzamiento. Cadena de relámpagos: la Descarga que le pega a la Tormenta se frena ahí y el tornado tira un rayo a cada enemigo a su alcance (lo que hoy está en la base), y cada golpe la AGRANDA: +1 m de radio de daño y de alcance de los rayos, hasta +4 m |
| R — Sobrecarga de hechizos | 8 s: levita (un vuelo que el daño no corta), +30 % de resistencia, 3 Q al instante y sin cooldown (la marca de la Memoria con 3 usos), y maná ilimitado (el maná se llena cada 0.5 s, la idea de Gustavo) |

## Abjuración
| Ranura | Qué hace |
|---|---|
| E — Proyección arcana | Sobre un aliado (25 m). Dura PARA SIEMPRE hasta que muere el Mago, muere el aliado o se acaba su escudo (arranca con el 20 % de la vida máxima del Mago). Mientras dura, cada clic izquierdo del Mago sale además como copia desde el aliado, hacia donde apunta el Mago: sin costo y con el 50 % del daño |
| Pasiva — Protección arcana | Al hacer daño, el Mago y su aliado proyectado ganan escudo: el 25 % del daño hecho, con tope en la mitad de la vida máxima del Mago. El escudo se guarda y, si el Mago deja de hacer daño, a los 3 s empieza a bajar de a poco; cada vez que vuelve a cargar, la cuenta de 3 s vuelve a empezar |
| R — Protección máxima | 8 s: la proyección sobre TODOS los aliados a 25 m o menos, y las copias salen de todos |

## Filo danzante
| Ranura | Qué hace |
|---|---|
| Clic izquierdo | Cortes en arco corto con espada corta (la del Explorador), daño FÍSICO (los +1 por punto arcano también valen). Con un hechizo elegido en el libro (el clic derecho sigue siendo el libro), el golpe sale como CORTE AVANZANDO (con dash) y cuesta el 50 % del maná MÁXIMO; el corte GASTA el hechizo y vuelve a los cortes normales |
| El corte avanzando | Según el hechizo: fuego (quema / +daño propio), hielo (ralentiza / +velocidad), tierra (aturde / +resistencia al aturdido), rayo (−velocidad de ataque enemiga / +la propia). Escala con los puntos arcanos (ver F abajo); el beneficio propio tiene tope del 20 % aunque pegue a varios. Solo el corte avanzando aplica el elemento |
| E — Espada mágica | 8 s, cooldown 15 s: los cortes hacen daño MÁGICO |
| Pasiva — Danza defensiva | +3 % de resistencia por punto arcano en enemigos a 8 m o menos, tope 30 % |
| R — Baile final | Zona amplia apuntada (6 m): daño mágico, aplica los CUATRO perjuicios a los enemigos y se da los CUATRO beneficios, contando los puntos de los golpeados |

## Decidido después (22 de octubre, respuestas de Gustavo)
- A. Mordedura de hielo: **3 s en total** para cargarla del todo (1 s la flecha normal + 2 s más la que aturde).
- B. La Vulnerabilidad arcana pasa a durar **15 s para TODOS los Magos** (`GE_ArcanePoint`, va en la etapa 1).
- C. Los 2 s de canalizar de las Q: el punto queda fijo donde soltó, el marcador sigue a la vista mientras
  canaliza, se puede mover, y cortada cuenta como lanzada.
- D. Proyección: arranca con un escudo del **20 % de la vida máxima del Mago**; cooldown de la E desde que se
  lanza; relanzarla sobre otro aliado la mueve; las copias salen solo si el aliado está a 25 m o menos.
- E. Filo: el corte avanzando **GASTA el hechizo** y vuelve a los cortes normales (para que no salga el mismo
  corte dos veces seguidas).
- F. Fórmula de los elementos, como la propuesta: al enemigo 1 s por punto suyo (mínimo 1 s, 5 s con 5); la
  tierra aturde 0.5 s por punto (máximo 2.5 s); a uno mismo 4 % por punto sumando los de todos los golpeados
  (mínimo 5 %, tope 20 %), 5 s; fijos: el hielo ralentiza 30 % y el rayo quita 20 % de velocidad de ataque.
- G. Baile final: **cuenta los puntos de los enemigos golpeados**, igual que el corte avanzando.
- H. Gustavo le quitó el pararrayos a `Summon_LightningStorm` (verificado el 22 de octubre).

- E2. Los cortes normales del Filo **dejan puntos arcanos** (1 por golpe).
- E3. El corte avanzando gasta el hechizo y la Q del Filo vuelve a la recarga hasta elegir otro (opción A).

## Etapas
- [x] **Etapa 1 — Escuela de evocación + los cambios al kit base: HECHA Y PROBADA ✅ (22 de octubre).**
      Código nuevo (todo genérico):
      · `GA_TagSwitch` **Al instante y sin cooldown con tag** (`InstantTag`; "La de por defecto no lo usa"):
        con el tag, lo que sale sale sin canalizar, sin costo y sin cooldown, y gasta una acumulación
        (`GameplayAbility.ActivateFree`). Con la habilidad en cooldown NO sale (se lee literal: "sin
        PONER cooldown"); si se quiere que también se salte uno que ya corría, es un cambio chico.
      · Lo apuntado que canaliza (las Q del Mago): cae donde soltó aunque mire a otro lado o se mueva
        (`PlayerController.NetworkRequestPosition`, y la mira se guarda al empezar), y el marcador se
        queda quieto a la vista hasta que sale (o la cortan).
      · `GA_ChargedAttack` **Soltar antes: sale al llegar a la primera** (la Mordedura de hielo).
      · `GA_GroundFissure` **Grietas extra por lado** + ángulo (Parte montañas), una sola lista de golpeados.
      · `Summon_ProjectileBooster` (nuevo, `Scripts/GAS/Entities`) + `GA_ProjectileShoot` **Explosión
        potenciada** y **Tamaño potenciado**: el proyectil del MISMO dueño que cruza el muro crece (en
        todas las pantallas) y suelta la otra explosión (la Gran bola de fuego).
      · La zona de una invocación puede CRECER (`Entity_Summon.GrowZone`, viaja en un SyncVar; el VFX
        crece con ella) y el pararrayos tiene **Crece por rayo / Crece hasta** (la Cadena de relámpagos).
      · Tags nuevos al final: `Status_PerfectMemory`, `Status_SteadyFlight` (el daño no corta el vuelo).
      · Bots: el que pega de lejos usa los buffs de pelea a su distancia (la Memoria perfecta).
  - [x] (Corrida y borrada el 22 de octubre, por Claude con Unity en batch: Gustavo tenía el editor cerrado.)
        **Arreglado el 22 de octubre:** la herramienta agarró el `GA_Fireball` de los fantasmas (`48toPlay`) en vez
        del del Mago y `GA_FireballEvocation` salió sin canalizar; se rehízo a mano desde el del Mago (con su
        explosión potenciada). La Mordedura de hielo canaliza con la BARRA DE CARGA por etapas (1 s y 3 s), no
        con la barra de canalizar.
        **Arreglado el 22 de octubre (2):** en el HOST lo apuntado que canaliza salía hacia donde miraba al
        terminar (el host lee su cámara en vivo): ahora la mira queda FIJA mientras canaliza
        (`PlayerController.LockAim`). Y la Mordedura de hielo pasa a la cámara de apuntar mientras carga
        (`GA_ChargedAttack` "Cámara de apuntar al cargar"), como la flecha base.
        **Arreglado el 22 de octubre (3):** el Muro y la Tormenta ya no se limitan a uno: "Máximo a la vez" en 0
        (sin límite) en `GA_FireWall`, `GA_LightningStorm` y `GA_LightningStormEvocation` (pedido de Gustavo).
        La subclase se llama **School of Evocation / Escuela de evocación** (`Class_Evocation`). Íconos: los
        pone Gustavo. Hizo:
        · **Kit base:** el Muro, el Levantamiento y la Tormenta canalizan 2 s; `GE_ArcanePoint` dura
          15 s; `VFX_FireWallFlames` (⚙ la copia estirada de las llamas: el alto, en la escala Y de su
          hijo) en `Summon_FireWall`, que además lleva el `Summon_ProjectileBooster`.
        · **Evocación** (`GameplayAbilities/Wizard/Evocation`, `Class_Evocation` + `ASDef_Evocation`
          en `Attributes/Wizard`, colgada de `Class_Wizard`): `GA_PerfectMemory` (+ `GE_PerfectMemory`),
          `GA_SpellOverload` (un `GA_Flight` + `GE_SpellOverload`, `_Mana`, `_Memory`),
          `GA_WizardSpellsEvocation` con `GA_FireballEvocation` (+ `GA_FireballExplosionBoosted`,
          `GE_FireballDamageBoosted`, `GE_DragonBurnBoosted`), `GA_FrostBite` (`GA_ChargedAttack` con
          `GA_FrostBoltShot` y `GA_FrostBoltStun`) y `GA_EarthquakeEvocation`; `GA_WizardQEvocation` con
          `GA_LightningStormEvocation` y `Summon_LightningStormEvocation` (el pararrayos que crece).
          Íconos PROVISIONALES.
  - [x] **PROBADO ✅ (22 de octubre, en el host).** Probar, en el host y en un cliente:
        · **Kit base (cualquier Mago):** el Muro, el Levantamiento y la Tormenta canalizan 2 s (barra y
          pose), caen donde soltaste aunque mires a otro lado o te muevas, y el marcador se queda a la
          vista mientras; un aturdido lo corta (y va a cooldown). La Tumba sigue instantánea. Los
          puntos duran 15 s. Las llamas del muro se ven más altas (ajustar a ojo). La Tormenta base no
          tira rayos.
        · **Evocación** (subir al nivel 3 y elegirla): la E y después una Q de hechizo → sale al
          instante y sin cooldown (la recarga no la gasta). La Bola de fuego que cruza TU muro crece y
          explota más grande. La Flecha: 1 s la normal, 3 s la que aturde (barra de carga por etapas);
          soltada antes de 1 s, sale al llegar. El Terremoto saca 3 grietas. La Descarga contra la
          Tormenta de la Evocación tira rayos y la agranda (hasta +4 m). La R: levita 8 s, el daño no
          lo baja, +30 % de resistencia, maná siempre lleno y 3 Q instantáneas.
- [x] **Etapa 2 — Escuela de abjuración: HECHA Y PROBADA ✅ (22 de octubre).**
      Decidido con Gustavo el 22 de octubre: el escudo que BAJA es solo lo extra de la pasiva (hasta 0);
      el inicial de la proyección no baja solo, así que la proyección dura hasta que se gasta a golpes.
      Baja el 10 % del tope por segundo. La R proyecta a todo aliado que esté a 25 m en algún momento de
      sus 8 s, cada uno con su escudo inicial (una vez por R). Las copias son copias: dejan puntos y
      todo lo del clic (daño y quemadura al 50 %), y no cargan la definitiva.
      Elegido por Claude (⚙, se cambia en un campo): la E con cooldown de 15 s (como la Memoria
      perfecta); la clase con rol **Soporte**, y el escudo que les da a los aliados cuenta como "curar
      a un aliado" para la carga de la definitiva del rol. La R solo proyecta a jugadores y bots (no a
      tótems ni invocaciones).
      Código nuevo:
      · **Ecos** (genérico, `Scripts/GAS/AbilityEcho.cs`): un `IAbilityEchoSource` registrado en el ASC
        dice desde quiénes sale además lo que dispara el dueño; `GameplayAbility.FireEchoes` tira la
        copia (otra instancia: `IsEcho`, `DamageScale`, sin costo, cooldown ni carga de definitiva).
        Lo saben hacer `GA_ProjectileShoot` (también la ráfaga, la explosión y la zona que deja),
        `GA_HitscanShot` (y la Descarga, con sus rebotes) y `GA_GroundFissure`.
        `GameplayEffect.CreateDamageScaledCopy`: los efectos que bajan vida salen escalados.
        `GameplayAbility.CountsAsBasicAttack`: ella o el switch que la lanzó es el clic izquierdo.
      · `GA_Target`: gancho `OnTargetChosen` (+ `NeedsHitEffects`).
      · `Wizard/Abjuration`: `GA_ArcaneProjection` (la E, un `GA_Target`) y `ArcaneProtectionPassive`
        (las proyecciones, el escudo de la pasiva con su desgaste, la R y las copias).
      · Tags nuevos al final: `Status_ArcaneProjection`, `Status_MaximumProtection`.
  - [x] (Corrida por Gustavo y borrada el 22 de octubre.) Correr **Mercenarios ▸ Una vez ▸ Crear la Escuela de abjuración** (y después borrar
        `MercWizardAbjuration.cs`). Arma `GE_ArcaneProjection` y `GE_MaximumProtection` (Buffs, VFX
        provisionales), `GA_ArcaneProjection` y `GA_MaximumProtection` (`Wizard/Abjuration`),
        `AbjurationBehaviours` (copia de `WizardBehaviours` + la pasiva), `ASDef_Abjuration` y
        `Class_Abjuration` ("School of Abjuration" / "Escuela de abjuración"), colgada de `Class_Wizard`.
        Íconos PROVISIONALES (los de la Evocación).
  - [x] **PROBADO ✅ (22 de octubre).** Probar, en el host y en un cliente (subir al nivel 3 y elegirla; hace falta un aliado: un bot
        del equipo sirve):
        · **E:** apuntando a un aliado a 25 m o menos → le aparece el ícono y el VFX y 28 de escudo
          (con 140 de vida). Sin aliado en la mira no sale ni gasta. Relanzarla sobre otro la mueve
          (al primero se le va lo nuestro). Se acaba si el aliado se queda sin ese escudo, si muere él
          o si muere el Mago.
        · **Copias:** con el aliado a 25 m o menos, cada clic izquierdo sale además desde él hacia
          donde apuntás: los 3 misiles, la bola (con su explosión y quemadura), la flecha, las grietas
          del terremoto y la descarga (con sus rebotes), todo con la mitad del daño. Lejos (+25 m) no copia.
        · **Pasiva:** al pegar, el Mago y el proyectado ganan el 25 % del daño en escudo, hasta 70; a
          los 3 s sin pegar lo extra baja (7 por segundo) y el inicial del aliado se queda.
        · **R:** 8 s; todo aliado que pase a 25 m queda proyectado (ícono, escudo inicial, copias); al
          terminar se les va, menos al de la E.
- [x] **Etapa 3 — Filo danzante: HECHA Y PROBADA ✅ (23 de octubre).**
      Decidido con Gustavo el 23 de octubre: el corte avanzando es el dash del Pícaro (6 m hacia la mira)
      con 1 × el ataque físico a cada uno que cruza; NO gasta ni deja puntos; los mínimos valen si le
      pega a alguien (1 s al enemigo, la tierra 0.5 s; 5 % propio) y sin golpear a nadie no hay
      beneficio; sin el 50 % del maná sale el corte normal y el hechizo se QUEDA elegido. La R: zona
      de 6 m de radio donde apuntás (hasta 20 m), 2 × daño mágico.
      Elegido por Claude (⚙): la clase se llama "Bladesinging" / "Filo danzante", rol Daño (los bots la
      juegan cuerpo a cuerpo); los cortes alternan derecha/izquierda (el combo del Guerrero, 1 × ataque,
      2 m, 180°); la E con cooldown 15 s; el beneficio propio va de a 1 % (se acumula hasta 20, 5 s
      desde el último); los perjuicios son la quemadura de la Bola de fuego (GE_DragonBurn), −30 % de
      velocidad, el aturdido de siempre y −20 % de velocidad de ataque.
      Código nuevo (genérico):
      · Entradas de efectos: "Con los puntos del objetivo" tiene **Mínimo / máximo** (con mínimo se aplica
        aunque no tenga puntos) y un modo nuevo **"Veces: por punto"**; momento nuevo **"Al terminar de
        golpear"** (una vez, al lanzador, si golpeó a alguien, con los puntos de TODOS los golpeados
        sumados; lo soportan GA_Dash y GA_InstantAoE).
      · `GA_ConeAttack` y `GA_Dash`: **"Daño mágico con el tag (en el lanzador)"**: con ese tag, su daño
        escala del daño mágico (`GameplayEffect.CreateMagicCopy`).
      · `GA_TagSwitch`: **"Sin para pagarla: le quita el tag"** (apagado = el tag se queda).
      · Un costo que escala (el 50 % del maná MÁXIMO) ya se mide bien al ver si alcanza
        (`ASC.CanAffordGameplayEffect`).
      · `Wizard/Blade/DefensiveDancePassive` (la Danza defensiva). Tags nuevos al final:
        `Status_MagicBlade`, `Status_DefensiveDance`.
        **Arreglado el 23 de octubre:** los cortes avanzando no cobraban maná: el costo se cobraba con la
        HABILIDAD de fuente y un costo que escala (el 50 % del maná máximo) leía los stats de nadie (daba 0).
        Ahora los costos que escalan se cobran con el dueño de fuente (`GameplayAbility.CostSource`).
        **Arreglado el 23 de octubre (2):** la rueda del Filo usaba el libro del Mago (que mide el maná de la
        bola, la flecha...): con 45 de maná se veía disponible aunque el corte avanzando pide 100. Ahora el
        Filo tiene su libro, `GA_SpellBookBlade` (textos del Filo): cada elemento mide el maná del corte
        avanzando y, sin alcanzar, se VE apagado pero se puede elegir igual (para la Q). Opción nueva de
        `GA_RadialChoice`: "Se puede elegir aunque no alcance".
  - [x] (Corrida por Gustavo y borrada el 23 de octubre.) Correr **Mercenarios ▸ Una vez ▸ Crear el Filo danzante** (y después borrar `MercWizardBlade.cs`).
        Arma en `Wizard/Blade`: `GA_BladeSlash` + `GA_BladeCuts` (los cortes), `GA_BladeDashFire/Ice/Earth/
        Lightning` (el corte avanzando), `GA_BladeSpells` (el clic izquierdo), `GA_MagicBlade` (E),
        `GA_FinalDance` (R) y `BladeBehaviours`; los GE (`GE_Cost_BladeDash`, `GE_BladeChill`,
        `GE_BladeShock`, `GE_Blade*Boon`, `GE_MagicBlade`, `GE_DefensiveDance`, `GE_FinalDanceDamage`);
        `ASDef_Blade` y `Class_Blade`, colgada de `Class_Wizard`, con la espada corta del Explorador en la
        mano. Íconos y VFX PROVISIONALES.
  - [x] **PROBADO ✅ (23 de octubre).** Probar, en el host y en un cliente (subir al nivel 3 y elegirla):
        · **Cortes:** alternan derecha/izquierda al ritmo de ataque, dejan un punto arcano por golpe.
        · **Corte avanzando:** con un hechizo elegido y 100 de maná (de 200), el clic sale como dash de 6 m
          hacia la mira, le pega a todos los que cruza, cobra 100 y gasta el hechizo (la Q vuelve a la
          recarga). Con menos de 100 sale el corte normal y el hechizo sigue elegido. Al enemigo: fuego
          quema, hielo −30 % de velocidad, tierra aturde, rayo −20 % de velocidad de ataque, 1 s por punto
          (mínimo 1 s; tierra 0.5 s por punto, hasta 2.5 s). A uno mismo, si golpeó a alguien: el
          beneficio del elemento, 4 % por punto de todos los golpeados (de 5 % a 20 %), 5 s, con su número
          en la barra.
        · **E:** 8 s con el brillo; los cortes y el corte avanzando hacen daño mágico (se nota contra uno
          con armadura y contra un escudo).
        · **Pasiva:** con enemigos marcados a 8 m o menos, el buff de la Danza defensiva sube 3 % por punto
          (hasta 30 %) y baja al alejarse o al vencerse los puntos.
        · **R:** el marcador en el piso (6 m, hasta 20 m), 2 × daño mágico, los cuatro perjuicios por los
          puntos de cada uno y los cuatro beneficios por los puntos de todos.
- [x] **Documentos al día (23 de octubre):** las tres subclases y los cambios al kit base (Q de 2 s, puntos de 15 s, misiles de a 3 con +1 por punto) en Diseño de clases, la Guía (catálogo de genéricos y la 3.3 nueva), la Arquitectura del GAS (los ecos en la 4.7 nueva, "al terminar de golpear", las copias de efectos, el escudo a la vista, los tags) y la Red (el rol de los bots).

---

# ★ El Mago: kit base — ETAPAS 1, 2 Y 3 HECHAS Y PROBADAS ✅ (21 de octubre); la 4 (bots y documentos) TAMBIÉN, PROBADA ✅ (23 de octubre)

**Corrección del 22 de octubre (Gustavo):** los rayos de la Tormenta (la Descarga que le pega se frena
ahí y el tornado tira un rayo a cada enemigo) son EXCLUSIVOS de la Evocación, no del kit base. El
código queda (el pararrayos `Summon_LightningRod` y lo de `GA_ChainLightning`): pasa a la tormenta de
la Evocación. Falta quitar el `Summon_LightningRod` de `Summon_LightningStorm` (el de la base). Los
documentos ya están corregidos.

Decidido con Gustavo el 20 de octubre. Cambia cosas del documento de diseño (la Tumba helada
congela al MAGO, no a los demás; la Q no gasta maná; el Terremoto avanza por el piso; la
Descarga rebota): ya está en el documento de diseño de clases (21 de octubre). **Los visuales son
provisionales** hasta los del artista. Sin arma por ahora (el libro se borró en septiembre):
manos vacías, con los clips de magia de Kevin Iglesias (los del Haz guía).

**Maná:** 100. Se regenera solo, 10 por segundo. Los hechizos del clic izquierdo cuestan maná;
sin el suficiente no salen, y en cuanto no alcanza para el elegido, el clic vuelve SOLO a los
misiles (aunque tengas el botón apretado). La Q no gasta maná. La barra del HUD ya existe y ya
es azul (aparece sola en las clases con maná: el Mago es la primera).

**Puntos arcanos (pasiva — Vulnerabilidad arcana):** solo los dan los Misiles arcanos, 1 por
golpe (cada misil de la ráfaga es un golpe); tope 5; duran 15 s y se refrescan. Cada punto suma **+1 de daño FIJO** a cada golpe del
Mago contra ese objetivo (con 5, +5), no a los ticks de la quemadura (era +2 hasta el 22 de octubre: bajó al pasar los misiles a 3). Se ven como debuff en la
barra (con su número) y como **bolas azules que giran alrededor** del marcado soltando
estrellitas azules (una bola por punto).

| Ranura | Qué hace | Maná | Con los puntos del objetivo |
|---|---|---|---|
| Clic izq. — Misiles arcanos | RÁFAGA de 3 bolitas azules, 0.2 s entre cada una (esquivables), cada una hacia donde mires en ese momento; vuela hasta chocar; cada una 1/4 del daño mágico (`GE_ArcaneMissileDamage`) y 1 punto. El cooldown se cobra una vez y la carga de la definitiva se reparte entre las 3. Se repite al mantener | 0 | +1 por punto, en CADA misil |
| Clic der. — Libro de hechizos | Rueda: ARRIBA los misiles (lo normal) y los 4 hechizos. El elegido se queda hasta cambiarlo o hasta que no alcance el maná. Cambia el clic izquierdo y la Q | — | — |
| Clic izq. con Fuego — Bola de fuego | Canaliza 0.5 s. Explota en 3 m: daño + quemadura, y deja la zona del dragón del Monje tal cual (2.5 m, 5 s, `GE_DragonBurn` cada 1 s). No se repite al mantener | 25 | La quemadura dura +1 s por punto |
| Clic izq. con Hielo — Flecha helada | Canaliza 1 s; se mantiene y sale al soltar. Proyectil rápido como la flecha cargada del Explorador (70) que ralentiza. No se repite al mantener | 50 | La ralentización dura +0.5 s por punto |
| Clic izq. con Tierra — Terremoto | Una grieta que AVANZA por el piso desde el Mago (se ve venir y se esquiva) siguiendo el terreno: si está arriba de algo, baja por el borde. Pega solo a lo pegado al piso (caja de 0.5 m de alto, ajustable): el que salta alto la esquiva. Solo daño. Lanzado VOLANDO: se corta el vuelo, cae en picada y la grieta sale al tocar el piso (como el terremoto de Spellbreak). Se repite al mantener | 20 | Aturde 0.5 s por punto y GASTA los puntos de ese enemigo (si no, manteniendo el botón quedaba aturdido sin parar) |
| Clic izq. con Rayo — Descarga eléctrica | Hitscan, con un rayito azul que dibuja la trayectoria al instante. Se repite al mantener | 10 | Rebota (ver abajo) |
| Q sin hechizo — Recargar maná | Canaliza 3 s y recarga el 100 % de a poco (+33 por segundo, como el Segundo aliento del Guerrero pero con maná); cortada, se queda con lo ganado. Cooldown 10 s, completa o cortada | — | — |
| Q con Fuego — Muro de fuego | Mantener Q muestra el muro en el piso y la RUEDA DEL MOUSE lo gira (15° por paso; con control, la cruceta izquierda y derecha). 5 zonas CUADRADAS de 2 m en fila de lado a lado (10 m), con daño hasta 2 m de alto; quema como la zona del dragón. Dura 5 s. Cooldown 8 s | 0 | (los +1 de la pasiva) |
| Q con Hielo — Tumba helada | Se congela a SÍ MISMO (el bloque de hielo de WoW / la crio-congelación de Mei): 3 s inmune a daño y a control, sin moverse ni atacar; apretar Q otra vez sale antes; CURA el 40 % de la vida máxima de a poco (8 % cada 0.5 s; pedido de Gustavo tras probarla, como Mei; si sale antes, se queda con lo que llevaba). Un cubo de hielo traslúcido lo encierra. Cooldown 20 s | 0 | — |
| Q con Tierra — Levantamiento | Área de 3 m donde apunta: daño y los lanza hacia arriba. Cooldown 8 s | 0 | (los +1 de la pasiva) |
| Q con Rayo — Tormenta eléctrica | Un tornado AZUL (copia de `Art/VFX/Tornado.prefab`) fijo donde apunta: 6 s, daño cada 1 s en 4 m. Cooldown 8 s. **Corrección del 22 de octubre (Gustavo): que la Descarga le pegue y el tornado tire rayos es EXCLUSIVO de la Evocación** (Cadena de relámpagos); la base solo daña | 0 | — |
| Shift — Vuelo | `GA_Flight` + `GE_Fly` (hecho y probado el 6 de octubre) | — | — |

**El rebote de la Descarga es un grafo:** el golpeado salta a tantos enemigos como puntos tenga, y
cada uno de esos vuelve a saltar según los suyos (A con 2 → B y C; B con 1 → D). Pesan primero
los PUNTOS y, entre empatados, el MÁS CERCANO; a 4 m o menos y VISIBLE desde el anterior (sin
pared en medio, como si apuntara y disparara). Cada enemigo recibe un solo rayo por ataque, con
el daño completo. Con 0 puntos no rebota. Opción en el Inspector **Ramas / Cadena** para probar
también la cadena simple (A → B → C); la recomendada es Ramas.

**Lo que ya hay y se reusa:** `GA_ProjectileShoot` (misiles, bola y flecha; vuela hasta chocar como
el arco de luz del Clérigo), `GA_HitscanShot` (la descarga), `GA_TagSwitch` (el clic izquierdo y la
Q cambian según el hechizo; ya pasa el marcador del piso de la variante), `GA_InstantAoE`
(Levantamiento), `GA_ContinuousAoE` (la zona del dragón, el daño de la tormenta), Canalizar (hielo,
fuego, recarga), "Repetir ataque al mantener", los efectos de un mismo grupo que se reemplazan (el
hechizo elegido, como las posturas del Guerrero), las acumulaciones de un GE (los puntos), el "VFX
en el objetivo" de un GE (orbes, cubo), `GE_DragonBurn`, `GE_Slow`, el empuje con "Impulso hacia
arriba", `Status_Immunity` + `Status_Unstoppable` (los de la Muerte silenciosa), `GA_Flight`, el
atributo Maná con su red y su barra, y `GE_EnergyRegen` como molde de la regeneración.

**Lo nuevo (código):**
1. **La variante cobra y canaliza lo suyo** (`GA_TagSwitch`): hoy les quita el costo, y canalizar
   solo funciona en la habilidad de la ranura. Toca la red. Con eso: la vuelta sola a los misiles
   sin maná, y que las que canalizan no se repitan al mantener.
2. **Rueda genérica**: cada opción aplica un GE (el hechizo elegido). Le sirve después al Filo
   danzante (sus espadas).
3. **Pasiva Vulnerabilidad arcana**: +2 fijos por punto del objetivo (`IDamageModifier`) y los
   orbes que giran según los puntos.
4. **Escalar con los puntos del OBJETIVO**: duración por punto (fuego, hielo) y aturdido por punto
   que gasta los puntos (terremoto). Las acumulaciones de hoy cuentan las del LANZADOR.
5. **Explotar al chocar**: el proyectil suelta un área y una zona donde pega.
6. **El Terremoto**: la grieta que avanza pegada al piso, baja por los bordes, con su caja de 0.5 m;
   y cortar el vuelo con la caída en picada.
7. **La Descarga**: el grafo de rebotes con línea de vista, y el rayito de cada salto en todas
   las pantallas.
8. **La Tormenta**: que la Descarga le pegue al tornado (colisión propia) y dispare los rayos.
9. **El Muro**: zona cuadrada con alto, y el marcador del muro que gira con la rueda del mouse (el
   marcador de hoy es un círculo armado por código: pasarlo a prefab de paso).
10. **Tumba helada**: congelarse a sí mismo y salir antes con Q.

Los visuales provisionales (bolita, orbes con estrellas, rayito, grieta, cubo de hielo, tornado
azul) los crea una herramienta de una vez, como prefabs.

**Por etapas, probando entre una y otra:**
- [x] **Etapa 1 — la clase: HECHA Y PROBADA ✅ (20-21 de octubre).** El despegue del vuelo va ahora HACIA LA MIRA (pedido de Gustavo, 21 de octubre): `GA_Flight` tiene "Despegue" (hacia arriba / hacia donde mira) y, desde el piso, un ángulo mínimo hacia arriba (20°); en el aire sale exacto a la mira (en picada si mira abajo). Despegue probado ✅ (21 de octubre).
      Stats de Gustavo en `ASDef_Wizard`: 60 de vida (+40 por nivel), 100 de maná (+50 por nivel),
      2 de defensa, 8 de ataque, 10 de daño mágico, velocidad 6, 0.8 s entre ataques; por nivel
      también +1 de ataque y +1 de daño mágico, como el Clérigo. Código nuevo: el tag
      `Status_ArcanePoint` (al final de la lista), `ArcaneVulnerabilityPassive` (+2 fijos por punto
      del objetivo), `ArcaneOrbsVisual` (una bola por punto, lee las acumulaciones que ya viajan
      por red) y, para CUALQUIER `GA_ProjectileShoot`, la casilla **Se deshace en el primer
      enemigo** (hasta hoy todos los proyectiles atravesaban; los demás siguen igual).
  - [x] (Corrida y borrada el 20 de octubre.) Correr `Mercenarios ▸ Una vez ▸ Crear el Mago (etapa 1)` y borrar
        `Assets/Scripts/Editor/MercWizardStage1.cs`. Crea `Class_Wizard` y `ASDef_Wizard`
        (Attributes/Wizard), `GA_ArcaneMissiles` y `WizardBehaviours` (GameplayAbilities/Wizard),
        `GE_ArcanePoint` (Debuffs) y `GE_ManaRegen` (Buffs), `PF_ArcaneMissile` (copia del orbe del
        Monje, en azul), el material azul y `VFX_ArcaneOrbs` (Art/VFX/Wizard) y `AOC_Wizard`
        (la pose de lanzar hechizos). Agrega el Mago al FINAL de las clases del jugador y de la
        sala, y actualiza los registros de red. El Shift es el `GA_Flight` de siempre. Íconos
        PROVISIONALES (el de la Flecha mágica del Clérigo).
  - [x] **PROBADO ✅ (20 de octubre; el kit entero quedó probado el 21).** Probar, en el host y en un cliente: elegir el Mago en la sala; la barra de maná aparece
        (azul) y llena (todavía nada gasta maná); los misiles salen al ritmo de 0.8 s (y al mantener), vuelan
        derecho hasta lejos y se deshacen en el primer enemigo; cada misil deja un punto (el
        debuff con su número, hasta 5) y una bola más girando alrededor del enemigo, en las dos
        pantallas; con puntos, el número de daño sube 2 por punto; a los 6 s sin misiles se van
        todos; el vuelo; subir de nivel (vida y maná). Ajustar la "Espera al lanzar" de
        `GA_ArcaneMissiles` si la bolita no sale de la mano.
- [x] **Etapa 2 — el libro: HECHA Y PROBADA ✅ (21 de octubre)**, en dos partes:
  - [x] **2a — el libro, fuego y hielo: HECHA Y PROBADA ✅ (21 de octubre).**
        Código nuevo, todo genérico:
        · `GA_RadialChoice` (Create ▸ GAS ▸ Generics ▸ Radial Choice): una rueda donde cada opción deja
          PUESTO un estado (un GE con su tag); una opción sin efecto vuelve a lo normal; "Disponible si
          alcanza para" la apaga sin maná. Le va a servir al Filo danzante.
        · `GA_TagSwitch` con **Las variantes cobran y canalizan lo suyo** (cada variante paga SU costo y
          canaliza con SU sección Canalizar; el ritmo sigue siendo del switch) y **Sin para pagarla:
          vuelve a la de por defecto** (sin maná, el clic sale con los misiles y se quita el tag del
          hechizo). Por dentro: `GameplayAbility.CastSettingsSource` (de dónde salen los datos de
          canalizar) y `PayCutCast` (cortada, también se cobra el maná).
        · Lo que canaliza no se repite al mantener el clic izquierdo.
        · `GA_ProjectileShoot` "Al chocar": **Explosión** (un `GA_InstantAoE` tal cual, con
          `DetonateAt`) y **Zona que deja** (un `GA_ContinuousAoE` tal cual).
        · Cada entrada de efectos: **Con los puntos del objetivo** (cuenta un tag en el golpeado:
          duración por punto, o + duración por punto, y si los gasta). Tags nuevos: `Status_SpellSelected`,
          `Status_SpellFire`, `_Ice`, `_Earth`, `_Lightning` (al final de la lista).
  - [x] (Corrida y borrada el 21 de octubre.) Correr `Mercenarios ▸ Una vez ▸ Crear el Mago (etapa 2a: libro, fuego y hielo)` y borrar
        `Assets/Scripts/Editor/MercWizardStage2a.cs`. Crea `GA_SpellBook` (clic derecho),
        `GA_WizardSpells` (clic izquierdo: misiles o el hechizo), `GA_Fireball` + `GA_FireballExplosion`,
        `GA_FrostBolt`, los GE (`GE_Spell_Fire`/`_Ice`, `GE_Cost_Fireball` 25 y `GE_Cost_FrostBolt` 50,
        ⚙ `GE_FireballDamage` 1.5 × daño mágico, ⚙ `GE_FrostBoltDamage` 2 ×, ⚙ `GE_FrostSlow` mitad
        de velocidad 2 s) y los proyectiles `PF_WizardFireball` y `PF_FrostBolt`; pone el libro y el
        clic izquierdo en `Class_Wizard` y actualiza los registros. ⚙ = número provisional, ajustarlo.
  - [x] **PROBADO ✅ (21 de octubre).** Probar, en el host y en un cliente (21 de octubre: la Flecha helada ahora se MANTIENE, pedido de Gustavo: el clic izquierdo pasó a `GA_HoldTagSwitch`, cada hechizo lleva el ritmo de ataque): la rueda (arriba los misiles; fuego y hielo apagados sin
        maná); el ícono del clic izquierdo cambia con el hechizo; la Bola de fuego canaliza 0.5 s (barra
        y pose), cobra 25, explota en el enemigo y en una pared, quema más con puntos y deja la zona de
        fuego; la Flecha helada: mantener canaliza 1 s, se puede seguir manteniendo y apuntando, sale al soltar (soltar antes, sale apenas se llena), cobra 50 al soltar y ralentiza más con puntos; cortarlas a mitad
        (aturdido) cobra igual y no sale; mantener el clic no las repite (los misiles sí); sin maná
        vuelve solo a los misiles (con la Flecha: 60 de maná, se lanza una y vuelve).
  - [x] **2b — el Terremoto y la Descarga eléctrica: HECHA Y PROBADA ✅ (21 de octubre).**
        Código nuevo:
        · `GA_GroundFissure` (genérico, Create ▸ GAS ▸ Generics ▸ Ground Fissure): una grieta que AVANZA
          por el piso (Largo, Ancho, Alto: pega solo hasta esa altura; Velocidad; sube escalones de
          "Escalón que sube" y baja por bordes de hasta "Baja hasta"; una pared o el vacío la cortan),
          con sus VFX "en el impacto" cada "VFX cada" metros. Volando: corta el vuelo, el dueño cae en
          PICADA (`PlayerController.StartDive`, por `NetworkASC.ServerStartDive`) y la grieta sale
          cuando el servidor lo ve en el piso (o a los "Espera máxima" s).
        · `GA_ChainLightning` (sobre `GA_HitscanShot`, que ahora avisa dónde pegó: `OnShotResolved`):
          el grafo de rebotes (Ramas o Cadena; más puntos primero, después el más cercano; Salto 4 m;
          visible desde el anterior; un rayo por enemigo por ataque). `ChainFrom` es público: lo va a
          usar la Tormenta.
        · Rayos en red: `GameplayAbility.BroadcastBeams` / `PlayBeams` y `NetworkASC.ServerPlayBeams`
          (viajan solo los puntos de cada tramo); `VFX_LightningBeam` dibuja la línea quebrada.
  - [x] (Corrida y borrada el 21 de octubre.) Correr `Mercenarios ▸ Una vez ▸ Crear el Mago (etapa 2b: terremoto y descarga)` y borrar
        `Assets/Scripts/Editor/MercWizardStage2b.cs`. Crea `GA_Earthquake` (20 de maná; daño ⚙ 1.2 × daño
        mágico y `GE_Stun` 0.5 s por punto que los GASTA) y `GA_ChainLightning` (10 de maná; daño ⚙
        0.8 ×), `VFX_LightningBeam`, `GE_Spell_Earth`/`_Lightning` y sus costos, y los suma al clic
        izquierdo y al libro. Actualiza los registros.
  - [x] **PROBADO ✅ (21 de octubre).** Probar, en el host y en un cliente: el libro con los 5 (arriba los misiles); el Terremoto avanza
        (se ve venir), pega a los que están en el piso y NO al que salta, sube escalones, baja de una
        plataforma, se corta en una pared, aturde según los puntos y los gasta (las bolas se van);
        lanzado volando: corta el vuelo, cae en picada y sale al tocar el piso; la Descarga: el rayito
        en las dos pantallas (pegue o no), rebota según los puntos (A con 2 → B y C), no cruza paredes,
        cada enemigo una vez; mantener el clic repite los dos; sin maná, a los misiles.
- [x] **Etapa 3 — la Q: HECHA Y PROBADA ✅ (21 de octubre)**, en dos partes. Decidido con Gustavo el 21 de octubre: el **cooldown de la Q es
      COMPARTIDO** (cada hechizo pone el mismo GE con su duración: cambiar de hechizo no deja encadenar
      Q's); la recarga la cortan el aturdido, el silencio o usar otra habilidad (moverse y el daño no);
      la Tumba lanzada volando corta el vuelo y cae como bloque; la Descarga se FRENA en el tornado.
  - [x] **3a — recarga, tumba y levantamiento: HECHA Y PROBADA ✅ (21 de octubre).** Después de probarla, pedido de Gustavo: la Tumba CURA el 40 % de la vida máxima (`GE_IceTomb`: 8 % cada 0.5 s) y la Flecha helada vuela a 70 (antes 25; como la flecha cargada del Explorador). Falta probar esos dos.
        Código nuevo:
        · Canalizar **continuo** a través de un switch: la variante se repite como repetición
          (`GameplayAbility.ActivateAsCastTick`; la primera cobra, las demás gratis, sin animar ni
          avisar el fin). Y en un continuo, los efectos "al activarse" se repiten en cada pulso
          (antes solo en el primero; ningún asset usaba el continuo todavía).
        · `GA_SelfBuff` **Apretar otra vez la termina (tag)**: con ese tag puesto, apretar no la lanza,
          le saca los efectos que lo dan (aunque esté en cooldown). Salir antes de la Tumba.
        · Un **BUFF** que da un tag de control (enraizado, aturdido) ya no cuenta como control: la
          resistencia al control no lo recorta (se lo pone uno mismo).
        · `GA_HoldTagSwitch`: lo que la variante arrancó o cobró cuenta como del switch. Antes una
          variante que NO es de mantener no se animaba en las otras pantallas (los misiles del Mago,
          la Defensa paciente del Monje): ahora sí. Probarlo.
        · Tag nuevo `Status_IceTomb` (al final de la lista) e `IceTombVisual` (congela la pose y el
          cubo no gira con el personaje).
        · El impulso hacia arriba de un empuje (`KnockbackUpVelocity`) ahora levanta también a los
          NPCs y a los bots (antes solo a los jugadores: a ellos se los empujaba de lado y nada más).
          Con NavMeshAgent sube el cuerpo por su `baseOffset` (`NetworkASC.ServerLaunch`). Vale
          también para la trampa de empuje y la Patada de viento, que ya traían impulso.
  - [x] (Corrida y borrada el 21 de octubre.) Correr `Mercenarios ▸ Una vez ▸ Crear el Mago (etapa 3a: la Q — recarga, tumba y levantamiento)` y
        borrar `Assets/Scripts/Editor/MercWizardStage3a.cs`. Crea `GA_WizardQ` (la Q: un
        `GA_HoldTagSwitch`), `GA_ManaRecharge` (+ `GE_ManaRecharge`: 1/6 del maná máximo cada 0.5 s),
        `GA_IceTomb` (+ `GE_IceTomb`: 3 s inmune, imparable, enraizado y `Status_IceTomb`, y cura; `VFX_IceTomb`
        y `M_IceTomb` en Art/VFX/Wizard), `GA_Uplift` (+ ⚙ `GE_UpliftDamage`: 1 × daño mágico e impulso
        hacia arriba de 8 m/s); pone `Status_IceTomb` en lo que bloquea al clic izquierdo, al libro y al
        vuelo; pone la Q en `Class_Wizard` y actualiza los registros. Cooldowns: recarga 10 s, tumba
        20 s, levantamiento 8 s (todos con `GE_Cooldown_Special`). Íconos PROVISIONALES. Hasta la 3b,
        con fuego o rayo la Q también recarga.
  - [x] **PROBADO ✅ (21 de octubre).** Probar, en el host y en un cliente: **sin hechizo**, la Q canaliza 3 s (barra y pose) y el maná
        sube de a poco hasta llenarse; moverse no la corta; un clic izquierdo, el vuelo o un aturdido
        sí (se queda con lo ganado y la Q va a cooldown 10 s). **Con hielo**: el cubo de hielo lo
        encierra con la pose congelada (en las dos pantallas), no se mueve, no ataca, no abre el libro,
        no vuela, no recibe daño ni control (y se le van los debuffs); a los 3 s sale solo; apretar la
        Q adentro sale antes; volando, cae con el cubo. **Con tierra**: mantener Q muestra el círculo de
        3 m, soltar lo lanza: daño y los levanta, también a los NPCs y a los bots (⚙ ajustar el impulso en `GE_UpliftDamage`). **El
        cooldown compartido**: usar una y cambiar de hechizo no deja usar otra. En la otra pantalla el
        Mago anima al lanzar los misiles y el levantamiento.
  - [x] **3b — muro y tormenta: HECHA Y PROBADA ✅ (21 de octubre).**
        Código nuevo:
        · `Summon_Zone` tiene **Forma**: Círculo (el radio, lo de siempre) o **Caja** (ancho de lado a
          lado × alto × largo, apoyada en el piso y girada con la invocación; el que salta más alto no
          la toca). Con caja, las llamas van como hijos del prefab.
        · `GA_SpawnSummon`: si la zona del prefab es una caja, el marcador es el **muro** (atravesado a
          la mira, en "Casillas") y con **Giro con la rueda (°)** se gira mientras se apunta. Interfaz
          nueva `IWallTargetAbility` (el switch la pasa a su variante).
        · **El giro viaja al servidor** con el pedido (`ServerRequestActivateAbility` tiene un
          parámetro más, `placementYaw`; lo lee `PlayerController.NetworkPlacementYaw`).
        · **Input**: acciones nuevas `RotatePlacementLeft`/`Right` en `InputSystem_Actions` (rueda abajo /
          arriba; con control, cruceta izquierda / derecha). Si faltaran, `PlayerInputProvider` lee la
          rueda y la cruceta directo.
        · **El marcador del piso pasó al prefab de la cámara** (`UI_GroundTargetIndicator`): ya no arma
          nada por código. Sus piezas (zona, franja, el MOLDE de la casilla del muro, el material del
          contorno) se editan en `Player Camera ▸ GroundTargetIndicator`; colores y grosores en cada
          pieza. Al jugar se suelta de la cámara (si no, temblaba al girarla).
        · `Summon_LightningRod` (**pararrayos**, `Scripts/GAS/Entities`): un cilindro sin collider (radio
          × alto) que solo ven los rayos. La Descarga (`GA_ChainLightning`) que cruza el de su dueño o un
          aliado se frena ahí y el tornado tira un rayo a cada enemigo a "Alcance de sus rayos" que vea,
          del más cercano al más lejano; cada uno rebota con `ChainFrom`. Tirada desde adentro del
          tornado no cuenta. `GA_HitscanShot` tiene el gancho `StopDistance`.
  - [x] (Corrida y borrada el 21 de octubre.) Correr `Mercenarios ▸ Una vez ▸ Crear el Mago (etapa 3b: muro de fuego y tormenta)` y borrar
        `Assets/Scripts/Editor/MercWizardStage3b.cs`. Pone el marcador del piso en el prefab de la
        cámara (mallas y materiales en `Art/UI/GroundTarget`), crea `GA_FireWall` + `Summon_FireWall`
        (caja de 10 × 2 × 2 m, `GE_DragonBurn` cada 1 s, 5 s, una llama `VFX_FireZone` por casilla),
        `GA_LightningStorm` + `Summon_LightningStorm` (el tornado `VFX_LightningTornado` en azul, ⚙
        `GE_StormDamage` 0.5 × daño mágico cada 1 s en 4 m, 6 s, pararrayos de 1.5 × 6 m y rayos a 8 m),
        las suma a la Q y actualiza los registros. Cooldowns 8 s (compartido). Íconos PROVISIONALES.
        Si no aparece en el menú (como pasó con la 3a), clic derecho en
        `Assets/Scripts/GAS/Entities/Summon_LightningRod.cs` ▸ Reimport.
  - [x] **PROBADO ✅ (21 de octubre).** Probar, en el host y en un cliente: **las otras zonas apuntadas siguen viéndose** (el círculo
        del Levantamiento, Marcado para morir, la franja del Corte final con su contorno) y no tiemblan
        al girar la cámara. **Con fuego**: mantener Q muestra las 5 casillas atravesadas a la mira,
        apoyadas en el piso; la rueda (y la cruceta) las gira de a 15°; soltar pone el muro con ese
        giro (en las dos pantallas); quema a los enemigos que lo pisan cada 1 s (la quemadura del
        dragón), no al que salta alto, y dura 5 s. **Con rayo**: el tornado azul donde apunta (⚙ su
        tamaño: `VfxScale` de su zona; lo que recibe el rayo: el pararrayos), daño cada 1 s en 4 m;
        con la Descarga pegándole al tornado, el rayo termina en él y salen rayos a cada enemigo a
        8 m (no a través de paredes), cada uno rebota según sus puntos; cada Descarga lo dispara otra
        vez. Si una invocación no aparece en el cliente, revisar que FishNet la haya sumado a
        `DefaultPrefabObjects`.
- [x] **Etapa 4 — bots y documentos: HECHA (21 de octubre) Y PROBADA ✅ (23 de octubre).**
      Bots (`BotController`):
      · Rol nuevo **A distancia** (`EBotRole.Ranged`, al final del enum): un Daño cuyo básico pega de
        lejos —el Mago (el default de su clic izquierdo son los misiles) y TAMBIÉN EL EXPLORADOR (su arco)—
        se queda a 11 m rodeando, se aleja si se le acercan, se acerca si no lo ve, tira el básico hasta
        25 m solo si lo ve, y herido se va a curar como el Pícaro. El rol de una clase sin subclases sale
        de su rol de los menús (antes el Mago caía en Bárbaro y peleaba cuerpo a cuerpo).
      · **El libro** (`GA_RadialChoice`): en apuros elige el hielo si la Q está lista (para la Tumba),
        con poco maná vuelve a los misiles, y si no cambia a otro hechizo al azar cada ~8 s (usa todo
        el libro y todas las Q).
      · **La Q** se juzga por la variante que saldría (solo los switches con "Las variantes cobran y
        canalizan lo suyo"; las posturas y el Ki siguen como antes): Recarga con poco maná y nadie
        encima; Tumba en apuros (y no la vuelve a apretar adentro); Levantamiento, Muro y Tormenta
        sobre el que pelea si está a tiro y a la vista (el muro sale atravesado a la línea bot-enemigo).
      · Mientras canaliza algo propio (la Bola de fuego, la recarga) no pide otra cosa: la cortaría.
      · **Aturdido o enraizado ya no camina** (antes el agente seguía andando con un aturdido encima).
      · El Vuelo no lo usan (se mueve por el dueño).
      Documentos: el Mago entero en `Mercenaries_Diseno_de_Clases` (kit base, estado, bots, vuelo); en
      la guía, el catálogo al día (`GA_RadialChoice`, `GA_GroundFissure`, y lo nuevo de proyectiles, hitscan,
      switches, `GA_SelfBuff`, `GA_SpawnSummon` y `GA_Flight`), "3.2 Un kit entero con genéricos: el Mago",
      los puntos del objetivo, canalizar en un switch, el muro y el marcador del piso en la cámara; en
      la arquitectura del GAS y la de red, lo técnico (canalizar por un switch, buffs que no son control,
      la caja de la zona y el pararrayos, los tags del Mago, `IWallTargetAbility`, `placementYaw`,
      `ServerLaunch`, la picada y los rayos en red).
  - [x] Probado (23 de octubre): un bot Mago se queda a distancia y tira misiles; cambia de hechizo en el libro cada
        tanto (y usa la Bola de fuego, la Flecha, el Terremoto y la Descarga); usa la Q de cada hechizo
        (muro y tormenta sobre el enemigo, levantamiento, recarga con poco maná); con poca vida y alguien
        cerca elige el hielo y se encierra en la Tumba (y espera adentro); sin maná vuelve a los misiles.
        El Explorador bot ahora también pelea a distancia con el arco: revisar que se vea bien. Un bot
        aturdido (o en la Tumba) se queda quieto.

---

# ★ Base de las invocaciones (Entity_Summon + Summon_Zone) — HECHA Y PROBADA ✅ (19-20 de octubre: todas las invocaciones funcionan)

Lo de abajo ("Canalizar, choque de los proyectiles...") lo probó Gustavo: funciona.

**Las dos piezas** (`Scripts/GAS/Entities`):
- **`Entity_Summon`**: lo que tiene toda invocación, en el Inspector: **Dura** (0 = hasta que la quiten),
  **Máximo por dueño** (la más vieja se va; quien la invoca puede pedir otro número), **Grupo del límite**,
  **Si el dueño muere** (se queda / desaparece; si se va del juego o cambia de clase, desaparece siempre),
  **La ven** (todos / solo su equipo), **Modelo**, **Recibe zonas**. Por dentro: el equipo del dueño en red,
  una sola regla de enemigo/aliado (la del ASC), su propio ASC opcional (con la vida a 0 desaparece; puede ser
  autor de efectos), y `Entity_Summon.Spawn(...)` para crearla. Lo propio de cada una, en una subclase.
- **`Summon_Zone`**: "a los que estén en el radio, cada tanto, estos efectos". **A quién** (aliados / enemigos),
  **Solo jugadores**, **Autor** (el dueño / la invocación), **Potenciada** (otros efectos si el dueño tiene un
  tag), **Empieza prendida** o la prende la invocación, **Dura**, y su **VFX** del tamaño del radio en todas las
  pantallas. Una invocación + una zona, sin código, ya es un muro de fuego o un aura.

**El tótem, ya pasado:** `Entity_Totem` ahora es una invocación sin nada propio; el aura es su zona.
`GA_SpawnTotem` tiene **Máximo a la vez** (2) en vez del número fijo en el código, y los tótems de la Furia
elemental no cuentan para ese límite. **Si el Chamán muere, sus tótems desaparecen** (pedido de Gustavo; también
los de la Furia).

- [x] (Corrida y borrada el 19 de octubre; el aura funciona.) Correr `Mercenarios ▸ Una vez ▸ Pasar los tótems a la base de invocaciones` y borrar
      `Assets/Scripts/Editor/MercSummonTotems.cs`. Le agrega a cada prefab de tótem su zona con el aura que
      tenía (efecto, potenciado con el Frenesí, radio, cada cuánto, VFX) y le pone: máximo 2, desaparece si
      el Chamán muere, no recibe zonas. **Sin correrla, los tótems no dan aura.**
- [x] **PROBADO ✅ (20 de octubre).** Probar: poner tótems (dan el aura a los aliados y al Chamán, potenciada con el Frenesí; el VFX del aura en
      las dos pantallas); el tercero saca al más viejo; romper uno a golpes; **morir con tótems puestos (se van)**;
      la Furia elemental (sus 4 tótems no sacan a los de la rueda, se van al terminar y si el Chamán muere).
- [x] **PROBADO ✅ — Barras de vida** (19 de octubre, lo vio Gustavo): la del tótem salía VACÍA (ya pasaba antes de la base: la vida
      solo viajaba al cambiar; ahora la base la manda al aparecer). El Lobo y el Oso no tenían barra armada, y la
      COPIA del Ilusionista no tenía ninguna. Correr `Mercenarios ▸ Una vez ▸ Poner barras de vida a compañeros y
      copias` y borrar `Assets/Scripts/Editor/MercSummonHealthbars.cs` (corrida y borrada): copia la barra de los tótems al Lobo y al
      Oso (a la altura de su modelo) y la del JUGADOR a la copia. En juego, la copia muestra la barra del jugador
      copiado (nombre, vida, buffs, ícono de clase: `UI_WorldHealthbar.Mirror`), así el enemigo no sabe cuál es
      el real. Probar: barra llena del tótem al ponerlo; las del Lobo y el Oso (bajan al pegarles); la copia con
      el nombre y la vida del Ilusionista (y en la Fiesta, la de cada aliado copiado), en las dos pantallas.
- [x] **PROBADO ✅ — UN prefab para las barras de vida** (19 de octubre, pedido de Gustavo): `Prefabs/UI/Nameplate`, con
      `UI_Nameplate` (las piezas: vida, buffs, nombre, aviso de crítico, ícono de clase —antes lo armaba el
      código—, y los colores). Antes cada prefab tenía su copia de la barra de la jam, cada una de otro tamaño.
      Correr `Mercenarios ▸ Una vez ▸ Hacer el prefab de las barras de vida` y borrar
      `Assets/Scripts/Editor/MercNameplatePrefab.cs` (corrida y borrada el 19 de octubre): crea el prefab desde la barra del jugador y, en cada prefab
      con barra (jugador, enemigos, tótems, compañeros, copia, muñeco), cambia la vieja por una instancia del
      prefab a la misma altura. **El tamaño pasa a ser el mismo para todos** (el del jugador). **Sin correrla,
      los jugadores no muestran el ícono de clase.** Probar: barras de jugadores, enemigos, tótems, Lobo, Oso y
      copias; cambiar algo en el prefab (el color) y que cambie en todos.
- [x] **PROBADO ✅ — Tótems arriba del techo** (lo vio Gustavo): se buscaba el piso desde 10 m arriba del punto (las trampas
      desde 3 m) y bajo techo quedaba encima del techo. Ahora `Entity_Summon.GroundUnderAim` lo busca desde un
      poco antes del punto, del lado desde donde se miró (apuntando al techo, cae al piso de abajo), y
      `GroundNear` para lo que se reparte alrededor (los tótems de la Furia, las bombas). Probar bajo techo:
      tótems, trampas al aire y la definitiva del Trampero.
- [x] **Plantillas de invocación** (20 de octubre, pedido de Gustavo; corrida y borrada el 20, falta probar `GA_SpawnSummon` con una variante): correr `Mercenarios ▸ Una vez ▸ Crear las
      plantillas de invocación` y borrar `Assets/Scripts/Editor/MercSummonTemplates.cs`. Crea en `Prefabs/Summons`:
      **`Summon_Base`** (con vida: NetworkObject, NetworkTransform, ASC con `ASDef_SummonBase` de 100 de vida,
      NetworkASC, collider trigger en la capa 7, Entity_Summon que desaparece si el dueño muere, barra de vida y
      una cápsula de muestra como modelo) y **`SummonZone_Base`** (solo zona: Entity_Summon de 6 s + Summon_Zone a
      enemigos, cada 0.5 s, en 4 m). Una invocación nueva = clic derecho ▸ Create ▸ Prefab Variant. La pone el
      genérico nuevo **`GA_SpawnSummon`** (Create ▸ GAS ▸ Generics ▸ Spawn Summon: donde apunta con marcador, a
      los pies o delante; "Máximo a la vez"). Los pasos, en la guía de habilidades (7.1). El límite de una
      invocación genérica ahora se agrupa por su prefab (el muro de fuego no comparte límite con el elemental).
- [ ] **Otros prefabs que valdrían la pena** (revisado el 19 de octubre, por decidir):
  - **Variantes**: los 4 tótems, las 4 trampas y los enemigos (`Net_Enemy`, `Net_Mage`, `Net_Boss`) son casi
    iguales entre sí. Con un prefab base y variantes, agregarles algo a todos es tocar uno. Las trampas, al
    pasarlas a la base de invocaciones (lo siguiente).
  - **UI que todavía se arma por código** (va contra "la UI vive en el prefab"): el marcador de zona en el piso
    (`UI_GroundTargetIndicator`, se crea la primera vez), el dibujo del botiquín (`HealthPackVisual`: disco,
    cruz, texto), la grilla de tarjetas del menú de clases y los `EventSystem` que crean tres menús por su
    cuenta (uno en `Menus_UI` alcanza).
  - No hace falta: el audio (fuentes de sonido en juego), las siluetas (`TeamReveal`, `CharacterOutline`) y los
    bordes invisibles de la arena (son constructores de geometría).
- [x] **PROBADO ✅ — La TRAMPA pasada a la base** (19 de octubre; probada el 20): `Entity_Trap` es una invocación. La base resuelve el equipo,
      que la vean solo los suyos, "Dura", el límite de 6 (las bombas no cuentan: campo nuevo de la base,
      **Cuenta para el límite**) y desaparecer si el Trampero se va o cambia de clase; si muere, siguen puestas
      (como antes). La zona de espinas pasa a ser una `Summon_Zone`. Quedan suyos: armarse, saltar, el aviso y
      explotar. Y las 4 trampas pasan a ser **VARIANTES de `Prefabs/Summons/Trap_Base`**: lo que se cambie en la
      base cambia en todas.
- [x] (Corrida y borrada el 19 de octubre.) Correr `Mercenarios ▸ Una vez ▸ Pasar las trampas a la base de invocaciones` y borrar
      `Assets/Scripts/Editor/MercSummonTraps.cs`. Crea la base, convierte cada trampa en variante EN SU MISMO
      ARCHIVO (el modelo se mueve tal cual, con la pieza que le quitaste a la bomba), pasa la zona de espinas a
      su `Summon_Zone` y reconecta las trampas a `GA_PlaceTrap` y `GA_BombBarrage`. **Sin correrla, la de
      espinas no hace su zona.**
- [x] **PROBADO ✅ (20 de octubre).** Probar: las 4 trampas (se arman en 1.5 s, invisibles para el enemigo, saltan solo con jugadores; la de
      espinas con su zona; la de frío con su aviso; la explosiva empuja desde ella), el límite de 6, la R (las
      bombas no sacan trampas y explotan al vencerse), en el piso y en una pared; en un cliente.
- [x] **PROBADO ✅ — La CUCHILLA ILUSORIA y la COPIA del Ilusionista, pasadas a la base** (20 de octubre). Sin herramienta que
      correr. La cuchilla: su "Malla visual" pasa a ser el "Modelo" de la base (el mismo dato); la crea
      `IllusoryBladesPassive` con `Entity_Summon.Spawn` + `Launch`. La copia: el límite de la Copia exacta (4, la
      más vieja se va; las de la Fiesta no cuentan), irse si el Ilusionista muere (puesto en su prefab), se va o
      cambia de clase: todo de la base; `PlayerCopyManager` quedó solo para crearlas. Probar: las cuchillas al
      pegar (suben, viajan, dejan herida, se cortan en paredes); la Copia exacta (la quinta saca a la más vieja;
      camina, explota al golpearla con ceguera y herida al que pegó; su barra imita al Ilusionista); la Fiesta
      (no saca a las exactas); morir con copias puestas (se van); en un cliente. **Las cuchillas, probadas ✅ (20 de
      octubre).** La copia lanzada en el aire se quedaba flotando (caminaba a la altura en que nacía): ahora
      sigue el piso (cae con gravedad, sube escalones chicos). Probar: lanzarla saltando y en una rampa.
- [x] **PROBADO ✅ — El COMPAÑERO del Explorador, pasado a la base** (20 de octubre; con esto TODAS las invocaciones usan la
      base). Sin herramienta que correr: su "Modelo" pasa a ser el de la base (mismo dato). La base le da el
      equipo, la regla de enemigo, irse si el Explorador se va o cambia de clase, el dueño sincronizado a todas
      las pantallas (`SummonerObject`: el panel del HUD encuentra así a su compañero) y su propia muerte, que
      ahora se resuelve en Update y no en medio del daño. Si el Explorador muere NO se va solo: lo guarda el
      `CompanionManager` (anota su vida), que sigue con lo suyo (cuál está afuera, Ira primal, vida guardada,
      espera para volver, órdenes). Probar: Lobo, Oso y Búho (atacan solos, órdenes al pegar, defienden, el Oso
      provoca a los fantasmas, el Búho caza invisibles, el aullido, la presencia); cambiarlos con la rueda (vuelve
      con su vida); que muera uno (su espera); morir con uno afuera; la Ira primal (salen los tres y al terminar
      queda el elegido); el panel del HUD y sus barras; en un cliente.
- [x] ~~Después: la trampa, la cuchilla ilusoria, la copia del Ilusionista y el
      compañero (el más grande, al final).~~

---

# ★ Canalizar, choque de los proyectiles y registro del desatascarse — HECHO Y PROBADO ✅ (19 de octubre)

**1. Canalizar, en cualquier habilidad.** Todo GA tiene una sección nueva **Canalizar**:
- **Canalizar:** "No canaliza" / "Canaliza y se lanza al terminar" / "Continuo: hace efecto mientras canaliza".
- **Dura (s)**; en el continuo, **Hace efecto cada (s)** (la habilidad se repite; el costo y el cooldown, una vez).
- **Bloquea las demás habilidades** (sí por defecto; moverse siempre se puede) y **Se corta al recibir daño** (no por defecto).
- Clips: **empezar a canalizar** (opcional), **canalizando (bucle)** y, en el continuo, **soltar**. Al lanzar al
  terminar, el de soltar es el clip de siempre de la habilidad (sección Animación).
- **Si la cortan, cuenta como lanzada** (cobra costo y cooldown). La corta lo que la bloquea (el aturdido; el
  silencio si es magia; el desarme si es de arma), morir, otra habilidad si es interrumpible y, si se marcó, el daño.
- Mientras canaliza, la mira del dueño le llega al servidor en vivo: sale hacia donde apuntas AL SOLTAR.
- La barra de canalizar del HUD se llena (al terminar) o se vacía (continuo); roja si la cortan.
- No vale en las de mantener, las de rueda ni las que ya canalizan solas (molinete, Aliento del dragón, Golpe final).
- **El Haz guía del Clérigo (y el radiante del Clérigo de la Luz): 1 s de canalización**, con la pose
  `MagicAttackDirect1H01_L - Load` en bucle (la pareja del "- Cast" con el que ya lanzaba). Se cambia en el Inspector.

**2. El choque de los proyectiles, por barrido** (`GC_Projectile`). Pediste que la caja tomara el tamaño del arma:
- **Caja al tamaño del arma** (casilla nueva del prefab, prendida): el hacha, la daga, el kunai y la lanza chocan
  con paredes y escudos con SU tamaño, no con la caja de 1 m de antes. **Grosor mínimo** 0.1 m.
- Pero achicar la caja también hacía más difícil acertarle a la gente, así que eso va aparte: **Radio contra
  personajes** (0.5 m, lo mismo que daba la caja de antes). La flecha queda en 0 (su caja de siempre, precisa).
- Y el choque ya no espera a que la caja TOQUE algo en un paso de física: el servidor barre el tramo que
  recorrió. Antes, a 100 m/s (el Haz guía) avanzaba 2 m por paso y se podía saltar una pared o un escudo finos.
  Lo encontrado se procesa en orden: un escudo frena antes que el que está detrás. El primer tramo sale del cuerpo
  del que tira: tirar de frente pegado a una pared pega en la pared (no la cruza); de costado ya no.

**3. El desatascarse queda guardado.** Cada pedido (aceptado o rechazado) se escribe en **`Unstuck.log`** del HOST:
en el editor, `GasTest/Logs/Unstuck.log`; en una build, junto al `Player.log`. Junta las dos partes: lo que ve el
que lo pidió (input, menús abiertos, cursor, si estaba atacando y con qué, movimientos especiales, el
CharacterController, ragdoll, tags) y lo que ve el servidor (tags, efectos, cuánto lleva quieto, último golpe).

- [x] Probar el **Haz guía** (es un lanzamiento APUNTADO: mantener y soltar): mantener canaliza con la pose
      y la barra; soltando antes de que se llene, el rayo sale apenas se llena (hacia donde apuntas en ese
      momento); aturdido o silenciado a mitad, se corta (barra roja) y queda en cooldown. (19 de octubre: la
      primera versión no canalizaba porque los mantenidos tienen su propio ciclo con el botón; arreglado.)
- [x] El **bastón del Clérigo**, a 0.8 (campo nuevo de la clase "Arma principal: tamaño", en el Clérigo y en
      sus dominios de la Vida y de la Luz). Es el mismo modelo que la lanza del Comandante y del Trampero:
      ellos no cambian. En un cliente: que se vea igual (el bucle
      y el lanzamiento) en las dos pantallas.
- [x] Probar un **Continuo** en cualquier habilidad (por ejemplo, ponerle a un ataque "Continuo, 2 s, cada 0.4 s").
- [x] Probar el **choque**: tirar el hacha o la daga pasando cerca de una pared (ya no pega en ella); acertarle
      a alguien (igual de fácil que antes); el Haz guía contra un escudo (lo frena, no lo cruza); el parry del
      Guardián (sigue devolviendo).
- [ ] La próxima vez que alguien se atasque: traer `Logs/Unstuck.log`.

**Después de probar esto (decidido el 19 de octubre): una base para las entidades invocadas, antes del Mago.**
Tótems, trampas, compañeros, copias del Ilusionista y cuchillas ilusorias siguen la misma receta pero cada una
está escrita por su cuenta: el límite "la más vieja se va" está tres veces (`GA_SpawnTotem`, `PlayerCopyManager`,
`Entity_Trap`), "quién es enemigo" se decide de tres formas, y qué pasa si el dueño muere varía (las copias se van;
trampas y tótems siguen). Plan: `Entity_Summon` (dueño y equipo, duración, límite por dueño, irse o no si el dueño
muere o cambia de clase, regla de enemigo/aliado, ASC propio opcional, visibilidad por equipo) y una pieza de
**zona** (aplicar algo a los que estén dentro cada X s: el aura del tótem, la zona de espinas, las áreas
continuas). Pasar las cinco existentes de a una, probando cada una.

---

# ★ Parry del Guardián: el proyectil devuelto hace el daño que traía — CÓDIGO HECHO, falta probar (15 de octubre)

Revisando el pendiente viejo ("sigue haciendo el daño que traía") aparecieron dos fallas:

- **La quemadura del Clérigo de la Luz devuelta no hacía nada.** Su Haz guía radiante y su Arco
  radiante dañan con una quemadura que escala con el daño MÁGICO. Devuelta, se recalculaba con
  los stats del Guardián, que no tiene daño mágico: 0 por segundo.
- **Un daño mágico devuelto pasaba a ser físico.** El Haz guía, los arcos de luz, los orbes
  del Maestro elemental y el Castigo divino volvían como un golpe plano: la armadura del que lo
  tiró se lo recortaba, cuando lo mágico la ignora.

**Cómo queda:** al devolverlo, cada efecto del proyectil que escala con un stat del que lo
tiró se cambia por una COPIA CONGELADA con esos stats (`GameplayEffect.CreateFrozenCopy`): el
número queda fijo y el tipo también (lo mágico sigue siendo mágico). La quemadura vuelve como
quemadura, con su ícono y su VFX en todas las pantallas (la copia viaja por red como el original:
`GameplayEffect.SourceTemplate`). Lo que no escala va tal cual (la ralentización del hacha, las
heridas de la daga). La baja sigue siendo del Guardián. Antes se devolvía solo el daño principal,
como un número plano y físico.

- [ ] Probar con el Guardián, en un cliente (no en el host), haciendo parry a:
  - el **Haz guía radiante del Clérigo de la Luz**: le vuelve quemando (números cada segundo) y
    el ícono de la quemadura sale en su barra;
  - el **Haz guía** del Clérigo base o un **orbe** del Maestro elemental: el número devuelto es el
    mismo que el que le haría a alguien sin armadura (lo mágico la ignora);
  - una **flecha cargada entera** del Explorador: vuelve con el daño de la carga completa;
  - una **daga** del Pícaro: además del daño, le deja las heridas.

---

# ★ Revisión: la UI vive en el prefab y variables claras — LAS 6 ETAPAS HECHAS Y PROBADAS ✅ (15 de octubre)

Pedido de Gustavo: la UI tiene que vivir en el prefab de la cámara del jugador y no "ponerse por
encima" por código, y las variables tienen que entenderse sin leer el código ("Otros: más que un
buff: −1" no se entiende). Hay que revisar todo lo hecho desde el Guerrero con ese criterio.

**El patrón nuevo** (el piloto es el panel de compañeros): la pieza es un objeto del HUD en
`Player Camera.prefab`, con sus partes armadas ahí (un MOLDE apagado si se repite, como los íconos
de buffs). El script solo lee el juego y actualiza lo que ya existe; no crea canvas ni objetos de la
nada. Los tamaños y colores son los de la pieza (en px del HUD), con nombres que dicen qué son.
Una herramienta de una vez arma la pieza en el prefab la primera vez; después se edita a mano.

- [x] **Piloto: panel de compañeros.** `UI_CompanionPanel` + `UI_CompanionSlot` (el molde) viven en
      el HUD; tamaños "Tamaño del activo (px)" y "Tamaño de los otros (px)"; el CompanionManager ya
      no sabe nada de la UI. La herramienta ya se corrió y se borró.
  - [x] Acomodarlo a mano encima de los buffs (el script apaga solo el LayoutGroup que tenga; se
        puede quitar del prefab).

**Dónde va cada cosa** (lo que se habló el 14 de octubre):

- **Prefab de la cámara** (`Player Camera`, solo existe la del jugador local): lo que es de MI
  personaje y va pegado a la mira o al HUD — barras de canalizar y de carga, la X / calavera / arco
  de daño, la viñeta, los compañeros, la rueda.
- **Prefab de partida** (uno por escena, puesto en las dos): lo que es de la partida y tiene que
  seguir aunque el personaje muera o cambie — registro de bajas, pantalla de muerte y avisos,
  números flotantes, marcador, avisos de partida, marcador del Objetivo, espectador.
- **Prefab de menús**: menú principal, sala, ajustes.

**Lo que se armaba solo por código** (auditado el 14 de octubre):

| Pieza | Script | Dónde vive | Desde |
|---|---|---|---|
| Barra de canalizar | `UI_CastBar` | **cámara** | 6 oct |
| Barra de carga por etapas | `UI_ChargeBar` + molde `UI_ChargeSegment` | **cámara** | 5 oct |
| La X, la calavera y el arco de daño | `UI_CombatFeedback` | **cámara** | 22 sep |
| Viñeta de vida baja | `UI_LowHealthVignette` (antes en `UI_ScreenFeedback`) | **cámara** | 25 sep |
| Pantalla de muerte y avisos | `UI_ScreenFeedback` | **partida** (`Match_UI`, 14 oct) | 25 sep |
| Números flotantes | `UI_DamageNumbers` | **partida** (`Match_UI`, 14 oct) | 25 sep |
| Registro de bajas | `UI_KillFeed` | **partida** (`Match_UI`, 14 oct) | 25 sep |
| Rueda | `UI_RadialMenu` + molde `UI_RadialOption` + `UI_RingWedge` | **cámara** | — |
| Curva del proyectil al apuntar | `TrajectoryPreview` | **cámara** | 13 oct |
| Silueta a través de paredes | `TeamReveal` + `TeamRevealStyle` | se agrega sola; su material, en la **cámara** | 13 oct |
| Marcador, avisos de partida, marcador del Objetivo | `UI_MercenariesHUD`, `UI_MatchAnnouncer`, `UI_ObjectiveMarker` | **partida** (`Match_UI`, 14 oct) | 18 sep |
| Menú principal, sala, ajustes | `UI_MainMenu`, `UI_LobbyPanel`, `UI_SettingsPanel` | **menús** (`Menus_UI`, 14 oct) | 18 sep |

- [x] **Etapa 2 — el HUD de la cámara.** Código hecho (14 de octubre): las cuatro piezas leen sus
      partes del prefab (referencias en el Inspector) y ya no crean canvas. Medidas en px del HUD
      (el canvas del HUD es de 800 de ancho: lo de antes, pensado para 1920, va × 800/1920).
      La X tiene sus puntas dentro de soportes girados: seleccionando las cuatro se les cambia el
      largo, el grosor y el hueco a la vez. El arco de daño saca el ángulo de dónde está cada
      pedacito y usa su color del prefab; la viñeta usa el color de su Image (el más fuerte).
  - [x] Correr `Mercenarios ▸ Una vez ▸ Poner las piezas del HUD en la cámara`, revisar y
        acomodar en el prefab, y borrar `MercCameraHudSetup.cs`. La textura de la viñeta queda en
        `Art/UI/Vignette_LowHealth.png`.
  - [x] Probar: canalizar (Golpe final, molinete) y que "Interrumpido" salga rojo; cargar un
        ataque por etapas (los tramos, el destello, el saltito); pegar (X roja, amarilla en
        crítico), matar (calavera), recibir por la espalda (arco abajo); bajar del 30 % de vida.
- [x] **Etapa 3 — la rueda.** Hecha y se ve bien (14 de octubre): `UI_RadialMenu` ya no dibuja nada ni
      crea texturas; usa sus piezas del prefab (`Player Camera ▸ UI ▸ RadialMenu`): el fondo, el
      MOLDE de una porción (`UI_RadialOption`: brillo, porción e ícono), el centro, el puntero y los
      dos textos. Las porciones son `UI_RingWedge`, una pieza nueva que dibuja un trozo de anillo
      con radio interior y exterior en px y su ancho en grados (como la Reticle, sin sprite): se ve
      en el prefab tal cual. Lo quieto (radios, colores, tamaño máximo del ícono, el radio del
      centro, que es también la zona de cancelar) se cambia en las piezas; lo que cambia al
      apuntar (resaltado, en espera, cancelar, tiempos) en el componente. Se fue el parche
      `UpgradeStyle` y el `RadialMenu_Container` viejo.
  - [x] Correr `Mercenarios ▸ Una vez ▸ Poner la rueda en el HUD de la cámara`, revisar y
        acomodar, y borrar `MercRadialMenuSetup.cs`. Hasta correrla la rueda no abre (avisa en
        la consola).
  - [x] **Se ven bien (14 de octubre).** Probar jugando: el tótem del Chamán y los compañeros del Maestro de bestias (abrir, resaltar,
        cancelar en el centro, una opción en espera, elegir), con mouse y con control.
- [x] **Etapa 4 — lo que se dibuja en el mundo.** Hecha y se ve bien (14 de octubre):
  - **La curva** (`TrajectoryPreview`) vive en `Player Camera ▸ TrajectoryPreview`, con sus dos
    LineRenderer (`Line` y `Ring`): el color, el grosor y el material se cambian ahí, y se ven en
    el prefab con una curva de muestra. Material nuevo `Art/Materials/Mat_TrajectoryLine`.
  - **La silueta** (`TeamReveal`) toma su material de `Player Camera ▸ TeamRevealStyle`; el color
    es el de `Art/Materials/Mat_TeamReveal` (shader Mercenaries/XRay).
  - **La curva en TODOS los lanzamientos apuntados** (pedido de Gustavo): el hacha (Bárbaro y
    subclases), las dagas (Pícaro, Asesino, Ilusionista), la lanza del Comandante (por su
    switch) y el Guiding Bolt (y el radiante). Cada `GA_ProjectileShoot` tiene la casilla "Curva
    para el que apunta" (prendida por defecto; también vale para las fases del arco). La curva
    llega hasta donde vuela el proyectil ("Tiempo de vuelo", o 5 s). Se pregunta a cualquier
    mantenido con `GameplayAbility.ProjectileToPreview` (lanzamiento, arco por fases, switch).
  - [x] Correr `Mercenarios ▸ Una vez ▸ Poner la curva y la silueta en la cámara` (corrida y borrada el
        14 de octubre).
  - [x] **Se ven bien (14 de octubre).** Probar jugando: apuntar con el hacha, las dagas, la lanza (en ofensiva) y el Guiding Bolt, y
        tensar el arco: la curva sale de la mano, cae como el proyectil y el anillo marca donde
        choca. La Marca del cazador sigue mostrando la silueta a través de las paredes.
- [x] **Etapa 5 — el prefab de partida y el de menús** (ver "Dónde va cada cosa"), puestos en las
      dos escenas. Va en tres tandas:
  - [x] **5a — lo de la pelea: HECHO (14 de octubre; herramienta corrida en las dos escenas y borrada), falta probar.**
        Prefab nuevo **`Prefabs/UI/Match_UI`** con tres canvas (mismo orden de dibujo que antes):
        `DamageNumbers` (los números flotantes, con el MOLDE `NumberTemplate`), `KillFeed` (el registro
        de bajas: `Entries` es la esquina y `EntryTemplate` el MOLDE de una línea) y `ScreenFeedback`
        (la franja de muerte y el aviso corto). Los tres scripts ya no crean nada: si la escena no
        tiene el prefab, avisan una vez en la consola y no muestran nada. Los canvas son de
        1920×1080 (escalan con la pantalla), como eran: las medidas de las piezas siguen iguales.
    - [x] Con `Mercenaries_Gamemode` abierta: `Mercenarios ▸ Una vez ▸ Crear el prefab de partida y
          ponerlo en la escena`, guardar la escena. Abrir `Mercenaries_Graveyard`, correrla otra vez
          (solo lo pone) y guardar. Si querés también en `Test_Network`, igual. Después borrar
          `Assets/Scripts/Editor/MercMatchUiSetup.cs`.
    - [x] Probar: números de daño, crítico y curación; matar y morir (el registro arriba a la derecha,
          tu línea resaltada; "Te eliminó X" y la cuenta para reaparecer); "¡Definitiva lista!".
  - [x] **5b — lo del modo: HECHO (14 de octubre; herramienta corrida y borrada), falta probar.** Al
        mismo `Match_UI`, cada uno con su canvas y su orden de siempre (todo igual a como se veía):
        - `Scoreboard` (`UI_MercenariesHUD`): `Panel` con el reloj, las tres filas (fondo, franja de
          color, nombre, nivel, barrita de experiencia y dos tramos de entrega) y el estado del
          Objetivo. El script pinta del color del equipo la franja, la barrita y los tramos; para
          jugar a más de 2 puntos, la fila necesita más tramos.
        - `Announcer` (`UI_MatchAnnouncer`): `Messages` es la altura del aviso y `MessageTemplate` el
          MOLDE de uno. Los tres tamaños de letra según el aviso, en el componente.
        - `ObjectiveMarkers` (`UI_ObjectiveMarker`): el del Objetivo y el de tu entrega, cada uno con
          rombo, distancia y barra de entrega. El tamaño del rombo y el ancho de la barra salen del prefab.
        - `Spectator` (`SpectatorCamera`): el componente entero (la cámara y su canvas: el panel de
          quien mirás y la ayuda de teclas). Se fue el `SpectatorCamera` suelto de la escena.
        - Las barras que se llenan usan un sprite blanco nuevo, `Art/UI/UI_White.png` (lo crea la herramienta).
    - [x] (En `Mercenaries_Gamemode` la limpieza no quedó guardada: Claude sacó `HUD_Mercenarios` y
          `SpectatorCamera` del archivo de la escena a mano.) Con `Mercenaries_Gamemode` abierta: `Mercenarios ▸ Una vez ▸ Agregar el modo al prefab de
          partida (5b)` y guardar (agrega las piezas al prefab y saca `HUD_Mercenarios` y
          `SpectatorCamera` de la escena). Abrir `Mercenaries_Graveyard`, correrla otra vez (solo
          limpia la escena) y guardar. Después borrar `Assets/Scripts/Editor/MercMatchUi5bSetup.cs`.
          **Hasta correrla, el marcador y los avisos no se ven** (los objetos viejos quedan sin piezas).
    - [x] Probar: el marcador (reloj, niveles, tu fila resaltada, entregas que se llenan), los avisos
          (aparece el Objetivo, lo toma un equipo, entrega, aniquilación), el rombo del Objetivo (dentro
          y fuera de cámara, la barra de entrega) y tu punto de entrega; entrar de espectador (volar,
          seguir con clic, H y M).
  - [x] **5c — los menús: HECHO (15 de octubre; herramienta corrida en las dos escenas y borrada), falta probar.** Prefab
        nuevo **`Prefabs/UI/Menus_UI`** con tres partes; cada una es un objeto siempre prendido con su
        componente y, adentro, su canvas (lo que se prende y se apaga), con el orden de dibujo de siempre
        y las medidas y colores que tenían las escenas:
        - `MainMenu` (`UI_MainMenu`): velo, franja, título, subtítulo, Jugar / Ajustes / Salir y la
          versión. Los textos llevan su `LocalizedText`.
        - `Settings` (`UI_SettingsPanel`): las secciones y las filas. Cada fila lleva una pieza nueva,
          **`UI_SettingRow`**, que dice qué ajuste maneja (Idioma, Control, Sensibilidad...) y dónde
          están su barra, su casilla o sus flechas; el panel las conecta solas. La sección de sonido no
          se arma (estaba apagada): para prenderla, duplicar una fila de barra y elegirle "Volumen
          general", "Música" o "Efectos". `LocalizedText` tiene ahora "En mayúsculas" (los títulos de
          sección).
        - `Lobby` (`UI_LobbyPanel`): IP, nombre, Start, las cabeceras, los lugares (cada uno con la pieza
          nueva **`UI_LobbySlot`**: de qué equipo es, su fondo, ícono, nombre y botones), la línea de
          estado, Confirmar y el selector de clase. Lo que depende de la lista de clases (las pestañas
          por rol y los íconos) se arma al empezar copiando dos MOLDES del selector (`TabTemplate` y
          `ClassIconTemplate`); el selector se ensancha solo si las clases de un rol no entran. Los
          espectadores que no entran en las dos filas armadas copian la última.
        - La sala de `Mercenaries_Gamemode` tenía 6 clases y la del cementerio 7: el prefab se queda con
          las 7 (la herramienta agrega las que falten).
    - [x] Con `Mercenaries_Gamemode` abierta: `Mercenarios ▸ Una vez ▸ Crear el prefab de menús y ponerlo
          en la escena (5c)` y **guardar**. Abrir `Mercenaries_Graveyard`, correrla otra vez (pone el
          prefab, agrega la clase que falta y saca los viejos) y **guardar**. Después borrar
          `Assets/Scripts/Editor/MercMenusUiSetup.cs`. **Hasta correrla, los menús no se ven.**
    - [x] Probar: el menú (Jugar, Ajustes, Salir, ESC abre los ajustes), los ajustes (cada fila: idioma,
          control, sensibilidad, invertir Y, pantalla, resolución, sincronización; Restablecer y Cerrar),
          la sala (unirse a un equipo, elegir clase con las pestañas, confirmar, Start, agregar y sacar
          bots, el nombre, irse a espectadores con varios espectadores), en los dos idiomas.
- [x] **Etapa 6 — variables claras: HECHA en TODO el código (15 de octubre), no solo desde el Guerrero.**
      Se revisaron los 1 247 campos que se ven en el Inspector (habilidades, efectos, pasivas,
      componentes, compañeros, paredes, cámara, modo, bots, UI):
      - **275 nombres con su unidad**: `(m)`, `(s)`, `(m/s)`, `(°)`, `(px)`, `(×)`, `(0 a 1)`, `(vida)`,
        `(exp)`; los que eran "(fracción)" pasaron a "(0 a 1)", y los 0 especiales van en el nombre
        ("Alcance máximo (m; 0 = sin tope)"). No se tocó ningún valor ni ningún nombre de campo en el
        código: los assets siguen iguales.
      - **Lo que salía en inglés** ahora está en español: la biblioteca de audio (y cada sonido), el
        catálogo de enemigos, los dos registros de red, la retícula, y en los VFX de una habilidad
        "Delay / Offset / Scale" (ahora "Espera (s)", "Posición (m)", "Escala (×)").
      - La regla quedó en CLAUDE.md, junto a la de `[Label]`.
      - [x] Mirar en Unity que se vea bien (un GA, un GE, la cámara, la biblioteca de audio).

---

# ★ Maestro de bestias: los compañeros — HECHO Y PROBADO ✅ (15 de octubre); la Ira primal, probada ✅

Diseño de Gustavo (14 de octubre). Cada compañero es una IA del servidor (`Entity_Companion`) que
maneja el `CompanionManager` del Explorador (en `BeastMasterBehaviours`):

- **Pasivo:** cada tantos segundos ataca solo a un enemigo en su rango (medido desde el Explorador),
  primero jugadores y el más cercano al Explorador, y se pone en cooldown.
- **Orden:** cuando el Explorador pega, el compañero se olvida del cooldown y corre al doble de
  velocidad a pegarle a ese, aunque esté fuera de rango; si estaba fuera, después vuelve a merodear.
  Se queda con su objetivo mientras siga en rango. **Defiende** al Explorador del que le pega.
- No ataca invisibles, salvo el Búho, que los busca primero. Todos abren heridas; el Lobo dos por
  mordida. Su daño cuenta como del Explorador (escala con su ataque, la baja es suya).
- **Presencia bestial:** la dan el compañero Y el Explorador (8 m alrededor de cada uno), sin sumarse.
- **Físicas:** Lobo y Oso por NavMesh; a un objetivo en una pared o en el aire lo esperan abajo; si
  quedan lejos o trabados, reaparecen junto al Explorador. El Búho no busca camino: aparece junto al
  objetivo para el picotazo y vuelve.
- **HUD:** un panel arriba del ícono de clase con los tres (el activo más grande y con su vida; el que
  murió, oscurecido con los segundos que le faltan) y el botón E muestra al activo. Cada compañero
  muerto tiene su propia espera (sale en la barra de efectos); mientras, se puede sacar otro.
- Los fantasmas prefieren al Oso si cerca no hay otro jugador que su dueño (`Status_Taunting`).

| | Lobo | Oso | Búho |
|---|---|---|---|
| Vida | 50 % del Explorador | 150 % | indestructible |
| Rango pasivo | 20 m | 12 m | 30 m |
| Ataque | mordida cada 1 s (×0.35) + 2 heridas | zarpazo cada 2.2 s (×0.9) + herida + aturdido 0.3 s (al mismo, 1 vez cada 2 s) | picotazo cada 3 s (×0.3) + herida + Marca doble (+20 %) + revelado 4 s |
| Especial | aúlla si muere algo que mordió: +5 % de ataque a aliados (hasta 3) | se pone delante; los fantasmas lo prefieren | caza invisibles |
| Presencia | +10 % velocidad | +10 % resistencia | ×0.9 entre ataques |
| Vuelve en | 10 s | 15 s | — |

- [x] **Correr `Mercenarios ▸ Una vez ▸ Crear el Maestro de bestias (compañeros)`** (corrida y borrada el 14 de octubre; el Búho salió mal, ver abajo). Crea los efectos,
      `ASDef_Companion`, los tres prefabs en `Prefabs/Summons` (copias del fantasma de red sin su IA,
      con una figura de primitivas), `BeastMasterBehaviours`, `GA_SwitchCompanion` (la E) y
      `Class_BeastMaster` como subclase del Explorador; actualiza los registros y la lista de FishNet.
      Después se borra `MercBeastMasterSetup.cs`.
- [x] **Correr `Mercenarios ▸ Una vez ▸ Arreglar los compañeros (Búho de cero)`** (corrida y borrada el 14 de octubre) y borrar `MercCompanionFix.cs`.
      La barra de vida del fantasma vive en la RAÍZ de su prefab: al quitársela al Búho se borró todo y quedó
      como copia cruda del fantasma, sin `Entity_Companion`; el manager no lo reconocía y sacaba otro cada frame
      (los fantasmas que se multiplicaban). Ahora el Búho se arma de cero con primitivas (mismo archivo), al Lobo
      y al Oso se les devuelve la raíz a cero y se les sube la barra, y el manager ya no reintenta un prefab sin
      `Entity_Companion` (lo saca y avisa en la consola).
- [x] **Arreglos tras la primera prueba (14 de octubre) — PROBADOS ✅:**
  - Bucle de correr y reaparecer: reaparecía junto al Explorador a 25 m aunque estuviera persiguiendo
    una orden (que llegaba hasta 40 m). Ahora persiguiendo no reaparece, y una orden a más de 40 m del
    Explorador (`Alcance de las órdenes`) se ignora.
  - **Salta entre pisos:** si el camino por el NavMesh da mucha vuelta (más de 1.8 veces la línea
    recta) o no llega, y el punto está a 14 m o menos y 9 m de alto o menos, con el aire libre en el
    medio, salta derecho en arco y cae sobre el NavMesh del otro lado. Vale para ir a un objetivo y
    para seguir al Explorador. Sección `Saltar entre pisos` del compañero.
  - El panel y la rueda pasaron a ser piezas del prefab de la cámara (ver la revisión de la UI, arriba):
    Gustavo acomodó el panel sobre los buffs y la rueda quedó como estaba (grande, resaltado verde).
- [x] **Probado (14 de octubre): ataca bien.** Probar en una escena con NavMesh (la del modo): elegir Maestro de bestias, que salga el Lobo, que
      ataque solo, que vaya a lo que le pegás, que defienda, que vuelva si se aleja.
- [x] La rueda (E): cambiar al Oso y al Búho; el que murió aparece apagado con su tiempo.
- [x] El panel del HUD y el ícono del botón con el compañero activo.
- [x] Las posiciones de los tres están bien. Falta el Búho junto a un invisible.
- [x] En un cliente: que se vean moverse y atacar, y **la barra de vida del compañero, para aliados y
      para enemigos** (Lobo y Oso la traen en su prefab; el Búho no tiene, es indestructible).
- [x] **Arreglos tras la segunda prueba (14 de octubre, noche), falta probar:**
  - **Los fantasmas van por el compañero** si está en su radio, como por cualquier jugador (el más
    cercano); antes solo el Oso por provocar. El Búho no (indestructible). `MercEnemyAI.FindTarget`.
  - **La vida se guarda al cambiarlo:** un compañero que sacas herido vuelve con la vida que tenía, y
    mientras está guardado se cura de a poco: "Curación guardado (% de vida por s)" del
    `CompanionManager` (en `BeastMasterBehaviours`), 2 % por segundo (lleno en 50 s). Uno que MUERE
    vuelve lleno tras su espera. Si muere el Explorador, también se guarda.
  - **El Lobo merodea:** sin nadie a quien pegar, elige puntos alrededor del Explorador (entre 2.5 y
    6 m), camina hasta ahí, se queda 1 a 3 s y elige otro; si el Explorador se aleja, corre a
    alcanzarlo. Sección "Merodear (sin pelear)" del compañero (`Merodea alrededor del dueño`, prendido;
    no vale para el Oso, que se pone delante, ni para el Búho).
  - [x] Probar: un fantasma yendo por el Lobo; sacar al Lobo herido, poner al Oso, volver al Lobo (vuelve
        con lo que tenía, un poco más curado); el Lobo dando vueltas alrededor sin quedarse detrás.
- [x] **Ira primal (la definitiva) — HECHA Y PROBADA ✅ (15 de octubre).**
      Lo acordado con Gustavo: dura 10 s; salen los otros dos compañeros junto al elegido (y el elegido
      también si estaba muerto esperando: lo trae igual y al terminar sigue su espera donde estaba);
      los tres pelean con su comportamiento y su rango; atacan el doble de rápido ("Ira primal: entre
      ataques (×)" 0.5 en cada compañero); la Presencia bestial de los tres a la vez y al doble (cada uno
      aplica además una copia oculta de su presencia: +21 % velocidad, +20 % resistencia, ×0.81 entre
      ataques); no se puede cambiar de compañero mientras dura; al terminar los dos extra desaparecen y
      queda el elegido con la vida que tenía. Los extra salen llenos y, si mueren, no les empieza
      espera. Carga como las demás definitivas (Daño: por bajas). En el HUD, los tres se ven encendidos.
      Código: `CompanionManager` (StartFury / EndFury, extras), `Entity_Companion` (sección Ira primal;
      cada compañero sabe qué opción de la rueda es, para que el HUD encuentre al elegido entre tres),
      tag nuevo al final `Status_PrimalFury`.
  - [x] (Corrida y borrada el 15 de octubre.) Correr `Mercenarios ▸ Una vez ▸ Crear la Ira primal (definitiva del Maestro de bestias)` y borrar
        `MercPrimalFurySetup.cs`. Crea `GE_PrimalFury` (10 s, ícono primal fury), las presencias extra
        (`GE_Presence*Fury`, ocultas) y se las pone a cada compañero, `GA_PrimalFury` (copia de la definitiva
        del Guardián, con su espera de definitiva) en la R de `Class_BeastMaster`, bloquea la E durante la
        Ira primal y actualiza los registros de red.
  - [x] Probar: R con el Lobo elegido (salen Oso y Búho), con el elegido muerto (sale igual), que ataquen
        más rápido, la presencia doble en la barra del Explorador y de un aliado, que la E no cambie, y que
        al terminar quede solo el elegido con su vida. En un cliente, que el panel muestre bien la vida.
- [x] Después: el Trampero (ver su sección, arriba del Cazador). Hecho y probado el 15 de octubre.

---

# ★ Trampero (subclase del Explorador) — HECHO Y PROBADO ✅ (15 de octubre)

**La rueda ya no mueve la cámara** (pedido de Gustavo): con un menú que libera el cursor (la rueda),
`ThirdPersonOrbitCam` no gira con el mouse; la mira se queda donde estaba y lo que sale al soltar (una
trampa, un tótem) cae donde apuntabas al abrirla.
- [x] Probar: abrir la rueda (tótem del Chamán o compañero) y mover el mouse: la cámara no se mueve.

**Lo acordado hasta ahora** (15 de octubre):
- **Las trampas**: una pieza genérica en red (`Entity_Trap`, con su propio ASC como los tótems, así el
  empuje sale DESDE la trampa) configurada por datos. Se eligen en la rueda (E) y caen en el piso donde
  apunta la mira; 1 s para armarse; invisibles para los enemigos (las ve su equipo); saltan con un
  JUGADOR o bot enemigo cerca, **nunca con NPCs**; el daño y las bajas son del Trampero.
  Duran **3 minutos**, **hasta 6 puestas** (la séptima saca la más vieja) y **3 s de espera** entre una y otra.
  - Espinas: zona de 5 m, 4 s: ralentiza 40 %, 0.3× del ataque por segundo y una herida por segundo.
  - Frío: al pisarla se escarcha (1.5 s de aviso visible) y explota en 3 m: 1× del ataque y aturde 1.5 s.
  - Explosiva: explota en 4 m: 2× del ataque y repele 6 m desde la trampa.
- **Apuntado (clic derecho)**: la lanza del Comandante, "tribal" de mano y media: estocada en línea de
  **2.5 m**, ×1.1 del ataque, abre una herida; **crítica si el enemigo ya tiene una herida**.
- **Cazador paciente (pasiva)**: 4 s sin recibir ni hacer daño → invisible, +20 % velocidad, 2 % de vida
  por segundo; el primer golpe al salir es crítico seguro.
- **Bombas súper explosivas (R)**: con las mismas perillas que las zonas del Aliento de dragón (cuántas
  caen y cómo se reparten en el círculo, para ajustarlas en el Inspector). Las bombas son TRAMPAS de 60 s
  (menos que las otras) que explotan al pisarlas o al vencerse.
- [x] **Modelos de las bombas** (corrida y borrada; a Gustavo le encantaron y les puso base a las trampas normales): correr
      `Mercenarios ▸ Una vez ▸ Crear los modelos de las bombas (Trampero)` y borrar `MercTrapModelsSetup.cs`.
      Crea con primitivas `Art/Models/Traps/Bomb_Spikes`, `Bomb_Cold`, `Bomb_Explosive` y
      `Bomb_BlackPowder` (solo modelo, sin colliders ni scripts, ~0.5 m, pivote en el piso), con sus
      materiales en `Art/Materials/Traps`. Gustavo los retoca o los reemplaza.
- [x] **Código de la subclase** (15 de octubre):
  - `Entity_Trap` (Scripts/GAS/Entities): UNA pieza en red para todas las trampas, configurada por datos
    (se arma en, salta a, dura, aviso antes de explotar, radio, efectos del Trampero, efectos DESDE la
    trampa —el empuje—, zona con golpes cada tanto, explota al vencerse, cuenta para el límite). Lleva
    un ASC para que el empuje salga desde ella. Su equipo viaja en un SyncVar y cada pantalla dibuja el
    modelo solo si es de su equipo (o durante el aviso). Solo jugadores y bots la activan y la sufren.
  - `GA_PlaceTrap` (la rueda; límite por dueño, la más vieja se va), `GA_BombBarrage` (la R: zona en el
    piso; "Distribución de las bombas" al azar / en anillo / rellenar el círculo, "Cantidad", "Separación",
    "Radio del círculo", "Caen durante"; con gizmo), `PatientHunterPassive` (todos sus efectos llevan
    Status_Invisible: se cortan juntos al pelear).
  - **Campo nuevo en los efectos: "Crítico si el objetivo tiene"** (`GameplayEffect.CritIfTargetHas`):
    el daño de ese efecto sale crítico si el golpeado tiene el tag (la estocada: Status_Wound).
- [x] (Corrida y borrada el 15 de octubre.) Correr `Mercenarios ▸ Una vez ▸ Crear el Trampero (subclase del Explorador)` y borrar
      `MercTrapperSetup.cs`. Crea los efectos, las 4 trampas en `Prefabs/Summons` (con el modelo de
      `Art/Models/Traps` anidado), la rueda, la estocada, la definitiva, `TrapperBehaviours`,
      `Class_Trapper` con `ASDef_Trapper` y la lanza del Comandante, y lo agrega como subclase.
      Después: acomodar en `Class_Trapper` (Armas) la lanza guardada en la espalda.
- [x] **Después de la primera prueba (15 de octubre):** las trampas también se ponen en PAREDES (quedan pegadas a la superficie donde apunta la mira, con su base contra ella; mirando al aire caen al piso) y tardan **1.5 s** en armarse (antes 1 s), para que no exploten en el acto.
- [x] Probar: poner cada trampa (cae donde apuntabas), que el enemigo no la vea y el aliado sí, que
      salte solo con jugadores, la de frío con su aviso, la explosiva que empuja desde ella, el límite
      de 6 y los 3 s; la estocada (crítica contra un herido); quedarse 4 s quieto (invisible, más
      rápido, se cura; el primer golpe crítico); la R (las bombas, y que exploten al vencerse). En un cliente.

---

# ★ Cazador (subclase del Explorador) — HECHO Y PROBADO ✅ (15 de octubre)

Lo acordado con Gustavo (15 de octubre):

| Ranura | Qué hace | Cómo |
|---|---|---|
| Clic izq. — Arco largo | 3 fases a los 0 / 0.8 / 1.6 s; ×1.2 / ×2 / ×3 del ataque; vuela recto 30 / 55 m y la 3.ª no cae nunca | `GA_HunterBow` (copia del arco) con `GA_HunterArrow1..3` y `GE_ArrowDamage_Hunter1..3` |
| E — Presa | Marca 8 s al enemigo de la mira (hasta 30 m). Solo jugadores y bots, nunca NPCs. Tanque: sus golpes le hacen +2 % de su vida máx.; Daño: +3 % de su vida faltante; Soporte: −40 % de curación recibida. Si lo mata marcado, ese beneficio vale contra todos 6 s. Espera 15 s | `GA_Prey` (nueva) + `HunterPreyPassive` (nueva, el daño extra por el pipeline) + `GE_Prey`, `GE_PreyHealingCut`, `GE_PreyHunt` |
| Pasiva — Tácticas defensivas | Cada golpe recibido: +5 % velocidad y +5 % resistencia (6 s, máx. 3). Con las 3, la próxima Retirada lo vuelve invisible 3 s y las gasta | `DefensiveTacticsPassive` (nueva) + `GE_DefensiveTactics`, `GE_TacticsReady`, `GE_HunterVanish`; `GA_HunterRetreat` (copia de la Retirada con un "al activarse, solo si tiene Status_TacticsReady") |
| R — Depredador definitivo | 8 s: todos los jugadores y bots enemigos son su presa (los NPCs no) (cada uno según su rol), con la Marca del cazador y revelados a su equipo. Cada baja suma 3 s | `GA_ApexPredator` (copia de la Ira primal) + `GE_ApexPredator`; lo hace `HunterPreyPassive` |

Tags nuevos al final: `Status_Prey`, `Status_ApexPredator`, `Status_DefensiveTactics`, `Status_TacticsReady`.

- [x] (Corrida y borrada el 15 de octubre.) Correr `Mercenarios ▸ Una vez ▸ Crear el Cazador (subclase del Explorador)` y borrar
      `MercHunterSetup.cs`. Crea los efectos, el arco largo y sus flechas, la Presa, la Retirada del
      Cazador, el Depredador definitivo, `HunterBehaviours` (copia de las pasivas del Explorador con la
      Presa y las Tácticas), `Class_Hunter` con `ASDef_Hunter` (copia de las stats del Maestro de
      bestias) y lo agrega a las subclases del Explorador; actualiza los registros de red.
- [x] Probar: el arco largo (las 3 fases, el daño, la 3.ª que no cae y la curva); la Presa a un Tanque,
      a un Daño y a un Soporte (que se cure menos), que a un NPC no se pueda, y matarla (6 s contra todos); recibir 3
      golpes y la Retirada invisible; el Depredador (todos marcados y vistos por el equipo, una baja
      lo alarga). En un cliente.

---

# ★ Explorador (Ranger): kit base — HECHO Y PROBADO ✅ (13-14 de octubre; los bots, probados el 23); las 3 subclases, también: EL EXPLORADOR QUEDA COMPLETO

Pedido de Gustavo: primero el kit base, después la mascota del Maestro de bestias (el Lobo,
con una figura de primitivas mientras no haya modelo). Sin modelos ni animaciones nuevas: las
dagas del Pícaro hacen de espadas cortas y de arco, y los clips de lanzar hacen de tensar y
soltar; el proyectil es el de la daga, sin girar. Los íconos son prestados.

| Ranura | Habilidad | Cómo |
|---|---|---|
| Pasiva | Marca del cazador: cada impacto marca 4 s (+10 % de daño recibido) y su equipo lo ve a través de las paredes | `HuntersMarkPassive` (nueva) + `GE_HuntersMark`, en `ExplorerBehaviours` |
| Clic izq. | Arco corto: mantener carga en 3 fases (0 / 0.5 / 1 s): ×1 / ×1.6 / ×2.4 del ataque, 24 / 44 / 70 m, más rápido y con zoom; cargando camina más lento | `GA_ChargedAttack` con `GA_ExplorerArrow1..3` |
| Clic der. | Espadas cortas: un corte en arco corto | copia de `GA_DaggerCone` |
| Q | Ataque rápido: los próximos 3 disparos salen con la carga máxima al instante, y 6 s atacando más rápido (×0.6 entre ataques). 12 s | `GA_SelfBuff` + `GE_QuickShot` (3 acumulaciones) + `GE_QuickShotHaste` |
| Shift | Retirada: salta hacia atrás; donde estaba, daña y ralentiza. 7 s | `GA_LeapAttack` con impulso negativo y `Golpe al despegar` |

Lo nuevo de código, que sirve para más cosas:
- **Revelar a un equipo** (`NetworkAbilitySystemComponent.ServerRevealToTeam`): ese equipo ve la
  silueta del personaje a través de las paredes (`TeamReveal` + shader `Mercenaries/XRay`); los
  demás, no. Se agrega solo a todo personaje en red. Lo podrán usar el Búho y el Depredador
  definitivo.
- **Zoom por etapa** en el ataque cargado (`Zoom de cámara` de cada etapa) y la cámara con zoom
  (`ThirdPersonOrbitCam.ZoomFov`).
- **Carga completa con tag** en el ataque cargado: con el tag, suelta YA la última etapa y gasta
  una acumulación.
- **Golpe al despegar** en el salto (`GA_LeapAttack.HitAtTakeoff`), y el impulso negativo
  salta hacia atrás.
- Tags nuevos al final: `Status_QuickShot`, `Status_HuntersMark`.

- [x] **Correr `Mercenarios ▸ Una vez ▸ Crear el Explorador (kit base)`** (corrida el 13 de octubre; herramienta borrada). Crea los assets en
      `GameplayAbilities/Explorer` y `Attributes/Explorer`, agrega la clase al FINAL de
      `MainBaseClasses` del jugador y de la sala de la escena abierta, y actualiza los registros
      de red. **Guardar la escena**, y en las otras escenas con sala agregar `Class_Explorer` al
      final de `Clases elegibles` del `UI_LobbyPanel`. Después se borra `MercExplorerSetup.cs`.
- [x] **Armas del pack samurái** (tt-3d, importado por Gustavo; corrida el 13 de octubre, herramienta borrada): correr `Mercenarios ▸ Una vez ▸ Poner las armas
      samurái (arco, katana, kunai)`. Pasa sus materiales a URP, arma `Bow`, `Katana`, `ShortSword`,
      `Kunai` y `Spear` (la del Trampero) en `Art/Models/Weapons` con el tamaño y la dirección del arma que
      reemplazan, y `PF_Arrow` (la flecha vuela con su modelo, no con el arco: `GC_Projectile` tiene la
      casilla nueva `Usar el arma del que lo tira`). Explorador: la espada de siempre al 70 % en la derecha (`ShortSword`), arco en la izquierda;
      Samurái: katana; Shinobi: un kunai en cada mano. Después se borra `MercWeaponPackSetup.cs`.
- [x] **Íconos del Explorador** (corrida el 13 de octubre, herramienta borrada) (los puso Gustavo en `Art/Icons/Abilities/Ranger` y `Classes`): correr
      `Mercenarios ▸ Una vez ▸ Poner los íconos del Explorador` y borrar `MercExplorerIcons.cs`. Arco y
      flechas: Bow Attack; espadas: Slash attack; Ataque rápido y su buff: Arrows Rain; Retirada: Jump;
      la marca: Hunter's_Mark_Icon; la clase: Class_Ranger_Icon. Para las subclases quedan: Improved
      bow attack (el arco largo del Cazador), prey, Depredator, Ranger Invisibility (Tácticas defensivas o
      Cazador paciente), owl / wolf / bear / primal fury (Maestro de bestias), las bombas (Trampero), marked,
      y los íconos de clase Hunter, Beast Master y Trampero.
- [x] **Espada del Explorador a la espalda** (herramienta corrida y borrada) (pedido de Gustavo): va colgada del pecho y solo sale a la mano
      para el corte del clic derecho (campo nuevo de la clase `Sacar arma principal con tag`, con
      `Status_BladesDrawn` que da `GE_BladesDrawn` 1 s al cortar). Correr `Mercenarios ▸ Una vez ▸ Espada del
      Explorador a la espalda` y borrar `MercExplorerBladesStow.cs`. Ajustar en `Class_Explorer` (Armas) la
      posición y rotación de la espada guardada mirando en Play.
- [x] **Flechas hasta chocar y trayectoria al tensar** (herramienta corrida y borrada; falta probar la curva en un cliente) (pedido de Gustavo): las flechas vuelan RECTO hacia la mira lo que era el alcance de su fase (24 / 44 / 70 m; `GA_ProjectileShoot` ▸ `Vuelo recto`) y después caen, y
      vuelan hasta chocar (antes desaparecían en el aire a los 0.6-1 s), con la punta siguiendo la curva
      (`GC_Projectile` ▸ `Apunta hacia donde vuela`). Mientras tensa, el dueño ve la curva de la fase que
      saldría y un anillo donde cae (`TrajectoryPreview`, solo en su pantalla). Correr `Mercenarios ▸ Una vez ▸
      Flechas del Explorador hasta chocar` y borrar `MercRangerArrows.cs`. Probar: la curva coincide con
      donde cae la flecha de verdad, en las 3 fases y en un cliente.
- [x] Arreglados (falta probar): disparar enseguida después del cooldown ya no corta la carga nueva, y con
      Ataque rápido el arco dispara al apretar (sin pose ni barra de carga).
- [x] **Engancharse a una pared a mitad de la Retirada** (Gustavo le puso al Explorador el perfil de pared del
      Pícaro): antes el impulso de un salto de habilidad no dejaba engancharse. Ahora sí, y el salto termina ahí
      (`PlayerController.IsOnWall`). De paso, saltar hacia ATRÁS ya no da vuelta al personaje. Probar en un cliente.
- [x] Mirar en Play cómo quedan en la mano; si alguna está girada o corrida, se arregla UNA vez en el
      hijo de su prefab (vale para todas las clases que la usan).
- [x] Probar: el arco con las 3 fases (la barra de carga, el zoom, el alcance y el daño de cada
      una); soltar rápido sale la flecha corta.
- [x] Ataque rápido: los 3 disparos siguientes salen al instante con la carga completa; el
      ícono muestra cuántos quedan.
- [x] La Retirada: salta hacia atrás y daña y ralentiza a los que estaban pegados.
- [x] La marca: el enemigo golpeado se ve en rojo a través de las paredes para el Explorador y
      sus aliados (dos ventanas), y NO para su propio equipo ni para el tercero.
- [ ] Los bots: probar que jueguen el Explorador (que carguen y suelten el arco), el Monje y el Guerrero. **El Explorador (el arco a distancia y el Maestro de bestias con sus compañeros) y el Monje (las patadas, el Samurái y el Shinobi): PROBADOS ✅ (23 de octubre).** Falta el Guerrero.
- [x] Después: el Maestro de bestias y su Lobo (la mascota con IA en red). Hecho el 14 de octubre, con los tres compañeros (ver arriba).

---

# ★ Misma pared otra vez y espera por enemigo en las ráfagas — HECHO Y PROBADO ✅ (13 de octubre; probado el 15)

Pedidos de Gustavo después de probar el Monje y sus subclases.

- **Volver a la misma pared** (Monje y Pícaro). Después de SALTAR de una pared ya se puede
  engancharse otra vez a esa misma. Si se soltó SOLO (el Pícaro al terminar de resbalar, el
  Monje al llegar al final), esa pared sigue bloqueada hasta tocar el piso; si no, empujando
  contra ella quedaría colgado para siempre. Es la casilla nueva `Volver a la misma pared tras
  saltar` del perfil de pared (`WallMove_*`), prendida por defecto.
- **Espera por enemigo** en cada entrada de la lista de efectos de una habilidad (desplegando
  la flechita): después de aplicarle ese efecto a un objetivo, la habilidad no se lo vuelve a
  aplicar a ESE objetivo hasta pasados esos segundos; a los demás sí. Los pasos de un combo
  comparten la espera del combo, y dura entre un uso y el siguiente.
  - Ráfaga aturdidora: los 4 golpes ya llevaban el aturdido. Ahora cada uno puede aturdir a un
    enemigo distinto, pero al mismo solo una vez cada 2 s (con la Paz mental ya no lo deja
    aturdido para siempre).
  - Ráfaga de kunais del Shinobi: los 4 kunais hacen el daño de Artes oscuras (antes solo el
    primero), con 2 s de espera por enemigo.
- [x] **Correr `Mercenarios ▸ Una vez ▸ Espera por enemigo en las ráfagas con Ki`** (pone los
      2 s en los puñetazos de la Ráfaga aturdidora y en el kunai de Artes oscuras, y pasa los 4
      kunais de la Ráfaga a Artes oscuras). Después se borra `MercFlurryPerTargetSetup.cs`.
- [x] Probar: saltar de una pared, volver a ella y engancharse (Monje y Pícaro); resbalar hasta
      soltarse y que NO se vuelva a pegar a la misma.
- [x] Ráfaga aturdidora contra dos enemigos juntos: los dos quedan aturdidos. Con la Paz mental,
      ráfagas seguidas al mismo: entre una y otra hay un hueco.
- [x] Ráfaga de kunais contra dos enemigos: los dos reciben el daño extra; al mismo, una vez
      cada 2 s.
- **Escudos:** los cuatro (Guerrero, Paladín, Monje, Shinobi) ya cobran 5 de energía al
  levantarse, todos con `GE_Cost_Shield`.

---

# ★ Balance de curaciones, desarme, aturdido e invisibilidad — HECHO Y PROBADO ✅ (8 de octubre, noche; probado el 13)

Pedidos de Gustavo después de probar. Compila limpio. Hay que dejar que Unity recompile.

- **Clérigo, menos curación seguida:** `GA_HealingWord` y `GA_RadiantHealingWord` pasan de 4 a
  **3 cargas**, y cada carga tarda **4.5 s** en volver (antes 3 s). Lo usan también Vida y Orden.
- **Paladín, más curación por golpe:** el aura cura el **65 %** del daño hecho (antes 50 %),
  en `PaladinBehaviours` y en los Behaviours de las tres subclases. Lo bloqueado sigue en 20 %.
- **El Desarme no desarmaba.** El bloqueo estaba bien puesto (los ataques del Clérigo y del
  Paladín sí llevan `State_Disarmed`). Lo que fallaba era encontrar al objetivo: con un dueño
  REMOTO, el servidor lo busca con las posiciones que él tiene, que van atrasadas, y con 3 m de
  alcance contra alguien que se mueve no lo encontraba. El dueño veía el golpe (lo anticipa en
  su pantalla), pero el servidor rechazaba la habilidad y el efecto nunca entraba. Ahora, para un
  dueño remoto, el servidor acepta **1.5 m más de alcance y 10° más de ángulo**
  (`FindBestTargetInAim`). Sirve igual para todas las de objetivo único: Golpe mortal, Enemigo
  jurado, Intercepción, Imposición de manos, Aturdir...
- **Aturdido en todas las clases.** Todas tenían el clip (`HumanM@Stun01` en sus AOC). Lo que
  pasaba es que el aturdido va en la capa Base y la capa del torso (`UpperBody`: el ataque a
  medias, el escudo levantado, la reacción al golpe) y la del brazo del escudo (`OffHandPose`)
  se dibujaban encima: en las clases de escudo, o atacando, solo se aturdían las piernas.
  Ahora, mientras dura el aturdido, esas dos capas se apagan (`TickStunAnimation`,
  `TickStanceVisuals`).
- **Invisibilidad: a veces no te veías a ti mismo al salir.** La cámara esconde tu modelo
  dejando solo la sombra cuando se pega a una pared o mientras apuntas un lanzamiento. Si te
  volvías invisible en ese momento, el fantasma se guardaba "solo sombra" como estado original
  y te lo devolvía al salir: quedabas invisible para ti, visible para los demás. Ahora
  `PlayerVisibility` nunca devuelve "solo sombra" y la cámara sostiene lo suyo en cada frame.

**Qué probar:**

- [x] Clérigo: 3 curaciones seguidas y cada una vuelve a los 4.5 s. ¿Sigue curando demasiado?
- [x] **PROBADO ✅, se ve bien.** **VFX "Al curar" por habilidad (14 de octubre).** La Palabra de curación del Clérigo base
      no tenía ningún VFX propio desde la limpieza del 30 de septiembre (lo único que salía era el
      aura azul de la Bendición, y al curarte sin que te faltara vida, ni eso). Momento nuevo en
      la lista de VFX de cada GA: **"Al curar (en el aliado curado)"**. Sale solo sobre el aliado
      (o uno mismo) que esa habilidad cura, nunca sobre un enemigo golpeado, y una vez por golpe;
      lo detecta `ApplyHitEffects` (un efecto que sube la vida, `GameplayEffect.HealsHealth`).
      Cada habilidad elige el suyo: por ahora todas con "Healing buff" (pegado, 1 s) en la
      Palabra de curación, los tres arcos de luz, los dos Castigos (rayo) y la Imposición de
      manos (pasada de "al golpear" a "al curar"). Sin cargar a propósito: las zonas (Luz del
      amanecer, Fuego sagrado, Zona de verdad) y las curaciones con el tiempo (Palabra radiante,
      Quemadura santa), porque curan en cada tick y ya tienen el VFX de su efecto. El aura del
      Paladín no es un GA: sigue con su "Heal VFX". Probar: Palabra a uno mismo y a un aliado,
      el arco pasando por un aliado y un enemigo (solo el aliado), el Castigo, la Imposición.
- [x] Paladín: el número verde del aura al pegar, un poco más alto.
- [x] Con dos ventanas (o la prueba en red): el Desarme del Maestro de batalla a un Clérigo o
      Paladín que se mueve; el desarmado no puede atacar 2 s (sí bloquear y lanzar magia).
- [x] Aturdir a un Guerrero o Paladín con el escudo levantado, y a alguien a mitad de un ataque:
      todo el cuerpo hace la animación, y al terminar el escudo vuelve a su lugar.
- [x] Invisibilidad (Asesino, Shinobi): volverse invisible pegado a una pared o apuntando un
      lanzamiento, y salir de la invisibilidad: te tienes que volver a ver.

---

# ★ Inspector de habilidades nuevo — HECHO Y PROBADO ✅ (7 de octubre; probado el 15)

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
- [ ] Proyectil devuelto por el parry del Guardián: sigue haciendo el daño que traía. **Revisado el 15 de
      octubre: tenía dos fallas, arregladas; se prueba en su sección de arriba ("Parry del Guardián").**
- [x] Bots: siguen usando sus buffs (Comida de emergencia, Emboscada sombría) y sus aturdidos.

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

# ★ El juego en español e inglés — LAS 3 ETAPAS HECHAS Y PROBADAS ✅ (8 de octubre; probado el 15)

Pedido en la prueba: poder jugar en español, y que las builds arranquen en español. "Mercenaries"
(nombre provisional) no se traduce. Cómo funciona: ver CLAUDE.md ("Lo que ve el jugador va en
ESPAÑOL e INGLÉS"). En corto: cada texto tiene una clave, y la tabla
**`Assets/Resources/Localization.csv`** (columnas `key;es;en`) dice qué es en cada idioma. Se abre
con Excel: quien ayude a traducir no necesita Unity. Guardarla como **"CSV UTF-8"**.

**Etapa 1 — los menús (código hecho, compila limpio):**

- `Loc` + `LocalizedText` (carpeta nueva `Scripts/Localization`), la tabla con ~90 textos, y
  `GameSettings.Language` (**arranca en español**; se guarda como el resto de los ajustes).
- Ajustes: fila nueva **"Idioma · Language"** arriba de todo (dice lo mismo en los dos idiomas,
  para que la encuentre cualquiera). Al cambiarlo, todo lo que está en pantalla se reescribe en
  el momento. Las etiquetas largas se achican solas si no entran.
- Menú principal, Ajustes, recuadro de red (ESC) y la sala de espera (pestañas de rol incluidas)
  pasan por la tabla. "Restablecer" no cambia el idioma.
- [x] Probar: abrir el juego → todo en español; Ajustes ▸ Idioma → English cambia todo al momento
      (menú de atrás, sala, recuadro de red); cerrar y volver a abrir → se acuerda.
- [x] Mirar que ningún texto en español se salga de su recuadro (botones, encabezados de la sala).
- [x] Una build: que arranque en español y que se lean las tildes, la ñ y ¿ ¡ (la fuente las
      agrega sola con su fallback dinámico).
- [-] Fuera de la etapa: la sala VIEJA de la escena de pruebas (`UI_LobbyMenu`, textos fijos en
      su prefab) sigue en inglés. Si se usa, se le pone `LocalizedText` a cada texto.

**Etapa 2 — la partida (código hecho; compila con Unity y con el codegen de red):**

- Pasan por la tabla: el marcador de arriba (equipos, nivel, reloj, "PREPÁRATE", quién gana,
  la línea del Objetivo), el anunciador (todos los avisos grandes), el marcador del Objetivo en
  pantalla, el registro de bajas, la pantalla de muerte, los avisos del menú de clases, "¡NIVEL
  MÁXIMO!", la rueda (Cancelar / En recarga), "Interrumpido" de la barra de casteo, la definitiva
  ("LISTA", "¡DEFINITIVA LISTA!"), la etiqueta de los botiquines (+75 vida), los motivos del
  desatascarse, la ayuda del espectador y los botones de teclado que se dibujan (Clic I / Clic D,
  Espacio, Clic). Los de control (RB, R1, VIEW...) se quedan igual en los dos idiomas.
- **El servidor ya no manda textos escritos** en lo que es de la partida: el registro de bajas y
  la pantalla de muerte mandan el nombre del jugador, el índice de su clase y qué NPC es (el
  Jefe, un Mago...); el desatascarse manda la clave del motivo. Cada pantalla lo escribe en SU
  idioma: en una partida mezclada, cada uno lo ve en el suyo. (Cambiaron tres RPC.)
- Quitada la fila de **Calidad** de Ajustes (pedido de Gustavo: no hay niveles de calidad
  pensados para elegir).
- [x] Probar en partida, en español y en inglés: el marcador, los avisos, el Objetivo, morir
      (contra un jugador y contra un NPC), el registro de bajas, la definitiva lista, la rueda.
- [x] Con dos ventanas en idiomas distintos: cada una ve las bajas en el suyo.
- [x] Que "EQUIPO 1 (TÚ)" entre en el marcador (se achica solo si no).
- [-] Lo que todavía sale en inglés es CONTENIDO (etapa 3): los nombres de clases (en las bajas,
      las tarjetas, la sala), los nombres de habilidades (la barra de casteo) y las opciones de
      la rueda (las mascotas del Explorador...).

**Lo que falta:**

- [x] **Etapa 3 — el contenido: CÓDIGO HECHO (8 de octubre), falta importar y probar.**
  Se eligió la propuesta: **el español va en el propio asset**, al lado del inglés. Así no se
  rompe al renombrar un asset y se edita donde se crea la habilidad.
  - **Campos nuevos:** `ClassNameEs` y `DescriptionEs` en cada `Class_*` (sección Identidad),
    `AbilityNameEs` en cada GA (sección General), `NameEs` y `DescriptionEs` en cada tótem de la
    rueda del Chamán. Vacío = sale el inglés. Lo que ve el jugador sale de `DisplayName` /
    `DisplayDescription` (`Loc.Pick`): la sala, el menú de clases, la elección de subclase,
    el registro de bajas, la pantalla de muerte, la barra de carga y la rueda.
  - **La barra de casteo ya no recibe el nombre escrito:** viaja el índice de la habilidad en
    `GameplayAbilityRegistry` y el dueño escribe el nombre en su idioma (cambió la RPC
    `TargetShowCastBar`: Unity tiene que recompilar).
  - **Los textos en español ya están escritos** en `DesignDocuments/Traduccion_Contenido.csv`
    (242: las 25 clases con su descripción, las 184 habilidades y los 4 tótems), con los
    nombres del documento de diseño. Cada fila va por el GUID del asset, no por su nombre.
  - **Herramienta nueva:** `Mercenarios ▸ Idiomas ▸ Importar nombres traducidos` escribe la
    columna `es` en los assets; `Exportar nombres para traducir` regenera el CSV con lo que haya
    (para ver qué falta o pasárselo a quien traduzca). Una celda `es` vacía no toca el asset.
  - Las habilidades nuevas: llenarles `Ability Name Es` al crearlas (o exportar, completar e
    importar).
- [x] **(Hecho el 8 de octubre: importados en los 209 assets.)** **Una vez, con Unity abierto (después de que recompile):** `Mercenarios ▸ Idiomas ▸
      Importar nombres traducidos`. La consola dice cuántos cambió (tienen que ser 242). Revisar
      los nombres en el CSV o en los assets: los internos (pasos de combo, variantes) no se ven en
      pantalla, pero también quedaron traducidos.
  - [x] Probar: elegir clase y subclase, morir, canalizar y abrir una rueda, en los dos idiomas;
        con dos ventanas en idiomas distintos, la barra de casteo de cada uno en el suyo.
- [x] **El Inspector en español — HECHO Y REVISADO ✅ (13 de octubre: "están todos correctos").** Pedido de Gustavo,
      para quienes le ayuden. Cada campo de los GA, GE, clases, ASDef y perfiles de pared tiene su
      nombre en español con `[Label("...")]` (331 campos); el código y los assets siguen igual
      (renombrar borraría lo guardado). El tooltip no cambió, y el buscador del inspector encuentra
      el nombre en español y el de siempre. Las entradas de las listas (opciones de tótem, etapas de
      carga, pasos de combo, variantes por tag) también, con `LabeledFieldsDrawer`. Los menús
      desplegables también: atributos (Vida máxima, Defensa...), botones (Clic izquierdo, Q...), rol,
      cómo se acumula, tipo de efecto, dirección del empuje, dónde aparece el área, a quién apunta.
      Los tags (`State_Stunned`...) quedan como están: son los nombres que usan el código y los
      documentos.
  - [x] Abrir unos cuantos (una habilidad de cada tipo, un efecto, una clase) y ver que ningún nombre
        quede cortado y que se entienda. Si alguno no te gusta, se cambia en su `[Label]`.
  - [x] **Los componentes también — HECHO, falta mirarlo (13 de octubre).** 644 campos más en ~75
        componentes nuestros: pasivas, tótems, proyectiles, barreras, el `PlayerController`, la
        cámara, los bots y NPCs, el modo de juego y toda la UI. Lo dibuja `LabeledComponentEditor`, el
        inspector general de los MonoBehaviour: mismo orden, `[Header]`, `[Range]` y tooltips de
        siempre; un script sin `[Label]` (los de los packs, los de Unity) se ve exactamente igual que
        antes. Las entradas de lista de la aura del Paladín, las habilidades extra de los NPCs y las
        reglas de aparición, con `LabeledFieldsDrawer`. Quedan fuera los restos de la jam (`48toPlay`).
  - [x] Mirar unos cuantos prefabs (el jugador, un tótem, una pasiva, un NPC, el HUD) y que nada se
        vea raro ni cortado.

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
- [x] Mirar: la quemadura del Monje roja y la del Clérigo como siempre; las zonas, los orbes y su
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
- [x] En un cliente: las zonas caen donde mira él y se ven en las dos ventanas.
- [x] ¿Se ve amarillo el fuego? (si las fogatas amarillas de Hovl no alcanzan, se le cambia el color).

---

# ★ Después de probar el Maestro elemental y el Shinobi — HECHO Y PROBADO ✅ (8 de octubre; probado el 15)

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

- [x] Wall jump con Pícaro y Monje: al aterrizar se frena ahí, sin resbalar.
- [x] Paz mental + Defensa de agua varias veces: un solo buff en la barra, que se reinicia.
- [x] Aliento de dragón rojo: 3 golpes en 3 s, una zona por golpe donde mirás; girando, el cono y
      las zonas siguen la mira; cada zona dura 5 s y suma una quemadura por segundo (hasta 5).
      En un cliente (no el host) también: que las zonas caigan donde mira ÉL. **PROBADO ✅ en el
      cliente (8 de octubre).**
- [x] Aturdido o silenciado a mitad del aliento: se corta (barra roja) y puede volver a usar todo.
- [x] Asesino: invisible, el aviso del crítico mejorado vuelve antes; salir de la invisibilidad
      y matar a alguien (o un NPC) en 6 s → la Emboscada lista; pasados los 6 s, no.
- [x] Shinobi: humo al ocultarse, en las dos puntas del Paso de las sombras y en cada salto
      oculto (piso y pared), en las dos ventanas.

---

# ★ Paredes (Pícaro y Monje), Ki, Ráfaga y escudos — HECHO Y PROBADO ✅ (8 de octubre; probado el 15)

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
  - [x] Probar: pegado, mirar a la izquierda o la derecha y saltar → sale torcido hacia ese lado.
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

- [x] Pícaro: saltar contra una pared → se pega 1 s, resbala y se suelta; Espacio pegado →
      salta hacia afuera; entre dos paredes enfrentadas, subir saltando de una a otra; que no
      se vuelva a pegar a la misma sin tocar el piso.
- [x] Monje: correr a lo largo de una pared, que baje de a poco y termine en el piso; que la
      pared se acabe a mitad de camino; saltar hacia afuera; la patada desde la pared.
- [x] Que NO se pegue a: los límites invisibles de la arena, otros jugadores, tótems, la barrera
      de un escudo.
- [x] Atacar pegado (dagas, puños, el escudo); aturdido o empujado pegado → se cae.
- [x] Con dos ventanas: que el otro vea la pose (Monje corriendo inclinado, Pícaro pegado).
- [x] La Ráfaga con 4 golpes (¿se ve bien a esa velocidad?) y el Ki con 2 cargas.
- [x] Escudos: levantar cobra 5 de energía; con 5 o menos no se levanta.
- [-] Animaciones de pared propias: no hay en el pack de Kevin Iglesias (se usan caer y correr);
      del artista si las hace.

---

# ★ Samurái (subclase del Monje) — HECHO Y PROBADO ✅ (8 de octubre), la franja del Corte final también

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
- [x] **R: la franja NO se veía** (8 de octubre). No era la franja: el marcador del piso
      (`UI_GroundTargetIndicator`) no estaba puesto en NINGUNA escena ni prefab, así que ninguna
      habilidad de zona mostró nunca nada. Ahora se crea solo y dibuja una caja rellena con borde
      (la franja) o un círculo relleno con borde (las zonas). Probar: la franja sigue la mira, se
      corta en las paredes, marca a los enemigos de adentro (y no a un invisible); al soltar los
      atraviesa a todos. Un enemigo con la marca que baja al 5 % muere y la baja es del Samurái.
- [x] De paso, ahora SÍ se ve el círculo de las demás habilidades de zona: Marcado para morir del
      Asesino, Salto heroico del Comandante, las áreas "en la retícula".
- [x] Las patadas del Samurái ponen herida (las dos, la voladora y la del dragón, ya llevan GE_Wounds al golpear; revisado el 14 de octubre).
- [-] ⚙ Animaciones: el corte giratorio usa el clip del molinete (`MagicAttackOmni01`) y el Corte
      final el corte del Guerrero; cambiarlos si hay algo mejor.

---

# ★ Maestro elemental y Shinobi (subclases del Monje) — HECHOS Y PROBADOS ✅ (8 de octubre; probado el 15)

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

- [x] Maestro elemental: los orbes salen con cada puñetazo y pegan de lejos (daño mágico).
- [x] Ráfaga aturdidora con 0, 2 y 4 acumulaciones: no aturde / 1 s / 2 s; después tiene 4.
- [x] Defensa de agua: los primeros 2 s los golpes curan (número verde); después frena como siempre.
- [x] Patada del viento: atraviesa a varios, los empuja y los aturde.
- [x] Aliento de fuego: el cono quema. Con Ki: ver el Aliento de dragón rojo sostenido arriba
      (¿se ve bien el VFX de la zona y del tamaño correcto?).
- [x] Paz mental: 6 s en los que clic izq./der., Shift y E salen con Ki sin gastar el Ki.
- [x] Shinobi: kunais por la espalda hacen crítico (número amarillo). Con Ki, el primero de la
      Ráfaga hace más daño con más acumulaciones.
- [x] Bloqueo / Defensa paciente: un hacha, una daga o un arco lo atraviesan sin dañarlo (y
      siguen); un golpe cuerpo a cuerpo se frena normal.
- [x] Manto: invisible, rápido, el primer golpe crítico; 2 cargas. Con Ki, Paso de las sombras:
      el doble de lejos; mirando una pared, aparece pegado a ella.
- [x] Muerte silenciosa con 3+ enemigos: salta de uno a otro, primero jugadores y el más herido,
      a cada uno una vez; nadie le hace daño mientras dura.
- [x] En red (dos ventanas): los orbes y kunais se ven, los saltos de la Muerte silenciosa, la zona
      de fuego.
- [-] ⚙ Animaciones: el Aliento usa `MagicAttackDirect1H01_R` y la Muerte silenciosa el golpe del
      cono de dagas; los kunais, el lanzamiento de dagas (el mismo clip para los dos).

---

# ★ Menús de clase más compactos e ícono de clase en las barras — HECHO Y PROBADO ✅ (8 de octubre; probado el 15)

Pedido de Gustavo: con más clases todo se estiraba. Compila limpio. No toca prefabs ni assets:
todo se arma por código.

- **Sala (pantalla principal): clases agrupadas por rol.** Arriba del selector, una pestaña
  por rol del color del rol —`Tanks`, `Damage`, `Supports`, y `Other` si alguna clase no tiene
  rol— con cuántas clases tiene; debajo, solo las del rol abierto. Se abre en la pestaña de tu
  clase. Una clase BASE no tiene rol (no tiene definitiva): aparece con el de la mayoría de sus
  subclases (`CharacterClassDefinition.DisplayRole`). Hoy: Bárbaro y Guerrero en Tanks, Pícaro
  en Damage, Paladín y Clérigo en Supports.
- [x] **El Monje no tiene subclases todavía, así que sale en `Other`: ponerle `Menu Role` =
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

- [x] La sala: las pestañas cambian las clases, el recuadro no salta de tamaño, elegir y
      confirmar sigue funcionando (también eligiéndole clase a un bot).
- [x] El menú C con 6 clases en dos filas de 3; el V con las 3 subclases en una fila; las
      teclas 1–6 y el control siguen eligiendo la tarjeta correcta.
- [x] El ícono al lado de la barra: que se vea en aliados y enemigos, que cambie al cambiar de
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
**6 de octubre:** barra de carga por etapas del ataque cargado (`UI_ChargeBar`), repeler / atraer y vuelo libre: hechos y probados. **Monje: kit base hecho y probado (8 de octubre). Barra de canalizar (`UI_CastBar`): hecha.** Queda la mascota con IA en red. **13 de octubre: Explorador (Ranger), kit base hecho; sigue el Maestro de bestias con su Lobo (la mascota con IA en red). Documentos al día (diseño de clases, arquitectura del GAS y de red, guía y glosario).** **14 de octubre: Maestro de bestias (los tres compañeros) y la UI del jugador pasada al prefab de la cámara (barras, avisos de combate, viñeta, rueda, curva de los lanzamientos y silueta); la curva sale ahora en todos los lanzamientos apuntados. Documentos al día.** **15 de octubre: probado todo lo de la UI (las 6 etapas) y lo viejo pendiente; variables claras con su unidad en los 1 247 campos del Inspector; la Ira primal (la definitiva del Maestro de bestias), falta probarla; 50 de experiencia por eliminar a un jugador. Cazador (subclase del Explorador) hecho y probado. Trampero hecho y probado (trampas en el piso o en paredes, se arman en 1.5 s; la rueda deja la cámara quieta): **el Explorador queda completo** (kit base y las 3 subclases). Documentos al día.** **19 y 20 de octubre: canalizar en cualquier habilidad (el Haz guía canaliza 1 s), el choque de los proyectiles por barrido (caja al tamaño del arma), el desatascarse guardado en Unstuck.log, el parry devuelve el daño que traía, la BASE DE INVOCACIONES (Entity_Summon + Summon_Zone: tótems, trampas como variantes, cuchilla, copia y compañero ya la usan), un solo prefab para las barras de vida (la copia muestra la del copiado) y las invocaciones bien apoyadas bajo techo. Documentos al día.** **20 de octubre (tarde): el plan del Mago, decidido con Gustavo, y su etapa 1 armada (la clase, el maná, los misiles, los puntos arcanos con sus bolas y el vuelo); falta probarla.**

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
   - [x] **Jueves 9:** probar el aturdido mientras se apunta (no lanza ni cobra), con
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
   - [x] Probar con el Golpe final (y cortarlo con un aturdido) y con el molinete.

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
   - [x] Canalizar como MECÁNICA genérica (un tiempo de lanzamiento antes de que salga
         cualquier habilidad, que se corta si te aturden). **Hecho el 19 de octubre: sección
         Canalizar de cualquier GA (ver arriba).**

2. **El mapa del cementerio — JUGADO Y APROBADO ✅ (20 de octubre: Gustavo lo probó varias veces, le gusta y ya le puso texturas).** Escena
   nueva `Scenes/Mercenaries_Graveyard.unity` (copia de la del modo, con sala, red y menús;
   la vieja no se tocó). Ya la estás editando a mano. Detalle, rutas
   medidas y lo que falta en la sección 7.
3. **Sonido**: el sistema está; faltan los clips (y volver a prender la sección de
   sonido de Ajustes).
4. **Animaciones que se ven raras** (sección 5).
5. **Quedarse sin control al spawnear**: cuando pase, traer `Logs/Unstuck.log` del host (se guarda solo).

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
- [ ] Cuando alguien se atasque de verdad: traer `Logs/Unstuck.log` del host (desde el 19 de
      octubre se guarda solo, con el lado del cliente y el del servidor juntos).

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

**Cerrado por Gustavo el 15 de octubre:** todo lo de esta lista quedó visto y decidido, igual que el
mapa del Cementerio (recorrido, rutas con bots, NavMesh, techo) y el arte (lo lleva el artista).

Son las perillas y dudas que quedaron abiertas y que solo se contestan jugando:

- [x] **Subida a 6 s (30 de septiembre, `MercObjective.DeliverSeconds`).** **La entrega de 3 s**: ¿los rivales llegan a cortarla? ¿3 s es poco o mucho?
      ¿Una herida o un veneno encima reiniciándola se siente justo o frustrante? De
      paso: salir de la zona a mitad vuelve la barra a 0, y en un cliente la barra
      avanza suave, no a saltos.
- [x] **El mapa**: con la entrega de 3 s, ¿se sigue sintiendo chico? Si sí, la regla
      siguiente es que cargar la bolsa bloquee el Shift (sección 7).
- [x] **La definitiva por rol**: ¿llega a tiempo para usarla en la partida? ¿Qué rol la
      carga demasiado rápido o demasiado lento?
- [x] **La retroalimentación del combate**: la X, la calavera y el círculo de daño
      (`DamageRingRadius` 190). ¿Se entienden? ¿El círculo molesta?
- [x] **El Asesino invisible** (`GhostAlpha` 0.35): ¿el que lo usa entiende que es
      invisible?
- [x] **Apuntar al objetivo** (Blink, Enemigo jurado, Intercepción): ¿demasiado exacto
      contra alguien que se mueve?
- [x] **Botiquines**: ¿se usan? ¿están donde pasa la gente?
- [x] **La red con 9**: animaciones de los demás (ataques, lanzamientos, torso), lag,
      desconexiones, y los FPS del host.
- [x] **Blink** (nuevo, sin probar): al aparecer en la espalda, la cámara gira hacia el
      enemigo y le puedes seguir pegando. ¿Marea o se siente bien?
- [x] **Tótems del Chamán** (nuevo, sin probar): ahora tienen collider y barra de vida
      (roja para los rivales, verde para los aliados) y se pueden romper. Romper uno da
      15 de experiencia al equipo. Los de la definitiva son indestructibles
      (inmunidad real: su barra se queda llena). El equipo del tótem viaja por la red.
- [x] **La retroalimentación nueva con 9** (sección 8, probada solo de a pocos):
      ¿los números de daño se leen o ensucian la pelea grande? ¿El registro de bajas
      tapa algo? ¿La viñeta roja molesta? ¿La pantalla de muerte dice lo que hace falta?
      ¿Alguien no entendió algún aviso por estar en inglés?
- [x] **El Chamán y la velocidad de ataque**: los bonos ahora se multiplican y el tope es
      0.3 s. ¿El Chamán con Enfurecer + Tigre se siente bien, o quedó lento? ¿El
      Berserker sigue rápido? (ver sección 3, velocidad de ataque)
- [x] **Balance**: qué clase o subclase dominó, cuál no eligió nadie, qué se sintió
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
- [x] Decidir los valores del Tigre: tu 0.9 / 0.8 era para frenar la suma. Multiplicando,
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
- [x] Mirar si el círculo (`DamageRingRadius`, 190) queda donde molesta o donde se ve.
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
- [x] **Mascota con IA en red** — Explorador (Maestro de bestias); sirve para futuros summons. Hecha el 14 de octubre: `Entity_Companion` + `CompanionManager` (ver arriba)

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
- [x] Antes del Mago: definir la extra de Filo danzante — DECIDIDO (13 de octubre): **Espada mágica** (Magic blade): por un tiempo sus ataques hacen daño MÁGICO; sin ella, la espada solo hace daño físico. Ya está en el documento de diseño de clases.

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
- [x] NavMesh con niveles: NavMesh Links para bordes y saltos; los bots ya batallaban
      saltando
- [x] Subir `MercArenaBounds.CeilingHeight` (20 m): el salto del Bárbaro sube ~11.5 m, y
      desde una torre de +8 m llega a ~19.5 m
- [x] Túneles anchos: la cámara en tercera persona sufre en lo estrecho
- [x] Mover bases, campamentos, botiquines y plataformas de entrega
- [x] Greybox con cubos y probar con bots ANTES de decorar
- Estimación: 1–2 semanas de editor. Compite con Clérigo + Guerrero (sección 6).

## El Cementerio de los Tres Panteones — el mapa para la demo (bosquejo del 24 de sept.) — JUGADO Y APROBADO ✅, ya con texturas (20 de octubre)

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
- [x] **Abrir la escena en el editor, darle Play y recorrerla** (ver abajo qué mirar)
- [x] Probar con bots cuánto tardan en volver por cada ruta, y jugarlo, ANTES de decorar
- [x] Decoración al final: nichos, cruces, velas, pétalos

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

- **En Build Settings es la ÚNICA escena activa** (revisado el 20 de octubre): la build abre el cementerio.
  Las otras dos (el mapa viejo y Test_Network) siguen en la lista, desactivadas.
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
