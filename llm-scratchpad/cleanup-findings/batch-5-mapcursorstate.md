# Cleanup findings — States/MapCursorState.cs

## States/MapCursorState.cs

---

### Finding 1: `FindPCOnTile` is dead code — never called
- Lines: 3070–3079
- Removed: ~10
- Risk: **low**
- Detail: `FindPCOnTile()` returns the first living PC from `FindMobsOnTile()`. Every call site in the file uses `FindPCsOnTile()` (plural) instead. A grep across the entire codebase shows no callers outside this file. Safe to delete.

---

### Finding 2: `DIRECTION_NAMES` is declared but never used
- Lines: 115
- Removed: 1
- Risk: **low**
- Detail: `DIRECTION_NAMES = { "north", "east", "south", "west" }` is declared at line 115 alongside `COVER_DIRECTIONS` (identical content). There is no reference to `DIRECTION_NAMES` anywhere in the file or codebase outside its declaration line. All direction strings are produced by `DirectionHelper.GetDirectionDescription()`. The array is pure dead code.

---

### Finding 3: `COVER_DIRECTIONS` and `DIRECTION_NAMES` are identical — and duplicate `CombatState.COVER_DIRECTIONS`
- Lines: 112–115 (MapCursorState), also CombatState.cs:63
- Removed: 1–2 (per file if consolidated); 0 immediate risk
- Risk: **medium** (requires shared helper change)
- Detail: `COVER_DIRECTIONS` has the same four strings in both `MapCursorState` and `CombatState`, and `DIRECTION_NAMES` in `MapCursorState` and `WorldMapState` are identical to it. If a shared constant were added to a helper (e.g. `TileCoordinateSystem` or a new `GridConstants`), all three private copies could be removed. Marking medium because it touches two other files; `DIRECTION_NAMES` removal in this file is low-risk (Finding 2).

---

### Finding 4: `CARDINAL_DIRECTIONS` duplicated in `CombatState`
- Lines: 116–122 (MapCursorState), CombatState.cs:66–72
- Removed: 0 immediate; ~7 per file if consolidated
- Risk: **medium** (requires shared helper)
- Detail: The four `Vector3` direction constants (`forward`, `right`, `back`, `left`) are declared identically in both `MapCursorState` and `CombatState`. Neither is referenced outside its own file, so consolidation would require a shared constant class. Same situation as `COVER_DIRECTIONS`; a single `GridConstants` static class could hold all four arrays.

---

### Finding 5: Two separate `shiftHeld` / `shiftHeldForArrows` variables for the same keys
- Lines: 267–268 (`shiftHeldForArrows`), 427 (`shiftHeld`)
- Removed: 1 (deduplicate to one variable)
- Risk: **low**
- Detail: `shiftHeldForArrows` is computed at line 267 by reading `LeftShift || RightShift`. Then at line 427, a new local `shiftHeld` is computed identically. Both are used in the same `HandleInput()` frame. The second assignment could just reuse `shiftHeldForArrows`. The names differ only for documentation; `shiftHeld` is the better name, so either rename or alias.

---

### Finding 6: `interactables.IndexOf(nexus)` called in a `foreach` — O(n²) and error-prone
- Lines: 1419, 1464
- Removed: 0 (logic fix, not line removal) — but removes the O(n²) call
- Risk: **low**
- Detail: In both `OpenContextMenu` and `OpenTileSelectionMenu`, a `foreach (var nexus in interactables)` loop calls `interactables.IndexOf(nexus)` to build the `"select_N"` ASIName. This is O(n²) and can silently produce the wrong index if `interactables` contains duplicate references. Replace with a `for (int i = 0; i < interactables.Count; i++)` loop and use `i` directly.

---

### Finding 7: `SuppressInput()` helper omits `ShouldSuppressButtonEvents`, but the sub-mode preambles set all three flags inline
- Lines: 171–173 (`browsingPartyInfo` preamble), 211–213 (`browsingActions` preamble), 254–255 (normal cursor mode), 3208–3212 (`SuppressInput` definition)
- Removed: ~4 (if `SuppressInput` is extended or a full-suppress variant is added)
- Risk: **low**
- Detail: `SuppressInput()` sets `ShouldSuppressGameInput` and `ShouldSuppressUINavigation` but not `ShouldSuppressButtonEvents`. The two sub-mode preambles (lines 171–173, 211–213) set all three inline rather than calling `SuppressInput()`. This is an inconsistency: either `SuppressInput()` should also set `ShouldSuppressButtonEvents`, or an overload/new helper `SuppressAllInput()` is needed so the preamble code can call a single method instead of three inline assignments.

---

### Finding 8: `contextMenuActive = false` set in two places inside `BuildActionMenu` before calling `ExecuteInteraction` / `ExecuteInteraction` — but not via `CloseContextMenu`
- Lines: 1612, 1622
- Removed: 0 (logic concern, not line removal)
- Risk: **low**
- Detail: When `BuildActionMenu` detects 0 or 1 options, it manually sets `contextMenuActive = false` and clears `contextMenuOptions` (line 1622), but does not call `CloseContextMenu()`. This means `contextMenuIndex`, `contextMenuTarget`, `contextMenuPCs`, `pendingPCSelectionCallback`, `contextMenuTargetables`, and `pendingTargetableSelectionCallback` are not reset. In practice this is harmless because they are set fresh when the next menu opens, but it is fragile. Calling `CloseContextMenu()` instead of the manual assignments would be safer and shorter.

---

### Finding 9: `PerformFreeAimAttack` and `AnnounceCurrentTile` both compute hit/crit chance with identical silent try/catch blocks
- Lines: 836–845 (`AnnounceCurrentTile`), 3003–3011 (`PerformFreeAimAttack`)
- Removed: ~6 (extract helper)
- Risk: **low**
- Detail: Both methods call `pc.GetChanceToHit(mob/target, false)` and `pc.GetChanceToCriticalHit(target)`, clamp to 0–100, format as `"N% hit"` / `"N% crit"`, and swallow the exception with `catch (Exception) {}`. This is an ideal candidate for a small private helper such as `string GetHitChanceString(PC pc, Targetable target)`.

---

### Finding 10: Repeated `Language.Localize(x.displayName, false, false, string.Empty)` pattern (8 occurrences)
- Lines: 1248, 1557, 2364, 2401, 2504, 2645, 2996, 3140
- Removed: ~8 (reduce to helper call)
- Risk: **low**
- Detail: The call `UITextExtractor.CleanText(Language.Localize(template.displayName, false, false, string.Empty))` appears 8 times with the same constant arguments. A one-liner private static helper `LocalizeName(string raw)` would eliminate the repetition and make the intent clearer.

---

### Finding 11: Repeated `Replace("_", " ").Replace("(Clone)", "").Trim()` name-cleaning pattern (3 occurrences)
- Lines: 1254, 1307, 2273
- Removed: ~3 (replace with helper call)
- Risk: **low**
- Detail: The identical `.Replace("_", " ").Replace("(Clone)", "").Trim()` chain appears in `GetMobName`, `GetInteractableName`, and `GetMeaningfulName`. It could be extracted into a private static helper `CleanGameObjectName(string name)`. Since `UITextExtractor.CleanText` already exists for NGUI text, this is a separate concern (raw Unity object names, not NGUI-formatted), so a local helper is appropriate.

---

### Finding 12: `BuildExplorationActionList` uses five separate try/catch blocks that only log to `MelonLogger.Warning` and swallow the exception
- Lines: 2314–2348, 2351–2392, 2395–2418, 2421–2444, 2447–2495, 2498–2582
- Removed: 0 (no lines removed, but this is a correctness concern)
- Risk: **low**
- Detail: Each section of `BuildExplorationActionList` (skills, items, swap, crouch, reload, free aim) wraps its block in `try { ... } catch (Exception ex) { MelonLogger.Warning(...) }`. Swallowing and only logging means a bug in one section silently produces a partial action list with no user-visible feedback. At minimum, the exceptions should be surfaced to `MelonLogger.Error` rather than `Warning` since the catch point discards the exception entirely. Consider whether each section genuinely needs independent isolation or if a single outer try/catch with `MelonLogger.Error` suffices.

---

### Finding 13: `BuildPartyMemberInfo` uses bare `catch { }` (three occurrences) that swallow exceptions with zero logging
- Lines: 3131, 3151, 3167
- Removed: 0
- Risk: **low**
- Detail: The Level/XP, Weapon, and Status Effects sections each silently swallow all exceptions with an empty `catch { }`. Unlike `BuildExplorationActionList`, there is not even a log message. These should at minimum become `catch (Exception ex) { MelonLogger.Warning(...) }` so failures are diagnosable.

---

### Finding 14: `ExecuteInteraction` — `bInstigateBlocked` check speaks two announcements when a real skill ASI is active
- Lines: 1929–1936
- Removed: 0 (logic bug, not dead code)
- Risk: **low**
- Detail: When `target.drama.bInstigateBlocked` is true and a real skill ASI is active, the code: (a) calls `ScreenReaderManager.SpeakInterrupt("Using [skill] on [name]")` at line 1926, then immediately (b) calls `ScreenReaderManager.SpeakInterrupt("Cannot interact with [name]")` at line 1932. The first announcement is immediately interrupted by the second, so the user hears only "Cannot interact" — but it wastes a Tolk call. The initial announcement at line 1926 should be deferred until after the `bInstigateBlocked` guard has been cleared.

---

### Finding 15: `AnnounceDistanceToSelected` and `AnnounceDistanceToParty` share ~15 lines of identical tile-distance-and-direction logic
- Lines: 2078–2108, 2126–2154
- Removed: ~12 (extract helper)
- Risk: **low**
- Detail: Both methods: (1) round a world position to grid coordinates, (2) refine via `FindNodeAtPosition`, (3) compute Chebyshev tile distance from cursor, (4) call `DirectionHelper.GetDirectionDescription`, (5) format `"N tile(s) [direction]"`. The only differences are the source position (cursor vs cursor) and what gets announced. A private helper `GetTileDistanceAndDirection(Vector3 targetWorldPos, out int dist, out string direction)` would absorb the common logic.

---

### Finding 16: `JumpToParty` checks for null `pc` but then calls `InitializeToPartyPosition` which checks for null again internally
- Lines: 2110–2124
- Removed: ~4 (remove redundant guard)
- Risk: **low**
- Detail: `JumpToParty` calls `GetPartyLeader()` at line 2112, returns early if null, then calls `InitializeToPartyPosition()` which also calls `GetPartyLeader()` and returns early if null (line 563). The outer null-check in `JumpToParty` is redundant — `InitializeToPartyPosition` handles it. The guard can be removed. Conversely, the `GetPCDisplayName(pc)` call that comes after (in `AnnounceDistanceToParty`) legitimately needs a null check, so the pattern is asymmetric.

---

### Finding 17: `FormatAction` uses `actionList.IndexOf(action)` (O(n) linear scan on every navigation keypress)
- Lines: 2587
- Removed: 0 (performance fix, no line change)
- Risk: **low**
- Detail: `FormatAction(ExplorationAction action)` resolves the 1-based position by calling `actionList.IndexOf(action)`, which is an O(n) list scan. Since `AnnounceCurrentAction` already holds `actionIndex`, it could pass the index directly: `FormatAction(actionList[actionIndex], actionIndex)` or compute position as `actionIndex + 1` in the caller. For action lists of typical size (< 20 items) this is cosmetic but inconsistent with how `FormatPartyInfoLine(int index)` works (which already receives the index directly).

---

### Finding 18: Misleading comment on `FindNodeAtPosition` — says "Try direct ID lookup first" but the actual direct lookup (at the raycasted floor) is never tried
- Lines: 598–609
- Removed: 0 (comment fix)
- Risk: **low**
- Detail: `int floor = GetFloorLevel(worldPos)` at line 599 raycasts to get the real floor level, but the loop at lines 604–610 iterates all floors 0–5 in order anyway — it does not start from `floor` specifically. The comment "works when grid origin aligns with world origin" implies the `floor` variable is used for a priority lookup, but it is only used in the fallback debug log at line 630. The comment should clarify that the loop tries all floors (including the raycasted one, which happens to be checked somewhere in the iteration) rather than implying a direct single-floor lookup.

---

### Finding 19: `GetCoverDescription` returns `"No cover info"` when `node.cover` is null, distinct from `"No cover"` when it is empty
- Lines: 927, 936
- Removed: 0 (minor inconsistency)
- Risk: **low**
- Detail: When `node.cover == null`, the method returns `"No cover info"` (line 927); when `node.cover` is a non-null empty array, it returns `"No cover"` (line 936). In `AnnounceCurrentTile`, both strings are appended to the announcement identically. A screen reader user hears "No cover info" vs "No cover" for technically equivalent states. Unify to `"No cover"`.

---

### Finding 20: `PerformFreeAimAttack` outer try/catch swallows exceptions and announces "Attack failed" — inner `CanAttack`/`TargetVisible` try/catches also swallow silently
- Lines: 2862–3025 (outer), 2907–2908, 2913–2914, 2925–2926, 2953–2954, 2961–2962, 3003–3011
- Removed: 0
- Risk: **low**
- Detail: There are seven separate swallowing catch blocks inside `PerformFreeAimAttack`. The outer one logs to `MelonLogger.Error` (acceptable), but the five inline ones for `CanAttack`, `IsAttackableTarget`, `TargetVisible`, `GetAdditionalHitRange`, and hit-chance computation are completely silent. If any of these game API calls throws (e.g. due to a null component), the corresponding variable stays at its default (`false`, `0f`, `""`) and the flow silently branches incorrectly. Adding at least `MelonLogger.Warning` to each silent catch would aid debugging.

---

### Finding 21: `follower.mobState` is never checked in `FindMobsOnTile` for party followers — inconsistent with NPC path
- Lines: 1186–1200 (NPC loop checks `mobState == DEAD`), 1203–1219 (follower loop — no `mobState` check)
- Removed: 0 (missing filter)
- Risk: **low**
- Detail: The NPC loop at lines 1188–1200 explicitly skips dead NPCs (`if (npc.mobState == Mob.MobState.DEAD) continue`). The party-follower loop at lines 1205–1218 has no such check. If a follower is dead (e.g. knocked unconscious and revived partially, or a dead NPC-follower), their body could appear in tile results. Adding `if (follower.mobState == Mob.MobState.DEAD) continue;` after the hidden/perception checks would be consistent.

---

### Finding 22: `UseItemOnTile` and `UseSkillOnTile` share identical priority structure (PCs > non-PC mobs > interactables) with 20+ duplicated lines
- Lines: 2639–2724 (`UseItemOnTile`), 2726–2784 (`UseSkillOnTile`)
- Removed: ~20 (extract shared dispatch helper)
- Risk: **medium**
- Detail: Both methods follow the same "try multiple PCs → try single PC → try non-PC mobs → try interactables" dispatch pattern. The log/speak/call patterns differ only in the verb and the actual API call at the leaf. A private helper `FindFirstTileTarget(out PC pc, out Mob mob, out InteractableNexus nexus)` returning an enum/result type, or a callback-accepting dispatch, would reduce ~40 lines of near-duplicate code to ~10. Marking medium due to the callback indirection needed.

---

### Finding 23: `contextMenuActive = false` field initializer is redundant (bool defaults to false in C#)
- Lines: 53
- Removed: 1 (the `= false`)
- Risk: **low**
- Detail: `private bool contextMenuActive = false;` — the explicit `= false` initializer is redundant for a bool field in C#. Same applies to `lastMoveTime = 0f` (line 32) and `lastStepChangeTime = 0f` (line 40), `contextMenuIndex = -1` is intentional and non-default, so leave that. The `= false` and `= 0f` initializers add no information. Minor, purely cosmetic.

---

### Finding 24: Sub-mode dispatch preambles (`browsingPartyInfo`, `browsingActions`) inline three identical suppression assignments rather than calling a helper
- Lines: 170–173 (`browsingPartyInfo`), 210–213 (`browsingActions`)
- Removed: ~4 if `SuppressAllInput()` helper is introduced (relates to Finding 7)
- Risk: **low**
- Detail: Both sub-mode entry blocks set `ShouldSuppressUINavigation`, `ShouldSuppressGameInput`, and `ShouldSuppressButtonEvents` to `true` using three separate inline statements. The context menu mode (`contextMenuActive`) sets them inside `HandleContextMenuInput` via `SuppressInput()` plus an additional inline `ShouldSuppressButtonEvents`. A `SuppressAllInput()` helper that sets all three would unify the pattern. This is a duplicate of Finding 7 expressed from the call-site perspective.

---

### Finding 25: `ExitActionsBrowse` announces "Actions closed" unconditionally — called from `ExecuteCurrentAction` where the announcement is immediately followed by the action's own announcement
- Lines: 2630–2635, 2617 (call from `ExecuteCurrentAction`)
- Removed: 0 (verbal noise, not lines)
- Risk: **low**
- Detail: When `ExecuteCurrentAction` runs, it calls `ExitActionsBrowse()` (which says "Actions closed"), then calls `action.Execute()` which makes its own announcement (e.g. "Pick Lock active. Target an object with Enter"). The user hears two rapid-fire announcements. "Actions closed" is potentially useful when the user presses Escape, but redundant when they press Enter to execute. `ExitActionsBrowse` could accept a `bool silent` parameter, or `ExecuteCurrentAction` could inline its own teardown without calling the announcing version.

---
