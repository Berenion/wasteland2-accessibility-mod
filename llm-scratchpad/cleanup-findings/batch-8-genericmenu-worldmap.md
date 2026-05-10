# Cleanup findings — GenericMenuState + WorldMapState

## States/GenericMenuState.cs

### Finding 1: `tabOrder` field declared but never read
- Lines: 61
- Removed: 1
- Risk: low
- Detail: `private static readonly string[] tabOrder = { "gameplay", "display", "controls", "audio" }` is declared at the class level but never referenced anywhere in the file or elsewhere in the project. The tab-switching logic in `SwitchOptionsTab` and `SwitchToTab` uses a hardcoded `switch` on integer indices, not this array. Safe to delete.

---

### Finding 2: `mValueField` reflection lookup performed three separate times per text-edit session
- Lines: 138–147, 220–229, 1425–1432
- Removed: ~9 (by caching as a `static readonly FieldInfo`)
- Risk: low
- Detail: `typeof(UIInput).GetField("mValue", BindingFlags.NonPublic | BindingFlags.Instance)` is computed fresh at lines 138, 220, and 1425 — up to three times per save-name editing session. All three results are used identically (set value, call UpdateLabel, or fall back to the public setter). Extracting a `private static readonly FieldInfo s_mValueField` initialised once at class load eliminates two redundant reflection calls and removes the repeated `if (mValueField != null) … else …` fallback boilerplate at each site.

---

### Finding 3: `saveTimeField` reflection lookup repeated inside a lambda comparator on every comparison call
- Lines: 697–710
- Removed: 3 (hoist to a local before the sort)
- Risk: low
- Detail: `typeof(SaveGameListEntry).GetField("saveTime", BindingFlags.NonPublic | BindingFlags.Instance)` is called once and stored in `saveTimeField` before the lambda, so the field-info lookup itself is not per-comparison. However, `a.GetComponent<SaveGameListEntry>()` and `b.GetComponent<SaveGameListEntry>()` are called inside the comparator, which Unity may invoke O(n log n) times on large save-game lists. These components were already retrieved when building the `children` list and could be stored in a parallel list of `(Transform, SaveGameListEntry, DateTime)` tuples to avoid repeated `GetComponent` calls. Low-risk improvement; no semantic change.

---

### Finding 4: `isEditingTextField = false` set redundantly in `OnDeactivated` at line 361 and again via `ReinitializeForScreen` which is not called from `OnDeactivated`
- Lines: 342, 361, 378
- Removed: 1
- Risk: low
- Detail: `OnActivated` (line 342) sets `isEditingTextField = false` before calling `ReinitializeForScreen` (line 344), which also sets it (line 378). `OnDeactivated` sets it at line 361 directly. This is correct behaviour, but `OnActivated` sets it at line 342 and then `ReinitializeForScreen` sets it again at line 378 — one of those two is redundant. The assignment in `OnActivated` (line 342) could be removed since `ReinitializeForScreen` always runs immediately after and unconditionally sets it. Zero semantic change.

---

### Finding 5: `SwitchOptionsTab` re-derives `currentTab` from panel `activeSelf` flags using the same pattern as `GetActiveTabName` but returns an integer instead of a string
- Lines: 473–478 vs 458–465
- Removed: 0 (no lines removed, but a private `GetActiveTabIndex()` helper of ~5 lines could replace both)
- Risk: low
- Detail: `GetActiveTabName` (lines 458–465) and the `currentTab` detection block in `SwitchOptionsTab` (lines 474–478) both walk the four panels in the same `if/else if` order checking `activeSelf`. They are not duplicates of each other (one returns a string, one an int), but the detection logic is identical. Extracting a private `GetActiveTabIndex(OptionsMenu menu)` helper and having `GetActiveTabName` call it would consolidate the panel-scanning logic into one place. Currently, if a new panel were added, it must be updated in three locations (`GetActiveTabName`, `GetActiveTabIndex`-equivalent, and `GetActivePanel`).

---

### Finding 6: `GetActiveTabName` and `GetActivePanel` implement the same panel-scanning logic independently
- Lines: 458–465 (`GetActiveTabName`), 842–848 (`GetActivePanel`)
- Removed: 0 (logic unification opportunity)
- Risk: low
- Detail: Both methods iterate `gameplayPanel`, `displayPanel`, `controlsPanel`, `audioPanel` in the same order checking `activeSelf`. `GetActivePanel` returns the `GameObject`; `GetActiveTabName` returns a human-readable string. They are maintained separately and must both be updated if a fifth tab is ever added. A single `GetActiveTabIndex` returning an int (0–3, -1 on miss) would let both derive their results, eliminating one copy of the four-panel check.

---

### Finding 7: `SwitchOptionsTab` calls `GetActiveTabName` _after_ calling `SwitchToTab`, relying on the tab switch having already taken effect
- Lines: 491–500
- Removed: 0
- Risk: low (comment only)
- Detail: `SwitchToTab(newTab)` invokes a private Unity game method via reflection; there is no documented guarantee that `activeSelf` flags update synchronously before `GetActiveTabName` is called on line 498. In practice this works because NGUI panel switches activate/deactivate immediately. However, `tabName` could instead be derived from `tabOrder[newTab]` using the already-computed `newTab` value (e.g. `char.ToUpper(tabOrder[newTab][0]) + tabOrder[newTab].Substring(1)`) without relying on reflection side effects, which would also make `tabOrder` useful for the first time. Low risk as-is, but the current code is fragile to any future async tab-switch implementation.

---

### Finding 8: Empty `if` branch in `ReinitializeForScreen` — OptionsMenu case does nothing
- Lines: 388–391
- Removed: 3
- Risk: low
- Detail: The `if (cachedOptionsMenu != null) { // OptionsMenu handled by BuildOptionsControlList in EnsureSelection }` block (lines 388–391) has a comment body and does nothing. The comment is explanatory but the branch structure adds visual noise. The `else if` chain naturally falls through to `AnnounceMenu` and `EnsureSelection` whether or not this branch exists. The comment could be moved to `EnsureSelection` where the deferred build actually happens.

---

### Finding 9: `NavigateGeneric` references `cachedTopScreen as SaveLoadScreen` instead of `cachedSaveLoadScreen`
- Lines: 1072
- Removed: 0
- Risk: low
- Detail: At line 1072, `NavigateGeneric` casts `cachedTopScreen as SaveLoadScreen` to a local `saveLoadScreen` variable. `cachedSaveLoadScreen` is already the same reference, stored at `ReinitializeForScreen` line 384. Using `cachedSaveLoadScreen` directly (which will be non-null when the top screen is a `SaveLoadScreen`) avoids the redundant cast and aligns with all other SaveLoadScreen accesses in the file.

---

### Finding 10: `ActivateSelected` casts `cachedTopScreen as SaveLoadScreen` to a local instead of using `cachedSaveLoadScreen`
- Lines: 1397
- Removed: 1
- Risk: low
- Detail: Same pattern as Finding 9 — `SaveLoadScreen saveLoadScreen = cachedTopScreen as SaveLoadScreen;` at line 1397 creates a redundant local variable when `cachedSaveLoadScreen` is always the equivalent non-null reference at this point. Using `cachedSaveLoadScreen` directly and removing the local removes one line and one cast.

---

### Finding 11: Backspace handled twice in the text-edit branch — once by `GetKeyDown` and once via `inputString`
- Lines: 184–188, 196–202
- Removed: ~5
- Risk: low
- Detail: In the `isEditingTextField` block, lines 184–188 handle `Input.GetKeyDown(KeyCode.Backspace)` first. Lines 196–202 inside the `inputString` loop also handle `'\b'` (which Unity sends in `inputString` alongside the `Backspace` key). On a frame where Backspace is pressed, both branches execute, potentially deleting two characters instead of one. This is a latent bug. The `GetKeyDown(Backspace)` check at line 184 should be removed and backspace should be handled only through `inputString` (as `'\b'`), or the `'\b'` branch inside the loop should be skipped with a flag set by the `GetKeyDown` check.

---

### Finding 12: `HandleInput` checks `currentTop is ModalMessageMenu` at line 245 and returns false, but `IsActive` already excludes `ModalMessageMenu` at line 103
- Lines: 244–246
- Removed: 3
- Risk: low
- Detail: `IsActive` calls `FindTopScreen()` and returns `false` when `topScreen is ModalMessageMenu`, so `HandleInput` should never be called while a modal is on top. The second check at lines 244–246 is defensive dead code. The only scenario where it could fire is if `IsActive` and `HandleInput` are called with a stale state between frames, but `InputRouter` evaluates `IsActive` before each `HandleInput` call. The check could be replaced with a Debug assertion or removed. Keeping it adds ~3 lines and `FindTopScreen()` (an `Object.FindObjectsOfType` scan) runs an extra time on every non-editing frame.

---

### Finding 13: `FindTopScreen()` called twice per non-editing `HandleInput` frame
- Lines: 244, 248 vs call within `IsActive` at line 102
- Removed: 0 (refactor opportunity)
- Risk: low
- Detail: In a single `HandleInput` call, `FindTopScreen()` is called at line 244 (to check for ModalMessageMenu) and conditionally at line 248 (the result is already in `currentTop`). `IsActive` also calls it, so in a frame where `IsActive` is polled and `HandleInput` is then called, `FindObjectsOfType<GUIScreen>` runs at least twice. Caching `currentTop` once at the top of `HandleInput` and reusing it for both checks eliminates the redundant scan. Currently lines 244 and 248 already share the `currentTop` local, so this is only an issue relative to the `IsActive` call.

---

### Finding 14: `CloseMenu` calls `FindObjectsOfType<GUIScreen>` independently instead of reusing `FindTopScreen()`
- Lines: 1574–1585
- Removed: ~6
- Risk: low
- Detail: `CloseMenu` contains its own inline loop over `FindObjectsOfType<GUIScreen>()` to find the top screen. `FindTopScreen()` already encapsulates this logic. Replacing the inline loop with a call to `FindTopScreen()` removes ~8 lines and makes the two implementations consistent (both skip `MainMenu`, both check `isTopMenu`). The only minor difference is that the inline loop breaks on the first `isTopMenu` hit without skipping `MainMenu` — but the `if (screen is MainMenu) continue` guard in `FindTopScreen` is the safer behaviour.

---

### Finding 15: `blockUIInput` coupling — Core/InputSuppressor directly references the States namespace
- Lines: `Core/InputSuppressor.cs` lines 137, 157, 172; `Patches/CharacterScreenPatches.cs` lines 218, 233
- Removed: 0 (architectural note)
- Risk: low (noting, not recommending split)
- Detail: `InputSuppressor.cs` imports `Wasteland2AccessibilityMod.States.GenericMenuState.blockUIInput` by full name. This creates an inward dependency from Core to States. `CharacterScreenPatches.cs` also references all three `blockUIInput` flags (GenericMenuState, CharacterState, CharacterInfoState) directly. This is already flagged in `llm-scratchpad/current_status.md` as a known coupling issue. A static `InputSuppressor.ShouldBlockUIInput` property that the three states write into (similar to `ShouldSuppressGameInput`) would decouple Core and Patches from States. Out of scope as an architectural change, but noted as confirmation the issue is real.

---

### Finding 16: `FormatCamelCase` — the `i > 0` guard inside the loop body is always true because the loop starts at `i = 1`
- Lines: 449–451
- Removed: 0 (condition simplification)
- Risk: low
- Detail: The loop runs from `i = 1` to `text.Length - 1`, so `i > 0` is invariably `true` inside the loop body at line 451. The condition `char.IsUpper(text[i]) && i > 0 && char.IsLower(text[i - 1])` can be simplified to `char.IsUpper(text[i]) && char.IsLower(text[i - 1])` without any semantic change.

---

### Finding 17: Priority comment in the class `<summary>` is stale ("below MainMenu but above conversation states")
- Lines: 12–14 (XML doc comment)
- Removed: 0
- Risk: low
- Detail: The summary says "Priority 55 - below MainMenu but above conversation states." The actual priority ordering (from CLAUDE.md) is MainMenuState at its own priority, then GenericMenuState 55, CharacterState 50, CombatState 45. ConversationState/DialogState sits at 70, which is _above_ GenericMenuState, not below it. The comment is misleading; it should say "Priority 55 — below DialogState (70) and ConversationState, above CharacterState (50)."

---

### Finding 18: `OnDeactivated` resets `isEditingTextField` at line 361 but the text-edit flow can leave `nameInput` in a partial state
- Lines: 347–362
- Removed: 0 (correctness note)
- Risk: medium
- Detail: If `OnDeactivated` fires while `isEditingTextField` is true (e.g. a modal dialog opens over SaveLoadScreen mid-edit), `isEditingTextField` is reset but `cachedSaveLoadScreen.nameInput` is not restored to `editingOriginalValue`. The next activation will see the partial text as the starting value. The fix is a single call to restore `editingOriginalValue` into the nameInput inside `OnDeactivated` before clearing references, but requires the same `mValueField` reflection pattern. Noting as medium risk because the window is narrow (a modal appearing mid-name-edit) and the consequence is user-visible stale text, not a crash.

---

### Finding 19: `BuildSaveLoadControlList` reflection for `saveTimeField` is not cached — recalculated every time the save list is built
- Lines: 697–698
- Removed: 0 (cache as static)
- Risk: low
- Detail: `typeof(SaveGameListEntry).GetField("saveTime", BindingFlags.NonPublic | BindingFlags.Instance)` is retrieved via reflection every time `BuildSaveLoadControlList` is called (on each activation or top-screen change). It could be promoted to a `private static readonly FieldInfo s_saveTimeField` to match the intent of `intendedPathField` in WorldMapState. Eliminates one reflection call per save/load screen activation.

---

### Finding 20: `NavigateControlList` recalculates `current` from `optionsControlIndex` and then immediately falls back to `UICamera.selectedObject` — the fallback is unreachable when `optionsControlIndex` is in range
- Lines: 877–879
- Removed: 0
- Risk: low
- Detail: At line 877, `current` is assigned as `optionsControls[optionsControlIndex]` when in-range, else `UICamera.selectedObject`. This block is only reached after the guard at lines 869–872 calls `FindCurrentControlIndex()` which guarantees `optionsControlIndex >= 0`. So the ternary's false branch (`UICamera.selectedObject`) is unreachable when `optionsControls` is non-empty. The ternary can be simplified to a direct array access.

---

## States/WorldMapState.cs

### Finding 1: `Vector2Distance` is a private static method duplicated across four files
- Lines: 849–854
- Removed: 6
- Risk: low
- Detail: Identical `Vector2Distance(Vector3, Vector3)` implementations exist in `WorldMapState`, `WorldMapNavigationManager` (line 383), `WorldMapProximityAlert` (line 428), and `WorldMapPatches.cs` as `WorldMapPatchUtils.Vector2Distance`. This was already identified in batch-1-toplevel.md as Finding 30. In `WorldMapState` specifically, removing the private copy and calling the existing `WorldMapPatchUtils.Vector2Distance` (which is `internal static`) would eliminate 6 lines with zero behavioural change.

---

### Finding 2: Radiation severity string (`"lethal"` / `"high"` / `"low"`) computed identically in two methods
- Lines: 419–421 (`GetEmptyTileAnnouncement`), 743–745 (`AnnounceCursorSummary`)
- Removed: ~3 (extract a 3-line helper)
- Risk: low
- Detail: Both `GetEmptyTileAnnouncement` and `AnnounceCursorSummary` contain the same ternary expression `radLevel >= 3 ? "lethal" : radLevel == 2 ? "high" : "low"`. A private static helper `GetRadiationSeverity(int level)` would remove one copy and make any future severity-wording change apply everywhere. The expressions also differ slightly in their surrounding string format (`", {severity} radiation"` vs `"In level {radLevel} {severity} radiation"`), which is intentional and not a bug.

---

### Finding 3: `SuppressInput()` omits `ShouldSuppressButtonEvents`, causing inconsistency with post-party-switch handling
- Lines: 841–845 vs 123–126
- Removed: 0 (missing flag)
- Risk: medium
- Detail: `SuppressInput()` sets only `ShouldSuppressGameInput` and `ShouldSuppressUINavigation`. After calling `SuppressInput()` at line 129, most actions return `true` without setting `ShouldSuppressButtonEvents`. The `R`, `I`, and `Escape` handlers set `ShouldSuppressButtonEvents = true` individually (lines 337, 345, 353) because they involve Enter/Escape-like actions, but the party-switch path sets it separately at line 125. This means arrow-key movement and cursor commands do not suppress `EventManager.Update()`, allowing `EventManager` to process any button-down event registered for the same frame as a cursor move. If no conflicting event exists this is harmless, but it is an asymmetry. `SuppressInput()` should set all three flags, removing the three ad-hoc per-handler assignments and the special case at line 123–126.

---

### Finding 4: `HandleInput` early-exits with `return false` if `!cursorInitialized` but does not call `SuppressInput()` first
- Lines: 119
- Removed: 0
- Risk: low
- Detail: When `cursorInitialized` is false (only on the very first frame before `OnActivated` sets it), `HandleInput` returns `false` without suppressing game input. This allows `InputManager`, `UICamera`, and `EventManager` to process keys during this brief window. `OnActivated` always sets `cursorPosition` before returning, so this path is only exposed in the single frame between `IsActive` becoming true and `OnActivated` completing. Low risk but the intent is clearly to suppress input whenever the state is active.

---

### Finding 5: Magic string `"LosAngelesWorldMap"` is an inline hardcoded scene name
- Lines: 79
- Removed: 0 (extract to constant)
- Risk: low
- Detail: `Application.loadedLevelName == "LosAngelesWorldMap"` hardcodes the scene name. Any typo or future scene rename would silently break the map detection (the state would default to "Arizona" without error). A `private const string LA_WORLD_MAP_SCENE = "LosAngelesWorldMap"` would make the intent clear and make the value searchable. The corresponding `WorldMapPatches.cs` may contain the same string; if so, both should reference the constant.

---

### Finding 6: Magic number `15f` used as NavMesh sample radius in two places
- Lines: 376 (`MoveCursor`), 583 (`MovePartyToCursor`)
- Removed: 0 (extract to constant)
- Risk: low
- Detail: `NavMesh.SamplePosition(…, 15f, 1)` appears at lines 376 and 583 with the same radius. This value is a domain constant (the NavMesh snap tolerance for world-map movement) and should be a named constant `private const float NAVMESH_SAMPLE_RADIUS = 15f` to prevent the two usages drifting apart.

---

### Finding 7: `try/catch` in `MovePartyToCursor` swallows exceptions from the radiation-path check silently except for a log message
- Lines: 595–612
- Removed: 0 (diagnostic note)
- Risk: low
- Detail: The `try/catch(System.Exception ex)` block around the `intendedPath` reflection read (lines 595–612) logs the exception message but swallows the exception. If `intendedPathField` resolution or the cast to `NavMeshPath` fails, `radiationWarning` silently stays empty and the user gets no radiation warning even when the path crosses a radiation zone. Since `intendedPathField` is a `static FieldInfo` cached across calls, a first-time failure will also suppress all subsequent radiation checks. The catch should at minimum set a flag to skip future attempts (avoid log spam) and ideally use a more specific exception type.

---

### Finding 8: `intendedPathField` is a `static FieldInfo` but the null-check initialisation is inside `MovePartyToCursor` rather than a static constructor or field initialiser
- Lines: 31, 597–598
- Removed: ~2 (move to static initialiser)
- Risk: low
- Detail: `intendedPathField` is declared `private static FieldInfo` at line 31 with no initialiser. It is lazily initialized inside `MovePartyToCursor` at lines 597–598 with a `if (intendedPathField == null)` guard. This is fine functionally but inconsistent with the pattern used by `s_mValueField` opportunities noted in GenericMenuState. A static field initialiser (`private static readonly FieldInfo intendedPathField = typeof(WorldMapParty).GetField(...)`) would make the intent explicit and remove the conditional inside the method.

---

### Finding 9: `AnnounceCursorSummary` and `InteractWithPOIAtCursor` both perform the same "find nearest visible POI" scan independently
- Lines: 748–777 (`AnnounceCursorSummary`), 651–678 (`InteractWithPOIAtCursor`)
- Removed: ~15 (extract a helper)
- Risk: low
- Detail: Both methods iterate `WorldMapInput.instance.pois` (falling back to `FindObjectsOfType`), filter by `IsVisible()`, compute `Vector2Distance` to each, and track the closest. A private helper `FindNearestVisiblePOI(Vector3 from, out float distance)` returning a `WorldMapPOI?` would remove one copy of this ~15-line pattern. The two call sites differ only in what they do with the result (announce vs interact), not how they find it.

---

### Finding 10: `HandleInput` sets `ShouldSuppressButtonEvents` redundantly for `R`, `I`, and `Escape` when `SuppressInput()` could cover all paths (see Finding 3), and the party-switch path sets it separately before `SuppressInput()` is called
- Lines: 123–126, 337, 345, 353
- Removed: ~6
- Risk: low
- Detail: Three handlers — `R` (line 337), `I` (line 345), `Escape` (line 353) — each individually set `ShouldSuppressButtonEvents = true` after their action. The party-switch path at lines 123–126 sets `ShouldSuppressGameInput` and `ShouldSuppressButtonEvents` manually because `SuppressInput()` is called at line 129 only after the party-switch check. If `SuppressInput()` were moved before the party-switch check (or the party-switch handler called `SuppressInput()` internally), all three ad-hoc flag assignments could be removed. This is tied to Finding 3.

---

### Finding 11: `AnnounceWaterCostToCursor` and `AnnounceDistanceToParty` both compute `Vector2Distance(cursorPosition, partyPos)` independently on every invocation
- Lines: 531, 563
- Removed: 0
- Risk: low
- Detail: Both methods access `WorldMapParty.instance.transform.position` and call `Vector2Distance`. These are called from user key-presses so the per-call cost is negligible. No change needed, noted for completeness.

---

### Finding 12: `OnDeactivated` is a no-op (only logs) — the comment "Don't reset cursor position - preserve it for when we return" explains intentional non-action
- Lines: 111–115
- Removed: 0
- Risk: low
- Detail: The implementation is correct and the comment is accurate. No cleanup needed, but the method body could be reduced to just the `MelonLogger.Msg` line if the comment were moved to the field declaration for `cursorPosition`. Minor readability improvement only.

---

### Finding 13: `SelectPartyMember` fetches `MonoBehaviourSingleton<Game>.GetInstance()` twice — once for the party list and once for `pcLeader`
- Lines: 805, 829
- Removed: ~1
- Risk: low
- Detail: `MonoBehaviourSingleton<Game>.GetInstance()` is called at line 805 (to get `game.party`) and again at line 829 (to get `game.pcLeader`). The result of the first call should be stored in a local and reused, eliminating one singleton lookup.

---

### Finding 14: `HandlePartySwitch` uses `KeyCode.F1 + i` integer arithmetic to iterate F1–F7
- Lines: 787–795
- Removed: 0
- Risk: low
- Detail: `KeyCode key = KeyCode.F1 + i` relies on `KeyCode` being a contiguous enum from F1 to F7. This is true in Unity's `KeyCode` enum and is a common idiom, but it is an implicit assumption about enum layout. A comment noting the assumption (`// KeyCode.F1 through F7 are contiguous in Unity's enum`) would prevent a future reader from questioning whether this is intentional. Not a bug.

---

### Finding 15: Magic number `7` in `HandlePartySwitch` — maximum party size is not explained
- Lines: 787
- Removed: 0 (extract to constant)
- Risk: low
- Detail: `for (int i = 0; i < 7; i++)` hardcodes the maximum party size as 7, matching the F1–F7 key range. A `private const int MAX_PARTY_MEMBERS = 7` would document why `7` specifically and make it easy to update if the game supports a different party limit. The same constant would clarify the `SelectPartyMember` method's `F-key` intent.

---

### Finding 16: `InteractWithPOIAtCursor` checks `closestPOI == null || closestDistance > closestPOI.instigateRadius` in a single condition — null case is already covered by the preceding loop
- Lines: 679
- Removed: 0
- Risk: low
- Detail: If all POIs are filtered by `IsVisible()` and none are visible, `closestPOI` will be null and the condition is true. This is correct. However the null-then-distance combined condition means the `if (closestPOI != null)` check inside the block (line 681) is always true when `closestPOI != null && closestDistance > instigateRadius`, and always false when `closestPOI == null`. The two cases (no POI found at all vs POI found but too far) have different user messages and could be split into two separate guards for clarity, removing the nested null check.

---

### Finding 17: `StopParty` silently does nothing if `party == null` — no announcement
- Lines: 628–629
- Removed: 0
- Risk: low
- Detail: `if (party == null) return;` at line 628 exits silently. If the party instance is not available (which would be unusual given `IsActive` guards on `WorldMapParty.instance != null`), the user hears nothing and may think the key press was ignored or Backspace did nothing. Consistent with other methods in the file that speak "Party not found" on null, this guard should announce "Party not available" before returning.

---

### Finding 18: `MoveCursor` stores the result in `lastAnnouncement` for both proximity and empty-tile paths but `JumpToParty` and `JumpToSelectedPOI` do not always set `lastAnnouncement`
- Lines: 437–453 (`JumpToParty`), 455–482 (`JumpToSelectedPOI`)
- Removed: 0
- Risk: low
- Detail: `JumpToParty` calls `ScreenReaderManager.SpeakInterrupt("Cursor at party position")` but never writes `lastAnnouncement`. If the user immediately presses Backslash to repeat, `WorldMapNavigationManager.RepeatLastAnnouncement()` is called (because `lastAnnouncement` is empty or stale) rather than repeating the jump confirmation. `JumpToParty` should set `lastAnnouncement` before speaking, matching the pattern used by `AnnounceDistanceToSelectedPOI`, `AnnounceWater`, and `MovePartyToCursor`.

---

### Finding 19: `OnActivated` computes `mapChanged` but does not log the map name change clearly; the log line duplicates `cursorInitialized` which may be confusing on first call
- Lines: 108
- Removed: 0
- Risk: low
- Detail: `MelonLogger.Msg($"[WorldMapState] Activated on {mapName}, cursorInitialized={cursorInitialized}, mapChanged={mapChanged}")` logs `cursorInitialized=True` and `mapChanged=False` on a menu-return even when cursor has been initialized. This is correct but the log is slightly misleading because `cursorInitialized` is set to `true` at line 91 on the first-call path and is already true on returns. The log message is benign but could be improved to distinguish first-activation from return-from-menu more clearly.

---

### Finding 20: `shiftHeld` is computed at line 194 after the Shift-key block (lines 134–190) which already handled all Shift key cases and returned; the variable is only used at line 196 and line 358
- Lines: 194
- Removed: 0
- Risk: low
- Detail: After the `if (Input.GetKey(LeftShift) || Input.GetKey(RightShift))` block at lines 134–190 (which `return true` for all its cases), `shiftHeld` at line 194 will be false whenever execution reaches it — because if Shift were held, the block above would have already returned. The assignment is harmless but misleading; `shiftHeld` could be replaced with `false` in the two guards at line 196 and line 358, or the logic could note that shift-held is implicitly false at that point. Current code reads as if shift might still be held, which it cannot be given the prior return paths.
