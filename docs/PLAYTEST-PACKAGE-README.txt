LIVING REALMS WINDOWS PLAYTEST 0.9.19

1. Extract the entire ZIP file to a normal folder.
2. Run LivingRealms.exe. Keep all extracted files together.
3. Create a separate player account or sign in with an existing account.
4. Choose Alden or Elara and enter Stonehaven Valley.

This build connects to the live Living Realms server at living-realms.com.
Account, character, inventory, construction, and world progress are stored on
that server and can follow the account between computers.

AUTOMATIC UPDATES
- Build 0.8.3 introduced the automatic updater.
- Future packages are downloaded, checksum-verified, installed, and relaunched
  when the game starts.
- Keep LivingRealms.exe, LivingRealms.Updater.ps1, LivingRealms.pck, and the
  data folder together in a writable folder.

CURRENT PLAYTEST SCOPE
- Shared persistent world: yes.
- Multiple separate player accounts: yes.
- Players visible to one another in real time: not yet.

IMPORTANT CONTROLS
- WASD / arrows: move
- Mouse: camera
- Mouse wheel: zoom
- Left click or F: attack
- H: chop a nearby tree or mine nearby exposed stone or iron
- B: deposit carried construction materials at a project marker
- I: inventory, capacity, gold, equipment, and buyers
- R: talk to a nearby resident
- U: return safely to Stonehaven's north gate if stuck
- F8: administrators only - show or hide detailed overhead statistics
- F10: release or recapture the mouse without pausing the realm
- F12: save a PNG screenshot to Pictures\Living Realms
- Escape: menu

BUILD 0.9.19

- Elowen is normalized to the established player and NPC scale.
- B1 farmhouses use the supplied detailed medieval-house model.
- Supplied trees, bushes, grass, and mossy rocks replace the placeholder nature art.
- Imported assets are now checked against a meter-based world-scale standard.

BUILD 0.9.18

- Replaces Elowen's shared prototype body with the supplied magic-girl
  Blender character as her unique healer/herbalist appearance.
- Preserves the authored PBR textures, witch hat, clothing, and hair while
  reducing oversized source textures to a game-safe resolution.
- Keeps Elowen's existing dialogue, medicine work, tonic trading, movement,
  targeting, and raid-support behavior.

BUILD 0.9.17
- Keeps a completely destroyed Stonehaven or Darkwood defeated for fifteen
  real minutes, even while no player is connected.
- Returns Stonehaven's eleven founders or Darkwood's seven founders after the
  recovery delay instead of restoring the settlement at full population.
- Rebuilds persistent stockpiles, farms, buildings, the mine, and the dock
  before repairing gates and walls.
- Consumes each settlement's real wood and stone stores while repairing.
- Blocks new major campaigns while either settlement is defeated or
  rebuilding.
- Shows the recovery countdown, founder return, current repair target,
  structure health, and rebuild progress on the Journey page.

BUILD 0.9.16
- Adds a separate Stonehaven Exchange and Darkwood Clan Hoard.
- Starts both banks with zero food, wood, stone, and iron; banks can resell
  only supplies their own faction previously sold to them.
- Automatically sells true settlement surpluses and buys shortages only when
  the matching bank has inventory and the faction treasury can afford it.
- Gives every resource a fixed bank purchase price and higher resale price.
- Records every transaction with quantity, price, bank gold, faction gold,
  and a Central-time ledger entry.
- Shows both bank inventories, balances, reserve targets, shortages, prices,
  and recent transactions on the Journey page.

BUILD 0.9.15
- Makes Irondeep in grid A3 the only source of newly created iron.
- Sends Dain and a named Darkwood miner on persistent trips to the mine, with
  visible travel position, carried cargo, delivered totals, and trip counts.
- Removes generic hourly iron gains; ore enters a faction store only after its
  worker physically returns home.
- Spends delivered iron on persistent weapon and armor tiers for Stonehaven
  and Darkwood, with combat bonuses and next-tier costs shown on Journey.
- Hires two named A3 mine guards when Darkwood contests Irondeep and pays each
  guard five town-treasury gold per world day.
- Shows the shared vein, mine health, both hauling operations, faction
  equipment, and Stonehaven guard payroll on the Journey page.

BUILD 0.9.14
- Starts Stonehaven with two named farmers and one named fisherman among its
  eleven founding residents.
- Makes every living Stonehaven resident and Darkwood goblin consume food.
- Makes farmers, fishermen, and hunters produce real food each world hour.
- Recruits workers to correct food shortages while respecting settlement
  population, food, housing, and recruitment costs.
- Adds ten persistent huntable rats and wolves outside the training yard.
- Sends named Stonehaven and Darkwood hunters after wildlife and records
  territorial hunting clashes when both factions compete.
- Shows food production, consumption, worker counts, shortages, reserves,
  recruitment needs, and wildlife status on the Journey page.

BUILD 0.9.13
- Uses one authoritative build identity for the login header and updater.
- Adds persistent health and armor for 34 Stonehaven and Darkwood assets.
- Makes campaign results damage real walls, gates, buildings, farms, mines,
  docks, and camp structures.
- Opens breached or destroyed wall sections for movement and pathfinding.
- Shows settlement asset totals and damage details on the Journey page.

BUILD 0.9.11
- Adds green Journey-page readiness panels for both major campaigns.
- Requires an online administrator to authorize either campaign before it starts.
- Adds separate administrator controls for the Darkwood raid and Stonehaven counterattack.
- Prevents creature attack requests from piling up and replaces rate-limit errors with automatic recovery.
- Spreads attackers around their targets instead of sending every creature into the same point.
- Adds stall recovery for roaming dragons and reduces player-avoidance oscillation.
- Keeps successor goblin leaders visibly identified as faction bosses.

BUILD 0.9.10
- Keeps collision floors active under persistent creatures on all nine grids,
  preventing A1 training animals and Darkwood goblins from falling underground.
- Recovers any creature that somehow drops below the playable terrain.
- Adds a bright gold ground ring beneath the currently selected creature.

BUILD 0.9.9
- Adds Emberwing, a red dragon, and Nightveil, a black dragon to Willowmere.
- Both dragons independently use Idle, Walk, Run, and Fly while roaming all
  nine connected map grids.
- Ground travel follows the world pathfinder while long cross-grid journeys
  use flight and safe landing positions.
- Player clearance and dragon-to-dragon separation prevent wings and bodies
  from repeatedly blocking the camera.

BUILD 0.9.8
- Replaces the fragile single-node gameplay request path so the persistent
  creature roster reliably reaches the world after character selection.
- Retries the first creature load three times and reports the loaded roster
  count instead of leaving "Loading persistent creatures..." on screen.
- Saves large creature rosters in server-safe batches so population growth does
  not make movement autosaves fail.
- Cycles the C1 review dragon through Idle, Walk, Run, and Fly every seven
  seconds and displays the current animation above the roost.

BUILD 0.9.7
- Adds a full-size, animated dragon model-review roost to Willowmere in grid C1.
- The dragon is non-hostile and currently plays its idle animation so its model,
  materials, proportions, and movement can be reviewed safely in the game.
- Clears a dedicated viewing area and adds a path, boundary stones, collision,
  and in-world attribution at the roost.
- Dragon model: "BGE Dragon 2.0" by 3DHaupt, used under CC BY from BlendSwap.

BUILD 0.9.6
- Fixes a startup request collision that could prevent every server creature from
  appearing even though its live record and position were correct.
- Loads the creature roster before the remaining world panels and serializes
  server requests so no startup response is silently skipped.
- Finishes adding creatures and residents to the active scene before enabling
  their AI, targets, and overhead details.

BUILD 0.9.5
- Restores the five named training creatures to the marked A1 testing grounds.
- Restores Darkwood's visible starting population when old world data leaves the
  C3 camp below its intended seven living goblins.
- Removes the invisible A2 water barriers, prevents the camera from filling the
  screen with the player model when it is pushed close, and gives the fishing
  skiff a solid hull.
- Reconnects the Mirrorwater dock road to the road north of the lake and replaces
  B1's overlapping road pieces with one continuous farmland road.
- Nessa now works beside the lumber yard and delivers timber to that project;
  Dain continues to supply the quarry works.
- Normal players see concise names and occupations or titles overhead. An
  administrator can press F8 to toggle full health, combat, skill, and duty data.
- Creature positions are repaired and bounded before autosave so one invalid
  creature can no longer cause the entire movement save to be rejected.

BUILD 0.9.4
- The 3x3 valley is divided into nine connected 96-by-96-meter regions that
  load the current cell and its directly connected neighbors.
- A1 remains the Test Creatures area. A2 now contains Mirrorwater Lake and a
  dock. A3 contains Irondeep Mine and the relocated stone quarry.
- B1 now contains eight working farm plots and two farmhouses. B2 remains
  Stonehaven, and C3 remains Darkwood's goblin camp.
- Stonehaven resets to 8 active named residents instead of reporting a hidden
  population of 84.
- At most one named resident can arrive per world day, and only when there is
  housing plus enough naturally produced food, timber, stone, and iron.
- The Living World panel now identifies Stonehaven's housing and supplies.
- Reeve Aldric Vale is Stonehaven's civil leader; Mira commands the guard.
- Mara Venn is preserved as the missing militia recruit from the concept lore.
- Residents expose a persistent skill, level, trait, experience, importance,
  and remembered history when players speak to them.
- NPC builders consume the same stored materials shown in the Chronicle.
- Named Darkwood members remain dead after permanent defeat. If the leader
  falls, the strongest living candidate takes command and the succession is
  recorded in world history.
- World time is explicit: one real minute equals one world minute.

BUILD 0.9.1
- Players can press F9 or use the Realm Menu to report bugs and request
  features, attach screenshots, and track player-visible ticket statuses.

BUILD 0.9.0
- Reset returns Stonehaven to 8 healthy residents and Darkwood to 7 goblins.
- The Chronicle clearly separates both factions and explains the population and
  event-readiness counts. Stonehaven's leader identity was superseded in 0.9.4.
- Darkwood automatically raids when 15 raid-ready goblins are available.
- A completed level 3 Darkwood camp causes the Guard Captain to assemble 20 named
  Stonehaven soldiers and militia. They march to Darkwood, fight the goblins,
  and then damage the camp until it loses one level or the force is defeated.
- Both battle types are persistent and visible in the Chronicle and game world.

BUILD 0.8.9
- Regular players see only Close Chronicles in the J-screen action area.
- World advance, world reset, refresh, and manual raid controls are admin-only.
- The live server rejects non-admin world advance and reset requests.

BUILD 0.8.8
- F attacks reliably even while the mouse has been released with F10.
- Left click continues to attack while the mouse is captured by the game.
- Combat refreshes stale targets immediately when another client, a raid, or a
  world reset changes the shared creature roster.

Windows may display an unrecognized-app warning because this early playtest is
not yet code-signed. The ZIP should only be downloaded from living-realms.com.
