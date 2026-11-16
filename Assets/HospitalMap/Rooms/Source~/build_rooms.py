"""Rooms 소품 빌더 (Blender 5.2, 헤드리스).

  blender -b --factory-startup --python-exit-code 1 -P build_rooms.py -- [이름|공간...] [--preview DIR] [--no-export]

병원 공간별 새 소품을 만들어 Assets/HospitalMap/Rooms/<공간>/Models/<이름>.fbx (+ <이름>_broken.fbx) 로 내보낸다.
인자가 없으면 전부. 이름 대신 공간 폴더 이름(Ward, Pharmacy …)을 주면 그 공간 전체.
--preview DIR : 내보낸 FBX 를 다시 가져와 공간별로 한 장씩 미리보기 PNG 를 만든다 (깨끗한 버전 위, 망가진 버전 아래).
모델 정의: m_common.py, m_waste.py, m_pharmacy.py, m_cold.py, m_nurse.py, m_pt.py, m_consult.py, m_ward.py
"""
import bpy, sys, os, math, random, traceback
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from mathutils import Vector
import rmlib
from rmlib import REG, Model, reset, export_fbx, V
import m_common, m_waste, m_pharmacy, m_cold, m_nurse, m_pt, m_consult, m_ward  # noqa: F401 (등록)

ROOMS = os.path.normpath(os.path.join(HERE, ".."))

def out_path(room, fname): return os.path.join(ROOMS, room, "Models", fname + ".fbx")

def build(name, broken):
    room, fn, _ = REG[name]
    fname = name + ("_broken" if broken else "")
    reset()
    md = Model(fname)
    fn(md, broken, random.Random(sum(map(ord, fname))))
    objs = md.build_objects()
    tris = sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)
    return room, fname, tris

def _world_bounds(objs):
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    return (Vector([min(p[k] for p in pts) for k in range(3)]), Vector([max(p[k] for p in pts) for k in range(3)]))

def preview_row(room, fnames, path, per_row=6):
    """fnames 를 X 방향 한 줄(per_row 개씩 여러 줄)로 늘어놓고 앞-위에서 정사영으로 찍는다."""
    reset()
    sc = bpy.context.scene
    x = 0.0; row = 0; zoff = 0.0; row_h = 0.0
    for i, f in enumerate(fnames):
        if i and i % per_row == 0:
            zoff -= row_h + 0.6; x = 0.0; row_h = 0.0
        before = set(sc.objects)
        bpy.ops.import_scene.fbx(filepath=out_path(room, f))
        new = [o for o in sc.objects if o not in before and o.type == 'MESH']
        mn, mx = _world_bounds(new)
        off = Vector((x - mn.x, -(mn.y + mx.y) / 2, zoff - mn.z))
        for o in new:
            if o.parent is None: o.location += off
        x += (mx.x - mn.x) + 0.4; row_h = max(row_h, mx.z - mn.z)
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_shadows = False; sh.show_cavity = True
    sc.display.shading.show_object_outline = True
    sc.render.resolution_x = 2000; sc.render.resolution_y = 1000
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.clip_end = 200
    d = Vector((0.45, 1.0, 0.45)).normalized()
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.view_layer.update()
    # 카메라 좌표계로 투영한 범위로 화면 맞추기
    allm = [o for o in sc.objects if o.type == 'MESH']
    pts = [o.matrix_world @ v.co for o in allm for v in o.data.vertices]
    R = cam.rotation_euler.to_matrix(); Ri = R.transposed()
    loc = [Ri @ p for p in pts]
    lo = Vector([min(p[k] for p in loc) for k in range(3)]); hi = Vector([max(p[k] for p in loc) for k in range(3)])
    ctr = R @ ((lo + hi) / 2)
    cam.location = ctr + d * 60
    w, h = hi.x - lo.x, hi.y - lo.y
    cam.data.ortho_scale = max(w, h * 2) * 1.15
    sc.view_settings.view_transform = "Standard"
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)

def preview(room, fnames, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    clean = [f for f in fnames if not f.endswith("_broken")]
    broken = [f for f in fnames if f.endswith("_broken")]
    preview_row(room, clean, os.path.join(out_dir, room + "_clean.png"))
    if broken: preview_row(room, broken, os.path.join(out_dir, room + "_broken.png"))

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    preview_dir = None; do_export = True; sel = []
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--preview": preview_dir = argv[i + 1]; i += 2; continue
        if a == "--no-export": do_export = False
        else: sel.append(a)
        i += 1
    names = [n for n, (room, _, _) in REG.items() if not sel or n in sel or room in sel]
    done = {}; failed = []
    for n in names:
        for br in ((False, True) if REG[n][2] else (False,)):
            try:
                room, fname, tris = build(n, br)
                if do_export: export_fbx(out_path(room, fname))
                done.setdefault(room, []).append(fname)
                print(f"OK {room}/{fname} tris={tris}")
            except Exception:
                failed.append(n + ("_broken" if br else ""))
                traceback.print_exc()
    if preview_dir:
        for room, fnames in done.items(): preview(room, fnames, preview_dir)
    print("FAILED", failed)
    if failed: sys.exit(1)

main()
