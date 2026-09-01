//! Microsoft Visual C++ 2015-2022 x64 runtime check.
//!
//! MelonLoader's native bootstrap (`version.dll`) and the mod's speech bridge
//! (Tolk.dll / nvdaControllerClient64.dll) are MSVC-built native DLLs that link
//! the redistributable CRT. Without it MelonLoader never loads, the game starts
//! silently unmodded, and nothing in the log explains why — so we check for it
//! and say so plainly instead of letting the user chase a silent failure.
//!
//! This is a warning, never a gate: the files can still be laid down, and the
//! user only needs the runtime before the next launch.

use std::path::PathBuf;

/// Microsoft's evergreen link for the current x64 package. The 2015-2022
/// runtimes share one binary-compatible package, so this single download covers
/// every version MelonLoader or the mod could ask for.
pub const DOWNLOAD_URL: &str = "https://aka.ms/vs/17/release/vc_redist.x64.exe";

/// The CRT DLLs the Windows loader must be able to resolve. `vcruntime140_1.dll`
/// only ships with the 2019 and newer package: a machine carrying just the 2015
/// redist has the other two and still fails, so check each by name rather than
/// treating the family as one unit.
const REQUIRED_DLLS: &[&str] = &["vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll"];

/// Registry home of the x64 runtime's install record. The package writes it to
/// both registry views, so the 64-bit view we get by default is enough.
const RUNTIME_KEY: &str = r"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";

pub struct VcRedistStatus {
    /// Required DLLs the loader wouldn't find. Empty means we're good.
    pub missing: Vec<&'static str>,
    /// Version recorded by the redist installer, e.g. "v14.44.35208.00". Absent
    /// on machines where the DLLs came from somewhere other than the package.
    pub version: Option<String>,
}

impl VcRedistStatus {
    pub fn ok(&self) -> bool {
        self.missing.is_empty()
    }

    /// One line for a status read-out, e.g. under `--check`.
    pub fn summary(&self) -> String {
        if self.ok() {
            match &self.version {
                Some(v) => format!("Visual C++ x64 runtime: installed ({v})"),
                None => "Visual C++ x64 runtime: present".to_string(),
            }
        } else {
            format!("Visual C++ x64 runtime: MISSING ({})", self.missing.join(", "))
        }
    }

    /// What the user has to do about it, or None when there's nothing to say.
    pub fn advice(&self) -> Option<String> {
        if self.ok() {
            return None;
        }
        Some(format!(
            "The Microsoft Visual C++ 2015-2022 x64 runtime is missing ({}). MelonLoader \
             cannot load without it, so the game would start with no mod and no speech. \
             Install it from {DOWNLOAD_URL} and then start the game.",
            self.missing.join(", ")
        ))
    }
}

/// Inspect the machine. File presence is the real test — it's what the loader
/// does — and the registry only supplies a version for the read-out.
pub fn check() -> VcRedistStatus {
    let system32 = system32_dir();
    let missing = REQUIRED_DLLS
        .iter()
        .filter(|dll| !system32.join(dll).is_file())
        .copied()
        .collect();
    VcRedistStatus {
        missing,
        version: registry_version(),
    }
}

/// The 64-bit system directory. `%SystemRoot%\System32` is the 64-bit one for a
/// 64-bit process, which this installer always is; falling back to the literal
/// path only matters if SystemRoot is unset.
fn system32_dir() -> PathBuf {
    let root = std::env::var("SystemRoot").unwrap_or_else(|_| r"C:\Windows".to_string());
    PathBuf::from(root).join("System32")
}

#[cfg(windows)]
fn registry_version() -> Option<String> {
    use winreg::RegKey;
    use winreg::enums::*;

    let key = RegKey::predef(HKEY_LOCAL_MACHINE)
        .open_subkey(RUNTIME_KEY)
        .ok()?;
    // `Installed` is 0 on a package that was rolled back; treat that as no record
    // rather than reporting a version the machine doesn't really have.
    if key.get_value::<u32, _>("Installed").unwrap_or(0) == 0 {
        return None;
    }
    key.get_value::<String, _>("Version").ok()
}

#[cfg(not(windows))]
fn registry_version() -> Option<String> {
    None
}
