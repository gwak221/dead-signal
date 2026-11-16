"""G. 진료실 — Rooms/Consultation"""
import math
from mathutils import Vector, Matrix
from rmlib import *
from m_common import chair_parts, books

@model("Consultation")
def doctor_desk(md, B, rng):
    """의사 책상 (어두운 나무, 서랍 3단 + 데스크 매트). 1.6 × 0.8 × 0.75 m, 의사는 -Y 쪽에 앉음 (손님 쪽 +Y 가 막힘)."""
    W, D, H = 1.6, 0.8, 0.75
    b = md.body
    top = md["top"]
    top.box("wood_dark", (W, D, 0.04), (0, 0, H - 0.02))
    top.box("black", (0.7, 0.4, 0.004), (-0.1, -0.1, H + 0.002))
    car = md["car"]
    car.box("wood_dark", (0.03, D - 0.04, H - 0.04), (-W / 2 + 0.03, 0, (H - 0.04) / 2))
    car.box("wood_dark", (0.45, D - 0.04, H - 0.04), (W / 2 - 0.24, 0, (H - 0.04) / 2))
    car.box("wood_dark", (W - 0.5, 0.025, 0.55), (-0.22, D / 2 - 0.03, H - 0.32))   # 앞 가림판
    drs = []
    for i in range(3):
        dr = M(); z = 0.14 + i * 0.2
        dr.box("wood_dark", (0.43, 0.02, 0.19), (W / 2 - 0.24, -D / 2 + 0.01, z)); dr.box("gold", (0.1, 0.012, 0.012), (W / 2 - 0.24, -D / 2 - 0.006, z + 0.05))
        dr.tub("brown", (0.38, D - 0.12, 0.15), (W / 2 - 0.24, -0.02, z - 0.08), 0.01)
        drs.append(dr)
    if not B:
        for p in [top, car] + drs: b.merge(p)
        return
    top.densify(0.12, 3); top.dent((0.3, 0.1, H), 0.2, 0.015, (0, 0, -1))
    for i in range(4): top.box("cream", (0.25, 0.004, 0.002), (rng.uniform(-0.5, 0.3), rng.uniform(-0.2, 0.2), H + 0.001), (0, 0, rng.uniform(0, 3)))
    drs[2].xform(Matrix.Translation(V(0, -0.3, 0))); drs[1].xform(Matrix.Translation(V(0, -0.12, 0)))
    c = V(W / 2 - 0.24, -0.02, 0.1)
    drs[0].xform(Matrix.Translation(V(0.0, -1.0, 0)) @ Matrix.Rotation(0.9, 4, 'Z') @ Matrix.Rotation(math.pi, 4, 'Y') @ Matrix.Translation(-c)); to_floor([drs[0]])
    for p in [top, car, drs[1], drs[2]]: b.merge(p)
    md["debris"].merge(drs[0]); sheets(md["debris"], rng, 9, (-0.6, -1.3), (0.7, -0.5))

@model("Consultation")
def leather_chair(md, B, rng):
    """하이백 가죽 의자 (검정, 머리받침)."""
    b = md.body
    base, seat = chair_parts("leather", high=True)
    if not B:
        b.merge(base); b.merge(seat); return
    # 망가진 버전: 뒤로 넘어짐, 가죽 찢어져 스펀지 노출
    seat.densify(0.08, 3); seat.crumple(0.008, 6, 2)
    for i in range(6): seat.sphere("foam", 1.0, (rng.uniform(-0.15, 0.15), rng.uniform(-0.05, 0.15), 0.53), (rng.uniform(0.03, 0.07), rng.uniform(0.03, 0.07), 0.025), 6, 4)
    ms = [base, seat]
    topple(ms, (0, -0.35, 0), (1, 0, 0), -math.pi / 2 + 0.25)
    b.merge(base); b.merge(seat)
    scatter(md["debris"], rng, ["foam", "leather"], 7, (-0.4, 0.1), (0.4, 0.6), (0.02, 0.02, 0.01), (0.07, 0.05, 0.03))

@model("Consultation")
def book_set(md, B, rng):
    """책 소품 세트 (한 줄 약 0.6 m + 북엔드). 선반·책상 위에 놓는다."""
    b = md.body
    if not B:
        b.box("steel", (0.12, 0.15, 0.004), (-0.33, 0, 0.002)); b.box("steel", (0.004, 0.15, 0.16), (-0.27, 0, 0.08))
        books(b, rng, -0.265, 0.32, 0, 0, 0.2)
        b.box("navy", (0.03, 0.2, 0.24), (0.34, 0.0, 0.11), (0, 0.25, 0))
        return
    # 망가진 버전: 바닥에 흩어진 책 (펼쳐진 것 포함)
    books(b, rng, -0.3, -0.05, 0, 0, 0.2)
    d = md["debris"]
    for i in range(9):
        t, h, w = rng.uniform(0.02, 0.045), rng.uniform(0.18, 0.27), rng.uniform(0.15, 0.2)
        bk = M(); col = rng.choice(("navy", "red", "green", "cream", "brown", "blue"))
        if i % 3 == 0:   # 펼쳐진 책
            bk.box(col, (w, h, 0.004), (-w / 2, 0, 0.012), (0, 0.12, 0)); bk.box(col, (w, h, 0.004), (w / 2, 0, 0.012), (0, -0.12, 0))
            bk.box("paper", (w * 0.95, h * 0.95, t * 0.5), (-w / 2, 0, 0.02), (0, 0.1, 0)); bk.box("paper", (w * 0.95, h * 0.95, t * 0.5), (w / 2, 0, 0.02), (0, -0.1, 0))
        else:
            bk.box(col, (w, h, t), (0, 0, t / 2))
        bk.xform(Matrix.Translation(V(rng.uniform(-0.2, 0.6), rng.uniform(0.15, 0.7), i * 0.002)) @ Matrix.Rotation(rng.uniform(0, 6), 4, 'Z'))
        d.merge(bk)
    sheets(d, rng, 4, (-0.2, 0.2), (0.5, 0.7))

@model("Consultation")
def sink_cabinet(md, B, rng):
    """하부 수납장 + 소형 싱크 (어두운 나무). 1.2 × 0.5 × 0.85 m, 뒤 y=-0.25."""
    W, D, H = 1.2, 0.5, 0.85
    b = md.body
    car = md["car"]
    car.box("wood_dark", (W, D - 0.03, H - 0.12), (0, -0.015, 0.08 + (H - 0.12) / 2)); car.box("black", (W - 0.04, D - 0.08, 0.08), (0, -0.03, 0.04))
    top = md["top"]
    top.box("wood_dark", (W, D, 0.04), (0, 0, H - 0.02))
    sink = md["sink"]
    sink.cyl("steel", 0.15, 0.012, (-0.3, 0, H + 0.003), seg=16); sink.cyl("grime", 0.12, 0.004, (-0.3, 0, H + 0.008), seg=16)
    sink.cyl("dark", 0.02, 0.003, (-0.3, 0, H + 0.011), seg=8)
    fau = md["faucet"]
    fau.cyl("steel", 0.018, 0.05, (-0.3, -D / 2 + 0.06, H + 0.025), seg=8)
    fau.poly("steel", [(-0.3, -D / 2 + 0.06, H + 0.04), (-0.3, -D / 2 + 0.06, H + 0.2), (-0.3, -D / 2 + 0.16, H + 0.22), (-0.3, -D / 2 + 0.2, H + 0.17)], 0.01, 8)
    b.box("white", (0.05, 0.05, 0.14), (0.0, -0.18, H + 0.07)); b.box("red", (0.051, 0.03, 0.03), (0.0, -0.18, H + 0.1))
    doors = []
    for i in range(3):
        dm = M(); x = -W / 2 + W / 6 + i * W / 3
        dm.box("wood_dark", (W / 3 - 0.006, 0.018, H - 0.14), (x, D / 2 - 0.021, 0.08 + (H - 0.14) / 2))
        dm.box("gold", (0.01, 0.012, 0.1), (x + (W / 6 - 0.04) * (-1 if i else 1), D / 2 - 0.006, H - 0.15))
        doors.append(dm)
    if not B:
        for p in [car, top, sink, fau] + doors: b.merge(p)
        return
    tip = [v for v in fau.bm.verts if v.co.z > H + 0.15]
    fau.xform(rot_about((-0.3, -D / 2 + 0.06, H + 0.15), (1, -0.5, 0), 1.1), tip)
    top.densify(0.12, 2); top.crumple(0.003, 5, 2)
    doors[0].xform(rot_about((-W / 2, D / 2 - 0.012, 0), (0, 0, 1), 1.6))
    c = V(W / 2 - W / 6, D / 2, 0.45)
    doors[2].xform(Matrix.Translation(V(0.5, 0.75, 0)) @ Matrix.Rotation(0.4, 4, 'Z') @ Matrix.Rotation(-math.pi / 2, 4, 'X') @ Matrix.Translation(-c)); to_floor([doors[2]])
    for p in [car, top, sink, fau, doors[0], doors[1]]: b.merge(p)
    md["debris"].merge(doors[2])
    md["debris"].cyl("white", 0.04, 0.09, (-0.2, 0.75, 0.04), (math.pi / 2, 0, 0.4), 10)

@model("Consultation")
def chiro_table(md, B, rng):
    """다단 진료 테이블 (카이로프랙틱, 4단 패드, 검은 받침). 0.6 × 1.9 m, 머리 +Y."""
    b = md.body
    base = md["base"]
    base.box("black", (0.35, 1.4, 0.06), (0, 0, 0.03)); base.box("black", (0.25, 1.2, 0.2), (0, 0, 0.2))
    for y in (-0.6, 0.6): base.box("black", (0.1, 0.1, 0.4), (0, y, 0.35))
    base.box("white", (0.18, 0.004, 0.04), (0, -0.7, 0.25)); base.box("black", (0.3, 0.2, 0.04), (0, -0.85, 0.08))
    pads = []
    specs = [(-0.75, 0.4, 0.0), (-0.3, 0.45, 0.0), (0.2, 0.5, 0.05), (0.68, 0.38, -0.12)]
    for y, L, tilt in specs:
        p = M()
        p.box("dark", (0.55, L, 0.1), (0, y, 0.6), (tilt, 0, 0)); p.box("black", (0.5, L - 0.04, 0.03), (0, y, 0.54), (tilt, 0, 0))
        for s in (-1, 1): p.box("grey", (0.004, L - 0.06, 0.004), (s * 0.276, y, 0.62), (tilt, 0, 0))
        pads.append(p)
    for s in (-1, 1): base.box("steel", (0.02, 0.15, 0.02), (s * 0.3, 0.5, 0.55))
    if not B:
        b.merge(base)
        for p in pads: b.merge(p)
        return
    # 망가진 버전: 머리 패드 떨어져 바닥에, 가운데 패드 찢어짐, 다리 패드 기울어짐
    pads[2].densify(0.1, 3); pads[2].dent((0.05, 0.2, 0.66), 0.2, 0.04, (0, 0, -1)); pads[2].crumple(0.008, 6, 2)
    for i in range(5): pads[2].sphere("foam", 1.0, (rng.uniform(-0.1, 0.15), rng.uniform(0.05, 0.35), 0.64), (rng.uniform(0.03, 0.07), rng.uniform(0.03, 0.07), 0.02), 6, 4)
    pads[0].xform(rot_about((0, -0.55, 0.55), (1, 0, 0), 0.35))
    b.merge(base); b.merge(pads[0]); b.merge(pads[1]); b.merge(pads[2])
    pads[3].xform(Matrix.Translation(V(0.55, 0.5, -0.55)) @ rot_about((0, 0.68, 0.6), (0, 0, 1), 0.7)); to_floor([pads[3]])
    md["debris"].merge(pads[3])

@model("Consultation", broken=False)
def coffee_machine(md, B, rng):
    """캡슐 커피머신. 0.2 × 0.35 × 0.3 m."""
    b = md.body
    b.box("black", (0.15, 0.32, 0.28), (0, 0, 0.14)); b.box("dark", (0.15, 0.06, 0.06), (0, 0.13, 0.25))
    b.box("glass", (0.06, 0.2, 0.22), (-0.105, -0.05, 0.13))
    b.box("dark", (0.15, 0.1, 0.02), (0, 0.18, 0.01)); b.box("steel", (0.12, 0.08, 0.004), (0, 0.18, 0.022))
    b.cyl("steel", 0.012, 0.03, (0, 0.14, 0.2), seg=8); b.cyl("white", 0.03, 0.06, (0, 0.18, 0.054), seg=10)
    for i in range(2): b.cyl("o2_green" if i else "white", 0.008, 0.004, (0.03 - i * 0.06, 0.14, 0.282), seg=8)

@model("Consultation", broken=False)
def trophies(md, B, rng):
    """트로피·장식품 (금색 컵, 말 조각, 둥근 기념패)."""
    b = md.body
    b.box("wood_dark", (0.08, 0.08, 0.06), (-0.15, 0, 0.03)); b.cyl("gold", 0.012, 0.06, (-0.15, 0, 0.09), seg=8)
    b.cyl("gold", 0.025, 0.07, (-0.15, 0, 0.155), seg=12, r2=0.05)
    for s in (-1, 1): b.poly("gold", [(-0.15 + s * 0.045, 0, 0.17), (-0.15 + s * 0.07, 0, 0.16), (-0.15 + s * 0.05, 0, 0.13)], 0.005, 4)
    b.box("black", (0.12, 0.06, 0.03), (0.05, 0, 0.015))
    h = M()
    h.sphere("gold", 1.0, (0.05, 0, 0.1), (0.05, 0.02, 0.03), 8, 5); h.box("gold", (0.02, 0.025, 0.08), (0.09, 0, 0.14), (0, -0.5, 0))
    h.box("gold", (0.035, 0.02, 0.025), (0.11, 0, 0.18), (0, 0.4, 0))
    for x in (0.02, 0.08): h.rod("gold", (x, 0, 0.08), (x, 0, 0.03), 0.006, 4)
    b.merge(h)
    b.box("wood_dark", (0.07, 0.05, 0.015), (0.22, 0, 0.0075)); b.cyl("gold", 0.045, 0.01, (0.22, 0, 0.065), (math.pi / 2, 0, 0), 14)
    b.cyl("steel", 0.035, 0.012, (0.22, 0.004, 0.065), (math.pi / 2, 0, 0), 14)

@model("Consultation")
def magnifier_lamp(md, B, rng):
    """확대경 램프 (둥근 받침, 관절형 팔, 원형 머리 + 렌즈). 책상 위에 놓는다."""
    b = md.body
    lamp = md["lamp"]
    lamp.cyl("light", 0.1, 0.03, (0, 0, 0.015), seg=14); lamp.cyl("light", 0.02, 0.06, (0, 0, 0.06), seg=8)
    lamp.rod("light", (0, 0, 0.08), (0.05, 0.1, 0.45), 0.01, 6)
    lamp.cyl("dark", 0.018, 0.03, (0.05, 0.1, 0.45), (0, math.pi / 2, 0), 8)
    lamp.rod("light", (0.05, 0.1, 0.45), (0.05, 0.42, 0.42), 0.01, 6)
    head = md["head"]
    c = V(0.05, 0.52, 0.36)
    for k in range(12):
        a0, a1 = k * math.pi / 6, (k + 1) * math.pi / 6
        head.rod("light", c + V(math.cos(a0) * 0.1, math.sin(a0) * 0.1, 0), c + V(math.cos(a1) * 0.1, math.sin(a1) * 0.1, 0), 0.018, 6)
    head.box("light", (0.05, 0.08, 0.04), (0.05, 0.43, 0.39))
    glass = md["glass"]
    glass.cyl("glass", 0.085, 0.01, c, seg=16)
    if not B:
        b.merge(lamp); b.merge(head); b.merge(glass); return
    # 망가진 버전: 팔이 꺾여 머리가 책상에 처박힘, 렌즈 깨짐
    T = rot_about((0.05, 0.1, 0.45), (1, 0, 0), -0.75)
    head.xform(T); lamp.xform(T, [v for v in lamp.bm.verts if v.co.y > 0.12 and v.co.z > 0.35])
    glass.bm.clear()
    hc = T @ c
    for i in range(3): glass.box("glass", (rng.uniform(0.03, 0.06), rng.uniform(0.02, 0.05), 0.008), hc + V(rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05), 0), (0, 0, rng.uniform(0, 3)))
    b.merge(lamp); b.merge(head)
    shards(glass, rng, 12, (-0.2, 0.3), (0.3, 0.8))

@model("Consultation", broken=False)
def diag_set(md, B, rng):
    """벽걸이 진단기 세트 (이경·검안경 손잡이 2개 + 코드 + 귀마개 통). 벽 소품: 뒤 y=0, 중심 높이 1.4 m."""
    b = md.body
    Z = 1.4
    b.box("light", (0.32, 0.05, 0.12), (0, 0.025, Z)); b.box("dark", (0.1, 0.004, 0.04), (0, 0.051, Z + 0.02))
    for i, x in enumerate((-0.1, 0.1)):
        b.cyl("dark", 0.025, 0.04, (x, 0.08, Z), seg=10)
        b.cyl("black", 0.016, 0.16, (x, 0.08, Z + 0.1), seg=8)
        b.cyl("dark" if i else "black", 0.02, 0.05, (x, 0.08, Z + 0.2), (0, 0.3 if i else 0, 0), 8)
        pts = [V(x, 0.07, Z - 0.03)] + [V(x + math.cos(t) * 0.025, 0.07 + math.sin(t) * 0.025, Z - 0.05 - t * 0.012) for t in [k * 0.9 for k in range(1, 14)]]
        b.poly("black", pts, 0.004, 4)
    b.box("white", (0.08, 0.06, 0.12), (0.22, 0.03, Z - 0.02)); b.cyl("blue", 0.02, 0.004, (0.22, 0.062, Z), (math.pi / 2, 0, 0), 8)
