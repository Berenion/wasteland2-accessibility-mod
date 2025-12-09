# Wasteland 2 Accessibility Mod - Project Summary

## Overview

This project creates a MelonLoader DLL mod for Wasteland 2 Director's Cut that provides screen reader accessibility support for UI navigation, improving accessibility for visually impaired users.

## Project Structure

```
Wasteland2AccessibilityMod/
├── Wasteland2AccessibilityMod.cs      # Main mod source code
├── Wasteland2AccessibilityMod.csproj  # Project file for building
├── build.bat                          # Automated build script
├── .gitignore                         # Git ignore rules
├── README.md                          # User documentation
├── SETUP_GUIDE.md                     # Installation instructions
├── TECHNICAL_DETAILS.md               # Developer documentation
├── CHANGELOG.md                       # Version history
└── PROJECT_SUMMARY.md                 # This file
```

## File Descriptions

### Source Code

#### `Wasteland2AccessibilityMod.cs`
**Purpose**: Main mod implementation
**Contains**:
- `AccessibilityMod` class - MelonLoader mod entry point with Tolk screen reader integration
- `UICamera_SetSelection_Patch` - Harmony patch for UI selection changes
- `UICamera_Notify_Patch` - Harmony patch for UI focus notifications
- `UIPopupList_Highlight_Patch` - Harmony patch for dropdown menu announcements
- `ModalMessageMenu_SetMessage_Patch` - Harmony patch for dialog announcements

**Key Features**:
- Announces UI elements through screen readers (NVDA, JAWS, SAPI)
- Filters non-interactive UI elements to reduce noise
- Cleans NGUI formatting codes from announcements
- Context-aware element type detection
- Prevents duplicate announcements

### Build Configuration

#### `Wasteland2AccessibilityMod.csproj`
**Purpose**: MSBuild project file
**Configuration**:
- Target Framework: .NET 3.5 (Unity 4.x compatibility)
- Dependencies: MelonLoader 0.6.1
- References: Game assemblies (Assembly-CSharp, UnityEngine)
- Post-build: Auto-copy to Mods folder

**Note**: Update `<HintPath>` values to match your game installation path

#### `build.bat`
**Purpose**: Simplified build script
**Features**:
- Checks for .NET SDK
- Restores NuGet packages
- Builds Release configuration
- Shows output location
- User-friendly error messages

### Documentation

#### `README.md`
**Audience**: End users and mod users
**Contents**:
- Feature overview
- Installation instructions
- Build from source guide
- Configuration options
- Troubleshooting
- Compatibility information

#### `SETUP_GUIDE.md`
**Audience**: First-time modders
**Contents**:
- Step-by-step MelonLoader installation
- Mod installation walkthrough
- Verification steps
- Common issues and solutions
- Screenshots locations (to be added)

#### `TECHNICAL_DETAILS.md`
**Audience**: Developers and advanced users
**Contents**:
- Architecture explanation
- Code flow diagrams
- Harmony patch mechanics
- PlayerPrefs storage details
- Performance analysis
- Debugging guide
- Extension examples

#### `CHANGELOG.md`
**Audience**: All users
**Contents**:
- Version history
- Feature additions
- Bug fixes
- Future roadmap

#### `PROJECT_SUMMARY.md`
**Audience**: Project maintainers
**Contents**:
- Project overview (this document)
- File structure
- Quick reference

### Git Configuration

#### `.gitignore`
**Purpose**: Version control exclusions
**Excludes**:
- Build outputs (bin/, obj/)
- IDE files (.vs/, .vscode/)
- Compiled binaries (*.dll, *.exe)
- NuGet packages

## Quick Start Guide

### For Mod Users

1. Install MelonLoader (see SETUP_GUIDE.md)
2. Download Wasteland2AccessibilityMod.dll
3. Place in `[Game Directory]\Mods\`
4. Launch game

### For Developers

1. Clone repository
2. Update assembly references in .csproj
3. Run `build.bat`
4. Copy DLL from `bin\Release\net35\` to game's Mods folder
5. Test in-game

## Build Instructions

### Prerequisites
- .NET SDK or Visual Studio 2019+
- Wasteland 2 Director's Cut installed
- MelonLoader installed in game directory

### Build Steps

**Windows Command Prompt:**
```batch
cd Wasteland2AccessibilityMod
build.bat
```

**PowerShell or Linux/Mac:**
```bash
cd Wasteland2AccessibilityMod
dotnet restore
dotnet build -c Release
```

**Output:**
```
bin/Release/net35/Wasteland2AccessibilityMod.dll
```

## Installation Paths

### Game Installation (Default Locations)

**Steam:**
```
C:\Program Files (x86)\Steam\steamapps\common\Wasteland 2 Director's Cut\
```

**GOG:**
```
C:\GOG Games\Wasteland 2 Director's Cut\
```

### Mod Installation
```
[Game Directory]\Mods\Wasteland2AccessibilityMod.dll
```

### Assembly References (for building)
```
[Game Directory]\Wasteland2_Data\Managed\
├── Assembly-CSharp.dll
├── UnityEngine.dll
└── UnityEngine.CoreModule.dll
```

## Configuration

### Screen Reader Support

The mod automatically detects and works with:
- NVDA
- JAWS
- SAPI (Windows built-in)

No configuration required - the mod detects available screen readers at startup.

### Customizing Announcements

Edit `Wasteland2AccessibilityMod.cs` to modify:
- `IsInteractiveElement()` - Filter which UI elements are announced
- `ExtractUIText()` - Change how text is extracted and formatted
- Add new Harmony patches for additional UI elements

## Testing Checklist

### Pre-Release Testing

- [ ] Build succeeds without errors
- [ ] MelonLoader loads the mod
- [ ] Mod logs initialization message
- [ ] Screen reader is detected at startup
- [ ] UI elements are announced when focused
- [ ] Dropdown menus announce options
- [ ] Modal dialogs are announced
- [ ] No errors in MelonLoader console
- [ ] No game crashes or freezes

### Compatibility Testing

- [ ] Fresh game installation
- [ ] Existing game with saved settings
- [ ] With other mods installed
- [ ] Steam version of game
- [ ] GOG version of game

## Troubleshooting Reference

### Mod Doesn't Load
1. Check MelonLoader is installed
2. Verify DLL is in Mods folder (not Plugins)
3. Review MelonLoader console for errors
4. Ensure MelonLoader version is 0.6.1+

### Build Fails
1. Update assembly reference paths in .csproj
2. Restore NuGet packages: `dotnet restore`
3. Check .NET SDK is installed
4. Verify game installation path is correct

### Screen Reader Not Announcing
1. Verify screen reader is running (NVDA, JAWS, or SAPI)
2. Check console for "Screen reader detected" message
3. Ensure Tolk.dll is in game directory
4. Verify mod is actually loading (check console)

## Version Information

**Current Version**: 1.0.0
**Release Date**: 2025-12-06
**Compatibility**:
- MelonLoader: 0.6.1+
- Game: Wasteland 2 Director's Cut (all versions)
- Unity: 4.x
- .NET: Framework 3.5

## Future Development

### Planned Features (v1.1.0)
- Announce combat state changes
- Announce inventory changes
- Character status announcements

### Planned Features (v1.2.0)
- Configuration file for verbosity levels
- Customizable announcement patterns
- Sound cues for important events

### Planned Features (v2.0.0)
- Complete accessibility overhaul
- Voice commands
- Enhanced keyboard navigation

## Contributing

### Code Style
- Use descriptive variable names
- Comment complex logic
- Follow existing formatting

### Testing
- Test on fresh installation
- Test with existing saves
- Verify backward compatibility

### Documentation
- Update README for user-facing changes
- Update TECHNICAL_DETAILS for code changes
- Add entries to CHANGELOG

## License

Community mod - free to use, modify, and distribute.

## Credits

Created for the Wasteland 2 accessibility community.

## Contact

For issues, suggestions, or contributions, please refer to the project repository.

## Quick Reference Commands

```bash
# Build
dotnet build -c Release

# Clean build
dotnet clean
dotnet build -c Release

# Restore packages
dotnet restore

# Build and install (manual)
dotnet build -c Release
copy bin\Release\net35\Wasteland2AccessibilityMod.dll "[Game]\Mods\"
```

## File Sizes (Approximate)

- Source code: ~8 KB
- Compiled DLL: ~20 KB
- Total project: ~30 KB (excluding bin/obj)

## Dependencies

**Runtime (included with MelonLoader)**:
- MelonLoader.dll
- 0Harmony.dll

**Build-time (NuGet)**:
- MelonLoader package (0.6.1)

**Game References** (not distributed):
- Assembly-CSharp.dll
- UnityEngine.dll
- UnityEngine.CoreModule.dll

---

**Document Version**: 1.0
**Last Updated**: 2025-12-06
