# Cleanup findings — States/CombatState.cs

## States/CombatState.cs

---

### Finding 1: `modePenalty` parameter in `BuildAttackInfoForMode` is received but never used

- Lines: 2770–2811
- Removed: ~0 lines of code removed, but 1 parameter eliminated from signature
- Risk: low
- Detail: `BuildAttackInfoForMode(PC pc, Mob target, int apCost, int ammoCost, int modePenalty)` accepts `modePenalty` but never references it inside the method body. The comment says hit% is read via `pc.GetChanceToHit` which reads `firingModeIndex` internally — the penalty was presumably superseded by the temporary-index approach. The unused parameter should be removed from the signature and from the call site at line 2505.

---

### Finding 2: `FormatAction` and `FormatTargetAction` are identical except for the list they read

- Lines: 1992–2005 and 2845–2858
- Removed: ~13
- Risk: low
- Detail: Both methods have the same body — append Status, optionally append "unavailable", append "N of Total" — differing only in which `List<CombatAction>` they index for position. A single `private string FormatCombatAction(CombatAction action, List<CombatAction> list)` helper eliminates the duplication. The callers already have the correct list in scope.

---

### Finding 3: Duplicate floor-scan loop in `GetNodeAtGridId` and `GetNodeInFullMap`

- Lines: 931–950 and 975–992
- Removed: ~12
- Risk: low
- Detail: Both methods contain the identical `for (int f = 0; f <= 5; f++)` block that tries alternate floor Y-levels at the same X,Z grid position. A private `TryGetNodeFromMap(Dictionary<Vector3,CombatAStarNode> map, Vector3 gridId, out CombatAStarNode node)` helper removes the copy. The magic `5` should become a named constant (see Finding 4).

---

### Finding 4: Magic number `5` (max floor level) inline in two places without a constant

- Lines: 941, 983
- Removed: 0 (replacement, not removal)
- Risk: low
- Detail: The floor-scan loops both use the literal `5` as the ceiling for floor Y-indices. There is no corresponding named constant. Given that `GRID_SQUARE_SIZE` is commented as "must match CombatAStar.squareSize", a `private const int MAX_FLOOR_INDEX = 5` would be equally self-documenting and safer to update. Should accompany Finding 3.

---

### Finding 5: Magic literal `0.75f` in `FindTargetableOnTile` is not `TILE_MATCH_RADIUS` and has no name

- Lines: 2340
- Removed: 0 (replacement)
- Risk: low
- Detail: `FindTargetableOnTile` uses a hard-coded `float tileRadius = 0.75f` for the `OverlapSphere` radius. `TILE_MATCH_RADIUS` is `GRID_SQUARE_SIZE * 0.75f = 1.6f * 0.75f = 1.2f`, so they are numerically different. A reader will assume the two should match, which is misleading. Either introduce a named constant (e.g. `TARGETABLE_SEARCH_RADIUS = 0.75f`) or add a comment clarifying why this radius is smaller than `TILE_MATCH_RADIUS`.

---

### Finding 6: Duplicate guard idiom in `JumpToSelectedCombatant` and `AnnounceDistanceToSelectedCombatant`

- Lines: 1510–1526 and 1528–1543
- Removed: ~12
- Risk: low
- Detail: Both methods open with the same two-step guard (`combatantList.Count == 0 || index out-of-range` → "No combatant selected"; then `mob == null || mob.mobState == DEAD` → "Selected combatant no longer valid") before diverging at the final call. A shared `private bool TryGetSelectedCombatant(out Mob mob)` helper that encapsulates both guards and the `ScreenReaderManager` messages removes the duplication. The pattern appears nowhere else so refactoring is contained.

---

### Finding 7: Duplicate hostile-detection block in `BuildInitiativeList` (two passes)

- Lines: 3234–3248 and 3263–3276
- Removed: ~12
- Risk: low
- Detail: The two passes over `actQueue` and `cm.mobs` in `BuildInitiativeList` share an identical code block: compute `bool hostile`, evaluate `IsMobRevealedToParty`, then `Add(new InitiativeEntry { ... })`. The only structural difference is `addedMobs.Add(mob)` in the first pass. Extracting a private `AddMobToInitiativeList(Mob mob, CombatManager cm, Mob currentActor)` helper (that returns a bool indicating whether the mob was added) removes both duplications cleanly.

---

### Finding 8: Duplicate `GetDisplayName` inline expression in `BuildInitiativeList`

- Lines: 3242, 3271
- Removed: ~2
- Risk: low
- Detail: Both passes use `GetDisplayName(mob.template != null ? mob.template.displayName : mob.name)` to set `InitiativeEntry.Name`. This ternary pattern for extracting a mob's raw display name already exists in `GetMobName` (line 1664–1678), which handles localization and fallback. If `InitiativeEntry.Name` should be the same as `GetMobName` output, these two sites should call `GetMobName(mob)` directly. If they intentionally differ (skipping `CleanText`), a comment explaining the reason is missing. Either way this is duplication.

---

### Finding 9: `AnnounceCombatant` is a one-liner wrapper that adds no value

- Lines: 1484–1488
- Removed: ~5
- Risk: low
- Detail: `AnnounceCombatant(Mob mob)` contains only `SpeakInterrupt(FormatMobForCycle(mob))`. It is called from `CycleNextCombatant` and `CyclePreviousCombatant` only. Both callers also have the mob in scope. The method can be inlined; doing so removes a level of indirection with no readability loss, consistent with how `JumpToCombatant` directly calls `SpeakInterrupt(FormatMobForCycle(mob))` at line 1507.

---

### Finding 10: Duplicate mob state/cover block in `FormatMobForTile` and `FormatMobForCycle`

- Lines: 1222–1229 and 1606–1613
- Removed: ~7
- Risk: low
- Detail: Both methods append the same four state flags: `UNCONSCIOUS`, `inCover` (with "tall cover"/"short cover" ternary), `isCrouching`, `isHidden`. This exact fragment is also partially repeated in `BuildInitiativeMobDetails` (lines 3314–3321) and `BuildPartyMemberInfo` (lines 2173–2178) with minor phrasing variations. A shared `AppendMobStateFlags(List<string> parts, Mob mob)` helper centralises the logic; the phrasing variant in `BuildInitiativeMobDetails` ("in tall cover" vs "tall cover") would need a parameter or separate call.

---

### Finding 11: `BuildPartyMemberInfo` has two bare `catch { }` blocks that silently drop errors

- Lines: 2199 and 2215
- Removed: 0 (replacement)
- Risk: low
- Detail: The weapon-info block (lines 2181–2199) and status-effects block (lines 2201–2215) both end with `catch { }`. On .NET 3.5, Unity can throw unexpected exceptions from game object access; swallowing them silently prevents diagnostic logging. Both should be `catch (Exception ex) { MelonLogger.Warning(...) }` consistent with every other method in the file that logs on failure.

---

### Finding 12: Two bare `catch { }` blocks in `FindMobsOnTile` silently swallow transform errors

- Lines: 1355 and 1377
- Removed: 0 (replacement)
- Risk: low
- Detail: The `IsOnCurrentTile(mob.transform.position)` calls inside `FindMobsOnTile` are wrapped in `catch { }` with the comment "Dead mobs with deactivated GameObjects may have stale transforms." This reasoning is sound, but swallowing all exceptions means `NullReferenceException` from live mobs would also be invisible. The catch should at minimum be `catch (Exception) { }` (already the "intentional miss" pattern at line 2283) or add a `null`-check on `mob.transform` before the call so only genuine stale-transform cases are caught.

---

### Finding 13: Bare `catch { }` in inner `CalculateDamage` calls in `BuildAttackInfo` and `BuildAttackInfoForMode`

- Lines: 2751 and 2797
- Removed: 0 (replacement)
- Risk: low
- Detail: Both methods contain an inner `try { CalculateDamage(...) } catch { }` that leaves `minDmg`/`maxDmg` at `GetMinDamage()`/`GetMaxDamage()` on failure without any log. Given that both outer methods already log their own errors, a `catch (Exception ex) { MelonLogger.Warning("[CombatState] CalculateDamage error: " + ex.Message); }` here would help diagnose mismatches between announced damage and actual.

---

### Finding 14: `SuppressInput()` omits `ShouldSuppressButtonEvents`; hot-key handlers set it manually instead

- Lines: 1707–1711 vs. 154–157, 216–217, 702–703, 711–712
- Removed: ~8 (consolidation)
- Risk: medium
- Detail: `SuppressInput()` sets only `ShouldSuppressGameInput` and `ShouldSuppressUINavigation`. However, at least four call sites (T key, Tab key, I key, Escape key) also manually set `ShouldSuppressButtonEvents = true`. The browsing-mode blocks (lines 172–175, 237–239, etc.) set all three flags via explicit lines (not via `SuppressInput()`), suggesting the omission of `ButtonEvents` from `SuppressInput` was intentional for cursor-movement calls. At minimum, a comment on `SuppressInput()` should explain why `ButtonEvents` is excluded, preventing future contributors from "completing" it incorrectly.

---

### Finding 15: Inline exit logic for `browsingPartyInfo` and `browsingLog` inconsistent with dedicated Exit* methods on other modes

- Lines: 382–389 (party info exit), 424–430 (log exit)
- Removed: ~10 (if extracted)
- Risk: low
- Detail: `browsingInitiative`, `browsingActions`, and `browsingTargetActions` each have a dedicated `Exit*Browse()` method called from `HandleInput`. The `browsingPartyInfo` close-path (lines 382–389) and `browsingLog` close-path (lines 424–430) instead inline the clear/flag/speak logic directly in `HandleInput`. This means `OnDeactivated` cannot call a single cleanup method for these modes. Extracting `ExitPartyInfoBrowse()` and `ExitLogBrowse()` would match the pattern of the other five sub-modes.

---

### Finding 16: `inCombat` check in `BuildTargetInfoLines` is redundant inside `CombatState`

- Lines: 2965–2966
- Removed: ~2
- Risk: low
- Detail: `BuildTargetInfoLines` calls `MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat` before showing "AP remaining this turn". Since `CombatState.IsActive` already requires `inCombat == true`, this guard is unreachable-false while the state is active. The check can be removed. (Same redundancy at line 2314 in `ExecuteFreeAimShot` — see Finding 17.)

---

### Finding 17: `inCombat` check in `ExecuteFreeAimShot` is always true in context

- Lines: 2314–2317
- Removed: ~3
- Risk: low
- Detail: `if (MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat) { combatAStar.ClearPath(); }` is redundant because `CombatState.IsActive` already gates the entire state on `inCombat`. The `ClearPath()` call should be unconditional (removing the `if`) or a comment should explain why the guard is needed.

---

### Finding 18: `GetCurrentActor()` reflection call repeated multiple times per frame

- Lines: 775, 1155, 1616
- Removed: 0 (optimization)
- Risk: low
- Detail: `GetCurrentActor()` calls `curActorField.GetValue(cm)` via reflection on every invocation. In a single frame, `HandleInput` can trigger `EnsureCursorReady()` (one call), `AnnounceTile` (another), and `FormatMobForCycle` (another via combatant cycling). While the field reference is lazily cached, `GetValue` still incurs an unboxing allocation per call. A frame-local variable cached at the top of `HandleInput`, or a per-call guard (`if (_currentActor == null || needsRefresh)`), would reduce allocations. Low urgency given combat's polling rate.

---

### Finding 19: `string.Join(", ", parts.ToArray())` — `.ToArray()` is required on .NET 3.5 but should have an explanatory comment

- Lines: 1188, 1202, 1231, 1246, 1640, 2178, 2425, 2764, 2810, 3323, 3343
- Removed: 0 (comment-only)
- Risk: low
- Detail: `string.Join(string, IEnumerable<string>)` does not exist in .NET 3.5 so `.ToArray()` is correctly required throughout. Without a comment, future contributors targeting a newer framework may "clean up" the `.ToArray()` calls not realising they are load-bearing for the .NET 3.5 target. A single `// .NET 3.5: string.Join requires array, no IEnumerable overload` comment near the first occurrence would suffice.

---

### Finding 20: Floor display offset `+ 1` is a magic number with no named constant

- Lines: 1112–1113
- Removed: 0 (replacement)
- Risk: low
- Detail: `coords += ", floor " + ((int)cursorGridId.y + 1)` adds 1 to the Y index before displaying it (so Y=0 → "floor 1"). This is intentional UX (floor 1 = ground), but the `+ 1` offset is undocumented. If `MAX_FLOOR_INDEX` is introduced (Finding 4), a `private const int FLOOR_DISPLAY_OFFSET = 1` would make the intent explicit and keep the two constants in the same place.

---

### Finding 21: Anonymous `{ }` scope blocks in `BuildActionList` are cosmetic and inconsistent

- Lines: 1815–1828, 1831–1850, 1852–1871, 1873–1896, 1946–1958
- Removed: ~10
- Risk: low
- Detail: The Reload/Unjam block uses a plain `if/else` without braces; the Swap Weapons, Crouch/Stand, Ambush, Free Aim, and End Turn sections are each wrapped in an anonymous `{ }` block as a visual separator. The braces do not change scope and are not needed. Replacing them with blank lines and `// ---` section comments matches the visual pattern used in `BuildTargetActionList` and removes unnecessary nesting.

---

### Finding 22: `attackStatus` in `BuildTargetActionList` computed from `targetMob` field, not from `capturedTarget`

- Lines: 2460, 2466
- Removed: 0 (consistency fix)
- Risk: medium
- Detail: `string attackStatus = GetAttackStatus(pc, targetMob, isThinking)` at line 2460 uses the `targetMob` field directly. The comment at lines 2443–2445 explicitly notes that `targetMob` is cleared by `ExitTargetActionsBrowse()` before `Execute()` runs. The status string is computed at build time (not in a lambda), so there is no runtime bug — but using `targetMob` here and `capturedTarget` in the Execute lambdas creates a readability hazard suggesting inconsistency. Replacing `targetMob` with `capturedTarget` at lines 2460 and 2466 makes the pattern uniform.

---

### Finding 23: `IsMobRevealedToParty` bare `catch` swallows all exceptions without logging

- Lines: 3184–3185
- Removed: 0 (replacement)
- Risk: low
- Detail: `catch { return true; }` means any internal error from `IsTargetVisibleToFaction` silently passes as "revealed", which could cause unspotted enemies to appear in the initiative list without any trace. A `catch (Exception ex) { MelonLogger.Warning("[CombatState] IsMobRevealedToParty error: " + ex.Message); return true; }` is consistent with the file's other safety-catch patterns and would surface issues without changing the fallback behaviour.

---

### Finding 24: Cover description phrasing inconsistency between `BuildInitiativeMobDetails` and other formatters

- Lines: 3316–3317 vs. 1224–1225 and 1608–1609
- Removed: 0 (replacement)
- Risk: low
- Detail: `BuildInitiativeMobDetails` says `"in tall cover"` / `"in short cover"` while `FormatMobForTile` and `FormatMobForCycle` say `"tall cover"` / `"short cover"` (no "in"). The inconsistency is audible since these strings go directly to the screen reader. Either standardise on one phrasing via a `FormatCoverState(Mob mob)` helper (which would also address Finding 10) or add a comment explaining the intentional difference.

---

### Finding 25: `ExitInitiativeBrowse` always announces "Initiative closed", including when called mid-jump

- Lines: 194–197 and 3361–3366
- Removed: 0 (API change)
- Risk: low
- Detail: When the user presses Enter in the initiative list, `ExitInitiativeBrowse()` is called before `JumpToCombatant(entry.Mob)`. The user hears "Initiative closed" followed immediately by the full combatant announcement — the close message is redundant. An optional `bool silent = false` parameter (default false for Escape/T close, `true` for jump-and-close) would match the pattern in `ExecuteCurrentAction` and `ExecuteCurrentTargetAction`, which exit without announcing a close because the action label already signals completion.

---

### Finding 26: `FormatMobForCycle` reads instance fields `combatantIndex` and `combatantList.Count` directly

- Lines: 1638
- Removed: 0 (coupling note)
- Risk: low
- Detail: `FormatMobForCycle` appends `(combatantIndex + 1) + " of " + combatantList.Count` using instance fields. When called from `JumpToCombatant` via the initiative list (line 197), `combatantIndex` and `combatantList` may not reflect the jumped-to mob (if the user never PageDown-cycled to it). The current callers do set `combatantIndex` appropriately before calling, but the implicit precondition is undocumented. Consider passing `int position, int total` as parameters, or adding a precondition comment.

---

### Finding 27: `GetWeaponRangeInfo` has a bare `catch` that returns `null` without logging

- Lines: 1320–1323
- Removed: 0 (replacement)
- Risk: low
- Detail: The outer `try { ... } catch { return null; }` pattern in `GetWeaponRangeInfo` silently drops any stats access failure. The structurally similar `GetMovementCostInfo` (same region, lines 1294–1297) uses `catch (Exception ex) { MelonLogger.Warning(...); return null; }`. Aligning `GetWeaponRangeInfo` to the same pattern makes diagnostic behaviour consistent.

---

### Finding 28: `shift` variable at line 644 is a duplicate of `shiftForArrows` declared at line 532

- Lines: 532, 644
- Removed: ~2
- Risk: low
- Detail: `shiftForArrows` is declared at line 532 (`Input.GetKey(LeftShift) || Input.GetKey(RightShift)`). At line 644, `shift` is declared with the identical expression. Both are used in different blocks within the same `HandleInput` method and evaluate to the same value on any given frame. `shift` should be deleted and replaced with `shiftForArrows` at its two use sites (lines 647 and 655).

---

### Finding 29: Redundant explicit default-value field initialisers on boolean and reference fields

- Lines: 33, 37, 45, 74, 75, 88, 89, 93, 94, 97, 98, 99, 102, 103, 104, 107, 108, 111, 113, 116, 117
- Removed: ~10 tokens (style)
- Risk: low
- Detail: C# default-initialises `bool` to `false`, `int` to `0`, and reference types to `null`. Explicit `= false`, `= null`, and `= 0f` initialisers on fields such as `browsingInitiative = false`, `lastMoveTime = 0f`, `lastTrackedActor = null`, `cursorInitialized = false`, and `targetMob = null` are redundant. Removing them reduces visual noise without any behavioural change. Note: non-default initialisers like `stepSize = 1`, `combatantIndex = -1`, and `cameraFollowsCursor = true` should be kept.

---

### Finding 30: Forward/backward combatant cycling pattern is inconsistently structured relative to the initiative tracker equivalent

- Lines: 1457–1482 vs. 3346–3358
- Removed: 0 (consistency note)
- Risk: low
- Detail: `CycleNextCombatant` and `CyclePreviousCombatant` both call `BuildCombatantList()` before wrapping the index, which means the list is always fresh. `CycleInitiativeForward` and `CycleInitiativeBackward` do not rebuild `initiativeList` — they just wrap the index. This asymmetry is intentional (the initiative list is built once on open, while the combatant list is built fresh each cycle to reflect changes mid-combat), but there is no comment documenting the reason for the difference. A brief note on `CycleNextCombatant` explaining why the rebuild is done every cycle would prevent a future contributor from "optimising" it to match the initiative pattern.
