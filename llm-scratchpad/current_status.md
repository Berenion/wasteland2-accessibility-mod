# Current Status

## Working branch
`claude-mod-cleanup` (re-forked from `test` after the master/test mismatch was resolved)

## Default branch (target for eventual merge)
`test` — `master` is a stale v1.0 snapshot 149 commits behind and is not the active branch.

## Procedure source
`D:\Claude\Wasteland 2\llm-mod-refactoring-prompts\` — clone of https://github.com/ahicks92/llm-mod-refactoring-prompts

## Prompts completed
- `prompts/sanity-checks-setup.md`
- `prompts/information-gathering-and-checking.md` (this one)

## Prompts pending
- `prompts/code-directory-construction.md` (next, per `information-gathering-and-checking.md`)

## Files in `llm-scratchpad/`
- `current_status.md` — this file
- `claude_md_validation.md` — validation report; cross-branch findings still apply, test-branch findings now confirmed and folded into the repo CLAUDE.md

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

## Open questions for the user
1. **Parent-dir `D:\Claude\Wasteland 2\CLAUDE.md`.** Now that the repo CLAUDE.md is the canonical source, the parent file is mostly redundant. Suggest trimming it down to just the machine-local pointers (decompiled-index path, log path, "repo lives in subfolder X") with a note pointing to the repo CLAUDE.md for everything else. Confirm before touching.
2. **`test` branch is 5 commits ahead of `origin/test`.** Want them pushed, or keep them local for now?
3. **`.csproj` `<Description>` is stale** — says "Sets Xbox controller as default input method", which no longer reflects the mod. Worth fixing in a follow-up commit?
4. **Does `master` serve a purpose** (release tag, public surface) or is it just the abandoned v1.0 snapshot? If the latter, consider deleting or fast-forwarding to `test` at some point — outside the scope of this procedure but worth flagging.

## Notes carried over from start
- In-progress inventory comparison work was committed to `test` branch as `6a8a33c` "Compare focused item to equipped in inventory info browser". `claude-mod-cleanup` is forked from that commit.
- Game: Wasteland 2 Director's Cut (MelonLoader mod, .NET 3.5, Harmony patching).
- Repo path: `D:\Claude\Wasteland 2\Wasteland2AccessibilityMod`.
