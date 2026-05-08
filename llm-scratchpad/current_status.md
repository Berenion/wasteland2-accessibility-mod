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

## Prompts pending
- `prompts/large-file-handling.md` (next — three files exceed 2000 lines: `States/CombatState.cs` 3368, `States/MapCursorState.cs` 3214, `States/InventoryState.cs` 2022)

## Files in `llm-scratchpad/`
- `current_status.md` — this file
- `claude_md_validation.md` — validation report; cross-branch findings still apply, test-branch findings now confirmed and folded into the repo CLAUDE.md
- `code-index/` — structural index of all 61 source files (one `.md` per `.cs`), built by 9 parallel sonnet agents. Use it as a fast map: search by class/method name to find which file owns a symbol without grep-walking the whole tree.

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
