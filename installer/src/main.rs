//! Wasteland 2 Accessibility Mod installer/updater.
//!
//! Double-click launches the accessible GUI; passing any CLI flag runs the
//! console front-end instead. Both drive the same core:: engine.
//!
//! Release builds use the Windows GUI subsystem so no console window appears for
//! the GUI. In CLI mode we re-attach to the launching terminal so output shows.
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod cli;
mod core;
mod gui;
mod speech;

/// Flags that select the CLI. Any of them (or --cli) routes to the console.
const CLI_FLAGS: &[&str] = &[
    "--cli",
    "--check",
    "--game-dir",
    "--yes",
    "-y",
    "--stable-only",
    "--force-melonloader",
    "--help",
    "-h",
];

fn main() {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let use_cli = args.iter().any(|a| CLI_FLAGS.contains(&a.as_str()));

    if !use_cli {
        gui::run();
        return;
    }

    attach_parent_console();
    let opts = match cli::parse_args() {
        Ok(o) => o,
        Err(msg) => {
            println!("{msg}");
            std::process::exit(0);
        }
    };
    std::process::exit(cli::run(opts));
}

/// In a GUI-subsystem build there's no console, so CLI output would vanish.
/// Attaching to the parent process's console lets println!/eprintln! reach the
/// terminal that launched us. No-op if there's no parent console.
#[cfg(windows)]
fn attach_parent_console() {
    use windows_sys::Win32::System::Console::{ATTACH_PARENT_PROCESS, AttachConsole};
    unsafe {
        AttachConsole(ATTACH_PARENT_PROCESS);
    }
}

#[cfg(not(windows))]
fn attach_parent_console() {}
