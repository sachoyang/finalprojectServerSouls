import json
import sys

import bpy


def main():
    if "--" not in sys.argv:
        raise SystemExit("Usage: blender --background --python analyze_fbx_blender.py -- <fbx_path>")

    fbx_path = sys.argv[sys.argv.index("--") + 1]
    bpy.ops.import_scene.fbx(filepath=fbx_path, automatic_bone_orientation=False)

    actions = []
    for action in bpy.data.actions:
        frame_start, frame_end = action.frame_range
        actions.append(
            {
                "name": action.name,
                "start": float(frame_start),
                "end": float(frame_end),
                "frames": float(frame_end - frame_start),
                "data_block_type": type(action).__name__,
            }
        )

    objects = [{"name": obj.name, "type": obj.type} for obj in bpy.context.scene.objects]
    armatures = []
    for obj in bpy.context.scene.objects:
        if obj.type != "ARMATURE":
            continue

        armatures.append(
            {
                "name": obj.name,
                "bone_count": len(obj.data.bones),
                "bones": [bone.name for bone in obj.data.bones[:80]],
            }
        )

    summary = {
        "object_count": len(objects),
        "objects": objects[:120],
        "action_count": len(actions),
        "actions": actions,
        "armatures": armatures,
    }

    print("@@FBX_SUMMARY@@" + json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    main()
