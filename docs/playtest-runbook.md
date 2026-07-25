# Living Realms Current Play Test

This play test exercises the live account-and-character flow, the walkable Stonehaven Valley, persistent creature combat and progression, plus the offline Darkwood Clan simulation and world chronicle.

## Start the client

1. Double-click `Living Realms Play Test.lnk` in the Living Realms folder. The original `Start-Living-Realms-Playtest.cmd` also works.
2. Wait for the Living Realms window to open.
3. Confirm the sign-in panel shows `API: https://living-realms.com/game-api`.

If the launcher cannot find Godot, open the Godot 4.7.1 .NET editor, import `client/LivingRealms.Client/project.godot`, and run the main scene.

## Sign in or create a player

1. Enter the email address for your player account.
2. Enter the player password. New passwords must be 12 to 128 characters and contain uppercase, lowercase, number, and symbol characters.
3. Click **Portal Login**, or click **Create Player Account** for a new player.
4. Confirm that both **Alden - Vanguard** and **Elara - Ranger** appear.

Use a unique player password rather than a website or server-administrator password.

## Enter Stonehaven Valley

1. Select Alden for the male melee test character or Elara for the female ranged test character.
2. Confirm the 3D Stonehaven Valley scene opens at that character's restored position.
3. Walk through the gate and village. Buildings, walls, trees, rocks, the bridge, ridges, player body, and camera all have basic collision.

The world is divided into nine connected 96-by-96-meter regions. The client keeps the
current region and its directly connected neighbors loaded as the player crosses a
boundary:

| Region | Current purpose |
|---|---|
| A1 | Test Creatures area |
| A2 | Mirrorwater Lake and dock |
| A3 | Irondeep Mine and relocated stone quarry |
| B1 | Eight farm plots and two farmhouses |
| B2 | Stonehaven |
| B3 | Eastern wilds |
| C1 | Western wilds |
| C2 | Darkwood approach |
| C3 | Darkwood goblin camp |

Controls:

- **WASD** or **arrow keys** - move
- **Mouse** - rotate the third-person camera
- **Mouse wheel** - zoom the third-person camera from a close view out to a wide 18-meter view
- **Shift** or **Ctrl** - sprint
- **Space** - jump
- **Left mouse button** or **F** - attack the nearest creature in range
- **Q** - offensive archetype skill: Shield Bash for Alden or Piercing Shot for Elara
- **E** - healing archetype skill: Second Wind for Alden or Field Dressing for Elara
- **I** - open or close inventory and equipment
- **J** - open or close the Living World and Chronicle panel
- **H** - chop a marked tree or mine a marked stone deposit
- **B** - contribute a resource bundle at the nearest Stonehaven construction marker
- **R** - talk to a named resident when standing nearby
- **F5** - save immediately
- **Esc** - open or close the Realm Menu

The Realm Menu can resume, save, return to character selection, or save and log out.

## Test position persistence

1. Move to a recognizable location such as the village well or bridge.
2. Press **F5**, wait for the saved confirmation, and use **Esc > Save and Log Out**.
3. Sign in again and select the same character.
4. Confirm that the character returns to the saved location.
5. Repeat with the other character if desired.

Position also saves automatically every ten seconds. Closing the window requests one final save before quitting.

## Test Stonehaven residents

1. Enter Stonehaven and confirm eight named residents appear with their role, current activity, and health above them.
2. Find Reeve Aldric Vale, Guard Captain Mira, guard Tomas, Brann, Elowen, Oren, Nessa, and Dain around the gate and village buildings. Mara Venn should appear in the roster as missing, not as a visible innkeeper.
3. Stand within a few steps of a resident and press **R**. Confirm that resident's name, role, dialogue, and current activity appear in the HUD message area.
4. Press **R** when nobody is nearby and confirm the client tells you to move closer.
5. Press **J**, advance the world clock, close the panel, and allow up to 15 seconds for a roster refresh. Confirm guards patrol or guard the gate and workers move between work and home according to the world hour.
6. Open a menu while residents are moving and confirm their movement pauses until the menu closes.

## Test creatures and combat

1. Walk through the gate and approach a Forest Rat. Its name and health appear when it becomes the nearest target.
2. Use **left mouse** or **F** to attack. Alden fights at close range; Elara can shoot from farther away.
3. Let a creature detect and chase you. Confirm that it stops at attack distance and reduces player health.
4. Put a wall, well, rock, or village structure between you and a pursuing creature. Confirm that it does not attack through the obstacle, immediately commits to one waypoint route around it, leaves enough room for its full body, continues toward you through an opening, drops the recovery route as soon as direct pursuit is possible, and rejects a route whenever its distance to the waypoint stops improving—even if collision sliding is still moving its body.
5. Defeat the creature. Confirm that the HUD reports the XP award and updates the XP bar.
6. Wait for an ordinary wildlife or test encounter to respawn, or leave and return after its respawn time.
7. Approach stronger Prairie Wolves and factionless test Goblin Raiders only after verifying the basic rat fight. Named Darkwood clan members do not respawn after permanent defeat. If the current Darkwood leader falls, inspect **J** to confirm a named successor and Chronicle entry.

## Test loot, equipment, and skills

1. Press **I** and confirm the selected character begins with a weapon and leather guard equipped.
2. Close the inventory and defeat a Forest Rat. Confirm the combat message names the dropped Rat Tail and Field Tonic.
3. Press **I** again and confirm the loot persisted. Use a Field Tonic after taking damage and confirm it restores health and reduces its stack count.
4. Defeat a Prairie Wolf, open the inventory, and equip its Wolf Pelt armor. Confirm the displayed Defense total increases.
5. Use **Q** on a creature in range. Alden's Shield Bash is close-range; Elara's Piercing Shot reaches farther.
6. Take damage and use **E** to heal. Attempting either skill again before its cooldown expires should show the remaining cooldown.
7. Defeat a Goblin Raider or Gorvak and equip the weapon matching the selected character. Confirm the displayed Attack total increases.
8. Log out, sign back in, select the same character, and confirm the inventory, equipped gear, skill progress, health, and world position were retained.

## Test the living world

1. Press **J** and confirm the panel shows the world day, real-time simulation speed, Darkwood Clan population, resources, structures, the current leader, Stonehaven's named living population, housing, supplies, defenses, Reeve Aldric Vale's leadership profile, scheduled-event counts, and the recent chronicle.
2. Use **Advance 24 World Hours**. This control exists only on the development playtest.
3. Confirm population and resources change, the current Darkwood leader progresses, construction consumes the displayed timber and stone, a completed scheduled event appears, and a new Central Time chronicle entry is recorded.
4. Confirm the current Darkwood leader's health, attack, and defense rise with level.
5. Close the panel and travel north beyond Stonehaven. Confirm the visible Darkwood camp matches the stage named in the panel and that each advanced structure has a purpose label.
6. Close the game for several real minutes, reopen it, log back in, and press **J**. Confirm the world advanced for the elapsed offline time.
7. Repeatedly advancing the same event or restarting the worker must not apply the same world-time block twice.
8. To restart the faction test, click **Reset Darkwood World** and then click the confirmation within five seconds. This clears Darkwood progression and chronicle history but keeps player accounts, characters, inventory, equipment, and saved positions.

Stonehaven resets to eight active named residents. It can gain no more than one new
named resident per world day, and only when housing space and the required food,
timber, stone, and iron are available. The farms and workers create supplies over
time; the simulation does not manufacture a hidden population or instantly restore
the former placeholder count of 84.

## Test the first Stonehaven raid

1. Press **J** and click **Start Darkwood Raid [Playtest]**.
2. Close the panel. Confirm four named Darkwood attackers march from the north toward Stonehaven's open gate instead of waiting at their spawn points.
3. Confirm the HUD displays live Darkwood and Stonehaven strength. Reopen **J** to see attackers standing, player contribution, and the current battle state.
4. Confirm guards report **Defending Stonehaven** and civilians report **Sheltering from the raid**.
5. Defeat one or more raid attackers. Confirm the combat message reports raid-strength contribution and that a defeated raid attacker is retired permanently rather than respawning.
6. Let the five-second battle rounds continue. Stonehaven wins when attacker strength reaches zero; Darkwood wins when defender strength reaches zero.
7. Confirm the final panel reports settlement damage, resident injuries/casualties, and the outcome, and that the World Chronicle records it in Central Time.
8. Use the confirmed **Reset Darkwood Simulation** control to remove the playtest raid and restore Stonehaven's settlement and named residents.

If health reaches zero, the character is restored to full health at Stonehaven's gate. The attacker immediately retreats to its spawn and Stonehaven's sanctuary prevents all creature targeting and player combat for eight seconds, which also invalidates queued attacks from the defeated encounter. Creature health, defeated/respawn state, player health, experience, level, and both player and creature positions are stored by the server.

## Test gathering and construction

1. Walk to a tree, stone deposit, or Irondeep ore vein with a gold resource label and press **H** while close to it.
2. Confirm its remaining amount falls and Stonehaven's matching shared supply rises. Depleted sites recover after a short real-time delay.
3. Walk to a Stonehaven wall, lumber-yard, or quarry construction marker and press **B**. Confirm only that project's requirements advance.
4. Continue contributing and confirm visible foundations, framing, wall sections, and completed building pieces appear as the meter advances.
5. Confirm Nessa travels to the forest, Dain works the quarry, and Skrit and Vrak leave the Darkwood camp to gather for its palisade.
6. Complete a wall tier and confirm Stonehaven defense, guard strength, and structural integrity rise. Lumber-yard tiers increase wood yield; quarry tiers increase stone yield. Darkwood palisade tiers increase its military strength and morale.

## Expected results

- Account creation and login use the live authenticated API.
- Alden and Elara retain separate saved positions.
- The third-person camera rotates with the mouse and retracts near world collision.
- Forest Rats, Prairie Wolves, Goblin Raiders, and Gorvak detect, chase, attack, return to their spawn area, die, and respawn.
- Pursuing creatures use a valley obstacle map to plan routes around walls, towers, houses, trees, and rocks instead of circling a collision until an opening is found.
- Alden uses a short-range melee attack and Elara uses a longer-range bow attack.
- Damage, range, cooldowns, XP awards, leveling, defeat, knockout, and respawn are validated by the API rather than trusted to the client.
- Creature defeats award persistent loot, and only owned compatible equipment can alter the selected character's server-calculated Attack and Defense totals.
- Alden has Shield Bash and Second Wind; Elara has Piercing Shot and Field Dressing. Skill cooldowns are stored by the server.
- The player cannot walk through the ground, settlement buildings, walls, trees, large rocks, bridge, or boundary ridges.
- The HUD displays character identity, level, archetype, health, XP, target health, skill hotkeys, region, coordinates, combat feedback, and save status. The inventory overlay displays equipment, item rarity, quantity, bonuses, and available actions.
- The Living World panel and northern camp reflect the same persistent faction state. The world advances without a connected player and records durable history.
- Stonehaven resets with eight active named residents. Every visible population increase creates another named resident with a real role, status, home, and workplace.
- Every wall and building has an independent, persistent three-tier resource meter and applies real statistical bonuses when a tier completes.
- Darkwood raids persist on the server, resolve while players are online or offline, retire defeated raid attackers, count player contribution, and apply settlement/resident consequences.
- Logout revokes the current session token.
- Audit events include account/session identifiers, IP address, user agent, and an America/Chicago Central Time timestamp. Database timestamps remain UTC for reliable storage.

## Service check

The live health check is `https://living-realms.com/game-api/health/ready`. A healthy response reports both the API and PostgreSQL as `Healthy`.

This play test uses `rinkratt_living_realms_dev`. The production database remains intentionally untouched.
