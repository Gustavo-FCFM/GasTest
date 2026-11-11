# -*- coding: utf-8 -*-
# ============================================================
# merc_export.py
#
# Saca un modelo de clase de Blender al formato que lee Unity (Mercenarios ▸ Modelos de clase ▸ Importar
# los modelos de Blender). Junta las piezas de la colección "Model" con sus modificadores, les pone los
# pesos según su "merc_skin", desenvuelve, hornea el color con sombras (AO), el pulido y el brillo, y
# escribe en Out/<Clase>/:
#   <Clase>.json        la malla en la pose T, en el espacio de Unity, con los pesos por nombre de hueso
#   <Clase>_Atlas.png   color (RGB) y pulido (alfa), como el acabado Polished de ClassModelKit
#   <Clase>_Glow.png    lo que brilla con luz propia
#   preview_*.png       cómo quedó, quieto y en una pose de prueba
#
# Para exportar un .blend que se editó a mano:
#   "H:/blender.exe" -b Blender/Paladin.blend --python Blender/merc_export.py
# ============================================================
import json
import re
import math
import os
import struct
import sys
import zlib

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import merc_kit as K   # noqa: E402

ATLAS = 2048
# Cuánto más grande sale cada material en el atlas (la cara necesita más píxeles que la capa)
TEXEL = {"Skin": 4.0, "LionFace": 4.0, "Mane": 0.8, "Cream": 0.8, "Hair": 1.0, "Lining": 0.5, "Under": 0.5, "Parchment": 1.4, "Wax": 1.4}
AO_STRENGTH = 0.45


# =========================================================
# PNG (sin depender de cómo guarde Blender el alfa)
# =========================================================

def write_png(path, rgba):
    """rgba: uint8 (alto, ancho, 4) con la fila 0 ABAJO, como los píxeles de Blender."""
    rgba = np.ascontiguousarray(rgba[::-1])
    h, w, _ = rgba.shape
    raw = b"".join(b"\x00" + rgba[y].tobytes() for y in range(h))

    def chunk(kind, data):
        c = struct.pack(">I", len(data)) + kind + data
        return c + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


# =========================================================
# JUNTAR CON PESOS
# =========================================================

def model_objects():
    # all_objects guarda una lista que se rompe al crear objetos nuevos: se recorre bpy.data.objects
    return [o for o in bpy.data.objects
            if o.type == 'MESH' and not o.hide_render and any(c.name == K.MODEL_COLLECTION for c in o.users_collection)]


def evaluated_copy(obj, coll):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    me.transform(obj.matrix_world)
    copy = bpy.data.objects.new(obj.name + "_Export", me)
    coll.objects.link(copy)

    spec = obj.get("merc_skin", "torso")
    if spec == "keep":
        # La malla ya trae los grupos por nombre: solo hay que asegurarse de que el objeto los tenga
        for g in obj.vertex_groups:
            if g.name not in copy.vertex_groups:
                copy.vertex_groups.new(name=g.name)
        return copy

    groups = {}
    for v in me.vertices:
        for bone, w in K.top4(K.skin_for(spec, K.U(v.co))):
            g = groups.get(bone)
            if g is None:
                g = groups[bone] = copy.vertex_groups.new(name=bone)
            g.add([v.index], w, 'REPLACE')
    return copy


def joined_model(name):
    coll = bpy.data.collections.new("Export")
    bpy.context.scene.collection.children.link(coll)
    copies = [evaluated_copy(o, coll) for o in model_objects()]

    bpy.ops.object.select_all(action='DESELECT')
    for c in copies:
        c.select_set(True)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = name
    return joined


def unwrap(obj):
    me = obj.data
    atlas = me.uv_layers.new(name="Atlas")
    me.uv_layers.active = atlas
    atlas.active_render = True

    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.002, area_weight=0.0,
                             scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')

    # Más píxeles para lo que se mira de cerca
    bm = bmesh.new()
    bm.from_mesh(me)
    layer = bm.loops.layers.uv["Atlas"]
    names = [m.name if m else "" for m in me.materials]
    for f in bm.faces:
        k = TEXEL.get(names[f.material_index], 1.0)
        if k != 1.0:
            for loop in f.loops:
                loop[layer].uv *= k
    bm.to_mesh(me)
    bm.free()

    # Con la selección sincronizada, todo lo seleccionado en la malla entra al empaque
    bpy.context.scene.tool_settings.use_uv_select_sync = True
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.0025)
    bpy.ops.object.mode_set(mode='OBJECT')


# =========================================================
# HORNEAR
# =========================================================

def bake(obj, kind, image, samples=1, pass_filter=None):
    sc = bpy.context.scene
    sc.cycles.samples = samples
    for slot in obj.material_slots:
        nt = slot.material.node_tree
        n = nt.nodes.get("BakeTarget")
        if n is None:
            n = nt.nodes.new("ShaderNodeTexImage")
            n.name = "BakeTarget"
            n.location = (600, 400)
        n.image = image
        nt.nodes.active = n
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    args = dict(type=kind, margin=12, use_clear=True)
    if pass_filter:
        args["pass_filter"] = pass_filter
    bpy.ops.object.bake(**args)
    px = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
    image.pixels.foreach_get(px)
    return px.reshape(image.size[1], image.size[0], 4)


def blur(px, passes=2, r=2):
    """Suaviza el ruido del AO (pocas muestras) sin cruzar mucho los bordes de las islas."""
    out = px.copy()
    for _ in range(passes):
        acc = np.zeros_like(out)
        for dy in range(-r, r + 1):
            acc += np.roll(out, dy, axis=0)
        out = acc / (2 * r + 1)
        acc = np.zeros_like(out)
        for dx in range(-r, r + 1):
            acc += np.roll(out, dx, axis=1)
        out = acc / (2 * r + 1)
    return out


def bake_all(obj, out_dir, name):
    sc = bpy.context.scene
    if sc.world is None:
        sc.world = bpy.data.worlds.new("Bake")
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.render.bake.target = 'IMAGE_TEXTURES'

    def img(n, color):
        i = bpy.data.images.new(n, ATLAS, ATLAS, alpha=False, float_buffer=False)
        i.colorspace_settings.name = 'sRGB' if color else 'Non-Color'
        return i

    # El color sin luz: lo metálico se hornea como si no lo fuera (si no, sale negro)
    metals = []
    for slot in obj.material_slots:
        b = slot.material.node_tree.nodes.get("Principled BSDF")
        metals.append((b, b.inputs["Metallic"].default_value))
        b.inputs["Metallic"].default_value = 0.0
    color = bake(obj, 'DIFFUSE', img("Bake_Color", True), 1, {'COLOR'})
    for b, v in metals:
        b.inputs["Metallic"].default_value = v
    print("[Export] color horneado")
    rough = bake(obj, 'ROUGHNESS', img("Bake_Rough", False), 1)
    print("[Export] aspereza horneada")
    glow = bake(obj, 'EMIT', img("Bake_Glow", True), 1)
    print("[Export] brillo horneado")
    # Sombra solo de lo que está cerca (pliegues, uniones): si no, el pelo oscurece toda la cara
    sc.world.light_settings.distance = 0.04
    ao = blur(bake(obj, 'AO', img("Bake_AO", False), 64))
    print("[Export] sombras horneadas")

    shade = (1.0 - AO_STRENGTH) + AO_STRENGTH * ao[..., :1]
    rgba = np.empty_like(color)
    rgba[..., :3] = color[..., :3] * shade
    rgba[..., 3] = 1.0 - rough[..., 0]
    write_png(os.path.join(out_dir, f"{name}_Atlas.png"), (np.clip(rgba, 0, 1) * 255 + 0.5).astype(np.uint8))

    g = np.empty_like(glow)
    g[..., :3] = glow[..., :3]
    g[..., 3] = 1.0
    write_png(os.path.join(out_dir, f"{name}_Glow.png"), (np.clip(g, 0, 1) * 255 + 0.5).astype(np.uint8))
    has_glow = bool(glow[..., :3].max() > 0.01)
    return has_glow


# =========================================================
# ESCRIBIR LA MALLA
# =========================================================

def write_json(obj, out_dir, name, has_glow):
    me = obj.data
    me.calc_loop_triangles()
    uv = me.uv_layers["Atlas"].data
    normals = me.corner_normals
    group_names = [g.name for g in obj.vertex_groups]

    bones = sorted({n for n in group_names})
    bone_index = {n: i for i, n in enumerate(bones)}
    vweights = []
    for v in me.vertices:
        w = {}
        for g in v.groups:
            if g.weight > 1e-4:
                n = re.sub(r"\.\d{3}$", "", group_names[g.group])
                w[n] = w.get(n, 0.0) + g.weight
        top = K.top4(w) if w else [("B-hips", 1.0)]
        vweights.append(top)

    positions, nrm, uvs, bidx, bw, tris = [], [], [], [], [], []
    unique = {}
    skipped = 0
    for tri in me.loop_triangles:
        if tri.area < 1e-10:
            skipped += 1
            continue
        ids = []
        for li in tri.loops:
            vi = me.loops[li].vertex_index
            n = normals[li].vector
            if n.length < 1e-6:
                n = tri.normal
            u = uv[li].uv
            key = (vi, round(n.x, 3), round(n.y, 3), round(n.z, 3), round(u.x, 5), round(u.y, 5))
            idx = unique.get(key)
            if idx is None:
                idx = unique[key] = len(positions) // 3
                p = K.U(me.vertices[vi].co)
                nn = K.U(n).normalized()
                positions += [round(p.x, 5), round(p.y, 5), round(p.z, 5)]
                nrm += [round(nn.x, 4), round(nn.y, 4), round(nn.z, 4)]
                uvs += [round(u.x, 5), round(u.y, 5)]
                top = vweights[vi] + [("", 0.0)] * (4 - len(vweights[vi]))
                for bone, w in top:
                    bidx.append(bone_index.get(bone, -1) if w > 0 else -1)
                    bw.append(round(w, 4))
            ids.append(idx)
        # Unity es zurdo: el espejo de x da vuelta el sentido de las caras
        tris += [ids[0], ids[2], ids[1]]

    bone_pos = []
    for b in bones:
        p = K.SK.head.get(b, Vector((0, 0, 0)))
        bone_pos += [round(p.x, 5), round(p.y, 5), round(p.z, 5)]

    data = dict(name=name, finish="Polished", glowIntensity=1.25 if has_glow else 0.0,
                boneNames=bones, bonePositions=bone_pos, positions=positions, normals=nrm, uvs=uvs,
                boneIndices=bidx, boneWeights=bw, triangles=tris)
    path = os.path.join(out_dir, f"{name}.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, separators=(",", ":"))
    print(f"[Export] {name}: {len(positions) // 3} vértices, {len(tris) // 3} triángulos "
          f"({skipped} caras sin área salteadas), {len(bones)} huesos -> {path}")


# =========================================================
# VISTAS PREVIAS
# =========================================================

def studio():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE_NEXT'
    sc.render.resolution_x = 900
    sc.render.resolution_y = 1100
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.get("Studio") or bpy.data.worlds.new("Studio")
    sc.world = w
    w.use_nodes = True
    bg = next((n for n in w.node_tree.nodes if n.type == 'BACKGROUND'), None)
    if bg is None:
        bg = w.node_tree.nodes.new("ShaderNodeBackground")
        out = next((n for n in w.node_tree.nodes if n.type == 'OUTPUT_WORLD'), None) or w.node_tree.nodes.new("ShaderNodeOutputWorld")
        w.node_tree.links.new(bg.outputs[0], out.inputs[0])
    bg.inputs[0].default_value = (0.22, 0.24, 0.27, 1)
    bg.inputs[1].default_value = 1.0
    if "Key" not in bpy.data.objects:
        for n, rot, energy in (("Key", (50, 0, 35), 3.2), ("Fill", (65, 0, -60), 1.2), ("Rim", (60, 0, 170), 2.0)):
            light = bpy.data.lights.new(n, 'SUN')
            light.energy = energy
            o = bpy.data.objects.new(n, light)
            sc.collection.objects.link(o)
            o.rotation_euler = tuple(math.radians(a) for a in rot)


def camera(name, target_u, from_u, scale):
    sc = bpy.context.scene
    cam = bpy.data.objects.get(name)
    if cam is None:
        cd = bpy.data.cameras.new(name)
        cam = bpy.data.objects.new(name, cd)
        sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = scale
    t = K.B(target_u)
    f = K.B(from_u)
    cam.location = f
    cam.rotation_euler = (t - f).to_track_quat('-Z', 'Y').to_euler()
    return cam


VIEWS = [
    ("front", (0, 1.0, 0), (0, 1.0, 6), 2.15),
    ("threequarter", (0, 1.0, 0), (-4.2, 1.6, 4.2), 2.15),
    ("back", (0, 1.0, 0), (0, 1.0, -6), 2.15),
    ("side", (0, 1.0, 0), (6, 1.0, 0), 2.15),
    ("face", (0, 1.63, 0), (-1.2, 1.7, 5.5), 0.62),
    ("faceside", (0, 1.66, 0.02), (6, 1.66, 0.02), 0.45),
    ("facefront", (0, 1.66, 0.02), (0, 1.66, 6), 0.45),
]


def render_views(out_dir, prefix, views=VIEWS):
    studio()
    sc = bpy.context.scene
    for name, target, frm, scale in views:
        sc.camera = camera("Cam_" + name, target, frm, scale)
        sc.render.filepath = os.path.join(out_dir, f"{prefix}_{name}.png")
        bpy.ops.render.render(write_still=True)


def baked_material(name, out_dir, has_glow):
    m = bpy.data.materials.new(name + "_Baked")
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(out_dir, f"{name}_Atlas.png"))
    tex.image.alpha_mode = 'CHANNEL_PACKED'
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = 'SUBTRACT'
    inv.inputs[0].default_value = 1.0
    nt.links.new(tex.outputs["Alpha"], inv.inputs[1])
    nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    bsdf.inputs["Specular IOR Level"].default_value = 0.5
    if has_glow:
        g = nt.nodes.new("ShaderNodeTexImage")
        g.image = bpy.data.images.load(os.path.join(out_dir, f"{name}_Glow.png"))
        nt.links.new(g.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 1.25
    return m


def pose_rig():
    """Un esqueleto con los nombres del jugador para probar los pesos (no se exporta)."""
    sk = K.SK
    arm = bpy.data.armatures.new("PoseRig")
    rig = bpy.data.objects.new("PoseRig", arm)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for n in sk.head:
        eb = arm.edit_bones.new(n)
        eb.head = K.B(sk.head[n])
        tail = K.B(sk.tail[n])
        if (tail - eb.head).length < 0.02:
            tail = eb.head + Vector((0, 0, 0.08))
        eb.tail = tail
    for n, p in sk.parent.items():
        if p:
            arm.edit_bones[n].parent = arm.edit_bones[p]
    bpy.ops.object.mode_set(mode='OBJECT')
    return rig


def turn(rig, bone, axis_u, degrees):
    """Gira un hueso alrededor de un eje del mundo (Unity)."""
    pb = rig.pose.bones[bone]
    pb.rotation_mode = 'QUATERNION'
    m = pb.bone.matrix_local.to_3x3()
    r = Matrix.Rotation(math.radians(degrees), 3, K.B(axis_u).normalized())
    q = (m.inverted() @ r @ m).to_quaternion()
    pb.rotation_quaternion = q @ pb.rotation_quaternion


def posed_views(obj, out_dir):
    rig = pose_rig()
    mod = obj.modifiers.new("Pose", 'ARMATURE')
    mod.object = rig
    # Brazos abajo, una pierna adelante, el torso girado y la cabeza de costado
    turn(rig, "B-upperArm.L", (0, 0, 1), -62)
    turn(rig, "B-upperArm.R", (0, 0, 1), 62)
    turn(rig, "B-forearm.L", (0, 1, 0), 35)
    turn(rig, "B-forearm.R", (0, 1, 0), -35)
    turn(rig, "B-thigh.L", (1, 0, 0), -38)
    turn(rig, "B-shin.L", (1, 0, 0), 45)
    turn(rig, "B-thigh.R", (1, 0, 0), 18)
    turn(rig, "B-chest", (0, 1, 0), 18)
    turn(rig, "B-head", (0, 1, 0), -25)
    render_views(out_dir, "posed", [VIEWS[0], VIEWS[1], VIEWS[3]])


# =========================================================
# TODO
# =========================================================

def run(name=None, preview_only=False):
    if K.SK is None:
        K.from_blend()
    name = name or bpy.context.scene.get("merc_name") or os.path.splitext(os.path.basename(bpy.data.filepath))[0]
    out_dir = os.path.join(K.OUT, name)
    os.makedirs(out_dir, exist_ok=True)

    if preview_only:
        render_views(out_dir, "shape")
        return

    # Las piezas originales se esconden: si no, al hornear la sombra cada superficie se tapa con su copia
    originals = model_objects()
    obj = joined_model(name)
    for o in originals:
        o.hide_render = True
    unwrap(obj)
    has_glow = bake_all(obj, out_dir, name)
    write_json(obj, out_dir, name, has_glow)

    # Lo que va a ver Unity: la malla con las texturas horneadas
    baked = baked_material(name, out_dir, has_glow)
    obj.data.materials.clear()
    obj.data.materials.append(baked)
    for p in obj.data.polygons:
        p.material_index = 0
    render_views(out_dir, "preview")
    posed_views(obj, out_dir)


if __name__ == "__main__":
    run()
