import sys

import bpy
import mathutils


def main():
    fbx_path = sys.argv[sys.argv.index("--") + 1]
    bpy.ops.import_scene.fbx(filepath=fbx_path, automatic_bone_orientation=False)
    bpy.context.view_layer.update()

    for obj in bpy.context.scene.objects:
        print(obj.name, obj.type, "loc", tuple(round(v, 3) for v in obj.location), "scale", tuple(round(v, 6) for v in obj.scale))
        if obj.type == "MESH":
            coords = [obj.matrix_world @ mathutils.Vector(corner) for corner in obj.bound_box]
            min_v = mathutils.Vector((min(v.x for v in coords), min(v.y for v in coords), min(v.z for v in coords)))
            max_v = mathutils.Vector((max(v.x for v in coords), max(v.y for v in coords), max(v.z for v in coords)))
            print("  bounds", tuple(round(v, 3) for v in min_v), tuple(round(v, 3) for v in max_v))


if __name__ == "__main__":
    main()
