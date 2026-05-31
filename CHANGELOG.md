# Changelog

## [2.0.0-beta] — 2026-05-28

First public beta of the rewritten accessibility mod. The 1.0 release (Xbox-controller-default) has been superseded entirely; v2.0 is a different mod sharing only the name.

### Added — Screen reader pipeline
- Tolk integration for NVDA, JAWS, and SAPI output.
- Audio-aware announcement queue that defers screen-reader speech while game voiceover is playing.
- `UITextExtractor` cleans NGUI formatting codes, color tags, and embedded markers before speech.

### Added — Keyboard navigation
- Unified input-routing system (`InputRouter`) that picks the right state per frame based on what's on screen.
- Full keyboard navigation in: main menu, modal dialogs, tutorials, keypad popups, character creation (all 8 panels), in-game character info (Attributes/Skills/Traits/Dossier/Logbook), inventory, loot containers, vendor/shop, conversations, and the world map.
- Save / Load screen text editing with screen-reader feedback.

### Added — Exploration
- Interactable list cursor (`PgUp`/`PgDn`) with category cycling (`Ctrl+PgUp`/`PgDn`).
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
