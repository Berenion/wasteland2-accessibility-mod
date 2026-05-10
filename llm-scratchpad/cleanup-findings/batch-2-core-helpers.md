# Cleanup findings — Core + Helpers

## Core/CameraLock.cs

### Finding 1: `initialized` field and `IsInitialized` property are redundant
- Lines: 22, 26, 90, 104
- Removed: ~5
- Risk: low
- Detail: `initialized` is set once in `Initialize()` and only checked in the Harmony postfix guard `!CameraLock.IsInitialized`. `Initialize()` is called exactly once at startup. The guard could instead check a static flag that is inherently always true once the class runs — or just remove the guard entirely since `IsLocked` is `true` by default and the postfix would harmlessly run zero frames before `Initialize()`. Removing the field, property, and guard eliminates 5 lines with no behaviour change.

### Finding 2: `lockedYRotation` backing field exposed via property when readonly access suffices
- Lines: 21, 88
- Removed: ~3
- Risk: low
- Detail: `lockedYRotation` is `private static float` and exposed via `public static float LockedYRotation => lockedYRotation;`. The only external consumer is the Harmony postfix (same file) which already uses the property. The backing field could be replaced with an `auto-property` or the property could simply be `{ get; private set; }`, removing the separate field declaration and keeping the class cleaner.

### Finding 3: `ResetToNorth` duplicates the "set euler to (x, 0, z)" logic already in the Harmony postfix
- Lines: 69-83 vs 107-115
- Removed: 0 (note only)
- Risk: low
- Detail: `ResetToNorth` manually sets the transform rotation to Y=0 and separately sets `lockedYRotation = 0f`. The postfix enforces the locked rotation every frame. If `ResetToNorth` just sets `lockedYRotation = 0f` and the postfix does the rest, the explicit transform mutation in `ResetToNorth` is unnecessary (happens next frame anyway). Low impact but worth noting for a future pass.

---

## Core/CutsceneDetector.cs

### Finding 4: `EnsureSubscribed` called on every `IsActive` access — no deferred concern
- Lines: 22-27
- Removed: 0 (note only)
- Risk: low
- Detail: `IsActive` is polled every frame by `InputRouter.ProcessInput`. Every frame it calls `EnsureSubscribed`, which checks `if (subscribed) return;` after the first successful call — so steady-state cost is one bool check. No bug, but the try/catch inside `EnsureSubscribed` swallows failures silently after the first attempt; if `EventManager` is not yet available, `subscribed` stays false and the subscription attempt repeats every frame until it succeeds, which is the intended behaviour. Only concern: if the subscription permanently fails (e.g. API changed), no follow-up error is logged. Low risk.

---

## Core/ExplorationState.cs

### Finding 5: `found` variable set but never read — dead write
- Lines: 377, 410
- Removed: ~3
- Risk: low
- Detail: Inside `TryUseItemOnObject`, the inner loop sets `bool found = false;` and sets `found = true;` inside the matching branch (lines 377, 398 area), but then immediately `return true` before `found` is ever tested. The `if (!found)` check at line 410 is the only read, but it is only reached when the loop completes without returning — meaning the matching branch was never hit and `found` is always still `false` at that point. The variable can be removed; the `if (!found)` becomes unconditional after the loop.

### Finding 6: `StopPartyMovement` — `MelonLogger.Msg` inside hot path with string formatting every call
- Lines: 219
- Removed: 0 (note only)
- Risk: low
- Detail: The `MelonLogger.Msg` call only executes when `anyStopped == true`, so it is not truly hot. No action needed; just confirming it is not a per-frame log.

### Finding 7: Suppressor flag set redundantly in multiple branches of `HandleInput`
- Lines: 122, 137, 145, 153, 161, 169, 176
- Removed: 0 (note only)
- Risk: low
- Detail: `InputSuppressor.ShouldSuppressButtonEvents = true` is set individually in six separate `if` blocks. This is intentional (only specific keys suppress events), so no refactor is needed, but grouping them into one flag-set at the end would save repeated lines. Medium complexity refactor; risk is accidentally suppressing button events for keys that don't need it.

### Finding 8: Catch-and-log without rethrowing in `AnnouncePartyScrap` and `ToggleGroupMode`
- Lines: 449-453, 469-471
- Removed: 0 (note only)
- Risk: low
- Detail: Both methods catch `System.Exception ex` and log it, which is consistent with the rest of the file. The pattern is intentional (single-frame input handlers). Consistent, no change needed.

---

## Core/IAccessibilityState.cs

### Finding 9: Comment in `Priority` doc-comment lists stale/incorrect values
- Lines: 17-19
- Removed: 0 (note only)
- Risk: low
- Detail: The summary comment lists "Conversation/Inventory/Character=50" but `CharacterInfoState` and `ConversationState` may have different priorities. The interface doc-comment is the only place these numbers are centralized. Keeping it accurate prevents confusion; no code change.

---

## Core/InputRouter.cs

### Finding 10: `MarkKeyConsumed` / `WasKeyConsumed` / `consumedKeys` are dead infrastructure
- Lines: 21, 94-105
- Removed: ~12
- Risk: low
- Detail: `MarkKeyConsumed` and `WasKeyConsumed` are defined but never called from anywhere in the codebase (confirmed by grep). The `consumedKeys` HashSet is populated only by `MarkKeyConsumed`, cleared in `ProcessInput`, and queried only by `WasKeyConsumed`. All three can be removed without any behaviour change.

### Finding 11: `IsAnyMenuStateActive` is dead infrastructure
- Lines: 126-134
- Removed: ~9
- Risk: low
- Detail: `IsAnyMenuStateActive()` (which filters `Priority >= 30`) has no callers outside its own file. `IsAnyStateActive()` is called in two patch files; `IsAnyMenuStateActive` is not called anywhere. Safe to remove.

### Finding 12: `InputConsumedThisFrame` is public but only read internally
- Lines: 18
- Removed: 0 (note only)
- Risk: low
- Detail: `InputConsumedThisFrame` is public but no external file reads it (grep confirms only the InputRouter itself sets/reads it). Could be made `private`, though keeping it public for debugging is harmless.

---

## Core/InputSuppressor.cs

### Finding 13: `SaveLoadScreen_OnSaveClicked_Suppressor` and `SaveLoadScreen_OnLoadClicked_Suppressor` have identical structure
- Lines: 127-164
- Removed: ~12
- Risk: medium
- Detail: The two patch classes are byte-for-byte identical except for the patched method name (`OnSaveClicked` vs `OnLoadClicked`). They could be collapsed into a single helper method `private static bool AllowOrBlock()` called by both `Prefix` bodies. Saves ~12 lines; the risk is that Harmony patches must be separate classes so the outer structure stays, but the Prefix body can share a helper.

### Finding 14: `SaveLoadScreen_OnButtonDown_Suppressor` uses a string literal button name
- Lines: 173
- Removed: 0 (note only)
- Risk: low
- Detail: `"Attack Current Target"` and `"Controller A"` are raw strings. These should ideally be named constants, but they come from the game's cInput button-name table, so they are effectively magic strings. Low priority; no defect.

---

## Core/TacticalPauseManager.cs

### Finding 15: `ForceResumeIfPaused` skips the `Resume()` call when `Game` instance is absent
- Lines: 63-71
- Removed: 0 (note only)
- Risk: low
- Detail: If `MonoBehaviourSingleton<Game>.HasInstance()` is false, `IsPaused` is still set to `false` even though `game.Resume()` was not called. This is a latent state desync: the game's `pauseCounter` would not be decremented. The same concern could theoretically arise in `Resume()` (lines 49-55). Worth a comment at minimum. The `Pause()` guard at line 33 (`game.IsPaused()`) reduces but does not eliminate the risk.

---

## Helpers/CharacterAnnouncementHelper.cs

### Finding 16: `inCC` detection duplicated in `GetSkillState` and `GetTraitAvailabilityState`
- Lines: 282-283 and 363-364
- Removed: ~4
- Risk: low
- Detail: Both methods compute `bool inCC = MonoBehaviourSingleton<HUD_Controller>.HasInstance() == false && MonoBehaviourSingleton<HUD_WorldMapController>.HasInstance() == false;` identically. Extracting this into a `private static bool IsInCharacterCreation()` helper removes the duplication and makes the intent explicit. Saves ~4 lines and improves readability.

### Finding 17: Empty catch blocks in `GetAttributeMaxValue`, `GetAttributeBuffState`, `GetSkillCap`, `GetSkillState`, `GetTraitAvailabilityState` swallow exceptions silently
- Lines: 223, 246, 264, 291, 390
- Removed: 0 (note only)
- Risk: low
- Detail: Five private helper methods have bare `catch { }` or `catch { return ...; }` blocks with no logging. On failures they silently return empty/zero. This is intentional for resilience (UI never crashes), but makes debugging hard. At minimum a `MelonLogger.Warning` in a debug build would help. Consistent with the rest of the file's pattern.

### Finding 18: `GetControlAnnouncement` button fallback duplicates the "strip Button from name" logic
- Lines: 422-424
- Removed: 0 (note only)
- Risk: low
- Detail: `.Replace("Button", "").Replace("button", "").Trim()` is a micro-pattern that appears only here. Not worth extracting, but noting as a minor readability smell.

### Finding 19: `DerivedStatNames` array has 11 entries but the field comment says "10 derived stats"
- Lines: 53-66
- Removed: 0 (note only)
- Risk: low
- Detail: The doc-comment says "The 10 derived stat names" but the array initializer has 11 elements (the last being `PCStatsManager.conPerLevel`). The comment is stale. One-line fix: change "10" to "11".

---

## Helpers/CharacterAnnouncementHelper.StatDescriptions.cs

### Finding 20: `AnnounceStatDescription` is a back-compat wrapper with no callers
- Lines: 134-143
- Removed: ~10
- Risk: low
- Detail: `AnnounceStatDescription(GameObject obj)` is marked "Back-compat: dump the description as one spoken string. New code should prefer BuildStatDescriptionLines + the info browser." Grep confirms it has no callers anywhere in the codebase. Safe to remove.

### Finding 21: `GetTraitDescription` re-fetches the trait via reflection when `GetTraitFromEditor` already does the same
- Lines: 369-400 vs 362-367
- Removed: ~5
- Risk: low
- Detail: `GetTraitDescription` calls `traitEditorTraitField.GetValue(editor)` directly (line 375), which is exactly what `GetTraitFromEditor` (line 362) does. The method should call `GetTraitFromEditor(editor)` instead of repeating the reflection read. Reduces duplication by ~3 lines.

### Finding 22: `BuildTraitDescription` is a one-line wrapper that could be inlined
- Lines: 407-411
- Removed: ~4
- Risk: low
- Detail: `BuildTraitDescription(Trait trait)` simply calls `BuildTraitDescriptionLines(trait)` and joins with ". ". Its only callers are `GetTraitDescription` (same file) and indirectly via `CharacterState.GetTraitDescription`. Inlining is low-risk and saves the wrapper, but keeping it for readability is also fine.

### Finding 23: `FindAnyDescriptionPanel` tries five different lookup paths sequentially
- Lines: 147-187
- Removed: 0 (note only)
- Risk: low
- Detail: The method is called from `BuildNextLevelPreview` and `BuildStatPerksUnlocked`, both of which are already inside `BuildStatDescriptionLines`. `FindAnyDescriptionPanel` is called twice per `BuildStatDescriptionLines` invocation. The result could be computed once and passed down, saving one extra scene-scan per call. Low impact given how rarely this runs.

### Finding 24: `BuildNextLevelPreview` does `while (clean.Contains(", , "))` loop-replace
- Lines: 221
- Removed: 0 (note only)
- Risk: low
- Detail: The loop replaces `", , "` repeatedly to collapse consecutive empty segments from the game's own string. This is fragile if the game ever produces `",  ,"`. A regex or `Split`+`Join` would be cleaner. Risk is low — the input is game text, not user input.

---

## Helpers/CharacterAnnouncementHelper.Snapshots.cs

### Finding 25: `GetFirstSelectedPC()` pattern repeated six times across the partials without a helper
- Lines: CharacterAnnouncementHelper.cs:237,369; StatDescriptions.cs:241,296,340,446
- Removed: ~12
- Risk: low
- Detail: The pattern `MonoBehaviourSingleton<Game>.HasInstance() ? MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC() : null` appears six times across the partial files. A `private static PC GetSelectedPC()` helper would remove the repetition. The helper already exists implicitly in `GetAttributeBuffState` and `GetTraitAvailabilityState` — factoring it out once at the top of the main partial saves ~12 lines overall.

### Finding 26: `BuildPointsAvailableHint` pluralization pattern repeated inline
- Lines: 262-270
- Removed: 0 (note only)
- Risk: low
- Detail: The `(count == 1 ? "" : "s")` pluralization idiom appears three times inside `BuildPointsAvailableHint` for attribute/skill/perk points, and again three times in `BuildHeaderSnapshotLines` (lines 223-227). Extracting `static string Plural(int n, string noun)` → `$"{n} {noun}{(n == 1 ? "" : "s")}"` would shrink both methods and is used elsewhere too (`BuildSkillNextLevelCost` line 351).

### Finding 27: `BuildHeaderSnapshotLines` has six separate try/catch blocks, each silently eating exceptions
- Lines: 161-247
- Removed: 0 (note only)
- Risk: low
- Detail: Each logical section (name, HP, capacity, money, points, status) is wrapped in its own `try { } catch { }` with no logging. This is intentional resilience, but a `MelonLogger.Warning` in each catch (or a shared helper) would assist debugging. Consistent with the broader file style.

### Finding 28: `AnnounceDerivedStatDescription` builds `var parts = new List<string>()` then immediately calls `parts.Add(name)` — could use collection initializer
- Lines: 97-98
- Removed: ~1
- Risk: low
- Detail: `var parts = new List<string>(); parts.Add(name);` can be `var parts = new List<string> { name };`. Trivial; one line saved.

---

## Helpers/CharacterAnnouncementHelper.ValueAdjustment.cs

### Finding 29: `AdjustSkill` does not check CanIncrease/CanDecrease before invoking; no boundary announcement
- Lines: 48-69
- Removed: 0 (note only)
- Risk: low
- Detail: Unlike `AdjustAttribute` which checks `CanIncreaseValue()` / `CanDecreaseValue()` and announces "Maximum" / "Minimum", `AdjustSkill` unconditionally invokes the plus/minus methods (guarded only by null check on the method info). If the skill is already at cap, the game's own handler silently does nothing and no boundary is announced to the user. Adding `editor.CanIncreaseValue()` / `CanDecreaseValue()` guards (if those methods exist on `CHA_SkillEditor`) would give consistent feedback. Medium importance for UX, but out of scope as a structural refactor.

---

## Helpers/StatusEffectHelper.cs

### Finding 30: Redundant double-null check on `effect.description`
- Lines: 55
- Removed: ~1
- Risk: low
- Detail: `if (!string.IsNullOrEmpty(effect.description) && effect.description != string.Empty)` — `string.IsNullOrEmpty` already tests for empty string, so `&& effect.description != string.Empty` is always true when the first condition passes. Remove the second clause.

### Finding 31: Six separate `try { } catch { }` blocks with no logging
- Lines: 36-47, 53-63, 66-76, 79-111, 113-135, 137-145
- Removed: 0 (note only)
- Risk: low
- Detail: Every section of `BuildEffectLine` uses a bare catch block. Consistent with the project style, but a single outer try/catch with selective logging would be more informative on failure.

---

## Helpers/TileCoordinateSystem.cs

### Finding 32: `GRID_SQUARE_SIZE` constant duplicated in `CombatState.cs` and `MapCursorState.cs`
- Lines: TileCoordinateSystem.cs:10; CombatState.cs:50; MapCursorState.cs:45
- Removed: ~2
- Risk: medium
- Detail: `TileCoordinateSystem.SquareSize = 1.6f` is the canonical constant, but `CombatState` and `MapCursorState` each declare their own `private const float GRID_SQUARE_SIZE = 1.6f`. The two state files should reference `TileCoordinateSystem.SquareSize` instead, eliminating the duplicate declarations and the risk of them drifting. Requires touching two state files (out of scope for this batch but noted here as the finding originates in `TileCoordinateSystem`).

### Finding 33: `IsGridAvailable` calls `TryGetFullMap` which may trigger reflection on first access
- Lines: 16-21
- Removed: 0 (note only)
- Risk: low
- Detail: `IsGridAvailable` is a property that calls `TryGetFullMap`, which does reflection field lookup on first call and caches it. `IsGridAvailable` is called twice per `GetDistanceText`/`GetRangeText` call (once by the property itself, once by `GetTileDistance`). The second call hits the cache so no real cost; this is just a note that the property is not free on first use.

### Finding 34: `GetTileDistance` fallback to `Vector3.Distance / SquareSize` returns `RoundToInt` which can differ by ±1 from grid distance
- Lines: 77
- Removed: 0 (note only)
- Risk: low
- Detail: When both nodes are null (no grid), the fallback `Mathf.RoundToInt(Vector3.Distance(a, b) / SquareSize)` uses Euclidean distance divided by tile size, which rounds differently than the Chebyshev-distance formula used when nodes are available (line 75: `Mathf.Max(dx, dz)`). This is an intentional approximation; no code change needed, but a comment noting the difference would clarify intent.
