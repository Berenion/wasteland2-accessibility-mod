<#
.SYNOPSIS
  Builds the release assets (mod zip + installer exe) and, with -Publish,
  publishes them as a GitHub prerelease.

.DESCRIPTION
  Orchestrates the two builders:
    package.ps1                     -> dist\Wasteland2AccessibilityMod-v<version>.zip
    installer\build-installer.ps1   -> dist\Wasteland2AccessibilityMod-Installer.exe

  The zip asset name (Wasteland2AccessibilityMod-v<version>.zip) and its
  Build-mirrored layout are what the installer's auto-updater expects — don't
  rename it without updating installer\src\core\paths.rs.

  The mod is in beta, so releases are marked --prerelease. That's deliberate:
  the installer queries the /releases list and includes prereleases by default,
  so beta builds are picked up (a plain "latest" release would hide them).

.PARAMETER Publish
  Actually create/upload the GitHub release. Without it this is a dry run that
  only builds the assets into dist\ and prints what would be uploaded.

.PARAMETER Notes
  Optional release notes. Defaults to a minimal auto-generated line.
#>
param(
    [switch]$Publish,
    [string]$Notes
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$version = ([xml](Get-Content "$root\Wasteland2AccessibilityMod.csproj")).Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) { throw "Could not read <Version> from the csproj." }
$tag = "v$version"

Write-Host "Preparing release $tag" -ForegroundColor Cyan

# 1. Mod release zip (builds Release + assembles the Build-mirrored archive).
& "$root\package.ps1"
$zip = "$root\dist\Wasteland2AccessibilityMod-v$version.zip"
if (-not (Test-Path $zip)) { throw "Mod zip not produced: $zip" }

# 2. Installer exe.
& "$root\installer\build-installer.ps1"
$exe = "$root\dist\Wasteland2AccessibilityMod-Installer.exe"
if (-not (Test-Path $exe)) { throw "Installer exe not produced: $exe" }

Write-Host "`nAssets:" -ForegroundColor Cyan
Write-Host "  $zip"
Write-Host "  $exe"

if (-not $Publish) {
    Write-Host "`nDry run (no -Publish): assets built but not uploaded." -ForegroundColor Yellow
    Write-Host "To publish the prerelease: .\create-release.ps1 -Publish" -ForegroundColor Yellow
    return
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "gh CLI not found. Install it from https://cli.github.com/ to publish."
}
if (-not $Notes) { $Notes = "Wasteland 2 Accessibility Mod $tag (beta prerelease)." }

# Pass notes via a temp file, not --notes: a multi-line notes string handed to
# gh inline gets mangled (backticks/markdown are re-parsed and gh fails with
# "no matches found"). --notes-file takes the body verbatim.
$notesFile = Join-Path ([System.IO.Path]::GetTempPath()) "wl2mod-release-notes-$version.md"
Set-Content -Path $notesFile -Value $Notes -Encoding utf8

# Create the prerelease, or upload assets to it if the tag already exists.
# gh writes progress to stderr; relax ErrorActionPreference around the native
# calls (as with cargo) and gate on the exit code.
$prevEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    gh release view $tag 1>$null 2>$null
    $exists = ($LASTEXITCODE -eq 0)
    if ($exists) {
        Write-Host "Release $tag exists; uploading assets (clobber)." -ForegroundColor Cyan
        gh release upload $tag $zip $exe --clobber
    } else {
        Write-Host "Creating prerelease $tag..." -ForegroundColor Cyan
        gh release create $tag $zip $exe --prerelease --title $tag --notes-file $notesFile
    }
    $code = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $prevEap
    Remove-Item $notesFile -ErrorAction SilentlyContinue
}
if ($code -ne 0) { throw "gh release publish failed (exit $code)." }
Write-Host "Published $tag." -ForegroundColor Green
