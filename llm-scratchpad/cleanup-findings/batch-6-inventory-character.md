# Cleanup findings — InventoryState + CharacterState

---

## States/InventoryState.cs

### Finding 1: Three reflection-cached fields never read after caching
- Lines: 70–72 (declarations), 1992–2001 (cache), 2014–2016 (cache)
- Removed: ~18
- Risk: low
- Detail: `charInfoPcContainerButtonsField`, `popupPcContainerButtonsField`, and `charInfoCurrentPanelField` are assigned in `CacheReflection()` but there is no `.GetValue()` or `.SetValue()` call on any of them anywhere in the file. `charInfoPcContainerButtonsField` and `popupPcContainerButtonsField` were presumably left over from an earlier party-switching implementation that was later replaced by the party-count-based approach in `SwitchPartyViaGameAPI` / `SwitchPopupPartyMember`. `charInfoCurrentPanelField` may have been intended for `SwitchCharacterInfoTab` but is never actually read there either. All three should be removed from the field list and from `CacheReflection()`.

### Finding 2: `inventoryContainerFilterField` cached but never read
- Lines: 73 (declaration), 2004–2006 (cache)
- Removed: ~4
- Risk: low
- Detail: `inventoryContainerFilterField` caches `InventoryContainer.filter` but the filter is always read via the public `container.GetFilter()` call at line 1008. The cached `FieldInfo` is never used. `CycleFilter()` only ever calls `inventoryContainerSetFilterMethod` (the setter), not the getter field. Remove the declaration and the caching block.

### Finding 3: `filterOrder` array allocated inside `CycleFilter()` on every call
- Lines: 1011–1021
- Removed: 0 lines removed, but allocation eliminated
- Risk: low
- Detail: `filterOrder` is a `new InventoryFilter[]` literal created every time the user presses F to cycle the filter. It never changes. It should be extracted to a `private static readonly InventoryFilter[] filterOrder` field alongside `equipmentSlotFieldNames` so the allocation happens once at class-init rather than each key press.

### Finding 4: Two near-identical log messages for context-change suspension discard
- Lines: 194–202
- Removed: ~6
- Risk: low
- Detail: `OnActivated()` contains two `if (hasSuspendedState && ...)` blocks that check whether charInfo context or popup context changed. Both print the identical log string template `"[InventoryState] Context changed (charInfo: ..., popup: ...), discarding suspended state"`. The two checks can be collapsed into one compound condition: `if (hasSuspendedState && (suspendedWasCharacterInfo != isCharacterInfoMenu || suspendedWasPopupInventory != isPopupInventoryMenu))`, eliminating one repeated log call and one duplicate `hasSuspendedState = false`.

### Finding 5: `GetCurrentPC()` does an uncached `GetField("pcSelected")` on every call
- Lines: 1841–1845
- Removed: 0 (extraction to cached field)
- Risk: low
- Detail: Inside `GetCurrentPC()`, when `isPopupInventoryMenu` is true, the method calls `typeof(PopupInventoryMenu).GetField("pcSelected", ...)` at runtime on every invocation — there is no cached `FieldInfo` for this field. The five other fields on `PopupInventoryMenu` are all pre-cached; `pcSelected` should join them as a static cache entry populated in `CacheReflection()`.

### Finding 6: `BuildEquipmentSlotList()` and `GetEquippedComparisonItem()` each call `typeof(INV_MainPanel).GetField(fieldName, ...)` in a loop
- Lines: 528–538 (BuildEquipmentSlotList), 1576–1580 (GetEquippedComparisonItem)
- Removed: 0 (caching improvement)
- Risk: low
- Detail: Both methods iterate `equipmentSlotFieldNames` and call `typeof(INV_MainPanel).GetField(...)` per iteration, at runtime, every time a list is built or a comparison is computed. Since `equipmentSlotFieldNames` is static and fixed, the corresponding `FieldInfo[]` array should also be cached statically (populated in `CacheReflection()`) so reflection lookup happens only once total rather than on each build and each comparison.

### Finding 7: `AnnounceDetailedInfo()` duplicates item-retrieval logic from `GetCurrentDragDropItem()` / `GetCurrentItemInstance()`
- Lines: 1688–1724
- Removed: ~8 (consolidation, not removal)
- Risk: low
- Detail: `AnnounceDetailedInfo()` has two separate code paths for Equipment zone vs other zones, each manually reaching into `currentList` to get the `ItemInstance`. Both `GetCurrentItemInstance()` (line 1603) and `GetCurrentDragDropItem()` (line 1813) already provide the same access with proper null guarding. The method can be simplified to call `GetCurrentItemInstance()` then `GetCurrentDragDropItem()` for the slot-name part, collapsing ~20 lines to ~10.

### Finding 8: `AnnounceDescription()` also duplicates equipment-vs-backpack item retrieval
- Lines: 1727–1763
- Removed: ~12
- Risk: low
- Detail: Same duplication pattern as Finding 7. The first half of `AnnounceDescription()` is a hand-written version of `GetCurrentItemInstance()`. Replacing it with a single `var item = GetCurrentItemInstance();` call eliminates the two-branch duplication and about 12 lines.

### Finding 9: `SwitchContainer()` re-finds `PopupInventoryMenu` when it was already just found in the caller chain
- Lines: 720–752
- Removed: 0 (style/efficiency)
- Risk: low
- Detail: `SwitchContainer()` calls `FindObjectOfType<PopupInventoryMenu>()`. So do `TakeAll()` (line 848), `DistributeAll()` (line 858), `CloseLoot()` (line 884), `TransferCurrentItem()` (line 831), `SwitchPopupPartyMember()` (line 968), and `BuildContainerItemList()` (line 604). All popup-path actions call `FindObjectOfType` independently. This is already the pattern used throughout the file, so not adding more, but note that all `isPopupInventoryMenu` paths repeat this lookup when a single helper `GetPopupInventoryMenu()` returning a cached (or single-call) instance would be cleaner.

### Finding 10: Duplicate `"No item selected"` string in `OpenContextMenuOnCurrentItem()`, `TransferCurrentItem()`, `QuickEquipUnequip()`
- Lines: 763, 827, 794
- Removed: 0 (const extraction)
- Risk: low
- Detail: The literal string `"No item selected"` appears three times. Extract to a `private const string NoItemSelected = "No item selected";` or a shared method. Low effort, consistent with how other states handle repetitive announcements.

### Finding 11: `HandleInfoBrowserInput()` — Home/End blocks are slightly inconsistent with Up/Down
- Lines: 1069–1087
- Removed: 0
- Risk: low
- Detail: Up and Down wrap-around unconditionally. Home and End guard with `if (infoLines.Count > 0)` before setting index and speaking. The guard is harmless but means the two key pairs have slightly different defensive styles. If `infoLines` is empty, `isInfoBrowsing` would have been false (set `true` only after confirming `infoLines.Count > 0` in `OpenInfoBrowser()`), so the guard is redundant. Minor inconsistency worth noting; no risk to remove the `Count > 0` guards on Home/End.

### Finding 12: `GetContainerName()` falls back to `"Unknown"` on null popup but `"Container"` on missing label
- Lines: 1881–1890
- Removed: 0
- Risk: low
- Detail: The method returns `"Unknown"` if `popupInv == null` and `"Container"` if `sourceLabel` is null or empty. The caller (`AnnounceLootContext`, `SwitchContainer`) uses this to build an announcement. The two different fallback strings could confuse screen-reader users ("Loot: Unknown" vs "Container: Container"). Standardise both to `"Container"` or `"Unknown"`.

### Finding 13: `try/catch` in `BuildInfoLines()` for ItemInfoBox label scraping swallows all exceptions silently except a `Warning` log
- Lines: 1368–1436
- Removed: 0
- Risk: low
- Detail: The `try` block reads six optional labels from `ItemInfoBox`. Catching `Exception e` and logging only the message (not the stack trace) with `MelonLogger.Warning` is adequate for optional UI scraping, but the catch is broader than it needs to be. Since the six label reads are all simple property accesses, null-guarding each one individually (without a blanket try/catch) would be cleaner and would surface any unexpected field-access error rather than silently degrading.

---

## States/CharacterState.cs

### Finding 1: Four reflection fields cached but never read after caching
- Lines: 59 (`skillCurrentCategoryField`), 63 (`addCharCurrentEntryField`), 64 (`traitCurrentEditorField`), 65 (`statDisplayListField`); corresponding cache lines 233, 236, 238, 242
- Removed: ~12
- Risk: low
- Detail: `skillCurrentCategoryField`, `addCharCurrentEntryField`, `traitCurrentEditorField`, and `statDisplayListField` are all populated in `CacheReflection()` but searched with `.GetValue()` nowhere in the file. `skillCurrentCategoryField` was likely superseded by `GetActiveSkillGrid()`/`GetActiveSkillCategory()` which directly inspect `activeInHierarchy`. `addCharCurrentEntryField` and `traitCurrentEditorField` may have been intended for selection tracking that was never implemented. `statDisplayListField` was likely an early approach to the derived-stats browser before `CharacterAnnouncementHelper.DerivedStatNames` was used. All four are dead weight.

### Finding 2: `GetTraitDescription(CHA_TraitEditor editor)` is a pure one-line pass-through never called within the file
- Lines: 1705–1708
- Removed: ~5
- Risk: low
- Detail: `GetTraitDescription` just delegates to `CharacterAnnouncementHelper.GetTraitDescription(editor)`. The only place trait descriptions are used is `HandleTraitsInput()` (line 1506), which calls `CharacterAnnouncementHelper.GetTraitFromEditor()` directly and then passes the result to `OpenInfoBrowser()` — it never calls `GetTraitDescription` at all. The wrapper method is dead. Remove it; if needed, callers can invoke `CharacterAnnouncementHelper.GetTraitDescription` directly.

### Finding 3: `GetAttributeEditorAnnouncement`, `GetSkillEditorAnnouncement`, `GetTraitEditorAnnouncement` are trivial one-line wrappers
- Lines: 852–865
- Removed: ~12 (inline into `GetControlAnnouncement`)
- Risk: low
- Detail: All three are private methods consisting solely of `return CharacterAnnouncementHelper.XxxAnnouncement(editor);`. The indirection adds no value since `GetControlAnnouncement()` is the only caller of all three. Inlining the delegate calls directly into the `if` branches at lines 774–784 eliminates three two-line wrapper methods.

### Finding 4: `HandleFlavorInput` is a single-line delegator to `HandleDossierInput`
- Lines: 1590–1593
- Removed: ~4
- Risk: low
- Detail: `HandleFlavorInput` exists only to call `HandleDossierInput(screen)`. The switch in `HandleInput()` (line 167) can directly map `Flavor` to `HandleDossierInput(charScreen)`, or the entry can remain as a comment-only note. Either way the method body is redundant. Same applies to `BuildFlavorControls` (line 714–718) which calls `BuildDossierControls(screen)` directly — it is a two-line wrapper that adds nothing, given `BuildControlList`'s switch also has a `Flavor` arm.

### Finding 5: `HandlePartyInput()` does an uncached `typeof(CHA_PartyPanel).GetMethod("OnPartyEntryClicked", ...)` on every Enter keypress
- Lines: 1127–1138
- Removed: 0 (caching improvement)
- Risk: low
- Detail: On every Enter press while in the Party panel, the code calls `typeof(CHA_PartyPanel).GetMethod("OnPartyEntryClicked", BindingFlags.NonPublic | BindingFlags.Instance)` at runtime. This is the same pattern that `CacheReflection()` avoids for all other methods. A `private static MethodInfo onPartyEntryClickedMethod` should be added alongside `onDoneClickedMethod` and populated in `CacheReflection()`.

### Finding 6: Up/Down direction-from-keycode is repeated in six panel input handlers
- Lines: 1086–1087, 1110–1111, 1205–1207, 1289–1291, 1399–1401, 1479–1480, 1523–1525, 1599–1601
- Removed: 0 (helper extraction)
- Risk: low
- Detail: The pattern `int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;` (preceded by `if (Input.GetKeyDown(UpArrow) || GetKeyDown(DownArrow))`) appears in every panel handler verbatim. A tiny local helper `private static int GetVerticalDirection()` returning `Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1` (guarded by the caller's existing `||` check) would dedup the direction extraction. Low importance but consistent with the codebase's style of reducing repeated boilerplate.

### Finding 7: `HandleAttributesInput` and `HandleSkillsInput` share ~60 lines of identical edit-mode boilerplate
- Lines: 1256–1284 (attrs edit mode), 1366–1394 (skills edit mode)
- Removed: ~30 (with extraction)
- Risk: medium
- Detail: Both methods open with an identical `if (isEditingValue) { ... }` block: same Enter/Escape exit with `"Done editing"`, same +/- and Left/Right adjust keys (differing only in calling `AdjustCurrentAttribute` vs `AdjustCurrentSkill`), same I-key stat description call, same Up/Down block. A private helper `HandleEditModeInput(Action<int> adjust)` taking a direction-to-adjust delegate would capture the shared pattern. Medium risk because the two adjust callbacks (`AdjustCurrentAttribute`, `AdjustCurrentSkill`) have different types and the extract would need to use `Action<int>` or a local lambda — verify nothing subtle differs before doing this.

### Finding 8: `AnnounceAttributePointsRemaining` and `AnnounceSkillPointsRemaining` are structurally identical
- Lines: 1667–1683, 1685–1701
- Removed: ~14 (with shared helper)
- Risk: low
- Detail: Both methods have the same shape: null-guard the panel, `try { points = panel.GetPointsRemaining(); title = panel.pointsRemainingTitleLabel?.text ?? fallbackString; SpeakInterrupt($"{points} {title}"); } catch { Warning(...) }`. The only differences are the panel type, the log prefix, and the fallback label string. Extract to `private void AnnouncePointsRemaining(int points, UILabel titleLabel, string fallback)` and call from both sites.

### Finding 9: `SwitchSkillCategory` re-detects current category with its own activeInHierarchy checks instead of reusing `GetActiveSkillCategory()`
- Lines: 1625–1632
- Removed: ~8
- Risk: low
- Detail: `SwitchSkillCategory` manually checks `combatGrid.activeInHierarchy`, `knowledgeGrid.activeInHierarchy`, `generalGrid.activeInHierarchy` to determine `current` (0/1/2). `GetActiveSkillCategory()` does exactly this but returns a string. A small private helper `GetActiveSkillCategoryIndex(CharacterScreen screen) : int` (returning 0/1/2) would serve both `SwitchSkillCategory` and `GetActiveSkillCategory()`, eliminating the duplication.

### Finding 10: `OnActivated()` and `OnDeactivated()` reset largely the same set of fields — some diverge silently
- Lines: 173–200 (OnActivated), 203–219 (OnDeactivated)
- Removed: 0
- Risk: low (observation)
- Detail: `OnActivated` resets 14 fields before calling `CacheReflection` and `OnPanelChanged`. `OnDeactivated` resets 10 fields, notably omitting `isEditingTextField` and `initialAnnouncementDone`. `isEditingTextField` is reset in `OnDeactivated` (line 206), so that is fine. `initialAnnouncementDone` is only meaningful during the active window and is reset in `OnPanelChanged`, so omitting it from `OnDeactivated` is also fine. The comment gap between the two is just worth noting — if fields are added in the future, both reset sites must be updated.

### Finding 11: `BuildAttributeControls` and `BuildSkillControls` share the same child-gather-and-sort-by-name pattern
- Lines: 514–533 (attrs), 536–568 (skills)
- Removed: ~12 (with shared helper)
- Risk: low
- Detail: Both methods iterate a `UIGrid`'s children, collect active `Transform` instances into a local `List<Transform>`, sort by `string.Compare(a.name, b.name, StringComparison.Ordinal)`, then add the `GameObject`s to `controlList`. The only difference is that `BuildSkillControls` additionally skips disabled `CHA_SkillEditor` components. A shared helper `private void AddSortedGridChildren(UIGrid grid, Predicate<Transform> filter = null)` would eliminate the ~12 duplicate lines.

### Finding 12: `OpenInfoBrowser(InfoMode.None, ...)` default case creates a new `List<string>` instead of using the existing `infoLines`
- Lines: 1816
- Removed: 0 (allocation)
- Risk: low
- Detail: The `default:` arm of the `switch` in `OpenInfoBrowser` assigns `infoLines = new List<string>()`. But `infoLines` already exists and would be cleared by the `infoLines.Clear()` path if the function proceeded. The `None` mode arm is dead in practice (it's never called with `None`), but replacing `new List<string>()` with `infoLines.Clear(); infoLines` would avoid the spurious allocation and keep a single `infoLines` instance.

### Finding 13: `CloseInfoBrowser()` sets `EventManager.ignoreNextBack = true` but `CloseDerivedStatsBrowser()` does the same — inconsistent position relative to the speech call
- Lines: 1841 (CloseInfoBrowser), 1725 (CloseDerivedStatsBrowser)
- Removed: 0
- Risk: low (observation)
- Detail: Both close methods set `EventManager.ignoreNextBack = true` before speaking. This is correct. `CloseInventory()` and `CloseLoot()` in `InventoryState` do the same. The inconsistency is that `CloseDerivedStatsBrowser` sets `lastAnnouncedText = null` before re-announcing while `CloseInfoBrowser` does the same — both are consistent with each other. Just noting that the `ignoreNextBack` guard appears in four close methods across the two files; if the pattern ever changes, all four need updating.

---

## Cross-file Findings (InventoryState ↔ CharacterState)

### Cross-file Finding 1: Info browser navigation is implemented twice with different variable names
- InventoryState lines: 1049–1100 (`HandleInfoBrowserInput`, `infoLines`/`infoLineIndex`)
- CharacterState lines: 1858–1897 (`HandleInfoInput`, `infoLines`/`infoIndex`)
- Removed: ~40 in shared helper
- Risk: medium
- Detail: Both files implement a browsable line list with Up/Down/Home/End/Escape navigation and announce `"{line}, {N} of {M}"`. The only differences are: the field names (`infoLineIndex` vs `infoIndex`), the close action (InventoryState speaks `"Closed item info"` and sets `isInfoBrowsing = false`; CharacterState calls `CloseInfoBrowser()` which also re-announces the focused control), and InventoryState additionally closes on `I` key. A shared helper `InfoBrowserNavigator` (or a base struct holding `List<string>`, index, and a close callback) would unify this pattern. Currently worth flagging; extraction is straightforward but touches both files.

### Cross-file Finding 2: Both files compute `Up = -1 / Down = +1` navigation direction inline everywhere
- InventoryState lines: 319/324 (up/down separate `if`s), 419/424, and implicitly via `NavigateList(-1)/NavigateList(1)` calls
- CharacterState lines: 1087, 1111, 1207, 1291, 1401, 1480, 1525, 1601
- Removed: 0 (tiny)
- Risk: low
- Detail: InventoryState uses separate `if (GetKeyDown(UpArrow))` / `if (GetKeyDown(DownArrow))` blocks (calling `NavigateList(-1)` and `NavigateList(1)` respectively). CharacterState uses a combined check plus the ternary `dir = UpArrow ? -1 : 1`. Both approaches are readable, but the inconsistency means that a future reader must understand both idioms. The combined-check + ternary approach (CharacterState's style) is more compact; InventoryState's input handlers could be updated for consistency.

### Cross-file Finding 3: Both files have a `GetCurrentPC()` method that falls back to `Game.GetFirstSelectedPC()` — identical fallback logic
- InventoryState lines: 1826–1854
- CharacterState lines: 1795–1800 (inline in `AnnounceCharacterSummary`)
- Removed: 0 (observation)
- Risk: low
- Detail: InventoryState has a full `GetCurrentPC()` helper. CharacterState has `GetCurrentPC(CharacterScreen screen)` for reflection-based retrieval and then repeats the `Game.GetFirstSelectedPC()` fallback inline in `AnnounceCharacterSummary`. The two files use different game-object sources (CharacterInfoMenu vs CharacterScreen) so a single shared utility is not directly possible, but the fallback pattern is identical boilerplate across both.

### Cross-file Finding 4: `MelonLogger.Msg` prefix tags are inconsistent in verbosity
- InventoryState: uses `[InventoryState]` consistently
- CharacterState: uses `[CharacterState]` consistently
- Risk: low (observation)
- Detail: Both files use their class name as a prefix, which is good. No action needed, just confirming the pattern is consistent.
