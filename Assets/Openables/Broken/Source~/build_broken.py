"""Openables 파손 모델 빌더 (Blender 5.2, 헤드리스).

원본 Assets/Openables/Models/<name>.fbx 를 가져와 찌그러뜨리고, 파츠를 뜯어내고, 유리를 깨뜨리고,
바닥에 파편을 깔아 Assets/Openables/Broken/Models/<name>_broken.fbx 로 내보낸다.

  blender -b --factory-startup --python-exit-code 1 -P build_broken.py -- [이름...] [--preview DIR] [--no-export]

좌표 (Blender, 원본 FBX 를 가져온 상태): Z 위, +Y 앞, 바닥 z=0, 벽걸이(elec_panel, fire_cabinet)는 뒤판 y=0.
모든 메쉬는 월드 좌표로 구운 뒤 작업하고, 내보낼 때는 본체(<name>_broken)를 루트로 두고 나머지를 자식(단위 변환)으로 붙인다.
이름이 _debris / _glass 로 끝나는 오브젝트는 Unity 에서 콜라이더를 붙이지 않는다(OpenablesBrokenPostprocessor).
"""
import bpy, bmesh, sys, os, math, random
from mathutils import Vector, Matrix, noise

HERE = os.path.dirname(os.path.abspath(__file__))
OPENABLES = os.path.normpath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(OPENABLES, "Models")
DST = os.path.join(OPENABLES, "Broken", "Models")
NAMES = ["anesthesia", "cardboard_box", "elec_panel", "filing", "fire_cabinet", "tall_cabinet", "tea_niche"]

# ───────────────────────── 기본 유틸 ─────────────────────────

def V(*a): return Vector(a)

def rot_about(p, axis, ang):
    p = Vector(p)
    return Matrix.Translation(p) @ Matrix.Rotation(ang, 4, Vector(axis).normalized()) @ Matrix.Translation(-p)

def load(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, name + ".fbx"))
    objs = {}
    for o in list(bpy.context.scene.objects):
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)
            continue
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = Matrix.Identity(4)
        o.data.transform(mw)
        for k in list(o.keys()):
            del o[k]
        me = o.data
        if "custom_normal" in me.attributes:
            me.attributes.remove(me.attributes["custom_normal"])
        objs[o.name] = o
    return objs

def mat(name):
    m = bpy.data.materials.get(name)
    if m is None:
        raise RuntimeError("material not found: " + name)
    return m

def bounds(objs):
    if not isinstance(objs, (list, tuple)): objs = [objs]
    pts = [v.co for o in objs for v in o.data.vertices]
    return (V(*(min(p[i] for p in pts) for i in range(3))), V(*(max(p[i] for p in pts) for i in range(3))))

def xform(objs, M):
    if not isinstance(objs, (list, tuple)): objs = [objs]
    for o in objs: o.data.transform(M)

def to_floor(objs, z=0.0):
    mn, _ = bounds(objs)
    xform(objs, Matrix.Translation(V(0, 0, z - mn.z)))

def bm_edit(o):
    bm = bmesh.new(); bm.from_mesh(o.data); return bm

def bm_done(o, bm):
    bm.normal_update(); bm.to_mesh(o.data); bm.free(); o.data.update()

# ───────────────────────── 손상 연산 ─────────────────────────

def densify(o, max_len=0.08, passes=4):
    """긴 모서리를 반복 이등분 — 저폴리 박스도 찌그러질 수 있게."""
    bm = bm_edit(o)
    for _ in range(passes):
        long = [e for e in bm.edges if e.calc_length() > max_len]
        if not long: break
        bmesh.ops.subdivide_edges(bm, edges=long, cuts=1, use_grid_fill=True)
    bm_done(o, bm)

def dent(o, center, radius, depth, direction):
    """구 영역 안 정점을 direction 으로 밀어 움푹 들어가게 (부드러운 falloff)."""
    c = Vector(center); d = Vector(direction).normalized() * depth
    for v in o.data.vertices:
        r = (v.co - c).length
        if r < radius:
            f = 1.0 - (r / radius) ** 2
            v.co += d * (f * f)
    o.data.update()

def crumple(o, amp, scale, seed):
    """공간 노이즈로 전체를 살짝 울퉁불퉁하게 — 공간 함수라 겹친 정점도 같이 움직여 틈이 안 생긴다."""
    off = V(seed * 17.3, seed * 5.1, seed * 9.7)
    for v in o.data.vertices:
        v.co += noise.noise_vector(v.co * scale + off) * amp
    o.data.update()

def islands(bm):
    seen = set(); out = []
    for f in bm.faces:
        if f.index in seen: continue
        stack = [f]; seen.add(f.index); isl = []
        while stack:
            g = stack.pop(); isl.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index); stack.append(h)
        out.append(isl)
    return out

def island_verts(faces):
    return list({v for f in faces for v in f.verts})

def island_center(faces):
    vs = island_verts(faces)
    return sum((v.co for v in vs), Vector()) / len(vs)

def extract(o, mat_name, new_name):
    """o 에서 mat_name 머티리얼 면을 떼어 새 오브젝트로."""
    idx = [i for i, s in enumerate(o.material_slots) if s.material and s.material.name == mat_name]
    me = o.data.copy(); me.name = new_name
    new = bpy.data.objects.new(new_name, me)
    bpy.context.scene.collection.objects.link(new)
    bm = bm_edit(new)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index not in idx], context='FACES')
    bm_done(new, bm)
    bm = bm_edit(o)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index in idx], context='FACES')
    bm_done(o, bm)
    return new

def shatter(o, cuts, rng, sheet=False):
    """판유리(가장 얇은 축이 법선)를 무작위 평면으로 잘라 조각(섬)으로 나눈다.
    sheet=True 면 판의 앞면 한 장만 남겨 자른다(옆면 띠가 조각으로 남지 않게) — 두께는 나중에 solidify 로."""
    mn, mx = bounds(o); size = mx - mn
    ax = min(range(3), key=lambda i: size[i])
    pane_n = V(0, 0, 0); pane_n[ax] = 1
    c = (mn + mx) / 2
    bm = bm_edit(o)
    if sheet:
        bm.normal_update()
        front = max((1, -1), key=lambda sgn: sum(f.calc_area() for f in bm.faces if f.normal.dot(pane_n) * sgn > 0.9))
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.dot(pane_n) * front <= 0.9], context='FACES')
    for _ in range(cuts):
        p = V(*(rng.uniform(mn[i] + size[i] * .2, mx[i] - size[i] * .2) for i in range(3)))
        d = Vector(rng.uniform(-1, 1) for _ in range(3)); d[ax] = 0
        if d.length < 1e-3: continue
        n = pane_n.cross(d).normalized()
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        res = bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=p, plane_no=n)
        cut = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        if cut: bmesh.ops.split_edges(bm, edges=cut)
    bm.faces.ensure_lookup_table()
    return bm, pane_n, c

def lay_flat_matrix(pane_n, src_center, dst, yaw, tilt=0.0):
    """판의 법선이 Z 를 보도록 눕혀 dst 에 놓는 행렬."""
    q = Vector(pane_n).rotation_difference(V(0, 0, 1))
    return (Matrix.Translation(dst) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(tilt, 4, 'X')
            @ q.to_matrix().to_4x4() @ Matrix.Translation(-Vector(src_center)))

def settle_island(faces, z=0.0):
    vs = island_verts(faces)
    mz = min(v.co.z for v in vs)
    for v in vs: v.co.z += z - mz

class Debris:
    """한 오브젝트에 모으는 바닥 파편 (박스·판·종이)."""
    def __init__(self, name):
        self.me = bpy.data.meshes.new(name)
        self.obj = bpy.data.objects.new(name, self.me)
        bpy.context.scene.collection.objects.link(self.obj)
        self.bm = bmesh.new(); self.uv = self.bm.loops.layers.uv.verify()
        self.slots = {}

    def _slot(self, m):
        if m not in self.slots:
            self.obj.data.materials.append(mat(m)); self.slots[m] = len(self.slots)
        return self.slots[m]

    def box(self, m, size, loc, rot=(0, 0, 0), on_floor=True):
        M = (Matrix.Translation(Vector(loc)) @ Matrix.Rotation(rot[2], 4, 'Z') @ Matrix.Rotation(rot[1], 4, 'Y')
             @ Matrix.Rotation(rot[0], 4, 'X') @ Matrix.Diagonal(Vector(size)).to_4x4())
        res = bmesh.ops.create_cube(self.bm, size=1.0, matrix=M, calc_uvs=True)
        vs = res["verts"]; fs = {f for v in vs for f in v.link_faces}
        k = self._slot(m)
        for f in fs: f.material_index = k
        if on_floor:
            mz = min(v.co.z for v in vs)
            for v in vs: v.co.z -= mz - loc[2]

    def sheet(self, m, w, h, loc, yaw, z_lift=0.0):
        k = self._slot(m)
        cs = [V(-w / 2, -h / 2, 0), V(w / 2, -h / 2, 0), V(w / 2, h / 2, 0), V(-w / 2, h / 2, 0)]
        R = Matrix.Rotation(yaw, 3, 'Z')
        vs = [self.bm.verts.new(Vector(loc) + R @ c + V(0, 0, z_lift)) for c in cs]
        f = self.bm.faces.new(vs); f.material_index = k
        for l, uv in zip(f.loops, [(0, 0), (1, 0), (1, 1), (0, 1)]): l[self.uv].uv = uv

    def scatter(self, rng, mats, n, area_min, area_max, smin, smax, z=0.0):
        for _ in range(n):
            s = [rng.uniform(smin[i], smax[i]) for i in range(3)]
            loc = (rng.uniform(area_min[0], area_max[0]), rng.uniform(area_min[1], area_max[1]), z)
            self.box(rng.choice(mats), s, loc, (rng.uniform(-.25, .25), rng.uniform(-.25, .25), rng.uniform(0, math.pi)))

    def finish(self):
        self.bm.normal_update(); self.bm.to_mesh(self.me); self.bm.free()
        return self.obj

def glass_scatter(o, rng, keep_ratio, floor_count, floor_min, floor_max, pose=None, floor_z=0.0, drop_rest=True, cuts=14, thickness=0.004):
    """shatter 후: 판 가장자리 조각은 남기고(pose 적용), floor_count 개는 바닥에 눕히고, 나머지는 지운다."""
    mn, mx = bounds(o)
    bm, pane_n, c = shatter(o, cuts, rng, sheet=thickness > 0)
    isl = islands(bm)
    half = (mx - mn) / 2
    def edge_score(fs):  # 0 중앙 ~ 1 가장자리 (얇은 축은 제외)
        p = island_center(fs) - c
        return max(abs(p[i]) / half[i] if half[i] > max(half) * 0.1 else 0 for i in range(3))
    isl.sort(key=edge_score, reverse=True)
    n_keep = max(1, int(len(isl) * keep_ratio))
    keep, rest = isl[:n_keep], isl[n_keep:]
    rng.shuffle(rest)
    floor, gone = rest[:floor_count], rest[floor_count:]
    if pose is not None:
        for fs in keep:
            for v in island_verts(fs): v.co = pose @ v.co
    for fs in floor:
        src = island_center(fs)
        dst = V(rng.uniform(floor_min[0], floor_max[0]), rng.uniform(floor_min[1], floor_max[1]), floor_z)
        M = lay_flat_matrix(pane_n, src, dst, rng.uniform(0, math.tau), rng.uniform(-.08, .08))
        for v in island_verts(fs): v.co = M @ v.co
        settle_island(fs, floor_z + 0.001)
    if drop_rest:
        bmesh.ops.delete(bm, geom=[f for fs in gone for f in fs], context='FACES')
    if thickness > 0:
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=thickness)
    bm_done(o, bm)
    return o

def finalize(name, body, extra):
    body.name = name + "_broken"; body.data.name = body.name
    for o in bpy.context.scene.objects:
        if o.type != 'MESH': continue
        me = o.data
        if len(me.polygons) == 0:
            bpy.data.objects.remove(o, do_unlink=True); continue
        # 찌그러진 n-gon 은 비평면/자기교차가 되어 Unity 가 버린다 → 여기서 삼각분할
        bm = bm_edit(o)
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
        bm_done(o, bm)
        me.shade_smooth()
        me.set_sharp_from_angle(angle=math.radians(35))
    for o in bpy.context.scene.objects:
        if o is body: continue
        o.parent = body; o.matrix_parent_inverse = Matrix.Identity(4)

def export(name, preview_dir, do_export):
    if preview_dir: preview(name, preview_dir)
    if not do_export: return
    os.makedirs(DST, exist_ok=True)
    path = os.path.join(DST, name + "_broken.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=False, object_types={'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                             mesh_smooth_type='OFF', use_mesh_modifiers=True, use_custom_props=False,
                             add_leaf_bones=False, bake_anim=False, path_mode='STRIP', use_tspace=False)
    print("EXPORTED", path)

# ───────────────────────── 미리보기 ─────────────────────────

def preview(name, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    sc = bpy.context.scene
    objs = [o for o in sc.objects if o.type == 'MESH']
    mn, mx = bounds(objs); c = (mn + mx) / 2; rad = (mx - mn).length / 2
    floor = bpy.data.objects.new("_floor", bpy.data.meshes.new("_floor"))
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=max(3, rad * 3))
    bm.to_mesh(floor.data); bm.free(); sc.collection.objects.link(floor)
    wall = None
    if name in ("elec_panel", "fire_cabinet"):
        wall = bpy.data.objects.new("_wall", bpy.data.meshes.new("_wall"))
        bm = bmesh.new()
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=3, matrix=Matrix.Translation(V(0, -0.001, 1.5)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
        bm.to_mesh(wall.data); bm.free(); sc.collection.objects.link(wall)
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_shadows = True; sh.show_cavity = True
    sc.render.resolution_x = 700; sc.render.resolution_y = 700
    cam = bpy.data.objects.new("_cam", bpy.data.cameras.new("_cam")); sc.collection.objects.link(cam)
    cam.data.lens = 40; sc.camera = cam
    fov = cam.data.angle
    dist = rad / math.sin(fov / 2) * 1.05
    for i, d in enumerate([V(0.9, 1.3, 0.55), V(-1.2, 0.9, 0.9)]):
        d.normalize()
        cam.location = c + d * dist
        cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(out_dir, f"{name}_{i}.png")
        bpy.ops.render.render(write_still=True)
    for o in (cam, floor, wall):
        if o: bpy.data.objects.remove(o, do_unlink=True)

# ───────────────────────── 모델별 ─────────────────────────

def b_anesthesia(rng):
    o = load("anesthesia")
    body = o["anesthesia"]; d1, d2, d3 = o["anesthesia_drawer1"], o["anesthesia_drawer2"], o["anesthesia_drawer3"]
    # 앞-왼쪽 바퀴 고무 떼기
    bm = bm_edit(body)
    ci = [i for i, s in enumerate(body.material_slots) if s.material and s.material.name.startswith("Caster rubber")]
    cand = [fs for fs in islands(bm) if len(fs) < 1500 and sum(f.material_index in ci for f in fs) > len(fs) * 0.5]
    if cand:
        wheel = min(cand, key=lambda fs: island_center(fs).x - island_center(fs).y)
        bmesh.ops.delete(bm, geom=wheel, context='FACES')
    bm_done(body, bm)
    # 모니터 화면 깨짐
    mon = extract(body, "Monitor display.002", "anesthesia_monitor_glass")
    # 모니터: 본체 안 섬들 중 모니터 영역에 완전히 들어가는 것을 비뚤게 꺾는다
    mp = (0.29, 0.6, 1.8)
    mpose = rot_about(mp, (0, 0, 1), 0.22) @ rot_about(mp, (0, 1, 0), 0.16) @ rot_about(mp, (1, 0, 0), -0.12)
    bm = bm_edit(body)
    moved = 0
    for fs in islands(bm):
        vs = island_verts(fs)
        if all(-0.02 < v.co.x < 0.62 and 0.4 < v.co.y < 0.72 and 1.76 < v.co.z < 2.16 for v in vs):
            for v in vs: v.co = mpose @ v.co
            moved += len(fs)
    bm_done(body, bm)
    print("anesthesia monitor faces moved:", moved)
    glass_scatter(mon, rng, keep_ratio=0.5, floor_count=0, floor_min=(0, 0), floor_max=(0, 0), pose=mpose, thickness=0)
    # 찌그러짐
    crumple(body, 0.004, 6, 1)
    dent(body, (-0.77, 0.45, 1.25), 0.28, 0.045, (1, 0, 0))
    dent(body, (0.4, 0.82, 0.62), 0.22, 0.03, (0, -1, 0))
    dent(body, (0.75, 0.2, 1.7), 0.2, 0.035, (-1, 0, 0))
    # 서랍: 1은 크게 빠져 비뚤, 2는 조금, 3은 바닥에 엎어짐
    xform(d1, rot_about((-0.2, 0.75, 0.43), (0, 0, 1), 0.06) @ Matrix.Translation(V(0, 0.27, 0)))
    xform(d1, rot_about((-0.2, 0.5, 0.335), (0, 1, 0), -0.05))
    xform(d2, rot_about((-0.2, 0.75, 0.64), (0, 0, 1), -0.04) @ Matrix.Translation(V(0, 0.12, 0)))
    # 몸체 기울기(앞-왼쪽 바퀴 빠짐)
    tilt = rot_about((0.17, 0.2, 0), (0, 1, 0), -0.055) @ rot_about((0, 0.2, 0), (1, 0, 0), -0.03)
    attached = [body, d1, d2, mon]
    xform(attached, tilt); to_floor(attached)
    # 바닥의 서랍: 엎어서 앞쪽에
    c3 = sum((v.co for v in d3.data.vertices), Vector()) / len(d3.data.vertices)
    xform(d3, Matrix.Translation(V(0.55, 1.35, 0)) @ Matrix.Rotation(0.5, 4, 'Z') @ Matrix.Rotation(math.pi * 0.93, 4, 'Y') @ Matrix.Translation(-c3))
    to_floor(d3)
    deb = Debris("anesthesia_debris")
    deb.scatter(rng, ["Dark steel.002", "Aged ivory enamel.002", "Faded medical blue.002"], 9, (-0.7, 0.85), (0.7, 1.6), (0.02, 0.02, 0.004), (0.09, 0.06, 0.012))
    deb.box("Caster rubber.002", (0.07, 0.03, 0.07), (-0.95, 1.05, 0.0), (math.pi / 2, 0, 0.7))
    deb.finish()
    finalize("anesthesia", body, [])

def b_cardboard_box(rng):
    o = load("cardboard_box")
    body = o["cardboard_box"]
    fB, fF, fL, fR = (o["cardboard_box_flap" + k] for k in "BFLR")
    for p in (body, fB, fF, fL, fR): densify(p, 0.05, 4)
    # 한쪽 모서리가 눌려 무너짐 + 앞면 함몰
    dent(body, (0.225, 0.35, 0.3), 0.26, 0.11, (-0.25, -0.2, -1))
    dent(body, (-0.05, 0.0, 0.14), 0.16, 0.05, (0, 1, 0))
    dent(body, (-0.225, 0.22, 0.2), 0.12, 0.03, (1, 0, 0))
    crumple(body, 0.006, 9, 2)
    # 뒷날개: 바깥으로 축 처짐
    xform(fB, rot_about((0, 0.35, 0.3), (1, 0, 0), -2.35))
    dent(fB, (0.15, 0.35, 0.47), 0.14, 0.03, (0, 0, 1))
    crumple(fB, 0.004, 14, 3)
    # 왼날개: 비스듬히 열림
    xform(fL, rot_about((-0.215, 0.0, 0.295), (0, 1, 0), -1.9))
    crumple(fL, 0.004, 14, 4)
    # 앞·오른날개: 찢겨 바닥
    for f, loc, yaw in ((fF, (0.05, 0.42, 0), 0.45), (fR, (0.42, 0.12, 0), -0.9)):
        mn, mx = bounds(f); c = (mn + mx) / 2
        xform(f, Matrix.Translation(V(*loc)) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(0.06, 4, 'X') @ Matrix.Translation(-c))
        crumple(f, 0.004, 14, 5)
        to_floor(f)
    deb = Debris("cardboard_box_debris")
    deb.scatter(rng, ["wp_cardboard"], 5, (-0.4, -0.15), (0.45, 0.55), (0.02, 0.02, 0.003), (0.07, 0.05, 0.004))
    deb.finish()
    finalize("cardboard_box", body, [])

def b_elec_panel(rng):
    o = load("elec_panel")
    body, door = o["elec_panel"], o["elec_panel_door"]
    densify(body, 0.06, 4); densify(door, 0.05, 4)
    dent(body, (0.12, 0.11, 1.42), 0.12, 0.03, (0, -1, 0))
    dent(body, (0.21, 0.06, 1.75), 0.1, 0.02, (-1, 0, 0))
    crumple(body, 0.0025, 12, 6)
    # 아래 경첩이 빠져 위 경첩에만 매달림: 문 아래쪽이 처지고 비틀림
    dent(door, (0.05, 0.13, 1.5), 0.16, 0.025, (0, -1, 0))
    crumple(door, 0.003, 12, 7)
    pivot = V(-0.195, 0.118, 1.835)
    xform(door, rot_about(pivot, (0, 0, 1), 1.35) @ rot_about(pivot, (0, 1, 0), 0.42))
    # 차단기 레버 몇 개 떨어져 바닥에
    deb = Debris("elec_panel_debris")
    deb.scatter(rng, ["wp_plastic", "wp_dark", "wp_red"], 7, (-0.35, 0.08), (0.35, 0.55), (0.015, 0.02, 0.01), (0.04, 0.05, 0.02))
    deb.scatter(rng, ["wp_metal"], 3, (-0.4, 0.1), (0.3, 0.5), (0.03, 0.03, 0.002), (0.08, 0.05, 0.003))
    deb.finish()
    finalize("elec_panel", body, [])

def b_filing(rng):
    o = load("filing")
    body = o["filing"]; dr = [o["filing_drawer%d" % i] for i in (1, 2, 3, 4)]
    densify(body, 0.07, 5)
    for d in dr: densify(d, 0.08, 3)
    dent(body, (0.27, 0.3, 0.95), 0.26, 0.05, (-1, 0, 0))
    dent(body, (-0.27, 0.55, 1.46), 0.22, 0.06, (0.4, -0.2, -1))
    dent(body, (-0.27, 0.15, 0.35), 0.18, 0.03, (1, 0, 0))
    crumple(body, 0.003, 8, 8)
    for i, d in enumerate(dr): crumple(d, 0.003, 10, 20 + i)
    dent(dr[2], (0.1, 0.686, 0.84), 0.12, 0.03, (0, -1, 0))
    # 맨 위 서랍: 거의 다 빠져 앞으로 처짐 / 3단: 조금 / 2단: 비뚤게 닫힘 / 1단: 바닥
    xform(dr[3], rot_about((0, 0.62, 1.016), (1, 0, 0), -0.12) @ Matrix.Translation(V(0, 0.36, 0)))
    xform(dr[2], rot_about((0, 0.62, 0.84), (0, 0, 1), 0.05) @ Matrix.Translation(V(0, 0.16, 0)))
    xform(dr[1], rot_about((0, 0.62, 0.52), (0, 0, 1), -0.025) @ Matrix.Translation(V(0, 0.03, 0)))
    c1 = sum((v.co for v in dr[0].data.vertices), Vector()) / len(dr[0].data.vertices)
    xform(dr[0], Matrix.Translation(V(-0.45, 1.05, 0)) @ Matrix.Rotation(0.55, 4, 'Z') @ Matrix.Rotation(math.pi / 2, 4, 'Y') @ Matrix.Translation(-c1))
    to_floor(dr[0])
    attached = [body] + dr[1:]
    xform(attached, rot_about((0.27, 0.3, 0), (0, 1, 0), 0.03)); to_floor(attached)
    deb = Debris("filing_debris")
    for i in range(11):
        deb.sheet("paper", 0.21, 0.297, (rng.uniform(-0.6, 0.55), rng.uniform(0.75, 1.65), 0), rng.uniform(0, math.pi), 0.002 + i * 0.0006)
    deb.box("light.004", (0.25, 0.32, 0.01), (0.35, 1.25, 0.0), (0, 0, 0.3))  # 떨어진 폴더
    deb.finish()
    finalize("filing", body, [])

def b_fire_cabinet(rng):
    o = load("fire_cabinet")
    body, door = o["fire_cabinet"], o["fire_cabinet_door"]
    glass = extract(door, "wp_glass", "fire_cabinet_glass")
    densify(body, 0.06, 4); densify(door, 0.06, 4)
    dent(body, (0.225, 0.1, 1.3), 0.15, 0.03, (-1, 0, 0))
    crumple(body, 0.0025, 10, 9)
    crumple(door, 0.003, 10, 10)
    pivot = V(-0.225, 0.172, 1.85)
    pose = rot_about(pivot, (0, 0, 1), 1.95) @ rot_about(pivot, (0, 1, 0), 0.06)
    xform(door, pose)
    glass_scatter(glass, rng, keep_ratio=0.35, floor_count=9, floor_min=(-0.55, 0.1), floor_max=(0.35, 0.75), pose=pose)
    deb = Debris("fire_cabinet_debris")
    deb.scatter(rng, ["wp_glass"], 10, (-0.6, 0.08), (0.4, 0.8), (0.008, 0.008, 0.003), (0.03, 0.02, 0.004))
    deb.finish()
    finalize("fire_cabinet", body, [])

def b_tall_cabinet(rng):
    o = load("tall_cabinet")
    body, dl, dr = o["tall_cabinet"], o["tall_cabinet_doorL"], o["tall_cabinet_doorR"]
    if body.data.materials and body.data.materials[0] is None:
        body.data.materials[0] = mat("Lit")
    elif not body.data.materials:
        body.data.materials.append(mat("Lit"))
    densify(body, 0.15, 4)
    dent(body, (0.635, 0.4, 1.4), 0.4, 0.06, (-1, 0, 0))
    dent(body, (-0.3, 0.3, 2.54), 0.35, 0.05, (0, 0, -1))
    dent(body, (-0.635, 0.6, 0.6), 0.3, 0.035, (1, 0, 0))
    crumple(body, 0.005, 4, 11)
    densify(dr, 0.15, 3)
    dent(dr, (0.3, 0.79, 1.3), 0.3, 0.04, (0, -1, 0))
    crumple(dr, 0.002, 4, 12)
    crumple(dl, 0.002, 4, 13)
    # 오른문: 휘어서 열림
    pr = V(0.584, 0.762, 2.4)
    xform(dr, rot_about(pr, (0, 0, 1), -1.15) @ rot_about(pr, (0, 1, 0), -0.035))
    attached = [body, dr]
    xform(attached, rot_about((0.635, 0.4, 0), (0, 1, 0), 0.025)); to_floor(attached)
    # 왼문: 뜯겨서 캐비닛 앞에 기대어 있음
    mn, mx = bounds(dl)
    bc = V((mn.x + mx.x) / 2, mx.y, mn.z)  # 앞면 아래 중심
    lean = 0.27
    h = mx.z - mn.z
    xform(dl, Matrix.Translation(V(-0.36, 0.80 + h * math.sin(lean) + 0.02, 0)) @ Matrix.Rotation(0.18, 4, 'Z')
          @ Matrix.Rotation(lean, 4, 'X') @ Matrix.Translation(-bc))
    to_floor(dl)
    deb = Debris("tall_cabinet_debris")
    deb.scatter(rng, ["Lit"], 6, (-0.8, 0.85), (0.9, 1.6), (0.02, 0.02, 0.004), (0.12, 0.05, 0.01))
    deb.finish()
    finalize("tall_cabinet", body, [])

def b_tea_niche(rng):
    o = load("tea_niche")
    body = o["tea_niche"]
    lL, lR, uL, uR = (o["tea_niche_" + k] for k in ("lowerL", "lowerR", "upperL", "upperR"))
    jar = extract(body, "wp_glass", "tea_niche_glass")
    densify(body, 0.08, 5)
    for p in (lL, lR, uL, uR): densify(p, 0.08, 4)
    dent(body, (0.25, 0.62, 1.3), 0.2, 0.035, (0, -0.3, -1))
    dent(body, (-0.52, 0.3, 1.9), 0.25, 0.03, (1, 0, 0))
    dent(body, (0.52, 0.5, 0.4), 0.2, 0.025, (-1, 0, 0))
    crumple(body, 0.003, 7, 14)
    for i, p in enumerate((lL, lR, uL, uR)): crumple(p, 0.003, 9, 30 + i)
    dent(uL, (-0.25, 0.37, 2.0), 0.18, 0.025, (0, -1, 0))
    # 하부 오른문: 처져 열림 / 상부 왼문: 크게 비틀려 열림 / 상부 오른문: 살짝 열림 / 하부 왼문: 떨어져 바닥에 누움
    p = V(0.5, 0.6, 0.83); xform(lR, rot_about(p, (0, 0, 1), -1.05) @ rot_about(p, (0, 1, 0), -0.1))
    p = V(-0.5, 0.37, 2.38); xform(uL, rot_about(p, (0, 0, 1), 1.85) @ rot_about(p, (0, 1, 0), 0.14))
    p = V(0.5, 0.37, 2.38); xform(uR, rot_about(p, (0, 0, 1), -0.28))
    mn, mx = bounds(lL); c = (mn + mx) / 2
    xform(lL, Matrix.Translation(V(-0.2, 1.15, 0)) @ Matrix.Rotation(-0.35, 4, 'Z') @ Matrix.Rotation(-math.pi / 2, 4, 'X') @ Matrix.Translation(-c))
    to_floor(lL)
    # 유리병: 아래 1/3 만 깨진 밑동으로 남고, 나머지는 납작한 조각으로 조리대·바닥에 흩어짐
    jmn, jmx = bounds(jar)
    cut_z = jmn.z + (jmx.z - jmn.z) * 0.33
    bm = bm_edit(jar)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-6,
                           plane_co=V(0, 0, cut_z), plane_no=V(0, 0, 1), clear_outer=True)
    for v in bm.verts:  # 깨진 테두리를 들쭉날쭉하게
        if abs(v.co.z - cut_z) < 1e-4: v.co.z -= rng.uniform(0, 0.03)
    bm_done(jar, bm)
    deb = Debris("tea_niche_debris")
    for k in range(14):
        loc = (rng.uniform(-0.45, 0.0), rng.uniform(0.08, 0.5), 1.3) if k < 6 else (rng.uniform(-0.5, 0.4), rng.uniform(0.7, 1.3), 0.0)
        deb.box("wp_glass", (rng.uniform(0.015, 0.05), rng.uniform(0.01, 0.035), 0.003), loc,
                (rng.uniform(-.05, .05), rng.uniform(-.05, .05), rng.uniform(0, math.pi)))
    deb.scatter(rng, ["wp_wood_light"], 7, (-0.6, 0.7), (0.6, 1.5), (0.015, 0.06, 0.006), (0.04, 0.22, 0.015))
    deb.scatter(rng, ["wp_white"], 4, (-0.5, 0.65), (0.5, 1.2), (0.02, 0.02, 0.004), (0.06, 0.05, 0.008))
    deb.finish()
    finalize("tea_niche", body, [])

BUILDERS = {n: globals()["b_" + n] for n in NAMES}

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    preview_dir = None; do_export = True; names = []
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--preview": preview_dir = argv[i + 1]; i += 2; continue
        if a == "--no-export": do_export = False
        else: names.append(a)
        i += 1
    names = names or NAMES
    report = {}
    for n in names:
        BUILDERS[n](random.Random(sum(map(ord, n))))
        objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        report[n] = {"objects": len(objs), "tris": sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs)}
        export(n, preview_dir, do_export)
    print("RESULT", report)

main()
