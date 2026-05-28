import csv
import json
import math
import sys
import time
from pathlib import Path

import bpy


def vec_distance(a, b):
    return (a - b).length


def safe_ratio(value, denom):
    return value / denom if denom else 0.0


def get_armature():
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if not armatures:
        raise RuntimeError("No armature found.")
    return armatures[0]


def get_root_bone(armature):
    roots = [bone for bone in armature.pose.bones if bone.parent is None]
    if roots:
        return roots[0]
    return armature.pose.bones[0]


def get_pose_points(armature):
    points = []
    for bone in armature.pose.bones:
        world = armature.matrix_world @ bone.matrix.translation
        points.append(world.copy())
    return points


def centroid(points):
    total = points[0].copy()
    for point in points[1:]:
        total += point
    return total / len(points)


def bounds(points):
    min_v = points[0].copy()
    max_v = points[0].copy()
    for point in points[1:]:
        min_v.x = min(min_v.x, point.x)
        min_v.y = min(min_v.y, point.y)
        min_v.z = min(min_v.z, point.z)
        max_v.x = max(max_v.x, point.x)
        max_v.y = max(max_v.y, point.y)
        max_v.z = max(max_v.z, point.z)
    return min_v, max_v


def model_scale_from_armature(armature):
    points = get_pose_points(armature)
    min_v, max_v = bounds(points)
    return max((max_v - min_v).length, 1.0)


def classify_action(metrics):
    frames = metrics["frames"]
    norm_center_span = metrics["norm_center_span"]
    norm_vertical_span = metrics["norm_vertical_span"]
    norm_pose_delta = metrics["norm_pose_delta"]
    norm_pose_motion = metrics["norm_pose_motion"]
    norm_root_span = metrics["norm_root_span"]
    root_turn = metrics["root_turn"]

    if frames <= 14:
        return "포즈 조각", "짧은 날개/자세 포즈", "10프레임 안팎의 매우 짧은 조각입니다. 블렌딩/전환용 후보."

    if frames <= 32:
        if norm_pose_motion > 0.22:
            return "전환/포즈 조각", "짧은 날개 전환", "짧지만 자세 변화가 있습니다."
        return "포즈 조각", "짧은 자세 조각", "짧은 포즈 또는 보간용 조각으로 보입니다."

    if norm_center_span < 0.035 and norm_pose_motion < 0.12:
        return "대기/호버", "공중 대기", "중심 이동과 자세 변화가 작습니다."

    if norm_center_span < 0.055 and norm_pose_motion < 0.20:
        return "대기/호버", "공중 대기 흔들림", "대기 루프 또는 약한 흔들림 후보."

    if norm_vertical_span > max(norm_center_span * 0.65, 0.08) and norm_center_span > 0.08:
        return "이동/이탈", "수직 공중 이탈", "높이 변화가 커서 상승/하강 이동 후보입니다."

    if norm_center_span > 0.22 or norm_root_span > 0.20:
        if root_turn > 1.2 or norm_pose_motion > 0.45:
            return "이동/선회", "긴 공중 선회", "중심 이동과 자세 회전이 큰 긴 이동입니다."
        return "이동/이탈", "긴 공중 이동", "중심 이동량이 커서 장거리 이동/이탈 후보입니다."

    if norm_pose_delta > 0.28 or norm_pose_motion > 0.36 or root_turn > 1.0:
        return "공중 회전", "공중 회전 전환", "중심 이동보다 자세 변화/회전이 큽니다."

    if norm_center_span > 0.09:
        return "이동/공격 후보", "공중 전진 돌진", "짧은 이동성이 있어 돌진/공격 전환 후보입니다."

    return "전환/검토 필요", "공중 자세 전환", "자동 수치만으로는 용도가 애매합니다."


def action_metrics(armature, action, sample_count):
    if armature.animation_data is None:
        armature.animation_data_create()
    armature.animation_data.action = action

    scene = bpy.context.scene
    frame_start, frame_end = action.frame_range
    frame_start = int(math.floor(frame_start))
    frame_end = int(math.ceil(frame_end))
    frames = max(frame_end - frame_start, 1)

    samples = [
        int(round(frame_start + (frame_end - frame_start) * i / max(sample_count - 1, 1)))
        for i in range(sample_count)
    ]

    root_bone = get_root_bone(armature)
    all_points = []
    centers = []
    root_positions = []
    first_pose = None
    last_pose = None
    pose_step_motion = 0.0
    prev_pose = None

    for frame in samples:
        scene.frame_set(frame)
        bpy.context.view_layer.update()

        points = get_pose_points(armature)
        all_points.extend(points)
        centers.append(centroid(points))
        root_positions.append((armature.matrix_world @ root_bone.matrix.translation).copy())

        if first_pose is None:
            first_pose = points
        last_pose = points

        if prev_pose is not None:
            step = sum(vec_distance(a, b) for a, b in zip(prev_pose, points)) / len(points)
            pose_step_motion += step
        prev_pose = points

    model_scale = model_scale_from_armature(armature)
    center_min, center_max = bounds(centers)
    root_min, root_max = bounds(root_positions)
    all_min, all_max = bounds(all_points)

    pose_delta = sum(vec_distance(a, b) for a, b in zip(first_pose, last_pose)) / len(first_pose)
    root_turn = 0.0
    for i in range(1, len(root_positions) - 1):
        a = root_positions[i] - root_positions[i - 1]
        b = root_positions[i + 1] - root_positions[i]
        if a.length > 0.001 and b.length > 0.001:
            root_turn += a.angle(b, 0.0)

    metrics = {
        "action": action.name,
        "frames": frames,
        "start": frame_start,
        "end": frame_end,
        "center_span": (center_max - center_min).length,
        "vertical_span": abs(center_max.z - center_min.z),
        "root_span": (root_max - root_min).length,
        "pose_delta": pose_delta,
        "pose_motion": pose_step_motion,
        "root_turn": root_turn,
        "bounds_span": (all_max - all_min).length,
    }
    metrics.update(
        {
            "norm_center_span": safe_ratio(metrics["center_span"], model_scale),
            "norm_vertical_span": safe_ratio(metrics["vertical_span"], model_scale),
            "norm_root_span": safe_ratio(metrics["root_span"], model_scale),
            "norm_pose_delta": safe_ratio(metrics["pose_delta"], model_scale),
            "norm_pose_motion": safe_ratio(metrics["pose_motion"], model_scale),
        }
    )
    category, korean_name, note = classify_action(metrics)
    metrics["category"] = category
    metrics["korean_name"] = korean_name
    metrics["note"] = note
    return metrics


def write_outputs(rows, out_csv, out_md):
    fieldnames = [
        "index",
        "action",
        "frames",
        "korean_name",
        "category",
        "note",
        "norm_center_span",
        "norm_vertical_span",
        "norm_root_span",
        "norm_pose_delta",
        "norm_pose_motion",
        "root_turn",
    ]
    with out_csv.open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            writer.writerow({key: row.get(key, "") for key in fieldnames})

    lines = [
        "# 103.fbx 애니메이션 자동 분류표",
        "",
        "자동 수치 분석 기준 초안입니다. 실제 공격 판정/루트 모션 적용 전에는 대표 클립만 6프레임 프리뷰로 재확인하는 것을 권장합니다.",
        "",
        "| 번호 | 원본 액션명 | 길이 | 한글명 | 분류 | 메모 |",
        "|---:|---|---:|---|---|---|",
    ]
    for row in rows:
        lines.append(
            f"| {row['index']:03d} | {row['action']} | {row['frames']} | {row['korean_name']} | {row['category']} | {row['note']} |"
        )
    out_md.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    if "--" not in sys.argv:
        raise SystemExit(
            "Usage: blender --background --python auto_classify_fbx_actions.py -- <fbx_path> <out_csv> <out_md> [sample_count]"
        )

    args = sys.argv[sys.argv.index("--") + 1 :]
    fbx_path = args[0]
    out_csv = Path(args[1])
    out_md = Path(args[2])
    sample_count = int(args[3]) if len(args) > 3 else 8
    out_csv.parent.mkdir(parents=True, exist_ok=True)
    out_md.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    bpy.ops.import_scene.fbx(filepath=fbx_path, automatic_bone_orientation=False)

    armature = get_armature()
    actions = sorted(bpy.data.actions, key=lambda action: action.name)

    rows = []
    started_at = time.time()
    print(f"@@PROGRESS@@ 0/{len(actions)} import_done elapsed=0s", flush=True)
    for index, action in enumerate(actions):
        row = action_metrics(armature, action, sample_count)
        row["index"] = index
        rows.append(row)
        if (index + 1) % 10 == 0 or index + 1 == len(actions):
            elapsed = int(time.time() - started_at)
            print(f"@@PROGRESS@@ {index + 1}/{len(actions)} elapsed={elapsed}s last={action.name}", flush=True)

    write_outputs(rows, out_csv, out_md)
    print("@@AUTO_CLASSIFY@@" + json.dumps({"count": len(rows), "csv": str(out_csv), "md": str(out_md)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
