# Prioritized cleanup proposal

Aggregated from 9 batch findings files (~250 individual findings → top 15 bundles).
Risk = low (no behavior change) / medium (needs smoke test) / high (could change announcements).
Line estimates approximate.

---

## Bundle A — Dead code removal pass
~250 lines removed across many small commits. Pure wins.

| # | Item | Lines | Risk |
|---|---|---|---|
| A1 | Delete two empty stub files: `Patches/UIDialogPatches.cs`, `Patches/CharacterInfoPatches.cs` | ~25 | low |
| A2 | Delete unused public Tolk wrappers (`HasBraille`, `Braille`, `Output`, `IsSpeaking`, `Silence`, `TrySAPI`, `PreferSAPI`) plus their P/Invoke decls | ~30 | low |
| A3 | Delete dead `WorldMapProximityAlert.CheckEncounterZoneProximity` + `IsPointInEncounterZone` + `insideEncounterZones` set (never called, comment confirms) | ~46 | low |
| A4 | Delete ~15 dead reflection field caches across `InventoryState`, `CharacterState`, `CharacterInfoState`, `ShopState` (cached but never read) | ~50 | low |
| A5 | Delete dead methods/wrappers: `FindPCOnTile` (MapCursor), `IsVisibleThroughFOW` (Scanner passthrough), `AnnounceStatDescription` (no callers), `GetTraitDescription` (CharacterState — no callers), `MarkKeyConsumed`/`WasKeyConsumed`/`IsAnyMenuStateActive` (InputRouter — no callers), `GetQueueSize` & `IsDialogueAudioPlaying` (AudioAware), empty Postfix body in `INV_DragDropItem_OnEnable_Patch` | ~50 | low |
| A6 | Delete dead variables/fields: `openToAttributes` & `suspendedPanel` (CharacterInfoState), `tabOrder` (GenericMenu), `DIRECTION_NAMES` (MapCursor — declared but unreferenced), `lastAnnouncement` & `lastAnnouncedInteractable` (NavigationManager — written never read), `DialogState.IsTutorialOpen()` dead `return false`, double `GetComponent<VND_DragDropItem>` in ShopState, several other small dead locals | ~30 | low |
| A7 | Remove ShortcutDoor `traceThis` ScottAFB-specific diagnostic block in `NavigationManager.UpdateFilteredList` (the entire `[NavTrace]`/`[NavDiag]`/`[NavReject]`/`[NavFilter]` instrumentation left over from a past investigation) | ~50 | low |
| A8 | Remove `FOWHelper.UpdateActivationTracking()` per-frame `[ActivationTrack]` diagnostic logging (or demote to a conditional flag) | ~12 | low |

---

## Bundle B — Cross-file consolidation
Eliminates duplication that compounds across the codebase. Most savings beyond just lines.

| # | Item | Lines | Risk |
|---|---|---|---|
| B1 | Consolidate `Vector2Distance` — currently 4 private copies (`WorldMapNavigationManager`, `WorldMapProximityAlert`, `WorldMapState`, `WorldMapPatchUtils`). Move to one shared static helper. | ~15 | low |
| B2 | Replace duplicate `GRID_SQUARE_SIZE = 1.6f` constants in `CombatState` and `MapCursorState` with `TileCoordinateSystem.SquareSize` (canonical) | ~5 | low |
| B3 | Consolidate direction lookup tables (`COVER_DIRECTIONS`, `CARDINAL_DIRECTIONS`, `DIRECTION_NAMES` — duplicated across `MapCursorState`, `CombatState`, `WorldMapState`) into a shared helper | ~25 | low |
| B4 | Consolidate `BubbleTextManager.bubbleTextInfos` reflection currently duplicated between `AudioAwareAnnouncementManager.IsVoiceAudioPlaying` and `VoiceoverHelper.IsVoiceoverPlaying` (same walk, same field cache) | ~50 | medium |

---

## Bundle C — Latent bug fixes + diagnostic logging
Small line impact, high correctness value.

| # | Item | Lines | Risk |
|---|---|---|---|
| C1 | Fix `GenericMenuState` Backspace double-handle bug — currently handled both via `Input.GetKeyDown(Backspace)` and via `inputString '\b'`, can delete two characters on one keypress | ~3 | low |
| C2 | Fix `WorldMapState.JumpToParty` not updating `lastAnnouncement` (breaks Backslash repeat) | ~2 | low |
| C3 | Add `MelonLogger.Warning` to ~15 silent `catch {}` blocks across `CombatState`, `MapCursorState`, `WorldMapState`, `CharacterInfoState`. Currently exceptions vanish — when a bug fires, nothing surfaces. | +30 | low |
| C4 | Fix the redundant `insideDiscoveryBoundary` add in `WorldMapProximityAlert.CheckRadiationProximity` (adds twice in one iteration) | ~4 | low |

---

## Bundle D — Reflection caching pass
Pure perf + readability improvement.

| # | Item | Lines | Risk |
|---|---|---|---|
| D1 | Cache 7 `AskQuantityMenu` reflection fields + 1 `DifficultySelectionMenu` field in `DialogState` as `static readonly` (currently re-acquired per call site) | ~15 | low |
| D2 | Cache `mValueField` and `saveTimeField` in `GenericMenuState` (currently resolved 3× and per-call respectively) | ~10 | low |
| D3 | Cache `saveGrid` and `sortMode` fields in `SaveLoadPatches` (currently re-fetched per call) | ~5 | low |
| D4 | Cache `buttonList` reflection walk in `ConversationPatches` (re-fetched per click and per hover) | ~10 | low |
| D5 | Cache `pcSelected` field in `InventoryState` popup branch (currently uncached at runtime) | ~3 | low |

---

## Bundle E — Method merges (bigger refactors, optional)
Higher line savings but riskier — every merge touches input handling.

| # | Item | Lines | Risk |
|---|---|---|---|
| E1 | Merge `HandleAttributesInput`/`HandleSkillsInput` edit-mode block in `CharacterState` (~60 lines duplicated) and `CharacterInfoState` (~22 lines duplicated) — extract a parameterised handler | ~80 | medium |
| E2 | Extract shared info-browser navigator (Up/Down/Home/End/Escape over `List<string>`) — implemented near-identically in `InventoryState`, `CharacterState`, `CharacterInfoState` | ~60 | medium |
| E3 | Merge `MainMenuState.NavigateUp`/`NavigateDown` (mirror methods) + collapse 6 structurally identical `ButtonEntry` blocks in `RebuildButtonList` | ~40 | low |
| E4 | Merge `NavigationManager.NextCategory`/`PreviousCategory` and `CycleNext`/`CyclePrevious` (same pattern in `WorldMapNavigationManager`) into single direction-parameterised methods | ~35 | low |
| E5 | Merge identical 4-line bodies in `UITooltipPatches` (`UITooltip_SetText` and `TextTooltip_SetText`) | ~10 | low |
| E6 | Merge identical bodies of `CharacterScreenPatches.UIInput_ProcessEvent_Patch` and `UIInput_Update_Patch` | ~15 | low |
| E7 | Merge `ConversationPatches.OnTopicPressed`/`OnTopicMouseOver` reflection walks (~45 duplicated lines each) | ~45 | medium |
| E8 | Merge `SaveLoadScreen_OnSaveClicked_Suppressor` and `SaveLoadScreen_OnLoadClicked_Suppressor` (identical bodies) | ~20 | low |
| E9 | Merge `ScannerState.ScanForEnemies` + `ScanForNPCs` into one pass over `Mob[]` (currently both call `FindObjectsOfType<Mob>` independently) | ~30 | medium |
| E10 | Combine `CombatState.FormatAction`/`FormatTargetAction` (identical bodies) | ~30 | low |

---

## Stale comments / minor cosmetic — fold into other commits as we touch files
- `DerivedStatNames` doc says "10 derived stats" but array has 11 elements
- `GenericMenuState` class summary refers to wrong priority ordering
- `MapCursor.FindNodeAtPosition` "direct ID lookup" comment misleading (actually iterates floors)
- `MainMenuState.AnnounceButton` "after a brief delay" stale (it's synchronous)
- Several others noted in batch findings — handle inline when we touch those files

---

## What I'd recommend

**Do all of A, B, C, D.** That's a low-to-medium-risk pass that removes ~330 lines, eliminates real duplication, fixes two latent bugs, and adds debuggability via logging. Each item is independent and committable on its own.

**Bundle E is optional.** Higher line savings (~360+ lines) but every merge touches input-handling code paths. Recommend smoke-testing each one in the affected screen. Pick the items you care about; the others can wait.
