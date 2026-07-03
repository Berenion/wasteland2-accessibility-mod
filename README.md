Wasteland 2 Accessibility Mod

A screen-reader and keyboard accessibility mod for Wasteland 2 Director's Cut. Built for blind and low-vision players, and for anyone who prefers keyboard-only play.

Status: public beta. Please report what you find (see Reporting bugs).

Sections in this file, in order. Search for a name to jump to it:
- What it does
- Requirements
- Installing
- Configuration
- Reporting bugs
- Controls
- Original game keys in combat
- Key conflicts
- Building from source
- Credits and license


What it does

- Speaks UI, dialogue, combat events, item info, and world objects through NVDA, JAWS, or Windows SAPI (via the Tolk library).
- Full keyboard navigation in every screen: menus, character creation, inventory, loot, shops, conversations, the world map, combat, and dialogs.
- A virtual grid cursor for exploration and combat: move tile by tile, hear what is on each tile, and act on it. Optionally confine it to walkable ground so it stops at walls.
- A scanner that cycles nearby interactables by category, plus a nearby-scan key (L) that lists everything within an adjustable radius of the cursor, each with its direction.
- Optional sound cues when a new item enters the scanner, one per category, with a glossary in the settings menu to learn and preview them.
- A "party stopped" notification when an ordered move finishes, in exploration and on the world map; ungrouped members are named individually.
- A world-map review cursor with POI and radiation alerts, water-cost estimates, and POI cycling.
- Tactical pause (Space) that freezes time outside combat. Auto-pauses for inventory, loot, and vendor screens.
- Camera rotation lock so "up" stays north (F10 toggles it).


Requirements

- Wasteland 2 Director's Cut (Steam or GOG).
- MelonLoader 0.5.7 Open-Beta, from https://github.com/LavaGang/MelonLoader/releases/tag/v0.5.7 . Do not use 0.6.x or newer; they crash with this game.
- Tolk.dll: bundled in the release archive. Copy it next to WL2.exe.
- nvdaControllerClient64.dll: bundled in the release archive too. NVDA needs it; copy it next to WL2.exe. Without it, NVDA falls back to SAPI.
- A screen reader is optional: NVDA, JAWS, or any SAPI voice. With none running, Tolk uses SAPI.

The mod targets .NET Framework 3.5; MelonLoader installs that runtime for you.


Installing

The release archive mirrors the game's Build folder, so the quickest path is to extract it straight into your Wasteland 2 Build directory after MelonLoader is installed: the mod DLL, Tolk.dll, and nvdaControllerClient64.dll land in the right places. In full:

1. Install MelonLoader 0.5.7. Use MelonLoader.Installer.exe from the 0.5.7 release linked above, not the latest installer (the latest one pulls a newer MelonLoader that crashes). Point it at the folder holding WL2.exe and install. The installer does not find Wasteland 2 on its own, so use its game selector to browse to WL2.exe yourself rather than waiting for it to appear in a list. On Steam that folder is ...\steamapps\common\Wasteland 2 Director's Cut\Build\ . If a newer MelonLoader is already installed and the game crashes on launch, delete version.dll, dobby.dll, and the MelonLoader folder from the game directory first, then install 0.5.7.
2. Copy Wasteland2AccessibilityMod.dll into <game>\Mods\ . MelonLoader creates the Mods folder on first launch.
3. Copy Tolk.dll next to WL2.exe, not into Mods. NVDA users: also copy nvdaControllerClient64.dll there (both ship in the archive).
4. Start your screen reader, then launch the game.

To confirm it loaded, open <game>\MelonLoader\Latest.log and look for a "Wasteland 2 Accessibility Mod" line and a "Screen reader detected" line. If they are missing, recheck that the DLL is in Mods and that Tolk.dll sits next to WL2.exe, then see Reporting bugs.


Configuration

The mod writes <game>\UserData\Wasteland2Accessibility.cfg on first run. Edit it directly, use the in-game settings menu (Shift+S), or use the per-setting quick toggles; changes save automatically.

The settings menu (Shift+S, available anywhere) lists every setting below as a navigable on/off list: Up and Down move, Enter or Left/Right toggle the focused setting, Escape closes. The single-key quick toggles still work if you prefer them. The last item is a sound glossary: press Enter to open it, then Up and Down to hear each scanner category sound and what it means, Enter to replay, Escape to go back.

- UseClockPositions (default off): speak directions as clock positions ("3 o'clock") instead of compass names. Toggle in-game with =.
- ObjectNamesFirst (default off): lead tile announcements with the object name instead of the coordinate. Toggle with K.
- UseTileDistances (default on): report distances in tiles when a combat grid is available (one tile is about 1.6 metres); otherwise in metres.
- ConveyElevation (default on): announce terrain height changes and height relative to the party. Toggle with H.
- AnnounceLineOfSight (default off): say whether a tile is in sight of the active character (perception in exploration, clear line of fire in combat). Toggle with Y.
- AnnouncePartyStopped (default on): announce when the party finishes an ordered move and comes to rest, in exploration and on the world map. When members move separately (ungrouped), the one that stopped is named.
- ScannerCategorySounds (default on): play a short sound cue when a new item enters the exploration scanner, with a distinct sound per category (characters, containers, objects, exits, examine, loot, and a generic cue for miscellaneous). Party members get no cue.
- CursorBlockedByTerrain (default off): confine the exploration grid cursor to walkable ground. When on, a single step onto a wall or terrain tile is refused and the obstruction is announced instead of the cursor passing through. Multi-tile moves always stop at walls regardless.


Reporting bugs

Open an issue at https://github.com/Berenion/wasteland2-accessibility-mod/issues . Attach the log file, <game>\MelonLoader\Latest.log, and say which screen reader you use and what you were doing. That is usually enough to track it down.


Controls

Contexts are detected automatically: open the world map and the world-map keys apply; open inventory and the inventory keys take over. You never switch contexts by hand.

Notation: Ctrl+X and Shift+X mean hold that modifier. For value changes, the top-row = / - keys and the numpad + / - keys are interchangeable. Backslash repeats the last announcement in most contexts.

Baseline keys are the same in nearly every list and menu. The sections below list only what differs from or adds to these:
- Up / Down: previous / next item.
- Enter: activate or confirm.
- Escape: close, cancel, or go back.
- Home / End: first / last item.
- Backslash: repeat the last announcement.
- Shift+/ (the ? key): read back the controls for whatever screen or cursor you are in. The numpad slash works too (Shift+numpad-/), for layouts where slash is only on the numpad.
- Shift+S: open the accessibility settings menu (a navigable on/off list of every mod setting). Works anywhere; Shift+S or Escape closes it.
- F1 to F7: select party member 1 to 7.
The grid and combat cursors reassign Home, End, Tab, and Backslash; this is noted where it applies.

Always on, in exploration and map contexts:
- F1 to F7: select party member 1 to 7 (exploration and the world map also allow Shift or Ctrl for multi-select).
- F10: toggle camera rotation lock. On by default, so up stays north.
- Space: toggle tactical pause. Auto-pauses for inventory, loot, and vendor screens.

Main menu:
- Up / Down move between entries, Enter activates.

Modal dialogs (confirmations, tutorials, world-map encounters):
- Yes/No and OK/Cancel: Left or Up is previous, Right or Down is next, Enter activates.
- Difficulty selection: Left / Right change difficulty (Rookie, Seasoned, Ranger, Legend); Up / Down switch Play and Back.
- Quantity prompt (split stack, partial buy): Left / Right change by one; PgDn / PgUp by ten; Home sets the minimum; End sets the maximum; Up / Down switch OK and Cancel.
- POI panel: Left / Right / Up / Down cycle the buttons, Enter activates.
- Tutorial popups: Enter continues; the full text reads on appearance.

Keypad (safes, doors, terminals):
- 0 to 9 (top row or numpad): type a digit, up to eight.
- Backspace: delete the last digit. C: clear. Enter: submit. Escape: cancel.

Generic menus (pause, options, save and load):
- Baseline, plus Left / Right to move within the focused control, PgUp / PgDn to switch Options tabs, Delete to delete the selected save.
- Save-name field: Enter saves, Escape cancels and restores the prior name; typing and Backspace work, and the last character typed is spoken.

Exploration runs two cursors at once: the scanner cycles nearby interactables, and the grid cursor moves tile by tile.

Exploration, scanner:
- PgUp / PgDn: previous / next interactable in the current category.
- Ctrl+PgUp / Ctrl+PgDn: previous / next category (containers, NPCs, doors, and so on).
- =: toggle direction format (compass or clock).
- K: toggle announcement order (coordinate first or object name first).
- ' (apostrophe): announce party scrap.
- Enter: interact with the item the scanner is on. If the object needs an item your party carries (a shovel for a dirt pile, say), it is used automatically.
- Backspace: stop party movement.
- R: answer the radio. I: open character/inventory. G: toggle group mode.
- Shift or Ctrl plus F1 to F7: add that ranger to the selection, to build a multi-ranger group.
- F1 to F7 on the current leader: re-press to centre the camera on them.
- Escape: pause menu.

Exploration, grid cursor:
- Arrows: move one step. Shift+Left / Shift+Right: decrease / increase step size, one to thirty tiles. By default a single step can move onto wall/terrain tiles to inspect them; turn on Cursor stops at walls (settings) to confine the cursor to walkable ground.
- Ctrl+Arrow: move that direction until blocked by terrain.
- Tab: open the actions menu (skills and usable items) at the cursor.
- Enter: open the tile's context menu. Or fire/use when a free-aim or item mode is active, or open a single ranger's info if one ranger is on the tile.
- Backslash: detailed scan of the current tile. X: examine the first examinable object on the tile.
- L: list every scanner-visible item (interactables, characters, party, loot) within the scan radius of the cursor, nearest first, each with its direction from the cursor. Comma / period: decrease / increase that radius, two to forty tiles (default ten).
- ]: order the selected ranger to walk to the cursor.
- Home: jump the cursor to the selected interactable (the one from the scanner). End: distance and direction to it.
- Shift+Home: jump the cursor to the party leader. Shift+End: distance and direction to the leader.
- F: toggle camera-follows-cursor. K: toggle announcement order. H: toggle elevation announcements. Y: toggle line-of-sight announcements.
- Escape: cancel an active free-aim or item mode; with none active, open the pause menu.
Context menus and selection lists use the baseline keys.

World map:
- Arrows: move the review cursor. Shift+Left / Shift+Right: step size, one to a hundred units.
- PgUp / PgDn: cycle visible POIs. Ctrl+PgUp / Ctrl+PgDn: cycle POI category.
- Home: jump the cursor to the selected POI. End: distance and direction to it.
- Shift+Home: jump the cursor to the party. Shift+End: distance to the party.
- ]: order the party to the cursor (announces water cost and any radiation crossings). Backspace: stop the party.
- Enter: interact with the POI at or near the cursor; the party walks there first if needed.
- Space: cursor summary (step size, distance to party, radiation, nearest POI).
- W: water supply. Shift+W: estimate water cost from the party to the cursor.
- F: toggle camera-follows-cursor. R: answer the radio. I: character/inventory. Escape: pause menu.
- Shift or Ctrl plus F1 to F7: add that ranger to the selection.

Combat is turn-based. The cursor jumps to the active actor each turn and announces whose turn it is. Space stays the game's End Turn.

Combat, cursor:
- Arrows: move one step. Shift+Left / Shift+Right: step size. Ctrl+Arrow: move until blocked.
- ]: move the current actor to the cursor (costs AP).
- Backslash: detailed tile announcement. K: toggle announcement order. Y: toggle line-of-sight (clear line of fire).
- F: toggle camera-follows-cursor. This takes over the game's precision-shot key; precision shots are in the target menu.
- Shift+Home: jump to the current actor. Shift+End: distance to the current actor.
- Home: jump to the selected combatant (from PgUp/PgDn). End: distance to it.

Combat, menus and review:
- Tab: open/close the actions menu (move, attack, skills, items).
- T: open/close the initiative tracker (turn order with HP, AP, status).
- Enter on a hostile: target actions menu. Enter on an ally: that ranger's info.
- L: open the combat log; L or Escape closes it.
- PgUp / PgDn: cycle combatants. Ctrl+PgUp / Ctrl+PgDn: cycle combatant category.
- In the actions and target menus, baseline keys cycle entries and Enter executes. In the target menu, Left / Right switch the Actions and Info tabs.

Combat, targeting and free aim:
- After choosing a targeted item or skill, move the cursor to the target and press Enter; Escape cancels.
- Free aim (Tab, then Free Aim): move to the target tile and press Enter to fire; Escape cancels.

Combat, other:
- I: inventory (your turn only). Escape: pause menu when no menu is open.

Conversations:
- Baseline keys move through options. While the NPC is still speaking, Enter skips the voiceover instead of selecting.
- Skill-check options announce the required level and whether you pass. Goodbye options announce as "ends conversation."

Inventory (character info screen):
- Up / Down move within a zone; Left / Right switch Equipment and Backpack.
- Enter: item context menu (Equip, Drop, Use, Split, and so on). E: quick equip/unequip.
- Tab: full item info, spoken once. I: item info browser (Up/Down lines, Home/End jump, Escape or I closes).
- R: flavor description. F: cycle the filter. C: context summary (ranger, weight, filter).
- PgUp / PgDn: switch character-info tabs.

Loot containers:
- Up / Down move items; Left / Right switch container; F1 to F7 choose the destination ranger.
- Enter: transfer to the selected ranger. T: take all. G: distribute across the party.
- Tab: item info. I: info browser. R: description. F: filter. C: context summary.

Vendor and shop:
- Left / Right cycle four zones: Player Inventory, Escrow, Vendor Inventory, Filters. Up / Down move within a zone.
- Enter: buy, sell, or move, depending on the zone. I: item info browser. R: description.
- S: scrap balance. J: sell all junk. F: filter.
- Quantity dialogs use the modal quantity-prompt keys above; the total price is announced as you adjust.

Character creation has eight panels: Use Default Party, Party, Add Character, Attributes, Skills, Traits, Dossier, Flavor.

Character creation, all panels:
- Tab: panel name and position. D: on Attributes/Skills, open the Derived Stats browser; on other panels, announce the character summary.
- N: Next/Done. Enter on the Previous/Next buttons calls Back/Done. Escape: game-native Back.

Character creation, party panels (Use Default Party, Party, Add Character):
- Baseline keys; Enter selects a slot, accepts a premade character, or starts the game.
- Party panel: I re-announces the character; Delete removes the focused slot (with confirmation); S starts playing once enough rangers are present.
- Add Character panel: R reads the biography.

Character creation, Attributes:
- Up / Down move attributes. + / - adjust (also = / -, numpad + / -, or Enter then Left/Right).
- Enter: edit mode (Left/Right adjust, Enter or Escape exits). I: describe. P: points remaining.
- F: switch to Skills without leaving the panel; F again switches back.

Character creation, Skills (or Attributes then F):
- Up / Down move skills. Left / Right switch category (Combat, Knowledge, General).
- + / - adjust (or Enter for edit mode). I: describe. P: points remaining. F (from Attributes): switch back.

Character creation, Traits:
- Up / Down move traits. Enter or Space toggles. I: browsable perk description (Up/Down lines, Escape closes).

Character creation, Dossier and Flavor:
- Up / Down move fields. Left / Right cycle a dropdown value or toggle gender.
- Enter on a text field starts editing: type, Enter confirms, Escape cancels.

Character creation, browsers:
- Derived Stats (Attributes/Skills, then D): baseline keys cycle stats, Home/End jump, I describes.
- Trait/perk description (I on a trait): baseline keys move through the lines, Home/End jump.

Character info in-game has tabs: Attributes, Skills, Traits, Dossier, Logbook, Inventory (inventory is covered above).

Character info, all tabs:
- Tab: panel and position. D: full character summary. PgUp / PgDn: switch tab.
- E: current XP and XP to the next level. S: unified stats browser (Header, Combat, Derived).

Character info, Attributes and Skills (level-up):
- As in character creation, plus: on Skills, F cycles Learned and the three unlearned categories, and Left / Right switch the unlearned category once past Learned.
- + / - or Enter raise with available points. P: points remaining. I: describe.

Character info, Traits:
- Up / Down move traits. Enter or Space toggles, if perk points are available. I: perk description. P: perk points remaining.

Character info, Dossier:
- Up / Down move fields. I on the Quirk line opens the quirk description; on any other line it reads the biography.

Character info, Logbook:
- Up / Down move entries. Enter or Right opens an entry's details. Left: previous sort category. F: next sort category.
- X: toggle flagged. I: announce the entry's location or source.
- In a detail view, baseline keys move through the lines (Home/End jump), Tab announces position, Escape or Left returns to the list.


Original game keys in combat

In combat the mod claims only the keys it needs; every other vanilla binding still reaches the game. The most useful:
- Space: end turn. X: swap weapons. V: ambush (overwatch). R: reload. G: show the movement grid.
- ' (apostrophe): stand. ; (semicolon): crouch. B: change fire mode. N: toggle show attack range. Z: highlight enemies.
- 1 to 9: the on-screen item hotkey slots.

In exploration the map cursor owns the keyboard and suppresses the game's own input, so vanilla hotkeys generally do not fire there. Use the mod's equivalents instead: I for character/inventory, and the cursor for navigation and interaction.

Camera keys: W A S D pan the camera and Q / E rotate it, but rotation only takes effect when the rotation lock is off (F10; on by default, so up stays north).


Key conflicts

Some mod keys overlap with vanilla bindings. In combat the mod takes only the keys it uses; in cursor and menu contexts, including exploration, it suppresses vanilla input more broadly. Where it matters:
- T (game: centre on character): the combat initiative tracker.
- Tab (game: select next mob): the actions menu in combat and at the map cursor.
- F (game: precision shot in combat): camera-follow toggle in every cursor context; precision shots are in the target menu.
- Space (game: end turn in combat, toggle group mode in exploration): tactical pause in exploration; left alone for End Turn in combat.
- ] (game: unbound): move to cursor in exploration, combat, and the world map.


Building from source

The repo includes the reference DLLs in libs/, so no setup is needed. Build with:

dotnet build -c Release

The DLL lands in bin\Release\net35\ and auto-copies to ..\Mods\ if that folder exists next to the repo. To make the release archive, run:

powershell -ExecutionPolicy Bypass -File package.ps1

That builds Release and produces dist\Wasteland2AccessibilityMod-v<version>.zip with the mod DLL, Tolk.dll, nvdaControllerClient64.dll, and this readme.


Credits and license

- Tolk, by Davy Kager (BSD 3-clause). Bridges the mod to NVDA, JAWS, and SAPI.
- NVDA Controller Client, by NV Access (LGPL 2.1). Lets Tolk speak through NVDA; redistributed unmodified next to WL2.exe.
- MelonLoader, by LavaGang. Loads the mod into the game.
- Harmony, by Andreas Pardeike. Runtime method patching.
- The Wasteland 2 community, for testing and feedback.

Community accessibility mod. Modify and share freely.
