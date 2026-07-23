# Phase 7A Completion - Stonehaven Residents

Phase 7A establishes the settlement-NPC foundation requested before the first raid. Stonehaven Village now has eight persistent named residents whose state is owned by PostgreSQL and presented by the Godot client.

## Delivered

- `SettlementResidents` stores each resident's name, role, health, maximum health, active/injured/missing/dead status, combat capability, home/work/safe locations, and dialogue.
- Stonehaven begins with Captain Rowan, guards Mira and Tomas, Brann the blacksmith, Mara the innkeeper, Elowen the healer, Oren the storekeeper, and Nessa the villager.
- The authenticated resident endpoint requires a selected character and derives each resident's current activity and destination from the persistent world clock.
- Guards patrol by day and guard the gate at night. Workers report to their places of work during role-specific hours and return home after hours. Injured residents use their safe location.
- The client renders role-colored low-poly residents with names, roles, activities, and health. NPCs navigate the same obstacle map used by hostile creatures.
- Pressing **R** near a resident displays that person's dialogue and current activity.
- Resident health, status, defensive ability, and safe locations are ready to be consumed by the first-raid simulation.

## Data and API

- Migration: `20260717203050_Phase7ASettlementResidents`
- Endpoint: `GET /api/v1/regions/stonehaven-valley/residents`
- API identity: Phase 7, `settlement-npcs-ready`
- Operator-facing time remains America/Chicago Central Time; database timestamps remain UTC.

## Verification

- The complete .NET test suite passes: 28 tests.
- The Godot C# client builds with zero warnings and zero errors.
- Phase 7 tests cover authentication, selected-character enforcement, seeded resident state, persistence, and day/night schedule changes.

## Deliberately deferred

Shops, service menus, resident quests, relationship/reputation systems, voice, combat behavior for guards, resident raid casualties, and rebuilding are not part of this foundation. The next slice can use these resident records in the first Darkwood raid.
