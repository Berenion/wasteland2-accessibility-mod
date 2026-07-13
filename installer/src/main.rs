//! Wasteland 2 Accessibility Mod installer/updater.
//!
//! Installs the mod (and MelonLoader 0.5.7) into the game's Build folder and
//! keeps it up to date from GitHub releases. Adapted from the soc-access
//! installer's architecture (see core::). A screen-reader-accessible GUI is a
//! planned second front-end; for now this ships the CLI.

mod cli;
mod core;

fn main() {
    let opts = match cli::parse_args() {
        Ok(o) => o,
        Err(msg) => {
            // --help lands here too; print to stdout and exit cleanly.
            println!("{msg}");
            std::process::exit(0);
        }
    };
    std::process::exit(cli::run(opts));
}
