# Campaign Alignment Phase 3

Build 0.9.13 completes the Persistent Campaigns phase.

## Authorization and readiness

- Darkwood requires fifteen living, raid-ready fighters excluding its current
  leader before an administrator may authorize the campaign.
- The current Darkwood leader has a persistent 35 percent campaign roll and
  may join as an additional attacker.
- Stonehaven requires twenty living residents and a completed level-three
  Darkwood camp before an administrator may authorize its counterattack.
- Regular players cannot authorize campaigns. Administrators always see both
  Journey controls, disabled with a clear not-ready label until their
  conditions are met.

## Persistent Darkwood campaign

- The selected raid roster is stored before the army moves.
- The campaign advances through assembling, marching, fighting Stonehaven's
  defenders, and attacking Stonehaven's persistent structures.
- Raiders cannot damage structures until Stonehaven has no living front-line
  defender.
- After the defenders fall, each campaign round damages the real wall, gate,
  farm, mine, dock, stockpile, or building selected by structure priority.
- Darkwood wins only after the remaining built Stonehaven structure health
  reaches zero. Stonehaven wins only after every selected attacker is
  defeated; the final wounded raider no longer retreats automatically.

## Persistent Stonehaven counterattack

- The twenty selected named residents remain tied to the assault through
  assembling, marching, fighting all living Darkwood defenders, and attacking
  the camp.
- Stonehaven cannot damage camp structures until the goblin defenders are
  defeated.
- Stonehaven wins only after the standing Darkwood structure health reaches
  zero. Darkwood wins only after every assault member is defeated.

## Continuity and visibility

- Campaign phase, phase progress, roster, casualties, force strength,
  structure strength, timestamps, and outcome remain in PostgreSQL.
- The server continues a started campaign after menus, disconnects, client
  restarts, API restarts, or worker restarts.
- The connected client and background worker share the same minimum round
  interval, preventing both stalled campaigns and double advancement.
- Journey shows the live campaign phase, living roster count, combat strength,
  structure health, and final chronicle outcome.

## Verification

- 52 .NET tests pass, including persistent structure victory, administrator
  authorization, final-raider fight-to-the-death, and both campaign outcomes.
- The Phase 3 EF Core migration upgrades existing completed campaigns to the
  resolved phase and safely resumes any pre-existing active raid at defender
  combat.
