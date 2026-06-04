<#
.SYNOPSIS
  Builds the mod in Release and assembles the distributable release zip.

.DESCRIPTION
  Produces dist\Wasteland2AccessibilityMod-v<version>.zip containing:
    Mods\Wasteland2AccessibilityMod.dll   - the mod (goes in <game>\Mods\)
    Tolk.dll                              - bundled Tolk bridge (goes next to WL2.exe)
    README.md                             - install + usage docs

  The zip mirrors the game's Build folder layout, so it can be extracted straight
  into the Wasteland 2 "Build" directory if desired. Tolk.dll is the only runtime
  DLL bundled; NVDA users still supply nvdaControllerClient64.dll themselves.
#>
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# Read the version from the csproj so the zip name tracks the build.
$version = ([xml](Get-Content "$root\Wasteland2AccessibilityMod.csproj")).Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) { $version = '0.0.0' }

$modDll  = "$root\bin\Release\net35\Wasteland2AccessibilityMod.dll"
$tolkDll = "$root\redist\Tolk.dll"
$readme  = "$root\README.md"

Write-Host "Building Release..." -ForegroundColor Cyan
dotnet build "$root\Wasteland2AccessibilityMod.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

foreach ($f in @($modDll, $tolkDll, $readme)) {
    if (-not (Test-Path $f)) { throw "Missing required file: $f" }
}

# Stage into a clean folder that mirrors the game's Build layout.
$stage = "$root\dist\stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path "$stage\Mods" -Force | Out-Null

Copy-Item $modDll  "$stage\Mods\Wasteland2AccessibilityMod.dll" -Force
Copy-Item $tolkDll "$stage\Tolk.dll" -Force
Copy-Item $readme  "$stage\README.md" -Force

$zip = "$root\dist\Wasteland2AccessibilityMod-v$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zip -Force
Remove-Item $stage -Recurse -Force

Write-Host "Created $zip" -ForegroundColor Green
