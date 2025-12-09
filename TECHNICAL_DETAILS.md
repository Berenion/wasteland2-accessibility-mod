# Technical Details: Wasteland 2 Accessibility Mod

This document explains how the mod works internally for developers and advanced users.

## Architecture Overview

The mod uses **MelonLoader** as the mod framework and **Harmony** for runtime code patching.

### Component Structure

```
Wasteland2AccessibilityMod
├── AccessibilityMod (Main Mod Class)
│   └── Inherits from MelonMod
│   └── Handles initialization and logging
├── InxilePlayerPrefs_GetInt_Patch (Harmony Patch)
│   └── Patches the two-parameter GetInt overload
└── InxilePlayerPrefs_GetInt_NoDefault_Patch (Harmony Patch)
    └── Patches the one-parameter GetInt overload
```

## How It Works

### Game Input Initialization

Wasteland 2 loads input settings in **Game.cs** during initialization:

```csharp
// Game.cs line 2181
MonoBehaviourSingleton<InputManager>.GetInstance().inputMode =
    (InputMode)InxilePlayerPrefs.GetInt("InputMode");
```

### Default Behavior (Without Mod)

1. `InxilePlayerPrefs.GetInt("InputMode")` is called
2. No default value parameter provided → defaults to 0
3. `PlayerPrefs.GetInt("InputMode", 0)` returns 0 (Keyboard)
4. Input mode is set to Keyboard

### Modified Behavior (With Mod)

1. `InxilePlayerPrefs.GetInt("InputMode")` is called
2. **Harmony Prefix patch intercepts the call**
3. Patch checks if "InputMode" key exists in PlayerPrefs
4. If key doesn't exist (first launch):
   - Patch sets `__result = 1` (Xbox)
   - Returns `false` to skip original method
5. If key exists (user has saved preference):
   - Returns `true` to execute original method
   - User's saved preference is respected

## Harmony Patches Explained

### Patch 1: Two-Parameter GetInt

```csharp
[HarmonyPatch(typeof(InxilePlayerPrefs), nameof(InxilePlayerPrefs.GetInt),
    new System.Type[] { typeof(string), typeof(int) })]
```

**Target Method:**
```csharp
public static int GetInt(string key, int defaultValue = 0)
```

**Patch Logic:**
- Intercepts calls like `GetInt("InputMode", 0)`
- Checks if key exists in PlayerPrefs
- If not: Returns 1 (Xbox) instead of 0 (Keyboard)

### Patch 2: One-Parameter GetInt

```csharp
[HarmonyPatch(typeof(InxilePlayerPrefs), nameof(InxilePlayerPrefs.GetInt),
    new System.Type[] { typeof(string) })]
```

**Target Method:**
```csharp
public static int GetInt(string key)
{
    return GetInt(key, 0); // Calls the two-parameter version
}
```

**Why Both Patches?**
- Different code paths might call different overloads
- Ensures complete coverage of all GetInt calls
- Defensive programming for mod compatibility

## Input Mode Enumeration

From **InputMode.cs**:

```csharp
public enum InputMode
{
    Keyboard = 0,      // Default in vanilla game
    Xbox = 1,          // Our new default
    PS4 = 2,
    SteamController = 3
}
```

## Code Flow Diagram

```
Game Launch
    ↓
Game.cs Initialization (line 2181)
    ↓
Call: InxilePlayerPrefs.GetInt("InputMode")
    ↓
[HARMONY INTERCEPTS]
    ↓
Patch: Check PlayerPrefs.HasKey("InputMode")
    ↓
    ├─→ TRUE (Key exists)
    │   └─→ Return true → Execute original method
    │       └─→ Return saved user preference
    │
    └─→ FALSE (First launch)
        └─→ Set __result = 1 (Xbox)
            └─→ Return false → Skip original method
                └─→ InputManager.inputMode = InputMode.Xbox
                    ↓
                Game configures controller input
```

## PlayerPrefs Storage

Wasteland 2 uses Unity's PlayerPrefs system to store settings:

### Windows Location
```
%USERPROFILE%\AppData\LocalLow\inXile entertainment\WL2DC\
```

### Registry Location (Unity's internal storage)
```
HKEY_CURRENT_USER\Software\inXile entertainment\WL2DC
```

### Key-Value for Input Mode
```
Key: "InputMode"
Value: Integer (0-3)
  0 = Keyboard
  1 = Xbox
  2 = PS4
  3 = Steam Controller
```

## Mod Execution Timeline

1. **Game Starts** → MelonLoader initializes
2. **MelonLoader Loads Mods** → Finds Wasteland2AccessibilityMod.dll
3. **OnInitializeMelon()** → Mod logs initialization message
4. **Harmony Auto-Patches** → Patches are automatically applied by MelonLoader
5. **Game Initialization** → Game.cs starts executing
6. **Input Mode Check** → Patch intercepts GetInt call
7. **Mod Logic Executes** → Determines default based on PlayerPrefs
8. **Game Continues** → Input mode is configured
9. **Main Menu** → Player sees controller-optimized UI

## Performance Impact

- **Memory**: Negligible (~few KB for mod assembly)
- **CPU**: Single check per game launch (HasKey + comparison)
- **Startup Time**: No measurable impact (<1ms)
- **Runtime**: No ongoing performance cost (patch only runs at startup)

## Compatibility

### MelonLoader Versions
- **Minimum**: 0.6.1
- **Tested**: 0.6.1, 0.6.2
- **Maximum**: Should work with future versions

### Game Versions
- **Target**: Wasteland 2 Director's Cut
- **Unity Version**: 4.x (older Unity, uses .NET 3.5)
- **Platform**: Windows (primary), Linux/Mac (untested)

### Other Mods
- **Generally Compatible**: This mod only patches input initialization
- **Conflict Risk**: Low - only affects InxilePlayerPrefs.GetInt for "InputMode"
- **Load Order**: Not critical - patch is specific enough to avoid conflicts

## Security Considerations

### What the Mod Does NOT Do

- ❌ Does not connect to the internet
- ❌ Does not read/write files outside game directory
- ❌ Does not access system resources beyond game scope
- ❌ Does not collect user data
- ❌ Does not modify save files
- ❌ Does not inject malicious code

### What the Mod DOES

- ✅ Patches a single method call
- ✅ Reads PlayerPrefs (standard Unity settings)
- ✅ Logs to MelonLoader console
- ✅ Modifies default input mode only

## Debugging

### Enable Verbose Logging

The mod already logs important events:

```csharp
MelonLogger.Msg("Wasteland 2 Accessibility Mod loaded!");
MelonLogger.Msg("Setting default InputMode to Xbox (1)");
```

### Adding More Logging

Edit `Wasteland2AccessibilityMod.cs` and add:

```csharp
MelonLogger.Msg($"HasKey check: {PlayerPrefs.HasKey(key)}");
MelonLogger.Msg($"Key: {key}, Default: {defaultValue}");
```

### View MelonLoader Logs

Logs are saved to:
```
[Game Directory]\MelonLoader\Latest.log
```

## Extending the Mod

### Adding Configuration File Support

Example: Allow users to choose default in a config file:

```csharp
// In OnInitializeMelon()
var configPath = Path.Combine(MelonUtils.UserDataDirectory, "AccessibilityConfig.txt");
var defaultMode = File.Exists(configPath)
    ? int.Parse(File.ReadAllText(configPath))
    : 1; // Default to Xbox

// Store in a static variable and use in patch
```

### Supporting Additional Settings

Patch other `InxilePlayerPrefs.GetInt()` calls:

```csharp
if (key == "DrawCombatGrid")
{
    // Set a different default for combat grid
    __result = 1; // Enable by default
    return false;
}
```

## Source Code Reference

### Key Files in Decompiled Code

| File | Location | Purpose |
|------|----------|---------|
| InputMode.cs | Decompiled code\InputMode.cs:1-7 | Enum definition |
| Game.cs | Decompiled code\Game.cs:2181 | Input mode initialization |
| InxilePlayerPrefs.cs | Decompiled code\InxilePlayerPrefs.cs | Wrapper for Unity PlayerPrefs |
| GameplayOptionsPanel.cs | Decompiled code\GameplayOptionsPanel.cs:109,587 | Options UI |
| InputManager.cs | Decompiled code\InputManager.cs:336,566 | Input handling |

## Building and Distribution

### Build Requirements
- .NET SDK (for dotnet build command)
- OR Visual Studio 2019+ with .NET desktop development workload

### Build Commands

```bash
# Restore NuGet packages
dotnet restore

# Build release version
dotnet build -c Release

# Output location
bin\Release\net35\Wasteland2AccessibilityMod.dll
```

### Distribution Checklist

- [ ] Build in Release mode
- [ ] Test with fresh game installation
- [ ] Verify MelonLoader console output
- [ ] Include README.md
- [ ] Include SETUP_GUIDE.md
- [ ] Package as ZIP with clear folder structure

## Troubleshooting Common Issues

### Issue: Patch Doesn't Apply

**Symptom**: No log message about setting InputMode

**Causes**:
1. Method signature changed in game update
2. Harmony version mismatch
3. MelonLoader not initializing patches

**Debug Steps**:
```csharp
// Add this to verify patch is being called
[HarmonyPrefix]
public static bool Prefix(string key, int defaultValue, ref int __result)
{
    MelonLogger.Msg($"PATCH CALLED: Key={key}");
    // ... rest of code
}
```

### Issue: Build Errors

**Missing Assembly References**:
- Verify paths in .csproj point to actual game installation
- Use absolute paths if relative paths fail

**NuGet Package Issues**:
```bash
dotnet nuget locals all --clear
dotnet restore --force
```

## Version History Technical Notes

### v1.0.0
- Initial implementation using Harmony prefix patches
- Dual patch strategy for both GetInt overloads
- MelonLoader attribute-based mod info

## References

- [MelonLoader Documentation](https://melonwiki.xyz/)
- [Harmony Documentation](https://harmony.pardeike.net/)
- [Unity PlayerPrefs Documentation](https://docs.unity3d.com/ScriptReference/PlayerPrefs.html)
