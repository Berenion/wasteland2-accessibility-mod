//! Core installer engine, split by concern and shared by the CLI (and later GUI)
//! front-ends. Adapted from the soc-access installer's `core` module, retargeted
//! from BepInEx/Songs of Conquest to MelonLoader/Wasteland 2 Director's Cut.

pub mod paths;
pub mod github;
pub mod manifest;
pub mod detect;
pub mod install;
pub mod melonloader;
pub mod process;
pub mod flow;
