# Changelog

## [0.8.6-beta] — 2026-07-18

Persistent location labels, a universal Drop action, and a batch of fixes across the world map, scanner, combat, and settings.

### Added
- **Location labels** (`N`) — name the cursor's tile so you can recognise a place on a later visit (the teleporting door among five identical "Door"s). Named places cycle in the scanner with distance and direction, and the tile read-out speaks the label first. Labels are keyed to a tile and stored in UserData, so they persist across saves and sessions; a confirmed "clear all labels" lives in the `Shift+S` settings menu.
- **Drop for every backpack item** — the inventory context menu now offers **Drop** for any carried item, not just books. Equipped gear and quest-critical (no-drop) items are left out.

### Fixed
- Jumping to a scanner location with `Home` on the world map no longer locks the cursor — POI targets sat below the walkable navmesh, so the arrows reported "Blocked" in every direction. The jump now snaps onto the navmesh and the arrows move normally.
- The `Shift+S` settings hotkey no longer goes dead for the rest of the session after a conversation.
- Disarmed land mines and tripwires drop out of the exploration scanner, so the live threats are easier to pick out.
- Free-aiming at destructible cover now hits the cover instead of falling through to a ground miss; static, indestructible cover is announced ("indestructible cover" / "That cover can't be destroyed") instead of silently wasting the shot.

### Changed
- Documentation now headlines the one-download installer (`Wasteland2AccessibilityMod-Installer.exe`) as the primary install method, with the manual MelonLoader steps kept as a fallback.

## [0.8.1-beta] — 2026-07-08

Cover awareness, plus a batch of exploration and combat fixes.

### Added
- **Cover scanner category** — the exploration scanner and the combat cursor can now cycle nearby cover positions (short / tall), each with distance and direction. Exploration lists visible cover around the party; combat lists cover within 50 tiles of the acting ranger. `Home` jumps the cursor to the selected cover.
- **Readable books get the standard item menu** — pressing Enter on a book now opens the usual submenu (Read, Flag as Junk, Drop, Give to) instead of opening the book directly, so books can be dropped or handed to another ranger. Quest-critical (no-drop) books stay protected.

### Fixed
- Free-aiming at a destructible piece of cover now sends the shot at the cover instead of missing at the ground — for both the combat and exploration cursors.
- The "cursor stops at walls" option no longer makes containers, doors, and other interactables that sit on obstacle tiles unreachable.
- The game-over screen's options are now read and selectable; the party-member death screen now closes with Enter as well as Escape.
- Visible, interactable NPCs are no longer hidden from the exploration scanner.

## [0.8.0-beta] — 2026-07-03

New exploration awareness features (sound cues, nearby scan, movement notifications), a cursor option, and conversation fixes.

### Added
- **Scanner category sounds** — a short cue plays when a new item enters the exploration scanner, with a distinct sound per category (characters, containers, objects, exits, examine, loot, and a generic cue for miscellaneous). Party members get no cue. Toggle in settings.
- **Sound glossary** — the last item in the `Shift+S` settings menu opens a preview browser: arrow through each scanner sound to hear it and what it means.
- **Nearby scan** (`L`) — lists every scanner-visible item within a radius of the grid cursor, nearest first, each with its direction. Comma/period shrink and grow the radius.
- **Party stopped notification** — announces when the party finishes an ordered move and comes to rest, in exploration and on the world map. Ungrouped members that stop are named individually. Toggle in settings.
- **Cursor stops at walls** (setting) — optionally confine the exploration grid cursor to walkable ground instead of letting a single step pass onto wall/terrain tiles.
- The NVDA controller client (`nvdaControllerClient64.dll`) is now bundled in the release archive, so NVDA users get speech without fetching it separately.

### Fixed
- Cutscene-driven conversations (e.g. the radio-tower toll shakedown) were unreadable and unnavigable — dialogue options now read and arrow-key navigation works during them.
- Multi-step dialogue no longer says "Loading, please wait" while waiting for the player to advance; arrows say "Press Enter to continue" and Enter advances the line.
- Attribute debuff sources are announced reliably on the character sheet.

## [0.7.5-beta] — 2026-07-01

Follow-up to the first public beta: a new settings/help layer, richer announcements, and a batch of fixes from beta feedback.

### Added
- **Accessibility settings menu** (`Shift+S`) — a modal, navigable on/off list of every mod setting, available anywhere.
- **Help key** (`Shift+/`) — reads back the controls for the current screen or cursor, with per-context text. Accepts the numpad slash too.
- Global help/settings hotkeys that work across contexts.
- Screen-reader support for the weapon field-strip result popup.
- A loading cue during the conversation pre-input dead zone, so the wait before options appear is no longer silent.

### Changed
- Enriched buff/debuff and stat announcements in the character screen.
- Inventory and shop item-info browsers now share one full item-info block, so both read the same detail.
- Voiced-dialogue handling adds a grace cap for lines that are pending but never play, so announcements aren't held indefinitely.
- Robustness pass: surface errors instead of swallowing them, de-duplicate repeated announcements, and quiet routine logging.
- Docs: Tolk.dll is now bundled in the release archive; controls consolidated and hotkey/version references corrected.

### Fixed
- Menu input lag caused by per-frame scene scans (now cached).
- Tamed-animal conversation options were not navigable.
- Main-menu version announcement was being clobbered by focus.
- Funeral intro now reads the NPC description and the click-to-continue prompt.
- Pokable door teleporters are revealed regardless of destination fog.
- Invisible scripting triggers are filtered out of interactable announcements and on-tile lookups.
- Menu wrap cues, `Home`/`End` coverage, and start-up and scanner behavior.

## [0.7.0-beta] — 2026-05-28

First public beta of the rewritten accessibility mod. The earlier Xbox-controller-default release has been superseded entirely; this is a different mod sharing only the name.

### Added — Screen reader pipeline
- Tolk integration for NVDA, JAWS, and SAPI output.
- Audio-aware announcement queue that defers screen-reader speech while game voiceover is playing.
- `UITextExtractor` cleans NGUI formatting codes, color tags, and embedded markers before speech.

### Added — Keyboard navigation
- Unified input-routing system (`InputRouter`) that picks the right state per frame based on what's on screen.
- Full keyboard navigation in: main menu, modal dialogs, tutorials, keypad popups, character creation (all 8 panels), in-game character info (Attributes/Skills/Traits/Dossier/Logbook), inventory, loot containers, vendor/shop, conversations, and the world map.
- Save / Load screen text editing with screen-reader feedback.

### Added — Exploration
- Interactable **scanner** (`PgUp`/`PgDn`) with category cycling (`Ctrl+PgUp`/`PgDn`).
- Grid-based **map cursor** for tile-by-tile world exploration with cardinal movement, step-size adjustment, ctrl-extend, and tile announcements.
- Context menu, actions menu, and tile selection for the map cursor.
- Item-on-object detection — auto-uses the right item (e.g. shovel on dirt pile) when present.
- Party-member info quick view from the cursor.
- Quick-switch party members with `F1`–`F7`.

### Added — World map
- Review cursor with cardinal movement and adjustable step size.
- POI cycling with category filtering.
- Proximity alerts for POIs and radiation as the cursor moves.
- Water-cost path estimation (`Shift+W`) and party water summary (`W`).
- Move-party-to-cursor command (`]`) with water cost and radiation warnings.

### Added — Combat
- Initiative tracker (`T`) — full turn order with HP, AP, and status.
- Combat actions menu (`Tab`) — movement, attacks, skills, items.
- Target actions menu with Actions / Info tabs.
- Combat log browser (`L`).
- Free-aim and item-use targeting modes through the same cursor.
- Combatant cycling by category.
- Auto-jumps cursor to active actor on turn change and announces the new turn.

### Added — Quality-of-life
- **Tactical pause** (`Space`) with auto-pause for inventory / loot / vendor screens.
- **Camera rotation lock** (`F10`) — "up" stays north.
- Configurable direction format (cardinal vs clock positions) — toggle with `=`.
- Configurable tile-vs-coordinate announcement order — toggle with `K`.
- Configurable distance units (tiles vs meters) — config file.
- Last-announcement repeat (`\`).
- Party scrap announcement (`'`).
- Persistent config at `UserData/Wasteland2Accessibility.cfg`.

### Known gaps
- Some one-off UI screens may not be announced. Please report.
- Keyboard support inside the in-game options menu's more complex controls (key rebinding lists, etc.) is partial.
- Multi-language: announcements use English strings derived from the game's own localization; non-English locales work but mod-side prompts ("Tactical pause", "Step size", etc.) remain English.
