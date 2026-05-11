# Current Status

## Working branch
`claude-mod-cleanup` (re-forked from `test` after the master/test mismatch was resolved)

## Default branch (target for eventual merge)
`test` — `master` is a stale v1.0 snapshot 149 commits behind and is not the active branch.

## Procedure source
`D:\Claude\Wasteland 2\llm-mod-refactoring-prompts\` — clone of https://github.com/ahicks92/llm-mod-refactoring-prompts

## Prompts completed
- `prompts/sanity-checks-setup.md`
- `prompts/information-gathering-and-checking.md`
- `prompts/code-directory-construction.md`
- `prompts/large-file-handling.md`
- `prompts/input-handling.md` — no changes; assessed the existing `Core/InputRouter` + `IAccessibilityState` + `Core/InputSuppressor` architecture as already meeting the prompt's target state-priority-router pattern. User chose to skip optional cleanups (KeyRepeat helper, decoupling state flags from suppressor, disambiguating priority-50 states).
- `prompts/string-builder.md` — confirmed the mod IS a string-builder mod (915 hits across 52 files; concentrated in the info-browser states and `InventoryFormatting.cs`). User chose to skip migration: existing `parts.Add() / string.Join(", ", ...)` pattern is uniform across the codebase, and migrating would risk wording regressions for announcements tuned over 149 commits. A `MessageBuilder` would mainly help new code; the win didn't justify the migration cost.
- `prompts/low-level-cleanup.md` — bundles A, B, C, D implemented and committed (24 commits total on branch `claude-mod-cleanup`). Bundle E still deferred. Also caught and fixed a pre-existing bug not in the original triage: shop info-browser Type line for weapons showed generic "Weapon" instead of the weapon-skill name ("Handguns" etc.) that inventory's info-browser uses — fixed in `532a20b` to match inventory's logic.

## Prompts pending
- `prompts/high-level-cleanup.md` (in progress — code review pass next)
- `prompts/finalization.md` (after high-level-cleanup)

## Deferred work for future session: Bundle E (method merges)

User approved bundles A, B, C, D for the current session and deferred Bundle E. Full triage with all 250 findings is in `llm-scratchpad/cleanup-findings/` (one file per batch; `PRIORITIZED.md` is the consolidated table). Bundle E specifically:

- **E1** — Merge `HandleAttributesInput`/`HandleSkillsInput` edit-mode block: `States/CharacterState.cs` (~60 lines duplicated) and `States/CharacterInfoState.cs` (~22 lines duplicated). Extract a parameterised handler. ~80 lines saved, **medium risk** — touches attribute/skill +/- input. Smoke-test in character creation + character info.
- **E2** — Extract shared info-browser navigator (Up/Down/Home/End/Escape over `List<string>`) implemented near-identically in `InventoryState`, `CharacterState`, `CharacterInfoState`. ~60 lines saved, **medium risk** — touches I-key info-browser flow.
- **E3** — Merge `MainMenuState.NavigateUp`/`NavigateDown` (mirror methods) + collapse 6 structurally identical `ButtonEntry` blocks in `RebuildButtonList`. ~40 lines, **low risk** (main menu only).
- **E4** — Merge `NavigationManager.NextCategory`/`PreviousCategory` and `CycleNext`/`CyclePrevious`; same pattern in `WorldMapNavigationManager`. ~35 lines, **low risk**.
- **E5** — Merge identical 4-line bodies in `Patches/UITooltipPatches.cs` (`UITooltip_SetText` and `TextTooltip_SetText`). ~10 lines, **low risk**.
- **E6** — Merge identical bodies of `CharacterScreenPatches.UIInput_ProcessEvent_Patch` and `UIInput_Update_Patch`. ~15 lines, **low risk**.
- **E7** — Merge `ConversationPatches.OnTopicPressed`/`OnTopicMouseOver` reflection walks (~45 duplicated lines each). ~45 lines, **medium risk** — touches conversation announcement timing.
- **E8** — Merge `SaveLoadScreen_OnSaveClicked_Suppressor` and `SaveLoadScreen_OnLoadClicked_Suppressor` (identical bodies). ~20 lines, **low risk**.
- **E9** — Merge `ScannerState.ScanForEnemies` + `ScanForNPCs` into one pass over `Mob[]` (currently both call `FindObjectsOfType<Mob>` independently). ~30 lines, **medium risk** — affects scanner output ordering.
- **E10** — Combine `CombatState.FormatAction`/`FormatTargetAction` (identical bodies). ~30 lines, **low risk**.

When resuming: read `llm-scratchpad/cleanup-findings/PRIORITIZED.md` Bundle E section and the per-batch files for full context.

## Optional follow-ups noted but deferred
*(Updated after the high-level-cleanup review pass. Items the user explicitly deferred from that pass appear under "High-level review — deferred themes" below; this section is now for residual smaller items.)*

- Extract a shared `KeyRepeat` helper for `Time.unscaledTime`-based debounce. (Only 3 files use this pattern — `MapCursorState`, `Patches/UITooltipPatches`, `Patches/UISliderPatches` — so the win is small.)
- Decouple `KeypadState.Active` (the other state-internal flag besides `GenericMenuState.blockUIInput`) from `Patches/InputSuppressor` if a third such coupling appears. Theme 1A already moved the SaveLoad suppressors out of Core; `KeypadState.Active` is referenced only inside `States/`, so this isn't a layering violation today.

## High-level review — themes performed
From the code-review pass at the start of `prompts/high-level-cleanup.md`:
- **1A — SaveLoad suppressors moved out of Core.** `Patches/SaveLoadInputSuppressorPatches.cs` now owns `SaveLoadScreenSuppressor` + the three Harmony patches. `Core/InputSuppressor.cs` no longer imports `States.GenericMenuState`. GenericMenuState's 4 call sites updated.
- **2A — Priority-50 cluster spread.** Alphabetical descending: CharacterInfoState=54, CharacterState=53, ConversationState=52, InventoryState=51, ShopState=50. CombatState=45 and GenericMenuState=55 leave the range clear. `IAccessibilityState` doc comment + repo `CLAUDE.md` priority list updated.
- **6c — `AccessibilityStateBase`.** New `Core/AccessibilityStateBase.cs` provides default `OnActivated`/`OnDeactivated` that log `[ClassName] Activated/Deactivated`. All 14 states inherit from it; states with richer existing log lines (CombatState, ConversationState, InventoryState, ShopState, WorldMapState) keep those and skip `base`; states with bare logging call `base.On*` instead.
- **7A — `code-index/` deleted.** It was a stale snapshot and grep + the source tree are authoritative.

## High-level review — deferred themes
User chose not to address these in this session; they remain for future reference.
- **Theme 3 — Cache `FindObjectOfType` per frame.** Real perf concern (64 calls across 13 files, several in per-frame `IsActive` predicates) but no observed lag, so the trade-off didn't favor it now. If lag surfaces, build `Core/SceneRefCache` that resolves each `FindObjectOfType<T>()` once per frame and route `IsActive` predicates through it.
- **Theme 4 — Consolidate the 81 reflection sites into one `Helpers/GameReflection.cs` registry.** Wasteland 2 isn't being patched anymore, so the latent fragility (game-side renames silently breaking features) is unlikely to bite. Revisit if a game patch ever lands.
- **Theme 5 — Restructure mega-classes (CombatState 3368, MapCursorState 3214, etc.).** Real but big refactor (~500 lines of structural churn per class). No specific change is currently blocked by the structure, so defer until one is.
- **Theme 6a — Shared `KeyRepeat` helper.** Only 3 files use the pattern; win didn't justify a new helper.
- **Theme 6b — Extract shared info-browser navigator (Up/Down/Home/End/Escape over `List<string>`).** This is **Bundle E2** from `cleanup-findings/PRIORITIZED.md`. Already on the Bundle E list, will be addressed if/when E is tackled.

## Splits performed under `large-file-handling.md`
- **`Helpers/CharacterAnnouncementHelper.cs` (1394 → 4 partials)**: split a single static class into `CharacterAnnouncementHelper.cs` (core: reflection cache + per-control announcements + buff/cap helpers, ~436 lines), `.StatDescriptions.cs` (stat & trait description builders + description-panel previews, ~500 lines), `.Snapshots.cs` (derived stats + header / combat snapshots + character summary + XP, ~404 lines), and `.ValueAdjustment.cs` (~107 lines). All four use `static partial class CharacterAnnouncementHelper`; no caller changes required. Smoke-tested.
- **`Patches/InventoryPatches.cs` (1639 → 648) + new `Patches/InventoryFormatting.cs` (1004)**: extracted the 25 formatting/calculation helpers (and the `lastAnnouncedItem` static field) out of the patch file into a new static class `InventoryFormatting`. Updated 41 call sites across `States/InventoryState.cs` (28), `States/ShopState.cs` (2), and the patches themselves (11). Smoke-tested.

## Splits deferred (not trivially separable)
The other 12 files >500 lines are single mega-classes whose sub-modes share state on `this` — splitting them is real refactoring, not a trivial split. Candidates if a refactoring phase happens later: `States/CombatState.cs` (3368), `States/MapCursorState.cs` (3214), `States/InventoryState.cs` (2022), `States/CharacterState.cs` (1899), `States/CharacterInfoState.cs` (1897), `States/ShopState.cs` (1653), `States/GenericMenuState.cs` (1597), `States/WorldMapState.cs` (856), `States/DialogState.cs` (791), `States/ScannerState.cs` (719), `NavigationManager.cs` (667), `Patches/ConversationPatches.cs` (666).

## Files in `llm-scratchpad/`
- `current_status.md` — this file
- `claude_md_validation.md` — validation report; cross-branch findings still apply, test-branch findings now confirmed and folded into the repo CLAUDE.md
- `cleanup-findings/` — triage from `prompts/low-level-cleanup.md`; `PRIORITIZED.md` is the consolidated bundle table, batch-*.md are the raw per-area findings

## Files added during this prompt
- `CLAUDE.md` (rewrite — facts corrected, gotchas promoted from parent, build instructions reflect bundled `libs/`)
- `llm-docs/CLAUDE.md` — index for the docs folder
- `llm-docs/game-model.md` — Wasteland 2 structural reference (moved from scratchpad)

## What was decided / what changed
- HintPath was wrong in CLAUDE.md (`..\Wasteland2_Data\Managed\` → reality is `libs\`). Fixed.
- The 4-patch list in old CLAUDE.md was both incomplete and architecturally outdated — the mod is now state-routing-first, not patch-first. Architecture section rewritten.
- Gotchas section from parent `D:\Claude\Wasteland 2\CLAUDE.md` was promoted into the repo CLAUDE.md with each gotcha re-verified against the test branch (state priorities confirmed, `RefreshButtons()` returns bool confirmed, `GenericMenuState` `CharacterScreen` exclusion confirmed at `States/GenericMenuState.cs:81-82`, `InputRouter` priority sort confirmed at `Core/InputRouter.cs:37`).
- Decompiled-index location is now described as machine-local in CLAUDE.md (no specific D:\ path).
- MelonLoader log path generic-ized to `<game-install>\MelonLoader\Latest.log` in CLAUDE.md.

## Resolved during this prompt
- **Parent-dir `D:\Claude\Wasteland 2\CLAUDE.md` trimmed** (user chose "trim to machine-local only"). Now contains only: pointer to repo CLAUDE.md, repo path, decompiled-index path, MelonLoader log path. Not in git.

## Still-open questions for later prompts
1. **`test` branch is 5 commits ahead of `origin/test`.** Push at some point? (Workflow, not docs scope.)
2. **`.csproj` `<Description>` is stale** — says "Sets Xbox controller as default input method", which no longer reflects the mod. Candidate for the refactoring/cleanup phase later.
3. **Does `master` serve a purpose** (release tag, public surface) or is it just the abandoned v1.0 snapshot? Outside this procedure's scope but worth flagging.

## Notes carried over from start
- In-progress inventory comparison work was committed to `test` branch as `6a8a33c` "Compare focused item to equipped in inventory info browser". `claude-mod-cleanup` is forked from that commit.
- Game: Wasteland 2 Director's Cut (MelonLoader mod, .NET 3.5, Harmony patching).
- Repo path: `D:\Claude\Wasteland 2\Wasteland2AccessibilityMod`.
