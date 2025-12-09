@echo off
echo ========================================
echo Wasteland 2 Accessibility Mod Builder
echo ========================================
echo.

REM Check if dotnet is available
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: .NET SDK not found!
    echo Please install .NET SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Restoring NuGet packages...
dotnet restore
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to restore packages!
    pause
    exit /b 1
)

echo.
echo Building mod (Release configuration)...
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Build successful!
echo ========================================
echo.
echo The mod DLL is located at:
echo bin\Release\net35\Wasteland2AccessibilityMod.dll
echo.
echo To install, copy this file to your game's Mods folder:
echo [Game Directory]\Mods\Wasteland2AccessibilityMod.dll
echo.
pause
