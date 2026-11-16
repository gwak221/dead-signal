"""Rooms 소품 빌더 공용 라이브러리 (Blender 5.2, 헤드리스).

좌표 (Blender): Z 위, +Y 앞 (Unity 에서 +Z 앞), 단위 m.
  - 바닥 소품: 바닥 z=0, 발자국 중심이 원점.
  - 벽 소품: 뒤판 y=0, z 는 바닥에서 실제 설치 높이 (바닥에 놓고 벽에 붙이면 끝).
  - 천장 소품: 윗면 z=0, 아래로 매달림 (천장에 붙이면 끝).
내보내기 계층: 본체 <파일이름> 이 루트, 나머지는 자식.
  이름이 _debris / _glass / _cloth 로 끝나는 자식은 Unity 에서 콜라이더를 붙이지 않는다 (RoomsPrefabBuilder).
  _leaf 자식은 문짝 (커스텀 속성 door_open_angle → HingedDoor 자동 부착).
"""
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, Euler, noise

def V(*a): return Vector(a)

# ───────────────────────── 재질 팔레트 ─────────────────────────
# 기존 HospitalMap 모델과 같은 계열 색 (white/light/metal/steel/dark …). 텍스처 없이 단색.
PALETTE = {
    "white":      (0.94, 0.92, 0.85), "light":   (0.76, 0.77, 0.73), "grey":    (0.55, 0.57, 0.57),
    "metal":      (0.34, 0.38, 0.40), "steel":   (0.59, 0.62, 0.62), "dark":    (0.15, 0.18, 0.20),
    "black":      (0.08, 0.10, 0.11), "rubber":  (0.14, 0.15, 0.16), "glass":   (0.38, 0.49, 0.51),
    "paper":      (0.85, 0.82, 0.74), "brown":   (0.53, 0.40, 0.27), "yellow":  (0.85, 0.67, 0.20),
    "wood_light": (0.62, 0.47, 0.30), "wood_dark": (0.13, 0.08, 0.05), "orange": (0.90, 0.42, 0.10),
    "red":        (0.62, 0.10, 0.08), "blue":    (0.24, 0.41, 0.52), "navy":    (0.10, 0.16, 0.30),
    "green":      (0.20, 0.50, 0.30), "pink":    (0.80, 0.35, 0.45), "purple":  (0.35, 0.30, 0.55),
    "cream":      (0.82, 0.78, 0.66), "mattress": (0.86, 0.72, 0.36), "curtain": (0.80, 0.74, 0.58),
    "vinyl_blue": (0.14, 0.30, 0.55), "leather": (0.06, 0.06, 0.07), "screen":  (0.10, 0.22, 0.35),
    "lamp":       (1.00, 0.85, 0.55), "rust":    (0.30, 0.13, 0.06), "blood":   (0.25, 0.02, 0.02),
    "frost":      (0.80, 0.88, 0.92), "foam":    (0.92, 0.85, 0.55), "cardboard": (0.66, 0.52, 0.36),
    "gold":       (0.75, 0.58, 0.20), "grime":   (0.04, 0.04, 0.05), "orange_bag": (0.95, 0.45, 0.12),
    "blanket":    (0.40, 0.52, 0.66), "gown":    (0.45, 0.62, 0.78), "o2_green": (0.15, 0.55, 0.30),
}

def mat(n):
    name = "rm_" + n
    m = bpy.data.materials.get(name)
    if m is None:
        c = PALETTE[n]
        m = bpy.data.materials.new(name)
        m.diffuse_color = (*c, 1.0)          # 미리보기(Workbench)용
        m.use_nodes = True
        b = m.node_tree.nodes.get("Principled BSDF")
        b.inputs["Base Color"].default_value = (*c, 1.0)
        b.inputs["Roughness"].default_value = 0.35 if n in ("steel", "glass", "screen", "gold") else 0.75
        if n in ("steel", "gold"): b.inputs["Metallic"].default_value = 0.8
        if n == "lamp":
            b.inputs["Emission Color"].default_value = (*c, 1.0)
            b.inputs["Emission Strength"].default_value = 1.0
    return m

# ───────────────────────── 메시 빌더 ─────────────────────────

def _rotm(rot):
    return Euler(rot).to_matrix().to_4x4() if not isinstance(rot, Matrix) else rot

class M:
    """bmesh 하나 + 재질 목록. 프리미티브를 계속 붙여 한 메시로 만든다."""
    def __init__(self):
        self.bm = bmesh.new(); self.mats = []

    def _mi(self, m):
        if m not in self.mats: self.mats.append(m)
        return self.mats.index(m)

    def _fin(self, verts, m):
        i = self._mi(m)
        for f in {f for v in verts for f in v.link_faces}: f.material_index = i
        return verts

    def box(self, m, size, loc, rot=(0, 0, 0)):
        T = Matrix.Translation(V(*loc)) @ _rotm(rot) @ Matrix.Diagonal((*size, 1))
        return self._fin(bmesh.ops.create_cube(self.bm, size=1.0, matrix=T)["verts"], m)

    def cyl(self, m, r, h, loc, rot=(0, 0, 0), seg=12, r2=None, caps=True):
        """loc = 원기둥 중심. 기본 축은 Z."""
        T = Matrix.Translation(V(*loc)) @ _rotm(rot)
        return self._fin(bmesh.ops.create_cone(self.bm, cap_ends=caps, cap_tris=False, segments=seg,
                                               radius1=r, radius2=r if r2 is None else r2, depth=h, matrix=T)["verts"], m)

    def rod(self, m, a, b, r, seg=6):
        a, b = V(*a), V(*b); d = b - a
        q = d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
        return self.cyl(m, r, d.length, (a + b) / 2, q, seg)

    def poly(self, m, pts, r, seg=6, closed=False):
        n = len(pts)
        for i in range(n if closed else n - 1):
            self.rod(m, pts[i], pts[(i + 1) % n], r, seg)

    def sphere(self, m, r, loc, scale=(1, 1, 1), seg=10, rings=6, rot=(0, 0, 0)):
        T = Matrix.Translation(V(*loc)) @ _rotm(rot) @ Matrix.Diagonal((*scale, 1))
        return self._fin(bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=r, matrix=T)["verts"], m)

    def plate(self, m, size, loc, rot=(0, 0, 0), t=0.004):
        """얇은 판 (종이·포스터·라벨). size=(가로, 세로), 기본은 XZ 평면(벽에 붙는 방향), 두께 t 는 Y."""
        return self.box(m, (size[0], t, size[1]), loc, rot)

    def tub(self, m, size, loc, wall=0.02, top=False, inner=None, rot=(0, 0, 0)):
        """속이 빈 상자 (위가 열림). loc = 바닥 중심."""
        sx, sy, sz = size; R = _rotm(rot); L = V(*loc)
        def b(mm, s, p): self.box(mm, s, L + R.to_3x3() @ V(*p), rot)
        inn = inner or m
        b(m, (sx, sy, wall), (0, 0, wall / 2))
        b(m, (wall, sy, sz), (-sx / 2 + wall / 2, 0, sz / 2)); b(m, (wall, sy, sz), (sx / 2 - wall / 2, 0, sz / 2))
        b(m, (sx - 2 * wall, wall, sz), (0, -sy / 2 + wall / 2, sz / 2)); b(m, (sx - 2 * wall, wall, sz), (0, sy / 2 - wall / 2, sz / 2))
        if inn is not m: b(inn, (sx - 2 * wall, sy - 2 * wall, 0.002), (0, 0, wall + 0.001))
        if top: b(m, (sx, sy, wall), (0, 0, sz - wall / 2))

    def caster(self, loc, r=0.04, housing="steel"):
        x, y, z = loc
        self.cyl(housing, 0.008, 0.03, (x, y, 2 * r + 0.015))
        self.box(housing, (0.035, 0.03, 0.03), (x, y, 2 * r))
        self.cyl("rubber", r, 0.022, (x, y, r), (0, math.pi / 2, 0), 10)

    def star_base(self, cz, arm, r_caster=0.03, mat_="black", n=5):
        for i in range(n):
            a = 2 * math.pi * i / n + math.pi / 2
            e = V(math.cos(a) * arm, math.sin(a) * arm, cz)
            self.rod(mat_, (0, 0, cz + 0.02), e, 0.018, 5)
            self.caster((e.x, e.y, 0), r_caster, mat_)

    def pleat(self, m, x0, x1, ztop, zbot, folds, amp, y=0.0, t=0.006, jag=None, rng=None):
        """주름 커튼 (X 방향으로 지그재그). jag = 아래 가장자리를 들쭉날쭉하게 할 최대 높이."""
        bm = self.bm; n = folds * 2
        cols = []
        for i in range(n + 1):
            x = x0 + (x1 - x0) * i / n; yy = y + (amp if i % 2 else -amp)
            zb = zbot + (rng.uniform(0, jag) if jag else 0)
            cols.append((bm.verts.new((x, yy, ztop)), bm.verts.new((x, yy, zb))))
        faces = []
        for i in range(n):
            faces.append(bm.faces.new((cols[i][0], cols[i][1], cols[i + 1][1], cols[i + 1][0])))
        r = bmesh.ops.solidify(bm, geom=faces, thickness=t)
        vs = list({v for f in faces for v in f.verts}) + [e for e in r["geom"] if isinstance(e, bmesh.types.BMVert)]
        return self._fin(vs, m)

    # ── 변형 ──
    def verts(self): return self.bm.verts[:]

    def xform(self, T, verts=None):
        bmesh.ops.transform(self.bm, matrix=T, verts=verts if verts is not None else self.bm.verts[:])

    def bounds(self):
        if not self.bm.verts: return V(0, 0, 0), V(0, 0, 0)
        cs = [v.co for v in self.bm.verts]
        return V(*(min(c[i] for c in cs) for i in range(3))), V(*(max(c[i] for c in cs) for i in range(3)))

    def densify(self, max_len=0.08, passes=4):
        for _ in range(passes):
            long = [e for e in self.bm.edges if e.calc_length() > max_len]
            if not long: break
            bmesh.ops.subdivide_edges(self.bm, edges=long, cuts=1, use_grid_fill=True)

    def dent(self, center, radius, depth, direction):
        c = V(*center); d = V(*direction).normalized() * depth
        for v in self.bm.verts:
            r = (v.co - c).length
            if r < radius:
                f = 1.0 - (r / radius) ** 2
                v.co += d * (f * f)

    def crumple(self, amp, scale, seed):
        off = V(seed * 17.3, seed * 5.1, seed * 9.7)
        for v in self.bm.verts:
            v.co += noise.noise_vector(v.co * scale + off) * amp

    def merge(self, other):
        """other 의 지오메트리를 이 메시에 붙인다 (재질 인덱스 다시 매핑)."""
        me = bpy.data.meshes.new("_tmp")
        remap = [self._mi(mm) for mm in other.mats]
        cp = other.bm.copy()
        for f in cp.faces: f.material_index = remap[f.material_index] if remap else 0
        cp.to_mesh(me); cp.free()
        self.bm.from_mesh(me)
        bpy.data.meshes.remove(me)
        other.merged = True   # build_objects 가 다시 합치지 않도록

def rot_about(p, axis, ang):
    p = V(*p)
    return Matrix.Translation(p) @ Matrix.Rotation(ang, 4, V(*axis).normalized()) @ Matrix.Translation(-p)

def group_bounds(ms):
    bs = [m.bounds() for m in ms if m.bm.verts]
    return V(*(min(b[0][i] for b in bs) for i in range(3))), V(*(max(b[1][i] for b in bs) for i in range(3)))

def xform_all(ms, T):
    for m in ms: m.xform(T)

def to_floor(ms, z=0.0):
    mn, _ = group_bounds(ms)
    xform_all(ms, Matrix.Translation(V(0, 0, z - mn.z)))

def topple(ms, edge_point, axis, ang):
    """ms 를 바닥의 한 모서리(edge_point, axis)를 축으로 쓰러뜨리고 바닥에 다시 붙인다."""
    xform_all(ms, rot_about(edge_point, axis, ang)); to_floor(ms)

# ───────────────────────── 파편 ─────────────────────────

def scatter(m, rng, mats, n, lo, hi, smin, smax, z=0.0):
    """바닥(z)에 작은 상자 조각을 흩뿌린다. lo/hi = (x, y) 범위."""
    for _ in range(n):
        s = [rng.uniform(smin[i], smax[i]) for i in range(3)]
        m.box(rng.choice(mats), s, (rng.uniform(lo[0], hi[0]), rng.uniform(lo[1], hi[1]), z + s[2] / 2),
              (rng.uniform(-.08, .08), rng.uniform(-.08, .08), rng.uniform(0, math.pi)))

def sheets(m, rng, n, lo, hi, z=0.0, mat_="paper", w=0.21, h=0.297):
    for i in range(n):
        m.box(mat_, (w, h, 0.002), (rng.uniform(lo[0], hi[0]), rng.uniform(lo[1], hi[1]), z + 0.002 + i * 0.0006),
              (rng.uniform(-.04, .04), rng.uniform(-.04, .04), rng.uniform(0, math.pi)))

def shards(m, rng, n, lo, hi, z=0.0, mat_="glass", smin=0.015, smax=0.07):
    for _ in range(n):
        a, b = rng.uniform(smin, smax), rng.uniform(smin, smax) * 0.7
        m.box(mat_, (a, b, 0.004), (rng.uniform(lo[0], hi[0]), rng.uniform(lo[1], hi[1]), z + 0.002),
              (rng.uniform(-.05, .05), rng.uniform(-.05, .05), rng.uniform(0, math.pi)))

def pills(m, rng, n, lo, hi, z=0.0, mats=("white", "yellow", "white", "pink")):
    for _ in range(n):
        m.cyl(rng.choice(mats), 0.005, 0.004, (rng.uniform(lo[0], hi[0]), rng.uniform(lo[1], hi[1]), z + 0.002), seg=6)

def blood(m, rng, n, lo, hi, z=0.0, smin=0.05, smax=0.25):
    """바닥 핏자국 (아주 얇은 판 여러 장)."""
    for _ in range(n):
        s = rng.uniform(smin, smax)
        m.cyl("blood", s, 0.002, (rng.uniform(lo[0], hi[0]), rng.uniform(lo[1], hi[1]), z + 0.001),
              (0, 0, rng.uniform(0, math.pi)), 7)

# ───────────────────────── 등록 ─────────────────────────

REG = {}   # 이름 → (공간 폴더, 함수, 망가진 버전 있음)

def model(room, broken=True):
    """@model("Ward") def iv_pole(md, B, rng): ...   B = 망가진 버전이면 True."""
    def deco(fn):
        REG[fn.__name__] = (room, fn, broken); return fn
    return deco

# ───────────────────────── 모델 ─────────────────────────

class Model:
    """parts: 키 → M. 'body' 와 _KEEP 가 아닌 파츠는 내보낼 때 body 로 합쳐진다."""
    KEEP = ("debris", "glass", "cloth", "leaf")

    def __init__(self, fname):
        self.fname = fname; self.parts = {}; self.props = {}; self.leaf_pose = {}

    def __getitem__(self, k):
        if k not in self.parts: self.parts[k] = M()
        return self.parts[k]

    @property
    def body(self): return self["body"]

    def all(self): return list(self.parts.values())

    def build_objects(self):
        body = self.body
        for k, p in list(self.parts.items()):
            if k == "body" or k.split(".")[0] in self.KEEP or getattr(p, "merged", False): continue
            body.merge(p)
        objs = []
        for k, p in self.parts.items():
            if k != "body" and (k.split(".")[0] not in self.KEEP or getattr(p, "merged", False)): continue
            if not p.bm.faces: continue
            name = self.fname if k == "body" else f"{self.fname}_{k.split('.')[0]}"
            bmesh.ops.triangulate(p.bm, faces=p.bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
            bmesh.ops.remove_doubles(p.bm, verts=p.bm.verts[:], dist=0.0002)
            me = bpy.data.meshes.new(name); p.bm.normal_update(); p.bm.to_mesh(me)
            for mm in p.mats: me.materials.append(mat(mm))
            me.shade_smooth(); me.set_sharp_from_angle(angle=math.radians(35))
            o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
            if k in self.leaf_pose:
                pivot, rot = self.leaf_pose[k]
                me.transform(Matrix.Translation(-V(*pivot)))
                o.location = pivot
                o.rotation_euler = rot
                for pk, pv in self.props.get(k, {}).items(): o[pk] = pv
            objs.append((k, o))
        root = next(o for k, o in objs if k == "body")
        for k, o in objs:
            if o is not root:
                o.parent = root; o.matrix_parent_inverse = Matrix.Identity(4)
        return [o for _, o in objs]

def export_fbx(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=False, object_types={'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                             mesh_smooth_type='OFF', use_mesh_modifiers=True, use_custom_props=True,
                             add_leaf_bones=False, bake_anim=False, path_mode='STRIP', use_tspace=False)

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
