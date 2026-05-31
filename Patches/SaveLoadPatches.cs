using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Forces the save/load screen to always sort by time descending (newest first).
    ///
    /// The game's UIGrid only applies its sort function when its "sorted" boolean is true.
    /// If that field is false in the scene prefab, items display in creation order (which
    /// ends up alphabetical from the dictionary). This patch ensures the grid is configured
    /// to sort by time descending before PopulateData calls Reposition().
    ///
    /// Also resets the static sortMode so InitializeSort always produces time-descending.
    /// </summary>
    [HarmonyPatch(typeof(SaveLoadScreen), "InitializeSort")]
    public class SaveLoadScreen_InitializeSort_Patch
    {
        private static FieldInfo sortModeField;

        [HarmonyPrefix]
        public static void Prefix()
        {
            if (sortModeField == null)
            {
                sortModeField = typeof(SaveLoadScreen).GetField("sortMode",
                    BindingFlags.Static | BindingFlags.NonPublic);
            }

            if (sortModeField != null)
            {
                sortModeField.SetValue(null, SortBarMode.TypeDescending);
            }
        }
    }

    [HarmonyPatch(typeof(SaveLoadScreen), "PopulateData")]
    public class SaveLoadScreen_PopulateData_Patch
    {
        // saveGrid is a public field on SaveLoadScreen; cache once at class load.
        private static readonly FieldInfo saveGridField =
            typeof(SaveLoadScreen).GetField("saveGrid", BindingFlags.Public | BindingFlags.Instance);

        [HarmonyPrefix]
        public static void Prefix(SaveLoadScreen __instance)
        {
            try
            {
                if (saveGridField == null) return;

                var grid = saveGridField.GetValue(__instance) as UIGrid;
                if (grid == null) return;

                // Ensure the grid actually applies its sort function during Reposition()
                grid.sorted = true;

                // Set the sort function to time descending (newest first)
                // SetSortFunction also triggers repositionNow, but PopulateData
                // calls Reposition() explicitly at the end, so the sort will apply.
                grid.SetSortFunction(new Comparison<Transform>(SaveGameListEntry.SortByTimeDescending));
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[SaveLoadPatches] Failed to configure grid sort: " + e.Message);
            }
        }
    }
}
