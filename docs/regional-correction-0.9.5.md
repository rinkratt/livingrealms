# Living Realms Regional Correction 0.9.5

This corrective release addresses the playtest problems reported after the
3x3-world alignment update. It does not reset player accounts, character
progress, inventories, settlement construction, or world history.

## Region corrections

- A1 now has a defined training yard with five marked creature positions.
- The five named training creatures are restored alive at those positions when
  this release is migrated.
- A2 no longer uses invisible collision boxes as lake boundaries.
- The dock road begins at the landward end of the dock, curves around the lake,
  and joins the east-west road north of Mirrorwater.
- The fishing skiff is identifiable and has a solid collision hull.
- B1 uses a single connected farmland road instead of overlapping straight road
  meshes through the crop rows.
- C3 repairs Darkwood to its intended minimum visible population of seven when
  older world state left the camp underpopulated.
- Living Darkwood members who are not participating in a scheduled or active
  raid return to their camp posts so the population reported by the world is
  physically represented in C3.

## Living work

- Nessa is the lumber-yard forewoman. She works beside the lumber yard and
  delivers harvested timber to the lumber-yard project.
- Dain remains the quarry foreman and supplies quarry works.
- Construction markers identify the responsible crew so visible activity and
  server-side contribution records describe the same work.

## Player and administrator visibility

- Regular players see an NPC's name plus occupation or title.
- Creature details remain visible when the creature is nearby, engaged, or
  selected, but health bars and full statistics are not permanently broadcast.
- Administrators can press F8 to toggle the detailed overhead view.

## Reliability

- Creature locations are checked for finite values and bounded to the playable
  world before autosave.
- The camera hides the local character model when an obstruction pushes the
  camera into the character's head.
- API coverage verifies that Nessa contributes to the lumber yard rather than
  silently advancing Stonehaven's curtain wall.
