import json
import sys
from pathlib import Path

import bpy


def value_to_json(value):
    try:
        if hasattr(value, "__len__") and not isinstance(value, (str, bytes)):
            return [float(v) if isinstance(v, (int, float)) else v for v in value]
        if isinstance(value, (int, float, str, bool)) or value is None:
            return value
    except TypeError:
        pass
    return str(value)


def socket_default(socket):
    if not hasattr(socket, "default_value"):
        return None
    return value_to_json(socket.default_value)


def dump_node(node):
    data = {
        "name": node.name,
        "label": node.label,
        "type": node.bl_idname,
        "location": [node.location.x, node.location.y],
        "inputs": [
            {
                "name": sock.name,
                "type": sock.bl_idname,
                "default": socket_default(sock),
                "linked": sock.is_linked,
            }
            for sock in node.inputs
        ],
        "outputs": [
            {
                "name": sock.name,
                "type": sock.bl_idname,
                "linked": sock.is_linked,
            }
            for sock in node.outputs
        ],
    }
    if getattr(node, "image", None):
        image = node.image
        data["image"] = {
            "name": image.name,
            "filepath": bpy.path.abspath(image.filepath) if image.filepath else "",
            "packed": image.packed_file is not None,
            "size": list(image.size),
            "colorspace": image.colorspace_settings.name,
            "source": image.source,
        }
    if getattr(node, "node_tree", None):
        data["node_tree"] = node.node_tree.name
    for attr in (
        "operation",
        "blend_type",
        "data_type",
        "factor_mode",
        "clamp",
        "use_clamp",
        "interpolation_type",
        "color_mode",
        "space",
        "vector_type",
        "gradient_type",
        "voronoi_dimensions",
        "feature",
    ):
        if hasattr(node, attr):
            data[attr] = value_to_json(getattr(node, attr))
    return data


def dump_node_tree(tree):
    return {
        "name": tree.name,
        "type": tree.bl_idname,
        "nodes": [dump_node(node) for node in tree.nodes],
        "links": [
            {
                "from_node": link.from_node.name,
                "from_socket": link.from_socket.name,
                "to_node": link.to_node.name,
                "to_socket": link.to_socket.name,
            }
            for link in tree.links
        ],
    }


def dump_material(mat):
    data = {
        "name": mat.name,
        "use_nodes": mat.use_nodes,
        "diffuse_color": value_to_json(mat.diffuse_color),
        "blend_method": getattr(mat, "blend_method", None),
        "surface_render_method": getattr(mat, "surface_render_method", None),
        "alpha_threshold": getattr(mat, "alpha_threshold", None),
        "nodes": [],
        "links": [],
    }
    if not mat.use_nodes or not mat.node_tree:
        return data

    for node in mat.node_tree.nodes:
        data["nodes"].append(dump_node(node))
    for link in mat.node_tree.links:
        data["links"].append(
            {
                "from_node": link.from_node.name,
                "from_socket": link.from_socket.name,
                "to_node": link.to_node.name,
                "to_socket": link.to_socket.name,
            }
        )
    return data


argv = sys.argv
out_path = None
if "--" in argv:
    extra = argv[argv.index("--") + 1 :]
    if extra:
        out_path = extra[0]
if out_path is None:
    out_path = str(Path.cwd() / "blend_material_dump.json")

result = {
    "file": bpy.data.filepath,
    "materials": [dump_material(mat) for mat in bpy.data.materials],
    "node_groups": [dump_node_tree(tree) for tree in bpy.data.node_groups],
    "images": [
        {
            "name": image.name,
            "filepath": bpy.path.abspath(image.filepath) if image.filepath else "",
            "packed": image.packed_file is not None,
            "size": list(image.size),
            "colorspace": image.colorspace_settings.name,
            "source": image.source,
        }
        for image in bpy.data.images
    ],
    "objects": [
        {
            "name": obj.name,
            "type": obj.type,
            "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
        }
        for obj in bpy.data.objects
    ],
}

Path(out_path).parent.mkdir(parents=True, exist_ok=True)
Path(out_path).write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Wrote {out_path}")
