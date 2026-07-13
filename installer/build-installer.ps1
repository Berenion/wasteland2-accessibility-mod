<#
.SYNOPSIS
  Builds the Rust installer in release mode and stages the exe into dist\.

.DESCRIPTION
  Produces dist\Wasteland2AccessibilityMod-Installer.exe — a standalone,
  self-updating installer that requests administrator elevation (see build.rs)
  so it can write into Program Files installs. The exe name is version-agnostic
  on purpose: it updates itself and the mod from GitHub, so users keep one file.

.PARAMETER OutDir
  Where to place the exe. Defaults to the repo's dist\ folder.
#>
param(
    [string]$OutDir
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot                    # installer\
$repo = Split-Path $root -Parent         # repo root
if (-not $OutDir) { $OutDir = Join-Path $repo 'dist' }

# Locate cargo: PATH first, then the default rustup install location.
$cargo = (Get-Command cargo -ErrorAction SilentlyContinue).Source
if (-not $cargo) { $cargo = Join-Path $env:USERPROFILE '.cargo\bin\cargo.exe' }
if (-not (Test-Path $cargo)) {
    throw "cargo not found. Install Rust from https://rustup.rs and re-run."
}

# The wxdragon GUI's native build (wxdragon-sys) needs libclang (bindgen) and
# cmake + ninja (C++ glue). These often aren't on PATH, so locate them from an
# LLVM install and Visual Studio's bundled copies.
if (-not $env:LIBCLANG_PATH) {
    foreach ($c in @("$env:ProgramFiles\LLVM\bin", "${env:ProgramFiles(x86)}\LLVM\bin")) {
        if (Test-Path (Join-Path $c 'libclang.dll')) { $env:LIBCLANG_PATH = $c; break }
    }
}
if (-not $env:LIBCLANG_PATH) {
    Write-Warning "libclang not found. If the build fails on bindgen, run: winget install LLVM.LLVM"
}
$missingCmake = -not (Get-Command cmake -ErrorAction SilentlyContinue)
$missingNinja = -not (Get-Command ninja -ErrorAction SilentlyContinue)
if ($missingCmake -or $missingNinja) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $vsPath = & $vswhere -latest -products * -property installationPath
        if ($vsPath) {
            $ext = Join-Path $vsPath 'Common7\IDE\CommonExtensions\Microsoft\CMake'
            foreach ($d in @((Join-Path $ext 'CMake\bin'), (Join-Path $ext 'Ninja'))) {
                if (Test-Path $d) { $env:PATH = "$d;$env:PATH" }
            }
        }
    }
}

Write-Host "Building installer (release)..." -ForegroundColor Cyan
# cargo writes progress to stderr; under ErrorActionPreference=Stop that would be
# treated as a terminating error even on success. Relax it around the native call
# and gate on the real exit code instead.
$prevEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
& $cargo build --release --manifest-path (Join-Path $root 'Cargo.toml')
$code = $LASTEXITCODE
$ErrorActionPreference = $prevEap
if ($code -ne 0) { throw "cargo build failed (exit $code)." }

$builtExe = Join-Path $root 'target\release\wl2-access-installer.exe'
if (-not (Test-Path $builtExe)) { throw "Built exe not found: $builtExe" }

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$destExe = Join-Path $OutDir 'Wasteland2AccessibilityMod-Installer.exe'
Copy-Item $builtExe $destExe -Force
Write-Host "Created $destExe" -ForegroundColor Green
