# Phase 6 Completion Report

> Historical phase record. Current world-time, leadership, death, and construction rules are documented in `alignment-update-0.9.4.md`.

Phase 6 adds the first server-driven world that continues without a connected player.

## Delivered

- Persistent Darkwood Clan population, capacity, resources, structures, development stage, territory, morale, aggression, military strength, and simulation clock
- Persistent Darkwood leadership, title, experience, level, combat growth, and succession state
- Stonehaven Village population, supplies, structural integrity, guard strength, and defense state
- Elapsed-time catch-up on worker startup and on every 60-second worker tick
- Durable scheduled events with unique idempotency keys, atomic claims, retry limits, and interrupted-event recovery
- Central Time world-history entries for resource growth, recruitment, camp development, and leader progression
- Authenticated world-state and history API endpoints
- Development-only accelerated-time endpoint for advancing 1 to 168 world hours
- Godot Living World panel on **J**, world-day HUD, recent chronicle, and stage-dependent Darkwood camp geometry
- Deterministic leader combat scaling: every level increases maximum health, attack, and defense, and the values appear in both the Living World panel and boss label
- Obstacle-aware creature pursuit with line-of-sight attacks, wall steering, and stuck recovery
- A* valley route planning with obstacle clearance and moving-target replanning, replacing circular wall-following with deliberate waypoint pursuit
- Committed pursuit state with detection hysteresis: recovery routes are discarded on clear sight, route switching is rate-limited, and enemies do not oscillate between chase and return-home behavior
- Full-width corridor checks, rotated-rock footprints, temporary failed-lane exclusions, and a nearest-walkable recovery prevent a large boss capsule from selecting or repeating a route that only its center point can clear
- Waypoint-progress monitoring detects circular sliding around any collider, including the village well, even when the creature is still physically moving
- True creature spawn anchors, leash behavior, attacker retreat on knockout, and an eight-second server-enforced sanctuary window that rejects queued repeat attacks
- Labeled camp structures with a short gameplay purpose for each development-stage addition
- A two-step development reset that restores only the Darkwood simulation and chronicle while retaining player accounts, characters, gear, and positions
- Mouse-wheel third-person camera zoom
- Phase 6-aware launcher health check and updated playtest instructions

## Simulation behavior

The current playtest uses one world minute per real minute. A single catch-up run is capped at 168 world hours. The worker stores the last successfully processed time and gives each queued progression block a deterministic key, so a restart cannot silently apply the same block twice.

The first three visible Darkwood stages are:

1. Encampment - hide tents and crude stockpile
2. Established Camp - palisade, hunter lodge, larger population capacity, and a Chieftain-led clan
3. Fortified Camp - watchtower, iron workshop, stronger military, and a Warlord-led clan

## Verification

- Full solution build: 0 warnings, 0 errors
- Automated tests: 25 passed, 0 failed
- Godot scene load: successful in the 4.7.1 .NET engine
- Development database migration: `20260717152347_Phase6OfflineWorldProgression`
- Live API identity: Phase 6, `offline-world-progression-ready`
- Live readiness: API and PostgreSQL Healthy
- Live account creation: Alden and Elara created successfully
- Live accelerated progression: Day 1 Encampment advanced to Day 2 Established Camp
- Live event queue after progression: 0 pending, 2 completed, 0 failed
- Live restart test: API and worker remained active; the restarted worker applied 0 duplicate hours or events
- Live gameplay update: API and worker active; level 20 Gorvak verified at 468 maximum health, 58 attack, and 38 defense
- Live knockout update: the API publishes Gorvak's true spawn separately from his current roaming position; the sanctuary and repeated-attack regression test passes
- Reset coverage: automated reset restored the initial Darkwood state while preserving both test characters; the live world was intentionally left intact
- Operator-facing API and worker logs include America/Chicago Central Time offsets

The migration and simulation run only against `rinkratt_living_realms_dev`. The production database remains untouched.

## Rollback assets

The server retains the Phase 5 API directory as `app-phase5-backup-for-phase6-20260717`, the Phase 5 migration executable as `efbundle-phase5-backup-for-phase6-20260717`, the pre-gameplay-update binaries as `app-phase6-backup-for-hotfix1-20260717` and `worker-phase6-backup-for-hotfix1-20260717`, and the pre-sanctuary API as `app-phase6-backup-for-hotfix2-20260717`. Database downgrade is intentionally not automated because Phase 6 creates persistent history and faction state.
