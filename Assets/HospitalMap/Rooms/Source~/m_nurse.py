"""E. 간호사 스테이션 — Rooms/NurseStation"""
import math
from mathutils import Vector, Matrix
from rmlib import *
from m_common import bottle_row, box_row, books

@model("NurseStation")
def upper_cabinet(md, B, rng):
    """상부 수납장 (흰색, 문 3짝). 벽 소품: 뒤 y=0, 아래 높이 1.5 m. 1.2 × 0.35 × 0.75 m."""
    W, D, H, Z = 1.2, 0.35, 0.75, 1.5
    b = md.body
    car = md["car"]
    car.box("white", (W, 0.012, H), (0, 0.006, Z + H / 2))
    for x in (-W / 2 + 0.009, W / 2 - 0.009): car.box("white", (0.018, D, H), (x, D / 2, Z + H / 2))
    for z in (Z + 0.009, Z + H - 0.009, Z + H / 2): car.box("white", (W - 0.036, D - 0.02, 0.018), (0, D / 2 - 0.01, z))
    goods = md["goods"]
    box_row(goods, rng, -W / 2 + 0.03, W / 2 - 0.03, D / 2 - 0.02, Z + 0.018, 0.22, 0.17)
    bottle_row(goods, rng, -W / 2 + 0.03, W / 2 - 0.03, D / 2 - 0.02, Z + H / 2 + 0.009, 0.2, 0.16)
    doors = [M(), M(), M()]
    for i, dm in enumerate(doors):
        x = -W / 2 + W / 6 + i * W / 3
        dm.box("white", (W / 3 - 0.006, 0.018, H - 0.006), (x, D + 0.009, Z + H / 2))
        dm.box("steel", (0.01, 0.014, 0.1), (x + (W / 6 - 0.035) * (1 if i < 2 else -1), D + 0.025, Z + 0.1))
    if not B:
        for p in [car, goods] + doors: b.merge(p)
        return
    # 망가진 버전: 가운데 문 떨어져 바닥에, 오른쪽 문은 한 경첩으로 처짐, 내용물 일부 쏟아짐
    car.densify(0.15, 2); car.crumple(0.003, 4, 1)
    doors[2].xform(rot_about((W / 2, D + 0.01, Z + H), (0, 0, 1), -1.3) @ rot_about((W / 2, D + 0.01, Z + H), (0, 1, 0), 0.35))
    doors[0].xform(rot_about((-W / 2, D + 0.01, Z), (0, 0, 1), 0.25))
    c = V(0, D + 0.01, Z + H / 2)
    doors[1].xform(Matrix.Translation(V(0.1, 0.8, 0)) @ Matrix.Rotation(0.5, 4, 'Z') @ Matrix.Rotation(-math.pi / 2, 4, 'X') @ Matrix.Translation(-c))
    to_floor([doors[1]])
    bmesh.ops.delete(goods.bm, geom=[v for v in goods.bm.verts if v.co.x >= 0.1], context='VERTS')
    for p in [car, goods, doors[0], doors[2]]: b.merge(p)
    d = md["debris"]; d.merge(doors[1])
    from m_pharmacy import _fall_boxes, _fall_bottles
    _fall_boxes(d, rng, 8, (0.0, 0.5), (0.7, 1.1)); _fall_bottles(d, rng, 6, (0.0, 0.5), (0.7, 1.1))

@model("NurseStation")
def sink_counter(md, B, rng):
    """하부 수납장 + 상판 + 싱크대 (수전, 비누통). 1.8 × 0.6 × 0.9 m, 뒤 y=-0.3."""
    W, D, H = 1.8, 0.6, 0.9
    b = md.body
    car = md["car"]
    car.box("white", (W, D - 0.04, H - 0.14), (0, -0.02, 0.1 + (H - 0.14) / 2)); car.box("dark", (W - 0.04, D - 0.1, 0.1), (0, -0.04, 0.05))
    top = md["top"]
    sx, sw, sd = 0.45, 0.5, 0.38
    top.box("light", (W / 2 + sx - sw / 2, D, 0.03), (-W / 2 + (W / 2 + sx - sw / 2) / 2, 0, H - 0.015))
    top.box("light", (W / 2 - sx - sw / 2, D, 0.03), (W / 2 - (W / 2 - sx - sw / 2) / 2, 0, H - 0.015))
    top.box("light", (sw, (D - sd) / 2, 0.03), (sx, D / 2 - (D - sd) / 4, H - 0.015)); top.box("light", (sw, (D - sd) / 2, 0.03), (sx, -D / 2 + (D - sd) / 4, H - 0.015))
    top.box("light", (W, 0.02, 0.1), (0, -D / 2 + 0.01, H + 0.05))
    sink = md["sink"]
    sink.tub("steel", (sw, sd, 0.18), (sx, 0, H - 0.18), 0.01)
    sink.cyl("dark", 0.025, 0.004, (sx, 0, H - 0.168), seg=10)
    fau = md["faucet"]
    fau.cyl("steel", 0.022, 0.06, (sx, -D / 2 + 0.07, H + 0.03), seg=10)
    fau.poly("steel", [(sx, -D / 2 + 0.07, H + 0.05), (sx, -D / 2 + 0.07, H + 0.28), (sx, -D / 2 + 0.17, H + 0.32), (sx, -D / 2 + 0.25, H + 0.24)], 0.012, 8)
    fau.box("steel", (0.012, 0.1, 0.012), (sx + 0.03, -D / 2 + 0.1, H + 0.09), (0.3, 0, 0))
    b.cyl("pink", 0.035, 0.18, (sx + 0.38, -0.2, H + 0.09), seg=10); b.cyl("white", 0.015, 0.05, (sx + 0.38, -0.2, H + 0.2), seg=6)
    doors = []
    n = 6
    for i in range(n):
        dm = M(); x = -W / 2 + W / (2 * n) + i * W / n
        dm.box("white", (W / n - 0.006, 0.018, H - 0.16), (x, D / 2 - 0.031, 0.1 + (H - 0.16) / 2))
        dm.box("steel", (0.01, 0.014, 0.1), (x + (W / (2 * n) - 0.03) * (1 if i % 2 else -1), D / 2 - 0.015, H - 0.15))
        doors.append(dm)
    if not B:
        for p in [car, top, sink, fau] + doors: b.merge(p)
        return
    # 망가진 버전: 문 두 짝 열리고 하나는 떨어짐, 수전 꺾임, 싱크에 오물, 상판 금
    top.box("grime", (0.4, 0.004, 0.002), (-0.4, 0.05, H + 0.001), (0, 0, 0.5)); top.densify(0.15, 2); top.crumple(0.003, 4, 2)
    sink.box("grime", (sw - 0.04, sd - 0.04, 0.003), (sx, 0, H - 0.165)); sink.sphere("grime", 1.0, (sx - 0.08, 0.04, H - 0.165), (0.12, 0.09, 0.02), 8, 5)
    tip = [v for v in fau.bm.verts if v.co.z > H + 0.2]
    fau.xform(rot_about((sx, -D / 2 + 0.07, H + 0.2), (1, 0.4, 0), 0.9), tip)
    doors[1].xform(rot_about((-W / 2 + W / 3, D / 2 - 0.022, 0), (0, 0, 1), -1.5))
    doors[4].xform(rot_about((-W / 2 + 4 * W / n, D / 2 - 0.022, 0), (0, 0, 1), 1.2) @ rot_about((-W / 2 + 4 * W / n, D / 2, 0.8), (0, 1, 0), 0.1))
    c = V(-W / 2 + 5 * W / n + W / (2 * n), D / 2, 0.45)
    doors[5].xform(Matrix.Translation(V(0.7, 0.75, 0)) @ Matrix.Rotation(-0.3, 4, 'Z') @ Matrix.Rotation(-math.pi / 2, 4, 'X') @ Matrix.Translation(-c)); to_floor([doors[5]])
    for p in [car, top, sink, fau] + doors[:5]: b.merge(p)
    md["debris"].merge(doors[5])
    from m_pharmacy import _fall_bottles
    _fall_bottles(md["debris"], rng, 5, (-0.6, 0.4), (0.5, 0.9))
    md["debris"].cyl("pink", 0.035, 0.18, (0.2, 0.7, 0.035), (math.pi / 2, 0, 1.0), 10)

@model("NurseStation")
def open_shelf(md, B, rng):
    """개방형 나무 선반 칸 (3 × 2 칸, 바인더). 1.0 × 0.3 × 0.62 m. 상판 위에 놓는다."""
    W, D, H, t = 1.0, 0.3, 0.62, 0.018
    b = md.body
    frame = md["frame"]
    for z in (t / 2, H / 2, H - t / 2): frame.box("wood_light", (W, D, t), (0, 0, z))
    for x in (-W / 2 + t / 2, -W / 6, W / 6, W / 2 - t / 2): frame.box("wood_light", (t, D, H), (x, 0, H / 2))
    frame.box("wood_light", (W, 0.006, H), (0, -D / 2 + 0.003, H / 2))
    cells = []
    for r in range(2):
        for c in range(3):
            x0 = -W / 2 + t + c * W / 3; x1 = x0 + W / 3 - 1.5 * t; z = t + r * (H / 2)
            cm = M()
            if (r + c) % 3 == 0: box_row(cm, rng, x0, x1, 0.0, z, 0.22, 0.24)
            elif (r + c) % 3 == 1: books(cm, rng, x0, x1, 0.0, z, 0.24, ("blue", "white", "red", "o2_green", "navy", "yellow"))
            else:
                for k in range(3): cm.box("paper", (x1 - x0 - 0.02, 0.24, 0.004), ((x0 + x1) / 2, 0, z + 0.003 + k * 0.005), (0, 0, rng.uniform(-0.05, 0.05)))
                cm.tub("light", (x1 - x0 - 0.04, 0.22, 0.08), ((x0 + x1) / 2, 0, z + 0.02), 0.004)
            cells.append(cm)
    if not B:
        for p in [frame] + cells: b.merge(p)
        return
    # 망가진 버전: 오른쪽 칸막이가 빠져 위 선반이 처짐, 바인더·서류가 흩어짐
    gone = [v for v in frame.bm.verts if abs(v.co.x - W / 6) < 0.012]
    bmesh.ops.delete(frame.bm, geom=gone, context='VERTS')
    frame.densify(0.1, 2)
    for v in frame.bm.verts:
        if v.co.z > H / 2 - 0.02 and v.co.z < H / 2 + 0.02 and v.co.x > -W / 6: v.co.z -= 0.08 * math.sin((v.co.x + W / 6) / (2 * W / 3) * math.pi)
    for i in (1, 2, 5): cells[i].bm.clear()
    for p in [frame] + cells: b.merge(p)
    d = md["debris"]
    lay = M(); books(lay, rng, 0.0, 0.3, 0.0, 0.0, 0.24)   # 옆으로 쓰러져 쌓인 바인더
    lay.xform(Matrix.Translation(V(0.2, 0.45, 0.0)) @ Matrix.Rotation(0.6, 4, 'Z') @ Matrix.Rotation(math.pi / 2, 4, 'Y')); to_floor([lay])
    d.merge(lay)
    sheets(d, rng, 6, (-0.3, 0.3), (0.6, 0.9))

@model("NurseStation")
def parts_drawer(md, B, rng):
    """소형 서랍 유닛 (부품함, 4 × 5 칸). 0.42 × 0.25 × 0.36 m."""
    W, D, H = 0.42, 0.25, 0.36
    b = md.body
    b.tub("grey", (W, D, H), (0, -0.005, 0), 0.008, top=True)
    drawers = []
    for r in range(5):
        for c in range(4):
            dm = M(); x = -W / 2 + W / 8 + c * W / 4; z = 0.01 + r * (H - 0.02) / 5
            dm.box("dark", (W / 4 - 0.008, 0.012, (H - 0.02) / 5 - 0.006), (x, D / 2 - 0.006, z + (H - 0.02) / 10))
            dm.box("white", (0.04, 0.003, 0.012), (x, D / 2 + 0.002, z + (H - 0.02) / 10 + 0.012))
            dm.box("grey", (0.025, 0.012, 0.008), (x, D / 2 + 0.006, z + (H - 0.02) / 10 - 0.012))
            dm.tub("glass", (W / 4 - 0.012, D - 0.03, (H - 0.02) / 5 - 0.012), (x, -0.005, z + 0.004), 0.003)
            drawers.append(dm)
    if not B:
        for p in drawers: b.merge(p)
        return
    b.densify(0.08, 2); b.crumple(0.003, 10, 1)
    d = md["debris"]
    for i, dm in enumerate(drawers):
        roll = rng.random()
        if roll < 0.2:
            c = V(*[sum(v.co[k] for v in dm.bm.verts) / len(dm.bm.verts) for k in range(3)])
            dm.xform(Matrix.Translation(V(rng.uniform(-0.3, 0.3), rng.uniform(0.35, 0.65), 0)) @ Matrix.Rotation(rng.uniform(0, 6), 4, 'Z') @ Matrix.Rotation(rng.choice((0, math.pi)), 4, 'Y') @ Matrix.Translation(-c))
            to_floor([dm]); d.merge(dm)
        elif roll < 0.5:
            dm.xform(Matrix.Translation(V(0, rng.uniform(0.04, 0.15), 0))); b.merge(dm)
        else:
            b.merge(dm)
    scatter(d, rng, ["steel", "red", "white", "blue"], 25, (-0.35, 0.2), (0.35, 0.7), (0.004, 0.004, 0.003), (0.02, 0.012, 0.008))

@model("NurseStation")
def service_cart(md, B, rng):
    """회색 2단 서비스 카트 (플라스틱, 손잡이, 바퀴). 0.8 × 0.45 × 0.95 m."""
    W, D = 0.8, 0.45
    b = md.body
    cart = md["cart"]
    for x in (-W / 2 + 0.03, W / 2 - 0.03):
        for y in (-D / 2 + 0.03, D / 2 - 0.03):
            cart.box("grey", (0.04, 0.04, 0.68), (x, y, 0.47)); cart.caster((x, y, 0), 0.045, "grey")
    trays = []
    for z in (0.18, 0.75):
        t = M(); t.tub("grey", (W, D, 0.07), (0, 0, z), 0.012); t.box("grey", (W + 0.02, D + 0.02, 0.012), (0, 0, z + 0.07)); trays.append(t)
    cart.poly("grey", [(-W / 2 - 0.06, -0.15, 0.78), (-W / 2 - 0.08, -0.15, 0.95), (-W / 2 - 0.08, 0.15, 0.95), (-W / 2 - 0.06, 0.15, 0.78)], 0.016, 6)
    goods = md["goods"]
    goods.box("white", (0.25, 0.2, 0.12), (0.15, 0, 0.82)); goods.box("blue", (0.251, 0.05, 0.04), (0.15, 0, 0.84))
    goods.cyl("white", 0.04, 0.18, (-0.15, 0.08, 0.85), seg=10); goods.cyl("blue", 0.035, 0.03, (-0.15, 0.08, 0.955), seg=10)
    goods.box("cardboard", (0.35, 0.3, 0.2), (0, 0, 0.29))
    if not B:
        for p in [cart, goods] + trays: b.merge(p)
        return
    # 망가진 버전: 옆으로 넘어짐, 위 트레이 금 가서 비틀림, 짐 쏟아짐
    trays[1].densify(0.1, 2); trays[1].crumple(0.006, 6, 2); trays[1].xform(rot_about((W / 2, 0, 0.75), (0, 1, 0), 0.15))
    ms = [cart] + trays
    topple(ms, (0, -D / 2 - 0.02, 0), (1, 0, 0), math.pi / 2 - 0.05)
    for p in ms: b.merge(p)
    md.parts.pop("goods")
    d = md["debris"]
    bx = M(); bx.box("cardboard", (0.35, 0.3, 0.2), (0, 0, 0.1)); bx.crumple(0.01, 8, 3); bx.xform(Matrix.Translation(V(-0.3, -0.9, 0)) @ Matrix.Rotation(0.7, 4, 'Z')); d.merge(bx)
    d.cyl("white", 0.04, 0.18, (0.3, -0.75, 0.04), (math.pi / 2, 0, 0.5), 10)
    sheets(d, rng, 5, (-0.5, -1.2), (0.5, -0.6))
    scatter(d, rng, ["white", "blue"], 8, (-0.4, -1.1), (0.4, -0.6), (0.03, 0.03, 0.01), (0.08, 0.06, 0.03))

@model("NurseStation")
def towel_dispenser(md, B, rng):
    """페이퍼타올 디스펜서. 벽 소품: 뒤 y=0, 아래 높이 1.25 m."""
    W, D, H, Z = 0.28, 0.12, 0.38, 1.25
    b = md.body
    b.tub("white", (W, D, H), (0, D / 2, Z), 0.008, rot=(0, 0, 0))
    b.box("paper", (W - 0.03, D - 0.03, H - 0.06), (0, D / 2, Z + H / 2))
    cover = md["cover"]
    cover.box("white", (W, 0.01, H), (0, D + 0.005, Z + H / 2)); cover.box("white", (W, D, 0.01), (0, D / 2, Z + H - 0.005))
    cover.box("o2_green", (0.12, 0.004, 0.04), (0, D + 0.011, Z + H - 0.08))
    cover.box("grey", (0.03, 0.004, 0.025), (0, D + 0.011, Z + H - 0.03))
    b.box("paper", (W - 0.06, 0.01, 0.05), (0, D / 2 + 0.01, Z - 0.02), (0.3, 0, 0))
    if not B:
        b.merge(cover); return
    # 망가진 버전: 앞 덮개가 아래 경첩에서 열려 늘어짐, 종이 수건이 바닥에 흩어짐
    cover.xform(rot_about((0, D, Z), (1, 0, 0), 2.3))
    b.merge(cover)
    sheets(md["debris"], rng, 9, (-0.3, 0.15), (0.3, 0.6), w=0.23, h=0.22)
    for i in range(3):
        cr = M(); cr.sphere("paper", 0.05, (rng.uniform(-0.3, 0.3), rng.uniform(0.2, 0.6), 0.03), (1, 0.8, 0.6), 6, 4); cr.crumple(0.015, 30, i); md["debris"].merge(cr)

@model("NurseStation", broken=False)
def towel_rack(md, B, rng):
    """수건걸이 + 수건. 벽 소품: 뒤 y=0, 막대 높이 1.0 m."""
    b = md.body
    for x in (-0.22, 0.22):
        b.cyl("steel", 0.02, 0.01, (x, 0.005, 1.0), (math.pi / 2, 0, 0), 10); b.rod("steel", (x, 0.0, 1.0), (x, 0.06, 1.0), 0.008, 6)
    b.rod("steel", (-0.23, 0.06, 1.0), (0.23, 0.06, 1.0), 0.009, 8)
    towel = md["towel"]
    towel.box("navy", (0.34, 0.012, 0.4), (0.0, 0.07, 0.8)); towel.box("navy", (0.34, 0.012, 0.28), (0.0, 0.05, 0.86))
    towel.box("navy", (0.34, 0.03, 0.02), (0.0, 0.06, 1.01))
    towel.densify(0.06, 2); towel.crumple(0.006, 10, 1)
    md.parts["cloth"] = md.parts.pop("towel")

import bmesh  # noqa: E402
