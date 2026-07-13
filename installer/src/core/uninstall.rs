//! Remove the mod (and optionally MelonLoader) from the Build folder, driven by
//! the install manifest so we only delete what we put there.

use super::detect::{self, InstallState};
use super::{paths, process};
use std::fs;
use std::path::Path;

pub struct Report {
    /// Files actually deleted.
    pub removed: usize,
    /// Manifest-listed files that were already gone.
    pub missing: usize,
    /// Whether MelonLoader was removed as well.
    pub melonloader_removed: bool,
}

/// The mod files a hand-install (no manifest) is known to place, used for a
/// best-effort uninstall when there's no manifest to consult.
fn default_files() -> Vec<String> {
    vec![
        paths::MOD_DLL_REL.to_string(),
        "Tolk.dll".to_string(),
        "nvdaControllerClient64.dll".to_string(),
    ]
}

/// Remove the mod from `game_dir`. Refuses to run while the game is open. With
/// `remove_melonloader`, also deletes the MelonLoader loader files.
pub fn uninstall(game_dir: &Path, remove_melonloader: bool) -> Result<Report, String> {
    if process::game_running() {
        return Err("Wasteland 2 is running. Close the game and try again.".to_string());
    }

    let files = match detect::classify_install(game_dir) {
        InstallState::Managed(m) => m.installed_files,
        InstallState::Unmanaged => default_files(),
        InstallState::Fresh => {
            return Err("The mod isn't installed in this folder.".to_string());
        }
    };

    let mut removed = 0;
    let mut missing = 0;
    for rel in &files {
        let path = game_dir.join(rel);
        if path.is_file() {
            fs::remove_file(&path).map_err(|e| format!("removing {rel}: {e}"))?;
            removed += 1;
        } else {
            missing += 1;
        }
    }

    // Drop our manifest last so a failure above leaves the install still tracked.
    let manifest = paths::manifest_path(game_dir);
    if manifest.is_file() {
        let _ = fs::remove_file(&manifest);
    }

    let melonloader_removed = if remove_melonloader {
        remove_melonloader_files(game_dir)?
    } else {
        false
    };

    Ok(Report {
        removed,
        missing,
        melonloader_removed,
    })
}

/// Delete the MelonLoader proxy DLLs and its folder. Leaves UserData/Mods (user
/// content) alone. Returns whether anything was removed.
fn remove_melonloader_files(game_dir: &Path) -> Result<bool, String> {
    let mut any = false;
    for name in ["version.dll", "dobby.dll"] {
        let path = game_dir.join(name);
        if path.is_file() {
            fs::remove_file(&path).map_err(|e| format!("removing {name}: {e}"))?;
            any = true;
        }
    }
    let dir = game_dir.join("MelonLoader");
    if dir.is_dir() {
        fs::remove_dir_all(&dir).map_err(|e| format!("removing MelonLoader folder: {e}"))?;
        any = true;
    }
    Ok(any)
}

impl Report {
    /// One-line human summary.
    pub fn summary(&self) -> String {
        let mut s = format!("Removed {} mod file(s)", self.removed);
        if self.missing > 0 {
            s += &format!(" ({} already missing)", self.missing);
        }
        if self.melonloader_removed {
            s += ", and removed MelonLoader";
        }
        s += ".";
        s
    }
}
