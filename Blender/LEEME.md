# Modelos de clase en Blender

Desde el 10 de noviembre de 2026 los modelos de las clases se hacen en Blender (empezando por la
Paladina), en vez de las recetas de `Assets/Scripts/Editor/ClassModels`. Blender 4.4 está en `H:\blender.exe`
en la PC de casa.

## Qué hay acá

| Archivo | Qué es |
|---|---|
| `merc_kit.py` | Las herramientas: el esqueleto del Dummy, mallas, ribetes de oro, símbolos, pesos por hueso. |
| `merc_export.py` | Junta las piezas, hornea las texturas y escribe `Out/<Clase>/`. |
| `paladin.py` | La receta de la Paladina. Arma `Paladin.blend` y lo exporta. |
| `cleric.py` | El Clérigo leonino. Reusa el cuerpo y la ropa de `paladin.py` (más ancho) y agrega cabeza de león, melena, cola y garras. |
| `ranger.py` | El Explorador elfo tribal, en verdes. Reusa el cuerpo, la cabeza y el pelo de `paladin.py` con otra cara (ojos y pintura tribal que brillan) y agrega orejas, hojas y carcaj. |
| `Paladin.blend` | El modelo, pieza por pieza, para abrirlo y retocarlo a mano. |
| `Out/<Clase>/` | Lo que lee Unity: `<Clase>.json` (la malla), `_Atlas.png` y `_Glow.png`. Las `preview_*.png` y `posed_*.png` muestran cómo quedó (no se suben). |

## Rehacer la Paladina desde cero

```bash
"H:/blender.exe" -b --factory-startup --python Blender/paladin.py
```

Tarda un minuto y medio (el horneado). Con `-- --preview` al final solo arma y saca fotos
(`Out/Paladin/shape_*.png`), sin hornear: sirve para probar formas rápido.

## Retocarla a mano en Blender

1. Abrir `Paladin.blend`. Cada pieza es un objeto de la colección **Model**.
2. Mover vértices, cambiar modificadores, borrar o agregar piezas. Una pieza nueva necesita la propiedad
   personalizada **`merc_skin`** (ver `merc_kit.py`: `torso`, `arm:.L`, `leg:.R`, `rigid:B-head`…), que
   dice a qué huesos sigue. Los pesos se calculan al exportar, así que no hay que pintarlos.
3. Guardar y exportar:

```bash
"H:/blender.exe" -b Blender/Paladin.blend --python Blender/merc_export.py
```

Ojo: si después se corre `paladin.py`, rehace el `.blend` desde cero y se pierden los retoques a mano.

## Llevarla al juego

En Unity: **Mercenarios ▸ Modelos de clase ▸ Importar los modelos de Blender**. Pisa el prefab
`Prefabs/Player/Classes/Model_<Clase>` y su malla, textura y material en `Art/Models/Classes/<Clase>`,
conservando los GUID: el campo "Modelo" de la clase sigue apuntando y no hay que tocar nada más.

Antes de guardar, revisa que los huesos de Blender estén donde los tiene el jugador; si no, avisa en la
consola y no importa nada.

## Reglas

- Se modela en la **pose T del Dummy**, en el espacio de Unity (`+X` derecha del personaje, `+Z` su frente).
- No pasa por FBX: el importador de FBX de Blender cambia la orientación de los huesos.
- El alfa del atlas es el **pulido** (acabado Polished): 1 − la aspereza del material en Blender.
- Lo que brilla con luz propia va en la emisión del material. El juego no tiene bloom: con mucha intensidad
  se vuelve blanco.
