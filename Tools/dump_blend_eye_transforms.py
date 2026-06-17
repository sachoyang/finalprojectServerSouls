import json
import mathutils
from pathlib import Path

import bpy


TARGETS = {
    "Green Deep Realistic",
    "Green Deep Realistic.001",
    "cs_eye_aim",
    "cs_eye_aim_global",
    "cs_c_eye",
    "cs_c_eye_offset",
    "cs_yeux",
}


def vec(values):
    return [float(v) for v in values]


def object_info(obj):
    matrix = obj.matrix_world.copy()
    basis = {
        "+X": matrix.to_quaternion() @ mathutils.Vector((1, 0, 0)),
        "+Y": matrix.to_quaternion() @ mathutils.Vector((0, 1, 0)),
        "+Z": matrix.to_quaternion() @ mathutils.Vector((0, 0, 1)),
        "-X": matrix.to_quaternion() @ mathutils.Vector((-1, 0, 0)),
        "-Y": matrix.to_quaternion() @ mathutils.Vector((0, -1, 0)),
        "-Z": matrix.to_quaternion() @ mathutils.Vector((0, 0, -1)),
    }
    return {
        "name": obj.name,
        "type": obj.type,
        "parent": obj.parent.name if obj.parent else None,
        "location": vec(obj.location),
        "rotation_euler": vec(obj.rotation_euler),
        "scale": vec(obj.scale),
        "world_location": vec(matrix.translation),
        "world_rotation_euler": vec(matrix.to_euler()),
        "world_scale": vec(matrix.to_scale()),
        "world_axes": {name: vec(axis) for name, axis in basis.items()},
        "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
    }


out = []
for obj in bpy.data.objects:
    if obj.name in TARGETS or obj.name.startswith("Green Deep Realistic"):
        out.append(object_info(obj))

output = Path(__file__).resolve().parents[1] / "Temp" / "odin_blend_eye_transforms.json"
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(out, indent=2), encoding="utf-8")
print(f"Wrote {output}")
