# Living Realms Campaign Alignment Plan

This plan supersedes older phase labels that no longer match the implemented
playtest. It records the agreed simulation rules for the current alignment
work.

## Phase 1: stability and campaign controls — complete in 0.9.11

- Recover roaming dragons from stalled paths and landing loops.
- Spread creatures around combat targets and prevent queued combat requests
  from exhausting the API rate limit.
- Battles become ready from world conditions but require an online
  administrator to authorize their start.
- Show ready campaigns in green on the Journey page.
- Keep the current Darkwood leader visibly identified even after succession.

## Phase 2: destructible settlements — complete in 0.9.12

- Give persistent hit points and damage states to individual walls, gates,
  buildings, farms, the A3 mine, and Darkwood camp structures.
- Destroyed structures alter collision and navigation.

## Phase 3: complete campaigns — complete in 0.9.13

- Darkwood attacks with fifteen eligible raiders after administrator
  authorization and fights through defenders, walls, and buildings.
- Stonehaven attacks with twenty eligible residents after the level-three
  Darkwood camp is ready and an administrator authorizes the campaign.
- Both forces fight to a persistent resolution rather than disappearing on a
  timer.

## Phase 4: survival and workers — complete in 0.9.14

- Stonehaven starts with two farmers and one fisherman among its founding
  residents.
- Every living member consumes food.
- Farmers, fishermen, and hunters produce food.
- Settlements recruit or train the worker types needed to correct shortages,
  subject to housing, population, food, and recruitment costs.
- Huntable wildlife appears in suitable areas for both factions. Competing
  hunting parties can create small territorial battles.

## Phase 5: A3 iron and equipment — complete in 0.9.15

- A3 is the only source of newly created iron.
- Both factions must physically mine and transport it.
- Iron is consumed by persistent weapon and armor upgrades.
- Stonehaven may hire A3 guards for five gold per guard per world day.

## Phase 6: faction banks — complete in 0.9.16

- Stonehaven and Darkwood have separate banks.
- Each bank starts with zero resources and can only resell real inventory it
  purchased.
- The Journey page exposes both inventories, gold balances, prices,
  shortages, and recent transactions.

## Phase 7: destruction and recovery

- A completely destroyed settlement remains defeated for fifteen real
  minutes.
- Its founding population then returns and begins rebuilding.
- Destroyed functional structures are rebuilt first.
- Walls and gates are rebuilt only after the required structures are
  restored.
- Growth resumes naturally from the founding population.
