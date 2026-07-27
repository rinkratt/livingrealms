# Campaign Alignment Phase 2

Build 0.9.12 completes the Destructible Settlements phase.

## Persistent assets

- Stonehaven and Darkwood contain 34 individually named structure records.
- Walls, gates, buildings, farms, the A3 iron mine, the Mirrorwater dock, and
  Darkwood camp assets have persistent health, maximum health, and armor.
- Structures know whether their construction or development requirement has
  been reached, so unfinished assets are not reported as standing buildings.
- Damage, last-damaged time, and destruction time survive server and client
  restarts.

## Battles and the world

- Darkwood raid outcomes damage Stonehaven's real structure records.
- Stonehaven counterattacks damage Darkwood's standing structures and measure
  victory against their actual remaining health.
- A breached or destroyed wall section stops creating collision and is removed
  from the navigation obstacle map.
- Destroyed assets display rubble and nearby standing assets display health,
  armor, and condition.
- The Journey page reports aggregate health plus damaged, breached, and
  destroyed assets for both settlements.

## Reset behavior

The administrator playtest reset restores all structure health and clears
damage/destruction timestamps. Automatic rebuilding after total defeat remains
Phase 7 work and is not simulated early.

## Verification

- 52 .NET tests pass.
- The EF Core migration produces an idempotent PostgreSQL script.
- Godot 4.7.1 imports, compiles, exports, and starts the Windows build.
- The Windows package passes a complete ZIP extraction check.
