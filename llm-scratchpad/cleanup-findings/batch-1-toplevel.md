# Cleanup findings — top-level files

---

## Tolk.cs

### Finding 1: `HasBraille()` and `Braille()` public methods never called
- Lines: 87-89, 102-104
- Removed: ~8
- Risk: low
- Detail: Neither `HasBraille()` nor `Braille()` is called anywhere in the mod. The underlying `Tolk_HasBraille` and `Tolk_Braille` P/Invoke declarations (lines 32-41) go with them. Tolk itself still supports braille; this just removes the unused C# surface.

### Finding 2: `Output()` public method never called
- Lines: 92-95
- Removed: ~5
- Risk: low
- Detail: `Output()` (which does both speech and braille) is declared but has zero call sites in the mod. Only `Speak()` is used. The `Tolk_Output` P/Invoke declaration on line 35 can go with it.

### Finding 3: `IsSpeaking()` and `Silence()` public methods never called
- Lines: 107-115
- Removed: ~8
- Risk: low
- Detail: `IsSpeaking()` and `Silence()` have no call sites in the mod. If future code needs them, they can be re-added; leaving them creates false impressions of available APIs.

### Finding 4: `TrySAPI()` and `PreferSAPI()` public methods never called
- Lines: 64-71
- Removed: ~8
- Risk: low
- Detail: Both wrapper methods are declared but never invoked. SAPI fallback is handled entirely inside Tolk itself. Remove unless there is a planned config option to prefer SAPI.

---

## AudioAwareAnnouncementManager.cs

### Finding 5: `GetQueueSize()` public method never called
- Lines: 329-332
- Removed: ~5
- Risk: low
- Detail: `GetQueueSize()` has no call sites in the codebase. It was likely added as a debugging aid. Can be removed without any behavioral change.

### Finding 6: `IsDialogueAudioPlaying()` public method never called
- Lines: 346-349
- Removed: ~5
- Risk: low
- Detail: This is a thin wrapper around the private `IsVoiceAudioPlaying()`. No caller in the codebase uses it; audio-playing checks are done directly through `VoiceoverHelper` or internally by the manager itself.

### Finding 7: Duplicate reflection field initialization pattern — `AudioAwareAnnouncementManager` vs `VoiceoverHelper`
- Lines: 47-48 (AudioAwareAnnouncementManager), VoiceoverHelper.cs:15-16
- Removed: 0 (design note, no immediate removal)
- Risk: low
- Detail: Both files independently cache `bubbleTextInfosField` and the `fieldInitialized` bool via identical reflection patterns targeting `BubbleTextManager.bubbleTextInfos`. The full walk logic (get instance → get field → get value → iterate) is duplicated across `AudioAwareAnnouncementManager.IsVoiceAudioPlaying()` and `VoiceoverHelper.IsVoiceoverPlaying()`. Consolidating into a single `BubbleTextHelper` utility would be ~40 lines removed across the two files.

### Finding 8: Redundant `bubbleTextInfosField != null` guard after already checking in same scope
- Lines: 151-153
- Removed: ~3
- Risk: low
- Detail: At line 144, `fieldInitialized` is set to `true` and a `null` check is done on `bubbleTextInfosField` with an early return. The next block at line 151 wraps the usage in another `if (bubbleTextInfosField != null)` — this outer check can never be false at that point since the function already returned if it was null.

### Finding 9: `[AudioAware] Speaking immediately (no voiceover)` log fires on every announcement in menus
- Lines: 262
- Removed: 0 (logging, not a remove candidate — just a flag)
- Risk: low
- Detail: This `MelonLogger.Msg` fires every single time any UI element is focused while not in a conversation. In normal gameplay this produces many log lines per second during menu navigation. Consider `MelonLogger.Msg` → conditional or remove. (Flagged for pruning decision, not automatic removal.)

---

## DirectionHelper.cs

### Finding 10: `DirectionFormat` enum is declared but never used
- Lines: 5-8
- Removed: ~5
- Risk: low
- Detail: The `DirectionFormat` enum (values `Cardinal`, `Clock`) is declared in this file but is never referenced anywhere in the codebase. Format selection is controlled by `ModConfig.UseClockPositions` (a bool). The enum was likely an earlier design artifact.

### Finding 11: Redundant intermediate `rawAngle` variable (duplicated in both methods)
- Lines: 38-40 in `GetCardinalDirection`, 70-72 in `GetClockPosition`
- Removed: ~2
- Risk: low
- Detail: Both methods assign `Mathf.Atan2(...)` to `rawAngle` and then immediately copy it to `angle`. Since `rawAngle` is never read again independently, it can be replaced with `float angle = Mathf.Atan2(...) * Mathf.Rad2Deg;` directly. Applies twice.

### Finding 12: `clockHour > 12` safety check cannot trigger
- Lines: 81
- Removed: ~1
- Risk: low
- Detail: `clockHour = Mathf.RoundToInt(angle / 30f)` where `angle` is in `[0, 360)`. The maximum round result is `Mathf.RoundToInt(360/30) = 12`, so the check `if (clockHour > 12)` is unreachable. The comment `// Safety check` acknowledges it is defensive, but it's misleading because `clockHour` could in theory be 13 only if `angle` reached 375°, which the prior normalization prevents.

---

## FOWHelper.cs

### Finding 13: Verbose per-frame `[ActivationTrack]` diagnostic logging runs in production
- Lines: 131, 136, 140, 143, 146, 150, 157-158
- Removed: ~12 (or demote to conditional compile flag)
- Risk: low
- Detail: `UpdateActivationTracking()` emits multiple `MelonLogger.Msg` lines every 5 seconds including a full teleporter dump and per-teleporter state changes. This is clearly leftover debug output from the ShortcutDoor investigation. The 5-second throttle helps but the output is still heavy for a release build.

---

## ModConfig.cs

### Finding 14: Missing blank line before `ToggleObjectNamesFirst()` (minor formatting)
- Lines: 77-78
- Removed: 0 (adds 1 line)
- Risk: low
- Detail: The other two Toggle methods have a blank line separating them; `ToggleObjectNamesFirst()` is immediately flush with the closing brace of `ToggleClockPositions()`. Cosmetic only.

---

## NavigationManager.cs

### Finding 15: Hardcoded ShortcutDoor world-coordinate diagnostic blocks in `UpdateFilteredList`
- Lines: 306-314, 318-393 (the `traceThis` branch logic and `NavDiag`/`NavTrace`/`NavReject`/`NavFilter` log calls)
- Removed: ~50
- Risk: low
- Detail: The entire `traceThis` logic (hardcoded coordinate range `x > 88f && x < 93f, z > 111f && z < 116f`) and all associated `[NavTrace]`, `[NavDiag]`, `[NavReject]`, `[NavFilter]` log calls are ScottAFB-specific debug code left in from diagnosing the ShortcutDoor perception-gating bug. This is the single largest cleanup target in the batch. It adds approximately 30 conditional branches and log strings to every interactable scan.

### Finding 16: `lastAnnouncement` field is written but never read from outside `AnnounceInteractable`
- Lines: 29, 234, 244, 209
- Removed: ~4
- Risk: low
- Detail: `lastAnnouncement` is a private static string that is set in `AnnounceInteractable` and cleared in `RepeatLastAnnouncement`, but is never read outside those two spots — the actual announcement text is always spoken inline via `ScreenReaderManager.SpeakInterrupt(lastAnnouncement)`. `RepeatLastAnnouncement` doesn't use it either; it re-builds the announcement via `AnnounceInteractable`. The field can be removed; `RepeatLastAnnouncement` already re-announces via the live method.

### Finding 17: `lastAnnouncedInteractable` field is written but never read
- Lines: 30, 235, 208
- Removed: ~3
- Risk: low
- Detail: `lastAnnouncedInteractable` is assigned in `AnnounceInteractable` and cleared in `RepeatLastAnnouncement`, but is never tested or returned anywhere. All repeat/refresh logic uses `selectedInteractable`. This field is dead.

### Finding 18: `NextCategory` and `PreviousCategory` share identical body except index arithmetic
- Lines: 63-114
- Removed: ~20
- Risk: low
- Detail: Both methods: guard on `IsFOWReady`, compute the new index (one adds, one subtracts), reset `currentIndex`/`selectedInteractable`, call `UpdateFilteredList`, speak the count, and log. They could share a private `ChangeCategory(int delta)` helper to remove the duplication.

### Finding 19: Same duplication between `CycleNext` and `CyclePrevious`
- Lines: 133-179
- Removed: ~15
- Risk: low
- Detail: Both cycle methods guard on `IsFOWReady`, call `UpdateFilteredList`, check for empty list and speak "No X nearby", then differ only in index increment vs decrement. A private `Cycle(int delta)` helper would collapse these.

### Finding 20: Diagnostic `[NavFilter]` log on every successfully-added interactable fires unconditionally
- Lines: 379-392
- Removed: ~12
- Risk: low
- Detail: After passing all filters, every added interactable triggers a `MelonLogger.Msg` with drama type, blocked flag, skob info, and teleporter destination. This is dense output during any interactable scan and was left over from the perception-gate debugging work.

---

## ScreenReaderManager.cs

### Finding 21: `CleanText` called twice for the same string in `SpeakDirect`
- Lines: 96
- Removed: 0 (net zero, but removes redundant work)
- Risk: low
- Detail: `SpeakDirect` calls `UITextExtractor.CleanText(text)` on line 96. All callers of `SpeakDirect` within the mod are `SpeakInterrupt` (which already cleaned the text on line 80) and `AudioAwareAnnouncementManager` (which receives already-cleaned text from `Speak`/`SpeakInterrupt`). The `CleanText` in `SpeakDirect` is thus double-cleaning in most code paths. Removing it from `SpeakDirect` is safe assuming the documented contract ("callers pass clean text") is respected; alternatively add a note that external callers should pre-clean.

---

## UITextExtractor.cs

### Finding 22: `IsInteractiveElement` performs `GetComponent<UISprite>` then immediately checks six other components — expensive and partially redundant
- Lines: 24-34
- Removed: 0 (design note)
- Risk: low
- Detail: The sprite-only block calls `GetComponent` seven times in sequence. The six subsequent `GetComponent` calls on lines 43-47 repeat some of the same components. On a focus-change path this runs once so the perf impact is minimal, but the structure is confusing — the function checks for buttons etc. twice (once to exclude sprite-only objects, once to include interactive objects). A single pass would be cleaner.

### Finding 23: Final fallback `return go.GetComponentInChildren<UILabel>() != null` is overly broad
- Lines: 72
- Removed: 0 (risk note)
- Risk: medium
- Detail: Any GameObject with a UILabel child anywhere in the hierarchy is treated as interactive. This can produce false positives for decorative containers that happen to have labels. Flagged for review rather than removal since it may be load-bearing for elements not otherwise caught.

---

## VoiceoverHelper.cs

### Finding 24: `IsVoiceoverPlaying()` — near-duplicate of `AudioAwareAnnouncementManager.IsVoiceAudioPlaying()`
- Lines: 21-103
- Removed: ~50 (if consolidated)
- Risk: medium
- Detail: `VoiceoverHelper.IsVoiceoverPlaying()` and the private `AudioAwareAnnouncementManager.IsVoiceAudioPlaying()` implement the same BubbleTextManager reflection walk with the same field cache pattern. The main difference is that `AudioAwareAnnouncementManager` also filters out Bark/Radio/Label textKinds. If the two callers can tolerate the same filter, one can delegate to the other, removing ~50 lines.

### Finding 25: `GetVoiceoverRemainingTime()` is only called by `SpeakWithVoiceoverDelay()`
- Lines: 286-334
- Removed: 0 (used internally, not dead)
- Risk: low
- Detail: The method is not directly dead, but the entire coroutine delay mechanism (`SpeakWithVoiceoverDelay` + `DelayedSpeak` + `GetVoiceoverRemainingTime`) adds ~60 lines of complexity for what is effectively a timer-based queue. The `AudioAwareAnnouncementManager` already provides audio-gated queuing. Worth noting as a future consolidation target.

### Finding 26: `HasPendingOrActiveVoicedAudio`, `HasActiveConversationBubbles`, `HasActiveDescriptionBubbles` each independently re-initialize `bubbleTextInfosField`
- Lines: 119-124, 187-192, 246-251
- Removed: ~15
- Risk: low
- Detail: All three methods contain identical `if (!fieldInitialized) { ... fieldInitialized = true; }` blocks. Since `fieldInitialized` is a shared static, only the first method to run actually needs to initialize; the subsequent blocks are dead after the first call. Extracting to a private `EnsureFieldInitialized()` helper would remove ~10 lines of repetition and make the lazy-init intent explicit.

### Finding 27: `HasActiveConversationBubbles` conversation-type string list is not a constant
- Lines: 210-218
- Removed: 0 (no lines saved, but improves maintainability)
- Risk: low
- Detail: The set of conversation textKind strings (`"Conversation"`, `"AudioConversation"`, `"DescConversation"`, `"DescPercConversation"`, `"AsciiArtConversation"`, `"Epilogue"`) is written as individual `||`-chained comparisons. A `static readonly HashSet<string>` would be clearer and slightly faster on repeated calls.

---

## Wasteland2AccessibilityMod.cs

### Finding 28: Version string "2.0.0" is hardcoded in two places
- Lines: 5 (`MelonInfo`), 19 (`MelonLogger.Msg`)
- Removed: 0 (no lines saved, maintainability)
- Risk: low
- Detail: The version appears in the assembly attribute on line 5 and again as a literal in the startup banner on line 19. A `const string VERSION = "2.0.0"` would keep them in sync and avoid accidental drift on future version bumps.

### Finding 29: Startup banner keyboard controls list is stale / potentially incorrect
- Lines: 29-40
- Removed: 0 (content review needed)
- Risk: low
- Detail: The printed key bindings (`[ ]`, `\`, `=`, `'`, etc.) are hardcoded strings; if any binding changed since this was written, the banner is misleading. Not a code defect, but worth verifying against the actual `HandleInput` methods in the States.

---

## WorldMapNavigationManager.cs

### Finding 30: `Vector2Distance` is a private static method duplicated in three files
- Lines: 383-388 (WorldMapNavigationManager), WorldMapProximityAlert.cs:428-433, States/WorldMapState.cs:849-854
- Removed: ~15 (if consolidated into a shared helper)
- Risk: low
- Detail: Identical `Vector2Distance(Vector3, Vector3)` implementations exist in `WorldMapNavigationManager`, `WorldMapProximityAlert`, and `WorldMapState`. `WorldMapPatches.cs` also has `WorldMapPatchUtils.Vector2Distance`. Consolidating into a single static helper (e.g., `WorldMapUtils.Vector2Distance`) would remove three copies and make future fixes apply everywhere.

### Finding 31: `NextCategory` and `PreviousCategory` bodies are near-identical (same pattern as NavigationManager)
- Lines: 43-75
- Removed: ~15
- Risk: low
- Detail: Same copy-paste pattern as `NavigationManager`: both methods compute a new index, reset state, call `UpdateFilteredList`, announce, and log. Only the index arithmetic differs. A private `ChangeCategory(int delta, Vector3 relativeTo)` would collapse both.

### Finding 32: `CycleNext` and `CyclePrevious` bodies are near-identical
- Lines: 77-110
- Removed: ~12
- Risk: low
- Detail: The two methods share all logic except index increment vs decrement. Collapsible into a single `Cycle(int delta, Vector3 relativeTo)` method.

### Finding 33: POI name fallback in `GetPOIName` duplicates cleanup logic from `CleanGameObjectName` in NavigationManager
- Lines: 325-331 (WorldMapNavigationManager.GetPOIName)
- Removed: ~8
- Risk: low
- Detail: `GetPOIName` contains its own `Replace("(Clone)", "")` + `Regex.Replace(_\d+$)` + `Replace("_", " ")` sequence that is identical to `NavigationManager.CleanGameObjectName`. The zone-prefix stripping regex (`^(AZ|CA|LA)\d?_`) present in `NavigationManager` is notably absent here. Sharing a common utility method would remove the duplication and keep cleanup rules consistent.

---

## WorldMapProximityAlert.cs

### Finding 34: `CheckEncounterZoneProximity()` is a private method never called
- Lines: 262-293
- Removed: ~32
- Risk: low
- Detail: `CheckEncounterZoneProximity` is declared private and has no call sites anywhere in the codebase. The caller comment in `CheckProximity` (lines 51-53) explicitly explains encounter zones are NOT announced. The method body, `insideEncounterZones` set, and `IsPointInEncounterZone` are all dead code as a result (see next finding).

### Finding 35: `insideEncounterZones` HashSet and `IsPointInEncounterZone()` are only used by the dead `CheckEncounterZoneProximity`
- Lines: 30, 131-132, 359-367
- Removed: ~14
- Risk: low
- Detail: `insideEncounterZones` is declared, cleared in `Reset()`, and used only inside `CheckEncounterZoneProximity` — which is never called. `IsPointInEncounterZone` is similarly dead. Both can be removed along with Finding 34.

### Finding 36: `insideDiscoveryBoundary` add/remove is duplicated within the same loop iteration
- Lines: 240, 248, 254-255
- Removed: ~4
- Risk: low
- Detail: In `CheckRadiationProximity`, when `inDiscovery && !wasInDiscovery && !inCloud` is true, the code adds to `insideDiscoveryBoundary` (line 240). Then unconditionally at lines 254-255 it adds it again if `inDiscovery` is true — which it is by the branch condition, so the second add is always redundant within the same iteration. The unconditional adds at lines 253-255 should be `else` branches or removed.

### Finding 37: `Vector2Distance` duplicated (see Finding 30)
- Lines: 428-433
- Removed: ~5 (if consolidated)
- Risk: low
- Detail: Same private `Vector2Distance` implementation as in `WorldMapNavigationManager` and `WorldMapState`. Covered in Finding 30.

### Finding 38: `CheckProximity` builds a `List<string> alerts` and calls `string.Join` even when only one alert fires
- Lines: 38-61
- Removed: 0 (no lines saved, minor allocation)
- Risk: low
- Detail: On the common hot path (cursor moves, nothing to announce), the list is allocated and immediately found empty. Could use early-out with a single string variable for the typical case, but this is a micro-optimization not worth doing unless profiling shows it matters.
