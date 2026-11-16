"""D. 냉장 약품 창고 — Rooms/ColdStorage"""
import math
from mathutils import Vector, Matrix
from rmlib import *

def _med_box(m, rng, x, y, z, s=None, crush=0.0, seed=0, yaw=0.0):
    """냉장 약품 박스 (흰 상자 + 파란 띠, 라벨)."""
    w, d, h = s or (rng.uniform(0.25, 0.35), rng.uniform(0.25, 0.32), rng.uniform(0.18, 0.26))
    bx = M()
    bx.box(rng.choice(("white", "white", "purple", "cream")), (w, d, h), (0, 0, h / 2))
    bx.box("navy", (w + 0.002, d + 0.002, 0.02), (0, 0, h * 0.75))
    bx.box("white", (w * 0.4, 0.002, h * 0.25), (0, d / 2 + 0.001, h * 0.4))
    if crush: bx.densify(0.08, 2); bx.crumple(crush, 8, seed)
    bx.xform(Matrix.Translation(V(x, y, z)) @ Matrix.Rotation(yaw, 4, 'Z'))
    m.merge(bx)
    return h

@model("ColdStorage")
def angle_rack(md, B, rng):
    """금속 앵글 랙 선반 (선반 5단). 1.5 × 0.5 × 2.0 m."""
    W, D, H = 1.5, 0.5, 2.0
    b = md.body
    posts = md["posts"]
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (W / 2 - 0.02), sy * (D / 2 - 0.02)
            posts.box("grey", (0.04, 0.003, H), (x, y + sy * 0.0185, H / 2)); posts.box("grey", (0.003, 0.04, H), (x + sx * 0.0185, y, H / 2))
            for z in [0.05 + 0.05 * k for k in range(0, 39, 3)]: posts.box("dark", (0.01, 0.004, 0.02), (x, y + sy * 0.02, z))
    zs = [0.1, 0.55, 1.0, 1.45, 1.92]
    shelves = []
    for z in zs:
        s = M()
        s.box("grey", (W, D, 0.004), (0, 0, z)); s.box("grey", (W, 0.004, 0.04), (0, D / 2, z - 0.02)); s.box("grey", (W, 0.004, 0.04), (0, -D / 2, z - 0.02))
        s.box("grey", (0.004, D, 0.04), (-W / 2, 0, z - 0.02)); s.box("grey", (0.004, D, 0.04), (W / 2, 0, z - 0.02))
        shelves.append(s)
    goods = []
    for i, z in enumerate(zs[:-1]):
        g = M(); x = -W / 2 + 0.05
        while x < W / 2 - 0.35:
            w = rng.uniform(0.26, 0.34); h = _med_box(g, rng, x + w / 2, 0, z + 0.002, (w, 0.3, rng.uniform(0.18, 0.3)))
            if rng.random() < 0.5 and h < 0.22: _med_box(g, rng, x + w / 2, 0, z + 0.002 + h, (w * 0.9, 0.28, 0.12))
            x += w + 0.02
        goods.append(g)
    if not B:
        for p in [posts] + shelves + goods: b.merge(p)
        return
    # 망가진 버전: 오른쪽 앞 기둥이 휘어 선반 3개가 앞으로 처지고, 박스가 쏟아짐
    posts.densify(0.15, 3)
    for v in posts.bm.verts:
        if v.co.x > 0.5 and v.co.y > 0.1:
            f = math.sin(min(1, v.co.z / H) * math.pi)
            v.co.x -= 0.12 * f; v.co.y += 0.06 * f
            if v.co.z > 0.8: v.co.z -= 0.08
    for idx, ang in ((2, 0.18), (3, 0.28), (4, 0.12)):
        shelves[idx].xform(rot_about((-W / 2, D / 2, zs[idx]), (0, 1, 0), -ang) @ rot_about((0, -D / 2, zs[idx]), (1, 0, 0), ang * 0.6))
        goods[idx if idx < 4 else 3].bm.clear()
    for s in shelves: s.crumple(0.004, 5, 2)
    g2 = M(); x = -W / 2 + 0.05
    while x < 0.0:
        w = 0.3; _med_box(g2, rng, x + w / 2, 0, 0, (w, 0.3, 0.22)); x += w + 0.03
    g2.xform(rot_about((-W / 2, D / 2, zs[2]), (0, 1, 0), -0.18) @ Matrix.Translation(V(0, 0, zs[2] + 0.002)))
    b.merge(g2)
    for p in [posts] + shelves + goods: b.merge(p)
    d = md["debris"]
    for i in range(9):
        _med_box(d, rng, rng.uniform(-0.3, 1.0), rng.uniform(0.45, 1.3), 0, crush=0.012, seed=i, yaw=rng.uniform(0, 6))
    scatter(d, rng, ["white", "glass"], 20, (-0.2, 0.4), (1.0, 1.3), (0.01, 0.01, 0.01), (0.03, 0.03, 0.03))

@model("ColdStorage")
def ceiling_cooler(md, B, rng):
    """천장형 냉각기 (팬 2개, 앞 +Y). 천장 소품: 윗면 z=0. 1.3 × 0.55 × 0.45 m."""
    W, D, H = 1.3, 0.55, 0.45
    b = md.body
    b.box("white", (W, D, H), (0, 0, -H / 2))
    b.box("light", (W + 0.02, D + 0.02, 0.03), (0, 0, -0.015))
    for x in (-W / 2 + 0.05, W / 2 - 0.05): b.box("light", (0.04, 0.04, 0.06), (x, -D / 2 + 0.05, 0.0))
    for k in range(12): b.box("light", (W - 0.1, 0.004, 0.006), (0, -D / 2 - 0.002, -0.05 - k * 0.03))   # 뒤쪽 핀
    b.box("light", (W - 0.06, 0.04, 0.03), (0, D / 2 - 0.02, -H + 0.015))   # 물받이
    fans = []
    for x in (-W / 4, W / 4):
        f = M(); c = V(x, D / 2, -H / 2 - 0.01)
        f.cyl("light", 0.19, 0.03, c + V(0, 0.01, 0), (math.pi / 2, 0, 0), 20)
        f.cyl("black", 0.165, 0.032, c + V(0, 0.012, 0), (math.pi / 2, 0, 0), 20)
        for i in range(5):
            a = i * 2 * math.pi / 5
            f.box("dark", (0.13, 0.006, 0.05), c + V(math.cos(a) * 0.075, 0.03, math.sin(a) * 0.075), (0, -a, 0.35))
        f.cyl("dark", 0.03, 0.04, c + V(0, 0.03, 0), (math.pi / 2, 0, 0), 10)
        for r in (0.06, 0.11, 0.16):
            pts = [c + V(math.cos(t) * r, 0.05, math.sin(t) * r) for t in [k * 2 * math.pi / 12 for k in range(12)]]
            f.poly("steel", pts, 0.003, 4, closed=True)
        f.rod("steel", c + V(-0.17, 0.05, 0), c + V(0.17, 0.05, 0), 0.003, 4); f.rod("steel", c + V(0, 0.05, -0.17), c + V(0, 0.05, 0.17), 0.003, 4)
        fans.append(f)
    if not B:
        for f in fans: b.merge(f)
        return
    # 망가진 버전: 오른쪽 팬 그릴이 떨어져 매달림 (날개 휨), 아래·앞에 성에, 녹물 자국
    grille = [v for v in fans[1].bm.verts if (v.co.y - D / 2) > 0.045]
    fans[1].xform(rot_about((W / 4, D / 2 + 0.05, -H / 2 + 0.16), (1, 0, 0), 1.1), grille)
    blades = [v for v in fans[1].bm.verts if 0.02 < (v.co.y - D / 2) < 0.04 and (v.co.x - W / 4) > 0.02]
    fans[1].xform(Matrix.Translation(V(0, 0.03, -0.02)), blades)
    for f in fans: b.merge(f)
    fr = md["frost"]
    for i in range(7):
        fr.sphere("frost", 1.0, (rng.uniform(-W / 2 + 0.1, W / 2 - 0.1), rng.uniform(-0.1, D / 2), -H - 0.01), (rng.uniform(0.08, 0.2), rng.uniform(0.05, 0.12), rng.uniform(0.02, 0.05)), 8, 5)
    for i in range(5):
        fr.cyl("frost", rng.uniform(0.006, 0.012), rng.uniform(0.04, 0.12), (rng.uniform(-W / 2 + 0.1, W / 2 - 0.1), D / 2 - 0.03, -H - 0.06), seg=5, r2=0.001)
    fr.crumple(0.008, 10, 3)
    b.merge(fr)
    for i in range(4): b.box("rust", (0.03, 0.003, rng.uniform(0.1, 0.25)), (rng.uniform(-W / 2 + 0.1, W / 2 - 0.1), D / 2 + 0.001, -H + 0.12))

@model("ColdStorage")
def vapor_light(md, B, rng):
    """방폭형 천장등 (유리 돔 + 보호망). 천장 소품: 윗면 z=0."""
    b = md.body
    b.cyl("steel", 0.09, 0.05, (0, 0, -0.025), seg=12); b.cyl("grey", 0.08, 0.03, (0, 0, -0.065), seg=12)
    dome = md["dome"]
    dome.sphere("lamp", 0.075, (0, 0, -0.08), (1, 1, 1.4), 12, 8)
    cage = md["cage"]
    for i in range(4):
        a = i * math.pi / 4
        cage.poly("grey", [V(math.cos(a) * 0.085, math.sin(a) * 0.085, -0.08)] + [V(math.cos(a) * 0.085 * math.cos(s), math.sin(a) * 0.085 * math.cos(s), -0.08 - 0.115 * math.sin(s)) for s in [k * math.pi / 8 for k in range(1, 5)]], 0.004, 4)
        cage.poly("grey", [V(-math.cos(a) * 0.085, -math.sin(a) * 0.085, -0.08)] + [V(-math.cos(a) * 0.085 * math.cos(s), -math.sin(a) * 0.085 * math.cos(s), -0.08 - 0.115 * math.sin(s)) for s in [k * math.pi / 8 for k in range(1, 5)]], 0.004, 4)
    cage.poly("grey", [V(math.cos(t) * 0.07, math.sin(t) * 0.07, -0.14) for t in [k * math.pi / 6 for k in range(12)]], 0.004, 4, closed=True)
    if not B:
        b.merge(dome); b.merge(cage); return
    # 망가진 버전: 유리 돔 깨짐 (아래 반쪽 없음, 전구 드러남), 보호망 휨
    top = [v for v in dome.bm.verts if v.co.z < -0.1]
    bmesh.ops.delete(dome.bm, geom=top, context='VERTS')
    for f in dome.bm.faces: f.material_index = dome._mi("glass")
    for v in dome.bm.verts:
        if v.co.z < -0.08: v.co.z -= rng.uniform(0, 0.02)
    dome.sphere("grime", 0.025, (0, 0, -0.1), (1, 1, 1.3), 8, 6)
    cage.densify(0.03, 2)
    for v in cage.bm.verts:
        if v.co.x > 0: v.co.z += 0.03 * v.co.x / 0.085; v.co.x += 0.02
    b.merge(dome); b.merge(cage)

@model("ColdStorage")
def box_stack(md, B, rng):
    """냉장 약품 박스 스택 (3열 × 3단 안팎)."""
    b = md.body
    stacks = []
    for i, x in enumerate((-0.35, 0.0, 0.35)):
        s = M(); z = 0.0
        for k in range(rng.randint(2, 4)):
            h = _med_box(s, rng, x + rng.uniform(-0.01, 0.01), rng.uniform(-0.01, 0.01), z, (0.32, 0.3, rng.uniform(0.18, 0.26)), yaw=rng.uniform(-0.05, 0.05))
            z += h
        stacks.append(s)
    if not B:
        for s in stacks: b.merge(s)
        return
    # 망가진 버전: 오른쪽 더미가 무너져 바닥에 흩어짐, 가운데는 기울고 찌그러짐, 하나는 뚜껑 열림
    b.merge(stacks[0])
    stacks[1].densify(0.08, 2); stacks[1].crumple(0.008, 8, 2); stacks[1].xform(rot_about((0.16, 0, 0), (0, 1, 0), -0.12))
    b.merge(stacks[1])
    d = md["debris"]
    for i in range(4):
        _med_box(d, rng, rng.uniform(0.4, 0.9), rng.uniform(0.2, 0.8), 0, crush=0.015, seed=i + 5, yaw=rng.uniform(0, 6))
    op = M(); op.tub("cardboard", (0.3, 0.28, 0.2), (0.0, 0.0, 0), 0.006, inner="white")
    for s in (-1, 1): op.box("cardboard", (0.3, 0.14, 0.004), (0, s * 0.2, 0.2), (s * -0.7, 0, 0))
    op.xform(Matrix.Translation(V(0.75, -0.25, 0)) @ Matrix.Rotation(0.4, 4, 'Z')); d.merge(op)
    for i in range(6): d.box("glass", (0.02, 0.02, 0.05), (0.5 + rng.uniform(0, 0.4), rng.uniform(0.3, 0.9), 0.01), (math.pi / 2, 0, rng.uniform(0, 6)))

import bmesh  # noqa: E402
