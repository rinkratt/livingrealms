# Campaign Alignment Phase 4: Survival and Workers

Build 0.9.14 completes the Survival and Workers phase.

## Settlement survival

- Stonehaven's founding population is eleven named residents, including two
  farmers and one fisherman.
- Every living Stonehaven resident and Darkwood goblin consumes one unit of
  food per world hour.
- Farmers, fishermen, and hunters produce food from their assigned work.
- The world records production, consumption, net change, stored food, shortage
  state, and estimated hours of reserves for each faction.

## Need-based workers

- Stonehaven recruits the food-producing role needed to correct a shortage or
  unsafe production margin.
- Darkwood trains clan hunters when its food economy needs them.
- Recruitment still pays the existing food and settlement growth costs and is
  limited by population capacity and world progression.
- Founding and recruited workers are named persistent residents or goblins,
  not abstract population counters.

## Wildlife and hunting

- Ten named huntable forest rats and prairie wolves persist in suitable parts
  of the connected map outside the A1 training yard.
- Named Stonehaven and Darkwood hunters travel toward available wildlife.
- Hunted wildlife dies persistently and respawns through the existing creature
  lifecycle.
- When both factions contest the same hunting grounds, the encounter injures
  both hunting parties and is written to the World Chronicle.

## Player visibility

The Journey page now includes a Survival & Workers section for both factions.
It explains population, food stores, production and consumption per hour,
worker counts, reserve time, shortages, the next recommended role, and
available or respawning wildlife.

## Verification

- All solution tests pass, including API, simulation, population, campaign,
  wildlife, and Journey-response coverage.
- The Phase 4 database migration raises only settlements below the new
  Stonehaven founding population, preserving larger live populations.
