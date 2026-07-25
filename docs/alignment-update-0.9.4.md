# Living Realms Alignment Update 0.9.4

This update reconciles the working vertical slice with the Game Concept Bible without deleting player accounts, characters, inventory, map progress, or world history.

## Canonical identity

- Stonehaven's civil leader is Reeve Aldric Vale. He coordinates stores, work, households, and defense policy.
- Mira is Guard Captain and commands patrols, raid defense, and Stonehaven's twenty-person counterattack.
- Mara Venn is the missing militia recruit described by the lore. She is not silently repurposed as an innkeeper.
- Every resident has a stored primary skill and level, trait, experience total, major/minor importance flag, and memory summary. These fields are returned by the API and explained when the player talks to the resident.

## Persistent death and succession

- Ordinary factionless wildlife and test encounters may respawn.
- Named faction members permanently leave the active population when defeated.
- A defeated faction leader remains dead. The strongest living eligible member becomes the new named leader, inherits an appropriate title, and receives a Chronicle entry.
- If no eligible member survives, the faction enters a visible leadership crisis.
- New clan recruits receive unique persistent names rather than reusing the identity of a dead member.

## One construction economy

- NPC builders no longer create project materials from nowhere.
- Nessa and Dain draw timber and stone from Stonehaven's stored supplies.
- Skrit and Vrak draw timber and stone from Darkwood's faction stores.
- Work records the actual amount consumed and keeps a small emergency reserve.
- Camp stage requirements recognize materials already committed to the visible palisade, avoiding a second hidden charge for the same construction.

## Time and compatibility

- The authoritative rate is one real minute equals one world minute.
- The database migration adds resident identity and memory fields and updates the existing seed residents in place.
- Existing accounts, characters, inventory, construction projects, faction resources, and Chronicle history are preserved.

## Still intentionally partial

The current multiplayer slice shares server-persisted world, combat, raid, resident, inventory, and construction state, but it is not yet a fully server-authoritative real-time MMO simulation. Client movement and visual interpolation remain part of the present playtest architecture and should be upgraded in a later networking-focused milestone.
