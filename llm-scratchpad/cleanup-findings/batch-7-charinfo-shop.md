# Cleanup findings — CharacterInfoState + ShopState

---

## States/CharacterInfoState.cs

### Finding 1: Dead public field `openToAttributes` is never written
- Lines: 25–26
- Removed: 5 (field + XML doc)
- Risk: **low**
- Detail: The comment on line 25 explicitly says "Kept for potential future use but no longer set by CharacterInfoPatches." No code in the file reads or writes this field since the patch was removed. It is `public static` so a grep of the whole solution confirms no external writer exists. Safe to delete the field and its XML comment block.

---

### Finding 2: `CacheReflection()` is an instance method but all its side-effects are on static fields
- Lines: 1876–1893
- Removed: 0 (signature change only, no lines removed)
- Risk: **low**
- Detail: The method is declared `private void CacheReflection()` (instance), but it only writes `charInfoCurrentPanelField`, `charInfoCurrentPCField`, `skillInfoEditorsField`, `skillInfoCurrentCategoryField`, and the `reflectionCached` bool — all four of which are `private static`. This should be `private static void CacheReflection()` to match `ShopState.CacheReflection()` and the InventoryState pattern. The callers use `if (!reflectionCached) CacheReflection()` which works either way, but the mismatch is misleading. Compare ShopState line 1618 where `CacheReflection()` is correctly declared `private static void`.

---

### Finding 3: `skillInfoEditorsField` and `skillInfoCurrentCategoryField` are cached but never read
- Lines: 80–81, 1888–1889
- Removed: 6 (two field declarations + two cache lines)
- Risk: **low**
- Detail: `skillInfoEditorsField` and `skillInfoCurrentCategoryField` are populated in `CacheReflection()` but are not referenced anywhere else in the file — no `GetValue` / `SetValue` call uses them. They were presumably kept "for potential future use." Removing them shrinks the reflection cache and eliminates two warning-suppressed null assignments.

---

### Finding 4: `suspendedPanel` field is set in `OnDeactivated()` but never read
- Lines: 38 (field), 55–56 (declaration), 213
- Removed: 3 (field declaration + assignment)
- Risk: **low**
- Detail: `suspendedPanel = lastPanel` is assigned in `OnDeactivated()` (line 213), but `suspendedPanel` is never subsequently read — `OnActivated()` restores using `GetCurrentPanel(charInfoMenu)` directly, not the saved panel value. The field declaration and the assignment line can both be removed. `suspendedIndex` and `hasSuspendedState` are still needed.

---

### Finding 5: Index-clamp logic duplicated across `OnActivated()` (suspend-restore branch)
- Lines: 194–197 (CharacterInfoState.OnActivated), compare ShopState lines 218–221
- Removed: 0 (no lines removed, but the two files have identical copy-pasted suspend-restore index clamps)
- Risk: **low** (cross-file note only)
- Detail: Both files contain the identical three-liner:
  ```csharp
  if (suspendedIndex >= 0 && suspendedIndex < currentList.Count)
      currentIndex = suspendedIndex;
  else if (currentList.Count > 0)
      currentIndex = Math.Min(suspendedIndex, controlList.Count - 1);
  ```
  In CharacterInfoState the last branch uses `controlList.Count - 1`; in ShopState it uses `currentList.Count - 1`. This inconsistency suggests copy-paste drift. The `Math.Min(suspendedIndex, …)` is also redundant when `suspendedIndex >= currentList.Count` — `currentList.Count - 1` is the correct clamp and the `Math.Min` adds no value. Not a bug, but both branches could be unified to `currentIndex = currentList.Count - 1` (or `controlList.Count - 1`).

---

### Finding 6: `AnnounceAttributePointsRemaining` and `AnnounceSkillPointsRemaining` are structurally identical
- Lines: 931–943 (`AnnounceAttributePointsRemaining`), 1146–1158 (`AnnounceSkillPointsRemaining`)
- Removed: ~10 (could extract a shared helper, collapsing two methods into calls)
- Risk: **low**
- Detail: Both methods do: `GetCurrentPC(menu)` → null check → read `pc.pcTemplate.available*Points` → speak `"{n} X point{s} remaining"` → else speak "No points information available". The only differences are the field name (`availableAttributePoints` vs `availableSkillPoints`) and the spoken noun ("attribute" vs "skill"). A private helper `AnnouncePointsRemaining(int points, string noun)` would eliminate ~12 duplicate lines. `AnnouncePerkPointsRemaining` (line 1219) is the same pattern again with `availableTraitPoints` and "perk".

---

### Finding 7: Edit-mode block in `HandleAttributesInput` and `HandleSkillsInput` is duplicated verbatim
- Lines: 836–863 (Attributes edit-mode block), 951–979 (Skills edit-mode block)
- Removed: ~20 (could factor into a shared `HandleEditModeInput(Action<int> adjust)` helper)
- Risk: **low**
- Detail: Both blocks handle the same five keys identically (Return/Escape → exit edit, Left/Minus/KeypadMinus → adjust(-1), Right/Equals/KeypadPlus → adjust(1), I → description, Up/Down → block). The only difference is that Attributes calls `AdjustCurrentAttribute(n)` and Skills calls `AdjustCurrentSkill(n)`. The Action<int> delegate pattern used elsewhere in the codebase would eliminate ~22 lines of copy-paste.

---

### Finding 8: Up/Down navigation boilerplate repeated in five panel handlers
- Lines: 867–871 (Attributes), 983–987 (Skills), 1168–1172 (Traits), 1241–1245 (Dossier), 1288–1292 (Logbook)
- Removed: ~15 if extracted to a helper
- Risk: **low**
- Detail: Every panel handler contains the pattern:
  ```csharp
  if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
  {
      int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
      NavigateList(dir); // or NavigateDossier
      return true;
  }
  ```
  The Dossier handler calls `NavigateDossier` directly but is otherwise identical. This is a low-priority style issue, but it means the same 4-line block appears five times. `HandleCommonInput` already factors out cross-panel keys; Up/Down could be added there, with per-panel overrides only where the target method differs.

---

### Finding 9: `HandleLogbookInput` — Right arrow opens details AND Left arrow switches category, creating a conflict comment but no guard
- Lines: 1295–1308
- Removed: 0 (design note only)
- Risk: **low**
- Detail: Line 1296 handles `RightArrow` as "open details". Line 1303 handles `LeftArrow` as "switch category backward". This means Left and Right have asymmetric behaviors: Right = drill-down, Left = category switch. The comment on line 1301 says "Left/Right to switch sort category" but only Left is actually a sort switch; Right is detail-open. This is confusing and the comment is stale/misleading. The actual behavior should be documented: Right = open details, Left = previous category, F = next category.

---

### Finding 10: `BuildAttributeControls` reimplements child-sort logic that `AddGridChildren` already provides
- Lines: 371–389 (BuildAttributeControls), 579–603 (AddGridChildren)
- Removed: ~12
- Risk: **low**
- Detail: `BuildAttributeControls` manually iterates `grid.transform`, filters `activeInHierarchy`, sorts by name, and adds to `controlList`. `AddGridChildren` does the identical operation plus additionally skips disabled `CHA_SkillEditor` components (which don't exist on attribute editors, so the extra guard is harmless). `BuildAttributeControls` could be reduced to two lines: `AddGridChildren(menu.attributePanel.attributeGrid); MelonLogger.Msg(…)`.

---

### Finding 11: `catch { }` swallows all exceptions in `AddDossierSkillPointsPerLevel`
- Lines: 548–554
- Removed: 0 (the try/catch should stay, but the catch body should at minimum log)
- Risk: **medium**
- Detail: The bare `catch { }` on lines 553–554 silently discards any exception that occurs when comparing `label.color` to `GUIManager.buffedTextColor` or `GUIManager.debuffedTextColor`. If `GUIManager` static fields have not been initialized, or if those fields were renamed in a game update, the failure is invisible. At minimum the catch should log a `MelonLogger.Warning`. Compare the try/catch pattern used in `TradeItem`, `RemoveFromEscrow`, `FinalizeTrade`, and `SellAllJunk` in ShopState — all include a `MelonLogger.Error` call.

---

### Finding 12: `HandleCommonInput` D-key and E-key both perform the same `GetCurrentPC` null-fallback pattern
- Lines: 714–720 (D key), 741–746 (E key)
- Removed: ~6
- Risk: **low**
- Detail: Both handlers do:
  ```csharp
  PC pc = GetCurrentPC(menu);
  if (pc == null && MonoBehaviourSingleton<Game>.HasInstance())
      pc = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();
  ```
  This null-fallback is also repeated in `OpenStatsBrowser` (1621–1623), `SwitchStatsSection` (1650–1652), `OpenInfoBrowser` (1694–1696), and the F1–F7 branch of `HandleInfoInput` (1841–1843) — six total occurrences. The fallback belongs inside `GetCurrentPC(CharacterInfoMenu)` itself, or a wrapper `GetCurrentPCWithFallback(CharacterInfoMenu)`.

---

### Finding 13: Magic number `3` in `SwitchStatsSection` for section wrap-around
- Lines: 1646
- Removed: 0 (value change only)
- Risk: **low**
- Detail: `int count = 3;` hard-codes the number of `StatsSection` enum values. `System.Enum.GetValues(typeof(StatsSection)).Length` or a named constant would be safer. The enum has exactly three values (Header, Combat, Derived) and is unlikely to change, but the pattern is inconsistent with `SwitchLogbookCategory` which uses a local array `categories` (line 1541) to drive the same kind of modular wrap.

---

### Finding 14: `AnnounceCurrentStatDescription` does a second `FindObjectOfType<CharacterInfoMenu>()` lookup
- Lines: 1864–1870
- Removed: 0 (refactor only)
- Risk: **low**
- Detail: `AnnounceCurrentStatDescription` (called from HandleAttributesInput and HandleSkillsInput) calls `UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>()` independently. All callers already have `menu` in scope. The method could accept `CharacterInfoMenu menu` as a parameter, eliminating a redundant scene query. The callers pass it to every other helper in the file except this one.

---

### Finding 15: `NavigateList` for non-Dossier panels guards `newIndex != controlIndex` before updating, but `NavigateDossier` has an extra `|| lastAnnouncedText == null` check that `NavigateList` lacks
- Lines: 619–629 (NavigateList), 632–648 (NavigateDossier)
- Removed: 0 (potential bug flag)
- Risk: **medium**
- Detail: `NavigateDossier` will re-announce even on no-index-change if `lastAnnouncedText == null` (line 640). `NavigateList` for non-Dossier panels silently does nothing when `newIndex == controlIndex` (no re-announce). This means a fresh activation with `controlIndex == 0` followed by pressing Up (wraps to 0 again) will re-announce on Dossier but not on other panels. The intent seems to be that Dossier should always speak on user input; if so, this inconsistency should be documented, or the `|| lastAnnouncedText == null` guard should be considered for `NavigateList` as well.

---

### Finding 16: `OnPanelChanged` always resets `lastAnnouncedIndex = -1` and then `OnActivated` (non-suspend path) immediately calls `OnPanelChanged` — but `OnActivated` also resets `lastAnnouncedIndex = -1` first
- Lines: 161–208
- Removed: 0 (redundancy note)
- Risk: **low**
- Detail: `OnActivated` clears `lastAnnouncedText = null` and `lastAnnouncedIndex = -1` on lines 164–165, then calls `OnPanelChanged` which clears them again on lines 289–290. The double reset is harmless but the two assignments in `OnActivated` are redundant for the non-suspend path.

---

### Finding 17: `logbookDetailLines`, `logbookDetailIndex`, `logbookDetailMode`, `currentLogbookEntryName` are reset in both `OnActivated` and `OnPanelChanged` — but `OnActivated` calls `OnPanelChanged`, so the reset in `OnActivated` is partially redundant
- Lines: 176–179 (OnActivated resets), 283–286 (OnPanelChanged resets)
- Removed: ~4 lines
- Risk: **low**
- Detail: `OnActivated` clears `logbookDetailMode`, `logbookDetailIndex`, `logbookDetailLines.Clear()`, and `currentLogbookEntryName` (lines 176–179), then immediately calls `OnPanelChanged` which clears them again (lines 283–286). The copies in `OnActivated` are redundant for the normal (non-suspend) flow. For the suspend-restore branch the logbook detail state is correctly already clear (from `OnDeactivated`), so the `OnActivated` resets serve no additional purpose.

---

## States/ShopState.cs

### Finding 1: `BuildPlayerItemList` has a dead duplicate `GetComponent<VND_DragDropItem>()` call
- Lines: 363–364
- Removed: 1
- Risk: **low**
- Detail: Line 363 assigns `var item = t.GetComponent<VND_DragDropItem>()` and line 364 immediately re-assigns `if (item == null) item = t.GetComponent<VND_DragDropItem>()` — both calls are identical, so the second is always a no-op. Furthermore `item` is never used after this block (line 365 gets `baseItem` via `INV_DragDropItem` instead). The entire two-line `item` variable block is dead code that can be deleted outright.

---

### Finding 2: `onSellJunkClickedMethod` is cached via reflection but `OnSellJunkClicked` is public
- Lines: 1628–1633 (CacheReflection), 864–868 (SellAllJunk)
- Removed: 0 (could eliminate reflection entirely and call directly)
- Risk: **low**
- Detail: The comment on line 1628 says "public but we cache it anyway." If the method is genuinely public on `VendorScreen`, it can be called directly as `vendorScreen.OnSellJunkClicked(null)` without `MethodInfo.Invoke`, removing the reflection cache entry and the associated `try/catch` in `SellAllJunk`. This should be verified against the decompiled source but the comment itself flags it as unnecessary.

---

### Finding 3: `PerformAction` uses two separate `current is string str` / `current is string str2` checks to avoid C# pattern-variable reuse
- Lines: 567–578
- Removed: ~4
- Risk: **low**
- Detail: Because C# 7.0 (or net35 language level) won't let you reuse a pattern variable name in consecutive `is` patterns, the code invents `str` and `str2`. A single `string strVal = current as string` before the if-chain, then `strVal == EscrowSummaryMarker` and `strVal == EscrowFinalizeMarker`, removes the hack and makes both comparisons readable. `FormatCurrentItemAnnouncement` at lines 1351–1358 has the same `str` / `str2` duplication. Both sites can be cleaned the same way.

---

### Finding 4: `SwitchZone` counts escrow items with a `foreach` loop when `BuildEscrowList` already logs `itemCount`
- Lines: 312–321
- Removed: ~6
- Risk: **low**
- Detail: After calling `RebuildCurrentList()`, `SwitchZone` immediately re-counts `EscrowEntry` items in `currentList` with a fresh `foreach` loop to form the zone announcement. `BuildEscrowList` computes `int itemCount = currentList.Count - 2` (line 448) and logs it, but that value is lost. A field or a return value from `BuildEscrowList` would avoid the redundant scan. Alternatively, since the sentinels are always the last two entries, `itemCount = currentList.Count - 2` (clamped to ≥0) is always correct and removes the loop.

---

### Finding 5: `ClampIndex` is called at the end of each `Build*List` method AND `RebuildCurrentList` applies its own index-preservation logic afterwards — the two mechanisms overlap
- Lines: 480–506 (RebuildCurrentList), 508–513 (ClampIndex)
- Removed: 0 (design note)
- Risk: **low**
- Detail: Each `Build*List` method ends with `ClampIndex()` which sets `currentIndex = 0` for empty→non-empty transitions and clamps for overflow. `RebuildCurrentList` then runs its own preserve/clamp block (lines 499–505) that overwrites whatever `ClampIndex` just set. This means `ClampIndex` inside the individual build methods is redundant when called via `RebuildCurrentList`. The `ClampIndex` calls in the build methods are only independently useful when those methods are called directly (e.g. `BuildVendorItemList()` from `OnActivated`). This should be documented or the single-entry-point pattern should be enforced.

---

### Finding 6: `GetFilterName` returns `"All"` for both `InventoryFilter.All` and `InventoryFilter.AllWithJunk`
- Lines: 1601–1602
- Removed: 0 (potential user-facing ambiguity)
- Risk: **low**
- Detail: `InventoryFilter.All` and `InventoryFilter.AllWithJunk` both map to `"All"`. If the filter cycles through `AllWithJunk` (which is in `filterOrder`) and the user presses S for scrap balance, the announcement says "All" regardless of which variant is active. This could be `"All items"` vs `"All with junk"` to be clearer. The `CycleFilter` method uses `AllWithJunk` in its `filterOrder` array (line 917), so `InventoryFilter.All` is never reached by cycling — the `All` case in `GetFilterName` is dead code.

---

### Finding 7: `AnnounceEscrowSummary` is a one-liner wrapper around `FormatEscrowSummary` + `SpeakInterrupt`
- Lines: 1446–1450
- Removed: 3
- Risk: **low**
- Detail: `AnnounceEscrowSummary` has three lines: get string, speak, close brace. Its only caller is `PerformAction` line 569. The call site could be `ScreenReaderManager.SpeakInterrupt(FormatEscrowSummary())` directly, eliminating the wrapper. The same pattern appears in other states (e.g. `AnnounceLogbookDetail` in CharacterInfoState which is similarly thin). Low priority but adds to the count of trivial wrappers.

---

### Finding 8: `HandleInfoBrowserInput` in ShopState and `HandleInfoInput` in CharacterInfoState both implement Up/Down/Home/End line browsing with identical patterns — cross-file duplication
- Lines: ShopState 1009–1059, CharacterInfoState 1762–1858
- Removed: 0 (extraction to a shared helper would be architectural)
- Risk: **low** (cross-file note)
- Detail: Both files contain the same four key-handlers (Up, Down, Home, End) that navigate `infoLines` / `infoLineIndex` and call `ScreenReaderManager.SpeakInterrupt($"{infoLines[idx]}, {idx+1} of {count}")`. ShopState's version also handles Escape/I to close. CharacterInfoState's version is more complex (stats sections, F1–F7 party switch). The core Up/Down/Home/End loop body is copy-paste identical. Per the out-of-scope note (no file splitting), this is flagged as a candidate for a `InfoBrowserNavigationHelper` if the architecture is ever refactored. Not actionable in this batch.

---

### Finding 9: `BuildInfoLines` `try/catch` around `ItemInfoBox` scraping swallows exception message at warning level only, inconsistent with other catches
- Lines: 1215–1228
- Removed: 0 (logging level note)
- Risk: **low**
- Detail: The catch on line 1226 logs `MelonLogger.Warning(...)` while `TradeItem`, `RemoveFromEscrow`, and `FinalizeTrade` all log `MelonLogger.Error(...)` for their caught exceptions. `ItemInfoBox` failure is relatively benign (the info browser just misses some display labels), so `Warning` may be correct, but the inconsistency is worth a comment explaining why it's not `Error`.

---

### Finding 10: `GetCurrentPC()` in ShopState ignores the `vendorScreen` reference after fetching it
- Lines: 1567–1574
- Removed: 0 (potential simplification)
- Risk: **low**
- Detail: `GetCurrentPC()` fetches `vendorScreen` via `GetVendorScreen()` on line 1569, checks it for null, then falls through unconditionally to `MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC()` — the `vendorScreen` is never actually used inside the method. The comment says "pcSelected is private; try the game's selected PC." If the intent is to guard against calling `Game.GetInstance()` without a valid screen, the check makes sense; but the method could simply skip the `GetVendorScreen()` call and call `Game.GetFirstSelectedPC()` directly, since the state is only active when `VendorScreen` is open anyway.

---

### Finding 11: Magic array `filterOrder` in `CycleFilter` is re-allocated on every key press
- Lines: 914–924
- Removed: 0 (allocation note)
- Risk: **low**
- Detail: `InventoryFilter[] filterOrder = new InventoryFilter[] { … }` (line 915) is declared as a local inside `CycleFilter`. This allocates a new array on every F key press. Since `filterOrder` is constant, it should be a `private static readonly InventoryFilter[]` field (compare `ZoneOrder` at line 37 which is already a `static readonly` array for the same kind of cyclic lookup). This affects only the GC pressure from key-repeat but is trivially fixable.

---

### Finding 12: `SwitchZone` always announces `"{zoneName}, {currentList.Count} items"` for non-Escrow zones, but `currentList.Count` for the Filters zone includes filter GameObjects — not items — making the string misleading
- Lines: 309–323
- Removed: 0 (wording note)
- Risk: **low**
- Detail: When switching to `ShopZone.Filters`, the announcement is e.g. `"Filters, 7 items"`. The word "items" is inaccurate for filter buttons. The Escrow zone already has a corrected announcement ("7 items in escrow"); Filters should say "7 filters" for consistency. The `GetZoneEmptyMessage` already returns `"No filters available"` for this zone, showing the distinction was intentional elsewhere.

---

### Finding 13: `FormatCurrentItemAnnouncement` and `PerformAction` both type-dispatch on `currentList[currentIndex]` with the same if-chain order — duplicated dispatch
- Lines: PerformAction 556–601, FormatCurrentItemAnnouncement 1346–1410
- Removed: 0 (structural duplication note)
- Risk: **low**
- Detail: Both methods open `object current = currentList[currentIndex]` and then check `string == EscrowSummaryMarker`, `string == EscrowFinalizeMarker`, `GameObject`, `EscrowEntry`, `INV_DragDropItem` in the same order. The type-dispatch logic is load-bearing and semantics differ between the two, so factoring it is non-trivial (not actionable in this batch). The comment in the index already calls this out: "big type-dispatch switch disguised as if-chain." Documenting the canonical dispatch order in a comment would reduce future maintenance risk.

---

### Finding 14: `FinalizeTrade` computes `vendorEscrowVal` and `playerEscrowVal` via `Escrow.GetTotalVendorEscrowValue()` / `GetTotalPlayerEscrowValue()`, then separately iterates `Escrow.escrowList` to check `hasItems` — a redundant scan
- Lines: 787–800
- Removed: ~8
- Risk: **low**
- Detail: If `vendorEscrowVal > 0 || playerEscrowVal > 0` then there are items by definition (a non-zero value implies at least one item). The `hasItems` loop (lines 791–800) adds 10 lines to detect the empty-escrow case, but that case is already covered: if both values are 0 and escrow is empty, `netCost == 0` and the trade would trivially succeed as an "even trade" of nothing. A guard `if (vendorEscrowVal == 0 && playerEscrowVal == 0) { SpeakInterrupt("Nothing to trade"); return; }` before the loop replaces the 10-line scan.

---

### Finding 15: `RemoveFromEscrow` and `TradeItem` both refresh all four containers (`playerInventoryContainer`, `playerEscrowContainer`, `vendorInventoryContainer`, `vendorEscrowContainer`) — this 4-line block is duplicated
- Lines: TradeItem 696–699, RemoveFromEscrow 758–763
- Removed: ~4 if extracted to a helper
- Risk: **low**
- Detail: Both methods call `.Refresh()` on all four `VendorScreen` container references in the same order. A private `void RefreshAllContainers(VendorScreen vendorScreen)` helper would make the intent explicit and avoid drift if a fifth container is ever added.

---

### Finding 16: `CycleFilter` silently does nothing when `inventoryContainerFilterField` is null but `setFilterMethod` is not null, or vice versa — no warning logged
- Lines: 908–945
- Removed: 0 (logging note)
- Risk: **low**
- Detail: If `inventoryContainerFilterField` is null (reflection failed at startup), `currentFilter` stays as `InventoryFilter.AllWithJunk` (the default) silently; no message is spoken to the user and no log is written. The user presses F and nothing happens — indistinguishable from a game bug. A guard before the filter read that checks both fields and speaks "Filter not available" (or logs a warning) matches the pattern used for `tryGetDestInventoryMethod` in `TradeItem` (line 633–638).

---
