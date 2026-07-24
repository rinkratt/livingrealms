LIVING REALMS WINDOWS PLAYTEST 0.9.2

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
- F10: release or recapture the mouse without pausing the realm
- F12: save a PNG screenshot to Pictures\Living Realms
- Escape: menu

BUILD 0.9.2
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

BUILD 0.9.1
- Players can press F9 or use the Realm Menu to report bugs and request
  features, attach screenshots, and track player-visible ticket statuses.

BUILD 0.9.0
- Reset returns Stonehaven to 8 healthy residents and Darkwood to 7 goblins.
- The Chronicle clearly separates both factions, names Captain Rowan as
  Stonehaven's Warden, and explains the population and event-readiness counts.
- Darkwood automatically raids when 15 raid-ready goblins are available.
- A completed level 3 Darkwood camp causes Captain Rowan to assemble 20 named
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
