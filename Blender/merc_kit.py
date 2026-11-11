# -*- coding: utf-8 -*-
# ============================================================
# merc_kit.py
#
# La caja de herramientas para armar en Blender los modelos de las clases (10 de noviembre de 2026,
# empezando por la Paladina). Reemplaza a las recetas de Scripts/Editor/ClassModels, que armaban la
# malla a mano dentro de Unity: acá hay modificadores (grosor, biseles, subdivisión), sombras horneadas
# y un render para ver el resultado.
#
# Cómo llega al juego (ver LEEME.md):
#   1. paladin.py arma el modelo encima del esqueleto del Dummy y guarda Paladin.blend.
#   2. merc_export.py hornea las texturas y escribe Out/<Clase>/: la malla (.json), el atlas y el brillo.
#   3. En Unity, Mercenarios ▸ Modelos de clase ▸ Importar los modelos de Blender arma la malla con los
#      huesos del jugador (los mismos que ClassModelSwap) y pisa el prefab Model_<Clase>.
#
# No pasa por FBX a propósito: el importador de FBX de Blender cambia la orientación de los huesos y
# convierte B-hips en el objeto del esqueleto. Unity usa SUS huesos y solo necesita la malla en la pose T,
# los pesos por NOMBRE de hueso y las texturas.
#
# Medidas en metros, en el espacio de Unity (como las recetas viejas, así se reusan sus tablas):
# +X es la derecha del personaje, +Y arriba, +Z su frente. B() pasa de ahí a Blender.
#
# Pesos: cada pieza dice a qué huesos sigue con su propiedad "merc_skin" (ver SKINS). Se calculan al
# exportar sobre la malla final, así que se puede editar la forma en Blender sin repintar pesos.
# ============================================================
import bpy
import bmesh
import math
import os
import random
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
OUT = os.path.join(HERE, "Out")

MODEL_COLLECTION = "Model"
DUMMY_COLLECTION = "Dummy"


# =========================================================
# ESPACIO: Unity <-> Blender
# =========================================================

def B(x, y=None, z=None):
    """De Unity (x derecha, y arriba, z frente) a Blender (z arriba, el frente es -y)."""
    if y is None:
        x, y, z = x
    return Vector((-x, -z, y))


def U(v):
    """De Blender a Unity."""
    return Vector((-v[0], v[2], -v[1]))


def V(x, y, z):
    return Vector((x, y, z))


# =========================================================
# CUENTAS
# =========================================================

def clamp01(x):
    return 0.0 if x < 0.0 else 1.0 if x > 1.0 else x


def lerp(a, b, t):
    return a + (b - a) * t


def smooth(a, b, x):
    t = clamp01((x - a) / (b - a))
    return t * t * (3.0 - 2.0 * t)


def bell(x, c, w):
    d = (x - c) / w
    return math.exp(-d * d)


def wrap_deg(d):
    while d > 180.0:
        d -= 360.0
    while d < -180.0:
        d += 360.0
    return d


def srgb(c):
    """Un color pensado en sRGB (como los de las recetas) a lineal, que es lo que pide Blender."""
    def one(x):
        return x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4
    return (one(c[0]), one(c[1]), one(c[2]))


class Keys:
    """Una curva por puntos (x, y, x, y…), suave entre ellos: las tablas de medidas de las recetas."""

    def __init__(self, *pairs):
        self.x = list(pairs[0::2])
        self.y = list(pairs[1::2])

    def _slope(self, i):
        a = max(i - 1, 0)
        b = min(i + 1, len(self.x) - 1)
        return (self.y[b] - self.y[a]) / (self.x[b] - self.x[a])

    def __call__(self, x):
        xs, ys = self.x, self.y
        if x <= xs[0]:
            return ys[0]
        if x >= xs[-1]:
            return ys[-1]
        i = 0
        while x > xs[i + 1]:
            i += 1
        h = xs[i + 1] - xs[i]
        t = (x - xs[i]) / h
        m0 = self._slope(i) * h
        m1 = self._slope(i + 1) * h
        t2, t3 = t * t, t * t * t
        return ((2 * t3 - 3 * t2 + 1) * ys[i] + (t3 - 2 * t2 + t) * m0
                + (-2 * t3 + 3 * t2) * ys[i + 1] + (t3 - t2) * m1)


def bezier(a, b, c, d, t):
    m = 1.0 - t
    return a * (m * m * m) + b * (3 * m * m * t) + c * (3 * m * t * t) + d * (t * t * t)


# =========================================================
# ESCENA Y ESQUELETO
# =========================================================

def find_dummy_fbx():
    """El FBX del Dummy: se busca por nombre, no por ruta fija (Gustavo mueve las carpetas)."""
    for root, _dirs, files in os.walk(os.path.join(PROJECT, "Assets")):
        if "HumanCharacterDummy_M.fbx" in files:
            return os.path.join(root, "HumanCharacterDummy_M.fbx")
    raise RuntimeError("No encontré HumanCharacterDummy_M.fbx en Assets.")


class Skeleton:
    """Los huesos del jugador en la pose T, en el espacio de Unity, y el cuerpo del Dummy (para las manos)."""

    def __init__(self):
        self.head = {}
        self.tail = {}
        self.parent = {}
        self.body = None

    @staticmethod
    def load():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        sk = Skeleton()

        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=find_dummy_fbx())
        imported = [o for o in bpy.data.objects if o not in before]

        dummy = bpy.data.collections.new(DUMMY_COLLECTION)
        bpy.context.scene.collection.children.link(dummy)
        for o in imported:
            for c in list(o.users_collection):
                c.objects.unlink(o)
            dummy.objects.link(o)

        rig = next(o for o in imported if o.type == 'ARMATURE')
        for b in rig.data.bones:
            sk.head[b.name] = U(rig.matrix_world @ b.head_local)
            sk.tail[b.name] = U(rig.matrix_world @ b.tail_local)
            sk.parent[b.name] = b.parent.name if b.parent else "B-hips"

        # Blender convierte B-hips en el objeto del esqueleto: se lo devuelve como hueso
        hips = U(rig.matrix_world.translation)
        sk.head["B-hips"] = hips
        sk.tail["B-hips"] = sk.head["B-spine"]
        sk.parent["B-hips"] = None

        sk.body = next(o for o in imported if o.type == 'MESH')
        dummy.hide_render = True
        bpy.context.view_layer.layer_collection.children[DUMMY_COLLECTION].exclude = False
        dummy.hide_viewport = True

        model = bpy.data.collections.new(MODEL_COLLECTION)
        bpy.context.scene.collection.children.link(model)
        return sk

    def pos(self, name):
        return self.head[name].copy()

    def end(self, name):
        return self.tail[name].copy()


SK = None   # el esqueleto cargado; lo pone begin()


def begin():
    global SK
    SK = Skeleton.load()
    return SK


def from_blend():
    """Al exportar un .blend ya armado: el esqueleto sale del Dummy guardado ahí."""
    global SK
    sk = Skeleton()
    rig = next(o for o in bpy.data.collections[DUMMY_COLLECTION].all_objects if o.type == 'ARMATURE')
    for b in rig.data.bones:
        sk.head[b.name] = U(rig.matrix_world @ b.head_local)
        sk.tail[b.name] = U(rig.matrix_world @ b.tail_local)
        sk.parent[b.name] = b.parent.name if b.parent else "B-hips"
    sk.head["B-hips"] = U(rig.matrix_world.translation)
    sk.tail["B-hips"] = sk.head["B-spine"]
    sk.parent["B-hips"] = None
    sk.body = next(o for o in bpy.data.collections[DUMMY_COLLECTION].all_objects if o.type == 'MESH')
    SK = sk
    return sk


# =========================================================
# PESOS
# =========================================================
# Un peso es un dict {hueso: peso}. Las piezas dicen cuál regla usan en "merc_skin":
#   "rigid:B-head"                     todo a un hueso
#   "blend:B-upperArm.L=0.6,B-shoulder.L=0.4"
#   "torso", "arm:.L", "leg:.R", "hair", "cape", "tabard", "gorget"
#   "keep"                             los pesos que ya trae (las manos del Dummy)

def W(bone):
    return {bone: 1.0}


def mix(a, b, t):
    t = clamp01(t)
    if t <= 0.0:
        return dict(a)
    if t >= 1.0:
        return dict(b)
    out = {}
    for k, v in a.items():
        out[k] = out.get(k, 0.0) + v * (1.0 - t)
    for k, v in b.items():
        out[k] = out.get(k, 0.0) + v * t
    return out


def top4(w):
    items = sorted(((k, v) for k, v in w.items() if v > 1e-4), key=lambda kv: -kv[1])[:4]
    total = sum(v for _, v in items) or 1.0
    return [(k, v / total) for k, v in items]


def chain_param(p, pts):
    """Qué tan lejos por un camino de puntos queda la proyección de p (puede ser negativo antes del
    primero o pasarse del último). Devuelve (s, largos acumulados de cada punto)."""
    acc = [0.0]
    for i in range(len(pts) - 1):
        acc.append(acc[-1] + (pts[i + 1] - pts[i]).length)
    best_d, best_s = None, 0.0
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        ab = b - a
        L2 = ab.length_squared
        t = (p - a).dot(ab) / L2
        lo = -1e9 if i == 0 else 0.0
        hi = 1e9 if i == len(pts) - 2 else 1.0
        t = max(lo, min(hi, t))
        d = (p - (a + ab * t)).length
        if best_d is None or d < best_d:
            best_d, best_s = d, acc[i] + t * math.sqrt(L2)
    return best_s, acc


def skin_torso(p):
    w = mix(W("B-hips"), W("B-spine"), smooth(1.0, 1.1, p.y))
    w = mix(w, W("B-chest"), smooth(1.19, 1.3, p.y))
    w = mix(w, W("B-neck"), smooth(1.5, 1.54, p.y))
    w = mix(w, W("B-head"), smooth(1.58, 1.6, p.y))
    side = ".R" if p.x > 0 else ".L"
    shoulder = smooth(0.12, 0.19, abs(p.x)) * smooth(1.36, 1.42, p.y)
    w = mix(w, W("B-shoulder" + side), shoulder * 0.5)
    w = mix(w, W("B-upperArm" + side), shoulder * 0.3)
    thigh = smooth(0.95, 0.86, p.y) * smooth(0.02, 0.09, abs(p.x))
    return mix(w, W("B-thigh" + side), thigh * 0.6)


def arm_chain(side):
    return [SK.pos("B-upperArm" + side), SK.pos("B-forearm" + side), SK.pos("B-hand" + side), SK.end("B-hand" + side)]


def leg_chain(side):
    return [SK.pos("B-thigh" + side), SK.pos("B-shin" + side), SK.pos("B-foot" + side),
            SK.pos("B-toe" + side), SK.end("B-toe" + side)]


def skin_arm(p, side, soft=1.0):
    s, acc = chain_param(p, arm_chain(side))
    elbow, wrist = acc[1], acc[2]
    w = mix(W("B-shoulder" + side), W("B-upperArm" + side), smooth(-0.04, 0.03, s))
    w = mix(w, W("B-forearm" + side), smooth(elbow - 0.03 * soft, elbow + 0.025 * soft, s))
    return mix(w, W("B-hand" + side), smooth(wrist - 0.015 * soft, wrist + 0.01 * soft, s))


def skin_leg(p, side, soft=1.0):
    s, acc = chain_param(p, leg_chain(side))
    knee, ankle, toes = acc[1], acc[2], acc[3]
    w = mix(W("B-hips"), W("B-thigh" + side), smooth(-0.07, 0.03, s))
    w = mix(w, W("B-shin" + side), smooth(knee - 0.04 * soft, knee + 0.035 * soft, s))
    w = mix(w, W("B-foot" + side), smooth(ankle - 0.03 * soft, ankle + 0.02 * soft, s))
    return mix(w, W("B-toe" + side), smooth(toes - 0.02, toes + 0.02, s))


def skin_hair(p):
    # Arriba sigue a la cabeza; lo que cae sobre la espalda, al cuello y al pecho.
    w = mix(W("B-head"), W("B-neck"), smooth(1.6, 1.5, p.y) * 0.5)
    return mix(w, W("B-chest"), smooth(1.56, 1.36, p.y) * 0.85)


def skin_cape(p):
    w = mix(W("B-chest"), W("B-spine"), smooth(1.38, 1.1, p.y) * 0.6)
    return mix(w, W("B-hips"), smooth(1.15, 0.75, p.y) * 0.7)


def skin_tabard(p):
    # El centro sigue a las dos piernas por igual: con las piernas abiertas cuelga derecho.
    reach = min(1.0, max(0.0, (1.03 - p.y) / 0.5) ** 0.8 * 1.1) * 0.75 * smooth(1.03, 0.84, p.y)
    right = smooth(-0.17, 0.17, p.x)
    w = W("B-hips")
    w = mix(w, W("B-thigh.R"), reach * right)
    return mix(w, W("B-thigh.L"), reach * (1 - right) / max(0.001, 1 - reach * right))


def skin_gorget(p):
    return mix(W("B-chest"), W("B-neck"), 0.45 * smooth(1.47, 1.6, p.y))


def skin_pauldron(p, side):
    # Mitad hombro y mitad brazo: la hombrera casi no se dobla, pero sigue al brazo cuando sube
    joint = SK.pos("B-upperArm" + side)
    axis = (SK.end("B-upperArm" + side) - joint).normalized()
    a = (p - joint).dot(axis)
    return mix(W("B-shoulder" + side), W("B-upperArm" + side), 0.35 + 0.45 * smooth(-0.08, 0.15, a))


def skin_tasset(p, side):
    # Las escarcelas cuelgan de la cadera y bajan con el muslo
    return mix(W("B-hips"), W("B-thigh" + side), smooth(0.92, 0.72, p.y) * 0.6)


def parse_weights(spec):
    out = {}
    for part in spec.split(","):
        bone, value = part.split("=")
        out[bone.strip()] = float(value)
    return out


def skin_for(spec, p):
    kind, _, arg = spec.partition(":")
    if kind == "rigid":
        return W(arg)
    if kind == "blend":
        return parse_weights(arg)
    if kind == "torso":
        return skin_torso(p)
    if kind == "arm":
        return skin_arm(p, arg)
    if kind == "armstiff":
        return skin_arm(p, arg, 0.4)
    if kind == "leg":
        return skin_leg(p, arg)
    if kind == "legstiff":
        return skin_leg(p, arg, 0.4)
    if kind == "hair":
        return skin_hair(p)
    if kind == "cape":
        return skin_cape(p)
    if kind == "tabard":
        return skin_tabard(p)
    if kind == "gorget":
        return skin_gorget(p)
    if kind == "tasset":
        return skin_tasset(p, arg)
    if kind == "pauldron":
        return skin_pauldron(p, arg)
    raise ValueError("merc_skin desconocido: " + spec)


# =========================================================
# MATERIALES
# =========================================================
# El color base, la aspereza y lo que brilla se hornean a las texturas del juego. El alfa del atlas es
# el pulido (1 - aspereza), como en el acabado Polished de ClassModelKit.

def material(name, color, rough=0.6, metal=0.0, emit=None, emit_strength=0.0):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*srgb(color), 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if emit is not None:
        bsdf.inputs["Emission Color"].default_value = (*srgb(emit), 1.0)
        bsdf.inputs["Emission Strength"].default_value = emit_strength
    m["merc_metal"] = metal
    return m


def node(nt, kind, loc=(0, 0), **inputs):
    n = nt.nodes.new(kind)
    n.location = loc
    for k, v in inputs.items():
        n.inputs[k].default_value = v
    return n


# =========================================================
# MALLAS
# =========================================================

class Mesh:
    """Junta vértices y caras en el espacio de Unity y arma un objeto de Blender."""

    def __init__(self):
        self.verts = []
        self.faces = []
        self.mats = []

    def v(self, p):
        self.verts.append(Vector(p))
        return len(self.verts) - 1

    def face(self, idx, mat=0):
        if len(set(idx)) >= 3:
            self.faces.append(list(idx))
            self.mats.append(mat)

    def loft(self, rings, wrap=False, cap_start=False, cap_end=False, mat=0, cap_mat=None):
        """Une anillos del mismo largo. wrap cierra cada anillo."""
        ids = [[self.v(p) for p in ring] for ring in rings]
        n = len(rings[0])
        cols = n if wrap else n - 1
        for r in range(len(rings) - 1):
            for i in range(cols):
                j = (i + 1) % n
                self.face([ids[r][i], ids[r][j], ids[r + 1][j], ids[r + 1][i]], mat)
        cm = mat if cap_mat is None else cap_mat
        for flag, ring in ((cap_start, ids[0]), (cap_end, ids[-1])):
            if not flag:
                continue
            c = sum((self.verts[i] for i in ring), Vector()) / len(ring)
            ci = self.v(c)
            for i in range(len(ring) if wrap else len(ring) - 1):
                self.face([ring[i], ring[(i + 1) % len(ring)], ci], cm)
        return ids

    def tube(self, path, radius, sides=10, up=None, closed=False, cap_start=True, cap_end=True, mat=0,
             twist=0.0):
        """Un tubo por un camino. radius(t) da (ancho, grosor) o un número; up(p, t) orienta el ancho."""
        n = len(path)
        frames = []
        prev_n = None
        for k in range(n):
            if closed:
                tng = path[(k + 1) % n] - path[k - 1]
            else:
                tng = path[min(k + 1, n - 1)] - path[max(k - 1, 0)]
            tng.normalize()
            t = k / (n - 1) if n > 1 else 0.0
            if up is not None:
                ref = up(path[k], t)
            elif prev_n is not None:
                ref = prev_n
            else:
                ref = Vector((0, 1, 0)) if abs(tng.y) < 0.9 else Vector((1, 0, 0))
            nrm = ref - tng * ref.dot(tng)
            if nrm.length < 1e-6:
                nrm = Vector((1, 0, 0)) - tng * tng.x
            nrm.normalize()
            prev_n = nrm
            frames.append((tng, nrm, tng.cross(nrm)))
        rings = []
        for k in range(n):
            t = k / (n - 1) if n > 1 else 0.0
            r = radius(t) if callable(radius) else radius
            rx, ry = (r, r) if isinstance(r, (int, float)) else r
            tng, nrm, bin_ = frames[k]
            ring = []
            for s in range(sides):
                a = 2 * math.pi * s / sides + twist * t
                ring.append(path[k] + nrm * (math.cos(a) * rx) + bin_ * (math.sin(a) * ry))
            rings.append(ring)
        if closed:
            rings.append(rings[0])
            ids = self.loft(rings, wrap=True, mat=mat)
            return ids
        return self.loft(rings, wrap=True, cap_start=cap_start, cap_end=cap_end, mat=mat)

    def gem(self, center, axis, side, length, width, thick, mat=0):
        """Un rombo (los rayos de la aureola): punta a lo largo de axis, ancho por side."""
        axis = axis.normalized()
        side = side.normalized()
        up = axis.cross(side).normalized()
        tip = center + axis * length
        root = center - axis * length * 0.35
        mid = center
        pts = [mid + side * width, mid + up * thick, mid - side * width, mid - up * thick]
        a = [self.v(p) for p in pts]
        ti, ri = self.v(tip), self.v(root)
        for i in range(4):
            self.face([a[i], a[(i + 1) % 4], ti], mat)
            self.face([a[(i + 1) % 4], a[i], ri], mat)

    def build(self, name, mats, skin, sharp=40.0, orient=None, mods=(), subsurf=0, collection=MODEL_COLLECTION,
              merge=True):
        """orient: None = recalcular hacia afuera (piezas cerradas); un punto o función p->punto adentro
        para las láminas abiertas; False = dejarlas como vienen. merge=False conserva el orden de los
        vértices (las piezas del Dummy, que traen sus pesos por índice)."""
        me = bpy.data.meshes.new(name)
        me.from_pydata([B(p) for p in self.verts], [], self.faces)
        for poly, m in zip(me.polygons, self.mats):
            poly.material_index = m
        for m in mats:
            me.materials.append(m)
        me.validate(clean_customdata=False)

        bm = bmesh.new()
        bm.from_mesh(me)
        if merge:
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        if orient is False:
            pass
        elif orient is None:
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        else:
            for f in bm.faces:
                c = U(f.calc_center_median())
                inside = orient(c) if callable(orient) else orient
                if U(f.normal).dot(c - inside) < 0:
                    f.normal_flip()
        bm.to_mesh(me)
        bm.free()

        obj = bpy.data.objects.new(name, me)
        bpy.data.collections[collection].objects.link(obj)
        obj["merc_skin"] = skin
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=math.radians(sharp))

        # La subdivisión va primero: suaviza la forma y después se le da grosor
        if subsurf:
            mod = obj.modifiers.new("Subdivision", 'SUBSURF')
            mod.levels = subsurf
            mod.render_levels = subsurf
        for kind, settings in mods:
            mod = obj.modifiers.new(kind.title(), kind)
            for k, v in settings.items():
                setattr(mod, k, v)
        return obj


def solidify(thick, offset=1.0, rim_mat=0, shell_mat=0):
    return ('SOLIDIFY', dict(thickness=thick, offset=offset, use_even_offset=False, use_rim=True,
                             material_offset=shell_mat, material_offset_rim=rim_mat))


def bevel(width=0.0015, segments=2, angle=35.0):
    return ('BEVEL', dict(width=width, segments=segments, limit_method='ANGLE',
                          angle_limit=math.radians(angle), harden_normals=False))


# =========================================================
# BORDES DE ORO
# =========================================================

def boundary_loops(obj):
    """Los bordes abiertos de una malla (antes de modificadores), en orden y en el espacio de Unity."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    edges = [e for e in bm.edges if e.is_boundary]
    nexts = {}
    for e in edges:
        a, b = e.verts
        nexts.setdefault(a.index, []).append(b.index)
        nexts.setdefault(b.index, []).append(a.index)
    pos = {v.index: U(obj.matrix_world @ v.co) for v in bm.verts}
    bm.free()
    seen = set()
    loops = []
    for start in nexts:
        if start in seen:
            continue
        loop = [start]
        seen.add(start)
        prev, cur = None, start
        closed = False
        while True:
            options = [n for n in nexts[cur] if n != prev]
            if not options:
                break
            nxt = options[0]
            if nxt == start:
                closed = True
                break
            if nxt in seen:
                break
            loop.append(nxt)
            seen.add(nxt)
            prev, cur = cur, nxt
        loops.append(([pos[i] for i in loop], closed))
    return loops


def resample(pts, closed, step):
    """Menos puntos en un borde: uno cada step metros (los ribetes no necesitan todos los de la pieza)."""
    if step <= 0 or len(pts) < 4:
        return pts
    ring = pts + [pts[0]] if closed else pts
    out = [ring[0]]
    walked = 0.0
    for a, b in zip(ring, ring[1:]):
        walked += (b - a).length
        if walked >= step:
            out.append(b)
            walked = 0.0
    if closed and (out[-1] - ring[0]).length < step * 0.5 and len(out) > 3:
        out.pop()
    if not closed and out[-1] is not ring[-1]:
        out.append(ring[-1])
    return out if len(out) >= 3 else pts


def trim(obj, mat, radius=0.004, flat=0.7, skin=None, push=None, select=None, name=None, step=0.014):
    """Un ribete (tubo) por cada borde abierto de la pieza. push(p) corre el ribete hacia afuera;
    select(puntos) elige qué bordes llevan ribete."""
    m = Mesh()
    for pts, closed in boundary_loops(obj):
        if len(pts) < 3:
            continue
        if select is not None and not select(pts):
            continue
        pts = resample(pts, closed, step)
        if push is not None:
            pts = [p + push(p) for p in pts]
        m.tube(pts, (radius, radius * flat), sides=4, closed=closed, cap_start=True, cap_end=True)
    if not m.faces:
        return None
    return m.build(name or obj.name + "_Trim", [mat], skin or obj["merc_skin"], sharp=60.0)


# =========================================================
# TEXTO (flor de lis y compañía)
# =========================================================

SYMBOL_FONT = r"C:\Windows\Fonts\seguisym.ttf"


def glyph(char, size, depth, origin, normal, up, mat, skin, name="Glyph"):
    """Un símbolo de la fuente, en relieve, plantado en origin con el frente hacia normal (Unity).
    size es el alto del dibujo (m), no el de la letra."""
    font = bpy.data.fonts.load(SYMBOL_FONT, check_existing=True)
    cu = bpy.data.curves.new(name + "_Curve", 'FONT')
    cu.body = char
    cu.font = font
    cu.size = 1.0
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    cu.extrude = 0.5
    cu.resolution_u = 1
    tmp = bpy.data.objects.new(name + "_Tmp", cu)
    bpy.context.scene.collection.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)

    # Se arma en el espacio de Blender (diestro) para que el símbolo no salga espejado
    nb = B(normal).normalized()
    ub = B(up)
    ub = (ub - nb * ub.dot(nb)).normalized()
    rb = ub.cross(nb).normalized()
    ob = B(origin)
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    k = size / max(max(ys) - min(ys), 1e-6)
    m = Mesh()
    for v in me.vertices:
        p = ob + rb * ((v.co.x - cx) * k) + ub * ((v.co.y - cy) * k) + nb * ((v.co.z + 0.5) * depth)
        m.v(U(p))
    for poly in me.polygons:
        m.face(list(poly.vertices))
    bpy.data.meshes.remove(me)
    obj = m.build(name, [mat], skin, sharp=30.0, orient=False)
    return obj


# =========================================================
# PIEZAS DEL DUMMY
# =========================================================

def dummy_part(name, bones, mat, skin="keep", inflate=0.0, keep=None):
    """Copia del cuerpo del Dummy las caras que siguen a esos huesos (las manos), con sus pesos.
    keep(p) puede descartar caras por posición."""
    body = SK.body
    me = body.data
    groups = {g.index: g.name for g in body.vertex_groups}

    def dominant(v):
        best, w = None, -1.0
        for g in v.groups:
            if g.weight > w:
                best, w = groups.get(g.group), g.weight
        return best

    chosen = set()
    for poly in me.polygons:
        if all(dominant(me.vertices[i]) in bones for i in poly.vertices):
            if keep is None or keep(U(body.matrix_world @ poly.center)):
                chosen.add(poly.index)

    remap = {}
    m = Mesh()
    weights = []
    for pi in sorted(chosen):
        poly = me.polygons[pi]
        idx = []
        for vi in poly.vertices:
            if vi not in remap:
                v = me.vertices[vi]
                p = U(body.matrix_world @ v.co)
                n = U((body.matrix_world.to_3x3() @ v.normal).normalized())
                remap[vi] = m.v(p + n * inflate)
                weights.append({groups[g.group]: g.weight for g in v.groups if g.group in groups})
            idx.append(remap[vi])
        m.face(idx)
    obj = m.build(name, [mat], skin, sharp=70.0, merge=False)
    if skin == "keep":
        names = sorted({k for w in weights for k in w})
        vg = {n: obj.vertex_groups.new(name=n) for n in names}
        for i, w in enumerate(weights):
            for bone, value in w.items():
                vg[bone].add([i], value, 'REPLACE')
    return obj
