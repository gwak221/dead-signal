"""F. 물리치료실 — Rooms/PhysicalTherapy"""
import math
from mathutils import Vector, Matrix
from rmlib import *

def _knobs(m, x0, y, z, n, col="black", r=0.012):
    for i in range(n): m.cyl(col, r, 0.015, (x0 + i * 0.04, y + 0.0075, z), (math.pi / 2, 0, 0), 8)

@model("PhysicalTherapy")
def therapy_bed(md, B, rng):
    """치료 침대 (나무 프레임 + 노란 매트 + 베개). 0.8 × 1.9 × 0.62 m, 길이 방향 Y."""
    W, L, H = 0.8, 1.9, 0.5
    b = md.body
    frame = md["frame"]
    for x in (-W / 2 + 0.04, W / 2 - 0.04):
        for y in (-L / 2 + 0.04, L / 2 - 0.04): frame.box("wood_light", (0.06, 0.06, H), (x, y, H / 2))
    for x in (-W / 2 + 0.02, W / 2 - 0.02): frame.box("wood_light", (0.03, L, 0.1), (x, 0, H - 0.06))
    for y in (-L / 2 + 0.02, L / 2 - 0.02): frame.box("wood_light", (W, 0.03, 0.1), (0, y, H - 0.06))
    frame.box("wood_light", (W - 0.06, L - 0.06, 0.02), (0, 0, H - 0.02))
    frame.box("wood_light", (W - 0.1, L - 0.1, 0.015), (0, 0, 0.12))
    mat_ = md["mat"]
    mat_.box("mattress", (W, L, 0.1), (0, 0, H + 0.05))
    for x in (-W / 2 + 0.002, W / 2 - 0.002):
        for k in range(6): mat_.box("brown", (0.004, 0.005, 0.1), (x, -L / 2 + 0.2 + k * 0.3, H + 0.05))
    pil = md["pillow"]; pil.sphere("mattress", 1.0, (0, L / 2 - 0.2, H + 0.13), (0.3, 0.15, 0.05), 10, 6); pil.crumple(0.006, 8, 1)
    if not B:
        b.merge(frame); b.merge(mat_); b.merge(pil); return
    # 망가진 버전: 앞 오른쪽 다리 부러져 기울어짐, 매트 찢어지고 속 스펀지 튀어나옴, 얼룩
    gone = [v for v in frame.bm.verts if v.co.x > W / 2 - 0.08 and v.co.y > L / 2 - 0.08 and v.co.z < H - 0.12]
    bmesh.ops.delete(frame.bm, geom=gone, context='VERTS')
    mat_.densify(0.1, 3); mat_.dent((0.1, 0.2, H + 0.1), 0.25, 0.05, (0, 0, -1)); mat_.crumple(0.008, 5, 2)
    for i in range(6): mat_.sphere("foam", 1.0, (rng.uniform(-0.1, 0.3), rng.uniform(0.0, 0.45), H + 0.08), (rng.uniform(0.04, 0.1), rng.uniform(0.04, 0.1), 0.04), 6, 4)
    mat_.cyl("blood", 0.18, 0.003, (-0.15, -0.4, H + 0.101), seg=7)
    ms = [frame, mat_]
    # 꺾인 다리 쪽 모서리를 내림: 그 모서리에서 멀어질수록 덜 내려가게
    for m in ms:
        for v in m.bm.verts:
            f = max(0.0, (v.co.x + W / 2) / W) * max(0.0, (v.co.y + L / 2) / L)
            v.co.z -= 0.36 * f * (1 if v.co.z > 0.1 or m is mat_ else 0)
    for p in ms: b.merge(p)
    md["debris"].merge(pil)
    md["debris"].xform(Matrix.Translation(V(0.5, 0.9, -H - 0.08)))
    for v in md["debris"].bm.verts: v.co.z = max(v.co.z, 0.0)
    scatter(md["debris"], rng, ["foam", "wood_light"], 8, (0.3, 0.6), (0.9, 1.3), (0.03, 0.03, 0.02), (0.1, 0.06, 0.04))

@model("PhysicalTherapy")
def estim_unit(md, B, rng):
    """전기치료기 (빨간 카트형, 위에 경사 조작부). 0.45 × 0.45 × 1.0 m."""
    W, D = 0.45, 0.42
    b = md.body
    body = md["case"]
    body.box("red", (W, D, 0.72), (0, 0, 0.1 + 0.36)); body.box("dark", (W - 0.06, 0.004, 0.25), (0, D / 2 + 0.002, 0.3))
    for i in range(6): body.box("black", (W - 0.1, 0.005, 0.01), (0, D / 2 + 0.003, 0.2 + i * 0.04))
    body.box("white", (0.2, 0.004, 0.05), (0, D / 2 + 0.002, 0.7)); body.box("black", (0.15, 0.005, 0.02), (0, D / 2 + 0.004, 0.7))
    panel = md["panel"]
    panel.box("dark", (W, D, 0.12), (0, 0, 0.88), (-0.35, 0, 0))
    panel.box("screen", (0.18, 0.004, 0.08), (-0.08, D / 2 * 0.95 - 0.02, 0.9), (-0.35 + math.pi / 2 - math.pi / 2, 0, 0))
    _knobs(panel, 0.06, D / 2 * 0.9 - 0.02, 0.86, 4)
    for x in (-W / 2 + 0.04, W / 2 - 0.04):
        for y in (-D / 2 + 0.04, D / 2 - 0.04): body.caster((x, y, 0), 0.035, "black")
    cab = md["cable"]
    for i, col in enumerate(("black", "red", "white")):
        x = W / 2 + 0.01
        cab.poly(col, [(x, 0.1 - i * 0.04, 0.85), (x + 0.04, 0.1 - i * 0.04, 0.6), (x + 0.02, 0.05 - i * 0.04, 0.35), (x + 0.05, 0.0 - i * 0.04, 0.4), (x + 0.03, -0.05, 0.65)], 0.005, 5)
    cab.cyl("black", 0.03, 0.08, (W / 2 + 0.06, -0.15, 0.55), (0, math.pi / 2, 0), 8)
    if not B:
        for p in (body, panel, cab): b.merge(p)
        return
    # 망가진 버전: 뒤로 넘어짐, 조작부 부서져 속이 보임, 손잡이 떨어짐
    panel.densify(0.06, 2); panel.dent((0, 0.1, 0.92), 0.15, 0.04, (0, -0.4, -1)); panel.crumple(0.005, 12, 2)
    panel.box("grime", (W - 0.08, 0.004, 0.08), (0, D / 2 * 0.9 - 0.03, 0.9), (-0.35, 0, 0))
    ms = [body, panel, cab]
    topple(ms, (0, -D / 2 - 0.02, 0), (1, 0, 0), -math.pi / 2 + 0.06)
    for p in ms: b.merge(p)
    d = md["debris"]
    for i in range(4): d.cyl("black", 0.012, 0.015, (rng.uniform(-0.3, 0.3), rng.uniform(0.3, 0.6), 0.006), seg=8)
    scatter(d, rng, ["dark", "red", "screen"], 8, (-0.4, 0.2), (0.4, 0.6), (0.01, 0.01, 0.004), (0.05, 0.04, 0.01))

@model("PhysicalTherapy")
def therapy_cart(md, B, rng):
    """치료기기 카트 (회색 3단, 위에 모니터 달린 기기). 0.55 × 0.45 × 1.2 m."""
    W, D = 0.55, 0.45
    b = md.body
    cart = md["cart"]
    for x in (-W / 2 + 0.03, W / 2 - 0.03):
        for y in (-D / 2 + 0.03, D / 2 - 0.03): cart.box("light", (0.03, 0.03, 0.75), (x, y, 0.47)); cart.caster((x, y, 0), 0.035, "dark")
    for z in (0.15, 0.48, 0.82): cart.box("light", (W, D, 0.025), (0, 0, z))
    dev = md["device"]
    dev.box("white", (0.45, 0.35, 0.16), (0, 0, 0.91)); dev.box("dark", (0.3, 0.004, 0.06), (0, 0.177, 0.93))
    _knobs(dev, -0.1, 0.175, 0.87, 6, "dark", 0.01)
    dev.box("light", (0.04, 0.04, 0.25), (0, -0.12, 1.1)); dev.box("white", (0.38, 0.05, 0.26), (0, -0.1, 1.3), (-0.15, 0, 0))
    dev.box("screen", (0.33, 0.004, 0.2), (0, -0.07, 1.31), (-0.15, 0, 0))
    dev.box("white", (0.4, 0.3, 0.14), (0, 0, 0.56)); dev.box("dark", (0.15, 0.004, 0.03), (0.08, 0.152, 0.58))
    dev.poly("light", [(0.2, 0.1, 0.96), (0.3, 0.2, 0.9), (0.33, 0.25, 0.6), (0.3, 0.2, 0.4)], 0.008, 6)
    dev.box("white", (0.05, 0.05, 0.12), (0.3, 0.2, 0.35))
    if not B:
        b.merge(cart); b.merge(dev); return
    # 망가진 버전: 모니터가 꺾여 앞으로 처지고 화면 깨짐, 위 기기는 반쯤 떨어짐
    mon = [v for v in dev.bm.verts if v.co.z > 1.14]
    dev.xform(rot_about((0, -0.12, 1.2), (1, 0, 0), 1.0), mon)
    for i in range(5):
        a = i * 1.2
        dev.box("light", (0.12, 0.004, 0.003), (0.05 + math.cos(a) * 0.04, 0.12, 1.15 + math.sin(a) * 0.03), (0, a, 0))
    top = [v for v in dev.bm.verts if 0.83 < v.co.z < 1.0]
    dev.xform(rot_about((W / 2, 0, 0.83), (0, 1, 0), -0.35) @ Matrix.Translation(V(0.08, 0, 0)), top)
    cart.densify(0.1, 2); cart.crumple(0.004, 6, 3)
    b.merge(cart); b.merge(dev)
    scatter(md["debris"], rng, ["screen", "glass", "white"], 10, (-0.4, 0.3), (0.5, 0.8), (0.01, 0.01, 0.004), (0.05, 0.03, 0.008))

@model("PhysicalTherapy")
def infrared_lamp(md, B, rng):
    """적외선 램프 스탠드 (바퀴 5개 받침, 관절 팔, 둥근 머리)."""
    b = md.body
    lamp = md["lamp"]
    lamp.star_base(0.08, 0.28, 0.03, "black")
    lamp.rod("light", (0, 0, 0.1), (0, 0, 1.15), 0.016, 8)
    lamp.cyl("dark", 0.025, 0.06, (0, 0, 1.15), seg=8)
    lamp.rod("light", (0, 0, 1.17), (0, 0.35, 1.45), 0.012, 8)
    lamp.cyl("dark", 0.02, 0.05, (0, 0.35, 1.45), (0, math.pi / 2, 0), 8)
    c = V(0, 0.45, 1.35)
    lamp.cyl("dark", 0.15, 0.12, c, (-0.7, 0, 0), 16, r2=0.11)
    lamp.cyl("orange", 0.13, 0.01, c + V(0, 0.04, -0.05), (-0.7, 0, 0), 16)
    lamp.box("dark", (0.12, 0.1, 0.08), (0, -0.05, 0.9)); lamp.box("black", (0.05, 0.004, 0.03), (0, 0.0, 0.92))
    if not B:
        b.merge(lamp); return
    # 망가진 버전: 바닥에 쓰러짐, 머리 깨져 붉은 유리 조각
    topple([lamp], (0, 0.3, 0), (1, 0, 0), 1.45)
    lamp.xform(Matrix.Rotation(-0.5, 4, 'Z'))
    b.merge(lamp)
    shards(md["glass"], rng, 14, (-0.9, 1.0), (0.2, 1.8), mat_="orange")

@model("PhysicalTherapy", broken=False)
def ultrasound_unit(md, B, rng):
    """초음파 치료기 (작은 바퀴 테이블 위 기기 + 프로브)."""
    b = md.body
    b.box("white", (0.45, 0.4, 0.02), (0, 0, 0.72)); b.box("white", (0.45, 0.4, 0.02), (0, 0, 0.2))
    for x in (-0.2, 0.2):
        for y in (-0.17, 0.17): b.rod("steel", (x, y, 0.08), (x, y, 0.72), 0.012, 6); b.caster((x, y, 0), 0.03, "steel")
    b.box("light", (0.36, 0.28, 0.12), (0, -0.02, 0.79), (-0.15, 0, 0))
    b.box("screen", (0.14, 0.004, 0.06), (-0.07, 0.12, 0.81), (-0.15, 0, 0))
    _knobs(b, 0.04, 0.115, 0.79, 3, "dark", 0.01)
    b.box("light", (0.04, 0.04, 0.03), (0.15, 0.12, 0.85))
    b.cyl("white", 0.018, 0.14, (0.2, 0.05, 0.88), (0.3, 0, 0), 8)
    b.poly("dark", [(0.2, 0.0, 0.82), (0.24, -0.05, 0.6), (0.18, -0.1, 0.45), (0.1, -0.12, 0.62), (0.0, -0.1, 0.78)], 0.005, 5)
    b.cyl("cream", 0.04, 0.1, (-0.12, 0.08, 0.28), seg=10)

@model("PhysicalTherapy")
def exercise_bike(md, B, rng):
    """실내 자전거 (앞 플라이휠 커버, 안장, 핸들, 계기판). 길이 방향 Y, 앞 +Y."""
    b = md.body
    bike = md["bike"]
    bike.box("dark", (0.5, 0.06, 0.06), (0, 0.45, 0.03)); bike.box("dark", (0.5, 0.06, 0.06), (0, -0.45, 0.03))
    bike.rod("dark", (0, -0.45, 0.05), (0, 0.45, 0.05), 0.03, 8)
    bike.rod("grey", (0, -0.3, 0.06), (0, -0.15, 0.75), 0.03, 8)
    bike.rod("grey", (0, 0.35, 0.06), (0, 0.25, 0.95), 0.03, 8)
    bike.rod("grey", (0, -0.15, 0.35), (0, 0.3, 0.45), 0.025, 8)
    bike.cyl("light", 0.24, 0.12, (0, 0.25, 0.38), (0, math.pi / 2, 0), 20)
    bike.cyl("dark", 0.06, 0.13, (0, 0.25, 0.38), (0, math.pi / 2, 0), 10)
    bike.box("black", (0.22, 0.28, 0.07), (0, -0.17, 0.82), (0.08, 0, 0))
    bike.rod("grey", (0, 0.25, 0.95), (0, 0.2, 1.1), 0.02, 8)
    bike.poly("black", [(-0.22, 0.12, 1.12), (-0.2, 0.2, 1.1), (0.2, 0.2, 1.1), (0.22, 0.12, 1.12)], 0.016, 6)
    bike.box("dark", (0.18, 0.08, 0.12), (0, 0.27, 1.12), (-0.4, 0, 0)); bike.box("screen", (0.12, 0.004, 0.06), (0, 0.24, 1.15), (-0.4 - math.pi / 2 + math.pi / 2, 0, 0))
    for s in (-1, 1):
        bike.rod("steel", (s * 0.07, 0.0, 0.38), (s * 0.07, 0.0 + s * 0.12, 0.38 + 0.12), 0.01, 5)
        bike.box("black", (0.1, 0.05, 0.02), (s * 0.12, s * 0.12, 0.5))
    if not B:
        b.merge(bike); return
    # 망가진 버전: 옆으로 넘어짐, 안장 빠져 바닥에, 계기판 깨짐
    seat = [v for v in bike.bm.verts if v.co.z > 0.77 and v.co.y < 0.0]
    bmesh.ops.delete(bike.bm, geom=seat, context='VERTS')
    bike.densify(0.12, 2); bike.dent((0.1, 0.25, 0.5), 0.15, 0.03, (-1, 0, 0))
    topple([bike], (0.25, 0, 0), (0, 1, 0), math.pi / 2 - 0.12)
    b.merge(bike)
    d = md["debris"]
    st = M(); st.box("black", (0.22, 0.28, 0.07), (0, 0, 0.035)); st.densify(0.05, 2); st.crumple(0.008, 10, 2)
    st.xform(Matrix.Translation(V(-0.3, -0.7, 0)) @ Matrix.Rotation(0.8, 4, 'Z')); d.merge(st)
    d.rod("grey", (-0.15, -0.5, 0.025), (0.1, -0.55, 0.025), 0.02, 8)
    shards(d, rng, 6, (-0.4, 0.5), (0.0, 0.9), mat_="screen", smin=0.01, smax=0.03)

@model("PhysicalTherapy")
def massage_chair(md, B, rng):
    """안마의자 (검은 가죽, 큰 등받이·팔걸이·다리받침). 앞 +Y."""
    b = md.body
    ch = md["chair"]
    ch.box("dark", (0.7, 0.75, 0.35), (0, 0.0, 0.175))
    ch.box("leather", (0.55, 0.6, 0.15), (0, 0.05, 0.42))
    for s in (-1, 1):
        ch.box("leather", (0.16, 0.85, 0.35), (s * 0.35, -0.02, 0.55)); ch.box("dark", (0.17, 0.3, 0.04), (s * 0.35, 0.25, 0.73))
    back = M()
    back.box("leather", (0.62, 0.3, 0.95), (0, -0.4, 0.95), (-0.35, 0, 0))
    back.box("leather", (0.66, 0.35, 0.25), (0, -0.55, 1.4), (-0.35, 0, 0))
    back.box("dark", (0.5, 0.05, 0.7), (0, -0.25, 0.9), (-0.35, 0, 0))
    leg = M()
    leg.box("leather", (0.5, 0.18, 0.5), (0, 0.48, 0.25), (0.3, 0, 0)); leg.box("dark", (0.4, 0.02, 0.4), (0, 0.57, 0.27), (0.3, 0, 0))
    ch.box("dark", (0.12, 0.08, 0.05), (0.4, 0.3, 0.78)); ch.box("screen", (0.08, 0.004, 0.035), (0.4, 0.34, 0.78))
    if not B:
        ch.merge(back); ch.merge(leg); b.merge(ch); return
    # 망가진 버전: 가죽 찢어져 스펀지 노출, 다리받침 떨어져 바닥에, 등받이 뒤로 꺾임
    back.densify(0.12, 2); back.crumple(0.012, 5, 2); back.xform(rot_about((0, -0.3, 0.5), (1, 0, 0), 0.35))
    for i in range(7): back.sphere("foam", 1.0, (rng.uniform(-0.2, 0.2), rng.uniform(-0.35, -0.25), rng.uniform(0.8, 1.2)), (rng.uniform(0.04, 0.09), 0.04, rng.uniform(0.04, 0.09)), 6, 4)
    ch.densify(0.12, 2); ch.crumple(0.008, 6, 3)
    for i in range(4): ch.sphere("foam", 1.0, (rng.uniform(-0.2, 0.2), rng.uniform(-0.1, 0.2), 0.5), (rng.uniform(0.04, 0.08), rng.uniform(0.04, 0.08), 0.03), 6, 4)
    ch.merge(back); b.merge(ch)
    leg.xform(Matrix.Translation(V(0.35, 0.65, 0)) @ Matrix.Rotation(0.6, 4, 'Z') @ rot_about((0, 0.48, 0.25), (1, 0, 0), 1.2)); to_floor([leg])
    md["debris"].merge(leg)
    scatter(md["debris"], rng, ["foam", "leather"], 8, (-0.4, 0.4), (0.5, 1.0), (0.02, 0.02, 0.01), (0.08, 0.06, 0.03))

@model("PhysicalTherapy")
def sling_frame(md, B, rng):
    """슬링 서스펜션 장비 (천장 프레임 + 줄 6개 + 패드). 천장 소품: 윗면 z=0. 1.0 × 2.0 m, 줄 끝 높이 약 -1.9."""
    b = md.body
    fr = md["frame"]
    fr.poly("steel", [(-0.5, -1.0, -0.04), (0.5, -1.0, -0.04), (0.5, 1.0, -0.04), (-0.5, 1.0, -0.04)], 0.02, 6, closed=True)
    for y in (-0.5, 0.0, 0.5): fr.rod("steel", (-0.5, y, -0.04), (0.5, y, -0.04), 0.015, 6)
    for x in (-0.5, 0.5):
        for y in (-1.0, 1.0): fr.box("steel", (0.06, 0.06, 0.02), (x, y, -0.01))
    ropes = [(-0.25, -0.6, 1.75), (0.25, -0.6, 1.75), (-0.2, 0.0, 1.85), (0.2, 0.0, 1.85), (-0.15, 0.6, 1.8), (0.15, 0.6, 1.8)]
    rp = md["ropes"]
    def rope(x, y, L, cut=False):
        rp.rod("black", (x, y, -0.05), (x, y, -L), 0.008, 5)
        rp.cyl("steel", 0.018, 0.05, (x, y, -L - 0.02), seg=6)
        if cut: return
        rp.box("black", (0.05, 0.03, 0.18), (x, y, -L - 0.13))
    for x, y, L in ropes: rope(x, y, L)
    pads = md["pads"]
    for y, L in ((-0.6, 1.75), (0.0, 1.85), (0.6, 1.8)):
        pts = [V(math.sin(t) * 0.25, y, -L - 0.22 - math.cos(t) * 0.08 + 0.08) for t in [-math.pi / 2 + k * math.pi / 6 for k in range(7)]]
        pads.poly("black", pts, 0.03, 6)
    if not B:
        b.merge(fr); b.merge(rp); b.merge(pads); return
    # 망가진 버전: 줄 3개 끊어져 짧게 늘어짐, 패드 하나는 한쪽만 매달려 늘어짐, 프레임 한쪽 처짐
    rp.bm.clear()
    for i, (x, y, L) in enumerate(ropes):
        if i in (1, 2, 4): rope(x, y, rng.uniform(0.5, 1.0), cut=True)
        else: rope(x, y, L)
    pads.bm.clear()
    y, L = -0.6, 1.75
    pads.poly("black", [(-0.25, y, -L - 0.22), (-0.23, y + 0.02, -L - 0.45), (-0.18, y + 0.05, -L - 0.62)], 0.03, 6)
    fr.xform(rot_about((-0.5, 0, -0.02), (0, 1, 0), -0.04))
    b.merge(fr); b.merge(rp); b.merge(pads)

@model("PhysicalTherapy")
def treatment_table(md, B, rng):
    """파란 치료 테이블 (얼굴 구멍 머리판). 0.65 × 1.95 × 0.7 m, 머리 +Y."""
    W, L, H = 0.65, 1.6, 0.62
    b = md.body
    base = md["base"]
    for y in (-L / 2 + 0.15, L / 2 - 0.15):
        base.box("white", (0.08, 0.08, H - 0.05), (0, y, (H - 0.05) / 2)); base.box("white", (W - 0.05, 0.1, 0.04), (0, y, 0.02))
    base.box("white", (0.06, L - 0.3, 0.06), (0, 0, 0.25))
    pad = md["pad"]
    pad.box("vinyl_blue", (W, L, 0.09), (0, 0, H + 0.02)); pad.box("light", (W - 0.02, L - 0.02, 0.03), (0, 0, H - 0.04))
    head = md["head"]
    hy = L / 2 + 0.2
    head.box("vinyl_blue", (0.12, 0.34, 0.08), (-0.14, hy, H + 0.02)); head.box("vinyl_blue", (0.12, 0.34, 0.08), (0.14, hy, H + 0.02))
    head.box("vinyl_blue", (0.4, 0.08, 0.08), (0, hy + 0.13, H + 0.02))
    head.box("steel", (0.04, 0.2, 0.03), (0, L / 2 + 0.05, H - 0.04))
    if not B:
        for p in (base, pad, head): b.merge(p)
        return
    # 망가진 버전: 비닐 찢어짐, 머리판 떨어져 바닥에, 한쪽 다리 휘어 기울어짐
    pad.densify(0.1, 3); pad.dent((0.1, -0.2, H + 0.07), 0.25, 0.04, (0, 0, -1)); pad.crumple(0.006, 5, 2)
    for i in range(5): pad.sphere("foam", 1.0, (rng.uniform(-0.1, 0.2), rng.uniform(-0.4, 0.0), H + 0.06), (rng.uniform(0.04, 0.08), rng.uniform(0.04, 0.08), 0.03), 6, 4)
    ms = [base, pad]
    xform_all(ms, rot_about((W / 2, -L / 2, 0), (0, 1, 0), 0.05)); to_floor(ms)
    for p in ms: b.merge(p)
    head.xform(Matrix.Translation(V(0.5, 0.0, -H + 0.02)) @ rot_about((0, hy, H), (0, 0, 1), 0.8)); to_floor([head])
    md["debris"].merge(head)

@model("PhysicalTherapy")
def world_map(md, B, rng):
    """세계지도 포스터. 벽 소품: 뒤 y=0, 중심 높이 1.6 m. 1.2 × 0.8 m."""
    W, H, Z = 1.2, 0.8, 1.6
    b = md.body
    sheet = md["sheet"]
    sheet.plate("cream", (W, H), (0, 0.002, Z), t=0.003)
    sheet.plate("blue", (W - 0.06, H - 0.1), (0, 0.004, Z - 0.02), t=0.001)
    sheet.plate("dark", (0.5, 0.025), (0, 0.004, Z + H / 2 - 0.025), t=0.001)
    land = [(-0.35, 0.12, 0.22, 0.18), (-0.25, -0.12, 0.1, 0.2), (-0.02, 0.15, 0.08, 0.1), (0.02, -0.02, 0.12, 0.2), (0.25, 0.1, 0.3, 0.18), (0.38, -0.2, 0.1, 0.08)]
    for x, z, w, h in land:
        sheet.plate(rng.choice(("yellow", "green", "o2_green", "pink", "orange")), (w, h), (x, 0.005, Z + z), t=0.001)
    if not B:
        b.merge(sheet); return
    # 망가진 버전: 오른쪽 아래 1/3 이 찢겨 말려 내려오고, 한 조각은 바닥에
    sheet.densify(0.06, 3)
    part = [v for v in sheet.bm.verts if v.co.x > 0.1 and v.co.z < Z + 0.05]
    sheet.xform(rot_about((0.1, 0.003, Z + 0.05), (1, 0.3, 0), -0.7), part)
    sheet.crumple(0.006, 8, 3)
    b.merge(sheet)
    pc = md["debris"]; pc.box("blue", (0.3, 0.2, 0.003), (0.3, 0.45, 0.0015), (0, 0, 0.5)); pc.box("cream", (0.32, 0.03, 0.0035), (0.28, 0.36, 0.0015), (0, 0, 0.5))

import bmesh  # noqa: E402
