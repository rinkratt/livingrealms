# Campaign Alignment Phase 5: A3 Iron and Equipment

Build 0.9.15 completes the A3 Iron and Equipment phase.

## One authoritative iron source

- The Irondeep Ore Vein in grid A3 is the only place that creates new iron.
- Stonehaven and Darkwood retain only their small founding reserves after a
  reset.
- Generic hourly iron grants and Stonehaven's former automatic iron cycle are
  removed.
- Player mining and faction mining both reduce the same persistent A3 vein.
- A destroyed Irondeep Mine stops both faction hauling operations.

## Persistent workers and transport

- Dain is Stonehaven's named Iron Miner.
- Darkwood assigns one living clan member as its named Iron Miner.
- Each operation persists its worker, state, world position, cargo, completed
  trips, and lifetime delivered iron.
- Workers travel to A3, mine one load, return to their own depot, and only then
  add that load to their faction store.
- The workers' server positions are synchronized with their visible resident
  or creature records.

## Persistent equipment

- Stonehaven and Darkwood each have weapon and armor tiers from zero to three.
- Armor costs 10, 20, and 30 iron for tiers one through three.
- Weapons cost 12, 24, and 36 iron for tiers one through three.
- The simulation spends stored iron on the lowest available tier; ore in
  transit cannot be spent.
- Darkwood equipment improves creature combat values and military strength.
- Stonehaven equipment improves guard strength, defense, and campaign combat.
- Equipment tiers survive logout, restart, and offline world processing.

## Irondeep guard contracts

- When a Darkwood mining party reaches the A3 contest radius, Stonehaven can
  hire Roderic Ames and Sela Brand as named A3 Mine Guards.
- Each guard costs five Stonehaven treasury gold per world day.
- Their names, daily cost, and treasury balance appear on Journey.
- If Stonehaven cannot meet payroll, the contracts end and the guards leave
  the active population.

## Journey and reset behavior

- Journey shows A3 ore remaining, mine hit points, operational status, both
  mining routes, cargo, deliveries, trip totals, equipment tiers and costs,
  named guards, payroll, and treasury gold.
- A playtest reset restores the vein, founding reserves, tier-zero equipment,
  a 30-gold Stonehaven treasury, no hired mine guards, and both mining routes
  at their home depots.
