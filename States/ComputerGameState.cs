using System;
using System.Reflection;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;
using Wasteland2AccessibilityMod.Helpers;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Sonifies the Ranger Citadel Snake Easter egg (the in-world "ATM" computer).
    /// The game runs on an emulated 6502 (<c>Computer</c> / <c>p6502.*</c>) and renders
    /// to a raw pixel texture with no readable UI text, so it can't be navigated like a
    /// normal menu. Instead this state reads the Snake game's state straight out of the
    /// emulated RAM each frame and turns it into audio: a panned/pitched beacon points
    /// at the food, with cues for eating and for a lethal cell directly ahead.
    ///
    /// RAM layout (verified by disassembling Computer.LoadDefaultProgram, screen page
    /// = $0400; one byte per 32x32 cell):
    ///   $00/$01  food pointer (lo/hi)        $02  direction 1=up 2=right 4=down 8=left
    ///   $03      snake length * 2            $06/$07 score units/tens
    ///   $10..$1F eight obstacle pointers     $20,$22,.. snake segment pointers (head $20)
    /// Game over / win halts the CPU (regPC pins at $FFFF).
    ///
    /// Priority 59: above GenericMenuState (55, which otherwise grabs the ComputerMenu
    /// GUIScreen) and KeypadState (58), below DialogState (70). This state never sets
    /// the InputSuppressor flags, so the game keeps receiving W/A/S/D (read directly by
    /// Computer.Update via Input.inputString) and Escape (native Back closes the menu).
    /// </summary>
    public class ComputerGameState : AccessibilityStateBase
    {
        public override string Name => "ComputerGame";
        public override int Priority => 59;

        private const int Screen = 0x0400;      // low-res graphics page (cell 0)
        private const int GridW = 32;
        private const int GridEnd = 0x0800;     // one past the last screen byte

        public override string GetHelpText()
        {
            return "Snake game. Steer with W A S D. A beep beacon points to the food: " +
                   "panned left or right for direction, higher pitch when the food is above you, " +
                   "faster and louder as you get closer. A low buzz warns when the cell ahead is deadly. " +
                   "Escape closes the computer.";
        }

        // Cached emulator access (resolved on activation; see RAM layout above).
        private int[] mem;
        private object cpu;
        private static FieldInfo regPcField;

        // Change/event tracking.
        private int lastDir;
        private int lastLen;
        private bool ended;          // CPU halted (game over / win) and announced
        private bool wasPlaying;     // we have seen valid in-game positions
        private float nextBeaconTime;

        public override bool IsActive
        {
            get
            {
                var menu = SceneQueryCache.Find<ComputerMenu>();
                return menu != null && menu.gameObject.activeInHierarchy;
            }
        }

        public override bool HandleInput()
        {
            // Consume the mod's input frame so lower states (GenericMenuState etc.) don't
            // fight us, but do NOT set the InputSuppressor flags: the game must keep
            // receiving W/A/S/D and Escape.
            if (mem == null && !ResolveEmulator())
                return true;

            int pc = ReadRegPC();

            // regPC == $FFFF means the emulated CPU halted: game over or win.
            if (pc == 0xFFFF)
            {
                if (wasPlaying && !ended)
                {
                    ended = true;
                    SnakeSonifier.Stop();
                    int score = (mem[0x07] & 0xFF) * 10 + (mem[0x06] & 0xFF);
                    bool won = ScreenTextContains("CONQUER");
                    ScreenReaderManager.SpeakInterrupt(won
                        ? $"You win. Score {score}. Press R to restart or Escape to close."
                        : $"Game over. Score {score}. Press R to restart or Escape to close.");
                }
                // The halted CPU ignores keys, so drive the menu's Reset button ourselves.
                if (ended && Input.GetKeyDown(KeyCode.R))
                {
                    var menu = SceneQueryCache.Find<ComputerMenu>();
                    if (menu != null)
                    {
                        menu.OnResetClicked();
                        ScreenReaderManager.SpeakInterrupt("Restarting. Press any letter to begin.");
                    }
                }
                return true;
            }

            int headPtr = mem[0x20] + (mem[0x21] << 8);
            int foodPtr = mem[0x00] + (mem[0x01] << 8);

            // Title screen / pre-game init: positions not yet placed on the board.
            if (!InGrid(headPtr) || !InGrid(foodPtr))
            {
                SnakeSonifier.Stop();
                return true;
            }

            // A fresh game after an ended one: re-arm and announce.
            if (ended)
            {
                ended = false;
                lastDir = 0;
                lastLen = mem[0x03] & 0xFF;
                nextBeaconTime = 0f;
                ScreenReaderManager.SpeakInterrupt("New game.");
            }
            wasPlaying = true;

            int dir = mem[0x02] & 0xFF;
            int len = mem[0x03] & 0xFF;

            // Direction change feedback (terse, only on change).
            if (dir != lastDir && dir != 0)
            {
                lastDir = dir;
                ScreenReaderManager.SpeakInterrupt(DirName(dir));
            }

            // Food eaten: snake length grew (stored as segments * 2).
            if (len > lastLen)
                SnakeSonifier.Eat();
            lastLen = len;

            UpdateBeacon(headPtr, foodPtr, dir, len);
            return true;
        }

        public override void OnActivated()
        {
            ResetTracking();
            ResolveEmulator();
            ScreenReaderManager.SpeakInterrupt(
                "Snake game. Steer with W A S D. A beep beacon points to the food. " +
                "Escape closes the computer.");
            base.OnActivated();
        }

        public override void OnDeactivated()
        {
            SnakeSonifier.Stop();
            mem = null;
            cpu = null;
            ResetTracking();
            base.OnDeactivated();
        }

        private void ResetTracking()
        {
            lastDir = 0;
            lastLen = 0;
            ended = false;
            wasPlaying = false;
            nextBeaconTime = 0f;
        }

        // ---- Sonification ----

        private void UpdateBeacon(int headPtr, int foodPtr, int dir, int len)
        {
            int hCell = headPtr - Screen, fCell = foodPtr - Screen;
            int hCol = hCell % GridW, hRow = hCell / GridW;
            int fCol = fCell % GridW, fRow = fCell / GridW;

            int dCol = fCol - hCol;      // negative = food to the left
            int dRow = fRow - hRow;      // negative = food above
            int dist = Mathf.Abs(dCol) + Mathf.Abs(dRow);

            // If the cell straight ahead is lethal, buzz instead of beaconing.
            if (DangerAhead(headPtr, dir, len))
            {
                if (Time.unscaledTime >= nextBeaconTime)
                {
                    nextBeaconTime = Time.unscaledTime + 0.18f;
                    SnakeSonifier.Danger(Mathf.Clamp(dCol / 10f, -1f, 1f));
                }
                return;
            }

            // Closer food -> faster, louder beeps.
            float t = Mathf.Clamp01(dist / 40f);
            float interval = Mathf.Lerp(0.13f, 0.8f, t);
            if (Time.unscaledTime < nextBeaconTime) return;
            nextBeaconTime = Time.unscaledTime + interval;

            float pan = Mathf.Clamp(dCol / 10f, -1f, 1f);
            float pitch = Mathf.Clamp(1f - dRow * 0.06f, 0.5f, 2.2f); // above = higher
            float vol = Mathf.Lerp(0.6f, 0.22f, t);
            SnakeSonifier.Beacon(pan, pitch, vol);
        }

        /// <summary>
        /// True if moving one cell in the current direction would end the game: leaving
        /// the board, wrapping a column edge, or hitting the snake body / an obstacle.
        /// </summary>
        private bool DangerAhead(int headPtr, int dir, int len)
        {
            int next;
            switch (dir)
            {
                case 1: // up
                    next = headPtr - GridW;
                    if (next < Screen) return true;
                    break;
                case 2: // right
                    if ((headPtr & 0x1F) == 0x1F) return true;
                    next = headPtr + 1;
                    break;
                case 4: // down
                    next = headPtr + GridW;
                    if (next >= GridEnd) return true;
                    break;
                case 8: // left
                    if ((headPtr & 0x1F) == 0) return true;
                    next = headPtr - 1;
                    break;
                default:
                    return false;
            }

            // Snake body (skip the head itself, segment 0).
            int segCount = (len & 0xFF) / 2;
            for (int i = 1; i < segCount; i++)
            {
                int seg = mem[0x20 + i * 2] + (mem[0x21 + i * 2] << 8);
                if (seg == next) return true;
            }
            // Obstacles that actually landed on the visible board.
            for (int i = 0; i < 8; i++)
            {
                int obs = mem[0x10 + i * 2] + (mem[0x11 + i * 2] << 8);
                if (InGrid(obs) && obs == next) return true;
            }
            return false;
        }

        private static bool InGrid(int ptr) => ptr >= Screen && ptr < GridEnd;

        private static string DirName(int dir)
        {
            switch (dir)
            {
                case 1: return "up";
                case 2: return "right";
                case 4: return "down";
                case 8: return "left";
                default: return "";
            }
        }

        /// <summary>
        /// Scans the emulated text buffer ($0200-$03FF, ASCII) for a substring, used to
        /// tell the win screen ("CONQUERED") from the loss screen ("GAME OVER").
        /// </summary>
        private bool ScreenTextContains(string needle)
        {
            try
            {
                var sb = new System.Text.StringBuilder(512);
                for (int a = 0x0200; a < 0x0400; a++)
                {
                    int c = mem[a] & 0xFF;
                    sb.Append(c >= 32 && c < 127 ? (char)c : ' ');
                }
                return sb.ToString().IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        // ---- Emulator reflection ----

        /// <summary>
        /// Resolves the active Computer's RAM array and CPU from the open ComputerMenu.
        /// The int[] backing RAM is stable per Computer instance, so we cache it.
        /// </summary>
        private bool ResolveEmulator()
        {
            try
            {
                var menu = SceneQueryCache.Find<ComputerMenu>();
                if (menu == null) return false;

                object computer = typeof(ComputerMenu)
                    .GetField("computer", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(menu);
                if (computer == null) return false;

                object ram = computer.GetType()
                    .GetField("ram", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(computer);
                if (ram == null) return false;

                mem = ram.GetType()
                    .GetField("memory", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(ram) as int[];

                cpu = computer.GetType()
                    .GetField("cpu", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(computer);
                if (cpu != null && regPcField == null)
                    regPcField = cpu.GetType().GetField("regPC", BindingFlags.Public | BindingFlags.Instance);

                return mem != null && mem.Length >= 0x10000;
            }
            catch (Exception ex)
            {
                ModLog.Debug($"[ComputerGameState] ResolveEmulator failed: {ex.Message}");
                return false;
            }
        }

        private int ReadRegPC()
        {
            if (cpu == null || regPcField == null) return 0;
            try { return (int)regPcField.GetValue(cpu); }
            catch { return 0; }
        }
    }
}
