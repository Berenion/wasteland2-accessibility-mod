//! Front-end-agnostic install flow: a `plan` step (query GitHub, classify the
//! install, decide the action) and an `apply` step that performs it while
//! reporting progress through a callback. The CLI and GUI share both, so the
//! logic lives in one place and the front-ends only handle presentation.

use super::detect::{self, InstallState};
use super::github::{self, Asset};
use super::install::{self, UpdateDecision};
use super::{melonloader, paths, process};
use std::path::Path;

/// The result of inspecting an install: what's available, what's installed, and
/// what installing would do. Cheap to show; holds the chosen asset so `apply`
/// installs exactly what was reported.
pub struct Plan {
    pub latest: semver::Version,
    pub tag: String,
    pub prerelease: bool,
    pub asset: Asset,
    pub installed: Option<semver::Version>,
    pub decision: UpdateDecision,
    pub melonloader_present: bool,
    pub melonloader_incompatible: bool,
}

impl Plan {
    /// One-line human summary of the action, e.g. "update 0.8.0 -> 0.8.1".
    pub fn summary(&self) -> String {
        let action = match self.decision {
            UpdateDecision::FreshInstall => "fresh install",
            UpdateDecision::Update => "update",
            UpdateDecision::Reinstall => "reinstall",
            UpdateDecision::UpToDate => "already up to date",
        };
        match &self.installed {
            Some(v) if *v != self.latest => format!("{action} {v} -> {}", self.latest),
            _ => format!("{action} ({})", self.latest),
        }
    }
}

/// Inspect the game folder and the latest release without changing anything.
pub fn plan(game_dir: &Path, include_prerelease: bool) -> Result<Plan, String> {
    let (release, asset, latest) =
        github::find_latest_mod_release(paths::MOD_REPO, include_prerelease)?;
    let state = detect::classify_install(game_dir);
    Ok(Plan {
        installed: detect::installed_version(&state),
        decision: install::decide(&state, &latest),
        melonloader_present: detect::melonloader_present(game_dir),
        melonloader_incompatible: detect::melonloader_looks_incompatible(game_dir),
        latest,
        tag: release.tag_name,
        prerelease: release.prerelease,
        asset,
    })
}

/// Perform the install/update described by `plan`, reporting progress via `log`.
/// Refuses to run while the game is open. Returns a final success message.
pub fn apply<F: FnMut(&str)>(
    game_dir: &Path,
    plan: &Plan,
    force_melonloader: bool,
    mut log: F,
) -> Result<String, String> {
    if process::game_running() {
        return Err("Wasteland 2 is running. Close the game and try again.".to_string());
    }

    let force_melon = force_melonloader || plan.melonloader_incompatible;

    log("Ensuring MelonLoader 0.5.7...");
    let ml_now = melonloader::ensure_melonloader(game_dir, force_melon)?;
    log(if ml_now {
        "MelonLoader: installed 0.5.7"
    } else {
        "MelonLoader: already present"
    });

    log(&format!("Downloading {}...", plan.asset.name));
    let tmp = std::env::temp_dir().join(&plan.asset.name);
    github::download_asset(&plan.asset, &tmp)?;

    log("Installing mod files...");
    let state = detect::classify_install(game_dir);
    let melon_flag = ml_now
        || matches!(&state, InstallState::Managed(m) if m.melonloader_installed);
    let manifest = install::install_mod(game_dir, &plan.asset, &plan.latest, &tmp, &state, melon_flag)?;
    let _ = std::fs::remove_file(&tmp);

    Ok(format!(
        "Done. Installed mod {} ({} files).",
        manifest.mod_version,
        manifest.installed_files.len()
    ))
}
