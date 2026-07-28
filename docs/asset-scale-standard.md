# Living Realms Asset Scale Standard

Living Realms uses real-world scale: **one Godot unit equals one meter**.

Imported art is normalized before it enters the game. The current reference
sizes are:

| Asset type | Game-size reference |
|---|---:|
| Adult character | 1.45–2.05 m tall |
| Medieval farmhouse | about 7.8 × 7.7 × 8.5 m |
| Mature tree | 5–8 m tall |
| Bush | about 1.6 m tall |
| Grass clump | about 0.65 m tall |
| Gatherable rock | about 0.6–1.1 m tall before gameplay variation |

The Blender environment exporter requires exactly one `--target-height` or
`--target-width` argument. This grounds and normalizes the source asset instead
of carrying its original Blender-unit scale into Godot.

Before packaging a playtest build, run:

```powershell
Godot_v4.7.1-stable_mono_win64_console.exe `
  --headless `
  --path client/LivingRealms.Client `
  --script ../../tools/godot/validate_game_scale.gd
```

The build is not ready when this check reports a character or environment
asset outside its allowed range.
