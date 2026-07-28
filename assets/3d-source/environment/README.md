# Production Environment Sources

These are compact, game-ready Blender derivatives of artwork supplied for the
Living Realms playtest:

- `medieval-farmhouse.blend` — B1 farmhouses
- `moss-rock-01.blend` through `moss-rock-03.blend` — gatherable stone variants
- `meadow-oak.blend` and `meadow-birch.blend` — trees from the grass-and-trees system
- `mature-broadleaf.blend` and `woodland-bush.blend` — tree and bush pack variants
- `meadow-grass-clump.blend` — optimized grass clump used by regional multimeshes

The original supplier files remain outside the repository. These copies remove
unused collections, rigs, and oversized textures so the game and Git history do
not carry the complete source packs.

All exports use one Blender/Godot unit per meter and must pass
`tools/godot/validate_game_scale.gd` before release.
