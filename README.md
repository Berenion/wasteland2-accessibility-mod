# Wasteland 2 Accessibility Mod

An accessibility mod for Wasteland 2 Director's Cut that sets Xbox controller as the default input method, making the game more accessible for controller users.

## Features

- Sets Xbox controller as the default input method instead of keyboard & mouse
- Works automatically on first launch
- Non-intrusive - only changes the default, players can still switch to keyboard if desired
- Compatible with MelonLoader mod framework

## How It Works

This mod uses Harmony patches to intercept the game's input mode initialization. When the game first launches (before any settings are saved), it defaults to Xbox controller input instead of keyboard & mouse.

### Technical Details

The mod patches `InxilePlayerPrefs.GetInt("InputMode")` to return:
- **1 (Xbox)** instead of **0 (Keyboard)** when no setting exists
- After the first launch, the user's preference is saved and respected

## Installation

### Prerequisites

1. **Wasteland 2 Director's Cut** installed
2. **MelonLoader** installed in your game directory
   - Download from: https://github.com/LavaGang/MelonLoader/releases
   - Run the installer and point it to your Wasteland 2 installation folder

### Installing the Mod

1. Download `Wasteland2AccessibilityMod.dll`
2. Place it in `[Game Directory]\Mods\`
   - Example: `C:\Program Files (x86)\Steam\steamapps\common\Wasteland 2 Director's Cut\Mods\`
3. Launch the game

### Verification

When the mod loads successfully, you should see these messages in the MelonLoader console:
```
[Wasteland 2 Accessibility Mod] Wasteland 2 Accessibility Mod loaded!
[Wasteland 2 Accessibility Mod] Setting Xbox controller as default input method...
```

## Building from Source

### Requirements

- .NET SDK (for building)
- Visual Studio 2019+ or VS Code with C# extension

### Build Steps

1. Update assembly references in `Wasteland2AccessibilityMod.csproj`:
   - Update paths to point to your Wasteland 2 installation's `Wasteland2_Data\Managed\` folder

2. Build using the command line:
   ```bash
   dotnet build -c Release
   ```

3. Or use the provided build script:
   ```bash
   build.bat
   ```

4. The compiled DLL will be in `bin\Release\net35\Wasteland2AccessibilityMod.dll`

### Manual Installation After Build

Copy the built DLL to your game's Mods folder:
```bash
copy bin\Release\net35\Wasteland2AccessibilityMod.dll "[Game Directory]\Mods\"
```

## Configuration

Currently, the mod sets Xbox controller as the default. To change the default input mode:

Edit `Wasteland2AccessibilityMod.cs` and change line with `__result = 1;` to:
- `0` = Keyboard & Mouse
- `1` = Xbox Controller
- `2` = PS4 Controller
- `3` = Steam Controller

Then rebuild the mod.

## Compatibility

- **Game Version**: Wasteland 2 Director's Cut
- **MelonLoader**: 0.6.1 or higher
- **Unity Version**: 4.x (as used by Wasteland 2)

## Troubleshooting

### Mod doesn't load
- Ensure MelonLoader is properly installed
- Check the MelonLoader console for error messages
- Verify the DLL is in the correct `Mods` folder

### Input mode still defaults to keyboard
- Delete or rename your game's settings file to force defaults:
  - Windows: `%USERPROFILE%\AppData\LocalLow\inXile entertainment\WL2DC\`
- The mod only affects the default when no saved preference exists

### Build errors
- Verify assembly reference paths in the .csproj file
- Ensure you have the correct .NET SDK installed
- Check that MelonLoader NuGet package is restored

## License

This is a community accessibility mod. Feel free to modify and share.

## Credits

Created for improved accessibility in Wasteland 2 Director's Cut.
