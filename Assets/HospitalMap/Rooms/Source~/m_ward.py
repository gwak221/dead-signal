"""H. 병실 문, I. 병실 — Rooms/Ward"""
import bpy, os, math
from mathutils import Vector, Matrix
from rmlib import *

HERE = os.path.dirname(os.path.abspath(__file__))
BED_FBX = os.path.normpath(os.path.join(HERE, "..", "..", "Objects", "bed.fbx"))

def _mattress():
    """기존 bed.fbx 의 매트리스(남색 비닐) 범위. 침구를 침대에 딱 맞추려고 쓴다."""
    sc = bpy.context.scene; before = set(sc.objects)
    bpy.ops.import_scene.fbx(filepath=BED_FBX)
    new = [o for o in sc.objects if o not in before]
    pts = []
    for o in new:
        if o.type != 'MESH': continue
        idx = {i for i, s in enumerate(o.material_slots) if s.material and "navy vinyl" in s.material.name.lower()}
        for p in o.data.polygons:
            if p.material_index in idx:
                pts += [o.matrix_world @ o.data.vertices[v].co for v in p.vertices]
    for o in new: bpy.data.objects.remove(o, do_unlink=True)
    if not pts: return V(-0.45, -1.0, 0.45), V(0.45, 1.0, 0.6)
    return (V(*(min(p[i] for p in pts) for i in range(3))), V(*(max(p[i] for p in pts) for i in range(3))))

@model("Ward")
def bedding(md, B, rng):
    """베개·담요·시트. 원점 = bed 프리팹 원점 (같은 위치·회전으로 놓으면 침대에 딱 맞음). 베개는 +Y 쪽."""
    mn, mx = _mattress()
    cx, cy = (mn.x + mx.x) / 2, (mn.y + mx.y) / 2
    W, L, top = mx.x - mn.x, mx.y - mn.y, mx.z
    b = md.body
    sheet = md["sheet"]
    sheet.box("white", (W + 0.02, L + 0.02, 0.012), (cx, cy, top + 0.006))
    for s in (-1, 1):
        sheet.box("white", (0.008, L + 0.02, 0.1), (cx + s * (W / 2 + 0.014), cy, top - 0.04))
    blanket = md["blanket"]
    bl = L * 0.62; by = mn.y + bl / 2 - 0.01
    blanket.box("blanket", (W + 0.08, bl, 0.03), (cx, by, top + 0.027))
    for s in (-1, 1):
        blanket.box("blanket", (0.012, bl, 0.2), (cx + s * (W / 2 + 0.045), by, top - 0.06))
    blanket.box("white", (W + 0.085, 0.12, 0.034), (cx, by + bl / 2 - 0.06, top + 0.03))  # 접힌 시트 끝
    pillow = md["pillow"]
    pillow.sphere("white", 1.0, (cx, mx.y - 0.25, top + 0.07), (W * 0.36, 0.2, 0.07), 12, 6)
    if not B:
        pillow.crumple(0.008, 6, 1)
        return
    # 망가진 버전: 구겨지고 피 묻음, 담요는 한쪽으로 끌려 바닥까지 흘러내림, 베개는 바닥에
    sheet.densify(0.12, 4); sheet.crumple(0.018, 4, 2)
    blanket.bm.clear()
    blanket.box("blanket", (W * 0.7, bl * 0.9, 0.03), (cx + W * 0.2, by - 0.05, top + 0.03), (0, 0, 0.12))
    drape = M(); drape.box("blanket", (0.03, bl * 0.8, 0.55), (cx + W / 2 + 0.06, by, top - 0.26), (0, -0.08, 0.05))
    blanket.merge(drape)
    blanket.sphere("blanket", 1.0, (cx + W / 2 + 0.32, by + 0.1, 0.06), (0.35, 0.5, 0.07), 10, 6)
    blanket.densify(0.1, 4); blanket.crumple(0.03, 3.5, 3)
    pillow.bm.clear()
    pillow.sphere("white", 1.0, (cx - W / 2 - 0.45, cy + 0.4, 0.07), (0.3, 0.2, 0.065), 12, 6, (0, 0, 0.6))
    pillow.crumple(0.02, 5, 4)
    stains = md["stain"]
    for _ in range(5):
        s = rng.uniform(0.06, 0.18)
        stains.cyl("blood", s, 0.003, (cx + rng.uniform(-W / 3, W / 4), rng.uniform(mn.y + 0.5, mx.y - 0.3), top + 0.02),
                   (0, 0, rng.uniform(0, 3)), 7)
    for v in stains.bm.verts: v.co.y += (v.co.x - cx) * 0.3
    blood(md["debris"], rng, 3, (cx - W / 2 - 0.8, cy - 0.3), (cx - W / 2 - 0.2, cy + 0.6))

@model("Ward")
def headwall_panel(md, B, rng):
    """의료가스 헤드월 패널. 벽 소품: 뒤 y=0, 중심 높이 1.45 m."""
    W, D, H, Z = 1.8, 0.07, 0.22, 1.45
    b = md.body
    b.box("white", (W, D - 0.012, H), (0, (D - 0.012) / 2, Z))
    b.box("light", (W + 0.02, D + 0.01, 0.02), (0, (D + 0.01) / 2, Z - H / 2 - 0.01))   # 아래 레일
    b.box("light", (W + 0.02, D + 0.005, 0.015), (0, (D + 0.005) / 2, Z + H / 2 + 0.0075))
    cover_y = D - 0.006
    halves = [M(), M()]
    for i, h in enumerate(halves):
        h.box("white", (W / 2 - 0.004, 0.012, H - 0.01), ((i - 0.5) * W / 2, cover_y, Z))
    fy = D + 0.001
    # 장치들 (커버 앞면)
    def outlet(m, x):
        m.box("white", (0.075, 0.012, 0.075), (x, fy + 0.004, Z - 0.02))
        for dx in (-0.015, 0.015): m.box("black", (0.006, 0.004, 0.016), (x + dx, fy + 0.011, Z - 0.02))
    def gas(m, x, col):
        m.cyl("steel", 0.03, 0.02, (x, fy + 0.008, Z + 0.03), (math.pi / 2, 0, 0), 12)
        m.cyl(col, 0.022, 0.01, (x, fy + 0.02, Z + 0.03), (math.pi / 2, 0, 0), 12)
    outlet(halves[0], -0.72); outlet(halves[0], -0.62); gas(halves[0], -0.4, "o2_green"); gas(halves[0], -0.28, "yellow")
    gas(halves[1], 0.28, "o2_green"); gas(halves[1], 0.4, "light"); outlet(halves[1], 0.62); outlet(halves[1], 0.72)
    for m_, x in ((halves[0], -0.12), (halves[1], 0.12)):
        m_.box("light", (0.06, 0.01, 0.09), (x, fy + 0.003, Z + 0.02))
        m_.cyl("red", 0.012, 0.01, (x, fy + 0.01, Z + 0.035), (math.pi / 2, 0, 0), 10)
    b.box("o2_green", (0.04, 0.003, 0.012), (-0.4, fy + 0.001, Z - 0.075))
    if not B:
        for h in halves: b.merge(h)
        return
    # 망가진 버전: 왼쪽 커버는 바닥에, 오른쪽 커버는 아래 모서리에 매달려 늘어짐, 속 배선 노출
    b.box("grime", (W - 0.04, 0.004, H - 0.03), (0, 0.04, Z))
    halves[1].xform(rot_about((0.45, cover_y, Z - H / 2 + 0.005), (1, 0, 0), 2.2) @ rot_about((0.9, cover_y, Z - H / 2), (0, 1, 0), 0.25))
    left = halves[0]
    c = V(-0.45, cover_y, Z)
    left.xform(Matrix.Translation(V(-0.3, 0.55, 0.0)) @ Matrix.Rotation(0.3, 4, 'Z') @ Matrix.Rotation(-math.pi / 2, 4, 'X') @ Matrix.Translation(-c))
    to_floor([left]); left.crumple(0.004, 6, 5)
    b.merge(halves[1]); md["debris"].merge(left)
    for i, col in enumerate(("red", "blue", "yellow", "black", "red", "white")):
        x0 = rng.uniform(-0.8, 0.2); x1 = x0 + rng.uniform(-0.15, 0.15)
        drop = rng.uniform(0.15, 0.55)
        b.poly(col, [(x0, 0.035, Z + rng.uniform(-0.05, 0.05)), (x0 + (x1 - x0) * 0.4, 0.09, Z - H / 2 - drop * 0.5),
                     (x1, 0.1, Z - H / 2 - drop)], 0.004, 5)
    b.box("white", (0.075, 0.03, 0.075), (-0.52, 0.11, Z - H / 2 - 0.42), (0.4, 0.2, 0.3))   # 뽑혀 매달린 콘센트
    b.poly("black", [(-0.6, 0.04, Z - 0.02), (-0.55, 0.09, Z - 0.25), (-0.52, 0.11, Z - H / 2 - 0.38)], 0.004, 5)

def _carcass(m, mat_, w, d, h, t=0.018, z0=0.0, shelves=()):
    m.box(mat_, (t, d, h), (-w / 2 + t / 2, 0, z0 + h / 2)); m.box(mat_, (t, d, h), (w / 2 - t / 2, 0, z0 + h / 2))
    m.box(mat_, (w - 2 * t, t, h), (0, -d / 2 + t / 2, z0 + h / 2))
    m.box(mat_, (w - 2 * t, d - t, t), (0, t / 2, z0 + 0.05 + t / 2)); m.box("dark", (w - 2 * t, 0.01, 0.05), (0, d / 2 - 0.03, z0 + 0.025))
    for z in shelves: m.box(mat_, (w - 2 * t, d - t, t), (0, t / 2, z0 + z))

def _handle(m, x, y, z, vertical=True, L=0.1):
    if vertical:
        m.box("steel", (0.012, 0.012, L), (x, y + 0.025, z))
        for dz in (-L / 2 + 0.01, L / 2 - 0.01): m.box("steel", (0.01, 0.025, 0.01), (x, y + 0.012, z + dz))
    else:
        m.box("steel", (L, 0.012, 0.012), (x, y + 0.025, z))
        for dx in (-L / 2 + 0.01, L / 2 - 0.01): m.box("steel", (0.01, 0.025, 0.01), (x + dx, y + 0.012, z))

@model("Ward")
def bedside_table(md, B, rng):
    W, D, H = 0.46, 0.45, 0.78
    b = md.body
    _carcass(b, "wood_light", W, D, H - 0.025, shelves=(0.5, 0.62))
    b.box("wood_light", (W + 0.02, D + 0.02, 0.025), (0, 0.01, H - 0.0125))
    drawer, door = md["drawer"], md["door"]
    drawer.box("wood_light", (W - 0.01, 0.02, 0.12), (0, D / 2 + 0.01, 0.69)); _handle(drawer, 0, D / 2 + 0.02, 0.69, False, 0.12)
    drawer.tub("cream", (W - 0.06, D - 0.06, 0.09), (0, 0.02, 0.635), 0.012)
    door.box("wood_light", (W - 0.01, 0.02, 0.44), (0, D / 2 + 0.01, 0.28)); _handle(door, W / 2 - 0.05, D / 2 + 0.02, 0.4, True)
    if not B: return
    b.densify(0.1, 3); b.dent((0.1, 0.1, H), 0.15, 0.015, (0, 0, -1)); b.crumple(0.002, 8, 6)
    # 서랍: 빠져서 앞 바닥에 뒤집혀 있음 / 문: 경첩 하나만 남아 처짐
    c = V(0, 0.25, 0.65)
    drawer.xform(Matrix.Translation(V(0.15, 0.62, 0.0)) @ Matrix.Rotation(0.5, 4, 'Z') @ Matrix.Rotation(2.6, 4, 'X') @ Matrix.Translation(-c))
    to_floor([drawer])
    p = V(-W / 2 + 0.005, D / 2 + 0.02, 0.28)
    door.xform(rot_about(p, (0, 0, 1), 1.9) @ rot_about((p.x, p.y, 0.5), (0, 1, 0), -0.12))
    md["debris"].merge(drawer); md.parts.pop("drawer")
    sheets(md["debris"], rng, 4, (-0.3, 0.4), (0.5, 0.9))
    md["debris"].cyl("white", 0.035, 0.09, (-0.25, 0.65, 0.035), (math.pi / 2, 0, 0.7), 10)   # 넘어진 컵

@model("Ward")
def wardrobe(md, B, rng):
    W, D, H = 0.6, 0.6, 2.1
    b = md.body
    _carcass(b, "wood_light", W, D, H - 0.02, shelves=(0.38, H - 0.04))
    b.box("wood_light", (W + 0.01, D + 0.01, 0.02), (0, 0, H - 0.01))
    b.rod("steel", (-W / 2 + 0.02, 0, 1.85), (W / 2 - 0.02, 0, 1.85), 0.012, 8)
    door, drawer = md["door"], md["drawer"]
    door.box("wood_light", (W - 0.01, 0.022, H - 0.42), (0, D / 2 + 0.011, 0.4 + (H - 0.42) / 2))
    _handle(door, W / 2 - 0.05, D / 2 + 0.022, 1.1, True, 0.3)
    drawer.box("wood_light", (W - 0.01, 0.022, 0.3), (0, D / 2 + 0.011, 0.215)); _handle(drawer, 0, D / 2 + 0.022, 0.25, False, 0.2)
    drawer.tub("cream", (W - 0.07, D - 0.08, 0.2), (0, 0.02, 0.09), 0.012)
    gown = md["gown"]
    for i, x in enumerate((-0.12, 0.08)):
        gown.rod("steel", (x, 0, 1.86), (x, 0, 1.8), 0.004, 4)
        gown.box("gown", (0.4, 0.04, 0.85), (x, 0.0, 1.38), (0, 0, math.pi / 2 + 0.1 * i))
    gown.densify(0.15, 3); gown.crumple(0.012, 5, 7)
    if not B: return
    b.densify(0.2, 3); b.crumple(0.004, 3, 8); b.dent((W / 2, 0.1, 1.3), 0.3, 0.03, (-1, 0, 0))
    p = V(-W / 2 + 0.005, D / 2 + 0.02, 1.2)
    door.xform(rot_about(p, (0, 0, 1), 2.05) @ rot_about((p.x, p.y, 2.0), (0, 1, 0), -0.08))
    c = V(0, 0.32, 0.2)
    drawer.xform(Matrix.Translation(V(0.1, 0.75, 0)) @ Matrix.Rotation(-0.35, 4, 'Z') @ Matrix.Translation(-c)); to_floor([drawer])
    drawer.xform(rot_about((0.4, 0.6, 0), (0, 1, 0), 0.25)); to_floor([drawer])
    gown.bm.clear()
    gown.rod("steel", (0.08, 0, 1.86), (0.08, 0, 1.8), 0.004, 4)
    gown.box("gown", (0.4, 0.04, 0.85), (0.08, 0.0, 1.38), (0.1, 0.2, math.pi / 2 + 0.3))
    gown.sphere("gown", 1.0, (-0.25, 0.9, 0.05), (0.35, 0.25, 0.05), 10, 6)
    gown.densify(0.15, 3); gown.crumple(0.02, 4, 9)
    md.parts["cloth"] = md.parts.pop("gown")
    md["debris"].merge(drawer); md.parts.pop("drawer")

@model("Ward")
def wall_light(md, B, rng):
    """벽 간접 조명. 벽 소품: 뒤 y=0, 높이 1.95 m."""
    W, Z = 0.9, 1.95
    lamp = md["lamp"]
    lamp.box("cream", (W, 0.1, 0.07), (0, 0.06, Z))
    lamp.box("cream", (W, 0.02, 0.1), (0, 0.11, Z + 0.015))
    lamp.box("cream", (W - 0.04, 0.02, 0.03), (0, 0.005, Z))
    if not B:
        lamp.box("lamp", (W - 0.04, 0.08, 0.004), (0, 0.06, Z - 0.036))
        md.body.merge(lamp); md.parts.pop("lamp"); return
    lamp.box("grime", (W - 0.04, 0.08, 0.004), (0, 0.06, Z - 0.036))
    lamp.densify(0.12, 3); lamp.crumple(0.004, 6, 10)
    lamp.xform(rot_about((W / 2 - 0.02, 0.03, Z), (0, 1, 0), -0.55) @ rot_about((W / 2, 0.0, Z), (0, 0, 1), -0.12))
    b = md.body
    b.box("cream", (0.06, 0.01, 0.04), (W / 2 - 0.03, 0.005, Z))  # 남은 브래킷
    b.box("grime", (0.04, 0.002, 0.06), (-W / 2 + 0.05, 0.001, Z))  # 뜯긴 자리
    b.merge(lamp); md.parts.pop("lamp")
    b.poly("black", [(-W / 2 + 0.05, 0.01, Z), (-W / 2 + 0.12, 0.05, Z - 0.2), (-W / 2 + 0.18, 0.07, Z - 0.33)], 0.004, 5)
    b.poly("red", [(-W / 2 + 0.06, 0.01, Z), (-W / 2 + 0.1, 0.06, Z - 0.25), (-W / 2 + 0.2, 0.08, Z - 0.36)], 0.004, 5)
    shards(md["glass"], rng, 14, (-0.5, 0.05), (0.4, 0.6), mat_="frost", smin=0.02, smax=0.06)

@model("Ward")
def iv_pole(md, B, rng):
    b = md.body
    b.star_base(0.11, 0.3, 0.032, "steel")
    b.cyl("steel", 0.03, 0.06, (0, 0, 0.13))
    b.rod("steel", (0, 0, 0.12), (0, 0, 2.0), 0.013, 8)
    b.cyl("black", 0.14, 0.03, (0, 0, 1.12), seg=16); b.cyl("black", 0.03, 0.06, (0, 0, 1.12))   # 둥근 받침
    for a in (0, math.pi / 2):
        d = V(math.cos(a), math.sin(a), 0) * 0.16
        b.rod("steel", -d + V(0, 0, 2.0), d + V(0, 0, 2.0), 0.006, 6)
        for s in (-1, 1):
            e = d * s + V(0, 0, 2.0)
            b.poly("steel", [e, e + V(0, 0, 0.03), e + d.normalized() * s * 0.02 + V(0, 0, 0.05)], 0.004, 4)
    bag = md["bag"]
    bag.box("glass", (0.12, 0.03, 0.18), (0.16, 0, 1.86)); bag.box("white", (0.08, 0.031, 0.05), (0.16, 0, 1.87))
    bag.rod("glass", (0.16, 0, 1.77), (0.18, 0.02, 1.2), 0.003, 4)
    if not B: return
    # 쓰러짐: 바닥에 누운 폴대, 받침은 떨어져 굴러감, 수액 팩 터짐
    ms = [b, bag]
    xform_all(ms, rot_about((0, 0.3, 0), (1, 0, 0), 1.45)); to_floor(ms)
    xform_all(ms, Matrix.Rotation(0.4, 4, 'Z'))
    bag.bm.clear()
    bag.box("glass", (0.13, 0.18, 0.012), (-0.5, 1.75, 0.006), (0, 0, 0.3))
    bag.cyl("glass", 0.3, 0.002, (-0.45, 1.8, 0.001), seg=9)   # 쏟아진 수액
    md["debris"].cyl("black", 0.14, 0.03, (0.5, 0.9, 0.015), seg=16)
    md["debris"].merge(bag); md.parts.pop("bag")

@model("Ward")
def ward_door(md, B, rng):
    """병실 문 (문틀 + 문짝). 원점 = 문 아래 중앙, 벽면 y=0, 문은 +Y 쪽으로 열림.
    깨끗한 버전: 문짝(_leaf)에 door_open_angle → HingedDoor, 프리팹에 DoorPair."""
    OW, OH, FD, FT = 1.0, 2.1, 0.2, 0.05
    b = md.body
    for s in (-1, 1): b.box("light", (FT, FD, OH + FT), (s * (OW / 2 + FT / 2), 0, (OH + FT) / 2))
    b.box("light", (OW + 2 * FT, FD, FT), (0, 0, OH + FT / 2))
    for s in (-1, 1): b.box("light", (0.015, 0.03, OH), (s * (OW / 2 - 0.0075), -0.02, OH / 2))   # 문 멈춤턱
    b.box("light", (OW, 0.03, 0.015), (0, -0.02, OH - 0.0075))
    LW, LT, LH = OW - 0.01, 0.045, OH - 0.015
    lx0 = -OW / 2 + 0.005; ly = 0.0
    # 문짝: 경첩 오른쪽 (x=+0.495), 세로 유리창은 손잡이 쪽 위
    wx0, wx1, wz0, wz1 = lx0 + 0.18, lx0 + 0.31, 1.15, 1.85
    leaf = md["leaf"]
    def L(x0, x1, z0, z1): leaf.box("white", (x1 - x0, LT, z1 - z0), ((x0 + x1) / 2, ly, (z0 + z1) / 2))
    L(lx0, wx0, 0.008, LH); L(wx1, lx0 + LW, 0.008, LH); L(wx0, wx1, 0.008, wz0); L(wx0, wx1, wz1, LH)
    for s in (-1, 1):
        leaf.box("light", (wx1 - wx0 + 0.03, 0.006, 0.015), ((wx0 + wx1) / 2, s * (LT / 2 + 0.003), wz0 - 0.0075))
        leaf.box("light", (wx1 - wx0 + 0.03, 0.006, 0.015), ((wx0 + wx1) / 2, s * (LT / 2 + 0.003), wz1 + 0.0075))
        for x in (wx0 - 0.0075, wx1 + 0.0075): leaf.box("light", (0.015, 0.006, wz1 - wz0), (x, s * (LT / 2 + 0.003), (wz0 + wz1) / 2))
        # 세로 막대 손잡이
        hx = lx0 + 0.07
        leaf.box("steel", (0.022, 0.022, 0.35), (hx, s * (LT / 2 + 0.05), 1.0))
        for dz in (-0.15, 0.15): leaf.box("steel", (0.016, 0.05, 0.016), (hx, s * (LT / 2 + 0.025), 1.0 + dz))
        leaf.box("steel", (LW - 0.1, 0.003, 0.2), (lx0 + LW / 2, s * (LT / 2 + 0.0015), 0.12))   # 발판 보호판
    glass = md["glass"]
    hinge = (OW / 2 - 0.005, ly + LT / 2, 0.0)
    for z in (0.25, 1.05, 1.85): b.box("steel", (0.012, 0.03, 0.1), (OW / 2 - 0.002, LT / 2, z))
    if not B:
        leaf.box("glass", (wx1 - wx0, 0.008, wz1 - wz0), ((wx0 + wx1) / 2, ly, (wz0 + wz1) / 2))
        # 피벗만 경첩으로 옮기고 닫힌 자세로 내보낸다 (Unity 가 FBX 의 오브젝트 회전을 버리므로
        # 열린 각도는 RoomsPrefabBuilder 가 door_open_angle 크기로 +Z(앞) 쪽에 넣는다).
        md.leaf_pose["leaf"] = (hinge, (0.0, 0.0, 0.0))
        md.props["leaf"] = {"door_open_angle": 86.0}
        return
    # 망가진 버전: 위 경첩이 빠져 문짝이 비스듬히 열린 채 처짐, 유리 깨짐, 할퀸 자국·핏자국
    for i in range(3):   # 남은 유리 조각 (창틀 아래쪽)
        glass.box("glass", (rng.uniform(0.03, 0.08), 0.008, rng.uniform(0.04, 0.12)),
                  (rng.uniform(wx0 + 0.03, wx1 - 0.03), ly, wz0 + 0.05), (0, rng.uniform(-0.4, 0.4), 0))
    for k in range(3):
        cx, cz = lx0 + 0.45 + k * 0.03, 1.2
        for j in range(4):
            leaf.box("grime", (0.008, 0.004, 0.42), (cx + j * 0.035, LT / 2 + 0.002, cz - j * 0.02), (0, 0.35, 0))
    leaf.cyl("blood", 0.11, 0.003, (lx0 + 0.15, LT / 2 + 0.003, 1.05), (math.pi / 2, 0, 0), 7)
    leaf.box("blood", (0.05, 0.003, 0.5), (lx0 + 0.17, LT / 2 + 0.003, 0.8), (0, 0.1, 0))
    leaf.densify(0.15, 3); leaf.crumple(0.003, 4, 11)
    T = rot_about(hinge, (0, 0, 1), -0.45) @ rot_about((hinge[0], hinge[1], 0.0), (0, 1, 0), -0.035)
    leaf.xform(T); glass.xform(T)
    md.parts["leafbroken"] = md.parts.pop("leaf")
    shards(glass, rng, 16, (-0.6, -0.6), (0.3, 0.9))

@model("Ward")
def door_plate(md, B, rng):
    """문 옆 명패 (높이 1.55) + 카드리더 (높이 1.15). 벽 소품: 뒤 y=0, x=0 이 명패 중심."""
    b = md.body
    plate = md["plate"]
    plate.box("white", (0.28, 0.012, 0.09), (0, 0.006, 1.55))
    plate.box("blue", (0.28, 0.002, 0.018), (0, 0.013, 1.585))
    for i, w in enumerate((0.16, 0.11, 0.13)): plate.box("dark", (w, 0.002, 0.008), (-0.12 + w / 2, 0.013, 1.56 - i * 0.017))
    reader = md["reader"]
    reader.box("dark", (0.07, 0.022, 0.11), (0, 0.011, 1.15))
    reader.box("black", (0.05, 0.004, 0.05), (0, 0.023, 1.16)); reader.box("o2_green", (0.008, 0.004, 0.008), (0, 0.023, 1.115))
    if not B:
        b.merge(plate); b.merge(reader); md.parts.pop("plate"); md.parts.pop("reader"); return
    plate.xform(rot_about((-0.13, 0.006, 1.59), (0, 1, 0), -0.5))
    b.merge(plate); md.parts.pop("plate")
    b.box("o2_green", (0.055, 0.004, 0.09), (0, 0.004, 1.15)); b.box("black", (0.015, 0.006, 0.02), (0.01, 0.008, 1.16))
    for i, col in enumerate(("red", "black", "yellow")):
        b.poly(col, [(0.0 + i * 0.01, 0.01, 1.11), (0.01 + i * 0.012, 0.03, 1.0), (0.02 + i * 0.02, 0.04, 0.9 - i * 0.04)], 0.0025, 4)
    reader.xform(Matrix.Translation(V(0.15, 0.35, -1.15 + 0.0)) @ rot_about((0, 0.011, 1.15), (1, 0, 0), -math.pi / 2))
    to_floor([reader])
    md["debris"].merge(reader); md.parts.pop("reader")
    scatter(md["debris"], rng, ["white"], 3, (-0.2, 0.1), (0.2, 0.4), (0.02, 0.02, 0.006), (0.06, 0.04, 0.012))

@model("Ward", broken=False)
def chart_clipboard(md, B, rng):
    """병상 차트 클립보드. 원점 = 집게 위 고리 (발판 차트꽂이에 걸기)."""
    b = md.body
    b.box("dark", (0.23, 0.008, 0.32), (0, 0, -0.17))
    b.box("steel", (0.1, 0.02, 0.035), (0, 0.008, -0.025))
    b.cyl("steel", 0.012, 0.004, (0, 0, -0.005), (math.pi / 2, 0, 0), 8)
    b.box("paper", (0.21, 0.003, 0.28), (0, 0.006, -0.18))
    for i in range(8): b.box("grey", (rng.uniform(0.08, 0.17), 0.001, 0.006), (-0.09 + 0.05, 0.008, -0.08 - i * 0.025))
