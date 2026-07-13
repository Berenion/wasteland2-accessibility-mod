//! Download verification, safe archive extraction, and mod install/update.

use super::detect::InstallState;
use super::github::Asset;
use super::manifest::InstallManifest;
use sha2::{Digest, Sha256};
use std::collections::HashSet;
use std::fs;
use std::io::Read;
use std::path::Path;

/// Hex sha256 of a file, hashed in chunks so large downloads don't balloon memory.
pub fn sha256_file(path: &Path) -> Result<String, String> {
    let mut file = fs::File::open(path).map_err(|e| format!("open {}: {e}", path.display()))?;
    let mut hasher = Sha256::new();
    let mut buf = [0u8; 81920];
    loop {
        let n = file
            .read(&mut buf)
            .map_err(|e| format!("read {}: {e}", path.display()))?;
        if n == 0 {
            break;
        }
        hasher.update(&buf[..n]);
    }
    Ok(hasher.finalize().iter().map(|b| format!("{b:02x}")).collect())
}

pub fn verify_sha256(path: &Path, expected_hex: &str) -> Result<(), String> {
    let got = sha256_file(path)?;
    if got.eq_ignore_ascii_case(expected_hex) {
        Ok(())
    } else {
        Err(format!(
            "checksum mismatch (download may be corrupt): expected {expected_hex}, got {got}"
        ))
    }
}

/// Extract a zip into `dest_dir`, overwriting existing files. Returns the list of
/// extracted file paths (relative to `dest_dir`, forward-slashed). Rejects unsafe
/// (zip-slip) entries via the zip crate's `enclosed_name`.
pub fn extract_zip_into(zip_path: &Path, dest_dir: &Path) -> Result<Vec<String>, String> {
    let file =
        fs::File::open(zip_path).map_err(|e| format!("open {}: {e}", zip_path.display()))?;
    let mut archive =
        zip::ZipArchive::new(file).map_err(|e| format!("read zip {}: {e}", zip_path.display()))?;

    let mut written = Vec::new();
    for i in 0..archive.len() {
        let mut entry = archive
            .by_index(i)
            .map_err(|e| format!("zip entry {i}: {e}"))?;
        let rel = match entry.enclosed_name() {
            Some(p) => p,
            None => return Err(format!("unsafe path in archive: {}", entry.name())),
        };
        let out = dest_dir.join(&rel);
        if entry.is_dir() {
            fs::create_dir_all(&out).map_err(|e| format!("mkdir {}: {e}", out.display()))?;
            continue;
        }
        if let Some(parent) = out.parent() {
            fs::create_dir_all(parent).map_err(|e| format!("mkdir {}: {e}", parent.display()))?;
        }
        let mut outfile =
            fs::File::create(&out).map_err(|e| format!("create {}: {e}", out.display()))?;
        std::io::copy(&mut entry, &mut outfile)
            .map_err(|e| format!("write {}: {e}", out.display()))?;
        written.push(rel.to_string_lossy().replace('\\', "/"));
    }
    Ok(written)
}

/// Install (or update) the mod from a downloaded zip into the Build folder.
///
/// Verifies the checksum when the release advertises one, extracts the archive
/// (mod DLL into `Mods\`, Tolk/NVDA sidecars into the Build root — the layout the
/// release archive already mirrors), removes files a prior managed install left
/// behind that this version no longer ships, and writes a fresh manifest.
pub fn install_mod(
    game_dir: &Path,
    asset: &Asset,
    version: &semver::Version,
    zip_path: &Path,
    prior_state: &InstallState,
    melonloader_installed: bool,
) -> Result<InstallManifest, String> {
    let sha = asset.sha256_hex();
    if let Some(expected) = sha {
        verify_sha256(zip_path, expected)?;
    }

    let new_files = extract_zip_into(zip_path, game_dir)?;

    // On update, delete files the previous managed install wrote that aren't part
    // of this version anymore, so renamed/removed files don't linger.
    if let InstallState::Managed(prior) = prior_state {
        let keep: HashSet<&str> = new_files.iter().map(|s| s.as_str()).collect();
        for old in &prior.installed_files {
            if !keep.contains(old.as_str()) {
                let p = game_dir.join(old);
                let _ = fs::remove_file(&p); // best-effort cleanup
            }
        }
    }

    let manifest = InstallManifest::new(
        &version.to_string(),
        &asset.name,
        sha.map(|s| s.to_string()),
        new_files,
        melonloader_installed,
    );
    manifest.write(game_dir)?;
    Ok(manifest)
}

/// What the CLI/GUI should do given the installed state and the latest version.
#[derive(Debug, PartialEq, Eq)]
pub enum UpdateDecision {
    FreshInstall,
    Update,
    Reinstall,
    UpToDate,
}

pub fn decide(state: &InstallState, latest: &semver::Version) -> UpdateDecision {
    match super::detect::installed_version(state) {
        Some(installed) if installed < *latest => UpdateDecision::Update,
        Some(installed) if installed == *latest => UpdateDecision::UpToDate,
        Some(_) => UpdateDecision::Reinstall, // installed newer than latest (dev build)
        None => match state {
            InstallState::Unmanaged => UpdateDecision::Reinstall,
            _ => UpdateDecision::FreshInstall,
        },
    }
}
