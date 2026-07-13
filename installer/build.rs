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
    // Always embed a manifest on Windows so wxWidgets finds the Common Controls
    // v6 dependency (embed-manifest includes it by default). Only the requested
    // execution level differs: release requests administrator (installs may write
    // under Program Files); debug stays asInvoker so developers can run it without
    // a UAC prompt on every launch.
    if std::env::var_os("CARGO_CFG_WINDOWS").is_some() {
        let release = std::env::var("PROFILE").as_deref() == Ok("release");
        let level = if release {
            ExecutionLevel::RequireAdministrator
        } else {
            ExecutionLevel::AsInvoker
        };
        embed_manifest(
            new_manifest("Berenion.Wasteland2AccessibilityMod.Installer")
                .requested_execution_level(level),
        )
        .expect("failed to embed application manifest");
    }
    println!("cargo:rerun-if-changed=build.rs");
}
