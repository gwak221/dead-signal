"""B. 약제실, C. 약품 카트 — Rooms/Pharmacy"""
import math
from mathutils import Vector, Matrix
from rmlib import *
from m_common import bottle, bottle_row, box_row, CAPS, BOX_COLS

def _fall_bottles(d, rng, n, lo, hi):
    for _ in range(n):
        bottle(d, rng.uniform(lo[0], hi[0]), rng.uniform(lo[1], hi[1]), 0, rng.uniform(0.022, 0.04), rng.uniform(0.06, 0.14),
               rng.choice(CAPS), lying=math.pi / 2, yaw=rng.uniform(0, 6.28))

def _fall_boxes(d, rng, n, lo, hi):
    for i in range(n):
        w, h, dd = rng.uniform(0.05, 0.1), rng.uniform(0.04, 0.07), rng.uniform(0.08, 0.15)
        bx = M(); bx.box(rng.choice(BOX_COLS), (w, dd, h), (0, 0, h / 2)); bx.crumple(0.004, 25, i)
        bx.xform(Matrix.Translation(V(rng.uniform(lo[0], hi[0]), rng.uniform(lo[1], hi[1]), 0)) @ Matrix.Rotation(rng.uniform(0, 6), 4, 'Z'))
        d.merge(bx)

@model("Pharmacy")
def pharmacy_shelf(md, B, rng):
    """개방형 약품 선반장 (흰 격자 + 하단 나무 수납장, 라벨 레일). 1.2 × 0.38 × 2.1 m, 뒤 y=-0.19."""
    W, D, H, LOW = 1.2, 0.38, 2.1, 0.72
    b = md.body
    cab = md["cab"]
    cab.box("wood_light", (W, D, LOW - 0.06), (0, 0, 0.06 + (LOW - 0.06) / 2)); cab.box("dark", (W - 0.04, D - 0.04, 0.06), (0, -0.01, 0.03))
    doors = md["doors"]
    for i in range(3):
        x = -W / 2 + W / 6 + i * W / 3
        doors.box("wood_light", (W / 3 - 0.008, 0.02, LOW - 0.08), (x, D / 2 + 0.01, 0.06 + (LOW - 0.06) / 2))
        doors.box("steel", (0.012, 0.015, 0.1), (x + W / 6 - 0.04, D / 2 + 0.027, LOW - 0.12))
    frame = md["frame"]
    t = 0.022
    for x in (-W / 2 + t / 2, -W / 6, W / 6, W / 2 - t / 2): frame.box("white", (t, D - 0.04, H - LOW), (x, -0.02, LOW + (H - LOW) / 2))
    frame.box("white", (W, 0.012, H - LOW), (0, -D / 2 + 0.006, LOW + (H - LOW) / 2))
    shelf_z = [LOW + 0.01 + i * 0.27 for i in range(6)]
    shelves = []
    for i, z in enumerate(shelf_z):
        s = M()
        s.box("white", (W, D - 0.04, 0.02), (0, -0.02, z))
        s.box("blue", (W, 0.006, 0.035), (0, D / 2 - 0.04, z + 0.005))
        for k in range(6): s.box("white", (0.05, 0.002, 0.02), (-W / 2 + 0.1 + k * 0.2, D / 2 - 0.036, z + 0.005))
        shelves.append(s)
    goods = []
    for i, z in enumerate(shelf_z[:-1]):
        g = M()
        for c in range(3):
            x0 = -W / 2 + 0.03 + c * W / 3; x1 = x0 + W / 3 - 0.06
            (bottle_row if (i + c) % 2 else box_row)(g, rng, x0, x1, 0.02, z + 0.01, 0.16, 0.2)
        goods.append(g)
    if not B:
        for p in [cab, doors, frame] + shelves + goods: b.merge(p)
        return
    # 망가진 버전: 선반 2개가 한쪽이 빠져 기울고, 약이 바닥에 쏟아짐, 수납장 문 하나 떨어지고 하나 열림
    for p in [cab, frame]: p.densify(0.2, 2); p.crumple(0.004, 3, 1)
    for idx, side in ((2, 1), (4, -1)):
        piv = (-side * W / 2, 0, shelf_z[idx])
        shelves[idx].xform(rot_about(piv, (0, 1, 0), side * 0.32))
        goods[idx].bm.clear()
    goods[1].bm.clear(); box_row(goods[1], rng, -W / 2 + 0.03, -W / 6 - 0.03, 0.02, shelf_z[1] + 0.01, 0.16, 0.2)
    dverts = doors.bm.verts[:]
    left = [v for v in dverts if v.co.x < -W / 6]
    mid = [v for v in dverts if -W / 6 <= v.co.x < W / 6]
    doors.xform(rot_about((-W / 2, D / 2 + 0.02, 0.4), (0, 0, 1), 1.7), left)
    bmesh.ops.delete(doors.bm, geom=mid, context='VERTS')
    for p in [cab, doors, frame] + shelves + goods: b.merge(p)
    for p in (cab, doors, frame): p.merged = True
    d = md["debris"]
    door = M(); door.box("wood_light", (W / 3 - 0.008, LOW - 0.08, 0.02), (0.05, 0.75, 0.01), (0, 0, 0.3)); d.merge(door)
    _fall_bottles(d, rng, 18, (-0.6, 0.3), (0.7, 1.1)); _fall_boxes(d, rng, 14, (-0.6, 0.3), (0.7, 1.2))
    pills(d, rng, 40, (-0.5, 0.3), (0.6, 1.0))

@model("Pharmacy")
def glass_shelf(md, B, rng):
    """유리 선반 진열장 (벽면형, 연두색 기둥). 1.0 × 0.35 × 2.0 m, 뒤 y=-0.175."""
    W, D, H = 1.0, 0.35, 2.0
    b = md.body
    for x in (-W / 2, W / 2):
        b.box("o2_green", (0.04, 0.04, H), (x, D / 2 - 0.02, H / 2)); b.box("white", (0.03, D, H), (x, 0, H / 2))
    b.box("white", (W, 0.015, H), (0, -D / 2 + 0.0075, H / 2))
    b.box("white", (W + 0.04, D, 0.1), (0, 0, 0.05)); b.box("white", (W + 0.04, D, 0.04), (0, 0, H - 0.02))
    zs = [0.1, 0.5, 0.88, 1.26, 1.62]
    glass = md["glass"]
    if not B:
        for z in zs[1:]: glass.box("glass", (W - 0.02, D - 0.03, 0.008), (0, 0.0, z))
        for z in zs: box_row(b, rng, -W / 2 + 0.04, W / 2 - 0.04, 0.0, z + 0.006, 0.2, 0.17)
        b.merge(glass); md.parts.pop("glass"); return
    # 망가진 버전: 가운데 유리 선반 두 장 깨짐 (조각만 남음), 위 선반은 기울어 상자들이 미끄러짐
    glass.box("glass", (W - 0.02, D - 0.03, 0.008), (0, 0, zs[1]))
    gt = M(); gt.box("glass", (W - 0.02, D - 0.03, 0.008), (0, 0, zs[4])); gt.xform(rot_about((W / 2, 0, zs[4]), (0, 1, 0), -0.2))
    glass.merge(gt)
    for z in (zs[2], zs[3]):
        for i in range(3): glass.box("glass", (rng.uniform(0.04, 0.12), rng.uniform(0.04, 0.12), 0.008),
                                     (rng.choice((-W / 2 + 0.05, W / 2 - 0.05)), rng.uniform(-0.1, 0.1), z), (0, 0, rng.uniform(0, 1)))
    box_row(b, rng, -W / 2 + 0.04, W / 2 - 0.04, 0.0, zs[0] + 0.006, 0.2, 0.17)
    box_row(b, rng, -W / 2 + 0.04, 0.1, 0.0, zs[1] + 0.006, 0.2, 0.17)
    sl = M(); box_row(sl, rng, -0.3, W / 2 - 0.04, 0.0, zs[4] + 0.006, 0.2, 0.12); sl.xform(rot_about((W / 2, 0, zs[4]), (0, 1, 0), -0.2)); b.merge(sl)
    shards(glass, rng, 30, (-0.7, 0.2), (0.7, 1.0))
    _fall_boxes(md["debris"], rng, 16, (-0.6, 0.25), (0.6, 1.0))

@model("Pharmacy")
def dispensing_desk(md, B, rng):
    """조제 책상 (나무, 오른쪽 서랍 3단). 1.4 × 0.7 × 0.75 m, 사람은 +Y 쪽에 앉음."""
    W, D, H = 1.4, 0.7, 0.75
    b = md.body
    top = md["top"]
    top.box("wood_light", (W, D, 0.03), (0, 0, H - 0.015))
    frame = md["frame"]
    frame.box("wood_light", (0.025, D - 0.04, H - 0.03), (-W / 2 + 0.03, 0, (H - 0.03) / 2))
    frame.box("wood_light", (0.42, D - 0.04, H - 0.03), (W / 2 - 0.23, 0, (H - 0.03) / 2))
    frame.box("wood_light", (W - 0.5, 0.02, 0.4), (-0.21, -D / 2 + 0.04, H - 0.25))
    drawers = []
    for i in range(3):
        dr = M(); z = 0.12 + i * 0.2
        dr.box("wood_light", (0.4, 0.02, 0.19), (W / 2 - 0.23, D / 2 - 0.01, z + 0.0)); dr.box("steel", (0.12, 0.012, 0.012), (W / 2 - 0.23, D / 2 + 0.008, z + 0.05))
        dr.tub("cream", (0.36, D - 0.12, 0.15), (W / 2 - 0.23, 0.02, z - 0.08), 0.01)
        drawers.append(dr)
    frame.box("dark", (W - 0.05, D - 0.08, 0.02), (0, 0, 0.01))
    if not B:
        for p in [top, frame] + drawers: b.merge(p)
        return
    top.densify(0.1, 3); top.dent((-0.3, 0.1, H), 0.2, 0.015, (0, 0, -1)); top.crumple(0.003, 6, 3)
    top.box("grime", (0.25, 0.18, 0.002), (0.2, -0.1, H + 0.001), (0, 0, 0.4))
    drawers[2].xform(Matrix.Translation(V(0, 0.3, 0)))
    drawers[1].xform(Matrix.Translation(V(0, 0.12, 0)) @ rot_about((W / 2 - 0.23, D / 2, 0.32), (0, 0, 1), 0.06))
    c = V(W / 2 - 0.23, 0.0, 0.1)
    drawers[0].xform(Matrix.Translation(V(-0.1, 0.95, 0)) @ Matrix.Rotation(-0.6, 4, 'Z') @ Matrix.Rotation(0.3, 4, 'X') @ Matrix.Translation(-c))
    to_floor([drawers[0]])
    for p in [top, frame, drawers[1], drawers[2]]: b.merge(p)
    md["debris"].merge(drawers[0])
    sheets(md["debris"], rng, 8, (-0.6, 0.4), (0.6, 1.2))

@model("Pharmacy", broken=False)
def desk_items(md, B, rng):
    """펜꽂이 + 노란 서류 트레이 (책상 위, 원점 = 책상면)."""
    b = md.body
    b.cyl("red", 0.04, 0.1, (-0.15, 0, 0.05), seg=12); b.cyl("black", 0.035, 0.002, (-0.15, 0, 0.099), seg=12)
    for i, col in enumerate(("blue", "black", "red", "yellow", "black", "o2_green")):
        a = i * 1.1
        b.rod(col, (-0.15 + math.cos(a) * 0.018, math.sin(a) * 0.018, 0.04), (-0.15 + math.cos(a) * 0.03, math.sin(a) * 0.03, 0.16 + 0.01 * (i % 3)), 0.004, 5)
    b.tub("yellow", (0.26, 0.34, 0.06), (0.12, 0, 0), 0.006)
    b.tub("yellow", (0.26, 0.34, 0.06), (0.12, 0, 0.07), 0.006)
    for i in range(4): b.box("paper", (0.21, 0.297, 0.002), (0.12, 0, 0.008 + i * 0.002), (0, 0, rng.uniform(-0.05, 0.05)))

@model("Pharmacy")
def med_cart(md, B, rng):
    """스테인리스 2단 약품 카트. 0.75 × 0.45 × 0.95 m."""
    W, D = 0.75, 0.45
    b = md.body
    cart = md["cart"]
    for x in (-W / 2 + 0.03, W / 2 - 0.03):
        for y in (-D / 2 + 0.03, D / 2 - 0.03):
            cart.rod("steel", (x, y, 0.12), (x, y, 0.88), 0.012, 8); cart.caster((x, y, 0), 0.05, "white")
    for z in (0.22, 0.7):
        cart.tub("steel", (W, D, 0.06), (0, 0, z), 0.008)
        cart.rod("steel", (-W / 2 + 0.01, -D / 2 + 0.01, z + 0.08), (W / 2 - 0.01, -D / 2 + 0.01, z + 0.08), 0.007, 6)
        cart.rod("steel", (-W / 2 + 0.01, D / 2 - 0.01, z + 0.08), (W / 2 - 0.01, D / 2 - 0.01, z + 0.08), 0.007, 6)
    for x in (-W / 2 - 0.05, W / 2 + 0.05):
        cart.poly("steel", [(x * 0.92, -D / 2 + 0.03, 0.82), (x, -D / 2 + 0.03, 0.9), (x, D / 2 - 0.03, 0.9), (x * 0.92, D / 2 - 0.03, 0.82)], 0.012, 6)
    goods = md["goods"]
    bottle_row(goods, rng, -0.33, 0.33, 0.0, 0.708, 0.3, 0.16)
    for i in range(5):
        bp = M(); _blister(bp, rng); bp.xform(Matrix.Translation(V(rng.uniform(-0.25, 0.25), rng.uniform(-0.12, 0.12), 0.23 + i * 0.008)) @ Matrix.Rotation(rng.uniform(-0.4, 0.4), 4, 'Z'))
        goods.merge(bp)
    goods.box("white", (0.12, 0.1, 0.12), (-0.22, 0.0, 0.29)); goods.box("red", (0.121, 0.06, 0.04), (-0.22, 0.0, 0.29))
    if not B:
        b.merge(cart); b.merge(goods); return
    # 망가진 버전: 옆으로 넘어짐, 위 트레이 찌그러짐, 바퀴 하나 빠짐, 약이 쏟아짐
    cart.densify(0.1, 2); cart.dent((W / 2, 0, 0.7), 0.2, 0.05, (-1, 0, -0.3)); cart.crumple(0.003, 6, 3)
    gone = [v for v in cart.bm.verts if v.co.z < 0.13 and v.co.x < 0 and v.co.y > 0]
    bmesh.ops.delete(cart.bm, geom=gone, context='VERTS')
    topple([cart], (0, D / 2 + 0.05, 0), (1, 0, 0), -math.pi / 2 + 0.1)
    b.merge(cart); md.parts.pop("goods")
    d = md["debris"]
    d.caster((0.6, 0.9, 0), 0.05, "white")
    _fall_bottles(d, rng, 12, (-0.5, -0.9), (0.6, -0.3))
    for i in range(6):
        bp = M(); _blister(bp, rng); bp.xform(Matrix.Translation(V(rng.uniform(-0.5, 0.5), rng.uniform(-1.0, -0.4), 0)) @ Matrix.Rotation(rng.uniform(0, 6), 4, 'Z')); d.merge(bp)
    pills(d, rng, 40, (-0.6, -1.1), (0.6, -0.3))

def _blister(m, rng, torn=False):
    m.box(rng.choice(("steel", "gold", "light")), (0.12, 0.06, 0.003), (0, 0, 0.0015))
    for i in range(5):
        for j in range(2):
            if torn and rng.random() < 0.5: continue
            m.cyl(rng.choice(("white", "yellow")) if not torn else "glass", 0.008, 0.006, (-0.045 + i * 0.0225, -0.013 + j * 0.026, 0.005), seg=6)

@model("Pharmacy")
def blister_packs(md, B, rng):
    """알약 블리스터 팩 소품 (몇 장 겹쳐 놓음)."""
    b = md.body
    for i in range(5 if not B else 2):
        bp = M(); _blister(bp, rng)
        bp.xform(Matrix.Translation(V(rng.uniform(-0.03, 0.03), rng.uniform(-0.02, 0.02), i * 0.007)) @ Matrix.Rotation(rng.uniform(-0.3, 0.3), 4, 'Z'))
        b.merge(bp)
    if not B: return
    d = md["debris"]
    for i in range(5):
        bp = M(); _blister(bp, rng, torn=True); bp.crumple(0.003, 30, i)
        bp.xform(Matrix.Translation(V(rng.uniform(-0.2, 0.25), rng.uniform(0.05, 0.3), 0)) @ Matrix.Rotation(rng.uniform(0, 6), 4, 'Z'))
        d.merge(bp)
    pills(d, rng, 25, (-0.25, 0.0), (0.3, 0.35))

import bmesh  # noqa: E402
