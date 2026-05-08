File: Helpers/CharacterAnnouncementHelper.ValueAdjustment.cs — AdjustAttribute, AdjustSkill, and ToggleTrait; invokes original NGUI handlers via the reflection cache and announces boundary cases.

namespace Wasteland2AccessibilityMod.Helpers  (line 5)

public static partial class CharacterAnnouncementHelper  (line 11)

    public static void AdjustAttribute(CHA_AttributeEditor editor, int direction, Action announceCallback)  (line 15)
        // note: calls attrOnPlusClickedMethod / attrOnMinusClickedMethod via reflection; speaks "Maximum" or "Minimum" when the boundary cannot be crossed; invokes announceCallback on success.

    public static void AdjustSkill(CHA_SkillEditor editor, int direction, Action announceCallback)  (line 48)
        // note: calls skillOnPlusClickedMethod / skillOnMinusClickedMethod via reflection; always invokes callback when the method is available (skill boundary enforcement is left to the game).

    // Invokes the trait's pressed callback (sets currentEditor in CHA_TraitsPanel) then toggles the checkbox; required to maintain proper game state.
    public static void ToggleTrait(CHA_TraitEditor editor)  (line 75)
        // note: must call pressedCallback BEFORE toggling checkbox; speaks "Locked", "selected", "not selected", or "Cannot select" depending on outcome.
