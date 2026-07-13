//! Classify the current state of an install so the CLI/GUI can choose an action
//! (fresh install, update, repair) and reason about MelonLoader.

use super::manifest::InstallManifest;
use super::paths;
use std::path::Path;

#[derive(Debug)]
pub enum InstallState {
    /// No mod files and no manifest — a clean Build folder.
    Fresh,
    /// We manage this install; carries the manifest for version comparison.
    Managed(InstallManifest),
    /// Mod files are present but there's no manifest (hand-installed). We can
    /// take it over on the next install, backing up nothing we didn't write.
    Unmanaged,
}

pub fn classify_install(game_dir: &Path) -> InstallState {
    if let Some(m) = InstallManifest::read(game_dir) {
        return InstallState::Managed(m);
    }
    if mod_files_present(game_dir) {
        InstallState::Unmanaged
    } else {
        InstallState::Fresh
    }
}

fn mod_files_present(game_dir: &Path) -> bool {
    game_dir.join(paths::MOD_DLL_REL).is_file()
}

/// Installed mod version, only known for a managed install.
pub fn installed_version(state: &InstallState) -> Option<semver::Version> {
    match state {
        InstallState::Managed(m) => m.version(),
        _ => None,
    }
}

/// True if any MelonLoader appears installed in the Build folder.
pub fn melonloader_present(game_dir: &Path) -> bool {
    game_dir.join("version.dll").is_file() && game_dir.join("MelonLoader").is_dir()
}

/// Heuristic for a MelonLoader 0.6.x+ layout, which crashes WL2 DC. 0.6 splits
/// runtime assemblies into `MelonLoader\net6`/`net35` and a `Dependencies` tree,
/// whereas 0.5.7 keeps `MelonLoader\MelonLoader.dll` at the top. Best-effort:
/// used only to warn/offer a downgrade, never as a hard gate.
pub fn melonloader_looks_incompatible(game_dir: &Path) -> bool {
    if !melonloader_present(game_dir) {
        return false;
    }
    let ml = game_dir.join("MelonLoader");
    let has_057_marker = ml.join("MelonLoader.dll").is_file();
    let has_06_marker = ml.join("net6").is_dir()
        || ml.join("Dependencies").is_dir()
        || game_dir.join("dobby.dll").is_file() && ml.join("net35").is_dir();
    has_06_marker && !has_057_marker
}
