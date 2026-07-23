# Phase 7B Completion - The First Stonehaven Raid

Phase 7B turns the Phase 7A resident foundation into a playable world event. A Darkwood raid is now persistent PostgreSQL state rather than a temporary client encounter.

## Delivered

- Persistent `SettlementRaid` and `SettlementRaidAttacker` records
- Four scaled Darkwood attackers that march toward Stonehaven and never respawn after the raid
- Server-authoritative attacker and defender strength with five-second live rounds
- Player raid contribution when an attacker is defeated by a normal attack or archetype skill
- Guards defending and civilians sheltering while the raid is active
- Persistent guard health, resident injury/casualty state, settlement damage, stolen resources, and faction morale effects
- Defender-win and attacker-win chronicle entries logged with Central Time presentation
- Offline raid advancement through the Phase 6 world worker
- Development-only raid start control and a full reset that restores Stonehaven
- Godot HUD and Living World raid summary

## Database and API

- Migration: `20260717221638_Phase7BStonehavenRaids`
- API identity: Phase 7, `first-raid-ready`
- Authenticated routes: `GET /api/v1/world/raid`, `POST /api/v1/world/raid/start`, and `POST /api/v1/world/raid/advance`

## Deliberate boundary

Stonehaven remains a protected settlement where the player cannot attack residents. Per-realm law, unsafe cities, resident combat choices, branching dialogue, trade, quests, and multiplayer raid coordination belong to later phases.
