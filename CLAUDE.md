# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a **MelonLoader mod** for Wasteland 2 Director's Cut that provides screen reader accessibility support for UI navigation. The mod uses **Harmony runtime patching** to intercept UI focus changes and announce UI elements through the Tolk screen reader library.

## Building the Mod

### Before First Build

**IMPORTANT**: Update assembly reference paths in `Wasteland2AccessibilityMod.csproj` to match the local game installation:

```xml
<Reference Include="Assembly-CSharp">
  <HintPath>..\Wasteland2_Data\Managed\Assembly-CSharp.dll</HintPath>
</Reference>
```

Common game installation paths:
- Steam: `C:\Program Files (x86)\Steam\steamapps\common\Wasteland 2 Director's Cut\`
- GOG: `C:\GOG Games\Wasteland 2 Director's Cut\`

### Build Commands

```bash
# Restore NuGet packages (first time or after clean)
dotnet restore

# Build release version
dotnet build -c Release

# Clean build
dotnet clean
dotnet build -c Release

# Using build script (Windows)
build.bat
```

**Output location**: `bin\Release\net35\Wasteland2AccessibilityMod.dll`

### Post-Build Installation

The .csproj includes an auto-copy target that copies the DLL to `..\Mods\` if it exists. For manual installation:

```bash
copy bin\Release\net35\Wasteland2AccessibilityMod.dll "[Game Directory]\Mods\"
```

## Architecture

### Harmony Patching Strategy

The mod patches several UI-related methods to detect focus changes and announce UI elements:

1. **UICamera.SetSelection** - Detects when UI elements are selected
2. **UICamera.Notify** - Captures OnSelect(true) events for focus changes
3. **UIPopupList.Highlight** - Announces dropdown menu options as they're highlighted
4. **ModalMessageMenu.SetMessage** - Announces dialog content when it appears

### Screen Reader Integration

The mod uses the Tolk library to communicate with screen readers:
- Supports NVDA, JAWS, and SAPI
- Automatically detects available screen readers at startup
- Interrupts previous speech for new announcements
- Filters out non-interactive UI elements to reduce noise

### UI Text Extraction

The mod extracts and cleans UI text:
- Removes NGUI formatting codes (color tags, bold markers, etc.)
- Identifies element types (buttons, sliders, toggles, dropdowns)
- Provides context-aware announcements
- Prevents duplicate announcements from multiple hooks

## Testing the Mod

### Testing with Screen Reader

1. Build the mod
2. Install MelonLoader in game directory (if not already installed)
3. Copy DLL to `[Game]\Mods\`
4. Ensure Tolk.dll is in the game directory
5. Launch game with a screen reader (NVDA, JAWS, or SAPI) running
6. Check MelonLoader console for:
   ```
   [Wasteland 2 Accessibility Mod] Wasteland 2 Accessibility Mod loaded!
   [Wasteland 2 Accessibility Mod] Screen reader detected: [Reader Name]
   ```

### Testing UI Announcements

Navigate through the game's UI using keyboard or controller. The mod will announce:
- Button focus changes
- Dropdown menu options
- Toggle states (checked/unchecked)
- Modal dialog content
- Interactive UI elements

## Common Issues

### Build Fails: Assembly References Not Found

Update `<HintPath>` in .csproj to point to the actual game installation's `Wasteland2_Data\Managed\` folder.

### Screen Reader Not Working

- Ensure Tolk.dll is in the game directory
- Check MelonLoader console for Tolk initialization errors
- Verify a screen reader (NVDA, JAWS, or SAPI) is running
- Check that the screen reader is configured to work with games

### UI Elements Not Being Announced

- Check MelonLoader console for focus change logs
- Some UI elements may be filtered as non-interactive
- Verify the element has a UILabel component or interactive component

## Target Framework

- **.NET Framework 3.5** (Unity 4.x compatibility)
- **MelonLoader 0.6.1+**
- Uses Harmony for runtime patching (included with MelonLoader)

## Extending the Mod

To add announcements for additional UI elements:

1. Identify the UI method that needs patching (use decompiled code)
2. Create a new Harmony patch class:
   ```csharp
   [HarmonyPatch(typeof(ClassName), "MethodName")]
   public class ClassName_MethodName_Patch
   {
       [HarmonyPostfix]
       public static void Postfix(/* method parameters */)
       {
           // Extract text and call AccessibilityMod.SpeakText()
       }
   }
   ```

For filtering specific UI elements, modify the `IsInteractiveElement()` method to add or exclude patterns.
