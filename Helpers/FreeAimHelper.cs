using System;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    /// <summary>
    /// Shared free-aim tile resolution for CombatState and MapCursorState.
    ///
    /// Destructible scenery — including propane tanks — is a normal
    /// <see cref="TargetableObject"/>. The wiring is indirect enough to be worth writing
    /// down, because it is NOT the DestructableObject pattern:
    ///
    ///   - The prop's root carries an <see cref="InteractableObject"/> (a Drama) plus a
    ///     plain TargetableObject. InteractableObject.InitializeMembers caches it
    ///     (InteractableObject.cs:2092) and calls myTargetable.Init() from Event_Awakened
    ///     (InteractableObject.cs:372), which is what registers it in Game.targetableObjects.
    ///     So it is in the registry from scene load — there is no lazy init to wait for.
    ///   - curHP is a serialized field on Targetable authored per-instance in the scene
    ///     (nothing in code assigns it for a plain TargetableObject; Game.cs:936 only
    ///     overwrites it from a save). hasHitpoints is declared but never read anywhere in
    ///     the game — do not gate on it.
    ///   - Shooting it runs TargetableObject.TakeDamage, and at curHP &lt;= 0 that calls
    ///     Drama.ExplodeObject (TargetableObject.cs:159) -> Event_Exploded ->
    ///     InteractableObject.Exploded (InteractableObject.cs:2960). That is what detonates
    ///     a propane tank and fires script callbacks like AZ2_Townie16.PropaneExplodes.
    ///
    /// <see cref="PropaneTankCollision"/> is a SEPARATE contact detonator — a trigger on
    /// layer 23 that fires when anything not on layer 1/2/4 walks into it. It is not the
    /// shooting mechanism, but it is why standing next to a tank is dangerous, so it is
    /// still worth announcing.
    ///
    /// The lookups below exist because a prop's collider often is not on the same
    /// GameObject as its TargetableObject: cover routinely puts the collider on a child or
    /// sibling and points at the real Targetable through a <see cref="RaycastPropagate"/>.
    /// A plain GetComponentInParent walk therefore misses it, which is why the registry is
    /// queried first and the collider walk follows RaycastPropagate the way vanilla's
    /// attack ray does (InputManager.cs:2029).
    /// </summary>
    internal static class FreeAimHelper
    {
        /// <summary>
        /// Radius for the physics overlaps below. Deliberately NOT the tile-match radius
        /// (SquareSize * 0.75 = 1.2), which is a loose axis-aligned test for matching a
        /// registered object's transform to a tile. A sphere that wide bleeds into the
        /// neighbouring tiles and would report cover the cursor isn't on; 0.75 stays inside
        /// the 1.6-unit tile and is the value the combat free-aim lookups have always used.
        /// </summary>
        public const float TileOverlapRadius = 0.75f;

        /// <summary>
        /// Resolves the <see cref="TargetableObject"/> reachable from a hit collider: the
        /// component itself, its parent, or — as vanilla's attack ray does
        /// (InputManager.cs:2029) — the target a <see cref="RaycastPropagate"/> on the
        /// collider points to. Returns null if the collider maps to no Targetable.
        /// </summary>
        public static TargetableObject ResolveTargetableObject(Collider collider)
        {
            if (collider == null || collider.gameObject == null) return null;

            var obj = collider.GetComponent<TargetableObject>();
            if (obj == null) obj = collider.GetComponentInParent<TargetableObject>();
            if (obj != null) return obj;

            var propagate = collider.GetComponent<RaycastPropagate>();
            if (propagate == null) propagate = collider.GetComponentInParent<RaycastPropagate>();
            if (propagate != null && propagate.target != null)
            {
                obj = propagate.target.GetComponent<TargetableObject>();
                if (obj == null) obj = propagate.target.GetComponentInParent<TargetableObject>();
            }
            return obj;
        }

        /// <summary>
        /// Every live destructible on the tile, most reliable source first.
        ///
        /// The game's own registry (Game.targetableObjects) is the list vanilla builds its
        /// attack targets from (InputManager.cs:3917, TargetingPanel.cs:744), gated on
        /// curHP &gt; 0 — the same validity check vanilla applies (InputManager.cs:3957,
        /// TargetingPanel.cs:748). Matching by transform position sidesteps all the collider
        /// guesswork. The physics overlap then adds anything whose registered transform sits
        /// off-tile but whose collider reaches onto it.
        /// </summary>
        /// <param name="matchRadius">Tile-match half-extent for the registry sweep.</param>
        public static System.Collections.Generic.List<TargetableObject> FindDestructiblesAt(
            Vector3 worldPos, float matchRadius)
        {
            var found = new System.Collections.Generic.List<TargetableObject>();
            var seen = new System.Collections.Generic.HashSet<TargetableObject>();

            if (MonoBehaviourSingleton<Game>.HasInstance())
            {
                var objects = MonoBehaviourSingleton<Game>.GetInstance().targetableObjects;
                if (objects != null)
                {
                    foreach (var obj in objects)
                    {
                        if (obj == null || obj.gameObject == null) continue;
                        if (obj.curHP <= 0f) continue;
                        if (!obj.gameObject.activeInHierarchy) continue;
                        try
                        {
                            Vector3 p = obj.transform.position;
                            if (Mathf.Abs(p.x - worldPos.x) <= matchRadius &&
                                Mathf.Abs(p.z - worldPos.z) <= matchRadius &&
                                seen.Add(obj))
                                found.Add(obj);
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"[FreeAimHelper] registry transform check failed: {ex.Message}");
                        }
                    }
                }
            }

            try
            {
                Collider[] colliders = Physics.OverlapSphere(worldPos, TileOverlapRadius);
                foreach (var collider in colliders)
                {
                    TargetableObject obj = ResolveTargetableObject(collider);
                    if (obj == null || obj.curHP <= 0f) continue;
                    if (!obj.gameObject.activeInHierarchy) continue;
                    if (seen.Add(obj)) found.Add(obj);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[FreeAimHelper] overlap sweep failed: {ex.Message}");
            }

            return found;
        }

        /// <summary>
        /// True when the tile holds static, indestructible cover — a collider that is not
        /// backed by a <see cref="TargetableObject"/> and is either on the Cover layer,
        /// carries a <see cref="Cover"/> component, or sits on the Wall layer. Wasteland 2
        /// models scenery cover two ways: low cover (P_CoverLow_*) on the Cover layer, high
        /// cover (P_CoverHigh_*) on the Wall layer. Both block line of sight and grant a
        /// cover bonus but have no hitpoints, so a shot at them only hits the ground. A live
        /// destructible also carries a Cover component / sits on these layers, so the
        /// no-Targetable check is what separates "can't be destroyed" from a real target.
        /// </summary>
        public static bool HasIndestructibleCoverAt(Vector3 worldPos, float radius)
        {
            int coverLayer = LayerMask.NameToLayer("Cover");
            int wallLayer = LayerMask.NameToLayer("Wall");
            Collider[] colliders = Physics.OverlapSphere(worldPos, radius);
            foreach (var collider in colliders)
            {
                if (collider == null || collider.gameObject == null) continue;

                int layer = collider.gameObject.layer;
                bool isCover = (coverLayer >= 0 && layer == coverLayer)
                               || (wallLayer >= 0 && layer == wallLayer)
                               || collider.GetComponent<Cover>() != null
                               || collider.GetComponentInParent<Cover>() != null;
                if (!isCover) continue;

                if (ResolveTargetableObject(collider) == null)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True when this prop detonates on contact — it carries a
        /// <see cref="PropaneTankCollision"/> trigger somewhere in its hierarchy. Shooting
        /// it is a normal attack (it is a TargetableObject like any other destructible);
        /// this only says the blast is also a hazard to anyone who walks into it, which is
        /// worth saying out loud before the player parks a ranger beside one.
        /// </summary>
        public static bool IsContactExplosive(TargetableObject obj)
        {
            if (obj == null || obj.gameObject == null) return false;
            try
            {
                return obj.GetComponentInChildren<PropaneTankCollision>() != null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[FreeAimHelper] IsContactExplosive failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Turns a prefab object name into something speakable: drops "(Clone)" and the
        /// level designers' "- HIDDENFORBUG"-style suffixes, splits CamelCase and
        /// underscores. "PropaneTank- HIDDENFORBUG" -&gt; "Propane Tank".
        /// </summary>
        public static string PrettifyPropName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "object";

            string name = rawName.Replace("(Clone)", "").Replace("_", " ");

            // Designer annotations are appended after a dash (e.g. "- HIDDENFORBUG").
            int dash = name.IndexOf('-');
            if (dash > 0) name = name.Substring(0, dash);

            name = name.Trim();
            if (name.Length == 0) return "object";

            // Split CamelCase runs so "PropaneTank" reads as two words.
            var sb = new System.Text.StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]) && name[i - 1] != ' ')
                    sb.Append(' ');
                sb.Append(c);
            }

            string result = sb.ToString().Trim();
            return result.Length > 0 ? result : "object";
        }
    }
}
