# CLAUDE.md Fact-Check — Findings

**Files checked**
- `D:\Claude\Wasteland 2\CLAUDE.md` (parent dir, machine-local)
- `D:\Claude\Wasteland 2\Wasteland2AccessibilityMod\CLAUDE.md` (in repo)

**Branch checked:** `claude-mod-cleanup` (forked from `master`)

> **CAVEAT — discovered mid-validation:**
> `master` is **149 commits behind `test`**. The fact-check below was run against `master`. Many "missing class" findings are spurious because those classes (`DialogState`, `InputRouter`, `AudioAwareAnnouncementManager`, `States/`, `Patches/TutorialPatches.cs`) only exist on `test`, where the actual development is happening. See `current_status.md` → "Branch base issue" section. Once the user decides which branch to base the cleanup on, several findings below need re-checking.

## Cross-branch valid findings (apply on either base)

These were verified by reading files that exist on both branches (`.csproj`, `build.bat`, parent CLAUDE.md, decompiled index file).

| # | Finding | Verdict | Action |
|---|---------|---------|--------|
| 1 | CLAUDE.md says Assembly-CSharp HintPath is `..\Wasteland2_Data\Managed\Assembly-CSharp.dll`. Actual `.csproj` HintPath is `libs\Assembly-CSharp.dll`, and `libs\` is checked into the repo with all required DLLs. The directory `D:\Claude\Wasteland 2\Wasteland2_Data\` does not exist. | INCORRECT | Replace the example HintPath; remove the "common game installation paths" advice or re-frame as historical/manual-fallback. The Before-First-Build section is misleading — there is no first-build step needed because `libs\` is bundled. |
| 2 | Parent CLAUDE.md claims decompiled code index is "49,952 lines covering 42,806 members". Actual is 46,389 lines per `wc -l` (member counts in the file's own header are accurate). | OUTDATED | Update line count or drop the specific number. |
| 3 | `build.bat` exists. | VALID | — |
| 4 | `.csproj` targets `net35` and has an auto-copy MSBuild target to `..\Mods\`. | VALID | — |
| 5 | Decompiled index file exists at `D:\Claude\Wasteland 2\Decompiled Code Index.txt` and uses the `=== ClassName ===` format. | VALID | — |
| 6 | MelonLoader log dir `D:\SteamLibrary\steamapps\common\Wasteland 2 Director's Cut\Build\MelonLoader\` exists. | VALID, machine-local | Keep in parent CLAUDE.md only. Repo CLAUDE.md should describe *where to find* the log generically (`<game install>\MelonLoader\Latest.log`), not pin to a D:\ path. |
| 7 | Parent CLAUDE.md notes "github repository lives in a subfolder at `D:\Claude\Wasteland 2\Wasteland2AccessibilityMod`" — true on this machine, meaningless to other readers. | MACHINE-LOCAL | Keep in parent only. |

## test-branch-specific findings (re-check needed if base changes)

These are asserted by the parent CLAUDE.md's "Key Takeaways & Gotchas" section. They are the kind of hard-won project knowledge worth preserving — but they only make sense against the `test` codebase.

- `DialogState`, `GenericMenuState`, `CharacterState`, `InputRouter`, `AudioAwareAnnouncementManager` — all exist on `test`, none exist on `master`.
- `Patches/TutorialPatches.cs` — exists on `test`, missing on `master`.
- State priority numerics (DialogState=70, GenericMenuState=55, CharacterState=50) — need a re-check against `test` source. **TODO** once branch base is decided.
- `RefreshButtons()` returning `bool` claim — need a re-check against `test`.
- The "Two Separate Tutorial Systems" gotcha (`TUT_TutorialPopup` vs `TutorialPopupMenu`) is verifiable against the decompiled index — both class names confirmed by name search in the index.
- `CHA_SkillPanel.OnCombatSkillsClicked / OnKnowledgeSkillsClicked / OnGeneralSkillsClicked` — confirmed in decompiled index.

## Anti-patterns identified

- **Drift between two CLAUDE.md files.** The parent file is a superset of the repo file. Hard-won gotchas live only in the parent (untracked). When the repo file is the one a contributor reads, those gotchas are invisible.
- **Machine-specific absolute paths in any committed CLAUDE.md.** Parent CLAUDE.md is fine since it's not tracked; repo CLAUDE.md must use generic language.
- **Outdated "before first build" section** that tells the user to point HintPath at `..\Wasteland2_Data\Managed\` — incorrect now that `libs\` is shipped. The current text would actively confuse new contributors.

## Recommendations summary

For the **repo CLAUDE.md** (after base-branch question is resolved):
- Fix the HintPath instructions and Before-First-Build flow.
- Replace machine-pinned paths with portable references.
- Promote the gotchas section verbatim from the parent file (after re-validating each gotcha against the chosen base branch).
- Update or drop the line-count claim for the decompiled index.
- Expand the Harmony Patching list to reflect actual coverage (will need a fresh enumeration on the chosen base).

For the **parent-dir CLAUDE.md** (machine-local notes):
- Keep machine paths and the github subfolder note.
- Remove duplication of repo-CLAUDE.md content that doesn't change machine-to-machine.

## Open questions for user

1. **Branch base for cleanup procedure.** `master` is 149 commits behind `test`. Should the cleanup branch be re-forked from `test`? (See `current_status.md`.)
2. Was anything intentionally removed from `test` that the gotchas describe? (Probably not — but worth confirming none of the gotchas describe deleted code.)
3. Do you maintain `master` as a stable release tag vs. `test` as active development? If yes, is the long-term plan to merge `test` to `master`, or is `test` the de facto main?
