//! Console front-end. All output is plain text so a screen reader on the terminal
//! reads it naturally; there are no spinners or cursor tricks. The GUI (added
//! later) is a second front-end over the same `core` engine.

use crate::core::{detect, github, install, melonloader, paths, process};
use std::io::Write;
use std::path::PathBuf;

pub struct Options {
    pub game_dir: Option<PathBuf>,
    pub check_only: bool,
    pub force_melonloader: bool,
    pub assume_yes: bool,
    /// Consider prerelease/beta releases (default true — the mod is in beta).
    pub include_prerelease: bool,
}

pub fn parse_args() -> Result<Options, String> {
    let mut opts = Options {
        game_dir: None,
        check_only: false,
        force_melonloader: false,
        assume_yes: false,
        include_prerelease: true,
    };
    let mut args = std::env::args().skip(1);
    while let Some(arg) = args.next() {
        match arg.as_str() {
            "--game-dir" => {
                let p = args
                    .next()
                    .ok_or_else(|| "--game-dir requires a path".to_string())?;
                opts.game_dir = Some(PathBuf::from(p));
            }
            "--check" => opts.check_only = true,
            "--force-melonloader" => opts.force_melonloader = true,
            "--yes" | "-y" => opts.assume_yes = true,
            "--stable-only" => opts.include_prerelease = false,
            "--cli" => {} // accepted for symmetry with the GUI launcher
            "--help" | "-h" => return Err(help_text()),
            other => return Err(format!("unknown argument: {other}\n\n{}", help_text())),
        }
    }
    Ok(opts)
}

fn help_text() -> String {
    "Wasteland 2 Accessibility Mod installer\n\n\
     Usage: wl2-access-installer [options]\n\n\
     --game-dir <path>     Path to the WL2 Build folder (skips auto-detection)\n\
     --check               Report versions and exit without installing\n\
     --force-melonloader   Reinstall MelonLoader 0.5.7 even if present\n\
     --stable-only         Ignore prerelease/beta builds (default installs betas)\n\
     --yes, -y             Don't prompt for confirmation\n\
     --help, -h            Show this help"
        .to_string()
}

/// Entry point; returns a process exit code.
pub fn run(opts: Options) -> i32 {
    println!("Wasteland 2 Accessibility Mod installer\n");

    match install_flow(&opts) {
        Ok(msg) => {
            println!("\n{msg}");
            0
        }
        Err(msg) => {
            eprintln!("\nError: {msg}");
            1
        }
    }
}

fn install_flow(opts: &Options) -> Result<String, String> {
    // 1. Locate the game.
    let game_dir = resolve_game_dir(opts)?;
    println!("Game folder: {}", game_dir.display());

    // 2. Ask GitHub what the latest mod release is. During beta we include
    // prereleases (the plain /releases/latest endpoint hides them).
    println!("Checking for the latest release...");
    let (release, asset, latest) =
        github::find_latest_mod_release(paths::MOD_REPO, opts.include_prerelease)?;
    println!(
        "Latest release: {latest}{} (tag {}, {})",
        if release.prerelease { " prerelease" } else { "" },
        release.tag_name,
        asset.name
    );

    // 3. Classify the current install and decide.
    let state = detect::classify_install(&game_dir);
    if let Some(installed) = detect::installed_version(&state) {
        println!("Installed version: {installed}");
    } else {
        println!("Installed version: none managed by this installer");
    }
    let decision = install::decide(&state, &latest);
    println!("Action: {}", describe(&decision));

    if detect::melonloader_looks_incompatible(&game_dir) {
        println!(
            "Note: the MelonLoader in this folder looks like a 0.6.x build, which crashes \
             this game. It will be replaced with the required 0.5.7."
        );
    }

    if opts.check_only {
        return Ok("Check complete (no changes made).".to_string());
    }

    let force_melon =
        opts.force_melonloader || detect::melonloader_looks_incompatible(&game_dir);

    if decision == install::UpdateDecision::UpToDate
        && detect::melonloader_present(&game_dir)
        && !force_melon
    {
        return Ok("Already up to date. Nothing to do.".to_string());
    }

    // 4. Never patch a running game.
    if process::game_running() {
        return Err("Wasteland 2 is running. Close the game and run this again.".to_string());
    }

    // 5. Confirm before touching the folder.
    if !opts.assume_yes && !confirm("Proceed?") {
        return Ok("Cancelled.".to_string());
    }

    // 6. MelonLoader bootstrap.
    println!("Ensuring MelonLoader 0.5.7...");
    let ml_installed_now = melonloader::ensure_melonloader(&game_dir, force_melon)?;
    println!(
        "MelonLoader: {}",
        if ml_installed_now {
            "installed 0.5.7"
        } else {
            "already present"
        }
    );

    // 7. Download and install the mod.
    println!("Downloading {}...", asset.name);
    let tmp = std::env::temp_dir().join(&asset.name);
    github::download_asset(&asset, &tmp)?;
    println!("Installing...");
    let melon_flag = ml_installed_now
        || matches!(&state, detect::InstallState::Managed(m) if m.melonloader_installed);
    let manifest =
        install::install_mod(&game_dir, &asset, &latest, &tmp, &state, melon_flag)?;
    let _ = std::fs::remove_file(&tmp);

    Ok(format!(
        "Done. Installed mod {} ({} files).\n\
         Start your screen reader, then launch the game. If it doesn't speak, check \
         {}\\MelonLoader\\Latest.log for a \"Screen reader detected\" line.",
        manifest.mod_version,
        manifest.installed_files.len(),
        game_dir.display()
    ))
}

fn describe(d: &install::UpdateDecision) -> &'static str {
    match d {
        install::UpdateDecision::FreshInstall => "fresh install",
        install::UpdateDecision::Update => "update",
        install::UpdateDecision::Reinstall => "reinstall / take over existing files",
        install::UpdateDecision::UpToDate => "already up to date",
    }
}

/// Resolve the Build folder: explicit flag, then auto-detect, then prompt.
fn resolve_game_dir(opts: &Options) -> Result<PathBuf, String> {
    if let Some(dir) = &opts.game_dir {
        if paths::is_game_build_dir(dir) {
            return Ok(dir.clone());
        }
        return Err(format!(
            "{} doesn't look like the WL2 Build folder (no game exe found)",
            dir.display()
        ));
    }

    let found = paths::autodetect_game_dirs();
    match found.len() {
        0 => {
            println!("Couldn't auto-detect the game.");
            prompt_for_game_dir()
        }
        1 => Ok(found.into_iter().next().unwrap()),
        _ => {
            println!("Found multiple installs:");
            for (i, dir) in found.iter().enumerate() {
                println!("  {}. {}", i + 1, dir.display());
            }
            let choice = prompt("Choose a number (or press Enter for 1): ");
            let idx = choice.trim().parse::<usize>().unwrap_or(1);
            found
                .get(idx.saturating_sub(1))
                .cloned()
                .ok_or_else(|| "invalid choice".to_string())
        }
    }
}

fn prompt_for_game_dir() -> Result<PathBuf, String> {
    let raw = prompt(
        "Enter the path to the WL2 Build folder\n\
         (e.g. ...\\steamapps\\common\\Wasteland 2 Director's Cut\\Build): ",
    );
    let dir = PathBuf::from(raw.trim().trim_matches('"'));
    if paths::is_game_build_dir(&dir) {
        Ok(dir)
    } else {
        Err(format!(
            "{} doesn't contain the game exe.",
            dir.display()
        ))
    }
}

fn confirm(msg: &str) -> bool {
    let ans = prompt(&format!("{msg} [y/N] "));
    matches!(ans.trim().to_lowercase().as_str(), "y" | "yes")
}

fn prompt(msg: &str) -> String {
    print!("{msg}");
    let _ = std::io::stdout().flush();
    let mut line = String::new();
    let _ = std::io::stdin().read_line(&mut line);
    line.trim_end_matches(['\r', '\n']).to_string()
}
