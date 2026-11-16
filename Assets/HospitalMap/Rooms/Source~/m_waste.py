"""A. 의료폐기물 보관실 — Rooms/WasteStorage"""
import math
from mathutils import Vector, Matrix
from rmlib import *

def biohazard(m, x, y, z, r, rot=(math.pi / 2, 0, 0), col="yellow"):
    """생물위해 마크 (원판 + 링 3개를 단순화). rot 기본값 = +Y 를 바라보는 면."""
    R = Euler(rot).to_matrix()
    for i in range(3):
        a = math.pi / 2 + i * 2 * math.pi / 3
        off = R @ V(math.cos(a) * r * 0.42, math.sin(a) * r * 0.42, 0)
        m.cyl(col, r * 0.42, 0.003, (x + off.x, y + off.y, z + off.z), rot, 10)
    m.cyl(col, r * 0.18, 0.004, (x, y, z), rot, 8)

def syringe(m, x, y, z, yaw):
    s = M()
    s.cyl("white", 0.007, 0.08, (0, 0, 0.04), seg=6); s.cyl("steel", 0.0012, 0.03, (0, 0, 0.095), seg=4)
    s.cyl("white", 0.012, 0.004, (0, 0, 0.0), seg=6)
    s.xform(Matrix.Translation(V(x, y, z + 0.008)) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(math.pi / 2, 4, 'X'))
    m.merge(s)

@model("WasteStorage")
def waste_bucket(md, B, rng):
    """의료폐기물 원형 버킷 (손잡이, 뚜껑)."""
    b = md.body
    pail, lid = md["pail"], md["lid"]
    pail.cyl("white", 0.17, 0.4, (0, 0, 0.2), r2=0.2, seg=16, caps=False)
    pail.cyl("white", 0.17, 0.01, (0, 0, 0.005), seg=16)
    pail.cyl("white", 0.165, 0.38, (0, 0, 0.2), r2=0.195, seg=16, caps=False)   # 안쪽 벽 (뒤집힌 노멀 대신 두 겹)
    for z in (0.12, 0.3): pail.cyl("white", 0.19 if z > 0.2 else 0.18, 0.015, (0, 0, z), seg=16)
    biohazard(pail, 0, 0.19, 0.2, 0.06)
    pail.box("white", (0.14, 0.004, 0.06), (0, 0.186, 0.08), (-0.08, 0, 0))
    for s in (-1, 1): pail.cyl("light", 0.015, 0.02, (s * 0.2, 0, 0.37), (0, math.pi / 2, 0), 8)
    handle = [(s * 0.205, 0, 0.37) for s in (-1,)] + [(math.cos(a) * 0.205, 0, 0.37 + math.sin(a) * 0.15) for a in [i * math.pi / 8 for i in range(1, 8)]] + [(0.205, 0, 0.37)]
    pail.poly("light", handle, 0.006, 5)
    lid.cyl("white", 0.21, 0.035, (0, 0, 0.415), seg=16); lid.cyl("white", 0.17, 0.02, (0, 0, 0.44), seg=16)
    if not B:
        b.merge(pail); b.merge(lid); return
    # 망가진 버전: 옆으로 넘어지고 찌그러짐, 뚜껑은 굴러가 있음, 내용물 쏟아짐
    pail.densify(0.06, 3); pail.dent((0.15, 0.1, 0.3), 0.15, 0.04, (-1, -0.5, 0)); pail.crumple(0.004, 8, 1)
    pail.xform(Matrix.Translation(V(0, 0, 0.2)) @ Matrix.Rotation(-math.pi / 2 + 0.08, 4, 'X') @ Matrix.Translation(V(0, 0, -0.2)))
    to_floor([pail]); pail.xform(Matrix.Rotation(0.5, 4, 'Z'))
    lid.xform(Matrix.Translation(V(-0.45, 0.3, -0.415 + 0.0)) @ Matrix.Rotation(0.2, 4, 'Y')); to_floor([lid])
    b.merge(pail); md.parts.pop("pail")
    d = md["debris"]; d.merge(lid); md.parts.pop("lid")
    d.sphere("orange_bag", 1.0, (0.35, 0.45, 0.06), (0.2, 0.15, 0.06), 10, 6); d.crumple(0.015, 8, 2)
    for i in range(5): syringe(d, rng.uniform(0.2, 0.7), rng.uniform(0.3, 0.8), 0, rng.uniform(0, 6))
    scatter(d, rng, ["white", "paper"], 6, (0.1, 0.3), (0.7, 0.9), (0.03, 0.03, 0.01), (0.08, 0.06, 0.03))

@model("WasteStorage")
def sharps_bin(md, B, rng):
    """소형 손상성 폐기물 통 (흰 사각 통 + 주황 비닐)."""
    b = md.body
    box = md["box"]
    box.tub("white", (0.2, 0.17, 0.2), (0, 0, 0), 0.008)
    biohazard(box, 0, 0.087, 0.12, 0.035)
    box.box("white", (0.12, 0.003, 0.04), (0, 0.087, 0.05))
    bag = md["bag"]
    bag.tub("orange_bag", (0.19, 0.16, 0.12), (0, 0, 0.13), 0.004)
    for s in (-1, 1): bag.box("orange_bag", (0.004, 0.2, 0.08), (s * 0.115, 0, 0.22), (0, s * 0.5, 0))
    for s in (-1, 1): bag.box("orange_bag", (0.22, 0.004, 0.08), (0, s * 0.1, 0.22), (s * -0.5, 0, 0))
    biohazard(bag, 0, 0.13, 0.23, 0.03, (math.pi / 2 - 0.5, 0, 0), "black")
    bag.densify(0.04, 2); bag.crumple(0.006, 15, 1)
    box.box("white", (0.08, 0.17, 0.006), (-0.14, 0, 0.21), (0, 0.6, 0))   # 열린 뚜껑
    if not B:
        b.merge(box); b.merge(bag); return
    ms = [box, bag]
    topple(ms, (0, 0.085, 0), (1, 0, 0), math.pi / 2 - 0.1)
    b.merge(box); b.merge(bag); md.parts.pop("box"); md.parts.pop("bag")
    d = md["debris"]
    for i in range(9): syringe(d, rng.uniform(-0.25, 0.3), rng.uniform(0.25, 0.65), 0, rng.uniform(0, 6))
    scatter(d, rng, ["white", "steel"], 6, (-0.2, 0.25), (0.3, 0.6), (0.01, 0.01, 0.005), (0.04, 0.02, 0.01))

@model("WasteStorage")
def waste_bags(md, B, rng):
    """주황 폐기물 비닐봉투 더미 (3~4개, 묶인 매듭)."""
    b = md.body
    spots = [(-0.25, 0.0, 0.0, 0.26), (0.2, -0.05, 0.0, 0.3), (0.0, 0.3, 0.0, 0.24), (0.0, 0.05, 0.38, 0.2)]
    for i, (x, y, z, r) in enumerate(spots):
        bag = M()
        bag.sphere("orange_bag", 1.0, (0, 0, r * 0.9), (r, r * 0.85, r * 0.95), 12, 8)
        bag.cyl("orange_bag", 0.025, 0.12, (0, 0, r * 1.85 + 0.05), seg=6, r2=0.06)
        biohazard(bag, 0, r * 0.86, r * 0.9, r * 0.35, col="black")
        bag.densify(0.08, 2); bag.crumple(r * 0.12, 4 / r, i)
        bag.xform(Matrix.Translation(V(x, y, z)) @ Matrix.Rotation(rng.uniform(0, 6), 4, 'Z') @ Matrix.Rotation(0.25 if z else 0, 4, 'X'))
        if B and i == 1:
            # 터진 봉투: 위쪽 반을 지우고 납작하게
            top = [v for v in bag.bm.verts if v.co.z > 0.3]
            bmesh.ops.delete(bag.bm, geom=top, context='VERTS')
            for v in bag.bm.verts: v.co.z *= 0.5
        b.merge(bag)
    if B:
        d = md["debris"]
        for i in range(10): syringe(d, rng.uniform(0.0, 0.9), rng.uniform(0.2, 0.9), 0, rng.uniform(0, 6))
        scatter(d, rng, ["white", "paper", "blue"], 14, (0.0, 0.2), (1.0, 1.0), (0.03, 0.03, 0.004), (0.1, 0.08, 0.02))
        blood(d, rng, 3, (0.2, 0.3), (0.8, 0.8), smin=0.08, smax=0.2)

@model("WasteStorage")
def chest_freezer(md, B, rng):
    """상자형 냉동고. 앞 +Y, 경첩 뒤쪽."""
    W, D, H = 1.0, 0.65, 0.85
    b = md.body
    body = md["fbody"]
    body.tub("white", (W, D, H - 0.04), (0, 0, 0.05), 0.04, inner="light")
    body.box("dark", (W - 0.04, D - 0.04, 0.05), (0, 0, 0.025))
    body.box("light", (W + 0.004, D + 0.004, 0.02), (0, 0, H - 0.06))
    body.box("grey", (0.2, 0.01, 0.06), (W / 2 - 0.15, D / 2 + 0.004, 0.75))
    body.box("o2_green", (0.01, 0.004, 0.01), (W / 2 - 0.1, D / 2 + 0.01, 0.75))
    for x in (-0.4, 0.4):
        for z in (0.25, 0.4): body.box("dark", (0.12, 0.004, 0.006), (x, D / 2 + 0.002, z))
    lid = md["lid"]
    lid.box("white", (W + 0.01, D + 0.01, 0.07), (0, 0, H - 0.015))
    lid.box("light", (W - 0.06, D - 0.06, 0.01), (0, 0, H - 0.055))
    lid.box("grey", (0.3, 0.03, 0.025), (0, D / 2 + 0.02, H - 0.03))
    if not B:
        b.merge(body); b.merge(lid); return
    # 망가진 버전: 뚜껑이 경첩에서 뒤틀려 열림, 녹·찌그러짐, 안에 성에 낀 상자
    body.densify(0.12, 3); body.dent((W / 2, 0.1, 0.4), 0.25, 0.04, (-1, 0, 0)); body.dent((-0.2, D / 2, 0.3), 0.2, 0.03, (0, -1, 0))
    body.crumple(0.004, 4, 3)
    for i in range(8):
        body.box("rust", (rng.uniform(0.04, 0.14), 0.004, rng.uniform(0.02, 0.08)),
                 (rng.uniform(-0.45, 0.45), D / 2 + 0.003, rng.uniform(0.08, 0.7)))
    body.box("rust", (W - 0.1, 0.004, 0.03), (0, D / 2 + 0.003, 0.07))
    lid.densify(0.15, 2); lid.crumple(0.004, 4, 4)
    p = (0, -D / 2, H)
    lid.xform(rot_about(p, (1, 0, 0), -1.75) @ rot_about((W / 2, -D / 2, H), (0, 1, 0), 0.12))
    inside = md["inside"]
    for i in range(5):
        inside.box(rng.choice(("cardboard", "white", "frost")), (rng.uniform(0.15, 0.3), rng.uniform(0.15, 0.25), rng.uniform(0.1, 0.25)),
                   (rng.uniform(-0.3, 0.3), rng.uniform(-0.15, 0.15), 0.15), (0, 0, rng.uniform(-0.3, 0.3)))
    inside.sphere("frost", 1.0, (0, 0, 0.55), (0.42, 0.25, 0.12), 10, 6); inside.crumple(0.02, 6, 5)
    b.merge(body); b.merge(lid); b.merge(inside)
    for k in ("fbody", "lid", "inside"): md.parts.pop(k)
    blood(md["debris"], rng, 2, (-0.4, 0.4), (0.3, 0.8), smin=0.05, smax=0.15)
    for f in md["debris"].bm.faces: f.material_index = md["debris"]._mi("rust")

@model("WasteStorage")
def wire_cart(md, B, rng):
    """철망 바구니 카트 (바퀴 4개, 손잡이)."""
    W, D, H = 0.6, 0.45, 0.9
    b = md.body
    frame = md["frame"]
    for x in (-W / 2, W / 2):
        for y in (-D / 2, D / 2):
            frame.rod("steel", (x, y, 0.1), (x, y, 0.55), 0.01, 6); frame.caster((x, y, 0), 0.04)
    frame.poly("steel", [(-W / 2 - 0.05, -D / 2, 0.55), (-W / 2 - 0.08, -D / 2, H), (W / 2 + 0.08, -D / 2, H), (W / 2 + 0.05, -D / 2, 0.55)], 0.012, 6)
    basket = md["basket"]
    z0, z1 = 0.3, 0.65
    for z in (z0, z0 + 0.12, z0 + 0.24, z1):
        basket.poly("steel", [(-W / 2, -D / 2, z), (W / 2, -D / 2, z), (W / 2, D / 2, z), (-W / 2, D / 2, z)], 0.005, 4, closed=True)
    n = 8
    for i in range(n + 1):
        x = -W / 2 + W * i / n
        basket.rod("steel", (x, -D / 2, z0), (x, -D / 2, z1), 0.003, 4); basket.rod("steel", (x, D / 2, z0), (x, D / 2, z1), 0.003, 4)
        basket.rod("steel", (x, -D / 2, z0), (x, D / 2, z0), 0.003, 4)
    for j in range(6):
        y = -D / 2 + D * j / 5
        basket.rod("steel", (-W / 2, y, z0), (-W / 2, y, z1), 0.003, 4); basket.rod("steel", (W / 2, y, z0), (W / 2, y, z1), 0.003, 4)
    frame.rod("steel", (-W / 2, -D / 2, 0.15), (W / 2, -D / 2, 0.15), 0.008, 6); frame.rod("steel", (-W / 2, D / 2, 0.15), (W / 2, D / 2, 0.15), 0.008, 6)
    if not B:
        b.merge(frame); b.merge(basket); return
    # 망가진 버전: 옆으로 넘어짐, 바구니 찌그러짐, 바퀴 하나 빠짐, 내용물 쏟아짐
    basket.dent((W / 2, 0, 0.5), 0.25, 0.08, (-1, 0, 0)); basket.crumple(0.01, 5, 6)
    gone = [v for v in frame.bm.verts if v.co.z < 0.13 and v.co.x > 0 and v.co.y > 0]
    bmesh.ops.delete(frame.bm, geom=gone, context='VERTS')
    ms = [frame, basket]
    topple(ms, (W / 2, 0, 0), (0, 1, 0), math.pi / 2 - 0.05)
    b.merge(frame); b.merge(basket); md.parts.pop("frame"); md.parts.pop("basket")
    d = md["debris"]
    d.caster((-0.3, 0.6, 0), 0.04)
    d.sphere("orange_bag", 1.0, (0.9, 0.35, 0.12), (0.22, 0.18, 0.12), 10, 6); d.crumple(0.02, 6, 7)
    sheets(d, rng, 5, (0.8, -0.3), (1.4, 0.6))
    scatter(d, rng, ["white", "cardboard"], 5, (0.8, -0.3), (1.4, 0.6), (0.05, 0.05, 0.02), (0.12, 0.1, 0.06))

@model("WasteStorage")
def biohazard_sign(md, B, rng):
    """생물위해 안내문 (A4, 벽 부착). 벽 소품: 뒤 y=0, 중심 높이 1.5 m."""
    b = md.body
    W, H, Z = 0.3, 0.21, 1.5
    sheet = md["sheet"]
    sheet.plate("white", (W, H), (0, 0.001, Z), t=0.002)
    sheet.plate("o2_green", (W - 0.01, 0.012), (0, 0.0025, Z + H / 2 - 0.012), t=0.001)
    biohazard(sheet, -W / 2 + 0.05, 0.003, Z, 0.04, col="o2_green")
    for i in range(5):
        sheet.plate("dark", (rng.uniform(0.12, 0.18), 0.006), (0.035, 0.003, Z + 0.05 - i * 0.025), t=0.001)
    for x in (-W / 2 + 0.02, W / 2 - 0.02): b.box("grey", (0.03, 0.002, 0.015), (x, 0.003, Z + H / 2 - 0.005))   # 테이프
    if not B:
        b.merge(sheet); return
    # 망가진 버전: 오른쪽 아래가 찢겨 바닥에, 남은 부분은 말려 처짐
    sheet.densify(0.03, 3)
    lower_right = [v for v in sheet.bm.verts if v.co.x > 0.0 and v.co.z < Z + 0.02]
    sheet.xform(rot_about((0.0, 0.002, Z + 0.02), (1, 0, 0), -0.6), lower_right)
    sheet.crumple(0.004, 20, 8)
    b.merge(sheet); md.parts.pop("sheet")
    piece = md["debris"]
    piece.box("white", (0.13, 0.09, 0.002), (0.15, 0.4, 0.001), (0, 0, 0.6))
    piece.box("dark", (0.08, 0.006, 0.0015), (0.15, 0.4, 0.0025), (0, 0, 0.6))

import bmesh  # noqa: E402
