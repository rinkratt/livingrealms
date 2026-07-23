# Living Realms

Living Realms is a persistent medieval-fantasy online world where creatures learn, factions expand, settlements change, and history continues while players are offline.

This repository contains the promotional website plus the account, combat, living-world, NPC, raid, visual, and settlement-development vertical slices. Players can register or log in through the Godot client, select Alden or Elara, explore Stonehaven Valley in third person, meet named residents, fight persistent creatures, gather renewable wood and stone, contribute shared resources to independent three-tier construction projects, and help Stonehaven's guards repel persistent Darkwood raids.

## Repository structure

```text
Living Realms/
|-- client/
|   `-- LivingRealms.Client/        Godot 4.7.1 C# desktop client
|-- src/
|   |-- LivingRealms.Api/           ASP.NET Core HTTP API and health checks
|   |-- LivingRealms.DiscordBot/    Discord /ask assistant backed by OpenAI
|   |-- LivingRealms.Domain/        World entities and domain enums
|   |-- LivingRealms.Infrastructure/PostgreSQL and Entity Framework Core
|   `-- LivingRealms.Worker/        Offline-world background service
|-- tests/
|   |-- LivingRealms.Api.Tests/     API integration tests
|   `-- LivingRealms.Domain.Tests/  Domain foundation tests
|-- deploy/
|   `-- postgres/compose.yml        Optional local PostgreSQL development instance
|-- docs/                            Architecture and phase completion notes
|-- assets/3d-source/                Editable Blender source artwork
|-- tools/blender/                   Reproducible Blender asset-build scripts
|-- assets/, content/, includes/     Promotional website assets and PHP components
|-- LivingRealms.slnx               Complete .NET/Godot solution
`-- dotnet-tools.json                Repository-local EF Core tool
```

## Work on another computer

The source of truth is the GitHub repository at
`https://github.com/rinkratt/livingrealms`. On another Windows computer:

```powershell
git clone https://github.com/rinkratt/livingrealms.git
cd livingrealms
dotnet tool restore
dotnet restore LivingRealms.slnx
```

Install the required software below, then double-click
`Start-Living-Realms-Playtest.cmd` or import
`client/LivingRealms.Client/project.godot` into Godot. The launcher searches for
Godot on `PATH` and in the project's usual local install locations. If necessary,
set the `GODOT_EXE` environment variable to the full path of the Godot 4.7.1 .NET
executable.

Before starting work on either computer, run `git pull`. When a tested change is
ready to share, commit it and run `git push`. Generated builds, server secrets,
local databases, caches, and machine-specific files are intentionally excluded
from GitHub.

## Required software

- .NET 8 SDK or newer. The repository targets .NET 8 and permits newer installed SDKs.
- Godot 4.7.1 **.NET edition** for C# support. The Standard editor cannot compile this client.
- PostgreSQL 17 for the game database. The intended hosted instance is the private PostgreSQL service on the Plesk server.
- Blender 5.2 LTS for regenerating Phase 8 artwork. Blender is not required merely to run the included GLB assets.
- Optional: Docker or Podman for the local-only PostgreSQL configuration in `deploy/postgres/compose.yml`.
- Git and a C# IDE such as Visual Studio 2022/2026, Rider, or VS Code.

## Restore and build

```powershell
dotnet tool restore
dotnet restore LivingRealms.slnx
dotnet build LivingRealms.slnx --configuration Debug
dotnet test LivingRealms.slnx --configuration Debug
```

## Regenerate the Stonehaven visual pass

The editable Blender scene, Godot GLB, and review render are generated together so their scale and placement remain synchronized:

```powershell
& 'C:\Users\Kelly\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' `
  tools\blender\generate_stonehaven_textures.py

& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' `
  --background `
  --python tools\blender\build_stonehaven_vertical_slice.py
```

The outputs are:

- `assets/3d-source/stonehaven/stonehaven_vertical_slice.blend` - editable source.
- `assets/3d-source/stonehaven/textures/` - generated PBR source textures.
- `client/LivingRealms.Client/Assets/Environment/stonehaven_vertical_slice.glb` - Godot runtime asset.
- `docs/phase-8-stonehaven-preview.png` - review render.

Godot keeps the existing primitive collision and pathfinding shapes as invisible gameplay geometry. If the GLB is missing or fails to import, the client automatically falls back to the prototype visuals.

## Database configuration

The API and worker read the same standard .NET configuration key:

```text
ConnectionStrings__GameDatabase
```

The checked-in configuration contains no password. For local development with a password-protected database, set the connection string in your shell or .NET user secrets. Never put production credentials in this repository.

Example for the current PowerShell session:

```powershell
$env:ConnectionStrings__GameDatabase = 'Host=127.0.0.1;Port=5432;Database=living_realms_dev;Username=living_realms;Password=YOUR_LOCAL_PASSWORD'
```

On the Plesk server, the play-test API connects to PostgreSQL over `127.0.0.1:5432`. PostgreSQL is not exposed publicly. The real development connection string is stored in a private server environment file and is not checked into this repository.

### Optional local PostgreSQL

The Compose file binds PostgreSQL only to `127.0.0.1` and uses trust authentication strictly for local development:

```powershell
docker compose -f deploy/postgres/compose.yml up -d
```

Do not use that Compose authentication configuration on a public or production host.

## Entity Framework migrations

The Phase 1, Phase 2, Phase 4, Phase 5, Phase 6, Phase 7A, and Phase 7B migrations are generated under `src/LivingRealms.Infrastructure/Persistence/Migrations`. Phase 7B adds persistent settlement raids and raid-attacker participation, including player contribution, casualties, injuries, structural damage, and outcomes.

```powershell
dotnet tool run dotnet-ef database update `
  --project src/LivingRealms.Infrastructure/LivingRealms.Infrastructure.csproj `
  --startup-project src/LivingRealms.Api/LivingRealms.Api.csproj
```

Database migrations are a controlled operation. The API and worker do not apply them automatically at startup. The play-test migrations are applied only to the Plesk **development** database. The production database remains untouched.

## Run the API

```powershell
dotnet run --project src/LivingRealms.Api/LivingRealms.Api.csproj
```

Endpoints:

- `GET /api` - Phase 7 service identity
- `GET /health/live` - process liveness; does not require PostgreSQL
- `GET /health/ready` - readiness; verifies PostgreSQL connectivity
- `POST /api/v1/accounts/register` - create a player account and its Alden/Elara characters
- `POST /api/v1/auth/login` - start an authenticated player session
- `POST /api/v1/auth/logout` - revoke the current session
- `GET /api/v1/characters` - list characters owned by the player
- `GET /api/v1/characters/current` - load the selected character
- `POST /api/v1/characters/{id}/select` - select an owned character
- `PUT /api/v1/characters/{id}/position` - save the selected character's position
- `GET /api/v1/regions/stonehaven-valley/creatures` - load persistent encounters and respawn state
- `PUT /api/v1/regions/stonehaven-valley/creatures/positions` - persist active creature positions
- `POST /api/v1/combat/player-attack` - resolve an authenticated Alden or Elara attack
- `POST /api/v1/combat/creature-attack` - resolve an authenticated creature attack
- `GET /api/v1/inventory` - load the selected character's inventory, equipment, and combat stats
- `POST /api/v1/inventory/{entryId}/equip` - equip an owned weapon or armor item
- `POST /api/v1/inventory/{entryId}/unequip` - unequip an owned item
- `POST /api/v1/inventory/{entryId}/use` - use an owned consumable
- `GET /api/v1/skills` - load the selected character's archetype skills and cooldown state
- `POST /api/v1/combat/player-skill` - resolve a server-authoritative offensive or healing skill
- `GET /api/v1/world/state` - load persistent faction, settlement, event-queue, and chronicle state
- `GET /api/v1/world/history` - load up to 50 persistent world-history entries
- `POST /api/v1/world/advance` - advance 1-168 world hours in the development playtest
- `GET /api/v1/regions/stonehaven-valley/residents` - load named residents and their server-driven daily activities
- `GET /api/v1/world/raid` - load the current or latest persistent Stonehaven raid
- `POST /api/v1/world/raid/start` - start a Darkwood raid in the development playtest
- `POST /api/v1/world/raid/advance` - advance one live raid combat round

## Run the world worker

```powershell
dotnet run --project src/LivingRealms.Worker/LivingRealms.Worker.csproj
```

The Phase 6 worker calculates elapsed real time, queues idempotent scheduled events, advances faction state, heals or respawns inactive creatures, records world history, and recovers interrupted events after a restart. The playtest runs at one world hour per real minute and caps a single offline catch-up at seven world days.

## Run the Discord AI bot

The Discord bot exposes a guild-scoped `/ask` command and does not request the
privileged Message Content intent. Configure its Discord token and OpenAI API
key with environment variables or .NET user secrets, then run:

```powershell
dotnet run --project src/LivingRealms.DiscordBot/LivingRealms.DiscordBot.csproj
```

See `docs/discord-bot.md` for Discord application setup, local secret storage,
permissions, and deployment guidance.

## Run the Godot client

The quickest option on Windows is to double-click `Start-Living-Realms-Playtest.cmd` in the repository root. It finds the locally installed Godot 4.7.1 .NET executable, checks the live API, and opens the game client.

To open it manually:

1. Start the Godot 4.7.1 .NET editor.
2. Import `client/LivingRealms.Client/project.godot`.
3. Allow the editor to restore/build the C# project.
4. Run the main scene.

The client includes persistent raids and the first construction economy. Press **H** beside a marked tree or stone deposit to gather, then press **B** beside a Stonehaven project marker to contribute a bundle from the settlement stores. Walls and buildings add visible sections as their own meters advance; completed tiers improve defense, structural strength, or gathering yield. Press **R** near a resident to talk and **J** to inspect the Living World. The client defaults to the live play-test API at `https://living-realms.com/game-api`; override it with `LIVING_REALMS_API_URL` only for local development. See `docs/playtest-runbook.md` for the complete test script.

## Phase 2 security behavior

- Passwords are hashed with ASP.NET Core's versioned password hasher and are never returned by the API.
- Session tokens contain 256 bits of cryptographic randomness; only their SHA-256 hashes are stored.
- Sessions expire after 12 hours by default and are revoked on logout.
- Registration and login are throttled per IP address.
- Character reads and writes enforce account ownership.
- Position writes require the character to be selected and reject non-finite or unreasonable coordinates.
- Session audit events capture account/session identifiers, IP address, user agent, and an America/Chicago timestamp. Database timestamps remain UTC for correctness.

## Security rules

- Never commit passwords, tokens, production connection strings, or API keys.
- Never connect the Godot client directly to PostgreSQL.
- Keep PostgreSQL bound to the server loopback interface/private socket.
- Keep the API behind the configured Plesk-managed Nginx HTTPS route.
- Apply migrations through an explicit deployment step, not automatically on every startup.
