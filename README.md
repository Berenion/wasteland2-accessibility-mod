# Wasteland 2 Accessibility Mod

A screen-reader and keyboard accessibility mod for **Wasteland 2 Director's Cut**. Built primarily for blind and low-vision players, but useful for anyone who prefers keyboard-only play.

**Status:** Public beta. Expect rough edges; please report what you find.

## Table of contents

- [What it does](#what-it-does)
- [Requirements](#requirements)
- [Installation](#installation)
  - [Verifying the mod loaded](#verifying-the-mod-loaded)
- [First-launch quickstart](#first-launch-quickstart)
- [Configuration](#configuration)
- [Reporting bugs](#reporting-bugs)
- [Troubleshooting](#troubleshooting)
- [Building from source](#building-from-source)
- [Controls and hotkeys](#controls-and-hotkeys)
  - [Notation](#notation)
  - [Universal navigation](#universal-navigation)
  - [Always-on (most exploration / map contexts)](#always-on-most-exploration--map-contexts)
  - [Main menu](#main-menu)
  - [Modal dialogs](#modal-dialogs-confirmations-tutorials-world-map-encounters)
  - [Keypad popup](#keypad-popup-safes-doors-terminals)
  - [Generic menus](#generic-menus-pause-options-save--load-etc)
  - [Exploration](#exploration-world--dungeon)
    - [List cursor (interactables)](#list-cursor-interactables)
    - [Grid cursor (map cursor)](#grid-cursor-map-cursor)
  - [World map](#world-map)
  - [Combat](#combat)
    - [Combat cursor movement](#combat-cursor-movement)
    - [Combat action menus](#combat-action-menus)
    - [Combatant cycling](#combatant-cycling)
    - [Item / skill targeting](#item--skill-targeting)
    - [Free aim](#free-aim-tab--free-aim)
    - [Other combat keys](#other-combat-keys)
  - [Conversations](#conversations)
  - [Inventory (character info screen)](#inventory-character-info-screen)
  - [Loot containers](#loot-containers-popupinventorymenu)
  - [Vendor / shop](#vendor--shop)
  - [Character creation](#character-creation)
  - [Character info (in-game)](#character-info-in-game)
- [Original game hotkeys in combat](#original-game-hotkeys-in-combat)
- [Notes on key conflicts](#notes-on-key-conflicts)
- [Credits](#credits)
- [License](#license)

## What it does

- Reads game UI, dialogue, combat events, item info, and world objects through NVDA, JAWS, or Windows SAPI (via the Tolk library).
- Replaces mouse-driven interaction with full keyboard navigation in every menu: main menu, character creation, inventory, loot, shops, conversations, the world map, combat, and modal dialogs.
- Adds a **virtual grid cursor** in exploration and combat, so you can move tile-by-tile, hear what's on each tile, and order interactions without seeing the screen.
- Adds a **world-map review cursor** with proximity alerts for POIs and radiation, water-cost path estimation, and POI cycling.
- Adds **tactical pause** (Space) that freezes time outside combat. Auto-pauses while inventory / loot / vendor screens are open.
- Locks camera rotation so "up" stays north (toggleable with F10), removing rotational disorientation.

## Requirements

| Component | Version / source |
|---|---|
| Wasteland 2 Director's Cut | Steam or GOG |
| MelonLoader | **0.5.7 Open-Beta** — <https://github.com/LavaGang/MelonLoader/releases/tag/v0.5.7>. **Do not use 0.6.x or newer**; they crash with Wasteland 2 Director's Cut. |
| Tolk runtime DLL | `Tolk.dll` is **bundled in the release archive** — just copy it next to `WL2.exe`. |
| `nvdaControllerClient64.dll` | **NVDA users only**, and **not** bundled — supply it yourself (from a Tolk release) next to `WL2.exe`. Without it, NVDA falls back to SAPI speech. |
| Screen reader (optional) | NVDA, JAWS, or any SAPI voice. If none is running, Tolk falls back to SAPI. |

The mod targets .NET Framework 3.5 (Unity 4.x). MelonLoader installs that runtime automatically.

## Installation

1. **Install MelonLoader 0.5.7** (specifically — newer versions crash with this game)
   - Download from <https://github.com/LavaGang/MelonLoader/releases/tag/v0.5.7>. Use `MelonLoader.Installer.exe` from that release, **not** the latest installer (the latest installer will pull a newer MelonLoader that crashes).
   - Run it, point it at your Wasteland 2 install folder (the folder that contains `WL2.exe`), and click **Install**.
   - On a normal Steam install, that folder is `...\steamapps\common\Wasteland 2 Director's Cut\Build\`.
   - If you already have a newer MelonLoader installed and the game crashes on launch, uninstall it (delete `version.dll`, `dobby.dll`, and the `MelonLoader` folder from the game directory) before installing 0.5.7.
2. **Install the mod DLL.**
   - Copy `Wasteland2AccessibilityMod.dll` from the release archive's `Mods\` folder into `<game folder>\Mods\`. (MelonLoader creates the `Mods` folder on first launch.)
3. **Install the bundled `Tolk.dll`.**
   - Copy **`Tolk.dll`** from the release archive into the **same folder as `WL2.exe`** (not into `Mods`). This is the screen-reader bridge and ships with the mod.
   - **NVDA users:** also place **`nvdaControllerClient64.dll`** next to `WL2.exe`. It is *not* bundled — grab it from a [Tolk release](https://github.com/dkager/tolk/releases). Without it, NVDA falls back to SAPI speech.
4. **Start your screen reader** before launching the game (recommended — Tolk can also attach to one started later, but starting first is the reliable path).
5. **Launch the game.**

> The release archive mirrors the game's `Build` folder layout, so you can also just extract it straight into your Wasteland 2 `Build` directory — `Mods\` and `Tolk.dll` land in the right places.

### Verifying the mod loaded

Open `<game folder>\MelonLoader\Latest.log` after the game has started. You should see:

```
MelonLoader v0.5.7 Open-Beta
...
Melon Assembly loaded: '.\Mods\Wasteland2AccessibilityMod.dll'
...
Wasteland 2 Accessibility Mod v0.7.0 (beta)
Screen reader detected: NVDA   (or JAWS, or "No screen reader detected (Tolk loaded, will use SAPI if available)")
[Core] Input router initialized with all states (including MainMenu)
```

If you don't see those lines, see [Troubleshooting](#troubleshooting) below.

## First-launch quickstart

The main menu announces itself. From there:

- **Up / Down** to move through menu options, **Enter** to select.
- Pick **New Game** or **Continue** to get in-world.

Once you're in the game world:

- **Arrow keys** move the grid cursor around the current tile. The cursor tells you what's on each tile as you move.
- **Enter** interacts with whatever the cursor is on — talk to an NPC, open a container, attack a frozen enemy, examine an object.
- **Space** toggles tactical pause. Use it freely; the game also auto-pauses for inventory/loot/vendor screens.
- **]** (right bracket) tells the selected party member to walk to where the cursor is.
- **I** opens the character / inventory screen. **Escape** opens the pause menu.
- **F1–F7** select party member 1 through 7.

The full hotkey reference is in the [Controls and hotkeys](#controls-and-hotkeys) section below.

## Configuration

The mod creates `<game folder>\UserData\Wasteland2Accessibility.cfg` on first run. These settings live there:

| Setting | Default | What it does |
|---|---|---|
| `UseClockPositions` | `false` | When `true`, directions are spoken as clock positions ("3 o'clock") instead of compass names ("east"). Toggle in-game with `=`. |
| `ObjectNamesFirst` | `false` | When `true`, tile announcements lead with the object name; when `false`, they lead with the tile coordinate. Toggle in-game with `K`. |
| `UseTileDistances` | `true` | When `true` and a combat grid is available, distances are reported in tiles (1 tile ≈ 1.6 m). When `false`, always in meters. |
| `ConveyElevation` | `true` | When `true`, the exploration cursor announces terrain height changes and height relative to the party (for finding ramps and edges). Toggle in-game with `H`. |
| `AnnounceLineOfSight` | `false` | When `true`, the tile cursor also says whether the tile is within line of sight of the selected/active character. In exploration it reflects perception range plus occlusion; in combat it's a clear line of fire. Toggle in-game with `Y`. |

You can edit the file directly or use the in-game toggles. Changes save automatically.

## Reporting bugs

Useful information when filing an issue:

1. **What you were doing**, step by step from the last menu / screen change. Include the screen / context the bug occurred in (exploration, combat, inventory, vendor, etc.).
2. **What you expected to be announced** vs **what was actually announced** (or silence, if nothing was).
3. The relevant section of `<game folder>\MelonLoader\Latest.log`. Lines tagged `[Wasteland 2 Accessibility Mod]`, `[CombatState]`, `[MapCursorState]`, etc. are the mod's own logs and usually pinpoint the issue.
4. Your screen reader (NVDA / JAWS / SAPI) and its version.
5. Whether the issue reproduces consistently or only sometimes.

A short screen-reader audio capture (Windows + Alt + R, or any tool that records system audio) is the gold standard for "speech sounds wrong" bugs.

## Troubleshooting

**Game starts but no MelonLoader console appears.**
MelonLoader installs in console-less mode by default in recent versions. Look at `<game folder>\MelonLoader\Latest.log` instead — it has the same content.

**Game starts, console shows MelonLoader, but no mod messages.**
The DLL isn't in the right folder. Confirm `Wasteland2AccessibilityMod.dll` is in `<game folder>\Mods\` (not `Plugins`, not `UserLibs`).

**Mod loads but says `Failed to initialize Tolk` in the log.**
`Tolk.dll` or `nvdaControllerClient64.dll` is missing from the folder that contains `WL2.exe`. They must sit next to the executable, not inside `Mods`.

**Mod loads, Tolk initializes, but nothing speaks.**
Confirm a screen reader is running. If you're using SAPI fallback, the log will say `No screen reader detected (Tolk loaded, will use SAPI if available)` — make sure Windows has at least one SAPI voice installed.

**Game says "Still loading, try again" when you press Space.**
The game is in a paused state it manages itself (scene load, cutscene start, etc.). Wait a moment and try again.

**A specific UI element isn't announced.**
That's likely a missing patch — file a bug with the exact screen and element. The mod covers a lot of the game, but Wasteland 2 has many one-off UI screens and there are gaps.

**Camera keeps rotating away from north.**
Press **F10** to re-lock it. The lock state is per-session.

## Building from source

The repo includes the required reference DLLs in `libs/`, so no first-time setup is needed:

```
dotnet build -c Release
```

Output lands in `bin\Release\net35\Wasteland2AccessibilityMod.dll`. An MSBuild target auto-copies it to `..\Mods\` if that folder exists next to the repo (useful when developing against a local game install).

To assemble the distributable release archive, run:

```
powershell -ExecutionPolicy Bypass -File package.ps1
```

This builds Release and produces `dist\Wasteland2AccessibilityMod-v<version>.zip`, bundling the mod DLL, the Tolk bridge (`redist\Tolk.dll`), and this README in the game's `Build` folder layout.

## Controls and hotkeys

Every keybinding the mod adds, organized by context. Most contexts are auto-detected: when you're on the world map, the world-map controls apply; when you open inventory, inventory controls take over. You don't switch contexts manually.

### Notation

- Keys are written with their printed names: `Up`, `Down`, `PgUp`, `PgDn`, `Backspace`, `Home`, `End`, `\` (backslash), `]` (right bracket), `=` (equals), `'` (apostrophe).
- `Ctrl+X` means hold Ctrl while pressing X. `Shift+X` same idea.
- `Numpad +/-` and the top-row `=` / `-` keys are interchangeable wherever value adjustment is mentioned.

### Universal navigation

These keys behave the same in nearly every menu, list, and cursor context. The sections below list only what's **different from or in addition to** this baseline — if a context isn't mentioned for one of these keys, it follows the baseline.

| Key | Action |
|---|---|
| `Up` / `Down` | Previous / next item in the current list. |
| `Enter` | Activate / select / confirm the focused item. |
| `Escape` | Close, cancel, or go back. |
| `Home` / `End` | Jump to first / last item (in lists that support it). |
| `\` | Repeat the last announcement. |
| `F1`–`F7` | Switch to party member 1 through 7. |

A few contexts override these — combat leaves `Space` to the game for End Turn, and the grid/combat cursors give `Home`/`End`, `Tab`, and `\` cursor-specific meanings (`\` becomes a detailed scan of the current tile rather than a repeat). Those overrides are called out where they apply.

### Always-on (most exploration / map contexts)

| Key | Action |
|---|---|
| `F1`–`F7` | Select party member 1 through 7. (Exploration and world map additionally support Shift/Ctrl multi-select — see those sections.) |
| `F10` | Toggle camera rotation lock. Locked = "up" is always north. Default: locked. |
| `Space` | Toggle tactical pause. Auto-pauses while inventory / loot / vendor screens are open. |

### Main menu

Baseline navigation. `Up` / `Down` move between Continue / Load / New Game / Options / Credits / Exit; `Enter` activates.

### Modal dialogs (confirmations, tutorials, world-map encounters)

Generic Yes/No and OK/Cancel modals — `Left` / `Up` is the previous button, `Right` / `Down` the next, `Enter` activates.

Difficulty selection (when starting a new game):

| Key | Action |
|---|---|
| `Left` / `Right` | Change difficulty (Rookie / Seasoned / Ranger / Legend). |
| `Up` / `Down` | Switch between Play and Back. |

Quantity prompt (split-stack, partial-buy):

| Key | Action |
|---|---|
| `Left` / `Right` | Adjust quantity by 1. |
| `PgDn` / `PgUp` | Adjust quantity by 10. |
| `Home` | Set to minimum. |
| `End` | Set to maximum. |
| `Up` / `Down` | Switch between OK and Cancel. |

POI panel (world-map encounters, locations with multiple entries): `Left` / `Right` / `Up` / `Down` cycle the buttons (Attack / Run, Confirm / Cancel, or per-entry buttons), `Enter` activates.

Tutorial popups: `Enter` continues. The full tutorial text reads on appearance.

### Keypad popup (safes, doors, terminals)

| Key | Action |
|---|---|
| `0`–`9` (top row or numpad) | Type that digit (up to 8 digits, the game's own limit). |
| `Backspace` | Delete the last digit. |
| `C` | Clear the field. |
| `Enter` | Submit. |
| `Escape` | Cancel. |

### Generic menus (pause, options, save / load, etc.)

Baseline navigation, plus:

| Key | Action |
|---|---|
| `Left` / `Right` | Navigate the focused control list (alongside `Up` / `Down`). |
| `Tab` | Move to the next element. |
| `PgUp` / `PgDn` | Switch tabs in the Options menu. |
| `Delete` | Delete the selected save (Save / Load screen only). |

Save-name text field: `Enter` confirms and saves, `Escape` cancels and restores the prior name. Typed characters and Backspace work as expected; the last typed character is spoken for feedback.

### Exploration (world / dungeon)

Two cursor systems run in parallel during exploration: a **list cursor** that cycles through nearby interactables, and a **grid cursor** (map cursor) for tile-by-tile navigation.

#### List cursor (interactables)

| Key | Action |
|---|---|
| `PgUp` / `PgDn` | Previous / next interactable in the current category. |
| `Ctrl+PgUp` / `Ctrl+PgDn` | Previous / next category (containers → NPCs → doors → ...). |
| `=` | Toggle direction format (cardinal vs clock positions). |
| `K` | Toggle tile announcement order (coordinates first vs object names first). |
| `'` | Announce party scrap (currency). |
| `Enter` | Interact with the selected list item. If the object accepts an item your party carries (shovel for a dirt pile, etc.), it uses it automatically. |
| `Backspace` | Stop party movement. |
| `R` | Answer the radio. |
| `I` | Open the character / inventory screen. |
| `G` | Toggle party group mode (grouped / ungrouped). |
| `Shift+F1`–`F7` or `Ctrl+F1`–`F7` | Add that ranger to the current selection (instead of replacing it). Use to build a multi-ranger group. |
| `F1`–`F7` (already-selected leader) | Re-pressing the key for the current leader centers the camera on them. |
| `Escape` | Open the pause menu. |

#### Grid cursor (map cursor)

| Key | Action |
|---|---|
| `Up` / `Down` / `Left` / `Right` | Move the cursor one step in that cardinal direction. |
| `Shift+Left` / `Shift+Right` | Decrease / increase the step size (1–30 tiles per key press). |
| `Ctrl+Arrow` | Move in that direction until blocked by terrain. |
| `Tab` | Open the actions menu (skills + usable items) at the cursor. |
| `Enter` | Open the context menu on the cursor's tile (or attack / use item if a free-aim or item-use mode is active, or open party-member info if a single ranger is on the tile). |
| `\` | Detailed scan of the current tile (everything on it). |
| `X` | Examine the first examinable object on the tile. |
| `]` | Order the selected ranger to walk to the cursor. |
| `Shift+Home` | Jump cursor to the party leader. |
| `Shift+End` | Announce distance and direction from cursor to party leader. |
| `Home` | Jump cursor to the currently selected interactable (the one from PgUp/PgDn cycling). |
| `End` | Distance and direction to the selected interactable. |
| `F` | Toggle camera-follows-cursor. |
| `K` | Toggle tile announcement order. |
| `H` | Toggle elevation announcements (height changes and height relative to the party). |
| `Y` | Toggle line-of-sight announcements (whether the tile is within sight of the selected ranger). |
| `Escape` | Cancel an active free-aim / item-use mode. (With no active mode, opens the pause menu via exploration.) |

Context menus and selection lists (PC selection, target selection) use baseline navigation.

### World map

| Key | Action |
|---|---|
| `Up` / `Down` / `Left` / `Right` | Move the review cursor in that direction. |
| `Shift+Left` / `Shift+Right` | Decrease / increase step size (1–100 world units). |
| `PgUp` / `PgDn` | Cycle through visible POIs (locations, oases, encounters). |
| `Ctrl+PgUp` / `Ctrl+PgDn` | Cycle POI category. |
| `Home` | Jump cursor to the selected POI. |
| `End` | Announce distance and direction from cursor to selected POI. |
| `Shift+Home` | Jump cursor to the party. |
| `Shift+End` | Announce distance from cursor to the party. |
| `]` | Order the party to walk to the cursor (announces water cost and any radiation crossings). |
| `Backspace` | Stop the party. |
| `Enter` | Interact with the POI at or near the cursor (walks the party there first if needed). |
| `Space` | Announce a cursor summary (step size, distance to party, radiation, nearest POI). |
| `W` | Announce water supply (current / max). |
| `Shift+W` | Estimate water cost from the party to the cursor. |
| `F` | Toggle camera-follows-cursor. |
| `R` | Answer the radio. |
| `I` | Open the character / inventory screen. |
| `Escape` | Pause menu. |
| `Shift+F1`–`F7` or `Ctrl+F1`–`F7` | Add that ranger to the current selection (instead of replacing it). |

### Combat

Combat is turn-based. The cursor auto-jumps to the active actor when a new turn starts and announces whose turn it is.

#### Combat cursor movement

| Key | Action |
|---|---|
| `Up` / `Down` / `Left` / `Right` | Move the cursor one step. |
| `Shift+Left` / `Shift+Right` | Decrease / increase step size. |
| `Ctrl+Arrow` | Move until blocked. |
| `]` | Move the current actor to the cursor (costs AP). |
| `\` | Detailed tile announcement. |
| `K` | Toggle tile announcement order. |
| `Y` | Toggle line-of-sight announcements (whether the active character has a clear line of fire to the tile). |
| `F` | Toggle camera-follows-cursor. (Suppresses the game's "headshot/precision shot" binding — precision shots are available through the Tab target-actions menu instead.) |
| `Shift+Home` | Jump cursor to the current actor. |
| `Shift+End` | Distance from cursor to the current actor. |
| `Home` | Jump cursor to the selected combatant (from PgUp/PgDn cycling). |
| `End` | Distance from cursor to the selected combatant. |

> `Space` is the game's "end turn" key in combat — the mod leaves it alone.

#### Combat action menus

| Key | Action |
|---|---|
| `Tab` | Open / close the combat actions menu (movement, attacks, skills, items). |
| `T` | Open / close the initiative tracker (full turn order with HP, AP, status). |
| `Enter` on cursor over a hostile | Open the target actions menu for that enemy. |
| `Enter` on cursor over an ally | Open that ranger's info screen. |
| `L` | Open the combat log. `L` or `Escape` to close. |

Inside the actions menu and target actions menu, baseline navigation cycles entries (`Left` / `Right` also work, except in the target actions menu where they switch between the Actions and Info tabs). `Enter` executes the focused action.

#### Combatant cycling

| Key | Action |
|---|---|
| `PgUp` / `PgDn` | Previous / next combatant in the current category. |
| `Ctrl+PgUp` / `Ctrl+PgDn` | Previous / next combatant category (allies / enemies / etc.). |

#### Item / skill targeting

After choosing an item or skill from the Tab actions menu that needs a target, the cursor enters a targeting mode:

- Move the cursor onto the desired character / tile and press `Enter`.
- `Escape` cancels.

#### Free aim (Tab → Free Aim)

- Move cursor to target tile and press `Enter` to fire.
- `Escape` cancels.

#### Other combat keys

| Key | Action |
|---|---|
| `I` | Open inventory (player's turn only). |
| `Escape` | Open the pause menu (when no browse mode is active). |

### Conversations

Baseline navigation moves through dialogue options. The one override: while the NPC is still speaking, `Enter` skips the current voiceover instead of selecting an option.

Skill-check options announce the required skill level and whether you can pass. Goodbye options announce as "ends conversation."

### Inventory (character info screen)

`Up` / `Down` navigate within the current zone (Equipment or Backpack); `Left` / `Right` switch between the two zones.

| Key | Action |
|---|---|
| `Enter` | Open the context menu on the current item (Equip, Drop, Use, Split, etc.). |
| `E` | Quick equip / unequip the current item. |
| `Tab` | Detailed item info (one-shot, spoken in full). |
| `I` | Open the **item info browser** — full item details split into browsable lines (`Up`/`Down` to navigate, `Home`/`End` to jump, `Escape` or `I` to close). |
| `R` | Read the item's flavor description. |
| `F` | Cycle the inventory filter (All / Weapons / Armor / ...). |
| `C` | Read the inventory context summary (selected ranger, weight, filter, etc.). |
| `PgUp` / `PgDn` | Switch character-info tabs (Attributes / Skills / Inventory / ...). |

### Loot containers (PopupInventoryMenu)

`Up` / `Down` navigate items; `Left` / `Right` switch container (if multiple are open). `F1`–`F7` choose the destination party member.

| Key | Action |
|---|---|
| `Enter` | Transfer the item to the currently selected ranger. |
| `T` | Take all. |
| `G` | Distribute all (split intelligently across the party). |
| `Tab` | Detailed item info. |
| `I` | Item info browser. |
| `R` | Read item description. |
| `F` | Cycle filter. |
| `C` | Loot context summary. |

### Vendor / shop

Four zones cycle with `Left` / `Right`: **Player Inventory → Escrow → Vendor Inventory → Filters**. `Up` / `Down` navigate within the current zone.

| Key | Action |
|---|---|
| `Enter` | Buy / sell / move item depending on the zone you're in. |
| `I` | Open the item info browser for the current item. |
| `R` | Read the item description. |
| `S` | Announce your party's scrap balance. |
| `J` | Sell all junk. |
| `F` | Cycle filter. |

Quantity-selection dialogs that pop up during buy/sell use the **modal quantity prompt** controls listed above. Total price is announced as you adjust.

### Character creation

Eight panels: Use Default Party / Party / Add Character / Attributes / Skills / Traits / Dossier / Flavor. Some keys are common across all panels.

#### Character creation — common across all panels

| Key | Action |
|---|---|
| `Tab` | Announce panel name + current position. |
| `D` | On Attributes / Skills: open the Derived Stats browser. On other panels: announce the character summary. |
| `N` | Next / Done (same as the Done button). |
| `Enter` on Previous / Next nav buttons | Calls Back / Done respectively. |
| `Escape` | Game-native Back — the mod lets it pass through. |

#### Use Default Party / Party / Add Character panels

Baseline navigation: `Up` / `Down` move through the list, `Enter` activates (select a slot, accept a premade character, start the game).

**Party panel extras**

| Key | Action |
|---|---|
| `I` | Re-announce the current character details. |
| `Delete` | Remove the character in the focused slot (with confirmation). |
| `S` | Start playing (shortcut — only when enough rangers are present). |

**Add Character panel extras**

| Key | Action |
|---|---|
| `R` | Read the biography. |

#### Attributes panel

`Up` / `Down` navigate attributes.

| Key | Action |
|---|---|
| `+` / `-` (or `=` / `-`, numpad `+/-`, or `Left` / `Right` after pressing `Enter`) | Adjust the focused attribute. |
| `Enter` | Enter edit mode — `Left`/`Right` adjust, `Enter` or `Escape` exits. |
| `I` | Describe the focused attribute. |
| `P` | Announce attribute points remaining. |
| `F` | Switch sub-area to Skills (you can adjust skills without leaving this panel). `F` again to switch back. |

#### Skills panel (or Attributes → F)

`Up` / `Down` navigate skills.

| Key | Action |
|---|---|
| `Left` / `Right` | Switch skill category (Combat / Knowledge / General). |
| `+` / `-` (or `Enter` for edit mode) | Adjust the focused skill. |
| `I` | Describe the focused skill. |
| `P` | Announce skill points remaining. |
| `F` (only from Attributes panel) | Switch back to attributes. |

#### Traits panel

`Up` / `Down` navigate traits.

| Key | Action |
|---|---|
| `Enter` / `Space` | Toggle the focused trait. |
| `I` | Open the browsable perk description (`Up`/`Down` lines, `Escape` to close). |

#### Dossier / Flavor panels

`Up` / `Down` navigate fields.

| Key | Action |
|---|---|
| `Left` / `Right` | Cycle a dropdown value or toggle a gender button. |
| `Enter` on a text input | Enter text-editing mode. Type normally, `Enter` to confirm, `Escape` to cancel. |

#### Derived Stats browser (Attributes/Skills → `D`)

Baseline navigation cycles derived stats (Hit Points, Action Points, Combat Speed, etc.); `Home` / `End` jump to first / last. `I` describes the focused stat.

#### Trait / perk description browser (`I` on a trait)

Baseline navigation moves through the description lines (`Home` / `End` for first / last).

### Character info (in-game)

Tabs: Attributes / Skills / Traits / Dossier / Logbook / Inventory (inventory is documented in its own section above).

#### Character info — common

| Key | Action |
|---|---|
| `Tab` | Announce current panel and position. |
| `D` | Announce full character summary. |
| `PgUp` / `PgDn` | Switch tab. |
| `E` | Announce current XP and XP to next level. |
| `S` | Open the unified stats browser (Header / Combat / Derived sections). |

#### Attributes / Skills (level-up flow)

Same as character creation, plus:

- **Skills:** `F` cycles section between **Learned** and the three **unlearned** categories (Combat / Knowledge / General).
- `Left` / `Right` switches unlearned category once you're past Learned.
- `+`/`-` or `Enter` → edit mode for raising attributes/skills with available points.
- `P` announces points remaining.
- `I` describes the focused attribute or skill.

#### Traits panel (in-game)

`Up` / `Down` navigate traits.

| Key | Action |
|---|---|
| `Enter` / `Space` | Toggle (if perk points available). |
| `I` | Open browsable perk description. |
| `P` | Announce perk points remaining. |

#### Dossier panel (in-game)

`Up` / `Down` navigate fields (name, biography, quirk, etc.).

| Key | Action |
|---|---|
| `I` | On the Quirk line: open the quirk description browser. On any other line: read the biography. |

#### Logbook panel

`Up` / `Down` navigate entries.

| Key | Action |
|---|---|
| `Enter` or `Right` | Open the selected entry's details. |
| `Left` | Previous sort category. |
| `F` | Next sort category. |
| `X` | Toggle flagged on the current entry. |
| `I` | Announce the entry's location / source. |

Inside a logbook detail view, baseline navigation moves through the detail lines (`Home` / `End` for first / last). `Tab` announces position; `Escape` or `Left` returns to the entry list.

## Original game hotkeys in combat

During **combat**, the mod intercepts only the keys it needs for the accessible cursor and menus — every other Wasteland 2 default binding passes straight through to the game, so you still issue the vanilla combat commands directly. The most useful:

| Key | Game action |
|---|---|
| `Space` | End turn. |
| `X` | Swap weapons. |
| `V` | Ambush (overwatch). |
| `R` | Reload. |
| `G` | Show the movement grid. |
| `'` (apostrophe) | Stand. |
| `;` (semicolon) | Crouch. |
| `B` | Change fire mode. |
| `N` | Toggle "show attack range". |
| `Z` | Highlight all enemies. |
| `1`–`9` | Trigger the on-screen item hotkey slots. |

Any other vanilla binding the mod doesn't claim in combat also reaches the game, subject to what the game itself allows mid-combat.

During **exploration the mod's map cursor owns the keyboard** — it suppresses the game's own input processing every frame — so these vanilla hotkeys generally do **not** fire there. Use the mod's equivalents instead (see [Exploration](#exploration-world--dungeon)); for example `I` opens the character / inventory screen, and the cursor handles map navigation and interaction.

**Camera keys:** `W` `A` `S` `D` still pan the camera, and `Q` / `E` rotate it — but rotation only takes effect when the camera-rotation lock is **off** (toggle with `F10`; the lock is on by default so "up" stays north).

## Notes on key conflicts

A few of the mod's keys overlap with Wasteland 2's own bindings. In **combat** the mod grabs only the keys it uses and leaves the rest to the game; in its **cursor and menu contexts (including exploration)** it suppresses the game's input more broadly, so the vanilla bindings are inactive there. Where an overlapping key matters:

- `T` (game: "center on character") — used by the combat initiative tracker in combat.
- `Tab` (game: "select next mob") — used for the actions menu in combat and the actions menu at the map cursor.
- `F` (game: "headshot / precision shot" in combat, unbound elsewhere) — camera-follow toggle in every cursor context, including combat. Precision shots are available through the Tab target-actions menu.
- `Space` (game: "end turn" in combat, "toggle group mode" in exploration) — `Space` is tactical pause in exploration. In combat, the mod leaves Space alone for End Turn.
- `]` (game: unbound by default) — "move to cursor" in exploration / combat / world map.

## Credits

- **Tolk** — Davy Kager (BSD 3-clause). Bridges the mod to NVDA, JAWS, and SAPI.
- **MelonLoader** — LavaGang. Loads the mod into the Unity game.
- **Harmony** — Andreas Pardeike. Runtime method patching.
- The Wasteland 2 community — testing, feedback, and putting up with the rough edges.

## License

Community accessibility mod. Modify and share freely.
