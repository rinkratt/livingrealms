# Elowen herbalist character

The source character was supplied by the Living Realms project owner as
`Chubby magic girl.blend` and approved for use as Stonehaven's Elowen
healer/herbalist NPC.

`elowen-herbalist.blend` is the cleaned game-production scene. It keeps the
visible character and Rigify deformation skeleton while removing facial
controls, Rigify widget geometry, review-only objects, and unused production
helpers. Packed 4K and 8K images are reduced to a maximum of 1024 pixels for
the playtest.

Regenerate the runtime GLB and review render with:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' `
  --background `
  'C:\path\to\Chubby magic girl.blend' `
  --python tools\blender\export_herbalist_character.py `
  -- `
  --output client/LivingRealms.Client/Assets/Characters3D/elowen-herbalist.glb `
  --source-output assets/3d-source/characters/elowen/elowen-herbalist.blend `
  --preview docs/elowen-herbalist-preview.png
```

The cleaned source and its textures are project assets. Do not redistribute
the original source file separately from Living Realms without confirming the
project owner's underlying asset rights.
