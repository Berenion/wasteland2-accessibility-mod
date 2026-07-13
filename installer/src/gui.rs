//! Accessible GUI front-end (wxWidgets via wxdragon). Native controls give
//! screen readers (NVDA/JAWS) proper MSAA/UIA semantics out of the box; the
//! multiline log is the read-out surface a screen-reader user follows.
//!
//! Long-running work (network + file IO) runs on a background thread and streams
//! progress back over a channel that the frame drains in its idle handler, so
//! the window never freezes mid-install.

use crate::core::{flow, paths};
use crate::speech;
use std::cell::{Cell, RefCell};
use std::path::PathBuf;
use std::rc::Rc;
use std::sync::mpsc::{self, Receiver};
use std::thread;
use wxdragon::prelude::*;

/// Messages from the worker thread to the UI.
enum Msg {
    Line(String),
    Done(Result<String, String>),
}

/// UI-thread state shared across event closures.
struct Shared {
    rx: RefCell<Option<Receiver<Msg>>>,
    busy: Cell<bool>,
}

pub fn run() {
    speech::init();
    let _ = wxdragon::main(|_| {
        let frame = Frame::builder()
            .with_title("Wasteland 2 Accessibility Mod installer")
            .with_size(Size::new(560, 460))
            .build();

        let panel = Panel::builder(&frame).build();
        let root = BoxSizer::builder(Orientation::Vertical).build();

        // Intro / instructions.
        let intro = StaticText::builder(&panel)
            .with_label(
                "Installs the Wasteland 2 Accessibility Mod and MelonLoader, and keeps \
                 them up to date. Pick your game's Build folder, then Check or Install.",
            )
            .build();
        root.add(&intro, 0, SizerFlag::Expand | SizerFlag::All, 8);

        // Game-folder row: label + path field + Browse.
        let dir_label = StaticText::builder(&panel)
            .with_label("Game Build folder:")
            .build();
        root.add(&dir_label, 0, SizerFlag::Left | SizerFlag::All, 8);

        let dir_row = BoxSizer::builder(Orientation::Horizontal).build();
        let dir_field = TextCtrl::builder(&panel)
            .with_value(&default_game_dir())
            .build();
        let browse_btn = Button::builder(&panel).with_label("Browse...").build();
        dir_row.add(&dir_field, 1, SizerFlag::Expand | SizerFlag::All, 4);
        dir_row.add(&browse_btn, 0, SizerFlag::All, 4);
        root.add_sizer(&dir_row, 0, SizerFlag::Expand | SizerFlag::All, 4);

        // Action buttons.
        let btn_row = BoxSizer::builder(Orientation::Horizontal).build();
        let check_btn = Button::builder(&panel).with_label("Check for updates").build();
        let install_btn = Button::builder(&panel)
            .with_label("Install / Update")
            .build();
        let close_btn = Button::builder(&panel).with_label("Close").build();
        btn_row.add(&check_btn, 0, SizerFlag::All, 4);
        btn_row.add(&install_btn, 0, SizerFlag::All, 4);
        btn_row.add_stretch_spacer(1);
        btn_row.add(&close_btn, 0, SizerFlag::All, 4);
        root.add_sizer(&btn_row, 0, SizerFlag::Expand | SizerFlag::All, 4);

        // Log / status read-out.
        let log = TextCtrl::builder(&panel)
            .with_style(TextCtrlStyle::MultiLine | TextCtrlStyle::ReadOnly)
            .build();
        root.add(&log, 1, SizerFlag::Expand | SizerFlag::All, 8);

        panel.set_sizer(root, true);
        frame.centre();
        frame.show(true);
        speech::speak(
            "Wasteland 2 Accessibility Mod installer ready. Tab to move between controls.",
            true,
        );

        let shared = Rc::new(Shared {
            rx: RefCell::new(None),
            busy: Cell::new(false),
        });

        // --- Browse: native, accessible folder picker. ---
        {
            browse_btn.on_click(move |_| {
                let dlg = DirDialog::builder(
                    &frame,
                    "Select the Wasteland 2 Build folder",
                    &dir_field.get_value(),
                )
                .build();
                if dlg.show_modal() == ID_OK
                    && let Some(path) = dlg.get_path()
                    && !path.is_empty()
                {
                    dir_field.set_value(&path);
                }
            });
        }

        // --- Check: inspect only, on a worker thread. ---
        {
            let shared = shared.clone();
            check_btn.on_click(move |_| {
                let dir = dir_field.get_value();
                if !start_guard(&shared, &log, &dir, &check_btn, &install_btn) {
                    return;
                }
                announce(&log, "Checking for updates...", true);
                let (tx, rx) = mpsc::channel();
                *shared.rx.borrow_mut() = Some(rx);
                let path = PathBuf::from(dir);
                thread::spawn(move || {
                    let result = flow::plan(&path, true).map(|plan| {
                        let _ = tx.send(Msg::Line(format!(
                            "Latest: {}{} (tag {})",
                            plan.latest,
                            if plan.prerelease { " prerelease" } else { "" },
                            plan.tag
                        )));
                        let _ = tx.send(Msg::Line(match &plan.installed {
                            Some(v) => format!("Installed: {v}"),
                            None => "Installed: none managed by this installer".to_string(),
                        }));
                        format!("Action would be: {}", plan.summary())
                    });
                    let _ = tx.send(Msg::Done(result));
                });
            });
        }

        // --- Install / Update: plan + apply on a worker thread. ---
        {
            let shared = shared.clone();
            install_btn.on_click(move |_| {
                let dir = dir_field.get_value();
                if !start_guard(&shared, &log, &dir, &check_btn, &install_btn) {
                    return;
                }
                announce(&log, "Starting install...", true);
                let (tx, rx) = mpsc::channel();
                *shared.rx.borrow_mut() = Some(rx);
                let path = PathBuf::from(dir);
                thread::spawn(move || {
                    let tx_log = tx.clone();
                    let result = flow::plan(&path, true).and_then(|plan| {
                        flow::apply(&path, &plan, false, |line| {
                            let _ = tx_log.send(Msg::Line(line.to_string()));
                        })
                    });
                    let _ = tx.send(Msg::Done(result));
                });
            });
        }

        // --- Close. ---
        {
            close_btn.on_click(move |_| {
                frame.close(true);
            });
        }

        // --- Idle pump: drain worker messages into the log. ---
        {
            let shared = shared.clone();
            frame.on_idle(move |event| {
                let mut done: Option<Result<String, String>> = None;
                if let Some(rx) = shared.rx.borrow().as_ref() {
                    for _ in 0..20 {
                        match rx.try_recv() {
                            Ok(Msg::Line(l)) => announce(&log, &l, false),
                            Ok(Msg::Done(res)) => {
                                done = Some(res);
                                break;
                            }
                            Err(_) => break,
                        }
                    }
                }
                if let Some(res) = done {
                    match res {
                        Ok(msg) => announce(&log, &msg, false),
                        Err(e) => announce(&log, &format!("Error: {e}"), false),
                    }
                    shared.busy.set(false);
                    *shared.rx.borrow_mut() = None;
                    check_btn.enable(true);
                    install_btn.enable(true);
                }
                if let WindowEventData::Idle(idle) = event {
                    idle.request_more(shared.busy.get());
                }
            });
        }
    });
}

/// Append a line to the log and speak it, so screen-reader users hear progress
/// the read-only log box wouldn't otherwise announce.
fn announce(log: &TextCtrl, line: &str, interrupt: bool) {
    log.append_text(&format!("{line}\n"));
    speech::speak(line, interrupt);
}

/// Validate input and mark the UI busy before starting a worker. Returns false
/// (and logs why) if the folder is wrong or a job is already running.
fn start_guard(
    shared: &Rc<Shared>,
    log: &TextCtrl,
    dir: &str,
    check_btn: &Button,
    install_btn: &Button,
) -> bool {
    if shared.busy.get() {
        return false;
    }
    let path = PathBuf::from(dir);
    if dir.trim().is_empty() || !paths::is_game_build_dir(&path) {
        announce(
            log,
            "That folder doesn't contain the game. Pick the WL2 Build folder.",
            true,
        );
        return false;
    }
    shared.busy.set(true);
    check_btn.enable(false);
    install_btn.enable(false);
    true
}

fn default_game_dir() -> String {
    paths::autodetect_game_dirs()
        .into_iter()
        .next()
        .map(|p| p.to_string_lossy().to_string())
        .unwrap_or_default()
}
