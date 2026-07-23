# Phase 7B Architecture

## Runtime flow

```text
Godot C# Client
      |
      | Authenticated HTTPS (Phase 7B) / WebSocket (later phase)
      v
Plesk-managed Nginx
      |
      v
LivingRealms.Api on 127.0.0.1
      |
      +--------------------------+
      |                          |
      v                          v
PostgreSQL on 127.0.0.1   Future active services
      ^
      |
LivingRealms.Worker
```

The database is the source of truth for the complete world. Godot displays only the active portion relevant to the player. The worker advances inactive regions using elapsed-time calculations and durable, idempotent scheduled events.

## Project responsibilities

### LivingRealms.Domain

Contains persistence-neutral entities and enums. It has no dependency on ASP.NET Core, PostgreSQL, Entity Framework, Godot, or logging.

### LivingRealms.Infrastructure

Owns the Entity Framework Core context, PostgreSQL mappings, indexes, constraints, design-time factory, and migrations. Other processes consume it through dependency injection.

### LivingRealms.Api

Provides the authenticated HTTP boundary. Phase 2 adds registration, login/logout, opaque bearer sessions, character listing/selection/loading, and position persistence. Phase 4 adds persistent combat. Phase 5 adds inventory, loot, equipment, consumables, and skills. Phase 6 adds living-world state and history reads plus a development-only accelerated-time control. Phase 7A adds the authenticated Stonehaven resident roster and server-derived daily schedules. Phase 7B adds live raid state, playtest start/advance controls, and player-contribution hooks in both normal and skill combat. The WebSocket boundary remains a later phase.

### LivingRealms.Worker

Runs independently of player connections. Phase 6 calculates elapsed time on startup and each tick, queues uniquely keyed events, atomically claims them, advances the Darkwood Clan, records history, and recovers events left in processing by an interrupted worker. Phase 7B also starts eligible raids and advances active raid rounds during offline catch-up.

### LivingRealms.Client

Godot 4.7.1 C# desktop client. Phase 3 adds the procedural Stonehaven Valley and third-person play. Phase 4 adds persistent creature combat. Phase 5 adds Q/E archetype skills and inventory/equipment. Phase 6 adds the J-key Living World panel, chronicle, world-day HUD, and a Darkwood camp whose geometry reflects the persistent faction development stage. Phase 7A renders named residents, follows server schedule destinations, and adds R-key dialogue. Phase 7B adds marching raid attackers, a raid HUD, resident emergency behavior, and outcome/consequence display. The client communicates with the API, never PostgreSQL.

## Database model

The initial model creates 16 tables in the `living_realms` schema. Phase 5 adds `CharacterSkills`, Phase 7A adds `SettlementResidents`, and Phase 7B adds `SettlementRaids` plus `SettlementRaidAttackers`:

- Accounts
- Characters
- CharacterInventory
- Items
- Regions
- Settlements
- Factions
- FactionResources
- FactionStructures
- CreatureSpecies
- Creatures
- CreatureSkills
- CreatureEquipment
- ScheduledEvents
- WorldHistory
- PlayerSessions
- CharacterSkills
- SettlementResidents
- SettlementRaids
- SettlementRaidAttackers

Important indexes cover email lookup, character ownership, creature region/faction/status, scheduled-event status/time, unique idempotency keys, history time/region, and active player sessions.

## Decisions and assumptions

- .NET 8 is the shared target because it is stable, supported by the selected Godot .NET release, and appropriate for Linux deployment.
- Godot 4.7.1 .NET is pinned for repeatable client builds.
- PostgreSQL remains private on the Plesk server. No public database port is needed.
- API and worker are separate processes but share Domain and Infrastructure assemblies.
- A modular monolith is preferred over premature microservices.
- All database timestamps use UTC. Audit and operator-facing log events include America/Chicago Central Time (CST/CDT).
- Entity Framework migrations are generated and reviewed in source control, then applied through a controlled deployment step.
- Phase 7B implements the first persistent faction raid and resident consequences. Per-realm safety laws, resident attacks, economic trading, procedural quests, and live multiplayer remain later work.
