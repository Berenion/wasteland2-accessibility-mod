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
    "--uninstall",
    "--remove-melonloader",
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
///
/// AttachConsole repoints the standard handles at the console it attaches to,
/// which would throw away any redirection the launcher set up — `installer.exe
/// --check > log.txt` would write an empty file and print to the screen instead.
/// So we save the handles first and restore the ones that were already valid,
/// keeping the redirect while a plain terminal launch still gets the console.
#[cfg(windows)]
fn attach_parent_console() {
    use windows_sys::Win32::Foundation::{HANDLE, INVALID_HANDLE_VALUE};
    use windows_sys::Win32::System::Console::{
        ATTACH_PARENT_PROCESS, AttachConsole, GetStdHandle, STD_ERROR_HANDLE, STD_INPUT_HANDLE,
        STD_OUTPUT_HANDLE, SetStdHandle,
    };

    /// A handle we were actually given. Null means the launcher set none (a
    /// double-click, or elevation, which doesn't pass handles across).
    fn inherited(h: HANDLE) -> bool {
        !h.is_null() && h != INVALID_HANDLE_VALUE
    }

    unsafe {
        let ids = [STD_INPUT_HANDLE, STD_OUTPUT_HANDLE, STD_ERROR_HANDLE];
        let saved = ids.map(|id| (id, GetStdHandle(id)));
        if AttachConsole(ATTACH_PARENT_PROCESS) == 0 {
            return;
        }
        // Console handles are only usable once attached, so this also makes the
        // inherited ones live — restore them all rather than only file/pipe ones.
        for (id, handle) in saved {
            if inherited(handle) {
                SetStdHandle(id, handle);
            }
        }
    }
}

#[cfg(not(windows))]
fn attach_parent_console() {}
