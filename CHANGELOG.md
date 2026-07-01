# Changelog

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
