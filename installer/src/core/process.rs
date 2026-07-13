//! Guard against patching files while the game is running.

use super::paths;
use sysinfo::System;

/// True if a Wasteland 2 process appears to be running right now.
pub fn game_running() -> bool {
    let sys = System::new_all();
    let targets: Vec<String> = paths::GAME_EXES.iter().map(|e| e.to_lowercase()).collect();
    sys.processes().values().any(|p| {
        let name = p.name().to_string_lossy().to_lowercase();
        targets.contains(&name)
    })
}
