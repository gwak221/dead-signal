"""0. 공용 — Rooms/Common (여러 공간에서 같이 쓰는 소품) + 다른 모듈이 쓰는 소품 헬퍼"""
import math
from mathutils import Vector, Matrix
from rmlib import *

# ───────────────────────── 헬퍼 (다른 모듈에서도 사용) ─────────────────────────

CAPS = ("blue", "white", "orange", "red", "white", "green", "yellow")

def bottle(m, x, y, z, r, h, cap, body="white", lying=0.0, yaw=0.0):
    """약병 하나. lying=0 이면 세워짐, π/2 면 누움 (바닥 z 에 닿게)."""
    parts = M()
    parts.cyl(body, r, h, (0, 0, h / 2), seg=10)
    parts.cyl(cap, r * 0.95, h * 0.22, (0, 0, h + h * 0.1), seg=10)
    if lying: parts.xform(Matrix.Translation(V(0, 0, r)) @ Matrix.Rotation(lying, 4, 'X'))
    parts.xform(Matrix.Translation(V(x, y, z)) @ Matrix.Rotation(yaw, 4, 'Z'))
    m.merge(parts)

def bottle_row(m, rng, x0, x1, y, z, depth=0.2, maxh=0.16):
    x = x0
    while x < x1 - 0.03:
        r = rng.uniform(0.022, 0.045); h = rng.uniform(0.06, maxh)
        if x + 2 * r > x1: break
        bottle(m, x + r, y + rng.uniform(-depth / 4, depth / 4), z, r, h, rng.choice(CAPS))
        x += 2 * r + rng.uniform(0.004, 0.02)

BOX_COLS = ("white", "white", "blue", "yellow", "pink", "green", "red", "orange", "cream", "purple")

def box_row(m, rng, x0, x1, y, z, depth=0.2, maxh=0.18, cols=BOX_COLS):
    x = x0
    while x < x1 - 0.03:
        w = rng.uniform(0.04, 0.1); h = rng.uniform(0.05, maxh); d = rng.uniform(0.08, depth)
        if x + w > x1: break
        col = rng.choice(cols)
        m.box(col, (w, d, h), (x + w / 2, y, z + h / 2))
        if col != "white": m.box("white", (w + 0.001, d * 0.5, h * 0.35), (x + w / 2, y, z + h * 0.45))
        x += w + rng.uniform(0.002, 0.012)

def books(m, rng, x0, x1, y, z, depth=0.2, cols=("navy", "red", "green", "cream", "brown", "blue", "yellow", "black")):
    x = x0
    while x < x1 - 0.015:
        t = rng.uniform(0.018, 0.045); h = rng.uniform(0.18, 0.27)
        if x + t > x1: break
        m.box(rng.choice(cols), (t, depth * rng.uniform(0.75, 1.0), h), (x + t / 2, y, z + h / 2))
        x += t + 0.002

def monitor_set(m, x, y, z, yaw=0.0, cracked=False):
    """모니터 + 키보드 + 마우스. 원점은 책상 위 (z), 모니터 화면이 +Y."""
    p = M()
    p.box("black", (0.22, 0.16, 0.012), (0, -0.05, 0.006)); p.box("black", (0.05, 0.03, 0.24), (0, -0.07, 0.13))
    p.box("black", (0.56, 0.04, 0.34), (0, -0.04, 0.36)); p.box("screen", (0.52, 0.004, 0.3), (0, -0.018, 0.365))
    if cracked:
        for i in range(6):
            a = i * 1.05 + 0.3
            p.box("light", (0.2, 0.002, 0.004), (0.08 + math.cos(a) * 0.09, -0.015, 0.4 + math.sin(a) * 0.07), (0, a, 0))
    p.box("dark", (0.44, 0.15, 0.022), (0, 0.18, 0.011)); p.box("black", (0.42, 0.13, 0.006), (0, 0.18, 0.024))
    p.box("dark", (0.06, 0.1, 0.03), (0.32, 0.18, 0.015))
    p.xform(Matrix.Translation(V(x, y, z)) @ Matrix.Rotation(yaw, 4, 'Z'))
    m.merge(p)

def chair_parts(seat_col="black", back_h=0.45, high=False):
    """사무용 의자 (바퀴 5개). 반환: (base, seat) — 망가진 버전에서 따로 움직이려고."""
    base, seat = M(), M()
    base.star_base(0.09, 0.3, 0.03, "black")
    base.cyl("steel", 0.025, 0.3, (0, 0, 0.27))
    seat.box("black", (0.3, 0.3, 0.03), (0, 0, 0.43))
    seat.box(seat_col, (0.5, 0.48, 0.08), (0, 0.02, 0.48))
    seat.box("black", (0.06, 0.04, 0.3), (0, -0.25, 0.6))
    bh = 0.7 if high else back_h
    seat.box(seat_col, (0.46 if not high else 0.52, 0.08, bh), (0, -0.27, 0.62 + bh / 2), (-0.12, 0, 0))
    if high: seat.box(seat_col, (0.34, 0.1, 0.18), (0, -0.37, 0.62 + bh + 0.06), (-0.15, 0, 0))
    for s in (-1, 1):
        seat.box("black", (0.04, 0.05, 0.18), (s * 0.27, 0.0, 0.58)); seat.box("black", (0.06, 0.26, 0.03), (s * 0.27, 0.02, 0.68))
    return base, seat

# ───────────────────────── 모델 ─────────────────────────

@model("Common")
def curtain_rail(md, B, rng):
    """천장 커튼 레일 + 칸막이 커튼. 천장 소품: 윗면 z=0. 레일 길이 2.4 m (X), 커튼 길이 2.0 m."""
    L = 2.4
    b = md.body
    rail = md["rail"]
    rail.box("light", (L, 0.035, 0.03), (0, 0, -0.025))
    for x in (-L / 2 + 0.1, 0, L / 2 - 0.1): rail.box("light", (0.05, 0.05, 0.012), (x, 0, -0.006))
    cur = md["curtain"]
    cur.pleat("curtain", -L / 2 + 0.05, 0.2, -0.06, -2.06, 9, 0.05)
    for i in range(19): cur.box("light", (0.01, 0.01, 0.03), (-L / 2 + 0.05 + i * (1.25 / 18), 0, -0.05))
    cur.pleat("white", -L / 2 + 0.05, 0.2, -0.06, -0.3, 9, 0.052)   # 위쪽 망사 띠
    if not B:
        b.merge(rail); md.parts.pop("rail"); md.parts["cloth"] = md.parts.pop("curtain"); return
    # 망가진 버전: 레일 한쪽 고정이 빠져 늘어지고, 커튼은 찢겨 아래가 들쭉날쭉, 일부 고리가 빠져 처짐
    cur.bm.clear()
    cur.pleat("curtain", -L / 2 + 0.05, -0.35, -0.06, -1.7, 6, 0.05, jag=0.5, rng=rng)
    cur.pleat("curtain", -0.3, 0.3, -0.25, -2.1, 3, 0.04, jag=0.3, rng=rng)
    cur.densify(0.2, 2); cur.crumple(0.03, 2.5, 3)
    T = rot_about((-L / 2 + 0.1, 0, -0.025), (0, 1, 0), 0.22)
    rail.xform(T); cur.xform(T)
    b.box("light", (0.05, 0.05, 0.012), (-L / 2 + 0.1, 0, -0.006))
    b.box("grime", (0.08, 0.08, 0.002), (L / 2 - 0.1, 0, -0.001))
    b.merge(rail); md.parts.pop("rail"); md.parts["cloth"] = md.parts.pop("curtain")

@model("Common")
def pill_bottles(md, B, rng):
    """약병 소품 세트 (약 0.4 × 0.2 m). 선반·카트 위에 놓는다."""
    b = md.body
    if not B:
        bottle_row(b, rng, -0.2, 0.2, -0.04, 0, 0.1)
        bottle_row(b, rng, -0.2, 0.2, 0.06, 0, 0.1, 0.11)
        return
    bottle_row(b, rng, -0.2, -0.02, -0.04, 0, 0.1)
    d = md["debris"]
    for i in range(6):
        r = rng.uniform(0.022, 0.04); h = rng.uniform(0.06, 0.14)
        bottle(d, rng.uniform(-0.05, 0.35), rng.uniform(-0.05, 0.3), 0, r, h, rng.choice(CAPS), lying=math.pi / 2, yaw=rng.uniform(0, 6.28))
    for i in range(3): d.cyl(rng.choice(CAPS), 0.03, 0.02, (rng.uniform(0, 0.4), rng.uniform(0.1, 0.4), 0.01), seg=10)
    pills(d, rng, 30, (-0.05, 0.0), (0.45, 0.45))

@model("Common")
def med_boxes(md, B, rng):
    """약 상자 소품 세트 (가로 0.6 m 한 줄 + 위에 몇 개). 선반 위에 놓는다."""
    b = md.body
    if not B:
        box_row(b, rng, -0.3, 0.3, 0, 0)
        box_row(b, rng, -0.25, 0.05, 0, 0.19, maxh=0.06)
        return
    box_row(b, rng, -0.3, -0.05, 0, 0)
    d = md["debris"]
    for i in range(10):
        w, h, dd = rng.uniform(0.05, 0.1), rng.uniform(0.04, 0.06), rng.uniform(0.08, 0.15)
        bx = M(); col = rng.choice(BOX_COLS)
        bx.box(col, (w, dd, h), (0, 0, h / 2)); bx.densify(0.03, 2); bx.crumple(0.006, 20, i)
        bx.xform(Matrix.Translation(V(rng.uniform(-0.1, 0.5), rng.uniform(0.0, 0.5), 0)) @ Matrix.Rotation(rng.uniform(0, 6), 4, 'Z'))
        d.merge(bx)
    sheets(d, rng, 3, (0, 0.1), (0.4, 0.5), w=0.12, h=0.18)

@model("Common")
def monitor(md, B, rng):
    """모니터 + 키보드 + 마우스. 책상 위에 놓는다 (원점 = 책상면), 화면이 +Y."""
    b = md.body
    if not B:
        monitor_set(b, 0, 0, 0); return
    # 망가진 버전: 모니터가 뒤로 넘어지고 화면 깨짐, 키보드 비뚤어짐
    mon = M(); monitor_set(mon, 0, 0, 0, cracked=True)
    # 화면·목(받침 발판 제외)만 뒤로 젖혀 눕힘, 키보드·마우스(y>=0.1)는 비뚤게 돌림
    mon.xform(rot_about((0, -0.13, 0.02), (1, 0, 0), 1.25), [v for v in mon.bm.verts if v.co.y < 0.1 and v.co.z > 0.013])
    for v in mon.bm.verts:
        if v.co.y >= 0.1 and v.co.z < 0.05: v.co = (Matrix.Rotation(0.35, 4, 'Z') @ V(v.co.x, v.co.y, v.co.z)) + V(0.05, 0.05, 0)
    b.merge(mon)
    scatter(md["debris"], rng, ["black", "dark"], 8, (-0.3, 0.1), (0.3, 0.45), (0.012, 0.012, 0.006), (0.018, 0.018, 0.01))

@model("Common")
def office_chair(md, B, rng):
    b = md.body
    base, seat = chair_parts("dark")
    if not B:
        b.merge(base); b.merge(seat); return
    # 망가진 버전: 옆으로 넘어짐, 바퀴 하나 빠짐, 등받이 꺾임, 방석 찢어짐
    seat.densify(0.08, 3); seat.crumple(0.008, 6, 2)
    seat.xform(rot_about((0, -0.25, 0.6), (1, 0, 0), -0.5), [v for v in seat.bm.verts if v.co.y < -0.2 and v.co.z > 0.6])
    caster = [v for v in base.bm.verts if v.co.z < 0.1 and v.co.y > 0.25]
    bmesh.ops.delete(base.bm, geom=caster, context='VERTS')
    ms = [base, seat]
    topple(ms, (0.3, 0, 0), (0, 1, 0), -math.pi / 2 + 0.15)
    b.merge(base); b.merge(seat)
    d = md["debris"]; d.caster((0.7, 0.6, 0), 0.03, "black")
    scatter(d, rng, ["foam"], 5, (0.2, 0.2), (0.7, 0.7), (0.02, 0.02, 0.01), (0.06, 0.05, 0.03))

import bmesh  # noqa: E402 (office_chair 에서 사용)
