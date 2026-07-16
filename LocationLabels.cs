using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Wasteland2AccessibilityMod.Helpers;

namespace Wasteland2AccessibilityMod
{
    /// <summary>One labelled tile, resolved for the current scene.</summary>
    public struct LabeledLocation
    {
        /// <summary>World position the label was placed at (used for distance / direction).</summary>
        public Vector3 Position;
        /// <summary>The player's own text for this place.</summary>
        public string Text;
    }

    /// <summary>
    /// Player-authored labels attached to map tiles, so a place found once can be
    /// recognised on return ("the door that teleports"). Labels are notes about the
    /// world, not game state.
    ///
    /// Keyed on scene + grid coordinate (x, floor, z) via TileCoordinateSystem.GetGridId.
    /// That key is stable across sessions because it is pure arithmetic over a world
    /// position against a fixed tile size, and world positions are baked scene data — the
    /// same tile yields the same key every load. The floor component is load-bearing: the
    /// game stacks storeys at the same x/z (CombatAStar distinguishes them by node id.y),
    /// so without it a door on the ground floor and one directly above would collide.
    ///
    /// Stored globally in UserData rather than inside a save. Labels are the player's
    /// notes, so tying them to a save would mean labelling a door, reloading an earlier
    /// save, and silently losing the note — exactly when it is most wanted. A global file
    /// also needs no save/load hooks, and since the keys are scene-baked the labels stay
    /// meaningful in a new playthrough (see ClearAll for starting fresh).
    /// </summary>
    public static class LocationLabels
    {
        // Relative path, matching the convention ModConfig already relies on for its .cfg.
        private const string FilePath = "UserData/Wasteland2AccessibilityLabels.txt";

        // Tab-separated: scene, gridX, floor, gridZ, worldX, worldY, worldZ, label.
        // The label comes last so it may contain spaces; tabs and newlines are stripped
        // on the way in (Sanitize) so a label can never break the line format.
        private const char FieldSeparator = '\t';
        private const int FieldCount = 8;

        private class Entry
        {
            public string Scene;
            public Vector3 GridId;
            public Vector3 World;
            public string Text;
        }

        private static Dictionary<string, Entry> entries;

        /// <summary>Total labels across all scenes. Loads the file if needed.</summary>
        public static int Count
        {
            get
            {
                EnsureLoaded();
                return entries.Count;
            }
        }

        // ===== Public API (all take a world position; key derivation lives here) =====

        /// <summary>The label on the tile containing <paramref name="worldPos"/>, or null.</summary>
        public static string Get(Vector3 worldPos)
        {
            EnsureLoaded();
            Entry entry;
            return entries.TryGetValue(KeyFor(worldPos), out entry) ? entry.Text : null;
        }

        /// <summary>True if the tile containing <paramref name="worldPos"/> is labelled.</summary>
        public static bool Has(Vector3 worldPos)
        {
            return Get(worldPos) != null;
        }

        /// <summary>
        /// Labels the tile containing <paramref name="worldPos"/>, replacing any existing
        /// label there. An empty or whitespace-only <paramref name="text"/> removes the
        /// label instead — that is what an emptied text box means. Returns the text stored,
        /// or null if the label was removed. Persists immediately.
        /// </summary>
        public static string Set(Vector3 worldPos, string text)
        {
            EnsureLoaded();

            string clean = Sanitize(text);
            string key = KeyFor(worldPos);

            if (string.IsNullOrEmpty(clean))
            {
                if (entries.Remove(key)) Save();
                return null;
            }

            entries[key] = new Entry
            {
                Scene = CurrentScene(),
                GridId = TileCoordinateSystem.GetGridId(worldPos),
                World = worldPos,
                Text = clean
            };
            Save();
            return clean;
        }

        /// <summary>Removes the label on this tile. True if there was one.</summary>
        public static bool Remove(Vector3 worldPos)
        {
            EnsureLoaded();
            if (!entries.Remove(KeyFor(worldPos))) return false;
            Save();
            return true;
        }

        /// <summary>
        /// Every label in the current scene, unordered. Callers sort (the scanner sorts by
        /// distance from the player). Labels in other scenes stay in the file untouched.
        /// </summary>
        public static List<LabeledLocation> GetForCurrentScene()
        {
            EnsureLoaded();
            string scene = CurrentScene();
            var result = new List<LabeledLocation>();
            foreach (var entry in entries.Values)
            {
                if (entry.Scene != scene) continue;
                result.Add(new LabeledLocation { Position = entry.World, Text = entry.Text });
            }
            return result;
        }

        /// <summary>
        /// Deletes every label in every scene and persists. Returns how many were removed.
        /// Irreversible — callers must confirm first (SettingsMenuState does).
        /// </summary>
        public static int ClearAll()
        {
            EnsureLoaded();
            int removed = entries.Count;
            entries.Clear();
            Save();
            ModLog.Info($"[LocationLabels] Cleared all {removed} location labels");
            return removed;
        }

        // ===== Key derivation =====

        private static string CurrentScene()
        {
            return Application.loadedLevelName ?? string.Empty;
        }

        private static string KeyFor(Vector3 worldPos)
        {
            return MakeKey(CurrentScene(), TileCoordinateSystem.GetGridId(worldPos));
        }

        private static string MakeKey(string scene, Vector3 gridId)
        {
            return scene + "|" + (int)gridId.x + "|" + (int)gridId.y + "|" + (int)gridId.z;
        }

        /// <summary>
        /// Strips the characters that would corrupt the line format, collapses surrounding
        /// whitespace, and caps length so a runaway label can't dominate an announcement.
        /// </summary>
        private static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string clean = text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (clean.Length == 0) return null;
            if (clean.Length > 100) clean = clean.Substring(0, 100).Trim();
            return clean;
        }

        // ===== Persistence =====

        private static void EnsureLoaded()
        {
            if (entries != null) return;
            entries = new Dictionary<string, Entry>();

            try
            {
                if (!File.Exists(FilePath)) return;

                int malformed = 0;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    if (string.IsNullOrEmpty(line) || line[0] == '#') continue;

                    Entry entry = ParseLine(line);
                    if (entry == null) { malformed++; continue; }

                    entries[MakeKey(entry.Scene, entry.GridId)] = entry;
                }

                ModLog.Info($"[LocationLabels] Loaded {entries.Count} location labels"
                    + (malformed > 0 ? $" ({malformed} malformed lines skipped)" : ""));
            }
            catch (Exception ex)
            {
                // A broken or unreadable file must not take the mod down; carry on with
                // whatever parsed. Save() will rewrite the file cleanly on the next edit.
                ModLog.Error($"[LocationLabels] Could not read {FilePath}: {ex.Message}");
            }
        }

        private static Entry ParseLine(string line)
        {
            string[] f = line.Split(new[] { FieldSeparator }, FieldCount);
            if (f.Length < FieldCount) return null;

            int gx, floor, gz;
            float wx, wy, wz;
            if (!TryInt(f[1], out gx) || !TryInt(f[2], out floor) || !TryInt(f[3], out gz)) return null;
            if (!TryFloat(f[4], out wx) || !TryFloat(f[5], out wy) || !TryFloat(f[6], out wz)) return null;

            string text = Sanitize(f[7]);
            if (string.IsNullOrEmpty(text)) return null;

            return new Entry
            {
                Scene = f[0],
                GridId = new Vector3(gx, floor, gz),
                World = new Vector3(wx, wy, wz),
                Text = text
            };
        }

        private static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var lines = new List<string>();
                lines.Add("# Wasteland 2 Accessibility Mod - location labels.");
                lines.Add("# scene<TAB>gridX<TAB>floor<TAB>gridZ<TAB>worldX<TAB>worldY<TAB>worldZ<TAB>label");
                foreach (var entry in entries.Values)
                    lines.Add(FormatLine(entry));

                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception ex)
            {
                ModLog.Error($"[LocationLabels] Could not write {FilePath}: {ex.Message}");
            }
        }

        private static string FormatLine(Entry e)
        {
            return string.Join(FieldSeparator.ToString(), new[]
            {
                e.Scene,
                ((int)e.GridId.x).ToString(CultureInfo.InvariantCulture),
                ((int)e.GridId.y).ToString(CultureInfo.InvariantCulture),
                ((int)e.GridId.z).ToString(CultureInfo.InvariantCulture),
                Num(e.World.x), Num(e.World.y), Num(e.World.z),
                e.Text
            });
        }

        // Invariant culture throughout: a comma-decimal locale would otherwise write
        // coordinates this parser cannot read back.
        private static string Num(float v)
        {
            return v.ToString("F3", CultureInfo.InvariantCulture);
        }

        private static bool TryInt(string s, out int value)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryFloat(string s, out float value)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
