# -*- coding: utf-8 -*-
# ============================================================
# paladin.py
#
# La Paladina en Blender (10 de noviembre de 2026; reemplaza a la receta PaladinModel.cs, de la que salen
# las tablas de medidas). Una aasimar guerrera de la fe: armadura de placas blanca con filos de oro, peto
# con brigantina de remaches en la cintura, gola alta con medalla, hombreras grandes de láminas, escarcelas,
# grebas, tabardo y capa carmesí, guantes de cuero. Detalles de fanática: flores de lis, sellos de cera con
# tiras de pergamino, un rosario y una aureola de rayos detrás de la cabeza. Pelo rubio, largo y abundante.
# Los ojos dorados brillan con luz propia. Sin alas, martillo ni escudo (son de la clase).
#
# Correr (arma Paladin.blend, hornea y exporta a Out/Paladin):
#   "H:/blender.exe" -b --factory-startup --python Blender/paladin.py
# Solo armar y renderizar, sin hornear (para probar formas rápido): agregar  -- --preview
# ============================================================
import math
import os
import random
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import merc_kit as K   # noqa: E402
from merc_kit import V, Keys, Mesh, smooth, bell, lerp, wrap_deg   # noqa: E402

NAME = "Paladin"
rng = random.Random(5531)

# El halo detrás de la cabeza. False = sin aureola (si en juego tapa la vista).
WITH_HALO = True

# ---------- Medidas (de la receta vieja, en el espacio de Unity) ----------
TorsoWidth = Keys(0.845, 0.06, 0.88, 0.135, 0.93, 0.170, 0.98, 0.168, 1.03, 0.150, 1.08, 0.126, 1.14, 0.112,
                  1.19, 0.116, 1.25, 0.134, 1.31, 0.152, 1.36, 0.162, 1.41, 0.172, 1.46, 0.170, 1.495, 0.112,
                  1.52, 0.062, 1.55, 0.050, 1.62, 0.046)
TorsoDepth = Keys(0.845, 0.06, 0.88, 0.095, 0.93, 0.116, 0.98, 0.116, 1.03, 0.104, 1.08, 0.090, 1.14, 0.083,
                  1.19, 0.086, 1.25, 0.096, 1.31, 0.108, 1.36, 0.112, 1.41, 0.104, 1.46, 0.090, 1.495, 0.072,
                  1.52, 0.058, 1.55, 0.052, 1.62, 0.050)
TorsoCenter = Keys(0.845, 0.015, 0.93, 0.0, 1.03, 0.008, 1.14, 0.02, 1.25, 0.028, 1.36, 0.036, 1.46, 0.015,
                   1.52, 0.005, 1.62, 0.016)

ArmRadius = Keys(-0.05, 0.040, 0, 0.055, 0.06, 0.058, 0.12, 0.050, 0.18, 0.043, 0.234, 0.038, 0.27, 0.042,
                 0.32, 0.040, 0.38, 0.032, 0.43, 0.026, 0.445, 0.025)
LegRadius = Keys(-0.07, 0.082, 0, 0.090, 0.08, 0.092, 0.16, 0.085, 0.26, 0.072, 0.35, 0.060, 0.41, 0.056,
                 0.46, 0.055, 0.53, 0.060, 0.62, 0.053, 0.72, 0.043, 0.79, 0.040, 0.82, 0.041)

HeadBottom, HeadHeight = 1.555, 0.255
HeadCenter = V(0.0, 1.685, 0.02)
HeadWidth = Keys(0, 0.020, 0.08, 0.039, 0.2, 0.055, 0.35, 0.068, 0.5, 0.077, 0.7, 0.081, 0.85, 0.068,
                 0.95, 0.042, 1, 0.005)
HeadDepth = Keys(0, 0.018, 0.1, 0.040, 0.22, 0.060, 0.35, 0.082, 0.5, 0.098, 0.7, 0.104, 0.85, 0.090,
                 0.95, 0.055, 1, 0.006)
HeadOffset = Keys(0, 0.060, 0.1, 0.047, 0.22, 0.032, 0.35, 0.022, 0.5, 0.012, 0.7, 0.005, 0.85, 0, 1, 0)
Hairline = Keys(0, 0.80, 20, 0.77, 40, 0.71, 60, 0.63, 90, 0.43, 120, 0.26, 180, 0.13)
FaceLeft, FaceRight, FaceBottom, FaceTop = -0.11, 0.11, 1.54, 1.83
HaloCenter = V(0.0, 1.73, -0.24)

# ---------- Colores (sRGB, como en la receta; la aspereza es 1 - el pulido de antes) ----------
M = {}
SKIN = (0.95, 0.76, 0.64)


def materials():
    M["skin"] = K.material("Skin", SKIN, rough=0.72)
    M["skin_plain"] = K.material("SkinPlain", SKIN, rough=0.72)
    M["hair"] = K.material("Hair", (0.93, 0.71, 0.29), rough=0.6)
    M["plate"] = K.material("Plate", (0.86, 0.84, 0.80), rough=0.38, metal=0.0)
    M["plate_dark"] = K.material("PlateShade", (0.62, 0.62, 0.66), rough=0.45)
    M["gold"] = K.material("Gold", (0.92, 0.66, 0.18), rough=0.16, metal=1.0)
    M["crimson"] = K.material("Crimson", (0.56, 0.05, 0.07), rough=0.9)
    M["lining"] = K.material("Lining", (0.25, 0.02, 0.04), rough=0.95)
    M["under"] = K.material("Under", (0.10, 0.09, 0.11), rough=0.9)
    M["leather"] = K.material("Leather", (0.36, 0.19, 0.09), rough=0.68)
    M["glove"] = K.material("Glove", (0.16, 0.12, 0.11), rough=0.62)
    M["parchment"] = K.material("Parchment", (0.90, 0.83, 0.62), rough=0.95)
    M["wax"] = K.material("Wax", (0.70, 0.07, 0.06), rough=0.38)
    M["wood"] = K.material("Bead", (0.62, 0.40, 0.18), rough=0.45)
    M["halo"] = K.material("Halo", (1.0, 0.82, 0.38), rough=0.2, metal=1.0, emit=(1.0, 0.82, 0.4), emit_strength=0.35)
    hair_nodes(M["hair"])
    face_nodes(M["skin"])
    plate_nodes(M["plate"])


# =========================================================
# MATERIALES CON DIBUJO
# =========================================================

def hair_nodes(mat):
    """Rubio: más oscuro en la raíz, claro en las puntas, con mechas."""
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    geo = K.node(nt, "ShaderNodeNewGeometry", (-900, 200))
    sep = K.node(nt, "ShaderNodeSeparateXYZ", (-700, 200))
    nt.links.new(geo.outputs["Position"], sep.inputs[0])
    rng_ = K.node(nt, "ShaderNodeMapRange", (-500, 200))
    rng_.inputs["From Min"].default_value = 1.80
    rng_.inputs["From Max"].default_value = 1.15
    nt.links.new(sep.outputs["Z"], rng_.inputs["Value"])
    ramp = K.node(nt, "ShaderNodeValToRGB", (-300, 200))
    cr = ramp.color_ramp
    cr.elements[0].position = 0.0
    cr.elements[0].color = (*K.srgb((0.62, 0.40, 0.12)), 1)
    cr.elements[1].position = 1.0
    cr.elements[1].color = (*K.srgb((1.0, 0.88, 0.52)), 1)
    mid = cr.elements.new(0.3)
    mid.color = (*K.srgb((0.93, 0.71, 0.29)), 1)
    nt.links.new(rng_.outputs["Result"], ramp.inputs["Fac"])

    scale = K.node(nt, "ShaderNodeVectorMath", (-700, -100))
    scale.operation = 'MULTIPLY'
    scale.inputs[1].default_value = (90.0, 90.0, 5.0)
    nt.links.new(geo.outputs["Position"], scale.inputs[0])
    noise = K.node(nt, "ShaderNodeTexNoise", (-500, -100))
    noise.inputs["Scale"].default_value = 1.0
    noise.inputs["Detail"].default_value = 3.0
    nt.links.new(scale.outputs[0], noise.inputs["Vector"])
    streak = K.node(nt, "ShaderNodeMapRange", (-300, -100))
    streak.inputs["From Min"].default_value = 0.3
    streak.inputs["From Max"].default_value = 0.7
    streak.inputs["To Min"].default_value = 0.78
    streak.inputs["To Max"].default_value = 1.12
    nt.links.new(noise.outputs["Fac"], streak.inputs["Value"])
    mul = K.node(nt, "ShaderNodeMix", (-100, 100))
    mul.data_type = 'RGBA'
    mul.blend_type = 'MULTIPLY'
    mul.inputs["Factor"].default_value = 1.0
    nt.links.new(ramp.outputs["Color"], mul.inputs[6])
    comb = K.node(nt, "ShaderNodeCombineColor", (-200, -250))
    nt.links.new(streak.outputs["Result"], comb.inputs[0])
    nt.links.new(streak.outputs["Result"], comb.inputs[1])
    nt.links.new(streak.outputs["Result"], comb.inputs[2])
    nt.links.new(comb.outputs[0], mul.inputs[7])
    nt.links.new(mul.outputs[2], bsdf.inputs["Base Color"])


def plate_nodes(mat):
    """El esmalte blanco: un poco más gris en lo que mira abajo, para que la placa tenga volumen."""
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    geo = K.node(nt, "ShaderNodeNewGeometry", (-700, 200))
    sep = K.node(nt, "ShaderNodeSeparateXYZ", (-500, 200))
    nt.links.new(geo.outputs["Normal"], sep.inputs[0])
    fac = K.node(nt, "ShaderNodeMapRange", (-300, 200))
    fac.inputs["From Min"].default_value = 0.6
    fac.inputs["From Max"].default_value = -0.9
    nt.links.new(sep.outputs["Z"], fac.inputs["Value"])
    mix = K.node(nt, "ShaderNodeMix", (-100, 200))
    mix.data_type = 'RGBA'
    mix.inputs[6].default_value = (*K.srgb((0.90, 0.88, 0.84)), 1)
    mix.inputs[7].default_value = (*K.srgb((0.66, 0.66, 0.72)), 1)
    nt.links.new(fac.outputs["Result"], mix.inputs["Factor"])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])


FACE_RES = 512


def face_image():
    """La cara pintada en una textura propia (proyectada de frente): cejas, delineado, labios, rubor y
    los ojos dorados. Se hornea al atlas al exportar."""
    n = FACE_RES
    u = (np.arange(n) + 0.5) / n
    xs = FaceLeft + u * (FaceRight - FaceLeft)
    ys = FaceBottom + u * (FaceTop - FaceBottom)
    X, Y = np.meshgrid(xs, ys)          # fila = y (abajo arriba), columna = x (derecha del personaje +)
    AX = np.abs(X)

    skin = np.array(K.srgb(SKIN))
    col = np.ones((n, n, 3)) * skin
    glow = np.zeros((n, n, 3))

    def paint(mask, color):
        c = np.array(K.srgb(color))
        m = np.clip(mask, 0, 1)[..., None]
        col[:] = col * (1 - m) + c * m

    def g(x, c, w):
        return np.exp(-((x - c) / w) ** 2)

    def line(dist, width):
        """Una línea con borde suave: dist es la distancia a la línea."""
        return np.clip(1 - np.abs(dist) / width, 0, 1)

    # Rubor en los pómulos
    paint(0.22 * g(AX, 0.046, 0.016) * g(Y, 1.648, 0.012), (0.93, 0.52, 0.50))

    # Labios: arco de cupido arriba, más lleno abajo
    lip_y = 1.6065
    mouth = 0.0195
    k = np.clip(1 - (X / mouth) ** 2, 0, 1)
    top = lip_y + 0.0042 * k ** 0.6 - 0.0012 * g(X, 0, 0.004)
    bottom = lip_y - 0.0060 * k ** 0.7
    lips = np.clip((top - Y) / 0.0012, 0, 1) * np.clip((Y - bottom) / 0.0012, 0, 1) * (AX < mouth)
    paint(lips * 0.85, (0.80, 0.40, 0.40))
    paint(0.7 * g(Y, lip_y, 0.0007) * (AX < mouth * 0.92), (0.45, 0.18, 0.18))

    # Ojos: almendra dorada que brilla, con el delineado arriba y una colita hacia afuera
    eye_x, eye_y, half_w, half_h = 0.0335, 1.6825, 0.0165, 0.0068
    dx = (AX - eye_x) / half_w
    shape = np.clip(1 - dx ** 2, 0, 1) ** 0.75
    up = eye_y + half_h * shape - 0.0012 * dx
    down = eye_y - half_h * 0.8 * shape - 0.0006 * dx
    inside = ((Y < up) & (Y > down) & (np.abs(dx) < 1)).astype(float)
    r = np.sqrt(((AX - eye_x) / 0.0075) ** 2 + ((Y - eye_y) / 0.0075) ** 2)
    iris = np.clip(1.15 - r, 0, 1)
    eye_col = np.array(K.srgb((1.0, 0.70, 0.16)))
    core = np.array(K.srgb((1.0, 0.97, 0.80)))
    white = np.array(K.srgb((0.98, 0.93, 0.80)))
    eye = white * (1 - iris[..., None]) * 0.85 + eye_col * iris[..., None]
    eye = eye * (1 - np.clip(1.6 - r * 2.2, 0, 1)[..., None]) + core * np.clip(1.6 - r * 2.2, 0, 1)[..., None]
    col[:] = col * (1 - inside[..., None]) + eye * inside[..., None]
    # Brillo dorado (con la piel ya en lineal, el blanco del ojo brillando se veía todo blanco)
    glow[:] = glow * (1 - inside[..., None]) + eye_col * inside[..., None] * (0.25 + 0.45 * iris[..., None])
    # Delineado y pestañas: más grueso hacia afuera, con una colita
    liner = np.clip(1 - np.abs(Y - (up + 0.0008)) / (0.0009 + 0.0011 * np.clip(dx, 0, 1)), 0, 1) * (np.abs(dx) < 1.05)
    wing = np.clip(1 - np.abs(Y - (eye_y + 0.002 + (AX - eye_x - half_w) * 0.5)) / 0.0010, 0, 1) \
        * ((AX - eye_x - half_w) > 0) * ((AX - eye_x - half_w) < 0.006)
    lid = np.clip(liner + wing, 0, 1)
    paint(lid, (0.22, 0.12, 0.06))
    glow[:] = glow * (1 - lid[..., None])
    paint(0.35 * g(Y, up + 0.0035, 0.0018) * (np.abs(dx) < 1), (0.80, 0.56, 0.48))   # sombra del párpado

    # Cejas: arco fino, más alto afuera
    t = np.clip((AX - 0.012) / 0.042, 0, 1)
    brow_y = 1.7005 + 0.0045 * np.sin(t * math.pi * 0.85) - 0.002 * t
    thick = 0.0024 * (1.0 - 0.6 * t)
    brow = np.clip(1 - np.abs(Y - brow_y) / thick, 0, 1) * (AX > 0.011) * (AX < 0.056)
    paint(brow * 0.9, (0.55, 0.36, 0.14))

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


def face_nodes(mat):
    paint_img, glow_img = face_image()
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    uv = K.node(nt, "ShaderNodeUVMap", (-900, 200))
    uv.uv_map = "FaceUV"
    tex = K.node(nt, "ShaderNodeTexImage", (-650, 250))
    tex.image = paint_img
    tex.extension = 'EXTEND'
    gtex = K.node(nt, "ShaderNodeTexImage", (-650, -50))
    gtex.image = glow_img
    gtex.extension = 'EXTEND'
    nt.links.new(uv.outputs["UV"], tex.inputs["Vector"])
    nt.links.new(uv.outputs["UV"], gtex.inputs["Vector"])
    # Solo la cara: lo de atrás de la cabeza no lleva lo pintado
    geo = K.node(nt, "ShaderNodeNewGeometry", (-900, -300))
    sep = K.node(nt, "ShaderNodeSeparateXYZ", (-700, -300))
    nt.links.new(geo.outputs["Normal"], sep.inputs[0])
    front = K.node(nt, "ShaderNodeMapRange", (-500, -300))
    front.inputs["From Min"].default_value = -0.15
    front.inputs["From Max"].default_value = -0.45
    nt.links.new(sep.outputs["Y"], front.inputs["Value"])
    mix = K.node(nt, "ShaderNodeMix", (-300, 200))
    mix.data_type = 'RGBA'
    mix.inputs[6].default_value = (*K.srgb(SKIN), 1)
    nt.links.new(front.outputs["Result"], mix.inputs["Factor"])
    nt.links.new(tex.outputs["Color"], mix.inputs[7])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    gmul = K.node(nt, "ShaderNodeMix", (-300, -100))
    gmul.data_type = 'RGBA'
    gmul.blend_type = 'MULTIPLY'
    gmul.inputs["Factor"].default_value = 1.0
    nt.links.new(gtex.outputs["Color"], gmul.inputs[6])
    nt.links.new(front.outputs["Result"], gmul.inputs[7])
    nt.links.new(gmul.outputs[2], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.0


# =========================================================
# TORSO
# =========================================================

def torso_dir(deg):
    a = math.radians(deg)
    s, c = math.sin(a), math.cos(a)
    rnd = 2.0 / 2.5
    return math.copysign(abs(s) ** rnd, s), math.copysign(abs(c) ** rnd, c)


def torso_point(y, deg, off):
    dx, dz = torso_dir(deg)
    return V(dx * (TorsoWidth(y) + off), y, TorsoCenter(y) + dz * (TorsoDepth(y) + off))


def torso_axis(p):
    return V(0.0, p.y, TorsoCenter(min(max(p.y, 0.845), 1.62)))


def skirt_point(y, deg, off, flare):
    dx, dz = torso_dir(deg)
    p = torso_point(max(y, 0.97), deg, off)
    p.x += dx * max(0.0, 0.97 - y) * flare
    p.z += dz * max(0.0, 0.97 - y) * flare
    p.y = y
    return p


def build_torso():
    # Termina abajo de la gola: arriba se ve el cuello (Neck)
    heights = [0.845, 0.87, 0.90, 0.93, 0.97, 1.01, 1.05, 1.09, 1.13, 1.17, 1.21, 1.25, 1.29, 1.33, 1.37,
               1.41, 1.44, 1.47, 1.495, 1.51]
    cols = 24
    rings = [[torso_point(y, i / cols * 360 - 180, 0.0) for i in range(cols)] for y in heights]
    m = Mesh()
    m.loft(rings, wrap=True, cap_start=True)
    m.build("Body_Under", [M["under"]], "torso", sharp=60)


def cuirass_top(deg):
    return 1.488 - 0.014 * smooth(70, 0, abs(wrap_deg(deg)))


def bust(y, deg):
    return 0.034 * bell(y, 1.335, 0.068) * (bell(deg, 26, 30) + bell(deg, -26, 30)) * 0.85


def build_cuirass():
    cols = 56
    bottom = 1.205
    rows = [bottom, 1.212, 1.225, 1.245, 1.265, 1.285, 1.305, 1.325, 1.345, 1.365, 1.385, 1.405, 1.425, 1.445, 1.46]
    rings = []
    for k, h in enumerate(rows + [None, None, None]):
        ring = []
        for i in range(cols):
            deg = i / cols * 360 - 180
            y = h if h is not None else lerp(1.46, cuirass_top(deg), [0.5, 0.88, 1.0][k - len(rows)])
            a = abs(wrap_deg(deg))
            keel = 0.0045 * bell(deg, 0, 5) * smooth(1.22, 1.27, y) * (1 - smooth(1.40, 1.46, y))
            shell = 0.016 + bust(y, deg) + keel + 0.004 * bell(a, 180, 7)
            # El borde de abajo se curva un poco hacia el cuerpo
            shell -= 0.004 * (1 - smooth(bottom, bottom + 0.02, y))
            ring.append(torso_point(y, deg, shell))
        rings.append(ring)
    m = Mesh()
    m.loft(rings, wrap=True)
    obj = m.build("Cuirass", [M["plate"]], "torso", sharp=50, orient=torso_axis,
                  mods=[K.solidify(0.004, offset=-1.0), K.bevel(0.001, 1)])
    K.trim(obj, M["gold"], radius=0.0042, flat=0.8, name="Cuirass_Trim")
    return obj


def build_brigandine():
    """La cintura: tela carmesí con remaches de oro."""
    cols = 48
    rows = [1.03, 1.07, 1.11, 1.15, 1.19, 1.215]
    rings = [[torso_point(y, i / cols * 360 - 180, 0.011) for i in range(cols)] for y in rows]
    m = Mesh()
    m.loft(rings, wrap=True)
    m.build("Brigandine", [M["crimson"]], "torso", sharp=50, orient=torso_axis,
            mods=[K.solidify(0.003, offset=-1.0)])

    r = Mesh()
    for y in (1.075, 1.125, 1.175):
        for i in range(22):
            deg = i / 22 * 360 - 180 + (8.2 if round((y - 1.075) / 0.05) % 2 else 0)
            p = torso_point(y, deg, 0.013)
            n = (p - torso_axis(p)).normalized()
            dome(r, p, n, 0.0042, rows=1, sides=6)
    r.build("Brigandine_Rivets", [M["gold"]], "torso", sharp=80)


def dome(m, center, normal, radius, rows=2, sides=7, mat=0):
    """Una cúpula chica (remaches)."""
    normal = normal.normalized()
    t1 = normal.cross(V(0, 1, 0))
    if t1.length < 1e-3:
        t1 = normal.cross(V(1, 0, 0))
    t1.normalize()
    t2 = normal.cross(t1)
    rings = []
    for k in range(rows + 1):
        a = k / (rows + 1) * math.pi / 2
        rr, hh = math.cos(a) * radius, math.sin(a) * radius * 0.8
        rings.append([center + normal * hh + (t1 * math.cos(2 * math.pi * s / sides) + t2 * math.sin(2 * math.pi * s / sides)) * rr
                      for s in range(sides)])
    m.loft(rings, wrap=True, cap_end=True, mat=mat)


def build_belt():
    cols = 48
    rows = [0.995, 1.04]
    rings = [[torso_point(y, i / cols * 360 - 180, 0.020) for i in range(cols)] for y in rows]
    m = Mesh()
    m.loft(rings, wrap=True)
    m.build("Belt", [M["leather"]], "torso", sharp=50, orient=torso_axis,
            mods=[K.solidify(0.006, offset=-1.0), K.bevel(0.0012, 1)])
    # La hebilla
    c = torso_point(1.0175, 0, 0.028)
    b = Mesh()
    w, h = 0.026, 0.022
    frame = [c + V(-w, -h, 0), c + V(w, -h, 0), c + V(w, h, 0), c + V(-w, h, 0)]
    path = []
    for i in range(4):
        a, bb = frame[i], frame[(i + 1) % 4]
        for k in range(4):
            path.append(a.lerp(bb, k / 4))
    b.tube(path, (0.0045, 0.003), sides=6, closed=True, up=lambda p, t: V(0, 0, 1))
    b.tube([c + V(0, -h, 0.002), c + V(0, h, 0.002)], 0.0022, sides=6)
    b.build("Belt_Buckle", [M["gold"]], "torso", sharp=50)


# =========================================================
# GOLA Y CABEZA
# =========================================================

def gorget_top(deg):
    return 1.535 + 0.065 * smooth(40, 150, abs(wrap_deg(deg)))


def build_gorget():
    cols = 40
    rows = [0.0, 0.3, 0.6, 0.85, 1.0]
    rings = []
    for t in rows:
        ring = []
        for i in range(cols):
            deg = i / cols * 360 - 180
            y = lerp(1.47, gorget_top(deg), t)
            radius = lerp(0.104, 0.090, smooth(0, 0.6, t)) + 0.010 * smooth(0.82, 1, t)
            a = math.radians(deg)
            ring.append(V(math.sin(a) * radius, y, 0.012 + math.cos(a) * radius * 1.04))
        rings.append(ring)
    m = Mesh()
    m.loft(rings, wrap=True)
    obj = m.build("Gorget", [M["plate"]], "gorget", sharp=50, orient=lambda p: V(0, p.y, 0.012),
                  mods=[K.solidify(0.003, offset=-1.0), K.bevel(0.0008, 1)])
    K.trim(obj, M["gold"], radius=0.0038, flat=0.8, name="Gorget_Trim")

    # El collar de abajo, que monta sobre el peto
    rings = []
    for t in (0.0, 0.5, 1.0):
        ring = []
        for i in range(cols):
            deg = i / cols * 360 - 180
            a = math.radians(deg)
            y = lerp(1.452, 1.49, t) - 0.012 * smooth(60, 0, abs(deg))
            radius = lerp(0.135, 0.108, t)
            ring.append(V(math.sin(a) * radius * 1.05, y, 0.016 + math.cos(a) * radius))
        rings.append(ring)
    m = Mesh()
    m.loft(rings, wrap=True)
    obj = m.build("Collar", [M["plate"]], "gorget", sharp=50, orient=lambda p: V(0, p.y + 0.2, 0.016),
                  mods=[K.solidify(0.003, offset=-1.0)])
    K.trim(obj, M["gold"], radius=0.0036, flat=0.8, name="Collar_Trim")

    # La medalla con la flor de lis
    c = V(0.0, 1.405, 0.0)
    c.z = max(torso_point(y, d, 0.016 + bust(y, d)).z for y in (1.38, 1.405, 1.43) for d in (-12, 0, 12)) + 0.007
    md = Mesh()
    ring = [c + V(math.cos(a) * 0.024, math.sin(a) * 0.024, 0) for a in np.linspace(0, 2 * math.pi, 24, endpoint=False)]
    back = [p + V(0, 0, -0.005) for p in ring]
    md.loft([back, ring], wrap=True, cap_start=True, cap_end=True)
    md.tube([c + V(math.cos(a) * 0.024, math.sin(a) * 0.024, 0.0005) for a in np.linspace(0, 2 * math.pi, 24, endpoint=False)],
            0.0028, sides=6, closed=True)
    md.tube([V(0, 1.45, c.z - 0.012), V(0, 1.433, c.z - 0.004), c + V(0, 0.026, -0.002)], 0.002, sides=6)
    md.build("Medal", [M["gold"]], "rigid:B-chest", sharp=45)
    K.glyph("⚜", 0.03, 0.003, c + V(0, 0.001, 0.0005), V(0, 0, 1), V(0, 1, 0), M["plate"], "rigid:B-chest", name="Medal_Lis")


def head_point(h, deg, off=0.0, features=True):
    a = math.radians(deg)
    s, c = math.sin(a), math.cos(a)
    rnd = 2.0 / 2.3
    x = math.copysign(abs(s) ** rnd, s) * HeadWidth(h)
    z = HeadOffset(h) + math.copysign(abs(c) ** rnd, c) * HeadDepth(h)
    ad = abs(deg)
    if features and off <= 0 and ad < 90:
        z += 0.009 * bell(deg, 0, 12) * bell(h, 0.36, 0.06)                         # nariz
        z += 0.004 * bell(deg, 0, 8) * smooth(0.36, 0.42, h) * (1 - smooth(0.48, 0.56, h))
        z += 0.005 * bell(h, 0.565, 0.04) * bell(deg, 0, 38)                         # ceja
        z -= 0.006 * bell(ad, 21, 13) * bell(h, 0.5, 0.045)                          # cuenca del ojo
        z += 0.005 * bell(deg, 0, 16) * bell(h, 0.2, 0.03)                           # labios
        z -= 0.003 * bell(deg, 0, 20) * bell(h, 0.14, 0.02)                          # bajo el labio
        z += 0.004 * bell(deg, 0, 18) * bell(h, 0.07, 0.04)                          # mentón
        x *= 1 + 0.05 * bell(h, 0.36, 0.06)                                          # pómulos
    p = V(x, HeadBottom + HeadHeight * h, 0.02 + z)
    if off:
        p = p + (p - HeadCenter).normalized() * off
    return p


def build_head():
    heights = [0, 0.03, 0.06, 0.1, 0.14, 0.17, 0.2, 0.23, 0.26, 0.3, 0.34, 0.38, 0.42, 0.46, 0.5, 0.53, 0.56,
               0.6, 0.66, 0.72, 0.8, 0.88, 0.95, 1.0]
    half = [0, 4, 8, 13, 19, 26, 34, 43, 54, 66, 80, 96, 115, 135, 157, 180]
    degs = [-d for d in reversed(half[1:-1])] + half
    rings = [[head_point(h, d) for d in degs] for h in heights]
    m = Mesh()
    m.loft(rings, wrap=True, cap_start=True, cap_end=True)
    obj = m.build("Head", [M["skin"]], "rigid:B-head", sharp=85, subsurf=1)
    # La cara se pinta proyectada de frente
    me = obj.data
    uv = me.uv_layers.new(name="FaceUV")
    for loop in me.loops:
        p = K.U(me.vertices[loop.vertex_index].co)
        uv.data[loop.index].uv = ((p.x - FaceLeft) / (FaceRight - FaceLeft), (p.y - FaceBottom) / (FaceTop - FaceBottom))

    # El cuello
    rings = []
    for y in (1.47, 1.53, 1.59, 1.625):
        r = lerp(0.052, 0.044, smooth(1.47, 1.6, y))
        rings.append([V(math.sin(a) * r, y, 0.006 + math.cos(a) * r * 1.05)
                      for a in np.linspace(0, 2 * math.pi, 16, endpoint=False)])
    n = Mesh()
    n.loft(rings, wrap=True)
    n.build("Neck", [M["skin_plain"]], "torso", sharp=70, orient=lambda p: V(0, p.y, 0.006))


# =========================================================
# HOMBRERAS
# =========================================================

def pauldron_frame(side):
    joint = K.SK.pos("B-upperArm" + side)
    axis = (K.SK.end("B-upperArm" + side) - joint).normalized()
    up = V(0, 1, 0)
    up = (up - axis * up.dot(axis)).normalized()
    front = V(0, 0, 1)
    front = (front - axis * front.dot(axis) - up * front.dot(up)).normalized()
    return joint + up * 0.012, axis, up, front


def build_pauldron(side):
    origin, axis, up, front = pauldron_frame(side)
    sgn = 1 if side == ".R" else -1

    def band(name, a_rows, r_rows, span, cols=18):
        rings = []
        for a, r in zip(a_rows, r_rows):
            ring = []
            for i in range(cols + 1):
                th = math.radians(lerp(-span, span, i / cols))
                rr = r * (1 - 0.07 * (math.degrees(th) / span) ** 2)
                d = up * math.cos(th) + front * math.sin(th)
                ring.append(origin + axis * a + d * rr)
            rings.append(ring)
        m = Mesh()
        m.loft(rings)
        obj = m.build(name + side, [M["plate"]], "pauldron:" + side, sharp=50,
                      orient=lambda p: origin + axis * (p - origin).dot(axis),
                      mods=[K.solidify(0.0035, offset=-1.0), K.bevel(0.0008, 1)])
        # Ribete de oro en el borde de afuera (el que mira al codo) y en los costados
        K.trim(obj, M["gold"], radius=0.0036, flat=0.8, name=name + "_Trim" + side,
               select=lambda pts: True)
        return obj

    # De abajo hacia arriba: cada lámina queda tapada por la de arriba
    band("Pauldron_Lame3", [0.112, 0.135, 0.16], [0.100, 0.104, 0.100], 100)
    band("Pauldron_Lame2", [0.072, 0.097, 0.122], [0.110, 0.114, 0.110], 108)
    band("Pauldron_Lame1", [0.032, 0.057, 0.082], [0.118, 0.122, 0.118], 114)
    band("Pauldron_Dome", [-0.088, -0.07, -0.045, -0.015, 0.012, 0.035, 0.047],
         [0.058, 0.088, 0.110, 0.122, 0.128, 0.128, 0.126], 120, cols=22)

    # Flor de lis en la cúpula, mirando hacia afuera y arriba
    nrm = (up * 0.8 + axis * 0.6).normalized()
    c = origin + nrm * 0.131
    K.glyph("⚜", 0.055, 0.003, c, nrm, front, M["gold"], "pauldron:" + side, name="Pauldron_Lis" + side)


# =========================================================
# BRAZOS
# =========================================================

def along(chain, d):
    acc = 0.0
    for i in range(len(chain) - 1):
        a, b = chain[i], chain[i + 1]
        L = (b - a).length
        last = i == len(chain) - 2
        if d <= acc + L or last:
            t = (d - acc) / L if not (i == 0 and d < 0) else d / L
            return a + (b - a) * t, (b - a) / L
        acc += L
    return chain[-1], (chain[-1] - chain[-2]).normalized()


def limb_ring(chain, d, radius, lateral, angles):
    """Un anillo alrededor del miembro. a = 0 es el frente (+Z), 90 es hacia lateral."""
    c, t = along(chain, d)
    f = V(0, 0, 1)
    f = (f - t * f.dot(t)).normalized()
    l = (lateral - t * lateral.dot(t) - f * lateral.dot(f)).normalized()
    out = []
    for a in angles:
        r = radius(a) if callable(radius) else radius
        ar = math.radians(a)
        out.append(c + (f * math.cos(ar) + l * math.sin(ar)) * r)
    return out


def limb_axis(chain):
    def inside(p):
        s, _ = K.chain_param(p, chain)
        c, _ = along(chain, s)
        return c
    return inside


def tube_piece(name, chain, d0, d1, rows, radius_fn, mat, skin, lateral, cols=18, span=None, mods=None,
               subsurf=0, trims=True):
    angles = [lerp(-span, span, i / cols) for i in range(cols + 1)] if span else [i / cols * 360 for i in range(cols)]
    rings = []
    for k in range(rows + 1):
        d = lerp(d0, d1, k / rows)
        rings.append(limb_ring(chain, d, lambda a, d=d: radius_fn(d, a), lateral, angles))
    m = Mesh()
    m.loft(rings, wrap=span is None)
    obj = m.build(name, [mat], skin, sharp=50, orient=limb_axis(chain),
                  mods=mods if mods is not None else [K.solidify(0.003, offset=-1.0), K.bevel(0.0008, 1)],
                  subsurf=subsurf)
    if trims:
        K.trim(obj, M["gold"], radius=0.0034, flat=0.8, name=name + "_Trim")
    return obj


def build_arm(side):
    chain = K.arm_chain(side)
    up = V(0, 1, 0)
    # La manga de abajo
    tube_piece("Sleeve" + side, chain, -0.04, 0.43, 14, lambda d, a: ArmRadius(d), M["under"], "arm:" + side,
               up, cols=12, mods=[], trims=False)
    # Brazal de arriba, codera, brazal de abajo y el puño del guantelete
    tube_piece("Rerebrace" + side, chain, 0.07, 0.200, 3,
               lambda d, a: ArmRadius(d) + 0.009 + 0.003 * bell(a, 0, 30), M["plate"], "armstiff:" + side, up)
    tube_piece("Couter" + side, chain, 0.198, 0.268, 4,
               lambda d, a: ArmRadius(d) + 0.012 + 0.012 * bell(d, 0.234, 0.022) * bell(a, 180, 70),
               M["plate"], "arm:" + side, up)
    tube_piece("Vambrace" + side, chain, 0.266, 0.352, 3,
               lambda d, a: ArmRadius(d) + 0.011 + 0.003 * bell(a, 90, 25), M["plate"], "armstiff:" + side, up)
    tube_piece("Cuff" + side, chain, 0.35, 0.425, 3,
               lambda d, a: lerp(0.058, 0.034, smooth(0.35, 0.425, d)), M["plate"], "armstiff:" + side, up)

    # El abanico de la codera, atrás del codo
    c, t = along(chain, 0.234)
    back = V(0, 0, -1)
    center = c + back * (ArmRadius(0.234) + 0.026)
    fan = Mesh()
    rings = [[center]]
    for r in (0.016, 0.03, 0.036):
        ring = []
        for i in range(20):
            a = 2 * math.pi * i / 20
            q = center + t * (math.cos(a) * r * 0.9) + V(0, 1, 0) * (math.sin(a) * r)
            q += back * (0.004 * (1 - r / 0.036))
            ring.append(q)
        rings.append(ring)
    ids = [[fan.v(p) for p in ring] for ring in rings]
    for i in range(20):
        fan.face([ids[0][0], ids[1][i], ids[1][(i + 1) % 20]])
    for k in range(1, len(ids) - 1):
        for i in range(20):
            j = (i + 1) % 20
            fan.face([ids[k][i], ids[k][j], ids[k + 1][j], ids[k + 1][i]])
    obj = fan.build("Couter_Fan" + side, [M["plate"]], "arm:" + side, sharp=50, orient=c,
                    mods=[K.solidify(0.003, offset=-1.0)])
    K.trim(obj, M["gold"], radius=0.0032, flat=0.8, name="Couter_Fan_Trim" + side)

    # La mano: la del Dummy, con guante
    hand_bones = {"B-hand" + side} | {f"B-{f}0{k}{side}" for f in ("thumb", "indexFinger", "middleFinger",
                                                                   "ringFinger", "pinky") for k in (1, 2, 3)}
    K.dummy_part("Glove" + side, hand_bones, M["glove"], skin="keep", inflate=0.002)


# =========================================================
# PIERNAS
# =========================================================

def build_leg(side):
    chain = K.leg_chain(side)
    sgn = 1 if side == ".R" else -1
    lat = V(sgn, 0, 0)     # hacia afuera
    tube_piece("Legging" + side, chain, -0.06, 0.80, 16, lambda d, a: LegRadius(d), M["under"], "leg:" + side,
               lat, cols=14, mods=[], trims=False)
    tube_piece("Cuisse" + side, chain, 0.12, 0.36, 4,
               lambda d, a: LegRadius(d) + 0.012 + 0.004 * bell(a, 0, 30), M["plate"], "legstiff:" + side, lat,
               cols=20, span=125)
    tube_piece("Poleyn" + side, chain, 0.355, 0.47, 5,
               lambda d, a: LegRadius(d) + 0.014 + 0.016 * bell(d, 0.41, 0.03) * bell(a, 0, 55), M["plate"],
               "leg:" + side, lat, cols=20, span=120)
    tube_piece("Greave" + side, chain, 0.465, 0.79, 5,
               lambda d, a: LegRadius(d) + 0.013 + 0.005 * bell(a, 0, 9), M["plate"], "legstiff:" + side, lat, cols=22)

    # El ala de la rodillera, del lado de afuera
    c, t = along(chain, 0.41)
    center = c + lat * (LegRadius(0.41) + 0.02)
    fan = Mesh()
    pts = []
    for i in range(24):
        a = 2 * math.pi * i / 24
        r = 0.03 * (1 + 0.25 * math.cos(a))          # corazón: más ancho arriba
        pts.append(center + V(0, 1, 0) * (math.cos(a) * r * 1.1) + V(0, 0, 1) * (math.sin(a) * r * 0.9))
    ids = [fan.v(p) for p in pts]
    ci = fan.v(center + lat * 0.006)
    for i in range(24):
        fan.face([ci, ids[i], ids[(i + 1) % 24]])
    obj = fan.build("Poleyn_Wing" + side, [M["plate"]], "leg:" + side, sharp=50, orient=c,
                    mods=[K.solidify(0.003, offset=-1.0)])
    K.trim(obj, M["gold"], radius=0.0032, flat=0.8, name="Poleyn_Wing_Trim" + side)

    # El pie: la bota del Dummy, un poco más grande, de placas
    K.dummy_part("Sabaton" + side, {"B-foot" + side, "B-toe" + side}, M["plate"], skin="leg:" + side, inflate=0.008)


def build_tassets():
    # Las faldas de láminas, de la cintura para abajo
    for k in range(4):
        top = 1.0 - k * 0.042
        rows = [top, top - 0.03, top - 0.058]
        cols = 36
        off = 0.024 + k * 0.006
        rings = [[skirt_point(y, i / cols * 360 - 180, off, 0.3) for i in range(cols)] for y in rows]
        m = Mesh()
        m.loft(rings, wrap=True)
        obj = m.build(f"Fauld{k}", [M["plate"]], "torso", sharp=50, orient=torso_axis,
                      mods=[K.solidify(0.003, offset=-1.0), K.bevel(0.0008, 1)])
        K.trim(obj, M["gold"], radius=0.0032, flat=0.8, name=f"Fauld{k}_Trim",
               select=lambda pts, top=top: sum(p.y for p in pts) / len(pts) < top - 0.03)

    # Las escarcelas: una placa sobre cada muslo
    for side in (".L", ".R"):
        sgn = 1 if side == ".R" else -1
        rings = []
        for y in (0.86, 0.82, 0.78, 0.74, 0.705):
            ring = []
            for i in range(13):
                deg = sgn * lerp(14, 78, i / 12)
                ring.append(skirt_point(y, deg, 0.05, 0.3) + V(0, 0, 0))
            rings.append(ring)
        m = Mesh()
        m.loft(rings)
        obj = m.build("Tasset" + side, [M["plate"]], "tasset:" + side, sharp=50, orient=torso_axis,
                      mods=[K.solidify(0.003, offset=-1.0), K.bevel(0.0008, 1)])
        K.trim(obj, M["gold"], radius=0.0034, flat=0.8, name="Tasset_Trim" + side)


# =========================================================
# TELA: TABARDO Y CAPA
# =========================================================

def build_tabard():
    for name, base_deg in (("Tabard_Front", 0.0), ("Tabard_Back", 180.0)):
        cols, rows = 16, 14
        rings = []
        for r in range(rows + 1):
            t = r / rows
            ring = []
            for i in range(cols + 1):
                s = i / cols * 2 - 1
                deg = base_deg + s * lerp(33, 27, t)
                hem = 0.50 + 0.06 * abs(s) - 0.02 * math.cos(s * math.pi * 2)
                y = lerp(1.022, hem, t)
                off = lerp(0.03, 0.062, smooth(0, 0.3, t)) + 0.006 * math.sin(s * 7 + base_deg) * t
                ring.append(skirt_point(y, deg, off, 0.3))
            rings.append(ring)
        m = Mesh()
        m.loft(rings)
        obj = m.build(name, [M["crimson"], M["lining"]], "tabard", sharp=50, orient=torso_axis,
                      mods=[K.solidify(0.0035, offset=-1.0, shell_mat=1, rim_mat=1)])
        K.trim(obj, M["gold"], radius=0.003, flat=0.6, name=name + "_Trim",
               select=lambda pts: True)

    # Flor de lis grande en el frente del tabardo
    p = skirt_point(0.80, 0, lerp(0.03, 0.062, 1.0) + 0.003, 0.3)
    K.glyph("⚜", 0.13, 0.003, p, V(0, 0.18, 1), V(0, 1, 0), M["gold"], "tabard", name="Tabard_Lis")


def cape_z(y, s):
    t = (1.49 - y) / (1.49 - 0.52)
    fold = (math.sin(s * 9.5 + 0.9) * 0.034 * smooth(0.08, 0.6, t)
            + math.sin(s * 19 + 2.1) * 0.011 * smooth(0.3, 0.9, t))
    return -0.13 - 0.15 * smooth(0, 1, t) + s * s * lerp(0.08, 0.05, t) + fold


def build_cape():
    cols, rows = 36, 24
    top, bottom = 1.49, 0.52
    hem = [rng.uniform(-0.03, 0.025) for _ in range(cols + 1)]
    rings = []
    for r in range(rows + 1):
        t = r / rows
        ring = []
        for i in range(cols + 1):
            s = i / cols * 2 - 1
            y = lerp(top, bottom, t) - 0.03 * s * s * (1 - t) + (hem[i] * t ** 4)
            width = lerp(0.17, 0.31, smooth(0, 0.7, t))
            ring.append(V(s * width, y, cape_z(y, s)))
        rings.append(ring)
    m = Mesh()
    m.loft(rings)
    obj = m.build("Cape", [M["crimson"], M["lining"]], "cape", sharp=60,
                  orient=lambda p: V(p.x * 0.3, p.y, 0.0),
                  mods=[K.solidify(0.004, offset=1.0, shell_mat=1, rim_mat=1)])
    K.trim(obj, M["gold"], radius=0.0032, flat=0.6, name="Cape_Trim",
           select=lambda pts: True, push=lambda p: V(0, 0, -0.002))

    # Los broches de la capa, sobre los hombros
    for sgn in (-1, 1):
        c = V(sgn * 0.15, 1.475, -0.115)
        b = Mesh()
        ring = [c + V(math.cos(a) * 0.022 * 0.0, math.sin(a) * 0.022, math.cos(a) * 0.022)
                for a in np.linspace(0, 2 * math.pi, 18, endpoint=False)]
        b.tube(ring, (0.0045, 0.004), sides=6, closed=True)
        b.build(f"Cape_Clasp{'R' if sgn > 0 else 'L'}", [M["gold"]], "rigid:B-chest", sharp=50)


# =========================================================
# FANATISMO: ROSARIO Y SELLOS
# =========================================================

def sphere(m, c, r, rings=5, sides=8, mat=0):
    pts = []
    for k in range(1, rings):
        a = math.pi * k / rings
        pts.append([c + V(math.sin(a) * math.cos(2 * math.pi * s / sides) * r, math.cos(a) * r,
                          math.sin(a) * math.sin(2 * math.pi * s / sides) * r) for s in range(sides)])
    ids = [[m.v(p) for p in ring] for ring in pts]
    top, bot = m.v(c + V(0, r, 0)), m.v(c + V(0, -r, 0))
    for s in range(sides):
        j = (s + 1) % sides
        m.face([top, ids[0][j], ids[0][s]], mat)
        m.face([bot, ids[-1][s], ids[-1][j]], mat)
    for k in range(len(ids) - 1):
        for s in range(sides):
            j = (s + 1) % sides
            m.face([ids[k][s], ids[k][j], ids[k + 1][j], ids[k + 1][s]], mat)


def build_rosary():
    # Cuelga del cinturón, del lado derecho: una U de cuentas con un sol al final
    a = torso_point(1.0, 28, 0.034)
    b = torso_point(1.0, 70, 0.034)
    pts = []
    n = 26
    for i in range(n):
        t = i / (n - 1)
        deg = lerp(28, 70, t)
        y = 1.0 - 0.17 * math.sin(math.pi * t) ** 0.8
        pts.append(skirt_point(y, deg, 0.052, 0.3))
    m = Mesh()
    for i, p in enumerate(pts):
        if 0 < i < n - 1:
            sphere(m, p, 0.0068 if i % 6 else 0.009, rings=4, sides=6)
    m.build("Rosary", [M["wood"]], "tasset:.R", sharp=80)
    # El cordón y el sol
    c = Mesh()
    c.tube(pts, 0.0013, sides=5, cap_start=False, cap_end=False)
    low = pts[n // 2]
    sun = low + V(0, -0.05, 0.01)
    c.tube([low, sun + V(0, 0.022, 0)], 0.0013, sides=5)
    c.build("Rosary_Cord", [M["leather"]], "tasset:.R", sharp=80)
    s = Mesh()
    nrm = (sun - V(0, sun.y, 0)).normalized()
    ring = []
    for i in range(16):
        ang = 2 * math.pi * i / 16
        r = 0.022 if i % 2 == 0 else 0.012
        ring.append(sun + V(0, math.cos(ang) * r, 0) + nrm.cross(V(0, 1, 0)).normalized() * (math.sin(ang) * r))
    back = [p - nrm * 0.004 for p in ring]
    s.loft([back, ring], wrap=True, cap_start=True, cap_end=True)
    s.build("Rosary_Sun", [M["gold"]], "tasset:.R", sharp=50)


def build_seal(name, center, normal, down, skin, strips=3):
    """Un sello de cera roja con tiras de pergamino que cuelgan."""
    normal = normal.normalized()
    side = normal.cross(down).normalized()
    wax = Mesh()
    ring = [center + (side * math.cos(a) + down * math.sin(a)) * 0.02 * (1 + 0.08 * math.sin(a * 5))
            for a in np.linspace(0, 2 * math.pi, 20, endpoint=False)]
    top = [p + normal * 0.006 for p in ring]
    inner = [center + normal * 0.0075 + (p - center) * 0.6 for p in ring]
    wax.loft([ring, top, inner], wrap=True, cap_end=True)
    wax.build(name, [M["wax"]], skin, sharp=55)

    paper = Mesh()
    for k in range(strips):
        x0 = (k - (strips - 1) / 2) * 0.011
        length = 0.07 + 0.025 * ((k * 7) % 3) / 2
        path = []
        for i in range(9):
            t = i / 8
            q = center + side * (x0 + 0.004 * math.sin(t * 3 + k)) + down * (0.012 + t * length) \
                - normal * 0.0015 + normal * (0.006 * math.sin(t * math.pi) * (1 if k % 2 else -0.4))
            path.append(q)
        w = 0.0048
        rings = []
        for i, q in enumerate(path):
            tw = 1.0 - 0.15 * i / 8
            rings.append([q - side * w * tw, q + side * w * tw])
        ids = [[paper.v(p) for p in r] for r in rings]
        for i in range(len(ids) - 1):
            paper.face([ids[i][0], ids[i][1], ids[i + 1][1], ids[i + 1][0]])
    paper.build(name + "_Paper", [M["parchment"]], skin, sharp=70, orient=lambda p: center - normal * 0.05,
                mods=[K.solidify(0.0008, offset=0.0)])


def build_seals():
    # En el pecho, del lado izquierdo
    p = torso_point(1.30, -40, 0.016 + bust(1.30, -40) + 0.002)
    n = (p - torso_axis(p)).normalized()
    build_seal("Seal_Chest", p, n, V(0, -1, 0), "rigid:B-chest")
    # En la hombrera derecha
    origin, axis, up, front = pauldron_frame(".R")
    c = origin + axis * 0.07 + up * 0.125 + front * 0.02
    build_seal("Seal_Pauldron", c, (up * 0.5 + front * 0.85).normalized(), (-up * 0.85 + axis * 0.5).normalized(),
               "pauldron:.R", strips=2)
    # En el muslo izquierdo
    chain = K.leg_chain(".L")
    c, t = along(chain, 0.2)
    q = c + V(0, 0, 1) * (LegRadius(0.2) + 0.019) + V(-1, 0, 0) * 0.02
    build_seal("Seal_Thigh", q, V(-0.3, 0, 1), V(0, -1, 0), "legstiff:.L", strips=2)


# =========================================================
# AUREOLA
# =========================================================

def build_halo():
    radius = 0.13
    m = Mesh()
    ring = [HaloCenter + V(math.sin(a), math.cos(a), 0) * radius for a in np.linspace(0, 2 * math.pi, 48, endpoint=False)]
    m.tube(ring, (0.0085, 0.0045), sides=6, closed=True, up=lambda p, t: (p - HaloCenter).normalized())
    inner = [HaloCenter + V(math.sin(a), math.cos(a), 0) * (radius - 0.022) for a in np.linspace(0, 2 * math.pi, 40, endpoint=False)]
    m.tube(inner, (0.0025, 0.0025), sides=5, closed=True)
    for i in range(16):
        a = 2 * math.pi * i / 16
        d = V(math.sin(a), math.cos(a), 0)
        big = i % 2 == 0
        L = 0.075 if big else 0.042
        m.gem(HaloCenter + d * (radius + 0.008), d, d.cross(V(0, 0, 1)), L, 0.012 if big else 0.009, 0.004)
    m.build("Halo", [M["halo"]], "blend:B-head=0.6,B-neck=0.4", sharp=40)


# =========================================================
# PELO
# =========================================================

def head_guard(p, margin):
    """Saca p de adentro de la cabeza (con un margen)."""
    d = p - HeadCenter
    h = (p.y - HeadBottom) / HeadHeight
    if h < -0.15 or h > 1.15:
        return p
    hh = min(max(h, 0.0), 1.0)
    deg = math.degrees(math.atan2(d.x, p.z - 0.02 - HeadOffset(hh)))
    surf = head_point(hh, deg, 0.0, features=False)
    rs = math.hypot(surf.x, surf.z - 0.02 - HeadOffset(hh))
    r = math.hypot(d.x, p.z - 0.02 - HeadOffset(hh))
    need = rs + margin
    if r < need and r > 1e-6:
        k = need / r
        return V(d.x * k, p.y, 0.02 + HeadOffset(hh) + (p.z - 0.02 - HeadOffset(hh)) * k)
    return p


def body_guard(p, margin):
    """Saca p de adentro del torso con la armadura, la gola y la capa de atrás."""
    if p.y < 0.9 or p.y > 1.62:
        return p
    ax = torso_axis(p)
    d = p - ax
    deg = math.degrees(math.atan2(d.x, d.z))
    surf = torso_point(p.y, deg, 0.03 + (bust(p.y, deg) if p.y < 1.47 else 0))
    rs = (surf - ax).length
    if p.y > 1.45:      # la gola
        rs = max(rs * smooth(1.6, 1.45, p.y), 0.12)
    r = d.length
    if r < rs + margin and r > 1e-6:
        p = ax + d * ((rs + margin) / r)
    # La capa: el pelo de atrás va por encima
    if p.z < 0 and p.y < 1.5 and abs(p.x) < 0.3:
        width = lerp(0.17, 0.31, smooth(0, 0.7, (1.49 - p.y) / 0.97))
        s = max(-1.0, min(1.0, p.x / width))
        cz = cape_z(p.y, s) - 0.008
        if p.z > cz - margin and p.z < 0.02 and abs(p.x) < width:
            p = V(p.x, p.y, cz - margin)
    return p


def catmull(points, samples):
    pts = [points[0]] + points + [points[-1]]
    out = []
    segs = len(points) - 1
    for k in range(samples):
        u = k / (samples - 1) * segs
        i = min(int(u), segs - 1)
        t = u - i
        p0, p1, p2, p3 = pts[i], pts[i + 1], pts[i + 2], pts[i + 3]
        t2, t3 = t * t, t * t * t
        out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    return out


def lock(m, ctrl, width, thick, samples=18, guard_head=0.008, guard_body=0.012, taper=1.0, mat=0,
         wave=0.0, phase=0.0):
    path = catmull(ctrl, samples)
    if wave:
        # Ondas desde la mandíbula para abajo
        waved = []
        for p in path:
            o = p - torso_axis(p)
            o.y = 0
            o = o.normalized() if o.length > 1e-6 else V(0, 0, -1)
            side = V(o.z, 0.0, -o.x)
            k = smooth(1.62, 1.52, p.y)
            a = (1.62 - p.y) * 38 + phase
            waved.append(p + side * (wave * k * math.sin(a)) + o * (0.4 * wave * k * math.cos(a)))
        path = waved

    def guard(pts):
        return [body_guard(head_guard(p, guard_head + thick * 0.5), guard_body + thick * 0.5) for p in pts]

    # Empujar afuera de la cabeza y el cuerpo deja quiebres: se suaviza y se vuelve a empujar
    fixed = guard(path)
    for _ in range(4):
        fixed = [fixed[0]] + [(fixed[i - 1] + fixed[i] * 2 + fixed[i + 1]) / 4 for i in range(1, len(fixed) - 1)] + [fixed[-1]]
        fixed = guard(fixed)

    def radius(t):
        w = width * (1 - 0.88 * t ** (1.8 / taper)) * (0.75 + 0.25 * smooth(0, 0.12, t))
        return (thick * (1 - 0.6 * t), w)

    def up(p, t):
        # Lo plano del mechón mira hacia afuera: de la cabeza arriba, del cuerpo abajo, mezclado sin saltos
        head = (p - HeadCenter).normalized()
        body = p - torso_axis(p)
        body.y = 0
        body = body.normalized() if body.length > 1e-6 else V(0, 0, -1)
        o = body.lerp(head, smooth(1.5, 1.64, p.y))
        return o.normalized() if o.length > 1e-6 else V(0, 0, -1)

    m.tube(fixed, radius, sides=6, up=up, cap_start=True, cap_end=True, mat=mat)


def build_hair():
    # El casco: sobre el cráneo, con surcos de mechones y la raya al medio
    m = Mesh()
    cols, rows = 48, 12
    rings = []
    for r in range(rows + 1):
        v = r / rows
        ring = []
        for i in range(cols):
            deg = i / cols * 360 - 180
            h0 = Hairline(abs(deg)) - 0.02
            h = lerp(h0, 1.0, v ** 0.9)
            groove = 0.0026 * math.sin(math.radians(deg) * 22) * smooth(0.0, 0.3, v)
            part = -0.004 * bell(deg, 0, 5) * smooth(0.5, 0.9, v)
            swell = 0.009 + 0.008 * smooth(0.2, 1.0, v) + groove + part + 0.006 * smooth(90, 180, abs(deg))
            ring.append(head_point(min(h, 1.0), deg, swell, features=False))
        rings.append(ring)
    m.loft(rings, wrap=True, cap_end=True)
    m.build("Hair_Cap", [M["hair"]], "rigid:B-head", sharp=70)

    h = Mesh()
    # Flequillo: cortina abierta al medio, cae a los costados de la frente
    for sgn in (-1, 1):
        for k in range(4):
            d0 = sgn * (4 + k * 7)
            d1 = sgn * (34 + k * 14)
            root = head_point(0.95 - k * 0.015, d0, 0.012, False)
            mid = head_point(0.80, lerp(d0, d1, 0.45), 0.026, False)
            low = head_point(0.68 - k * 0.025, lerp(d0, d1, 0.85), 0.02, False)
            tip = head_point(0.58 - k * 0.04, d1 + sgn * 6, 0.013, False)
            lock(h, [root, mid, low, tip], 0.034 - k * 0.002, 0.008, samples=14, guard_head=0.006, taper=1.3)

    # Mechones de los costados: enmarcan la cara y caen sobre los hombros, por delante
    for sgn in (-1, 1):
        for k in range(3):
            d0 = sgn * (66 + k * 13)
            root = head_point(0.76 - k * 0.04, d0, 0.012, False)
            p1 = head_point(0.45, sgn * (86 + k * 9), 0.026, False)
            p2 = head_point(0.08, sgn * (96 + k * 10), 0.034, False)
            p3 = V(sgn * (0.112 + k * 0.012), 1.50 - k * 0.015, 0.02 - k * 0.03)
            p4 = V(sgn * (0.12 + k * 0.014), 1.43 - k * 0.03, 0.035 - k * 0.04)
            lock(h, [root, p1, p2, p3, p4], 0.034, 0.011, samples=20, taper=0.9)

    # La espalda: dos capas de mechones largos que caen sobre la capa
    for layer in range(2):
        n = 9 if layer == 0 else 8
        for i in range(n):
            u = (i + 0.5 * layer) / (n - 1 + 0.5 * layer) * 2 - 1
            deg = 180 - u * (78 if layer == 0 else 70)
            deg = wrap_deg(deg)
            root = head_point(lerp(0.88, 0.7, abs(u)) - 0.06 * layer, deg, 0.012 + 0.004 * layer, False)
            over = head_point(0.45, deg, 0.03 + 0.006 * layer, False)
            nape = head_point(0.12, deg, 0.04 + 0.006 * layer, False)
            x_end = u * (0.15 if layer == 0 else 0.12) + rng.uniform(-0.01, 0.01)
            y_end = (1.12 if layer == 0 else 1.2) - 0.07 * (1 - abs(u)) + rng.uniform(-0.03, 0.03)
            ctrl = [root, over, nape]
            for y in (1.5, 1.4, 1.3, 1.2):
                if y <= y_end + 0.03:
                    break
                t = (1.55 - y) / (1.55 - y_end)
                x = lerp(nape.x, x_end, smooth(0, 1, t)) + 0.012 * math.sin(y * 30 + i)
                ctrl.append(V(x, y, -0.2))
            ctrl.append(V(x_end, y_end, -0.2))
            lock(h, ctrl, 0.05 if layer == 0 else 0.046, 0.016, samples=17, guard_body=0.012 + 0.012 * layer, taper=0.8)

    h.build("Hair_Locks", [M["hair"]], "hair", sharp=70)


# =========================================================
# TODO
# =========================================================

def build():
    K.begin()
    materials()
    build_torso()
    build_head()
    build_cuirass()
    build_brigandine()
    build_belt()
    build_gorget()
    for side in (".L", ".R"):
        build_pauldron(side)
        build_arm(side)
        build_leg(side)
    build_tassets()
    build_tabard()
    build_cape()
    build_rosary()
    build_seals()
    if WITH_HALO:
        build_halo()
    build_hair()

    path = os.path.join(K.HERE, NAME + ".blend")
    bpy.context.scene["merc_name"] = NAME
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print(f"[Paladin] guardado {path}")


if __name__ == "__main__":
    build()
    import merc_export
    preview_only = "--preview" in sys.argv
    merc_export.run(NAME, preview_only=preview_only)
