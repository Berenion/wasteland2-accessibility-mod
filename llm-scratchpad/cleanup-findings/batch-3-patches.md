# Cleanup findings — Patches

---

## Patches\UIDialogPatches.cs

### Finding 1: Entire file is a dead stub — delete it
- Lines: 1-10
- Removed: ~10
- Risk: low
- Detail: The file contains only a namespace block and a multi-line comment explaining why `ModalMessageMenu_SetMessage_Patch` was removed. There is no remaining code. The `using HarmonyLib;` and `using System;` at the top are therefore unused. The file should be deleted; no functionality lives here and it appears only in the codebase as noise.

---

## Patches\CharacterInfoPatches.cs

### Finding 2: Entire file is an empty stub — delete it
- Lines: 1-14
- Removed: ~14
- Risk: low
- Detail: The file declares `public static class CharacterInfoPatches {}` with no members inside. Three `using` directives (`HarmonyLib`, `MelonLoader`, `UnityEngine`, `States`) are all dead weight. The XML doc summary says "Patches for the CharacterInfoMenu" but nothing has been implemented. Either the class was scaffolded and then its patches were moved elsewhere, or it is waiting for future work. Either way there is nothing here to keep.

---

## Patches\CharacterCreationPatches.cs

### Finding 3: Stale comment at top referencing removed patch
- Lines: 7
- Removed: 1
- Risk: low
- Detail: `// CHA_UsePremadePartyPanel_OnEnable_Patch removed - CharacterState handles panel announcements` is a removal note that no longer conveys actionable information. This style of "graveyard comment" can be deleted now that the transition is stable.

### Finding 4: `difficultyLevel switch` expression requires C# 8+, mismatch with .NET 3.5 target
- Lines: 31-37
- Removed: 0 (may need expansion, not reduction)
- Risk: medium
- Detail: The file uses a `switch` expression (`difficultyLevel switch { ... }`) which is a C# 8 feature. The project targets `.NET Framework 3.5` (Unity 4.x). This compiles only because Roslyn allows the syntax regardless of framework target, but it is worth flagging as inconsistent with the stated target and the equivalent `switch` statement pattern used everywhere else in the mod. Not a runtime risk, but a style inconsistency.

---

## Patches\CharacterScreenPatches.cs

### Finding 5: `CharacterScreenPatches.FormatStatName` replaces only "AP", "CON", "HP" — very narrow scope
- Lines: 20-33
- Removed: 0
- Risk: low
- Detail: The method expands only three abbreviations but then `CHA_StatDisplay_SetValue_Patch` and `CHA_StatDisplay_SetSelected_Patch` both call it before constructing an announcement. This is fine, but the method name implies general formatting; the narrow scope should either be documented or the method inlined, since expanding three magic strings inline would be equally readable and remove the static helper dependency.

### Finding 6: `CHA_AttributePanel_PopulateData_Patch` has no `IsManagedNavigation` guard — may double-speak with `CharacterState`
- Lines: 41-56
- Removed: 0 (guard line to add)
- Risk: low
- Detail: Every comparable patch in `InventoryPatches.cs` and `ShopPatches.cs` guards with `if (XState.IsManagedNavigation) return;`. This patch fires whenever the attribute panel is repopulated (which includes during CharacterState navigation). A guard against `CharacterState.blockUIInput` or similar would prevent a second announcement when the state already narrates the panel change. Not a silent bug today only because the announcement is non-interrupting, but it is asymmetric with the rest of the codebase.

### Finding 7: Identical `Prefix()` body duplicated across `UIInput_ProcessEvent_Patch` and `UIInput_Update_Patch`
- Lines: 212-235
- Removed: ~8
- Risk: low
- Detail: Both patches return `!CharacterState.blockUIInput && !CharacterInfoState.blockUIInput && !GenericMenuState.blockUIInput`. The logic is identical. A shared static method (e.g., `CharacterScreenPatches.ShouldAllowUIInput()`) would express the rule once and make a future flag addition a one-line change rather than two.

### Finding 8: Stale comment at line 139
- Lines: 139
- Removed: 1
- Risk: low
- Detail: `// CharacterScreen_GoToPanel_Patch removed - CharacterState handles panel change announcements` is a graveyard comment with no actionable information. Can be deleted.

---

## Patches\CharacterSelectionPatches.cs

### Finding 9: `try/catch` in `PC_MakeLeader_Patch` wraps everything including the null-safe early-exit guard
- Lines: 19-41
- Removed: 0 (refactor only)
- Risk: low
- Detail: The `try` block begins before the `if (__instance == null)` check; a `NullReferenceException` from `.GetInstance()` would be caught and silently logged as "Error in PC_MakeLeader_Patch". This is fine for resilience, but the guard pattern is inverted relative to the rest of the codebase which guards at the top without try/catch. Cosmetic inconsistency only.

### Finding 10: `GetCharacterName` helper method does two `displayName` lookups with different field paths (`template` vs `pcTemplate`)
- Lines: 44-62
- Removed: 0
- Risk: low
- Detail: Worth a comment clarifying the difference between `pc.template` and `pc.pcTemplate` (or confirming which one is canonical). Without it, future maintainers may assume they are aliases and collapse the fallback chain. This is a documentation gap, not a code defect.

---

## Patches\CombatMovementPatches.cs

### Finding 11: Guards repeated identically in both `Mob_StartedMoving_Patch` and `Mob_FinishedMoving_Patch`
- Lines: 26-30 and 48-52
- Removed: ~5
- Risk: low
- Detail: Both postfixes open with the same four-line block: `if (__instance == null || __instance is PC) return; if (!MonoBehaviourSingleton<CombatManager>.HasInstance()) return; if (!MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat) return; if (__instance.currentSquare == null) return;`. Extracting a private `static bool ShouldProcess(Mob mob)` helper would halve the repeated code.

### Finding 12: `MoveStart` dictionary never purged on scene reload or combat end
- Lines: 19
- Removed: 0 (a `Clear()` call needed elsewhere)
- Risk: low
- Detail: `Mob_StartedMoving_Patch.MoveStart` is a `static Dictionary<Mob, Vector3>` that accumulates entries for the lifetime of the process. Mobs are destroyed between scenes; the old `Mob` keys become stale garbage-collectable objects but the dictionary holds strong references, preventing GC. A `Reset()` call (mirroring `WorldMapRadiationCloud_CheckDiscovery_Patch.Reset()`) on combat end or scene load would prevent unbounded growth in long play sessions.

---

## Patches\ConversationPatches.cs

### Finding 13: `buttonList` field looked up via reflection twice — identical code in `OnTopicPressed` and `OnTopicMouseOver`
- Lines: 349-395 and 469-535
- Removed: ~25
- Risk: low
- Detail: Both patches call `typeof(ConversationHUD).GetField("buttonList", NonPublic | Instance)`, then iterate `buttonList` looking up `gobButton`, `sayRangerText`, and `keywordInfo` fields on each entry. This is ~45 lines of verbatim duplication. A private static helper `TryGetButtonInfo(ConversationHUD hud, GameObject button, out string fullText, out string skillInfo, out string additionalInfo)` would centralise the reflection and remove the duplication entirely.

### Finding 14: `buttonList` `FieldInfo` is re-fetched on every call — not cached at class level
- Lines: 349, 469
- Removed: 0 (one static field to add, two local vars to remove)
- Risk: low
- Detail: `GetField` is called on every `OnTopicPressed` and `OnTopicMouseOver` invocation. Since the field is on a fixed class, caching it as `private static readonly FieldInfo s_buttonListField = typeof(ConversationHUD).GetField(...)` in a shared location (or in the patch class) would avoid repeated reflection cost. Same for `gobButtonField`, `sayRangerTextField`, and `keywordInfoField` which are re-fetched per `btnInfo` element inside the loop.

### Finding 15: `ConversationHUD_RemoveButton_Patch` does nothing but log — could be removed or collapsed into a comment
- Lines: 295-311
- Removed: ~16
- Risk: low
- Detail: The entire `Postfix` body is `MelonLogger.Msg($"[Conversation] Button removed: {keywordLabel}")` wrapped in a `try/catch`. There is no accessibility announcement and the comment says "useful for debugging". This is debug-only scaffolding. If the log is needed during active development it should be guarded by a `#if DEBUG` directive; otherwise the patch (and its associated Harmony overhead) should be removed.

### Finding 16: `ConversationHUD_Clear_Patch` does nothing but log — same issue
- Lines: 596-610
- Removed: ~14
- Risk: low
- Detail: `Postfix` body is `MelonLogger.Msg("[Conversation] Options cleared")` inside `try/catch`. No accessibility announcement. This is debug scaffolding with Harmony overhead. Remove or `#if DEBUG`-gate.

### Finding 17: `ConversationHUD_AddButton_Patch` is dead when `Drama.isConversationOn` (lines 206-209) — but also fires during non-conversation UI buttons
- Lines: 205-209
- Removed: 0
- Risk: low
- Detail: The early-exit `if (Drama.isConversationOn) return;` means this patch never announces during actual conversations (ConversationState handles those). In practice `AddButton` is only called during conversations, so this guard makes the entire patch a no-op during its only use case. Either the guard was added when the ConversationState was introduced and the patch's remaining purpose should be clarified, or the patch can be removed. This is worth confirming before removing.

### Finding 18: Magic constant `2.0f` (button reset timeout) and `0.5f` (duplicate window) as inline literals
- Lines: 227, 121
- Removed: 0 (name them)
- Risk: low
- Detail: Two `float` literals control deduplication windows in `AddButton` and `AddText` patches. They appear only once each so there is no duplication risk, but naming them (e.g., `const float ButtonResetWindowSeconds = 2.0f`) would improve readability with minimal effort.

---

## Patches\DescriptionPatches.cs

### Finding 19: `MAX_LOG_ENTRIES = 100` magic constant is a private `const` but not named at use site
- Lines: 14
- Removed: 0
- Risk: low
- Detail: The constant is already named and declared correctly. No issue beyond noting it is fine.

*(No other findings for DescriptionPatches.cs — clean file.)*

---

## Patches\DiaryPatches.cs

### Finding 20: `announcement.Length > 0` check is redundant after `string.IsNullOrEmpty` guard above
- Lines: 34
- Removed: 1
- Risk: low
- Detail: At line 27 there is `if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(entry)) return;`. On line 34, `if (announcement.Length > 0) announcement += ". ";` rechecks emptiness. Given the control flow — `announcement` is assigned to `title` only when `!string.IsNullOrEmpty(title)` — the `Length > 0` check is always true when reached. Replace with unconditional `announcement += ". ";` or restructure.

---

## Patches\ExplorationNavigationPatches.cs

### Finding 21: Three `InteractableTeleporter` diagnostic patches fire on every Poked/Activate/DoTeleport — production overhead
- Lines: 45-73
- Removed: ~30
- Risk: low
- Detail: `InteractableTeleporter_Poked_Patch`, `InteractableTeleporter_Activate_Patch`, and `InteractableTeleporter_DoTeleport_Patch` each have a `[HarmonyPrefix]` that does nothing except `MelonLogger.Msg(...)`. They are labelled "Diagnostic" in a comment. These patches add Harmony overhead on three frequently-called methods with no accessibility benefit. They should be removed or gated with `#if DEBUG`.

### Finding 22: `AZ10_HiddenShortcut_Examine_Patch` is a scenario-specific diagnostic patch that iterates `InteractableNexus.interactables` on every examine
- Lines: 75-168
- Removed: ~94
- Risk: low
- Detail: This patch was clearly written to debug a specific level (AZ10 Hidden Shortcut). It logs detailed door state, party positions, nexus visibility counts, and teleporter states. It calls `RegisterDoorIfActive` as a side-effect which has real runtime purpose (marking recently-activated doors for FOW bypass), but the rest of the logging is diagnostic scaffolding. The `RegisterDoorIfActive` side-effect should be preserved; the rest should be extracted to debug logging or removed. As written, the patch logs 8-15 lines to the MelonLoader log on every AZ10 shortcut examine.

---

## Patches\FloatingTextPatches.cs

*(No findings — clean, well-focused file.)*

---

## Patches\FOWPatches.cs

*(No findings — single-purpose, minimal file.)*

---

## Patches\InventoryFormatting.cs

### Finding 23: `FormatItemAnnouncement` has a dead `if (item is ItemInstance_Equipment equipment)` block with empty body
- Lines: 67-71
- Removed: ~4
- Risk: low
- Detail: The `if (item is ItemInstance_Equipment equipment)` branch inside the `detailed` block declares a local variable `equipment` and contains only a comment `// Check if this item is currently equipped` followed by nothing. The local variable `equipment` is therefore unused and the block adds no code. Remove the entire `if` body (or replace with the intended implementation).

### Finding 24: `BuildEnergyWeaponLines` reads `energyPenetratedMultiplier` for both `above` and `below` for melee energy weapons — acknowledged as intentional game mirror but labeled as "not a bug we need to fix"
- Lines: 778-781
- Removed: 0
- Risk: low
- Detail: The comment `// ItemInfoBox reads the same field twice for melee — not a bug we need to "fix"` is accurate and the intent is clear. The quotation marks around "fix" and the awkward phrasing could be simplified: `// Matches ItemInfoBox.cs: melee energy uses energyPenetratedMultiplier for both thresholds.`

### Finding 25: `InventoryFormatting.lastAnnouncedItem` is an `internal static string` but is mutated from two separate files (`InventoryPatches.cs` and itself)
- Lines: 15 (InventoryFormatting.cs), 75 and 323 (InventoryPatches.cs)
- Removed: 0
- Risk: low
- Detail: This is a design observation rather than a defect: the dedup field lives in `InventoryFormatting` but is written from `InventoryPatches`. This is intentional (both live in the same `Patches` namespace), but a brief comment on the field would clarify the shared-ownership contract.

---

## Patches\InventoryPatches.cs

### Finding 26: `INV_DragDropItem_OnEnable_Patch` is an effectively empty patch — delete it
- Lines: 43-52
- Removed: ~9
- Risk: low
- Detail: The `Postfix` body contains only a comment saying "We'll rely on the focus system to handle this via UICamera patches." There is no code. The Harmony patch class incurs registration overhead and shows up in the patch list for no benefit. The class should be deleted.

### Finding 27: `using System.Linq;` is unused
- Lines: 5
- Removed: 1
- Risk: low
- Detail: `System.Linq` is imported but no LINQ methods are called anywhere in the file. All iteration is done via `for` loops. Safe to remove.

### Finding 28: `InventoryGrid_Reposition_Patch` uses `FindObjectOfType<PopupInventoryMenu>()` and `FindObjectOfType<CharacterInfoMenu>()` on every Reposition call — expensive per-frame searches
- Lines: 232-234
- Removed: 0 (optimization, not deletion)
- Risk: low
- Detail: `FindObjectOfType` performs a full scene-graph search. `Reposition` is called whenever the inventory grid changes layout (item add/remove, panel switch, equipment change) — potentially many times per second during active play. Caching the result or using a lighter check (e.g., checking `GUIManager.IsAnyMenuActive()` or a state flag) would be more efficient. The comment already explains why the transform-based guard was abandoned, so this is a known trade-off.

### Finding 29: `OpenContextMenu_GiveTo_Patch.screensField` is re-fetched via reflection on every context menu open
- Lines: 455
- Removed: 0 (one static field to add)
- Risk: low
- Detail: `typeof(GUIManager).GetField("screens", NonPublic | Instance)` is called inside the postfix every time a context menu opens. Caching this as a `static readonly FieldInfo` at class level would avoid the reflection overhead.

---

## Patches\KeypadMenuPatches.cs

*(No findings — minimal, focused, correct.)*

---

## Patches\MouseInputPatches.cs

*(No findings — single-purpose block returning false.)*

---

## Patches\SaveLoadPatches.cs

### Finding 30: `sortModeField` lazy-init in `SaveLoadScreen_InitializeSort_Patch` — use static readonly instead
- Lines: 22-33
- Removed: ~3
- Risk: low
- Detail: The `sortModeField` field is lazy-initialised with a null-check on every `Prefix()` call. Since `typeof(SaveLoadScreen).GetField(...)` is deterministic and cheap at startup, declaring it `private static readonly FieldInfo sortModeField = typeof(SaveLoadScreen).GetField("sortMode", ...)` removes the null-check boilerplate and communicates intent (the field never changes).

### Finding 31: `SaveLoadScreen_PopulateData_Patch.Prefix` re-fetches `saveGrid` via reflection on every `PopulateData` call — should be cached
- Lines: 49-53
- Removed: ~4
- Risk: low
- Detail: `typeof(SaveLoadScreen).GetField("saveGrid", Public | Instance)` is called on every `PopulateData` invocation. `saveGrid` is a public field, so it could also be accessed directly if the class is accessible; otherwise cache as a `static readonly FieldInfo`.

---

## Patches\ShopPatches.cs

### Finding 32: Stale double XML doc comment at lines 10-14 and 15-18 (two `<summary>` blocks for the file, then one per class)
- Lines: 10-18
- Removed: ~4
- Risk: low
- Detail: There are two `/// <summary>` doc blocks at the top of the file before the first class: the file-level summary at lines 10-14 (inside the namespace) and then the per-class `VendorScreen_SetSellMode_Patch` summary at lines 15-18. The file-level floating comment is not attached to any declaration, so it is effectively a stale comment. Remove it or move it to a file-header `//` comment.

*(No other findings for ShopPatches.cs.)*

---

## Patches\TutorialPatches.cs

### Finding 33: `TUT_TutorialPopup_OnOkayClicked_Patch` has a non-obvious early return on success path with no comment explaining why
- Lines: 91-99
- Removed: 0
- Risk: low
- Detail: The logic `if (nextTutorial != null && !__instance.checkbox.value) return;` returns early (no announcement) when there is a valid next page and tutorials are not disabled. The variable `__instance.checkbox.value` controls "disable tutorials"; when it's true the tutorial is being skipped to closure even with a next page. A one-line comment would clarify the conditional for future readers.

*(No other significant findings for TutorialPatches.cs.)*

---

## Patches\UIDialogPatches.cs

*(Already covered as Finding 1 above.)*

---

## Patches\UIDropdownPatches.cs

*(No findings — minimal, correct.)*

---

## Patches\UIFocusPatches.cs

### Finding 34: `UIFocusPatches.SuppressAnnouncements` public property is set but also `InputRouter.IsAnyStateActive()` is checked — `SuppressAnnouncements` may be redundant
- Lines: 19-36
- Removed: 0 (verify before removing)
- Risk: low
- Detail: `HandleFocusChange` bails on `SuppressAnnouncements` first, then on `InputRouter.IsAnyStateActive()`. If every caller that sets `SuppressAnnouncements = true` also corresponds to an active InputRouter state, the first guard is made redundant by the second. Worth auditing callers of `SuppressAnnouncements` to determine if it can be removed and the second guard relied on exclusively. (If no callers remain after the state migration, the property is dead code.)

### Finding 35: Deduplication logic in `HandleFocusChange` allows the same text to be spoken again if `source` differs
- Lines: 61-63
- Removed: 0
- Risk: low
- Detail: The guard `if (text == lastSpokenText && source != lastSource) return;` suppresses only when both text and source match a previous call. When `SetSelection` and `Notify` both fire for the same element (different sources, same text), the second call is suppressed — correct. But when the same element fires the same source twice in a row (e.g., rapid re-focus), the check passes because `source == lastSource` makes the condition false. The intent is likely `text == lastSpokenText` alone; the `source` field seems vestigial.

---

## Patches\UISliderPatches.cs

### Finding 36: `SLIDER_DEBOUNCE_TIME` constant named with `SCREAMING_SNAKE_CASE` inconsistently — codebase uses `camelCase` elsewhere
- Lines: 17
- Removed: 0
- Risk: low
- Detail: The rest of the codebase does not use all-caps constants. The `TOOLTIP_DEBOUNCE_TIME` in `UITooltipPatches` has the same style. Minor style inconsistency; could be renamed to `SliderDebounceTime` to match C# `const` conventions, but this is cosmetic only.

### Finding 37: `isDifferentSlider` is computed but then only used in a combined condition — logic could be simplified
- Lines: 47, 62
- Removed: 0
- Risk: low
- Detail: `bool isDifferentSlider = lastSlider != __instance;` is computed before `valueChanged` and `enoughTimePassed`. The guard on line 62 is `if (!isDifferentSlider && !stepChanged && !enoughTimePassed)`. Rearranging to a positive-condition early-exit for different slider (`if (isDifferentSlider) { /* announce */ }`) would clarify the "always announce on first focus" path.

---

## Patches\UITogglePatches.cs

*(No findings — clean, minimal.)*

---

## Patches\UITooltipPatches.cs

### Finding 38: `UITooltipPatches` static class holds only two `internal static` fields — these could live on `TooltipManager_SetPopup_Patch` directly
- Lines: 11-16
- Removed: ~5
- Risk: low
- Detail: The `UITooltipPatches` class exists solely to hold `lastTooltipText` and `lastTooltipTime` so `TooltipManager_SetPopup_Patch` can access them. Since the only consumer is in the same file, these fields could be declared directly on `TooltipManager_SetPopup_Patch` as private statics, eliminating the extra class and the `internal` visibility exposure.

### Finding 39: `UITooltip_SetText_Patch` and `TextTooltip_SetText_Patch` have identical four-line bodies
- Lines: 27-39 and 49-62
- Removed: ~6
- Risk: low
- Detail: Both patches call `CleanText`, null-guard, `MelonLogger.Msg`, and `ScreenReaderManager.Speak`. The only difference is the prefix string in the log message. A shared private static helper `AnnounceTooltipText(string raw, string logPrefix)` would de-duplicate the logic.

---

## Patches\WorldMapPatches.cs

### Finding 40: `WorldMapPatchUtils.Vector2Distance` duplicates `Mathf.Sqrt((a-b).magnitude)` with manual dx/dz — `Vector3.Distance` on x/z plane would be equivalent and clearer
- Lines: 221-228
- Removed: ~4
- Risk: low
- Detail: The helper computes 2D distance manually on x/z. Unity's `Vector2.Distance(new Vector2(a.x,a.z), new Vector2(b.x,b.z))` or simply `Mathf.Sqrt(dx*dx+dz*dz)` (already what it does) with a comment would be fine. The utility class exists for only one consumer (`WorldMapPOI_Discover_Patch`); the helper could be inlined to avoid the extra class.

### Finding 41: `WorldMapRadiationCloud_CheckDiscovery_Patch.announcedClouds` HashSet is never reset between worlds — `Reset()` exists but is never called
- Lines: 136-144
- Removed: 0 (a call site needed)
- Risk: low
- Detail: `Reset()` is declared public but a `grep` across the codebase finds no call site. Cloud instance IDs may be reused between scenes or play sessions. Without `Reset()` being called on world map exit or scene load, previously-discovered clouds from a prior session will never be re-announced. Either wire up the call or note that instance IDs are globally unique across sessions.

### Finding 42: `WorldMapPOI_Instigate_Patch.Postfix` does nothing but log — same pattern as `RemoveButton`/`Clear` patches
- Lines: 108-126
- Removed: ~18
- Risk: low
- Detail: The entire body is a try/catch wrapping `MelonLogger.Msg($"[WorldMapState] Instigating POI: {name}")`. The comment explains the speech is delegated to `DialogState`. Remove the patch (eliminating Harmony overhead) and put the rationale in a code comment in `WorldMapState` or `DialogState` if it helps future readers.

---

## Cross-file findings

### Finding 43: `IsManagedNavigation` guard pattern inconsistently applied across patches that target the same state
- Files: InventoryPatches.cs, ShopPatches.cs, CharacterScreenPatches.cs
- Removed: 0 (additions needed)
- Risk: low
- Detail: `InventoryPatches` guards 8 patches with `InventoryState.IsManagedNavigation`. `ShopPatches` guards 4 patches. `CharacterScreenPatches` does not guard `CHA_AttributePanel_PopulateData_Patch` or `CHA_SkillPanel_PopulateData_Patch` with an equivalent `CharacterState` guard (see Finding 6). Auditing all population patches for the managed navigation guard would prevent double-announces when the owning state is active.

### Finding 44: Three `try/catch`-wrapped log-only patches (`RemoveButton`, `Clear`, `Instigate`) follow the same pattern — logging-only patch template adds Harmony overhead for zero accessibility gain
- Files: ConversationPatches.cs (×2), WorldMapPatches.cs (×1)
- Removed: ~48 total across three patches
- Risk: low
- Detail: Each patch wraps a single `MelonLogger.Msg` in `try/catch`. The `try/catch` exists to protect against null state on Postfix — unnecessary given the statements are just string formatting. Collectively these three patches contribute 3 Harmony trampolines and 48 lines for debug output that could instead be ordinary `MelonLogger` calls inside the patches that do real work nearby.

### Finding 45: Reflection `FieldInfo` objects fetched at call time rather than cached at class level — occurs in three separate files
- Files: ConversationPatches.cs (buttonList, gobButton, sayRangerText, keywordInfo), SaveLoadPatches.cs (sortMode, saveGrid), InventoryPatches.cs (screens)
- Removed: 0 (add ~6 static fields total)
- Risk: low
- Detail: In total, the codebase performs at least 6 `GetField` calls inside patch methods that run on game events. All of the target fields are on fixed, non-reloading game classes. Converting each to `private static readonly FieldInfo` at the class level would eliminate repeated reflection and is a straightforward low-risk change.
