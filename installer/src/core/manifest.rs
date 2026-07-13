//! The install manifest: a small JSON file written into the Build folder that
//! records what we installed, so later runs can tell "we manage this install",
//! compare versions for updates, and clean up stale files on update/uninstall.

use super::paths;
use serde::{Deserialize, Serialize};
use std::fs;
use std::path::Path;
use std::time::{SystemTime, UNIX_EPOCH};

pub const SCHEMA_VERSION: u32 = 1;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct InstallManifest {
    pub schema_version: u32,
    /// Installed mod version, e.g. "0.7.0".
    pub mod_version: String,
    /// The release asset this came from.
    pub asset_name: String,
    /// Hex sha256 of the downloaded asset, when known.
    pub sha256: Option<String>,
    /// Unix epoch seconds at install time.
    pub installed_at_unix: u64,
    /// Mod files we wrote, relative to the Build folder, forward-slashed.
    pub installed_files: Vec<String>,
    /// Whether this installer also bootstrapped MelonLoader.
    #[serde(default)]
    pub melonloader_installed: bool,
}

impl InstallManifest {
    pub fn new(
        version: &str,
        asset_name: &str,
        sha256: Option<String>,
        installed_files: Vec<String>,
        melonloader_installed: bool,
    ) -> Self {
        InstallManifest {
            schema_version: SCHEMA_VERSION,
            mod_version: version.to_string(),
            asset_name: asset_name.to_string(),
            sha256,
            installed_at_unix: now_unix(),
            installed_files,
            melonloader_installed,
        }
    }

    /// Read the manifest from a Build folder, or None if absent/unparseable.
    pub fn read(game_dir: &Path) -> Option<InstallManifest> {
        let data = fs::read_to_string(paths::manifest_path(game_dir)).ok()?;
        serde_json::from_str(&data).ok()
    }

    pub fn write(&self, game_dir: &Path) -> Result<(), String> {
        let data = serde_json::to_string_pretty(self)
            .map_err(|e| format!("serializing manifest: {e}"))?;
        fs::write(paths::manifest_path(game_dir), data)
            .map_err(|e| format!("writing manifest: {e}"))
    }

    pub fn version(&self) -> Option<semver::Version> {
        semver::Version::parse(&self.mod_version).ok()
    }
}

fn now_unix() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_secs())
        .unwrap_or(0)
}
