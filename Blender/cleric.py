# -*- coding: utf-8 -*-
# ============================================================
# cleric.py
#
# El Clérigo en Blender (10 de noviembre de 2026, referencias que mandó Gustavo): un leonino con ropa de
# clérigo. Cabeza de león con melena grande y dos trenzas con cuentas de oro; sobretodo largo color crema con
# una franja turquesa y un sol de oro en el pecho; estola alrededor del cuello con las puntas colgando; una
# sola hombrera de oro (la izquierda) con un medallón turquesa; cota de malla debajo; cinturón de cuero con
# bolsas; brazales de cuero; pantalón oscuro y botas; capa crema con forro turquesa. Manos con garras y cola.
# Sin maza ni escudo: son de la clase.
#
# El cuerpo y las piezas de ropa salen de paladin.py (torso, miembros, capa, mechones), con las medidas de
# un cuerpo más ancho. Lo propio del león (cabeza, melena, cola, garras) está acá.
#
# Correr (arma Cleric.blend, hornea y exporta a Out/Cleric):
#   "H:/blender.exe" -b --factory-startup --python Blender/cleric.py
# Solo armar y sacar fotos, sin hornear:  agregar  -- --preview
# Después, en Unity: Mercenarios ▸ Modelos de clase ▸ Importar los modelos de Blender, y poner el prefab
# Model_Cleric en el campo "Modelo" de Class_Cleric.
# ============================================================
import math
import os
import random
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import merc_kit as K   # noqa: E402
import paladin as P    # noqa: E402
from merc_kit import V, Keys, Mesh, smooth, bell, lerp, wrap_deg   # noqa: E402

NAME = "Cleric"
rng = random.Random(7741)

# =========================================================
# EL CUERPO: más ancho que el de la Paladina
# =========================================================

def scaled(keys, factor):
    pairs = []
    for x, y in zip(keys.x, keys.y):
        pairs += [x, y * factor(x)]
    return Keys(*pairs)


def use_leonin_body():
    """Las piezas de paladin.py leen estas tablas al armarse: se cambian por las de un cuerpo masculino."""
    P.TorsoWidth = scaled(P.TorsoWidth, lambda y: lerp(1.06, 1.17, smooth(1.12, 1.33, y)) * lerp(1.0, 1.25, smooth(1.47, 1.55, y)))
    P.TorsoDepth = scaled(P.TorsoDepth, lambda y: 1.1 * lerp(1.0, 1.2, smooth(1.47, 1.55, y)))
    P.ArmRadius = scaled(P.ArmRadius, lambda d: 1.22)
    P.LegRadius = scaled(P.LegRadius, lambda d: 1.12)
    P.bust = lambda y, deg: 0.0


# =========================================================
# MATERIALES
# =========================================================

M = {}
FUR = (0.80, 0.60, 0.36)
MUZZLE = (0.96, 0.89, 0.76)


def gradient_nodes(mat, root, mid, tip, top_z, bottom_z, streaks=(90.0, 90.0, 5.0)):
    """Un color que va de la raíz a la punta según la altura, con mechas (pelaje y melena)."""
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    geo = K.node(nt, "ShaderNodeNewGeometry", (-900, 200))
    sep = K.node(nt, "ShaderNodeSeparateXYZ", (-700, 200))
    nt.links.new(geo.outputs["Position"], sep.inputs[0])
    rng_ = K.node(nt, "ShaderNodeMapRange", (-500, 200))
    rng_.inputs["From Min"].default_value = top_z
    rng_.inputs["From Max"].default_value = bottom_z
    nt.links.new(sep.outputs["Z"], rng_.inputs["Value"])
    ramp = K.node(nt, "ShaderNodeValToRGB", (-300, 200))
    cr = ramp.color_ramp
    cr.elements[0].color = (*K.srgb(root), 1)
    cr.elements[1].color = (*K.srgb(tip), 1)
    cr.elements.new(0.4).color = (*K.srgb(mid), 1)
    nt.links.new(rng_.outputs["Result"], ramp.inputs["Fac"])

    scale = K.node(nt, "ShaderNodeVectorMath", (-700, -100))
    scale.operation = 'MULTIPLY'
    scale.inputs[1].default_value = streaks
    nt.links.new(geo.outputs["Position"], scale.inputs[0])
    noise = K.node(nt, "ShaderNodeTexNoise", (-500, -100))
    noise.inputs["Scale"].default_value = 1.0
    noise.inputs["Detail"].default_value = 3.0
    nt.links.new(scale.outputs[0], noise.inputs["Vector"])
    streak = K.node(nt, "ShaderNodeMapRange", (-300, -100))
    streak.inputs["From Min"].default_value = 0.3
    streak.inputs["From Max"].default_value = 0.7
    streak.inputs["To Min"].default_value = 0.8
    streak.inputs["To Max"].default_value = 1.1
    nt.links.new(noise.outputs["Fac"], streak.inputs["Value"])
    comb = K.node(nt, "ShaderNodeCombineColor", (-200, -250))
    for i in range(3):
        nt.links.new(streak.outputs["Result"], comb.inputs[i])
    mul = K.node(nt, "ShaderNodeMix", (-100, 100))
    mul.data_type = 'RGBA'
    mul.blend_type = 'MULTIPLY'
    mul.inputs["Factor"].default_value = 1.0
    nt.links.new(ramp.outputs["Color"], mul.inputs[6])
    nt.links.new(comb.outputs[0], mul.inputs[7])
    nt.links.new(mul.outputs[2], bsdf.inputs["Base Color"])


def chain_nodes(mat):
    """La cota de malla: anillos oscuros con un poco de brillo en el centro."""
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    geo = K.node(nt, "ShaderNodeNewGeometry", (-900, 200))
    vor = K.node(nt, "ShaderNodeTexVoronoi", (-650, 200))
    vor.inputs["Scale"].default_value = 260.0
    nt.links.new(geo.outputs["Position"], vor.inputs["Vector"])
    ring = K.node(nt, "ShaderNodeMapRange", (-400, 200))
    ring.inputs["From Min"].default_value = 0.15
    ring.inputs["From Max"].default_value = 0.55
    nt.links.new(vor.outputs["Distance"], ring.inputs["Value"])
    mix = K.node(nt, "ShaderNodeMix", (-200, 200))
    mix.data_type = 'RGBA'
    mix.inputs[6].default_value = (*K.srgb((0.62, 0.64, 0.68)), 1)
    mix.inputs[7].default_value = (*K.srgb((0.16, 0.17, 0.19)), 1)
    nt.links.new(ring.outputs["Result"], mix.inputs["Factor"])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])


def materials():
    M["fur"] = K.material("Fur", FUR, rough=0.85)
    M["mane"] = K.material("Mane", (0.62, 0.40, 0.18), rough=0.75)
    M["face"] = K.material("LionFace", FUR, rough=0.8)
    M["ear_in"] = K.material("EarInner", (0.45, 0.28, 0.22), rough=0.85)
    M["cream"] = K.material("Cream", (0.91, 0.87, 0.77), rough=0.9)
    M["cream_in"] = K.material("CreamInner", (0.74, 0.69, 0.60), rough=0.92)
    M["teal"] = K.material("Teal", (0.10, 0.46, 0.50), rough=0.8)
    M["teal_dark"] = K.material("TealDark", (0.05, 0.27, 0.30), rough=0.85)
    M["gold"] = K.material("Gold", (0.90, 0.68, 0.25), rough=0.2, metal=1.0)
    M["leather"] = K.material("Leather", (0.38, 0.22, 0.11), rough=0.65)
    M["leather_dark"] = K.material("LeatherDark", (0.17, 0.10, 0.06), rough=0.7)
    M["chain"] = K.material("Chain", (0.45, 0.47, 0.51), rough=0.45)
    M["trousers"] = K.material("Trousers", (0.17, 0.20, 0.28), rough=0.9)
    M["claw"] = K.material("Claw", (0.16, 0.13, 0.10), rough=0.35)
    gradient_nodes(M["fur"], (0.70, 0.50, 0.28), FUR, (0.88, 0.72, 0.48), 1.8, 0.9, (120.0, 120.0, 40.0))
    gradient_nodes(M["mane"], (0.36, 0.22, 0.10), (0.60, 0.40, 0.18), (0.86, 0.66, 0.38), 1.85, 1.2)
    chain_nodes(M["chain"])
    face_nodes(M["face"])

    # Las piezas de paladin.py piden estos nombres
    P.M = M
    M["under"] = M["chain"]
    M["crimson"] = M["cream"]
    M["lining"] = M["teal"]


# =========================================================
# CABEZA DE LEÓN
# =========================================================
# Como la de la Paladina medio anime: cada corte horizontal es la unión suave de varias formas (el cráneo,
# la quijada y el hocico), medidas desde un eje que queda adentro.

SKULL_C = V(0.0, 1.712, -0.005)
SKULL_R = (0.088, 0.096, 0.095)
CHIN_Y = 1.566
TOP_Y = SKULL_C.y + SKULL_R[1]
AXIS_Z = 0.03
HEAD_C = V(0.0, 1.69, 0.03)


def head_shapes(y):
    out = []
    dy = (y - SKULL_C.y) / SKULL_R[1]
    if abs(dy) < 1:
        k = math.sqrt(1 - dy * dy)
        out.append(((SKULL_R[0] * k, SKULL_C.z, SKULL_R[2] * k), 2.0))
    if CHIN_Y <= y <= 1.665:                                   # quijada y cachetes
        t = (y - CHIN_Y) / (1.665 - CHIN_Y)
        k = max(math.sqrt(max(0.0, 1 - (1 - min(t / 0.18, 1.0)) ** 2)), 0.05)
        w = 0.032 + 0.05 * (1 - (1 - t) ** 1.4)
        front = lerp(0.088, 0.100, t)
        back = lerp(0.05, -0.03, smooth(0, 0.5, t))
        out.append(((w * k, (front + back) / 2, (front - back) / 2 * k), 2.4))
    if 1.576 <= y <= 1.70:                                     # hocico, de la boca al puente de la nariz
        t = (y - 1.576) / (1.70 - 1.576)
        kb = max(math.sqrt(max(0.0, 1 - (1 - min(t / 0.12, 1.0)) ** 2)), 0.05)
        w = (lerp(0.040, 0.030, t) + 0.018 * bell(t, 0.38, 0.22)) * kb      # cojines de los bigotes
        front = lerp(0.118, 0.142, smooth(0.0, 0.62, t)) - 0.05 * smooth(0.72, 1.0, t)
        back = 0.03
        out.append(((w, (front + back) / 2, (front - back) / 2 * kb), 2.6))
    return out


def ray_to(oz, a, shape, n):
    w, zc, dz = shape
    sx, cz = math.sin(a), math.cos(a)

    def inside(r):
        return (abs(sx * r) / w) ** n + (abs(oz + cz * r - zc) / dz) ** n <= 1.0

    if not inside(0.0):
        return 0.0
    lo, hi = 0.0, 0.3
    for _ in range(26):
        mid = (lo + hi) / 2
        if inside(mid):
            lo = mid
        else:
            hi = mid
    return lo


def smax(a, b, k=0.014):
    h = K.clamp01(0.5 + 0.5 * (a - b) / k)
    return lerp(b, a, h) + k * h * (1 - h)


def head_axis_z(y):
    return lerp(0.07, AXIS_Z, smooth(1.575, 1.62, y))


def head_radius(y, deg):
    a = math.radians(deg)
    oz = head_axis_z(y)
    r = 0.0
    for shape, n in head_shapes(y):
        d = ray_to(oz, a, shape, n)
        r = d if r <= 0 else smax(r, d)
    return r, oz


def lion_point(y, deg, off=0.0, features=True):
    y = min(max(y, CHIN_Y + 1e-4), TOP_Y - 1e-4)
    r, oz = head_radius(y, deg)
    ad = abs(deg)
    if features and off <= 0 and ad < 80:
        r += 0.0075 * bell(ad, 26, 15) * bell(y, 1.704, 0.007)                    # cejas pesadas
        r -= 0.0025 * bell(ad, 25, 9) * bell(y, 1.688, 0.006)                     # cuencas
        r += 0.0045 * bell(deg, 0, 8) * bell(y, 1.664, 0.006)                     # la nariz
        r -= 0.0020 * bell(deg, 0, 3) * bell(y, 1.638, 0.010)                     # el surco bajo la nariz
        r -= 0.0030 * smooth(40, 18, ad) * bell(y, 1.613, 0.0028)                 # la boca
        r += 0.0030 * bell(ad, 40, 18) * bell(y, 1.672, 0.01)                     # pómulos
    a = math.radians(deg)
    p = V(math.sin(a) * r, y, oz + math.cos(a) * r)
    if off:
        p = p + (p - HEAD_C).normalized() * off
    return p


def lion_guard(p, margin):
    """Saca p de adentro de la cabeza (lo usan los mechones de paladin.lock)."""
    if p.y < CHIN_Y - 0.02 or p.y > TOP_Y + 0.02:
        return p
    y = min(max(p.y, CHIN_Y + 1e-4), TOP_Y - 1e-4)
    oz = head_axis_z(y)
    dx, dz = p.x, p.z - oz
    r = math.hypot(dx, dz)
    need = head_radius(y, math.degrees(math.atan2(dx, dz)))[0] + margin
    if 1e-6 < r < need:
        k = need / r
        return V(dx * k, p.y, oz + dz * k)
    return p


FACE_L, FACE_R, FACE_B, FACE_T = -0.11, 0.11, 1.55, 1.83
FACE_RES = 512


def face_image():
    """La cara pintada, proyectada de frente: hocico claro, nariz, boca, bigotes y ojos de león."""
    n = FACE_RES
    u = (np.arange(n) + 0.5) / n
    X, Y = np.meshgrid(FACE_L + u * (FACE_R - FACE_L), FACE_B + u * (FACE_T - FACE_B))
    AX = np.abs(X)
    col = np.ones((n, n, 3)) * np.array(K.srgb(FUR))

    def paint(mask, color):
        c = np.array(K.srgb(color))
        m = np.clip(mask, 0, 1)[..., None]
        col[:] = col * (1 - m) + c * m

    def g(x, c, w):
        return np.exp(-((x - c) / w) ** 2)

    def line(dist, width):
        return np.clip(1 - np.abs(dist) / width, 0, 1)

    # Pelo claro: hocico, mentón y una mancha sobre cada ojo
    muzzle = np.clip(1.4 - ((X / 0.052) ** 2 + ((Y - 1.622) / 0.042) ** 2), 0, 1)
    paint(muzzle, MUZZLE)
    paint(np.clip(1.3 - ((X / 0.03) ** 2 + ((Y - 1.585) / 0.022) ** 2), 0, 1), MUZZLE)
    paint(0.8 * g(AX, 0.034, 0.012) * g(Y, 1.676, 0.004), MUZZLE)
    paint(0.6 * g(AX, 0.036, 0.016) * g(Y, 1.698, 0.004), (0.92, 0.78, 0.55))

    # Rayitas oscuras de las lágrimas: del lagrimal hacia la nariz
    tear = line((Y - 1.684) + (AX - 0.020) * 0.9, 0.0012) * (AX > 0.012) * (AX < 0.022)
    paint(tear * 0.35, (0.40, 0.25, 0.14))

    # La nariz: un triángulo oscuro con las fosas
    nose_top, nose_bot = 1.670, 1.655
    tri = (Y < nose_top) & (Y > nose_bot) & (AX < 0.017 * (Y - nose_bot) / (nose_top - nose_bot) + 0.004)
    paint(tri.astype(float), (0.28, 0.17, 0.14))
    paint(0.35 * g(X, 0, 0.004) * g(Y, 1.667, 0.0015), (0.55, 0.40, 0.36))           # brillo de la nariz
    # El surco y la boca en "w"
    paint(line(X, 0.0009) * (Y < nose_bot) * (Y > 1.616), (0.28, 0.17, 0.14))
    mouth = 1.6145 - 0.0045 * np.clip(AX / 0.026, 0, 1) ** 1.5 + 0.0018 * g(AX, 0, 0.004)
    paint(line(Y - mouth, 0.0009) * (AX < 0.028), (0.28, 0.17, 0.14))
    # Los puntitos de los bigotes
    for row, (yy, n_dots) in enumerate(((1.636, 4), (1.629, 5), (1.622, 4))):
        for i in range(n_dots):
            xx = 0.012 + i * 0.0065 + row * 0.002
            paint(g(AX, xx, 0.0011) * g(Y, yy, 0.0011), (0.35, 0.22, 0.14))

    # Ojos: almendra caída hacia afuera, iris ámbar, pupila redonda y el borde negro de los leones
    eye_x, eye_y, half_w, half_h = 0.033, 1.6885, 0.0158, 0.0082
    dx = (AX - eye_x) / half_w
    up = eye_y + half_h * np.clip(1 - dx ** 2, 0, 1) ** 0.55 - 0.0018 * dx
    down = eye_y - half_h * 0.85 * np.clip(1 - dx ** 2, 0, 1) ** 0.8 - 0.0012 * dx
    inside = ((Y < up) & (Y > down) & (np.abs(dx) < 1)).astype(float)
    cx = np.sign(X) * eye_x
    r = np.sqrt((X - cx) ** 2 + (Y - (eye_y - 0.0004)) ** 2) / 0.0074
    amber = np.array(K.srgb((0.95, 0.66, 0.16)))
    amber_dark = np.array(K.srgb((0.62, 0.36, 0.06)))
    eye = amber * (1 - np.clip(r - 0.4, 0, 1)[..., None] * 0.5) + amber_dark * (np.clip(r - 0.4, 0, 1)[..., None] * 0.5)
    eye = eye * (1 - 0.85 * (r < 0.38)[..., None])
    hl = ((((X - (cx - 0.0018)) ** 2 + (Y - (eye_y + 0.0018)) ** 2) < 0.0011 ** 2)).astype(float)[..., None]
    eye = eye * (1 - hl) + hl
    col[:] = col * (1 - inside[..., None]) + eye * inside[..., None]
    rim = np.clip(line(Y - (up + 0.0004), 0.0011) + line(Y - (down - 0.0004), 0.0008), 0, 1) * (np.abs(dx) < 1.08)
    paint(rim, (0.10, 0.07, 0.05))

    img = bpy.data.images.get("Lion_Face") or bpy.data.images.new("Lion_Face", n, n, alpha=False, float_buffer=True)
    img.colorspace_settings.name = 'Linear Rec.709'
    px = np.ones((n, n, 4))
    px[..., :3] = col
    img.pixels.foreach_set(px.astype(np.float32).ravel())
    img.update()
    img.pack()
    return img


def face_nodes(mat):
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    uv = K.node(nt, "ShaderNodeUVMap", (-900, 200))
    uv.uv_map = "FaceUV"
    tex = K.node(nt, "ShaderNodeTexImage", (-650, 250))
    tex.image = face_image()
    tex.extension = 'EXTEND'
    nt.links.new(uv.outputs["UV"], tex.inputs["Vector"])
    geo = K.node(nt, "ShaderNodeNewGeometry", (-900, -300))
    sep = K.node(nt, "ShaderNodeSeparateXYZ", (-700, -300))
    nt.links.new(geo.outputs["Normal"], sep.inputs[0])
    front = K.node(nt, "ShaderNodeMapRange", (-500, -300))
    front.inputs["From Min"].default_value = -0.05
    front.inputs["From Max"].default_value = -0.35
    nt.links.new(sep.outputs["Y"], front.inputs["Value"])
    mix = K.node(nt, "ShaderNodeMix", (-300, 200))
    mix.data_type = 'RGBA'
    mix.inputs[6].default_value = (*K.srgb(FUR), 1)
    nt.links.new(front.outputs["Result"], mix.inputs["Factor"])
    nt.links.new(tex.outputs["Color"], mix.inputs[7])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])


def build_head():
    ys = [CHIN_Y + 0.0004, 1.568, 1.572, 1.577, 1.583, 1.59, 1.597, 1.604, 1.61, 1.616, 1.622, 1.628, 1.634, 1.64,
          1.646, 1.652, 1.658, 1.664, 1.67, 1.676, 1.682, 1.688, 1.694, 1.70, 1.707, 1.715, 1.725, 1.737, 1.75,
          1.763, 1.776, 1.788, 1.798, TOP_Y - 0.0004]
    half = [0, 4, 8, 13, 19, 26, 34, 43, 52, 62, 73, 86, 100, 116, 134, 156, 180]
    degs = [-d for d in reversed(half[1:-1])] + half
    m = Mesh()
    m.loft([[lion_point(y, d) for d in degs] for y in ys], wrap=True, cap_start=True, cap_end=True)
    obj = m.build("Head", [M["face"]], "rigid:B-head", sharp=85, subsurf=1)
    me = obj.data
    uv = me.uv_layers.new(name="FaceUV")
    for loop in me.loops:
        p = K.U(me.vertices[loop.vertex_index].co)
        uv.data[loop.index].uv = ((p.x - FACE_L) / (FACE_R - FACE_L), (p.y - FACE_B) / (FACE_T - FACE_B))

    # El cuello: grueso, de pelaje
    rings = []
    for y in (1.44, 1.50, 1.56, 1.62):
        r = lerp(0.075, 0.062, smooth(1.44, 1.6, y))
        rings.append([V(math.sin(a) * r * 1.05, y, 0.01 + math.cos(a) * r) for a in np.linspace(0, 2 * math.pi, 18, endpoint=False)])
    n = Mesh()
    n.loft(rings, wrap=True)
    n.build("Neck", [M["fur"]], "torso", sharp=70, orient=lambda p: V(0, p.y, 0.01))

    # Orejas redondas, un poco hacia atrás, asomando de la melena
    for sgn in (-1, 1):
        c = V(sgn * 0.062, 1.79, -0.012)
        normal = V(sgn * 0.35, 0.25, 1).normalized()
        side = V(0, 1, 0).cross(normal).normalized()
        upv = normal.cross(side).normalized()
        e = Mesh()
        rings = [[c + normal * 0.004]]
        for r in (0.012, 0.022, 0.026):
            ring = []
            for i in range(16):
                a = math.pi * i / 15                                   # media luna: solo la mitad de arriba
                q = c + side * (math.cos(a) * r) + upv * (math.sin(a) * r * 1.15) - normal * (0.006 * (r / 0.026) ** 2)
                ring.append(q)
            rings.append(ring)
        ids = [[e.v(p) for p in ring] for ring in rings]
        for i in range(15):
            e.face([ids[0][0], ids[1][i], ids[1][i + 1]])
        for k in range(1, len(ids) - 1):
            for i in range(15):
                e.face([ids[k][i], ids[k][i + 1], ids[k + 1][i + 1], ids[k + 1][i]])
        e.build(f"Ear{'R' if sgn > 0 else 'L'}", [M["ear_in"], M["fur"]], "rigid:B-head", sharp=60,
                orient=lambda p, c=c, normal=normal: c - normal, mods=[K.solidify(0.006, offset=-1.0, shell_mat=1, rim_mat=1)])


# =========================================================
# MELENA
# =========================================================

def mantle_center(a):
    """El centro del rollo de la estola alrededor del cuello, por ángulo (0 = adelante)."""
    return V(math.sin(a) * 0.165, 1.50 - 0.05 * bell(math.degrees(a), 0, 45), 0.012 + math.cos(a) * 0.15)


def mantle_guard(p, margin):
    if p.y < 1.36 or p.y > 1.6:
        return p
    a = math.atan2(p.x, p.z - 0.012)
    c = mantle_center(a)
    d = p - c
    need = 0.05 + margin
    if 1e-6 < d.length < need:
        return c + d * (need / d.length)
    return p


def build_mane():
    # El volumen: un casco grande y despeinado sobre el cráneo y la nuca, que deja la cara al aire
    m = Mesh()
    cols, rows = 40, 10
    rings = []
    for r in range(rows + 1):
        v = r / rows
        ring = []
        for i in range(cols):
            deg = i / cols * 360 - 180
            start = lerp(1.785, 1.58, smooth(35, 130, abs(deg)))             # adelante deja la frente al aire
            y = lerp(start, TOP_Y - 0.002, v ** 0.8)
            fluff = 0.006 + 0.034 * smooth(40, 120, abs(deg)) * (1 - v * 0.5) + 0.004 * math.sin(math.radians(deg) * 18 + v * 3)
            ring.append(lion_point(y, deg, fluff, features=False))
        rings.append(ring)
    m.loft(rings, wrap=True, cap_end=True)
    m.build("Mane_Cap", [M["mane"]], "rigid:B-head", sharp=70)

    h = Mesh()
    # La corona alrededor de la cara: mechones que salen hacia afuera y hacia atrás
    n = 30
    for i in range(n):
        th = -165 + 330 * i / (n - 1)                       # de un lado de la quijada, por arriba, al otro
        tr = math.radians(th)
        side = math.sin(tr)
        y0 = 1.69 + 0.095 * math.cos(tr)                    # arriba de la frente o abajo de la quijada
        deg = math.copysign(lerp(25, 92, abs(side) ** 0.7), side) if abs(side) > 0.05 else 0.0
        root = lion_point(y0, deg, 0.008, features=False) + V(0, 0, -0.03 * smooth(0.5, 0.95, math.cos(tr)))
        out = V(math.sin(tr) * 1.0, math.cos(tr) * 0.9, 0.0).normalized()
        length = 0.10 + 0.03 * rng.random() + 0.03 * smooth(0.3, 1.0, abs(side))
        mid = root + out * (length * 0.55) + V(0, -0.015, -0.05)
        tip = root + out * length + V(0, -0.05 - 0.03 * (1 - math.cos(tr)), -0.09)
        P.lock(h, [root, mid, tip], 0.05, 0.022, samples=10, guard_head=0.012, taper=0.8)
    # La melena de atrás, larga sobre la capa
    for layer in range(2):
        cnt = 10 if layer == 0 else 9
        for i in range(cnt):
            u = (i + 0.5 * layer) / (cnt - 1 + 0.5 * layer) * 2 - 1
            deg = wrap_deg(180 - u * (80 if layer == 0 else 70))
            root = lion_point(lerp(1.80, 1.70, abs(u)) - 0.04 * layer, deg, 0.02 + 0.01 * layer, features=False)
            nape = lion_point(1.60, deg, 0.06 + 0.01 * layer, features=False)
            x_end = u * (0.2 if layer == 0 else 0.16) + rng.uniform(-0.01, 0.01)
            y_end = (1.22 if layer == 0 else 1.3) - 0.06 * (1 - abs(u)) + rng.uniform(-0.03, 0.03)
            ctrl = [root, nape, V(lerp(nape.x, x_end, 0.5), 1.45, -0.22), V(x_end, y_end, -0.24)]
            P.lock(h, ctrl, 0.07, 0.024, samples=16, guard_body=0.014 + 0.012 * layer, taper=0.8)
    # El pecho: la melena baja por delante, sobre la estola
    for i in range(9):
        u = i / 8 * 2 - 1
        root = lion_point(1.585, u * 55, 0.004, features=False) + V(0, -0.01, -0.02)
        p1 = V(u * 0.08, 1.52, 0.11)
        tip = V(u * 0.10, 1.40 + 0.03 * abs(u), 0.15 - 0.02 * abs(u))
        P.lock(h, [root, p1, tip], 0.055, 0.02, samples=12, guard_head=0.01, guard_body=0.016, taper=0.9)
    h.build("Mane_Locks", [M["mane"]], "hair", sharp=70)

    # Dos trenzas con anillos de oro, por delante de los hombros
    b = Mesh()
    g = Mesh()
    for sgn in (-1, 1):
        top = lion_point(1.62, sgn * 92, 0.03, features=False)
        path = P.catmull([top, V(sgn * 0.14, 1.52, 0.06), V(sgn * 0.15, 1.40, 0.10), V(sgn * 0.15, 1.30, 0.12)], 16)
        path = [P.body_guard(mantle_guard(p, 0.02), 0.03) for p in path]
        for k, p in enumerate(path):
            P.sphere(b, p, 0.019 * (1 - 0.35 * k / len(path)), rings=5, sides=8)
        for k in (4, 9, 14):
            p, nxt = path[k], path[min(k + 1, len(path) - 1)]
            ring = [p + (V(math.cos(a), 0, math.sin(a))) * 0.016 for a in np.linspace(0, 2 * math.pi, 10, endpoint=False)]
            g.tube(ring, (0.004, 0.006), sides=5, closed=True, up=lambda q, t, p=p: V(0, 1, 0))
        P.sphere(g, path[-1] + V(0, -0.012, 0), 0.011, rings=5, sides=8)
    b.build("Braids", [M["mane"]], "hair", sharp=70)
    g.build("Braid_Rings", [M["gold"]], "hair", sharp=50)


# =========================================================
# ROPA
# =========================================================

def build_surcoat():
    """El sobretodo: arriba cerrado sobre la cota, abajo dos paños largos (adelante y atrás)."""
    cols = 44
    rows = [1.0, 1.06, 1.12, 1.18, 1.24, 1.30, 1.36, 1.41, 1.45, 1.48]
    rings = []
    for y in rows:
        ring = []
        for i in range(cols):
            deg = i / cols * 360 - 180
            yy = y - (0.05 * smooth(60, 0, abs(deg)) if y == rows[-1] else 0.0)      # escote en V
            ring.append(P.torso_point(yy, deg, 0.026))
        rings.append(ring)
    m = Mesh()
    m.loft(rings, wrap=True)
    obj = m.build("Surcoat", [M["cream"], M["cream_in"]], "torso", sharp=50, orient=P.torso_axis,
                  mods=[K.solidify(0.004, offset=-1.0, shell_mat=1, rim_mat=1)])
    K.trim(obj, M["gold"], radius=0.0034, flat=0.7, name="Surcoat_Trim",
           select=lambda pts: sum(p.y for p in pts) / len(pts) > 1.2)

    for name, base in (("Robe_Front", 0.0), ("Robe_Back", 180.0)):
        cols, rows_n = 16, 18
        rings = []
        for r in range(rows_n + 1):
            t = r / rows_n
            ring = []
            for i in range(cols + 1):
                s = i / cols * 2 - 1
                deg = base + s * lerp(44, 36, t)
                hem = 0.30 + 0.03 * abs(s) - 0.012 * math.cos(s * math.pi * 3)
                y = lerp(1.0, hem, t)
                off = lerp(0.03, 0.05, smooth(0, 0.3, t)) + 0.006 * math.sin(s * 6 + base) * t
                ring.append(P.skirt_point(y, deg, off, 0.28))
            rings.append(ring)
        m = Mesh()
        m.loft(rings)
        obj = m.build(name, [M["cream"], M["cream_in"]], "tabard", sharp=50, orient=P.torso_axis,
                      mods=[K.solidify(0.004, offset=-1.0, shell_mat=1, rim_mat=1)])
        K.trim(obj, M["gold"], radius=0.003, flat=0.6, name=name + "_Trim")

    # La franja turquesa por el medio, del escote hasta el ruedo (adelante y atrás)
    for name, base in (("Stripe_Front", 0.0), ("Stripe_Back", 180.0)):
        m = Mesh()
        rings = []
        for y in np.linspace(1.43 if base == 0 else 1.46, 1.0, 9):
            rings.append([P.torso_point(y, base + s * 7.5, 0.0315) for s in (-1, 1)])
        for k in range(1, 19):
            t = k / 18
            y = lerp(1.0, 0.31, t)
            off = lerp(0.03, 0.05, smooth(0, 0.3, t)) + 0.0045
            rings.append([P.skirt_point(y, base + s * lerp(7.5, 6.5, t), off, 0.28) for s in (-1, 1)])
        for k in range(len(rings) - 1):
            a, b = rings[k], rings[k + 1]
            ia, ib = [m.v(p) for p in a], [m.v(p) for p in b]
            m.face([ia[0], ia[1], ib[1], ib[0]])
        obj = m.build(name, [M["teal"]], "tabard", sharp=50, orient=P.torso_axis, mods=[K.solidify(0.0015, offset=-1.0)])
        K.trim(obj, M["gold"], radius=0.0024, flat=0.6, name=name + "_Trim")

    # El sol de oro en el pecho
    c = P.torso_point(1.33, 0, 0.034)
    K.glyph("☀", 0.11, 0.003, c, V(0, 0.1, 1), V(0, 1, 0), M["gold"], "rigid:B-chest", name="Chest_Sun")


def build_belt():
    cols = 48
    rings = [[P.torso_point(y, i / cols * 360 - 180, 0.036) for i in range(cols)] for y in (0.985, 1.045)]
    m = Mesh()
    m.loft(rings, wrap=True)
    m.build("Belt", [M["leather"]], "torso", sharp=50, orient=P.torso_axis,
            mods=[K.solidify(0.007, offset=-1.0), K.bevel(0.0012, 1)])
    # Hebilla: un sol
    c = P.torso_point(1.015, 0, 0.046)
    b = Mesh()
    ring = [c + V(math.cos(a) * 0.022, math.sin(a) * 0.022, 0) for a in np.linspace(0, 2 * math.pi, 20, endpoint=False)]
    b.loft([[p + V(0, 0, -0.006) for p in ring], ring], wrap=True, cap_end=True)
    b.build("Belt_Buckle", [M["gold"]], "torso", sharp=50)
    K.glyph("☀", 0.036, 0.002, c + V(0, 0, 0.0005), V(0, 0, 1), V(0, 1, 0), M["teal_dark"], "torso", name="Buckle_Sun")

    # Dos bolsas de cuero colgando del cinturón
    for deg in (52, -64):
        top = P.torso_point(1.0, deg, 0.044)
        n = (top - P.torso_axis(top))
        n.y = 0
        n.normalize()
        side = V(0, 1, 0).cross(n).normalized()
        p = Mesh()
        w, hgt, d = 0.034, 0.075, 0.026
        corners = []
        for yy in (0.0, -hgt):
            for sx in (-1, 1):
                for sz in (0, 1):
                    corners.append(top + V(0, yy, 0) + side * (sx * w) + n * (sz * d))
        ids = [p.v(q) for q in corners]
        # 0:(y0,-x,in) 1:(y0,-x,out) 2:(y0,+x,in) 3:(y0,+x,out) 4..7 lo mismo abajo
        for f in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (1, 3, 7, 5), (0, 4, 6, 2)):
            p.face([ids[i] for i in f])
        obj = p.build(f"Pouch_{'R' if deg > 0 else 'L'}", [M["leather"]], "tasset:" + (".R" if deg > 0 else ".L"), sharp=40,
                      mods=[K.bevel(0.004, 2, 30)])
        flap = Mesh()
        ids = [flap.v(q) for q in (top + side * (-w - 0.002) + n * (d + 0.002), top + side * (w + 0.002) + n * (d + 0.002),
                                    top + side * (w + 0.002) + n * (d + 0.004) + V(0, -0.035, 0),
                                    top + side * (-w - 0.002) + n * (d + 0.004) + V(0, -0.035, 0))]
        flap.face(ids)
        flap.build(f"Pouch_Flap_{'R' if deg > 0 else 'L'}", [M["leather_dark"]], "tasset:" + (".R" if deg > 0 else ".L"),
                   sharp=40, orient=top, mods=[K.solidify(0.003, offset=1.0)])


def build_mantle():
    """La estola: un rollo de tela alrededor del cuello y las dos puntas que cuelgan por delante."""
    m = Mesh()
    ring = []
    n = 40
    for i in range(n):
        a = 2 * math.pi * i / n
        ring.append(mantle_center(a))

    def radius(t):
        a = 2 * math.pi * t
        return (0.034 + 0.005 * math.sin(a * 7), 0.05 + 0.006 * math.sin(a * 5 + 1))

    m.tube(ring, radius, sides=10, closed=True,
           up=lambda p, t: V(p.x, 0, p.z - 0.012).normalized())
    m.build("Mantle", [M["cream"]], "gorget", sharp=60, subsurf=1)

    # Las puntas de la estola, con el final turquesa y flecos de oro
    for sgn in (-1, 1):
        s = Mesh()
        rows = []
        for k in range(14):
            t = k / 13
            y = lerp(1.47, 1.02, t)
            x = sgn * lerp(0.085, 0.075, t)
            p = P.torso_point(y, math.degrees(math.atan2(x, 0.12)), 0.042)
            p.x = x
            rows.append((p, t))
        for k in range(len(rows) - 1):
            (a, ta), (b, tb) = rows[k], rows[k + 1]
            mat = 1 if ta >= 0.78 else 0
            w = 0.03
            ids = [s.v(q) for q in (a + V(-w, 0, 0), a + V(w, 0, 0), b + V(w, 0, 0), b + V(-w, 0, 0))]
            s.face(ids, mat)
        obj = s.build(f"Stole{'R' if sgn > 0 else 'L'}", [M["cream"], M["teal"]], "tabard", sharp=60,
                      orient=P.torso_axis, mods=[K.solidify(0.003, offset=-1.0)])
        K.trim(obj, M["gold"], radius=0.0025, flat=0.6, name=f"Stole_Trim{'R' if sgn > 0 else 'L'}")
        f = Mesh()
        end = rows[-1][0]
        for i in range(7):
            x = end.x - 0.027 + i * 0.009
            f.tube([V(x, end.y, end.z + 0.002), V(x, end.y - 0.03, end.z + 0.004)], 0.0022, sides=5)
        f.build(f"Stole_Fringe{'R' if sgn > 0 else 'L'}", [M["gold"]], "tabard", sharp=60)


def build_pauldron():
    """Una sola hombrera de oro, la izquierda, con el medallón turquesa adelante."""
    side = ".L"
    origin, axis, up, front = P.pauldron_frame(side)
    for name, a_rows, r_rows, span in (
            ("Pauldron_Lame2", [0.09, 0.115, 0.14], [0.118, 0.122, 0.116], 104),
            ("Pauldron_Lame1", [0.045, 0.07, 0.095], [0.128, 0.132, 0.128], 112),
            ("Pauldron_Dome", [-0.09, -0.07, -0.045, -0.015, 0.012, 0.035, 0.05], [0.06, 0.095, 0.118, 0.132, 0.138, 0.138, 0.136], 122)):
        rings = []
        cols = 22
        for a, r in zip(a_rows, r_rows):
            ring = []
            for i in range(cols + 1):
                th = math.radians(lerp(-span, span, i / cols))
                rr = r * (1 - 0.07 * (math.degrees(th) / span) ** 2)
                ring.append(origin + axis * a + (up * math.cos(th) + front * math.sin(th)) * rr)
            rings.append(ring)
        m = Mesh()
        m.loft(rings)
        obj = m.build(name + side, [M["gold"]], "pauldron:" + side, sharp=50,
                      orient=lambda p: origin + axis * (p - origin).dot(axis),
                      mods=[K.solidify(0.004, offset=-1.0), K.bevel(0.0008, 1)])
        K.trim(obj, M["teal_dark"], radius=0.0036, flat=0.8, name=name + "_Trim" + side)

    # El medallón: disco turquesa con aro y centro de oro, en la cara de adelante
    nrm = (front * 0.85 + up * 0.35 + axis * 0.2).normalized()
    c = origin + nrm * 0.142
    tang = up.cross(nrm).normalized()
    bit = nrm.cross(tang).normalized()
    disc = Mesh()
    ring = [c + (tang * math.cos(a) + bit * math.sin(a)) * 0.042 for a in np.linspace(0, 2 * math.pi, 24, endpoint=False)]
    disc.loft([[p - nrm * 0.006 for p in ring], ring], wrap=True, cap_end=True)
    disc.build("Medallion", [M["teal"]], "pauldron:" + side, sharp=50)
    rim = Mesh()
    rim.tube([c + nrm * 0.001 + (tang * math.cos(a) + bit * math.sin(a)) * 0.042 for a in np.linspace(0, 2 * math.pi, 24, endpoint=False)],
             0.0045, sides=6, closed=True)
    P.dome(rim, c + nrm * 0.001, nrm, 0.012, rows=2, sides=10)
    rim.build("Medallion_Gold", [M["gold"]], "pauldron:" + side, sharp=50)
    # Cinta turquesa que cuelga del medallón
    r = Mesh()
    for k, (dx, ln) in enumerate(((-0.012, 0.11), (0.012, 0.085))):
        a = c + tang * dx - bit * 0.03
        pts = [a - bit * (ln * t) + nrm * (0.004 * math.sin(t * 3)) - up * (0.02 * t) for t in np.linspace(0, 1, 6)]
        ids = [[r.v(p - tang * 0.009), r.v(p + tang * 0.009)] for p in pts]
        for i in range(len(ids) - 1):
            r.face([ids[i][0], ids[i][1], ids[i + 1][1], ids[i + 1][0]])
    r.build("Medallion_Ribbon", [M["teal"]], "pauldron:" + side, sharp=60, orient=c - nrm * 0.1,
            mods=[K.solidify(0.0015, offset=0.0)])


def build_arm(side):
    chain = K.arm_chain(side)
    up = V(0, 1, 0)
    P.tube_piece("Sleeve" + side, chain, -0.04, 0.30, 10, lambda d, a: P.ArmRadius(d) + 0.004, M["chain"], "arm:" + side,
                 up, cols=14, mods=[], trims=False)
    P.tube_piece("Bracer" + side, chain, 0.27, 0.415, 4, lambda d, a: P.ArmRadius(d) + 0.012 + 0.004 * bell(a, 90, 30),
                 M["leather"], "armstiff:" + side, up)
    # Las mangas cortas del sobretodo: tela crema que cae sobre el brazo
    P.tube_piece("Sleeve_Cloth" + side, chain, -0.02, 0.12, 3,
                 lambda d, a: P.ArmRadius(d) + 0.016 + 0.01 * smooth(0.02, 0.12, d), M["cream"], "arm:" + side, up, cols=18)

    # Manos de pelaje con garras
    hand_bones = {"B-hand" + side} | {f"B-{f}0{k}{side}" for f in ("thumb", "indexFinger", "middleFinger",
                                                                   "ringFinger", "pinky") for k in (1, 2, 3)}
    K.dummy_part("Paw" + side, hand_bones, M["fur"], skin="keep", inflate=0.004)
    c = Mesh()
    for f in ("thumb", "indexFinger", "middleFinger", "ringFinger", "pinky"):
        a, b = K.SK.pos(f"B-{f}03{side}"), K.SK.end(f"B-{f}03{side}")
        d = (b - a).normalized()
        base = b - d * 0.004
        tip = b + d * 0.016 + V(0, -0.006, 0)
        c.tube([base, base.lerp(tip, 0.5), tip], lambda t: 0.0045 * (1 - 0.92 * t), sides=6, cap_end=True)
    c.build("Claws" + side, [M["claw"]], "blend:" + ",".join(f"B-{f}03{side}=0.2" for f in ("thumb", "indexFinger", "middleFinger", "ringFinger", "pinky")),
            sharp=50)


def build_leg(side):
    chain = K.leg_chain(side)
    sgn = 1 if side == ".R" else -1
    lat = V(sgn, 0, 0)
    P.tube_piece("Trousers" + side, chain, -0.06, 0.62, 12, lambda d, a: P.LegRadius(d) + 0.012 + 0.006 * bell(d, 0.3, 0.12),
                 M["trousers"], "leg:" + side, lat, cols=16, mods=[], trims=False)
    P.tube_piece("Boot" + side, chain, 0.5, 0.80, 5, lambda d, a: P.LegRadius(d) + 0.02 + 0.008 * smooth(0.6, 0.5, d),
                 M["leather"], "legstiff:" + side, lat, cols=18)
    K.dummy_part("Foot" + side, {"B-foot" + side, "B-toe" + side}, M["leather_dark"], skin="leg:" + side, inflate=0.012)


def build_tail():
    path = P.catmull([V(0.0, 0.93, -0.11), V(0.02, 0.80, -0.20), V(0.06, 0.62, -0.24), V(0.09, 0.46, -0.22),
                      V(0.11, 0.36, -0.17)], 18)
    m = Mesh()
    m.tube(path, lambda t: 0.024 * (1 - 0.45 * t), sides=10, cap_start=True, cap_end=True)
    m.build("Tail", [M["fur"]], "rigid:B-hips", sharp=60, subsurf=1)
    tuft = Mesh()
    end = path[-1]
    d = (path[-1] - path[-3]).normalized()
    for i in range(7):
        a = 2 * math.pi * i / 7
        off = V(math.cos(a), 0, math.sin(a)) * 0.012
        tuft.tube([end - d * 0.02 + off * 0.5, end + d * 0.02 + off, end + d * 0.055 + off * 1.4],
                  lambda t: (0.012 * (1 - 0.85 * t), 0.012 * (1 - 0.85 * t)), sides=6)
    tuft.build("Tail_Tuft", [M["mane"]], "rigid:B-hips", sharp=70)


# =========================================================
# TODO
# =========================================================

def build():
    K.begin()
    use_leonin_body()
    materials()
    # Los mechones de paladin.lock tienen que esquivar la cabeza de león y la estola
    body_guard = P.body_guard
    P.head_guard = lion_guard
    P.HeadCenter = HEAD_C
    P.body_guard = lambda p, margin: mantle_guard(body_guard(p, margin), margin)

    P.build_torso()
    build_head()
    build_surcoat()
    build_belt()
    build_mantle()
    build_pauldron()
    for side in (".L", ".R"):
        build_arm(side)
        build_leg(side)
    P.build_cape()
    build_tail()
    build_mane()

    path = os.path.join(K.HERE, NAME + ".blend")
    bpy.context.scene["merc_name"] = NAME
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print(f"[Cleric] guardado {path}")


if __name__ == "__main__":
    build()
    import merc_export
    merc_export.run(NAME, preview_only="--preview" in sys.argv)
