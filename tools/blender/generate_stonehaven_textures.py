"""Generate deterministic tileable PBR source textures for Stonehaven."""

from __future__ import annotations

import hashlib
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "assets" / "3d-source" / "stonehaven" / "textures"
SIZE = 512
WORLD_SIZE = 2048


TEXTURES: dict[str, tuple[str, str]] = {
    "Grass_Meadow": ("#33472b", "grass"),
    "Grass_Shadow": ("#213523", "grass"),
    "Road_PackedEarth": ("#655039", "earth"),
    "Earth_Damp": ("#443629", "earth"),
    "Stone_Cobble": ("#68665f", "stone"),
    "Stone_CobbleLight": ("#817a6d", "stone"),
    "Stone_Granite": ("#4d5252", "stone"),
    "Stone_WornFace": ("#666b69", "stone"),
    "Stone_Mortar": ("#363a39", "mortar"),
    "Plaster_Oat": ("#8e775b", "plaster"),
    "Plaster_Clay": ("#73533e", "plaster"),
    "Plaster_Moss": ("#667057", "plaster"),
    "Timber_DarkOak": ("#382317", "wood"),
    "Timber_WarmOak": ("#5a3922", "wood"),
    "Timber_WornEdge": ("#765033", "wood"),
    "Roof_Oxblood": ("#57241e", "roof"),
    "Roof_WoodShingle": ("#3e281f", "roof"),
    "Iron_Blackened": ("#2e3131", "metal"),
    "River_Water": ("#1c5260", "water"),
    "Leaves_ForestShadow": ("#17331f", "leaf"),
    "Leaves_Oak": ("#2d572f", "leaf"),
    "Leaves_Sunlit": ("#60733b", "leaf"),
    "Rock_Valley": ("#484b47", "rock"),
    "Rock_Lichen": ("#67695b", "rock"),
    "Banner_StonehavenRed": ("#7c211c", "fabric"),
}


def seed_for(name: str) -> int:
    return int.from_bytes(hashlib.sha256(name.encode("utf-8")).digest()[:8], "little")


def rgb(hex_color: str) -> np.ndarray:
    return np.array([int(hex_color[index:index + 2], 16) for index in (1, 3, 5)], dtype=np.float32)


def value_noise(rng: np.random.Generator, size: int, octaves: int = 6) -> np.ndarray:
    result = np.zeros((size, size), dtype=np.float32)
    weight_sum = 0.0
    for octave in range(octaves):
        cells = 4 * (2 ** octave)
        grid = rng.random((cells, cells), dtype=np.float32)
        tile = Image.fromarray(np.uint8(grid * 255), mode="L").resize((size, size), Image.Resampling.BICUBIC)
        layer = np.asarray(tile, dtype=np.float32) / 255.0
        weight = 0.55 ** octave
        result += layer * weight
        weight_sum += weight
    return np.clip(result / weight_sum, 0.0, 1.0)


def add_lines(height: np.ndarray, rng: np.random.Generator, style: str) -> np.ndarray:
    canvas = Image.fromarray(np.uint8(np.clip(height, 0, 1) * 255), mode="L")
    draw = ImageDraw.Draw(canvas)
    if style in {"stone", "rock"}:
        for _ in range(22 if style == "rock" else 12):
            points: list[tuple[int, int]] = []
            x = int(rng.integers(0, SIZE))
            y = int(rng.integers(0, SIZE))
            for _ in range(int(rng.integers(3, 7))):
                points.append((x % SIZE, y % SIZE))
                x += int(rng.integers(-55, 56))
                y += int(rng.integers(18, 70))
            draw.line(points, fill=int(rng.integers(34, 75)), width=int(rng.integers(1, 4)))
    elif style == "wood":
        for y in range(8, SIZE, 19):
            drift = int(7 * math.sin(y * 0.047))
            draw.line([(0, y + drift), (SIZE, y - drift)], fill=72, width=2)
        for _ in range(14):
            x = int(rng.integers(0, SIZE))
            y = int(rng.integers(0, SIZE))
            radius = int(rng.integers(8, 23))
            draw.ellipse((x - radius * 2, y - radius, x + radius * 2, y + radius), outline=82, width=2)
    elif style == "roof":
        row_height = 64
        for row, y in enumerate(range(0, SIZE, row_height)):
            draw.line([(0, y), (SIZE, y)], fill=60, width=6)
            offset = 32 if row % 2 else 0
            for x in range(-offset, SIZE, 64):
                draw.line([(x, y), (x, min(SIZE, y + row_height))], fill=75, width=4)
                draw.arc((x - 32, y + row_height - 18, x + 32, y + row_height + 16), 0, 180, fill=92, width=3)
    elif style == "fabric":
        for position in range(0, SIZE, 8):
            draw.line([(position, 0), (position, SIZE)], fill=118, width=1)
            draw.line([(0, position), (SIZE, position)], fill=138, width=1)
    elif style == "grass":
        for _ in range(640):
            x = int(rng.integers(0, SIZE))
            y = int(rng.integers(0, SIZE))
            length = int(rng.integers(3, 13))
            draw.line((x, y, x + int(rng.integers(-3, 4)), y - length), fill=int(rng.integers(145, 225)), width=1)
    return np.asarray(canvas, dtype=np.float32) / 255.0


def pattern(name: str, style: str) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    rng = np.random.default_rng(seed_for(name))
    noise = value_noise(rng, SIZE)
    fine = value_noise(rng, SIZE, 4)
    height = noise.copy()
    roughness = np.full((SIZE, SIZE), 0.82, dtype=np.float32)

    if style == "wood":
        yy, xx = np.mgrid[0:SIZE, 0:SIZE]
        grain = 0.5 + 0.5 * np.sin(yy * 0.13 + noise * 13.0 + np.sin(xx * 0.018) * 2.5)
        height = 0.62 * noise + 0.38 * grain
        roughness[:] = 0.74 + fine * 0.18
    elif style == "roof":
        height = 0.48 + noise * 0.28
        roughness[:] = 0.79 + fine * 0.15
    elif style in {"stone", "rock", "mortar"}:
        height = noise ** 1.35
        roughness[:] = 0.78 + fine * 0.18
    elif style == "plaster":
        height = noise * 0.7 + fine * 0.3
        roughness[:] = 0.84 + fine * 0.12
    elif style == "metal":
        height = fine * 0.42 + noise * 0.18
        roughness[:] = 0.42 + fine * 0.22
    elif style == "water":
        yy, xx = np.mgrid[0:SIZE, 0:SIZE]
        height = 0.5 + 0.2 * np.sin(xx * 0.045 + yy * 0.018) + 0.14 * np.sin(yy * 0.071 - xx * 0.025)
        roughness[:] = 0.14 + fine * 0.12
    elif style == "leaf":
        height = np.clip(noise * 0.72 + fine * 0.42, 0, 1)
        roughness[:] = 0.76 + fine * 0.16
    elif style == "grass":
        height = noise * 0.65 + fine * 0.35
        roughness[:] = 0.84 + fine * 0.12
    elif style == "earth":
        height = noise * 0.74 + fine * 0.26
        roughness[:] = 0.86 + fine * 0.1
    elif style == "fabric":
        height = fine * 0.5 + 0.25
        roughness[:] = 0.77 + fine * 0.12

    height = add_lines(height, rng, style)
    variation = (height - 0.5) * (0.44 if style not in {"water", "metal"} else 0.22)
    return height, np.clip(variation, -0.28, 0.28), np.clip(roughness, 0, 1)


def normal_map(height: np.ndarray, strength: float) -> np.ndarray:
    dx = np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)
    dy = np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)
    nx = -dx * strength
    ny = dy * strength
    nz = np.ones_like(height)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack((nx / length, ny / length, nz / length), axis=2)
    return np.uint8(np.clip((normal * 0.5 + 0.5) * 255, 0, 255))


def save_texture(name: str, base_hex: str, style: str) -> None:
    height, variation, roughness = pattern(name, style)
    base = rgb(base_hex)
    color = base[None, None, :] * (1.0 + variation[:, :, None])
    if style in {"grass", "leaf"}:
        color[:, :, 1] *= 1.0 + (height[:, :, None] - 0.5)[:, :, 0] * 0.17
    color = np.uint8(np.clip(color, 0, 255))
    normal_strength = 7.5 if style in {"stone", "rock", "roof"} else 4.5
    normal = normal_map(height, normal_strength)
    rough = np.uint8(np.clip(roughness * 255, 0, 255))

    Image.fromarray(color, mode="RGB").save(OUTPUT / f"{name}_basecolor.png", optimize=True)
    Image.fromarray(normal, mode="RGB").save(OUTPUT / f"{name}_normal.png", optimize=True)
    Image.fromarray(rough, mode="L").save(OUTPUT / f"{name}_roughness.png", optimize=True)


def smoothstep(value: np.ndarray) -> np.ndarray:
    value = np.clip(value, 0.0, 1.0)
    return value * value * (3.0 - 2.0 * value)


def generate_world_terrain() -> None:
    """Create one continuous terrain texture with noisy, blended biome boundaries."""
    rng = np.random.default_rng(seed_for("WorldTerrain"))
    macro = value_noise(rng, WORLD_SIZE, 7)
    medium = value_noise(rng, WORLD_SIZE, 6)
    fine = value_noise(rng, WORLD_SIZE, 5)
    warp_x = (value_noise(rng, WORLD_SIZE, 5) - 0.5) * 24.0
    warp_z = (value_noise(rng, WORLD_SIZE, 5) - 0.5) * 24.0

    coordinates = np.linspace(-144.0, 144.0, WORLD_SIZE, dtype=np.float32)
    xx, zz = np.meshgrid(coordinates, coordinates)
    grid_x = np.clip((xx + 96.0 + warp_x) / 96.0, 0.0, 2.0)
    grid_z = np.clip((zz + 96.0 + warp_z) / 96.0, 0.0, 2.0)
    x0 = np.floor(grid_x).astype(np.int32)
    z0 = np.floor(grid_z).astype(np.int32)
    x1 = np.minimum(x0 + 1, 2)
    z1 = np.minimum(z0 + 1, 2)
    tx = smoothstep(grid_x - x0)[:, :, None]
    tz = smoothstep(grid_z - z0)[:, :, None]

    palette = np.array(
        [
            [[27, 47, 32], [68, 76, 55], [48, 65, 53]],
            [[92, 101, 57], [51, 71, 43], [42, 77, 62]],
            [[50, 87, 67], [82, 98, 58], [83, 77, 67]],
        ],
        dtype=np.float32,
    )
    north = palette[z0, x0] * (1.0 - tx) + palette[z0, x1] * tx
    south = palette[z1, x0] * (1.0 - tx) + palette[z1, x1] * tx
    biome_color = north * (1.0 - tz) + south * tz

    height = np.clip(macro * 0.52 + medium * 0.31 + fine * 0.17, 0.0, 1.0)
    light = 0.70 + height[:, :, None] * 0.52
    color = biome_color * light

    exposed_earth = smoothstep((medium - 0.73) / 0.20)[:, :, None]
    exposed_earth *= 0.18 + 0.28 * smoothstep((fine - 0.55) / 0.35)[:, :, None]
    earth_color = np.array([82.0, 67.0, 45.0], dtype=np.float32)
    color = color * (1.0 - exposed_earth) + earth_color * exposed_earth

    meadow_threads = np.sin(xx * 1.31 + medium * 9.0) * np.sin(zz * 1.07 + fine * 7.0)
    color[:, :, 1] *= 1.0 + meadow_threads * 0.035
    color = np.uint8(np.clip(color, 0, 255))

    normal = normal_map(height, 10.5)
    roughness = np.uint8(np.clip((0.82 + fine * 0.16 - exposed_earth[:, :, 0] * 0.07) * 255, 0, 255))
    Image.fromarray(color, mode="RGB").save(OUTPUT / "WorldTerrain_basecolor.png", optimize=True)
    Image.fromarray(normal, mode="RGB").save(OUTPUT / "WorldTerrain_normal.png", optimize=True)
    Image.fromarray(roughness, mode="L").save(OUTPUT / "WorldTerrain_roughness.png", optimize=True)


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, (base_hex, style) in TEXTURES.items():
        save_texture(name, base_hex, style)
        print(f"generated {name}")
    generate_world_terrain()
    print("generated WorldTerrain")
    print(f"TEXTURES={OUTPUT}")


if __name__ == "__main__":
    main()
