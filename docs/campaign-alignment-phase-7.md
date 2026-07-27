# Campaign Alignment Phase 7: Destruction and Recovery

Build 0.9.17 completes the destruction-and-recovery phase for Stonehaven and
Darkwood.

## Persistent defeat

- A fully destroyed settlement enters a defeated state stored in PostgreSQL.
- Defeat lasts for fifteen real minutes. Accelerating world time does not
  shorten the real-time delay.
- Major campaigns cannot begin while either settlement is defeated or
  rebuilding.
- Journey shows the defeated settlement, Central-time timestamps, and the live
  recovery countdown.

## Founding population

- Stonehaven returns with its eleven named founding residents.
- Darkwood returns with its seven founding goblins, including Gorvak.
- Later recruits remain absent, so population must grow naturally again.
- The returning settlement receives only a small survival reserve and must
  resume worker production, consumption, trade, and recruitment normally.

## Ordered reconstruction

- Destroyed stockpiles, farms, houses, workshops, the mine, and the dock are
  restored before defensive structures.
- Gates, wall sections, and the Darkwood palisade become repair targets only
  after the required functional structures are operational.
- Every repair cycle consumes actual wood and stone owned by that settlement.
- Each structure retains its own hit points throughout recovery.
- Journey shows the current repair target, functional and defensive structure
  counts, total structure hit points, and completed rebuild cycles.

## Validation

- Automated tests verify the exact fifteen-minute delay, both founding
  populations, functional-before-defense ordering, completed reconstruction,
  campaign blocking, and Living World reset behavior.
- The complete solution passes 58 automated tests.
