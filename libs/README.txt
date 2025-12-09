REQUIRED GAME ASSEMBLY REFERENCES
==================================

To build the Wasteland 2 Accessibility Mod, you need to copy the following DLL files
from your Wasteland 2 Director's Cut installation to this folder:

1. Assembly-CSharp.dll
2. UnityEngine.dll
3. UnityEngine.CoreModule.dll

WHERE TO FIND THESE FILES:
--------------------------

These files are located in your game installation at:
[Game Directory]\Wasteland2_Data\Managed\

Common game installation locations:

Steam:
C:\Program Files (x86)\Steam\steamapps\common\Wasteland 2 Director's Cut\Wasteland2_Data\Managed\

GOG:
C:\GOG Games\Wasteland 2 Director's Cut\Wasteland2_Data\Managed\


HOW TO COPY:
------------

1. Locate your Wasteland 2 installation folder
2. Navigate to: Wasteland2_Data\Managed\
3. Copy the three DLL files listed above
4. Paste them into this libs folder

Once these files are in place, you can build the mod using:
  dotnet build -c Release

or run:
  build.bat


NOTE: These files are NOT included in the repository as they are proprietary
game files owned by inXile Entertainment.
