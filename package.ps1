<#
.SYNOPSIS
  Builds the mod in Release and assembles the distributable release zip.

.DESCRIPTION
  Produces dist\Wasteland2AccessibilityMod-v<version>.zip containing:
    Mods\Wasteland2AccessibilityMod.dll   - the mod (goes in <game>\Mods\)
    Tolk.dll                              - bundled Tolk bridge (goes next to WL2.exe)
    nvdaControllerClient64.dll            - NVDA controller client (goes next to WL2.exe)
    README.md                             - install + usage docs

  The zip mirrors the game's Build folder layout, so it can be extracted straight
  into the Wasteland 2 "Build" directory if desired. Tolk.dll and the NVDA
  controller client are bundled so NVDA users get speech out of the box; JAWS and
  SAPI users are handled by Tolk without extra files.
#>
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# Read the version from the csproj so the zip name tracks the build.
$version = ([xml](Get-Content "$root\Wasteland2AccessibilityMod.csproj")).Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) { $version = '0.0.0' }

$modDll  = "$root\bin\Release\net35\Wasteland2AccessibilityMod.dll"
$tolkDll = "$root\redist\Tolk.dll"
$nvdaDll = "$root\redist\nvdaControllerClient64.dll"
$readme  = "$root\README.md"

Write-Host "Building Release..." -ForegroundColor Cyan
dotnet build "$root\Wasteland2AccessibilityMod.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

foreach ($f in @($modDll, $tolkDll, $nvdaDll, $readme)) {
    if (-not (Test-Path $f)) { throw "Missing required file: $f" }
}

# Stage into a clean folder that mirrors the game's Build layout.
$stage = "$root\dist\stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path "$stage\Mods" -Force | Out-Null

Copy-Item $modDll  "$stage\Mods\Wasteland2AccessibilityMod.dll" -Force
Copy-Item $tolkDll "$stage\Tolk.dll" -Force
Copy-Item $nvdaDll "$stage\nvdaControllerClient64.dll" -Force
Copy-Item $readme  "$stage\README.md" -Force

$zip = "$root\dist\Wasteland2AccessibilityMod-v$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zip -Force
Remove-Item $stage -Recurse -Force

Write-Host "Created $zip" -ForegroundColor Green
