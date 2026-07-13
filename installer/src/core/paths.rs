//! Release coordinates, on-disk file names, and game-directory discovery.
//!
//! The "game dir" everywhere in this installer means the WL2 **Build** folder —
//! the one that holds WL2.exe, next to which Tolk.dll / nvdaControllerClient64.dll
//! live and under whose `Mods\` the mod DLL goes. MelonLoader also installs here.

use std::path::{Path, PathBuf};

/// GitHub `owner/repo` we pull mod releases from.
pub const MOD_REPO: &str = "Berenion/wasteland2-accessibility-mod";

/// MelonLoader source. We pin 0.5.7: 0.6.x and newer crash WL2 Director's Cut.
pub const MELONLOADER_REPO: &str = "LavaGang/MelonLoader";
pub const MELONLOADER_TAG: &str = "v0.5.7";
/// The manual-install archive on the MelonLoader release; extracting it into the
/// Build folder is equivalent to what the official installer lays down.
pub const MELONLOADER_ASSET: &str = "MelonLoader.x64.zip";

/// Release asset naming for the mod, e.g. `Wasteland2AccessibilityMod-0.7.0.zip`.
pub const MOD_ASSET_PREFIX: &str = "Wasteland2AccessibilityMod-";

/// Our install manifest, written into the Build folder.
pub const MANIFEST_FILE: &str = "wl2-access-manifest.json";

/// The mod DLL under `Mods\`, used to spot a hand-installed (unmanaged) copy.
pub const MOD_DLL_REL: &str = "Mods/Wasteland2AccessibilityMod.dll";

/// Candidate game executables (Steam and GOG have both been seen in the wild).
pub const GAME_EXES: &[&str] = &["WL2.exe", "Wasteland2.exe"];

/// Steam app id for Wasteland 2 Director's Cut.
const STEAM_APP_DIR: &str = "Wasteland 2 Director's Cut";

pub fn manifest_path(game_dir: &Path) -> PathBuf {
    game_dir.join(MANIFEST_FILE)
}

/// True if `dir` looks like the WL2 Build folder (holds a known game exe).
pub fn is_game_build_dir(dir: &Path) -> bool {
    GAME_EXES.iter().any(|exe| dir.join(exe).is_file())
}

/// Best-effort automatic discovery of WL2 Build folders across Steam libraries.
/// Returns every matching directory found (usually zero or one).
pub fn autodetect_game_dirs() -> Vec<PathBuf> {
    let mut found = Vec::new();
    for lib in steam_library_dirs() {
        let build = lib
            .join("steamapps")
            .join("common")
            .join(STEAM_APP_DIR)
            .join("Build");
        if is_game_build_dir(&build) && !found.contains(&build) {
            found.push(build);
        }
    }
    found
}

/// All Steam library root directories (the base install plus any extra libraries
/// listed in `libraryfolders.vdf`). Empty if Steam isn't installed.
fn steam_library_dirs() -> Vec<PathBuf> {
    let mut dirs = Vec::new();
    let steam = match steam_root() {
        Some(s) => s,
        None => return dirs,
    };
    dirs.push(steam.clone());

    // Parse the extra library paths out of libraryfolders.vdf. The format is a
    // Valve KeyValues blob; we only need the "path" entries, so a regex is enough
    // and avoids a VDF parser dependency.
    let vdf = steam.join("steamapps").join("libraryfolders.vdf");
    if let Ok(text) = std::fs::read_to_string(&vdf)
        && let Ok(re) = regex::Regex::new(r#""path"\s*"([^"]+)""#)
    {
        for cap in re.captures_iter(&text) {
            // VDF escapes backslashes as \\ — unescape to a real path.
            let p = PathBuf::from(cap[1].replace("\\\\", "\\"));
            if !dirs.contains(&p) {
                dirs.push(p);
            }
        }
    }
    dirs
}

/// Steam install root from the registry (HKCU first, then HKLM 32-bit view).
fn steam_root() -> Option<PathBuf> {
    use winreg::RegKey;
    use winreg::enums::*;

    let hkcu = RegKey::predef(HKEY_CURRENT_USER);
    if let Ok(key) = hkcu.open_subkey(r"Software\Valve\Steam")
        && let Ok(p) = key.get_value::<String, _>("SteamPath")
    {
        return Some(PathBuf::from(p));
    }
    let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
    if let Ok(key) = hklm.open_subkey(r"SOFTWARE\WOW6432Node\Valve\Steam")
        && let Ok(p) = key.get_value::<String, _>("InstallPath")
    {
        return Some(PathBuf::from(p));
    }
    None
}
