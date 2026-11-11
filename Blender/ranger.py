# -*- coding: utf-8 -*-
# ============================================================
# ranger.py
#
# El Explorador (Ranger) en Blender (10 de noviembre de 2026, referencias que mandó Gustavo): un elfo tribal
# vestido de verdes. Orejas largas y puntiagudas, pelo largo castaño con dos trenzas con cuentas de madera,
# ojos verdes y pintura tribal verde en la cara que brilla un poco. Túnica verde, jubón de cuero verde oscuro
# con escamas, faldón con el ruedo cortado en hojas, capucha corta sobre los hombros con hojas superpuestas,
# bufanda alrededor del cuello, capa larga verde oscura, correa cruzada, cinturón, brazales con vendas, botas
# envueltas, collar con una hoja y el carcaj con flechas en la cadera derecha. Sin arco: es de la clase.
#
# El cuerpo, la cabeza y el pelo salen de paladin.py (con otra cara pintada y otros colores). Lo propio del
# elfo (orejas, hojas, carcaj) está acá.
#
# Correr (arma Ranger.blend, hornea y exporta a Out/Ranger):
#   "H:/blender.exe" -b --factory-startup --python Blender/ranger.py
# Solo armar y sacar fotos, sin hornear:  agregar  -- --preview
# Después, en Unity: Mercenarios ▸ Modelos de clase ▸ Importar los modelos de Blender, y poner el prefab
# Model_Ranger en el campo "Modelo" de la clase del Explorador.
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
import cleric as C     # noqa: E402
from merc_kit import V, Mesh, smooth, bell, lerp, wrap_deg   # noqa: E402

NAME = "Ranger"
rng = random.Random(3307)

SKIN = (0.92, 0.77, 0.63)
TRIBAL = (0.20, 0.62, 0.26)
TRIBAL_GLOW = (0.25, 0.85, 0.35)


def use_elf_body():
    """Un cuerpo delgado, sin busto, con hombros un poco más anchos que la Paladina."""
    P.TorsoWidth = C.scaled(P.TorsoWidth, lambda y: lerp(1.0, 1.06, smooth(1.15, 1.35, y)))
    P.TorsoDepth = C.scaled(P.TorsoDepth, lambda y: 1.0)
    P.ArmRadius = C.scaled(P.ArmRadius, lambda d: 1.04)
    P.bust = lambda y, deg: 0.0
    P.SKIN = SKIN


# =========================================================
# MATERIALES
# =========================================================

M = {}


def scale_nodes(mat, light, dark, scale=180.0):
    """Cuero de escamas: celdas con el borde oscuro."""
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    geo = K.node(nt, "ShaderNodeNewGeometry", (-900, 200))
    vor = K.node(nt, "ShaderNodeTexVoronoi", (-650, 200))
    vor.feature = 'DISTANCE_TO_EDGE'
    vor.inputs["Scale"].default_value = scale
    nt.links.new(geo.outputs["Position"], vor.inputs["Vector"])
    edge = K.node(nt, "ShaderNodeMapRange", (-400, 200))
    edge.inputs["From Min"].default_value = 0.0
    edge.inputs["From Max"].default_value = 0.12
    nt.links.new(vor.outputs["Distance"], edge.inputs["Value"])
    mix = K.node(nt, "ShaderNodeMix", (-200, 200))
    mix.data_type = 'RGBA'
    mix.inputs[6].default_value = (*K.srgb(dark), 1)
    mix.inputs[7].default_value = (*K.srgb(light), 1)
    nt.links.new(edge.outputs["Result"], mix.inputs["Factor"])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])


def materials():
    M["skin"] = K.material("Skin", SKIN, rough=0.72)
    M["skin_plain"] = K.material("SkinPlain", SKIN, rough=0.72)
    M["hair"] = K.material("Hair", (0.42, 0.24, 0.12), rough=0.6)
    M["tunic"] = K.material("Tunic", (0.20, 0.40, 0.17), rough=0.9)
    M["tunic_dark"] = K.material("TunicDark", (0.11, 0.22, 0.10), rough=0.92)
    M["jerkin"] = K.material("Jerkin", (0.17, 0.30, 0.15), rough=0.6)
    M["cloak"] = K.material("Cloak", (0.13, 0.26, 0.12), rough=0.92)
    M["cloak_in"] = K.material("CloakInner", (0.30, 0.21, 0.12), rough=0.92)
    M["leaf"] = K.material("Leaf", (0.33, 0.55, 0.20), rough=0.7)
    M["leaf_dark"] = K.material("LeafDark", (0.18, 0.38, 0.13), rough=0.7)
    M["leather"] = K.material("Leather", (0.40, 0.24, 0.12), rough=0.65)
    M["leather_dark"] = K.material("LeatherDark", (0.20, 0.12, 0.07), rough=0.7)
    M["trousers"] = K.material("Trousers", (0.27, 0.26, 0.17), rough=0.9)
    M["wrap"] = K.material("Wrap", (0.62, 0.58, 0.44), rough=0.9)
    M["gold"] = K.material("Bronze", (0.70, 0.52, 0.26), rough=0.3, metal=1.0)
    M["wood"] = K.material("Bead", (0.50, 0.32, 0.16), rough=0.5)
    M["feather"] = K.material("Fletching", (0.88, 0.86, 0.78), rough=0.8)
    M["rune"] = K.material("Rune", TRIBAL, rough=0.5, emit=TRIBAL_GLOW, emit_strength=0.6)
    C.gradient_nodes(M["hair"], (0.22, 0.12, 0.06), (0.42, 0.24, 0.12), (0.62, 0.40, 0.20), 1.85, 1.15)
    scale_nodes(M["jerkin"], (0.20, 0.36, 0.17), (0.07, 0.13, 0.06))
    P.face_image = face_image
    P.face_nodes(M["skin"])
    # Las piezas de paladin.py piden estos nombres
    P.M = M
    M["under"] = M["tunic"]
    M["crimson"] = M["cloak"]
    M["lining"] = M["cloak_in"]


def face_image():
    """La cara de la primera Paladina con ojos verdes y pintura tribal verde que brilla."""
    n = P.FACE_RES
    u = (np.arange(n) + 0.5) / n
    X, Y = np.meshgrid(P.FaceLeft + u * (P.FaceRight - P.FaceLeft), P.FaceBottom + u * (P.FaceTop - P.FaceBottom))
    AX = np.abs(X)
    col = np.ones((n, n, 3)) * np.array(K.srgb(SKIN))
    glow = np.zeros((n, n, 3))

    def paint(mask, color):
        c = np.array(K.srgb(color))
        m = np.clip(mask, 0, 1)[..., None]
        col[:] = col * (1 - m) + c * m

    def g(x, c, w):
        return np.exp(-((x - c) / w) ** 2)

    def line(dist, width):
        return np.clip(1 - np.abs(dist) / width, 0, 1)

    paint(0.15 * g(AX, 0.046, 0.016) * g(Y, 1.648, 0.012), (0.85, 0.52, 0.45))

    # Labios finos
    lip_y = 1.6065
    mouth = 0.018
    k = np.clip(1 - (X / mouth) ** 2, 0, 1)
    top = lip_y + 0.003 * k ** 0.6 - 0.001 * g(X, 0, 0.004)
    bottom = lip_y - 0.0045 * k ** 0.7
    lips = np.clip((top - Y) / 0.0012, 0, 1) * np.clip((Y - bottom) / 0.0012, 0, 1) * (AX < mouth)
    paint(lips * 0.7, (0.72, 0.42, 0.38))
    paint(0.7 * g(Y, lip_y, 0.0007) * (AX < mouth * 0.92), (0.40, 0.20, 0.17))

    # Ojos verdes, almendrados y un poco rasgados hacia afuera
    eye_x, eye_y, half_w, half_h = 0.0335, 1.6825, 0.0165, 0.0064
    dx = (AX - eye_x) / half_w
    shape = np.clip(1 - dx ** 2, 0, 1) ** 0.75
    up = eye_y + half_h * shape + 0.0018 * dx
    down = eye_y - half_h * 0.8 * shape + 0.0010 * dx
    inside = ((Y < up) & (Y > down) & (np.abs(dx) < 1)).astype(float)
    r = np.sqrt(((AX - eye_x) / 0.0068) ** 2 + ((Y - eye_y) / 0.0068) ** 2)
    iris = np.clip(1.2 - r, 0, 1)
    green = np.array(K.srgb((0.30, 0.78, 0.38)))
    white = np.array(K.srgb((0.95, 0.94, 0.90)))
    eye = white * (1 - iris[..., None]) + green * iris[..., None]
    eye = eye * (1 - 0.8 * (r < 0.35)[..., None])
    col[:] = col * (1 - inside[..., None]) + eye * inside[..., None]
    glow[:] = glow * (1 - inside[..., None]) + np.array(K.srgb(TRIBAL_GLOW)) * (inside * iris * 0.45)[..., None]
    liner = np.clip(1 - np.abs(Y - (up + 0.0007)) / (0.0008 + 0.0009 * np.clip(dx, 0, 1)), 0, 1) * (np.abs(dx) < 1.05)
    paint(liner, (0.18, 0.11, 0.07))
    glow[:] = glow * (1 - liner[..., None])

    # Cejas finas y rectas, que suben hacia afuera
    t = np.clip((AX - 0.012) / 0.042, 0, 1)
    brow_y = 1.699 + 0.006 * t
    brow = line(Y - brow_y, 0.0018 * (1.0 - 0.5 * t)) * (AX > 0.011) * (AX < 0.055)
    paint(brow * 0.85, (0.30, 0.18, 0.09))

    # Pintura tribal: tres rayas en cada mejilla, una línea en el mentón y una hoja en la frente
    tribal = np.zeros_like(Y)
    for k_ in range(3):
        x0 = 0.026 + k_ * 0.0075
        y0 = 1.667 - k_ * 0.002
        along = (AX - x0) * 0.45 - (Y - y0) * 0.89
        across = (AX - x0) * 0.89 + (Y - y0) * 0.45
        tribal += line(across, 0.0016) * (along > 0) * (along < 0.024 - k_ * 0.004)
    tribal += line(X, 0.0016) * (Y < 1.597) * (Y > 1.572)
    leaf_y = 1.718
    tribal += ((X / (0.0045 * np.clip(1 - ((Y - leaf_y) / 0.013) ** 2, 0, 1) ** 0.6 + 1e-6)) ** 2 < 1).astype(float) \
        * (np.abs(Y - leaf_y) < 0.013)
    tribal = np.clip(tribal, 0, 1)
    paint(tribal * 0.9, TRIBAL)
    glow[:] = glow * (1 - tribal[..., None]) + np.array(K.srgb(TRIBAL_GLOW)) * (tribal * 0.5)[..., None]

    def to_image(name, rgb):
        img = bpy.data.images.get(name) or bpy.data.images.new(name, n, n, alpha=False, float_buffer=True)
        img.colorspace_settings.name = 'Linear Rec.709'
        px = np.ones((n, n, 4))
        px[..., :3] = rgb
        img.pixels.foreach_set(px.astype(np.float32).ravel())
        img.update()
        img.pack()
        return img

    return to_image("Face_Paint", col), to_image("Face_Glow", glow)


# =========================================================
# OREJAS DE ELFO
# =========================================================

def build_ears():
    m = Mesh()
    for sgn in (-1, 1):
        base = P.head_point(0.47, sgn * 92, -0.004, features=False)
        path = [base, base + V(sgn * 0.028, 0.03, -0.018), base + V(sgn * 0.055, 0.075, -0.045)]
        path = P.catmull(path, 10)
        m.tube(path, lambda t: (0.0045 * (1 - 0.6 * t), 0.017 * (1 - t) ** 0.7 + 0.0015), sides=8,
               up=lambda p, t: V(0, 0, 1))
    m.build("Ears", [M["skin_plain"]], "rigid:B-head", sharp=60, subsurf=1)


# =========================================================
# ROPA
# =========================================================

def cowl_center(a):
    return V(math.sin(a) * 0.105, 1.505 - 0.03 * bell(math.degrees(a), 0, 50), 0.012 + math.cos(a) * 0.10)


def cowl_guard(p, margin):
    if p.y < 1.4 or p.y > 1.6:
        return p
    c = cowl_center(math.atan2(p.x, p.z - 0.012))
    d = p - c
    need = 0.035 + margin
    if 1e-6 < d.length < need:
        return c + d * (need / d.length)
    return p


def build_jerkin():
    """El jubón de cuero de escamas, sin mangas, sobre la túnica."""
    cols = 44
    rows = [1.0, 1.06, 1.12, 1.18, 1.24, 1.30, 1.36, 1.41, 1.45, 1.48]
    rings = []
    for y in rows:
        ring = []
        for i in range(cols):
            deg = i / cols * 360 - 180
            yy = y - (0.04 * smooth(50, 0, abs(deg)) if y == rows[-1] else 0.0)
            ring.append(P.torso_point(yy, deg, 0.016))
        rings.append(ring)
    m = Mesh()
    m.loft(rings, wrap=True)
    obj = m.build("Jerkin", [M["jerkin"]], "torso", sharp=50, orient=P.torso_axis,
                  mods=[K.solidify(0.004, offset=-1.0), K.bevel(0.0012, 1)])
    K.trim(obj, M["leather_dark"], radius=0.0035, flat=0.7, name="Jerkin_Trim")

    # La correa cruzada: del hombro derecho a la cadera izquierda
    s = Mesh()
    pts = []
    for k in range(16):
        t = k / 15
        y = lerp(1.47, 1.02, t)
        deg = lerp(150, -40, t) if False else lerp(-25, 205, t) * 0 + lerp(35, -40, t)
        pts.append(P.torso_point(y, deg, 0.024))
    back = []
    for k in range(16):
        t = k / 15
        y = lerp(1.47, 1.02, t)
        back.append(P.torso_point(y, lerp(145, 220, t), 0.024))
    for path in (pts, back):
        rings = []
        for p in path:
            n = (p - P.torso_axis(p))
            n.y = 0
            n.normalize()
            d = V(0, 1, 0).cross(n).normalized()
            rings.append([p - V(0, 0.016, 0) + d * 0.0, p + V(0, 0.016, 0)])
        ids = [[s.v(q) for q in r] for r in rings]
        for i in range(len(ids) - 1):
            s.face([ids[i][0], ids[i][1], ids[i + 1][1], ids[i + 1][0]])
    s.build("Bandolier", [M["leather"]], "torso", sharp=60, orient=P.torso_axis, mods=[K.solidify(0.004, offset=-1.0)])


def build_skirt():
    """El faldón de la túnica: adelante y atrás, con el ruedo cortado en hojas."""
    for name, base in (("Skirt_Front", 0.0), ("Skirt_Back", 180.0)):
        cols, rows = 24, 12
        rings = []
        for r in range(rows + 1):
            t = r / rows
            ring = []
            for i in range(cols + 1):
                s = i / cols * 2 - 1
                deg = base + s * lerp(70, 64, t)
                leaf = abs(math.sin((s + 1) * math.pi * 3.5)) ** 0.7
                hem = 0.66 - 0.05 * leaf + 0.03 * abs(s)
                y = lerp(1.0, hem, t)
                off = lerp(0.022, 0.05 if base == 0 else 0.032, smooth(0, 0.35, t))
                ring.append(P.skirt_point(y, deg, off, 0.32 if base == 0 else 0.2))
            rings.append(ring)
        m = Mesh()
        m.loft(rings)
        m.build(name, [M["tunic"], M["tunic_dark"]], "tabard", sharp=50, orient=P.torso_axis,
                mods=[K.solidify(0.003, offset=-1.0, shell_mat=1, rim_mat=1)])


def leaf(m, base, direction, normal, length, width, mat=0, segs=6):
    """Una hoja plana con nervadura: base, dirección hacia la punta y hacia dónde mira."""
    direction = direction.normalized()
    side = direction.cross(normal).normalized()
    left, mid, right = [], [], []
    for k in range(segs + 1):
        t = k / segs
        w = width * math.sin(math.pi * t) ** 0.8
        curl = normal * (0.006 * math.sin(math.pi * t))
        c = base + direction * (length * t) + curl
        mid.append(m.v(c + normal * 0.0015))
        left.append(m.v(c - side * w))
        right.append(m.v(c + side * w))
    for k in range(segs):
        m.face([left[k], mid[k], mid[k + 1], left[k + 1]], mat)
        m.face([mid[k], right[k], right[k + 1], mid[k + 1]], mat)


def capelet_hem(deg):
    return 1.30 + 0.13 * bell(abs(wrap_deg(deg)), 90, 24)


def build_capelet():
    """La capucha corta sobre los hombros, con hojas superpuestas en el borde."""
    cols, rows = 48, 8
    rings = []
    for r in range(rows + 1):
        t = r / rows
        ring = []
        for i in range(cols):
            deg = i / cols * 360 - 180
            y = lerp(1.555, capelet_hem(deg), t)
            off = lerp(0.012, 0.055, smooth(0, 1, t))
            ring.append(P.torso_point(min(y, 1.55), deg, off) if y > 1.55 else P.torso_point(y, deg, off))
        rings.append(ring)
    m = Mesh()
    m.loft(rings, wrap=True)
    m.build("Capelet", [M["cloak"], M["cloak_in"]], "torso", sharp=50, orient=P.torso_axis,
            mods=[K.solidify(0.004, offset=-1.0, shell_mat=1, rim_mat=1)])

    # Dos vueltas de hojas por el borde, una clara y una oscura
    h = Mesh()
    for layer in range(2):
        n = 26
        for i in range(n):
            deg = i / n * 360 - 180 + layer * (180 / n)
            y = capelet_hem(deg) + 0.03 + layer * 0.025
            p = P.torso_point(y, deg, 0.058 + 0.004 * layer)
            out = (p - P.torso_axis(p))
            out.y = 0
            out.normalize()
            down = (V(0, -1, 0) + out * 0.35).normalized()
            leaf(h, p, down, out, 0.07 + 0.015 * rng.random(), 0.016, mat=layer)
    h.build("Capelet_Leaves", [M["leaf"], M["leaf_dark"]], "torso", sharp=60, orient=P.torso_axis,
            mods=[K.solidify(0.002, offset=0.0)])

    # La bufanda: un rollo de tela alrededor del cuello
    ring = [cowl_center(2 * math.pi * i / 36) for i in range(36)]
    c = Mesh()
    c.tube(ring, lambda t: (0.026 + 0.004 * math.sin(t * 2 * math.pi * 6), 0.034), sides=8, closed=True,
           up=lambda p, t: V(p.x, 0, p.z - 0.012).normalized())
    c.build("Cowl", [M["tunic_dark"]], "gorget", sharp=60, subsurf=1)


def build_brooch():
    """El broche de hoja que cierra la bufanda adelante."""
    c = cowl_center(0.0) + V(0, -0.006, 0.034)
    L = Mesh()
    for k, (ang, ln) in enumerate(((-30, 0.042), (30, 0.042), (0, 0.05))):
        a = math.radians(ang)
        leaf(L, c, V(math.sin(a), -math.cos(a), 0), V(0, 0, 1), ln, 0.012)
    L.build("Brooch", [M["gold"]], "rigid:B-chest", sharp=50, orient=c - V(0, 0, 0.1),
            mods=[K.solidify(0.002, offset=0.0)])


def build_belt_and_quiver():
    P.build_belt()

    # El carcaj en la cadera derecha, con flechas
    bottom, top = V(0.175, 0.70, -0.03), V(0.215, 1.02, -0.10)
    axis = (top - bottom).normalized()
    q = Mesh()
    q.tube([bottom, bottom.lerp(top, 0.5), top], lambda t: 0.034 + 0.004 * t, sides=12, cap_start=True, cap_end=False)
    q.build("Quiver", [M["leather"]], "tasset:.R", sharp=50, orient=lambda p: bottom + axis * (p - bottom).dot(axis),
            mods=[K.solidify(0.003, offset=-1.0)])
    bands = Mesh()
    for t in (0.08, 0.92):
        c = bottom.lerp(top, t)
        side = axis.cross(V(1, 0, 0)).normalized()
        other = axis.cross(side).normalized()
        bands.tube([c + (side * math.cos(a) + other * math.sin(a)) * (0.036 + 0.004 * t) for a in np.linspace(0, 2 * math.pi, 16, endpoint=False)],
                   0.004, sides=5, closed=True)
    bands.build("Quiver_Bands", [M["leather_dark"]], "tasset:.R", sharp=50)
    shafts, vanes = Mesh(), Mesh()
    for i in range(7):
        a = 2 * math.pi * i / 7
        side = axis.cross(V(1, 0, 0)).normalized()
        other = axis.cross(side).normalized()
        off = (side * math.cos(a) + other * math.sin(a)) * 0.017
        start = top - axis * 0.05 + off
        end = top + axis * (0.10 + 0.015 * (i % 3)) + off
        shafts.tube([start, end], 0.0028, sides=5)
        for k in range(3):
            b = 2 * math.pi * k / 3 + a
            d = (side * math.cos(b) + other * math.sin(b))
            leaf(vanes, end - axis * 0.055, axis, d.cross(axis), 0.05, 0.006, mat=k % 2)
    shafts.build("Arrow_Shafts", [M["wood"]], "tasset:.R", sharp=50)
    vanes.build("Arrow_Fletching", [M["feather"], M["leaf"]], "tasset:.R", sharp=60, orient=False,
                mods=[K.solidify(0.0012, offset=0.0)])
    strap = Mesh()
    strap.tube([P.torso_point(1.0, 75, 0.03), top - axis * 0.02 + V(0.0, 0, 0.02)], 0.006, sides=5)
    strap.build("Quiver_Strap", [M["leather_dark"]], "tasset:.R", sharp=50)


def build_arm(side):
    chain = K.arm_chain(side)
    up = V(0, 1, 0)
    P.tube_piece("Sleeve" + side, chain, -0.04, 0.42, 12, lambda d, a: P.ArmRadius(d) + 0.003, M["tunic"], "arm:" + side,
                 up, cols=12, mods=[], trims=False)
    P.tube_piece("Bracer" + side, chain, 0.27, 0.41, 4, lambda d, a: P.ArmRadius(d) + 0.012 + 0.004 * bell(a, 90, 30),
                 M["leather"], "armstiff:" + side, up)
    # Vendas claras y una línea tribal que brilla
    w = Mesh()
    r = Mesh()
    for k, d in enumerate((0.07, 0.10, 0.13)):
        c, t = P.along(chain, d)
        n1 = t.cross(V(0, 0, 1)).normalized()
        n2 = t.cross(n1).normalized()
        rad = P.ArmRadius(d) + 0.006
        w.tube([c + t * (0.01 * math.sin(a)) + (n1 * math.cos(a) + n2 * math.sin(a)) * rad for a in np.linspace(0, 2 * math.pi, 16, endpoint=False)],
               (0.004, 0.009), sides=5, closed=True, up=lambda p, tt, c=c: (p - c).normalized())
    for d in (0.30, 0.38):
        c, t = P.along(chain, d)
        n1 = t.cross(V(0, 0, 1)).normalized()
        n2 = t.cross(n1).normalized()
        rad = P.ArmRadius(d) + 0.0165
        r.tube([c + (n1 * math.cos(a) + n2 * math.sin(a)) * rad for a in np.linspace(0, 2 * math.pi, 18, endpoint=False)],
               0.0018, sides=5, closed=True)
    w.build("Arm_Wraps" + side, [M["wrap"]], "arm:" + side, sharp=60)
    r.build("Bracer_Runes" + side, [M["rune"]], "armstiff:" + side, sharp=60)
    hand_bones = {"B-hand" + side} | {f"B-{f}0{k}{side}" for f in ("thumb", "indexFinger", "middleFinger",
                                                                   "ringFinger", "pinky") for k in (1, 2, 3)}
    K.dummy_part("Glove" + side, hand_bones, M["leather_dark"], skin="keep", inflate=0.002)


def build_leg(side):
    chain = K.leg_chain(side)
    sgn = 1 if side == ".R" else -1
    lat = V(sgn, 0, 0)
    P.tube_piece("Trousers" + side, chain, -0.06, 0.62, 12, lambda d, a: P.LegRadius(d) + 0.006,
                 M["trousers"], "leg:" + side, lat, cols=14, mods=[], trims=False)
    P.tube_piece("Boot" + side, chain, 0.48, 0.80, 5, lambda d, a: P.LegRadius(d) + 0.016 + 0.006 * smooth(0.58, 0.48, d),
                 M["leather"], "legstiff:" + side, lat, cols=16, trims=False)
    # Las tiras que envuelven la bota, en diagonal
    w = Mesh()
    for k in range(5):
        d0 = 0.52 + k * 0.055
        path = []
        for j in range(17):
            a = 2 * math.pi * j / 16
            d = d0 + 0.025 * j / 16
            c, t = P.along(chain, d)
            f = (V(0, 0, 1) - t * t.z).normalized()
            l = t.cross(f).normalized()
            rad = P.LegRadius(d) + 0.0185
            path.append(c + (f * math.cos(a) + l * math.sin(a)) * rad)
        w.tube(path, (0.003, 0.008), sides=5, up=lambda p, tt, ch=chain: (p - P.along(ch, K.chain_param(p, ch)[0])[0]).normalized())
    w.build("Boot_Wraps" + side, [M["leather_dark"]], "legstiff:" + side, sharp=60)
    K.dummy_part("Foot" + side, {"B-foot" + side, "B-toe" + side}, M["leather_dark"], skin="leg:" + side, inflate=0.01)


def build_braids():
    """Dos trenzas finas desde las sienes, con dos cuentas de madera cada una."""
    b, g = Mesh(), Mesh()
    for sgn in (-1, 1):
        top = P.head_point(0.62, sgn * 70, 0.02, features=False)
        path = P.catmull([top, P.head_point(0.25, sgn * 78, 0.03, False), V(sgn * 0.10, 1.52, 0.05), V(sgn * 0.115, 1.40, 0.08)], 28)
        path = [P.body_guard(P.head_guard(p, 0.014), 0.02) for p in path]
        # El trenzado: el grosor sube y baja en cada vuelta
        b.tube(path, lambda t: 0.009 * (1 - 0.35 * t) * (0.8 + 0.2 * abs(math.sin(t * math.pi * 9))), sides=8)
        for k in (12, 20):
            P.sphere(g, path[k], 0.011, rings=4, sides=8)
        P.sphere(g, path[-1] + V(0, -0.01, 0), 0.008, rings=4, sides=8)
    b.build("Braids", [M["hair"]], "hair", sharp=70)
    g.build("Braid_Beads", [M["wood"]], "hair", sharp=60)


# =========================================================
# TODO
# =========================================================

def build():
    K.begin()
    use_elf_body()
    materials()
    body_guard = P.body_guard
    P.body_guard = lambda p, margin: cowl_guard(body_guard(p, margin), margin)

    P.build_torso()
    P.build_head()
    build_ears()
    build_jerkin()
    build_skirt()
    build_belt_and_quiver()
    build_capelet()
    build_brooch()
    for side in (".L", ".R"):
        build_arm(side)
        build_leg(side)
    P.build_cape()
    P.build_hair()
    build_braids()

    path = os.path.join(K.HERE, NAME + ".blend")
    bpy.context.scene["merc_name"] = NAME
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print(f"[Ranger] guardado {path}")


if __name__ == "__main__":
    build()
    import merc_export
    merc_export.run(NAME, preview_only="--preview" in sys.argv)
