"""
Export a custom (non-KSP) part mesh for Space Program Unity.

Writes:
  {name}.obj          Unity axes: Y-up, -Z forward, 1 unit = 1 meter
  {name}_albedo.png   Principled Base Color, if an image is connected
  {name}_metallic.png Principled Metallic
  {name}_roughness.png Principled Roughness  (Unity importer treats this as roughness)

Then in Unity: Space Program → Parts → Custom Part Importer → pick the .obj
(the window auto-detects sibling PNGs).

Usage
-----
Blender UI: Scripting → Open this file → select the part → Run Script.
CLI:
  blender --background part.blend --python export_unity_custom_part.py -- --out "D:/parts/first-engine.obj"
"""

from __future__ import annotations

import os
import shutil
import sys

import bpy


def _parse_out_path() -> str | None:
    argv = sys.argv
    if "--" not in argv:
        return None

    extra = argv[argv.index("--") + 1 :]
    index = 0
    while index < len(extra):
        token = extra[index]
        if token in ("--out", "-o") and index + 1 < len(extra):
            return extra[index + 1]
        index += 1
    return None


def _default_out_path() -> str:
    blend_path = bpy.data.filepath
    if blend_path:
        stem = os.path.splitext(os.path.basename(blend_path))[0]
        return os.path.join(os.path.dirname(blend_path), stem + ".obj")
    return os.path.join(os.path.expanduser("~"), "custom-part.obj")


def _find_principled(material):
    if material is None or not material.use_nodes or material.node_tree is None:
        return None

    for node in material.node_tree.nodes:
        if node.type == "BSDF_PRINCIPLED":
            return node
    return None


def _image_from_socket(socket):
    if socket is None or not socket.is_linked:
        return None

    node = socket.links[0].from_node
    if node.type == "TEX_IMAGE" and node.image is not None:
        return node.image
    return None


def _first_selected_mesh_material():
    selected = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]
    search = selected if selected else [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]

    for obj in search:
        for slot in obj.material_slots:
            principled = _find_principled(slot.material)
            if principled is not None:
                return principled
    return None


def _write_image_png(image, dest_path: str) -> bool:
    dest_path = os.path.abspath(dest_path)
    os.makedirs(os.path.dirname(dest_path), exist_ok=True)

    if image.packed_file:
        image.file_format = "PNG"
        image.save_render(dest_path)
        print("[export_unity_custom_part] packed image ->", dest_path)
        return True

    source = bpy.path.abspath(image.filepath) if image.filepath else ""
    if source and os.path.isfile(source):
        shutil.copy2(source, dest_path)
        print("[export_unity_custom_part] copied", source, "->", dest_path)
        return True

    image.file_format = "PNG"
    image.save_render(dest_path)
    print("[export_unity_custom_part] saved image ->", dest_path)
    return True


def _export_obj(filepath: str, selected_only: bool) -> None:
    os.makedirs(os.path.dirname(os.path.abspath(filepath)) or ".", exist_ok=True)

    if hasattr(bpy.ops.wm, "obj_export"):
        bpy.ops.wm.obj_export(
            filepath=filepath,
            export_selected_objects=selected_only,
            forward_axis="NEGATIVE_Z",
            up_axis="Y",
            export_uv=True,
            export_normals=True,
            export_materials=True,
            apply_modifiers=True,
            export_triangulated_mesh=True,
        )
        return

    bpy.ops.export_scene.obj(
        filepath=filepath,
        use_selection=selected_only,
        axis_forward="-Z",
        axis_up="Y",
        use_normals=True,
        use_uvs=True,
        use_materials=True,
        use_triangles=True,
        use_mesh_modifiers=True,
    )


def export_custom_part(out_path: str | None = None) -> str:
    filepath = os.path.abspath(out_path or _parse_out_path() or _default_out_path())
    if not filepath.lower().endswith(".obj"):
        filepath = filepath + ".obj"

    selected_meshes = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]
    selected_only = len(selected_meshes) > 0

    if selected_only:
        bpy.ops.object.select_all(action="DESELECT")
        for obj in selected_meshes:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = selected_meshes[0]

    _export_obj(filepath, selected_only=selected_only)

    stem = os.path.splitext(filepath)[0]
    principled = _first_selected_mesh_material()
    copied = 0

    if principled is not None:
        mapping = (
            ("Base Color", stem + "_albedo.png"),
            ("Metallic", stem + "_metallic.png"),
            ("Roughness", stem + "_roughness.png"),
        )
        for socket_name, dest in mapping:
            socket = principled.inputs.get(socket_name)
            image = _image_from_socket(socket)
            if image is None:
                continue
            if _write_image_png(image, dest):
                copied += 1

    print(
        "[export_unity_custom_part] OBJ",
        filepath,
        "selected_only=" + str(selected_only),
        "maps=" + str(copied),
    )
    print("[export_unity_custom_part] Next: Unity → Space Program → Parts → Custom Part Importer")
    return filepath


if __name__ == "__main__":
    export_custom_part()
