# CLAUDE.md

Guidance for Claude Code (claude.ai/code) and other LLMs working in this repository.

## Project Overview

A **MelonLoader mod** for **Wasteland 2 Director's Cut** that provides screen reader and keyboard accessibility for the game's UI. The mod intercepts the game via **Harmony runtime patching** and uses the **Tolk** library to speak through NVDA, JAWS, or SAPI.

The primary author is a blind player who is also the mod's main tester. Decisions about wording, pacing, and what gets announced reflect screen-reader UX, not what looks good in print.

## Building the Mod

Required assembly references are committed under `libs/` (Assembly-CSharp, UnityEngine, MelonLoader, 0Harmony). No first-time setup is needed — just restore and build.

```bash
dotnet restore
dotnet build -c Release        # or: build.bat
```

Output: `bin/Release/net35/Wasteland2AccessibilityMod.dll`. The `.csproj` includes an MSBuild target that auto-copies the DLL to `..\Mods\` if that folder exists next to the repo.

For a manual install, copy the DLL to `<game-install>\Mods\`.

If you need to refresh `libs/` (e.g. after a game update), copy the DLLs from `<game-install>\Wasteland2_Data\Managed\` and `<game-install>\MelonLoader\`.

## Architecture

The mod has two cooperating pipelines: a **state-based input router** that owns keyboard handling and high-level UI logic, and **Harmony patches** that observe game internals to drive announcements.

### State-based Input Routing

`Core/InputRouter` is a static registry of `IAccessibilityState` implementations. Each state declares a `Priority` and an `IsActive` predicate. On every frame (`MelonMod.OnUpdate`, before the game's `Update`), the router:

1. Sorts states by priority, highest first.
2. Fires `OnActivated` / `OnDeactivated` as `IsActive` flips.
3. Walks states top-down; the highest-priority active state whose `HandleInput()` returns `true` consumes input for that frame.

Priorities are intentional and load-bearing — see the gotchas below. The actual values live in each `States/*.cs` file (search `Priority =>`); examples include `ScannerState` 80, `DialogState` 70, `GenericMenuState` 55, `CharacterState` 50, `CombatState` 45, `MapCursorState` 30, `ExplorationState` 10.

### Harmony Patches

`Patches/*.cs` contains a class per UI subsystem (Inventory, Shop, Conversation, Tutorial, World Map, etc.). Each file declares one or more `[HarmonyPatch(typeof(GameClass), "Method")]` classes. Patches are applied automatically by `MelonMod.OnLateInitializeMelon` via MelonLoader's patcher. Their job is to detect game-side events (focus changed, item added, dialog populated) and call into the announcement pipeline.

Common targets: `UICamera.SetSelection` / `UICamera.Notify` for focus, `UIPopupList.Highlight` for dropdowns, `ItemInfoBox.SetItem` for inventory item details, `ConversationHUD.AddText` for dialogue, `WorldMapPOI.Discover` for map markers.

### Speech Pipeline

`ScreenReaderManager` wraps `Tolk`. Three entry points with distinct semantics:

- `SpeakInterrupt(text)` — clears the audio-aware queue and speaks immediately with Tolk's `interrupt=true`. Use for direct user actions (key presses, navigation moves) where stale speech should be cut off.
- `Speak(text)` — queues through `AudioAwareAnnouncementManager` with `interrupt=false`. Use for sequential or follow-up announcements that should not stomp on prior speech.
- `SpeakDirect(text, interrupt)` — bypasses the queue. Use only for critical system messages or debugging.

`AudioAwareAnnouncementManager` defers queued announcements while game voiceover is playing, so the mod doesn't speak over recorded dialogue. When no voiceover is active, queued announcements pass through to Tolk immediately.

### Text Extraction

`UITextExtractor.CleanText()` strips NGUI formatting codes, color tags, embedded markers, and other noise so Tolk receives plain text. All `Speak*` methods call it; do not pass raw NGUI strings further down.

## Key Takeaways & Gotchas

These are non-obvious facts the codebase relies on. Confirm before diverging.

### Two Separate Tutorial Systems

The game has two unrelated tutorial popup implementations:

1. **`TUT_TutorialPopup`** — Standalone `MonoBehaviour` driven by the `TutorialScreen` singleton. Used for in-game tutorials. Freezes input via `InputManager.SetFreezeInput()`. Patched in `Patches/TutorialPatches.cs`.
2. **`TutorialPopupMenu`** — Extends `GUIScreen`, lives on the GUIManager screen stack. Used by character creation (`CharacterCreationScreenConsole.ShowTutorial()`). Dismissed via `Close()`.

Both feed `DialogState`. When adding support for a new tutorial-like popup, identify which system it uses first.

### Speech Queuing Across States

`InputRouter` walks states in priority order, so on any given frame, higher-priority states announce before lower ones. To avoid clobbering, lower-priority states must use `Speak()` (queue) when a higher-priority state is also speaking that frame.

Example: when a `TutorialPopupMenu` is up during character creation, `DialogState` (70) announces the dialog. `CharacterState` (50) needs to use `Speak()` (not `SpeakInterrupt()`) for its panel-change announcement so the dialog is still heard.

### Stacked Dialogs

Character creation can stack multiple `TutorialPopupMenu` instances on the same panel change (e.g. Attributes + Combat Skills tutorials). When the front one dismisses, `DialogState` stays active and `OnActivated()` does **not** re-fire. `DialogState.RefreshButtons()` returns a `bool` indicating the dialog actually changed; `HandleInput()` uses that signal to call `AnnounceDialog()`.

### NGUI Button Click Dispatch

- `HUD_ModeButton.SendMessage("OnClick")` does **not** work for skill category buttons. Call `CHA_SkillPanel.OnCombatSkillsClicked()` / `OnKnowledgeSkillsClicked()` / `OnGeneralSkillsClicked()` directly.
- Always check the decompiled source for a public method on the target class before falling back to `SendMessage` or reflection.

### GenericMenuState Excludes CharacterScreen

`GenericMenuState` (priority 55) explicitly returns inactive when `CharacterScreen.instance` is active. Any `GUIScreen`-based popup that appears over character creation (e.g. `TutorialPopupMenu`) must be handled by `DialogState` or `CharacterState`, not `GenericMenuState`.

## Testing

1. Build (or run `build.bat`).
2. Ensure MelonLoader is installed in the game directory.
3. The DLL auto-copies to `..\Mods\`; otherwise copy manually.
4. Confirm `Tolk.dll` is in the game directory.
5. Launch with NVDA, JAWS, or SAPI running.
6. Watch the MelonLoader console for:

   ```
   Wasteland 2 Accessibility Mod v2.0.0
   Screen reader detected: <Reader Name>
   ```

The MelonLoader log lives at `<game-install>\MelonLoader\Latest.log`.

## Common Issues

**Build fails — references not found.** Verify `libs/` contains the four DLLs (`Assembly-CSharp.dll`, `UnityEngine.dll`, `MelonLoader.dll`, `0Harmony.dll`). If you removed them, refresh from the game install (see Building above).

**No screen reader output.** Confirm `Tolk.dll` is in the game directory, the screen reader is running before launch, and the MelonLoader log shows a "Screen reader detected" line. Tolk falls back to SAPI if no dedicated reader is found.

**A specific UI element isn't announced.** Find the relevant state in `States/` and the relevant patch in `Patches/`. Either the state's `HandleInput` doesn't cover that element, or no patch hooks the game method that populates it. Use the decompiled code (see below) to find the right method to patch.

## Target Framework

- **.NET Framework 3.5** (Unity 4.x compatibility — same target Unity uses)
- **MelonLoader 0.6.1+**
- **Harmony** (bundled with MelonLoader)

## Reference Material

- **Decompiled Wasteland 2 source index** — tabulates every class, method, field, enum, property, and delegate in the game's `Assembly-CSharp.dll`. The index file is machine-local; on this developer's machine it lives at `..\Decompiled Code Index.txt` relative to the repo. Search by class with `=== ClassName ===`. Always check the index before guessing a method signature.
- **`llm-docs/`** — structural reference written for LLM assistants. See `llm-docs/CLAUDE.md` for the index.

## Extending the Mod

To add coverage for a new UI element:

1. **Find the right hook.** Identify the method in the game that populates or activates the element. Use the decompiled code index — don't guess.
2. **Decide between a state and a patch.**
   - If the element introduces a new keyboard navigation context (a new screen, popup, or modal flow), add an `IAccessibilityState` in `States/`.
   - If you only need to react to an existing UI event, add a Harmony patch in the matching `Patches/*.cs` file.
3. **Write the patch.**
   ```csharp
   [HarmonyPatch(typeof(ClassName), "MethodName")]
   public class ClassName_MethodName_Patch
   {
       [HarmonyPostfix]
       public static void Postfix(/* method parameters */)
       {
           // Extract via UITextExtractor.CleanText, then ScreenReaderManager.Speak / SpeakInterrupt
       }
   }
   ```
4. **Pick the right speech method.** Direct user input → `SpeakInterrupt`. Follow-up context that should not preempt the prior announcement → `Speak`. See the speech-pipeline notes above.
5. **Filter noise.** If the patch fires more often than needed, gate it on what the user actually cares about (focus changed, value changed, etc.) rather than firing every frame.
