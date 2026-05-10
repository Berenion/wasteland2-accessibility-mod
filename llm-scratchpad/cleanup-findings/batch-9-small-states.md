# Cleanup findings — small States

Files reviewed:
- `States/DialogState.cs` (791 lines)
- `States/ScannerState.cs` (719 lines)
- `States/ConversationState.cs` (481 lines)
- `States/MainMenuState.cs` (399 lines)
- `States/KeypadState.cs` (154 lines)

---

## States/DialogState.cs

### Finding 1: AskQuantityMenu reflection fields re-acquired on every call site (no cache)
- Lines: 151–152, 558–559, 569–570, 732–733, 745–746, 753, 775
- Removed: ~10 (7 `GetField` calls collapse to 5 one-time `static readonly` field declarations; local var lines disappear)
- Risk: low
- Detail: `typeof(AskQuantityMenu).GetField("minValue", ...)`, `"maxValue"`, `"unitValue"`, and `"quantity"` are looked up via reflection on every key press and on every `AnnounceDialog()` call. `KeypadState` and `ConversationState` already use `static readonly FieldInfo` for the same pattern. Promoting these four to `private static readonly FieldInfo` at the class level (next to `currentDifficultyMenu`) eliminates all repeated `GetField` calls and matches the project convention.

### Finding 2: DifficultySelectionMenu `gameDifficultyIndex` reflection field also uncached
- Lines: 298
- Removed: ~2 (local `var field` replaced by a static field reference)
- Risk: low
- Detail: `typeof(DifficultySelectionMenu).GetField("gameDifficultyIndex", ...)` is created as a local variable inside `RefreshButtons()`, which runs every frame while a DifficultySelectionMenu is open. Should become a `private static readonly FieldInfo` alongside the AskQuantityMenu fields.

### Finding 3: `difficultyIndex < 3` / `difficultyIndex > 0` are magic-number bounds for a 4-entry array
- Lines: 62, 75
- Removed: 0 (change constants, not line count)
- Risk: low
- Detail: The upper bound `3` corresponds to `DifficultyNames.Length - 1`. The existing array `DifficultyNames` has 4 entries. Expressing the bounds as `difficultyIndex < DifficultyNames.Length - 1` and `difficultyIndex > 0` removes the dependency on the silent assumption that there are exactly 4 difficulties, making a future name-list change self-consistent.

### Finding 4: `HandleInput` for `AskQuantityMenu` — `End` key re-fetches `maxValue` via GetField inline
- Lines: 151–152
- Removed: 2 (after the cache is added in Finding 1, this becomes a single field read)
- Risk: low
- Detail: The `Home` key calls `SetQuantity(1)` directly. The `End` key performs an ad-hoc `GetField("maxValue")` inline (lines 151–152) rather than using `AdjustQuantity` or the cached field. Once the cache is in place this is a one-liner. Also, the fallback default `999` here is inconsistent with `AdjustQuantity` (which also uses `999`) — both would disappear once the field is always resolved through the cache.

### Finding 5: `SetQuantity` re-reads `minValue` and `maxValue` even though `AdjustQuantity` already clamped the value before calling it
- Lines: 745–748, 750
- Removed: ~4
- Risk: low
- Detail: `AdjustQuantity` clamps `newValue` before passing it to `SetQuantity` (line 738), so the second clamp inside `SetQuantity` (lines 745–750) is redundant when the path is `AdjustQuantity → SetQuantity`. However `SetQuantity` is also called directly from `OnEnd` (line 153) without prior clamping, so the clamp itself must remain — but the repeated `GetField` pair can be eliminated once the static cache is in place (Finding 1).

### Finding 6: `AnnounceDialog` repeats the same `FindObjectOfType` calls already made by `RefreshButtons` that ran immediately before it
- Lines: 524, 615, 637, 663 (in `AnnounceDialog`) vs. 289, 363, 423 (in `RefreshButtons`)
- Removed: 0 (structural; no lines removed without refactor)
- Risk: low
- Detail: Every call to `AnnounceDialog()` is immediately preceded by `RefreshButtons()`, which already resolved and stored the active menu type in `currentDifficultyMenu`, `currentQuantityMenu`, `buttons`, and `currentDialogId`. `AnnounceDialog` then runs four separate `FindObjectOfType` calls to rediscover the same objects. Passing the resolved object (or a dialog-type enum) from `RefreshButtons` to `AnnounceDialog` would eliminate 4–6 `FindObjectOfType` calls per dialog open event. Marked low-risk because it is purely a performance improvement with no behavioral change, but it is non-trivial to implement — consider a private "current dialog type" field set in `RefreshButtons`.

### Finding 7: `IsTutorialOpen()` contains an unconditional `return false` that is never reached
- Lines: 260–264
- Removed: 2
- Risk: low
- Detail: The method returns `true` inside the `if` block at line 261 and falls off the end of the block. The explicit `return false` at line 264 is dead code — the compiler generates the same fall-through. The simplified form is `return tutScreen.popup != null && tutScreen.popup.gameObject.activeInHierarchy;` (one line).

### Finding 8: `IsTutorialOpen()` re-fetches the singleton a second time in both `RefreshButtons` and `AnnounceDialog` after already checking it
- Lines: 392–394 (`RefreshButtons`), 663–665 (`AnnounceDialog`)
- Removed: 0 (structural)
- Risk: low
- Detail: Each of the two call sites that check `IsTutorialOpen()` then immediately calls `MonoBehaviourSingleton<TutorialScreen>.GetInstance()` again. `IsTutorialOpen()` itself already calls `GetInstance()` internally. A minor improvement is to have `IsTutorialOpen()` return the instance (or null) rather than bool, letting callers reuse it — but this is a small consistency fix.

### Finding 9: Comment "Harmony patch announces the result" at line 66 is misleading about when the patch fires
- Lines: 66
- Removed: 0
- Risk: low
- Detail: The comment implies the Harmony patch on `SelectDifficulty` always announces the difficulty change. However, when `difficultyIndex` is already 0 (leftmost) and Left is pressed, `SelectDifficulty` is not called (the `if (difficultyIndex > 0)` guard prevents it) but the suppressor is still set and `true` is returned. The comment should either be removed or clarified to say "…when the difficulty actually changes".

### Finding 10: `OnActivated` calls `RefreshButtons()` result but discards the return value, then unconditionally calls `AnnounceDialog()` — dialog-changed flag is thrown away
- Lines: 221–222
- Removed: 0 (no lines removed)
- Risk: low
- Detail: `OnActivated` calls `RefreshButtons()` without using the bool return. `HandleInput` correctly uses `dialogChanged` to gate `AnnounceDialog()`, but `OnActivated` calls `AnnounceDialog()` unconditionally anyway. The return value being unused here is harmless (the first activation always wants to announce), but it would be cleaner to document why — or use `_ = RefreshButtons()` explicitly to signal the intent.

### Finding 11: `ActivateButton` swallows `Exception` after logging only `ex.Message` (no stack trace)
- Lines: 715–722
- Removed: 0
- Risk: low
- Detail: The catch block logs `ex.Message` but not `ex.StackTrace` or the full `ex.ToString()`. If a button action throws a NullReferenceException, the message alone ("Object reference not set to an instance of an object") gives no location information. `MelonLogger.Error(ex.ToString())` is the consistent pattern used by other states.

---

## States/ScannerState.cs

### Finding 1: `IsVisibleThroughFOW(Vector3)` wrapper method is a one-line passthrough with no added value
- Lines: 175–178
- Removed: 4 (the wrapper method body + signature; all 6 call sites become `FOWHelper.IsVisibleThroughFOW(...)`)
- Risk: low
- Detail: The private method exists only to wrap `FOWHelper.IsVisibleThroughFOW(position)`. Every other state and helper in the codebase calls `FOWHelper.IsVisibleThroughFOW` directly (confirmed by grep: `MapCursorState`, `NavigationManager`, patches). The wrapper adds a stack frame and a layer of indirection with no documentation value. Removing it and inlining the call at the 6 usage sites is safe.

### Finding 2: `ScanForEnemies` and `ScanForNPCs` both call `FindObjectsOfType<Mob>()` and iterate the full list independently
- Lines: 182–226 (`ScanForEnemies`), 228–261 (`ScanForNPCs`)
- Removed: ~30 (merge into one pass; eliminate one `FindObjectsOfType` call and shared prologue)
- Risk: low
- Detail: Both methods call `UnityEngine.Object.FindObjectsOfType<Mob>()` at the top and then apply the same guards (`null`, `activeInHierarchy`, `is PC`, `IsVisibleThroughFOW`, distance). They differ only in how they classify the NPC's faction. A single combined pass over `allMobs` would cut the object enumeration from 2× to 1× per scan and reduce code duplication. In practice `FindObjectsOfType` returns the same list both times, so the duplication has a real per-scan cost on busy maps.

### Finding 3: `CycleScanRange` uses a `switch` over three known values when a lookup array would be cleaner and consistent with `CATEGORY_NAMES`
- Lines: 675–686
- Removed: ~6
- Risk: low
- Detail: The switch sets `currentScanRange` to one of the three constants. An array `private static readonly float[] SCAN_RANGES = { SHORT_RANGE, MEDIUM_RANGE, LONG_RANGE }` makes the range table explicit and reduces the switch to `currentScanRange = SCAN_RANGES[rangeIndex]`. This is also consistent with how `CATEGORY_NAMES` is indexed by `currentCategory`.

### Finding 4: `AnnounceScanSummary` builds `categoryCounts` dictionary by initialising all keys to 0, then iterates all results — a LINQ `GroupBy` or direct indexing would be shorter, but more concretely: the initial `foreach` over `CATEGORY_NAMES` to pre-fill zeros is unnecessary because the subsequent count access is always via `categoryCounts[cat]` which was pre-filled
- Lines: 495–507
- Removed: ~5 (the pre-fill loop and `ContainsKey` guard are redundant)
- Risk: low
- Detail: The dictionary is pre-filled with all category names at 0 (lines 496–499). The subsequent loop (lines 501–507) checks `ContainsKey` before incrementing — but since all keys are pre-filled, this check is always true and can be dropped. The pre-fill loop itself can be merged with the summary output loop. This saves one full pass over `CATEGORY_NAMES`.

### Finding 5: The `5` in `AnnounceCurrentCategory` ("Announce up to first 5 items") is a magic number
- Lines: 611, 618
- Removed: 0
- Risk: low
- Detail: The value `5` controls how many individual scan results are read aloud before the "and N more" tail. It should be a named constant, e.g. `private const int MAX_ANNOUNCE_ITEMS = 5`, so it can be adjusted in one place. The value `5` appears twice (as `Math.Min(5, ...)` and `results.Count > 5`).

### Finding 6: `GetResultsForCategory` is called twice in rapid succession in `CycleCategoryForward`, `CycleCategoryBackward`, and `AnnounceCurrentCategory`
- Lines: 571–581 (`CycleCategoryForward`), 583–594 (`CycleCategoryBackward`), 596–623 (`AnnounceCurrentCategory`)
- Removed: ~6 (avoid the double-call in cycle methods)
- Risk: low
- Detail: `CycleCategoryForward` calls `GetResultsForCategory(CATEGORY_NAMES[currentCategory]).Count == 0` in its `while` condition, then calls `AnnounceCurrentCategory()` which calls `GetResultsForCategory` again for the same category. Each call allocates and sorts a new `List<ScanResult>`. Passing the already-retrieved list to `AnnounceCurrentCategory` (or having the cycle methods return the list) would halve the allocations. The same pattern applies to `CycleCategoryBackward` and `SelectClosestInCategory`.

### Finding 7: `GetInteractableName` has an unreachable branch when `interactable == null`
- Lines: 462–463
- Removed: 2
- Risk: low
- Detail: `GetInteractableName` is only called from `ScanForInteractables`, where the `interactable` loop variable has already been checked for `null` at line 421 (`if (interactable == null || !interactable.isVisible) continue`). The null guard at line 462–463 is therefore dead. Removing it or replacing with a Debug.Assert is appropriate.

### Finding 8: `OnDeactivated` clears `lastScanResults` but `PerformScan` unconditionally calls `lastScanResults.Clear()` at the top anyway
- Lines: 136–137, 151
- Removed: 1 (`lastScanResults.Clear()` in `OnDeactivated`)
- Risk: low
- Detail: `PerformScan` (called when scan begins) always clears `lastScanResults` at line 151. `OnDeactivated` also clears it at line 137. Since the state is deactivated only after scan mode ends, and `PerformScan` clears on every fresh scan, the clear in `OnDeactivated` is defensive at best. It is harmless but creates a false impression that the list needs clearing on deactivation.

---

## States/ConversationState.cs

### Finding 1: `GetButtonCount()` calls `EnsureFieldsCached()` and then reads the HUD singleton — the same work that `RefreshOptions()` does moments later on the same frame
- Lines: 250–260, 262–349
- Removed: 0 (structural observation)
- Risk: low
- Detail: `IsInInputMode` (called from `IsActive` and repeatedly from `HandleInput`) calls `GetButtonCount()`, which reflects and reads the HUD's `buttonList`. When `HandleInput` then calls `RefreshOptions()` (line 129), it repeats the singleton fetch and field read. This is at most a micro-optimisation note — the calls are cheap — but the duplication is worth flagging: `GetButtonCount` could be eliminated by having `IsInInputMode` use `currentOptions.Count` if options are kept live, or by caching the count.

### Finding 2: `fieldsCached` bool is redundant — null-check on `buttonListField` is sufficient
- Lines: 36, 237, 243, 253, 267
- Removed: ~4
- Risk: low
- Detail: `EnsureFieldsCached` uses a separate `fieldsCached` bool flag to avoid re-running on subsequent calls. The same goal is achieved by checking `if (buttonListField != null) return;` — if the field was already successfully resolved, the null check short-circuits. If it failed (field is null), the bool currently causes the code to skip silently on every subsequent call, hiding the failure. The bool can be replaced by a lazily-set field: attempt once, log on failure, and check the result field directly everywhere.

### Finding 3: `SpeakOptionAfterVoiceover` coroutine checks `announcementGeneration != generation` three times but the pattern is slightly asymmetric
- Lines: 409, 415, 417
- Removed: 0
- Risk: low
- Detail: The generation check at line 409 (inside the poll loop) and at line 415 (after the loop exits) are correct. The check at line 417 (after the 0.3 s post-voiceover delay) is also correct. However, the three checks could be consolidated into a local helper or moved to a single guard at the start of each `yield return` line to be symmetric. This is a style/readability note only.

### Finding 4: `BuildOptionAnnouncement` always appends `", N of total"` even when there is only one option
- Lines: 445
- Removed: 0 (conditional would add a line)
- Risk: low
- Detail: When `currentOptions.Count == 1`, the announcement ends with `, 1 of 1`, which is audibly redundant for a screen reader user. Other announcement methods in the codebase (`AnnounceButton` in `DialogState` lines 695–697, `AnnounceButton` in `MainMenuState` lines 334–337) conditionally suppress the position when count ≤ 1. Applying the same guard here would reduce noise.

### Finding 5: `OnActivated` sets `selectedIndex = 0` unconditionally, then `RefreshOptions()` may clamp it anyway
- Lines: 202, 341–347
- Removed: 0
- Risk: low
- Detail: `OnActivated` sets `selectedIndex = 0` before calling `RefreshOptions()`. `RefreshOptions()` then clamps `selectedIndex` at the end (lines 341–347): if `Count == 0`, it stays at -1; if `Count > 0`, it stays at 0 (already in range). The pre-set to 0 in `OnActivated` is therefore harmless but masks the fact that `RefreshOptions` is the authoritative clamper. Setting to -1 in `OnActivated` (like `OnDeactivated` does) and letting `RefreshOptions` set the value would be more consistent.

---

## States/MainMenuState.cs

### Finding 1: `NavigateUp` and `NavigateDown` are near-identical; their loop bodies differ only in the direction of index increment/decrement
- Lines: 231–259 (`NavigateUp`), 261–289 (`NavigateDown`)
- Removed: ~20
- Risk: low
- Detail: Both methods declare `startIndex`/`newIndex`, run a `do/while` that steps one position until an enabled button is found or wrap-around occurs, and then call `SetSelection` + `AnnounceButton`. The only differences are `newIndex--` vs. `newIndex++` and the wrap condition `newIndex < 0 ? Count-1 : 0`. A single private `Navigate(int direction)` helper parameterised on `+1` or `-1` would eliminate ~20 lines of identical logic.

### Finding 2: `RebuildButtonList` has six nearly-identical `ButtonEntry` add blocks, each differing only in which fields of `MainMenu` are used
- Lines: 157–228
- Removed: ~35 (extract an `AddButton` helper)
- Risk: low
- Detail: Each of the six button-add blocks follows the same structure: guard on `activeInHierarchy`, construct `ButtonEntry` with `gameObject`/`label`/`button`/`name`. A helper `void AddIfActive(GameObject go, UILabel lbl, UIButton btn, string fallback)` would reduce the six blocks to six one-liners. The Credits and Exit entries have small quirks (different source types for `button`), but these are resolved before the call.

### Finding 3: `HandleInput` calls `CheckForExternalSelectionChange()` only when no key was pressed, but the comment says it "always runs"
- Lines: 84–87
- Removed: 0
- Risk: low
- Detail: `CheckForExternalSelectionChange()` is called at line 85 only in the fall-through branch after key checks fail (i.e. only when no recognised key was pressed on that frame). The index comment "// Check for external selection changes (mouse, etc.)" plus the CLAUDE.md note that it "always runs" are both inaccurate — the call is inside `HandleInput`'s final `return false` path, so it does not run on frames where the user presses Up/Down/Enter. Functionally this is not a bug (mouse navigation is still detected within a frame), but the comment is misleading.

### Finding 4: `OnActivated` comment says "after a brief delay" but there is no delay
- Lines: 131–135
- Removed: 0
- Risk: low
- Detail: The comment on line 131 reads "// Announce current selection after a brief delay". There is no `WaitForSeconds`, `MelonCoroutines`, or timer — `AnnounceButton` is called synchronously on the next line. The comment is a stale copy from an earlier version and should be removed.

### Finding 5: `FindCurrentSelectionIndex` is called once in `OnActivated` and once inside `CheckForExternalSelectionChange` — in the latter the result is immediately acted on, but the method duplicates a `UICamera.selectedObject` read with `CheckForExternalSelectionChange`
- Lines: 342–356, 358–373
- Removed: 0 (structural)
- Risk: low
- Detail: `CheckForExternalSelectionChange` reads `UICamera.selectedObject` at line 360 to compare with `lastSelectedObject`, then calls `FindCurrentSelectionIndex()` which reads `UICamera.selectedObject` again at line 344. The double read is harmless but could be eliminated by passing `current` as a parameter to `FindCurrentSelectionIndex`.

### Finding 6: `AnnounceButton` recomputes `enabledCount` and `positionInEnabled` on every navigation move by iterating all buttons
- Lines: 319–332
- Removed: 0 (structural)
- Risk: low
- Detail: With at most 6 menu buttons this is negligible, but the position counter loop is recalculated every time a button is announced (on every Up/Down press and on every external selection change). Caching `enabledCount` when `RebuildButtonList` runs would make this a single read.

---

## States/KeypadState.cs

### Finding 1: `Backspace` speaks "Empty" in two different branches (lines 137 and 149)
- Lines: 135–138, 148–149
- Removed: 2
- Risk: low
- Detail: When `current.Length == 0` at entry, the method speaks "Empty" and returns (line 137). After trimming, when `trimmed.Length == 0`, it speaks "Empty" again (line 149). These two paths are correct and serve the same purpose, but the first branch is an early exit for "already empty" while the second is "just became empty". The duplication is intentional but the first case could be collapsed into the general path: trim the string (which would produce "" for an already-empty input), then speak accordingly — eliminating the early-return and reducing to one announcement site. Risk is low but care is needed not to write the empty string back to the field when it was already empty.

### Finding 2: `static readonly FieldInfo currentValueField` and `addToValueMethod` will be `null` silently if reflection fails, but `EnterDigit` guards on them inconsistently
- Lines: 23–26, 113–127
- Removed: 0
- Risk: low
- Detail: `currentValueField` is used in both `EnterDigit` (line 114) and `Backspace` (line 132). In `EnterDigit`, a null `currentValueField` causes the length check to be skipped (defaults to empty string), so `addToValueMethod.Invoke` still fires — allowing an entry past the 8-digit limit. In `Backspace`, a null `currentValueField` causes an early return (line 132, combined guard). The inconsistency means a reflection failure could cause the 8-digit cap to not be enforced. A startup-time null check (in `OnActivated` or a static constructor) logging a warning would surface this early rather than silently misbehaving at runtime.

### Finding 3: `OnActivated` finds `KeypadMenu` via `FindObjectOfType`, redundant with `IsActive` which does the same
- Lines: 29–35 (`IsActive`), 96–98 (`OnActivated`)
- Removed: 0 (structural)
- Risk: low
- Detail: The `InputRouter` calls `IsActive` immediately before calling `OnActivated`. `IsActive` calls `FindObjectOfType<KeypadMenu>()`. `OnActivated` calls it again to cache the result. Architecturally, if `InputRouter` passed the active-check result to `OnActivated` the second `FindObjectOfType` could be avoided — but this is a framework-level pattern shared by all states, not specific to `KeypadState`. As a local fix, `OnActivated` could simply call `HandleInput`'s re-resolution path (lines 39–42) which is already guarded, rather than calling `FindObjectOfType` a second time.

### Finding 4: Magic number `8` (maximum passcode length) appears in two places with no named constant
- Lines: 117, and implicitly in the instruction string on line 100
- Removed: 0 (extract constant)
- Risk: low
- Detail: `current.Length >= 8` at line 117 uses the literal `8`. The instruction string at line 100 does not mention the maximum length to the user (it says "Type digits, Backspace to delete…" — so there is no inconsistency, but the `8` in code has no corresponding `private const int MAX_PASSCODE_LENGTH = 8`). If the game ever uses a different length passcode the single constant would be the place to change it.

---

## Cross-file findings

### Finding X1: `ShouldSuppressUINavigation = true` + `ShouldSuppressGameInput = true` + `return true` triplet repeated ~14 times in DialogState
- Files: `States/DialogState.cs` lines 68–70, 80–82, 91–93, 101–103, 112–114, 128–130, 134–136, 144–146, 163–167, 172–176, 186–190, 195–199, 205–208
- Removed: ~26 (two lines per site if extracted to a helper)
- Risk: low
- Detail: Every handled key in `HandleInput` ends with the same two suppressor assignments followed by `return true`. A private helper `bool SuppressAndReturn()` (sets both flags, returns true) would shrink each block by two lines and make the pattern explicit. This pattern also appears in `ConversationState` and `KeypadState` but less densely.

### Finding X2: `ScanForEnemies` and `ScanForNPCs` share identical mob-filtering prologues (duplicate `FindObjectsOfType`)
- Files: `States/ScannerState.cs` lines 182–192, 228–240
- Removed: ~15 (merge into one pass — see ScannerState Finding 2 above)
- Risk: low
- Detail: Cross-listed for visibility: this is the highest line-count reduction opportunity across all five files. Merging the two Mob scan passes into one reduces `FindObjectsOfType<Mob>()` from 2 calls to 1 per scan, and removes ~30 duplicate lines.
