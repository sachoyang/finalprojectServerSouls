import math
import sys
from pathlib import Path

import bpy


def look_at(obj, target):
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def get_scene_bounds():
    min_v = None
    max_v = None
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        for corner in obj.bound_box:
            world = obj.matrix_world @ bpy.mathutils.Vector(corner)
            if min_v is None:
                min_v = world.copy()
                max_v = world.copy()
            else:
                min_v.x = min(min_v.x, world.x)
                min_v.y = min(min_v.y, world.y)
                min_v.z = min(min_v.z, world.z)
                max_v.x = max(max_v.x, world.x)
                max_v.y = max(max_v.y, world.y)
                max_v.z = max(max_v.z, world.z)
    return min_v, max_v


def main():
    if "--" not in sys.argv:
        raise SystemExit("Usage: blender --background --python script.py -- <fbx_path> <out_dir> <start> <count>")

    args = sys.argv[sys.argv.index("--") + 1 :]
    fbx_path = args[0]
    out_dir = Path(args[1])
    start_index = int(args[2]) if len(args) > 2 else 0
    count = int(args[3]) if len(args) > 3 else 50
    sample_count = int(args[4]) if len(args) > 4 else 3
    out_dir.mkdir(parents=True, exist_ok=True)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    bpy.ops.import_scene.fbx(filepath=fbx_path, automatic_bone_orientation=False)

    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    if armature.animation_data is None:
        armature.animation_data_create()

    actions = sorted(bpy.data.actions, key=lambda action: action.name)
    selected_actions = actions[start_index : start_index + count]

    # Camera setup.
    min_v, max_v = get_scene_bounds()
    center = (min_v + max_v) * 0.5
    size = max((max_v - min_v).length, 1.0)

    bpy.ops.object.light_add(type="AREA", location=(center.x, center.y - size * 0.6, center.z + size * 0.8))
    light = bpy.context.object
    light.name = "Preview_Area_Light"
    light.data.energy = 5000
    light.data.size = size

    bpy.ops.object.camera_add(location=(center.x, center.y - size * 1.65, center.z + size * 0.45))
    camera = bpy.context.object
    camera.name = "Preview_Camera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = size * 0.95
    camera.data.clip_start = 0.1
    camera.data.clip_end = size * 10.0
    look_at(camera, center)
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.resolution_x = 320
    scene.render.resolution_y = 240
    scene.render.film_transparent = False
    scene.frame_set(1)

    try:
        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.shading.light = "STUDIO"
        scene.display.shading.color_type = "MATERIAL"
    except Exception:
        pass

    metadata_lines = ["index,action,frames,start,end"]
    for action_index, action in enumerate(selected_actions, start=start_index):
        armature.animation_data.action = action
        frame_start, frame_end = action.frame_range
        frame_start = int(math.floor(frame_start))
        frame_end = int(math.ceil(frame_end))
        if sample_count <= 1:
            sample_frames = [frame_start]
        else:
            sample_frames = [
                int(round(frame_start + (frame_end - frame_start) * i / (sample_count - 1)))
                for i in range(sample_count)
            ]

        safe_name = f"{action_index:03d}_{action.name}".replace("|", "_").replace(":", "_").replace("/", "_").replace("\\", "_")
        metadata_lines.append(f'{action_index},"{action.name}",{frame_end - frame_start},{frame_start},{frame_end}')

        for sample_index, frame in enumerate(sample_frames):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            scene.render.filepath = str(out_dir / f"{safe_name}_f{sample_index}_{frame}.png")
            bpy.ops.render.render(write_still=True)

    (out_dir / "metadata.csv").write_text("\n".join(metadata_lines), encoding="utf-8")


if __name__ == "__main__":
    import mathutils

    bpy.mathutils = mathutils
    main()
