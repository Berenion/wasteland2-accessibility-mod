using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    /// <summary>
    /// Area-of-effect aiming (grenades, rockets) for the combat and exploration cursors.
    ///
    /// AoE is a completely different attack path from a normal shot, which is why free aim
    /// could never fire one: an AoE weapon aims at a POSITION and never has a Targetable.
    /// Mob.CanAttack even hard-rejects RPG/Thrown weapons (Mob.cs:1479) unless allowAoe is
    /// set, and EventInfo_CommandAttack is not the event that carries the attack —
    /// EventInfo_CommandAreaAttack is, published from PC.ThrowGrenade / PC.ShootRocket.
    ///
    /// Vanilla's sequence, which <see cref="Throw"/> reproduces exactly:
    ///   1. InputManager.SetupThrowableAiming(pc) (InputManager.cs:4707) — sets the
    ///      "aoeattack" ASI, calls CursorManager.ShowSphere() (which caches the PC's
    ///      position, attack range, arc flag and blast radius via SetSphereRadius), and
    ///      flags inAimingMode.
    ///   2. CursorManager.SetSpherePosition(aimPoint) — clamps the point to the weapon's
    ///      range, raycasts it down onto the ground, and computes BOTH spherePosition (where
    ///      the blast lands) and arcAimPosition (where the projectile is thrown toward, which
    ///      differs for arcing grenades). Doing this by hand would get the arc wrong.
    ///   3. PC.UseAOEWeapon(arcAimPosition, spherePosition) (PC.cs:1360) — re-checks
    ///      Distance(pc, aimPosition) &lt; attackRange * 1.1f and SILENTLY does nothing if
    ///      that fails, then throws or shoots.
    ///
    /// AP is charged later by AIAction_AreaAttack.IsComplete (in combat only), and the
    /// grenade itself is consumed by AIAction_AreaAttack.Release -> DestroyEmptyWeapon.
    /// AIBehaviour_PCCombat.OnAreaAttack additionally requires the mob to be THINKING with
    /// enough AP, so both are checked up front here to avoid a silent no-op.
    /// </summary>
    internal static class AoeAimHelper
    {
        /// <summary>The AoE template of the PC's equipped weapon, or null if it isn't one.</summary>
        public static ItemTemplate_WeaponAoe GetAoeTemplate(PC pc)
        {
            if (pc == null || pc.stats == null) return null;
            try { return pc.stats.GetWeaponTemplate() as ItemTemplate_WeaponAoe; }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AoeAimHelper] GetAoeTemplate failed: {ex.Message}");
                return null;
            }
        }

        public static bool IsAoeWeapon(PC pc)
        {
            return GetAoeTemplate(pc) != null;
        }

        /// <summary>
        /// Blast radius in world units, matching what CursorManager.SetSphereRadius shows
        /// and what Mob.Attack uses for the damage sphere: the template radius plus the
        /// PC's own modifier (perks/traits).
        /// </summary>
        public static float GetBlastRadius(PC pc)
        {
            var template = GetAoeTemplate(pc);
            if (template == null) return 0f;
            float radius = template.aoeRadius;
            try { radius += pc.pcStats.GetAOERadiusModifier(); }
            catch (Exception ex) { MelonLogger.Warning($"[AoeAimHelper] AOE radius modifier failed: {ex.Message}"); }
            return radius;
        }

        /// <summary>
        /// The reach test PC.UseAOEWeapon itself applies (PC.cs:1362). Anything beyond this
        /// is a silent no-op in vanilla, so callers must check it and say so out loud.
        /// </summary>
        public static bool IsInThrowRange(PC pc, Vector3 target)
        {
            if (pc == null || pc.stats == null) return false;
            return Vector3.Distance(pc.transform.position, target) < pc.stats.GetAttackRange() * 1.1f;
        }

        /// <summary>Who a blast centred on <paramref name="center"/> would actually catch.</summary>
        public struct BlastSummary
        {
            public List<Mob> Hostiles;
            public List<Mob> Friendlies;   // party members and non-hostile NPCs, thrower excluded
            public List<TargetableObject> Objects;
            public bool CatchesSelf;       // the thrower is inside their own blast
            public float Radius;
        }

        /// <summary>
        /// Everything inside the blast, using the game's own sphere query
        /// (<c>Mob.GetTargetsInSphere</c>) — the SAME call that applies the damage
        /// (Mob.cs:2714), including its line-of-sight rule and the weapon's ignoreVisibility
        /// flag. Anything else would preview a different set than the one that gets hit.
        /// </summary>
        public static BlastSummary SummarizeBlast(PC pc, Vector3 center)
        {
            var summary = new BlastSummary
            {
                Hostiles = new List<Mob>(),
                Friendlies = new List<Mob>(),
                Objects = new List<TargetableObject>(),
                CatchesSelf = false,
                Radius = GetBlastRadius(pc)
            };

            var template = GetAoeTemplate(pc);
            if (template == null) return summary;

            List<Targetable> inBlast;
            try
            {
                inBlast = pc.GetTargetsInSphere(center, summary.Radius, template.ignoreVisibility);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AoeAimHelper] GetTargetsInSphere failed: {ex.Message}");
                return summary;
            }
            if (inBlast == null) return summary;

            foreach (var target in inBlast)
            {
                if (target == null) continue;

                var mob = target as Mob;
                if (mob == null)
                {
                    var obj = target as TargetableObject;
                    if (obj != null) summary.Objects.Add(obj);
                    continue;
                }

                if (mob == pc)
                {
                    // hitsSelf false means the thrower is immune to their own blast, so it
                    // isn't worth warning about (ItemTemplate_WeaponAoe.hitsSelf).
                    if (template.hitsSelf) summary.CatchesSelf = true;
                    continue;
                }

                bool hostile;
                try { hostile = mob.HatesParty(); }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[AoeAimHelper] HatesParty failed: {ex.Message}");
                    hostile = false;
                }

                if (hostile) summary.Hostiles.Add(mob);
                else summary.Friendlies.Add(mob);
            }

            return summary;
        }

        /// <summary>
        /// True when the blast would catch someone the player almost certainly didn't mean
        /// to hit — a party member / friendly NPC, or the thrower themselves.
        /// </summary>
        public static bool HasFriendlyFire(BlastSummary summary)
        {
            return summary.CatchesSelf || summary.Friendlies.Count > 0;
        }

        /// <summary>
        /// Runs vanilla's throw sequence at <paramref name="target"/>. Returns false with a
        /// spoken-ready reason when the attack could not be made, so the caller never
        /// reports a throw that silently didn't happen.
        /// </summary>
        public static bool Throw(PC pc, Vector3 target, out string failureReason)
        {
            failureReason = null;

            var template = GetAoeTemplate(pc);
            if (template == null)
            {
                failureReason = "No area weapon equipped";
                return false;
            }

            // AIBehaviour_PCCombat.OnAreaAttack drops the command on the floor unless the
            // mob is THINKING with the AP to pay for it, so check both rather than let the
            // throw vanish (AIBehaviour_PCCombat.cs:356).
            if (MonoBehaviourSingleton<CombatManager>.HasInstance() &&
                MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat)
            {
                int cost = pc.GetActionPointsToAttack();
                if (pc.combatActionPointsRemaining < cost)
                {
                    failureReason = "Not enough AP, needs " + cost;
                    return false;
                }
                if (pc.combatActionState != Mob.CombatActionState.THINKING)
                {
                    failureReason = "Not this character's turn";
                    return false;
                }
            }

            if (!IsInThrowRange(pc, target))
            {
                failureReason = "Out of range. Distance " +
                    TileCoordinateSystem.GetDistanceText(pc.transform.position, target) +
                    ", weapon range " + TileCoordinateSystem.GetRangeText(pc.stats.GetAttackRange());
                return false;
            }

            try
            {
                var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
                var cursorManager = MonoBehaviourSingleton<CursorManager>.GetInstance();

                // CursorManager solves the sphere for InputManager.GetFirstSelectedPlayer(),
                // NOT for the PC handed to UseAOEWeapon: SetSphereRadius reads that player's
                // position / range / blast radius / arc flag (CursorManager.cs:1204) and
                // SetSpherePosition uses their GetHighCoverAttackPoint (CursorManager.cs:648).
                // If the thrower isn't that player the arc is solved for someone else, so
                // refuse rather than lob a grenade along a stranger's trajectory. In practice
                // these agree — exploration aims with the first selected PC, and combat
                // selects the active one — so this is a guard, not an expected path.
                if (inputManager.GetFirstSelectedPlayer() != pc)
                {
                    failureReason = "Select this character before throwing";
                    return false;
                }

                // Step 1+2: let the game set up and solve the arc. SetupThrowableAiming
                // seeds the sphere from the PC's own stats; SetSpherePosition then clamps to
                // range and fills in spherePosition / arcAimPosition.
                inputManager.SetupThrowableAiming(pc);
                cursorManager.SetSpherePosition(target);

                // Step 3: throw at what the game resolved, not at our raw cursor point.
                pc.UseAOEWeapon(cursorManager.arcAimPosition, cursorManager.spherePosition);

                ClearAiming();
                return true;
            }
            catch (Exception ex)
            {
                // SetupThrowableAiming may already have put the game into aiming mode, so
                // tear it down rather than leave a stuck sphere and a live ASI behind.
                MelonLogger.Error($"[AoeAimHelper] Throw failed: {ex.Message}");
                ClearAiming();
                failureReason = "Attack failed";
                return false;
            }
        }

        /// <summary>
        /// Tears down the aiming visuals the way vanilla's private ClearAimingMode does
        /// (InputManager.cs:4833): drop the ASI, force the cursor off AOEAttack so
        /// CursorManager hides the sphere and clears the arc (CursorManager.cs:349), and
        /// leave aiming mode.
        /// </summary>
        public static void ClearAiming()
        {
            try
            {
                UseASIManager.SetActiveASIName(null);
                UseASIManager.SetActiveASIItem(null, null);
                MonoBehaviourSingleton<InputManager>.GetInstance().inAimingMode = false;
                MonoBehaviourSingleton<CursorManager>.GetInstance()
                    .SetCursor(CursorManager.Cursors.Default, forceChange: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AoeAimHelper] ClearAiming failed: {ex.Message}");
            }
        }
    }
}
