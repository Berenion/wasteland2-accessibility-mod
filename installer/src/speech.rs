//! Spoken status output via Tolk (NVDA / JAWS / SAPI).
//!
//! The GUI's status log is a read-only text box, and screen readers do not
//! announce text appended to such a control. So, in addition to writing to the
//! log, we speak each line. Tolk.dll and nvdaControllerClient64.dll (the same
//! bridge the mod uses) are embedded and extracted to a temp folder at runtime,
//! keeping the installer a single distributable exe. Every function degrades to
//! a no-op if Tolk can't be loaded, so speech never blocks the installer.

use libloading::{Library, Symbol};
use std::sync::OnceLock;

// Bundled at build time from the repo's redist/ (relative to this file).
const TOLK_DLL: &[u8] = include_bytes!("../../redist/Tolk.dll");
const NVDA_DLL: &[u8] = include_bytes!("../../redist/nvdaControllerClient64.dll");

/// Tolk's `bool Tolk_Speak(const wchar_t*, bool)` (cdecl; one ABI on x64).
type SpeakFn = unsafe extern "C" fn(*const u16, bool) -> bool;

struct Tolk {
    speak: SpeakFn,
}
// The fn pointer targets a leaked, never-unloaded Library, so it's safe to send.
unsafe impl Send for Tolk {}
unsafe impl Sync for Tolk {}

static TOLK: OnceLock<Option<Tolk>> = OnceLock::new();

/// Load Tolk once. Safe to call repeatedly; only the first call does work.
pub fn init() {
    TOLK.get_or_init(|| load().ok());
}

/// Speak `text`. `interrupt` clears queued speech first. No-op if Tolk is absent.
pub fn speak(text: &str, interrupt: bool) {
    if let Some(Some(tolk)) = TOLK.get() {
        let mut wide: Vec<u16> = text.encode_utf16().collect();
        wide.push(0);
        unsafe {
            (tolk.speak)(wide.as_ptr(), interrupt);
        }
    }
}

fn load() -> Result<Tolk, String> {
    let dir = std::env::temp_dir().join("wl2-access-installer");
    std::fs::create_dir_all(&dir).map_err(|e| e.to_string())?;
    let tolk_path = dir.join("Tolk.dll");
    std::fs::write(&tolk_path, TOLK_DLL).map_err(|e| e.to_string())?;
    std::fs::write(dir.join("nvdaControllerClient64.dll"), NVDA_DLL).map_err(|e| e.to_string())?;

    // Tolk loads nvdaControllerClient64.dll by name; point the loader at our temp
    // folder so it finds the copy we just extracted next to Tolk.dll.
    #[cfg(windows)]
    unsafe {
        use std::os::windows::ffi::OsStrExt;
        let wide: Vec<u16> = dir.as_os_str().encode_wide().chain(Some(0)).collect();
        windows_sys::Win32::System::LibraryLoader::SetDllDirectoryW(wide.as_ptr());
    }

    // Leak the Library so the fn pointers stay valid for the process lifetime.
    let lib: &'static Library =
        Box::leak(Box::new(unsafe { Library::new(&tolk_path).map_err(|e| e.to_string())? }));
    unsafe {
        let tolk_load: Symbol<unsafe extern "C" fn()> =
            lib.get(b"Tolk_Load\0").map_err(|e| e.to_string())?;
        let tolk_speak: Symbol<SpeakFn> =
            lib.get(b"Tolk_Speak\0").map_err(|e| e.to_string())?;
        tolk_load();
        Ok(Tolk { speak: *tolk_speak })
    }
}
