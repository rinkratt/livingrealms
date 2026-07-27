# Campaign Alignment Phase 6: Faction Banks

Build 0.9.16 completes the separate faction-bank phase.

## Separate closed inventories

- Stonehaven trades only with the Stonehaven Exchange.
- Darkwood trades only with the Darkwood Clan Hoard.
- Each bank starts with 300 gold but zero food, wood, stone, and iron.
- Banks cannot create resources. They can resell only inventory a faction
  previously sold to that same bank.
- A Living World reset clears both inventories and transaction histories and
  restores each bank's starting gold.

## Automatic settlement trade

- Each faction keeps a reserve target based on its population, development,
  and next equipment need.
- Surplus stored resources can be sold to that faction's bank, limited by the
  bank's available gold and hourly trade capacity.
- A shortage can be purchased from the bank only when that bank has real stock
  and the faction treasury can pay the listed price.
- The bank pays less than it charges, so every purchase and resale has a
  visible, consistent price.
- Construction, survival, camp progression, and equipment needs are resolved
  before surplus is offered to the bank.

## Persistent ledger and Journey display

- Every sale and purchase records the resource, quantity, unit price, total
  gold, bank balance, faction treasury balance, description, and Central-time
  timestamp.
- Journey shows both bank names, their separate gold balances, each faction's
  treasury, all four resource shelves, reserve targets, shortages, buy and sell
  prices, and recent transactions.
- Empty shelves explicitly show that no real inventory is available rather
  than silently providing unlimited supplies.
