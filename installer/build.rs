//! Embeds a Windows application manifest into the installer executable.
//!
//! The installer writes into the game's Build folder, which for many players
//! lives under Program Files (Steam on C:, or GOG) — writing there needs
//! elevation. Requesting administrator up front means the UAC prompt appears
//! once, at launch, instead of the install failing halfway with access-denied
//! errors. `embed-manifest` embeds the manifest via linker directives on MSVC,
//! so no rc.exe / Windows SDK resource compiler is required.

use embed_manifest::manifest::ExecutionLevel;
use embed_manifest::{embed_manifest, new_manifest};

fn main() {
    // Only meaningful for Windows targets; a no-op elsewhere.
    if std::env::var_os("CARGO_CFG_WINDOWS").is_some() {
        embed_manifest(
            new_manifest("Berenion.Wasteland2AccessibilityMod.Installer")
                .requested_execution_level(ExecutionLevel::RequireAdministrator),
        )
        .expect("failed to embed application manifest");
    }
    println!("cargo:rerun-if-changed=build.rs");
}
