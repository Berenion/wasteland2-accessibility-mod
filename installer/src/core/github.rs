//! GitHub Releases client: fetch a release, pick the asset we want, and download
//! it. This is the update channel — the same pattern the soc-access installer uses.

use serde::Deserialize;
use std::fs;
use std::io::Write;
use std::path::Path;
use std::time::Duration;

const USER_AGENT: &str = "wl2-access-installer";
const TIMEOUT_SECS: u64 = 120;

#[derive(Debug, Deserialize)]
pub struct ReleaseInfo {
    pub tag_name: String,
    #[serde(default)]
    pub draft: bool,
    #[serde(default)]
    pub prerelease: bool,
    pub assets: Vec<Asset>,
}

#[derive(Debug, Deserialize, Clone)]
pub struct Asset {
    pub name: String,
    pub browser_download_url: String,
    /// GitHub-computed digest, e.g. "sha256:abcd…". Present on newer uploads;
    /// used to verify the download without shipping a separate checksum file.
    #[serde(default)]
    pub digest: Option<String>,
}

impl Asset {
    /// The hex sha256 from the `digest` field, if it is a sha256 digest.
    pub fn sha256_hex(&self) -> Option<&str> {
        self.digest.as_deref()?.strip_prefix("sha256:")
    }
}

fn client() -> Result<reqwest::blocking::Client, String> {
    reqwest::blocking::Client::builder()
        .user_agent(USER_AGENT)
        .timeout(Duration::from_secs(TIMEOUT_SECS))
        .build()
        .map_err(|e| format!("failed to build HTTP client: {e}"))
}

fn get<T: serde::de::DeserializeOwned>(url: &str) -> Result<T, String> {
    let resp = client()?
        .get(url)
        .header("Accept", "application/vnd.github+json")
        .header("X-GitHub-Api-Version", "2022-11-28")
        .send()
        .map_err(|e| format!("request to {url} failed: {e}"))?;
    if !resp.status().is_success() {
        return Err(format!("GitHub returned {} for {url}", resp.status()));
    }
    resp.json::<T>()
        .map_err(|e| format!("failed to parse GitHub JSON: {e}"))
}

/// A specific tagged release (used to pin MelonLoader v0.5.7).
pub fn fetch_release_by_tag(repo: &str, tag: &str) -> Result<ReleaseInfo, String> {
    get(&format!("https://api.github.com/repos/{repo}/releases/tags/{tag}"))
}

/// All releases for `owner/repo`, newest first (drafts included; the caller
/// filters). We use the list endpoint rather than `/releases/latest` because the
/// latter hides prereleases — and the mod ships prerelease/beta builds.
pub fn fetch_releases(repo: &str) -> Result<Vec<ReleaseInfo>, String> {
    get(&format!(
        "https://api.github.com/repos/{repo}/releases?per_page=30"
    ))
}

/// The newest mod release by the semver of its zip asset. Skips drafts always;
/// skips prereleases unless `include_prerelease` is set (true during beta).
pub fn find_latest_mod_release(
    repo: &str,
    include_prerelease: bool,
) -> Result<(ReleaseInfo, Asset, semver::Version), String> {
    let releases = fetch_releases(repo)?;
    let mut best: Option<(ReleaseInfo, Asset, semver::Version)> = None;
    for rel in releases {
        if rel.draft {
            continue;
        }
        if rel.prerelease && !include_prerelease {
            continue;
        }
        if let Some((asset, version)) = find_mod_asset(&rel) {
            let better = best.as_ref().map(|(_, _, bv)| version > *bv).unwrap_or(true);
            if better {
                best = Some((rel, asset, version));
            }
        }
    }
    best.ok_or_else(|| {
        format!(
            "no installable mod .zip asset found in any {} release{}",
            repo,
            if include_prerelease {
                ""
            } else {
                " (prereleases were skipped — the beta may be prerelease-only)"
            }
        )
    })
}

/// Locate our mod's zip in a release and parse its semantic version from the
/// filename (e.g. `Wasteland2AccessibilityMod-0.7.0.zip` -> 0.7.0). Prefers an
/// asset with the expected prefix; falls back to any `.zip` carrying a version.
pub fn find_mod_asset(release: &ReleaseInfo) -> Option<(Asset, semver::Version)> {
    let mut fallback: Option<(Asset, semver::Version)> = None;
    for asset in &release.assets {
        if !asset.name.to_lowercase().ends_with(".zip") {
            continue;
        }
        let version = match parse_semver(&asset.name) {
            Some(v) => v,
            None => continue,
        };
        if asset.name.starts_with(paths_prefix()) {
            return Some((asset.clone(), version));
        }
        fallback.get_or_insert((asset.clone(), version));
    }
    fallback
}

fn paths_prefix() -> &'static str {
    super::paths::MOD_ASSET_PREFIX
}

/// Find a release asset by exact (case-insensitive) file name.
pub fn find_asset_named<'a>(release: &'a ReleaseInfo, name: &str) -> Option<&'a Asset> {
    release
        .assets
        .iter()
        .find(|a| a.name.eq_ignore_ascii_case(name))
}

/// Extract the first `X.Y.Z` semver found in a string.
pub fn parse_semver(text: &str) -> Option<semver::Version> {
    let re = regex::Regex::new(r"(\d+\.\d+\.\d+)").ok()?;
    let caps = re.captures(text)?;
    semver::Version::parse(&caps[1]).ok()
}

/// Download an asset to `dest`, creating parent directories as needed.
pub fn download_asset(asset: &Asset, dest: &Path) -> Result<(), String> {
    let resp = client()?
        .get(&asset.browser_download_url)
        .send()
        .map_err(|e| format!("download of {} failed: {e}", asset.name))?;
    if !resp.status().is_success() {
        return Err(format!(
            "download of {} returned {}",
            asset.name,
            resp.status()
        ));
    }
    let bytes = resp
        .bytes()
        .map_err(|e| format!("reading {} failed: {e}", asset.name))?;
    if let Some(parent) = dest.parent() {
        fs::create_dir_all(parent)
            .map_err(|e| format!("creating {}: {e}", parent.display()))?;
    }
    let mut file =
        fs::File::create(dest).map_err(|e| format!("creating {}: {e}", dest.display()))?;
    file.write_all(&bytes)
        .map_err(|e| format!("writing {}: {e}", dest.display()))?;
    Ok(())
}
