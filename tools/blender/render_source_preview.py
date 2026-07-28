"""Render a compact review image from the active camera in a supplied blend."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy


def arguments() -> argparse.Namespace:
    values = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--width", type=int, default=960)
    parser.add_argument("--height", type=int, default=540)
    return parser.parse_args(values)


def main() -> None:
    args = arguments()
    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)

    scene = bpy.context.scene
    if scene.camera is None:
        scene.camera = next((item for item in scene.objects if item.type == "CAMERA"), None)
    if scene.camera is None:
        raise RuntimeError("The source scene has no camera.")

    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "BOTH"
    for item in scene.objects:
        if item.type == "MESH":
            for modifier in item.modifiers:
                modifier.show_render = False
    scene.render.resolution_x = args.width
    scene.render.resolution_y = args.height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output)
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.use_file_extension = True

    bpy.ops.render.render(write_still=True)
    print(f"PREVIEW={output}")


if __name__ == "__main__":
    main()
