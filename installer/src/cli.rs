//! Console front-end. All output is plain text so a screen reader on the terminal
//! reads it naturally; there are no spinners or cursor tricks. The GUI (added
//! later) is a second front-end over the same `core` engine.

use crate::core::{flow, install, paths, uninstall};
use std::io::Write;
use std::path::PathBuf;

pub struct Options {
    pub game_dir: Option<PathBuf>,
    pub check_only: bool,
    pub force_melonloader: bool,
    pub assume_yes: bool,
    /// Consider prerelease/beta releases (default true — the mod is in beta).
    pub include_prerelease: bool,
    /// Remove the mod instead of installing.
    pub uninstall: bool,
    /// With --uninstall, also remove MelonLoader.
    pub remove_melonloader: bool,
}

pub fn parse_args() -> Result<Options, String> {
    let mut opts = Options {
        game_dir: None,
        check_only: false,
        force_melonloader: false,
        assume_yes: false,
        include_prerelease: true,
        uninstall: false,
        remove_melonloader: false,
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
            "--uninstall" => opts.uninstall = true,
            "--remove-melonloader" => opts.remove_melonloader = true,
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
     --uninstall           Remove the mod (add --remove-melonloader to also\n\
     \x20                     remove MelonLoader)\n\
     --remove-melonloader  With --uninstall, also remove MelonLoader\n\
     --yes, -y             Don't prompt for confirmation\n\
     --help, -h            Show this help"
        .to_string()
}

/// Entry point; returns a process exit code.
pub fn run(opts: Options) -> i32 {
    println!("Wasteland 2 Accessibility Mod installer\n");

    let result = if opts.uninstall {
        uninstall_flow(&opts)
    } else {
        install_flow(&opts)
    };
    match result {
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

fn uninstall_flow(opts: &Options) -> Result<String, String> {
    let game_dir = resolve_game_dir(opts)?;
    println!("Game folder: {}", game_dir.display());

    let what = if opts.remove_melonloader {
        "the mod and MelonLoader"
    } else {
        "the mod"
    };
    if !opts.assume_yes && !confirm(&format!("Remove {what} from this folder?")) {
        return Ok("Cancelled.".to_string());
    }

    let report = uninstall::uninstall(&game_dir, opts.remove_melonloader)?;
    Ok(report.summary())
}

fn install_flow(opts: &Options) -> Result<String, String> {
    // 1. Locate the game.
    let game_dir = resolve_game_dir(opts)?;
    println!("Game folder: {}", game_dir.display());

    // 2. Inspect: query GitHub (prereleases included during beta — the plain
    // /releases/latest endpoint hides them) and classify the install.
    println!("Checking for the latest release...");
    let plan = flow::plan(&game_dir, opts.include_prerelease)?;
    println!(
        "Latest release: {}{} (tag {}, {})",
        plan.latest,
        if plan.prerelease { " prerelease" } else { "" },
        plan.tag,
        plan.asset.name
    );
    match &plan.installed {
        Some(v) => println!("Installed version: {v}"),
        None => println!("Installed version: none managed by this installer"),
    }
    println!("Action: {}", plan.summary());

    if plan.melonloader_incompatible {
        println!(
            "Note: the MelonLoader in this folder looks like a 0.6.x build, which crashes \
             this game. It will be replaced with the required 0.5.7."
        );
    }

    if opts.check_only {
        return Ok("Check complete (no changes made).".to_string());
    }

    let force_melon = opts.force_melonloader || plan.melonloader_incompatible;
    if plan.decision == install::UpdateDecision::UpToDate
        && plan.melonloader_present
        && !force_melon
    {
        return Ok("Already up to date. Nothing to do.".to_string());
    }

    // 3. Confirm before touching the folder.
    if !opts.assume_yes && !confirm("Proceed?") {
        return Ok("Cancelled.".to_string());
    }

    // 4. Do it, streaming progress to the console.
    flow::apply(&game_dir, &plan, opts.force_melonloader, |line| println!("{line}"))?;

    Ok(format!(
        "Done. Start your screen reader, then launch the game. If it doesn't speak, check \
         {}\\MelonLoader\\Latest.log for a \"Screen reader detected\" line.",
        game_dir.display()
    ))
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
