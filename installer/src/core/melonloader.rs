//! MelonLoader 0.5.7 bootstrap — the piece soc-access doesn't need (it targets
//! BepInEx). Downloading MelonLoader.x64.zip from the pinned v0.5.7 release and
//! extracting it into the Build folder is exactly what the official installer's
//! manual path does, but with the version locked so users never land on a 0.6.x
//! build (which crashes WL2 Director's Cut).

use super::{detect, github, install, paths};
use std::path::Path;

/// Ensure MelonLoader 0.5.7 is present in the Build folder.
///
/// Returns `Ok(true)` if we installed it this run, `Ok(false)` if a compatible
/// install was already there. With `force`, installs over whatever is present
/// (used to replace an incompatible 0.6.x layout).
pub fn ensure_melonloader(game_dir: &Path, force: bool) -> Result<bool, String> {
    if detect::melonloader_present(game_dir) && !force {
        return Ok(false);
    }

    let release = github::fetch_release_by_tag(paths::MELONLOADER_REPO, paths::MELONLOADER_TAG)?;
    let asset = github::find_asset_named(&release, paths::MELONLOADER_ASSET).ok_or_else(|| {
        format!(
            "{} not found on MelonLoader {}",
            paths::MELONLOADER_ASSET,
            paths::MELONLOADER_TAG
        )
    })?;

    let tmp = std::env::temp_dir().join(paths::MELONLOADER_ASSET);
    github::download_asset(asset, &tmp)?;
    if let Some(expected) = asset.sha256_hex() {
        install::verify_sha256(&tmp, expected)?;
    }

    // Loader files aren't tracked in our mod manifest — MelonLoader owns its own
    // footprint and our uninstall leaves it in place.
    install::extract_zip_into(&tmp, game_dir)?;
    let _ = std::fs::remove_file(&tmp);
    Ok(true)
}
