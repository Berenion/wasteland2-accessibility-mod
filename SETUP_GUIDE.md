# Setup Guide: Installing MelonLoader and the Accessibility Mod

This guide walks you through setting up MelonLoader and installing the Wasteland 2 Accessibility Mod.

## Step 1: Install MelonLoader

MelonLoader is required to load mods in Unity games like Wasteland 2.

### Download MelonLoader

1. Go to: https://github.com/LavaGang/MelonLoader/releases
2. Download the latest **MelonLoader.Installer.exe**

### Install MelonLoader

1. Run **MelonLoader.Installer.exe**
2. Click **"SELECT"** and browse to your Wasteland 2 installation folder
   - Steam default: `C:\Program Files (x86)\Steam\steamapps\common\Wasteland 2 Director's Cut\`
   - GOG default: `C:\GOG Games\Wasteland 2 Director's Cut\`
3. Click **"INSTALL"** or **"UPDATE"**
4. Wait for the installation to complete
5. Close the installer

### Verify MelonLoader Installation

After installation, your game folder should contain:
- `MelonLoader` folder
- `version.dll`
- `dobby.dll` (on some versions)

## Step 2: Create Mods Folder

1. Navigate to your Wasteland 2 installation folder
2. Create a new folder named **"Mods"** if it doesn't exist
   - Full path example: `C:\Program Files (x86)\Steam\steamapps\common\Wasteland 2 Director's Cut\Mods\`

## Step 3: Build or Download the Mod

### Option A: Use Pre-built DLL (Easier)

1. Download the pre-built `Wasteland2AccessibilityMod.dll`
2. Skip to Step 4

### Option B: Build from Source

1. Ensure you have the .NET SDK installed
   - Download from: https://dotnet.microsoft.com/download

2. Update the assembly reference paths in `Wasteland2AccessibilityMod.csproj`:
   ```xml
   <Reference Include="Assembly-CSharp">
     <HintPath>YOUR_GAME_PATH\Wasteland2_Data\Managed\Assembly-CSharp.dll</HintPath>
     <Private>False</Private>
   </Reference>
   ```

3. Open Command Prompt or PowerShell in the mod folder

4. Run the build script:
   ```bash
   build.bat
   ```

   Or manually build:
   ```bash
   dotnet build -c Release
   ```

5. The DLL will be in `bin\Release\net35\Wasteland2AccessibilityMod.dll`

## Step 4: Install the Mod

1. Copy `Wasteland2AccessibilityMod.dll` to the game's **Mods** folder:
   ```
   [Game Directory]\Mods\Wasteland2AccessibilityMod.dll
   ```

2. Your Mods folder should now contain:
   ```
   Mods\
   └── Wasteland2AccessibilityMod.dll
   ```

## Step 5: Launch the Game

1. Launch Wasteland 2 normally (through Steam, GOG, or the executable)
2. MelonLoader will start first and show a console window
3. Wait for all mods to load

### Expected Console Output

You should see messages like:
```
[MelonLoader] Loading Mods...
[MelonLoader] Loading Wasteland2AccessibilityMod.dll
[Wasteland 2 Accessibility Mod] Wasteland 2 Accessibility Mod loaded!
[Wasteland 2 Accessibility Mod] Setting Xbox controller as default input method...
[Wasteland 2 Accessibility Mod] Applying Harmony patches...
```

## Step 6: Verify the Mod Works

### First Launch (New Installation)

1. When you reach the main menu, go to **Options > Gameplay**
2. Check the **Input** setting - it should default to **Xbox**
3. If you prefer a different controller (PS4, Steam Controller), you can change it here

### Existing Installation

If you already have Wasteland 2 installed with saved settings:

1. The mod only changes the *default* when no preference is saved
2. To test the mod's functionality, you need to reset your input preference:
   - **Option A**: In-game, go to Options > Gameplay > Reset to Defaults
   - **Option B**: Delete the saved preferences file:
     - Windows: `%USERPROFILE%\AppData\LocalLow\inXile entertainment\WL2DC\`
     - Delete or rename the preferences file
3. Restart the game

## Troubleshooting

### MelonLoader Console Doesn't Appear

- **Cause**: MelonLoader may not be properly installed
- **Solution**: Re-run the MelonLoader installer and ensure it completes successfully

### Mod Doesn't Load

**Check the MelonLoader console for errors:**

1. Look for red error messages
2. Common issues:
   - Missing dependencies: Install the latest MelonLoader version
   - Wrong folder: Ensure the DLL is in the `Mods` folder, not `Plugins`

### Input Still Defaults to Keyboard

1. **If you have existing saves/settings:**
   - The mod only affects the default for new installations
   - Delete your settings file (see "Existing Installation" above)

2. **Check the console:**
   - Look for the message: `Setting default InputMode to Xbox (1)`
   - If missing, the mod may not be loading

3. **Verify mod is active:**
   - In MelonLoader console, confirm mod loaded successfully
   - Check that there are no error messages

### Build Errors

**"Assembly-CSharp.dll not found":**
- Update the `<HintPath>` in the .csproj file to point to your actual game installation

**"MelonLoader package not found":**
```bash
dotnet restore
```

**".NET SDK not found":**
- Install .NET SDK from: https://dotnet.microsoft.com/download

## Advanced Configuration

### Changing Default Input Mode

To set a different controller as default:

1. Open `Wasteland2AccessibilityMod.cs` in a text editor
2. Find the line: `__result = 1; // InputMode.Xbox`
3. Change the number:
   - `0` = Keyboard & Mouse
   - `1` = Xbox Controller
   - `2` = PS4 Controller
   - `3` = Steam Controller
4. Rebuild the mod using `build.bat`
5. Replace the DLL in the Mods folder

## Uninstalling

### Remove the Mod
Simply delete `Wasteland2AccessibilityMod.dll` from the Mods folder

### Remove MelonLoader
1. Delete the `MelonLoader` folder from your game directory
2. Delete `version.dll` and `dobby.dll` (if present)
3. Verify game files through Steam/GOG to restore original files

## Support

If you encounter issues:

1. Check the MelonLoader console for error messages
2. Verify all installation steps were followed correctly
3. Ensure you're using compatible versions of:
   - Wasteland 2 Director's Cut
   - MelonLoader (0.6.1+)
   - .NET Framework 3.5 (usually pre-installed on Windows)

## Notes

- The mod is non-destructive and only changes the default input preference
- Users can still change their input method in the game's settings
- The mod does not modify any game files directly
- Controller must be connected before launching the game for best results
