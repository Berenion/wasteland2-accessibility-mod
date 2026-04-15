using HarmonyLib;
using System;

namespace Wasteland2AccessibilityMod.Patches
{
    // ModalMessageMenu_SetMessage_Patch removed — DialogState.AnnounceDialog() already reads
    // the full dialog content (title, message, buttons) when it activates.
    // The SetMessage patch was firing first and then being interrupted by AnnounceDialog's
    // SpeakInterrupt call, causing the text to be cut off.
}
