using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Virtual map cursor for exploring the game world without relying on
    /// visual feedback. Arrow keys move the cursor, and objects at the cursor
    /// position are announced.
    /// Priority 30 - below menu states but above exploration cycling.
    /// </summary>
    public class MapCursorState : IAccessibilityState
    {
        public string Name => "MapCursor";
        public int Priority => 30;

        // Cursor state
        private Vector3 cursorPosition;
        private bool cursorActive = false;
        private float lastMoveTime = 0f;

        // Movement settings
        private const float MOVE_STEP = 2.0f;  // Units per key press
        private const float MOVE_REPEAT_DELAY = 0.15f;  // Seconds between repeated moves when holding
        private const float SCAN_RADIUS = 3.0f;  // Radius to search for objects

        // Camera follow
        private bool cameraFollowsCursor = true;

        // Layer masks for raycasting
        private const int LAYER_MASK_GROUND = 33024;  // Ground/terrain layers
        private const int LAYER_MASK_OBJECTS = 1050112;  // Interactable objects

        public bool IsActive
        {
            get
            {
                // Only active during gameplay, not in menus or combat
                if (!MonoBehaviourSingleton<GUIManager>.HasInstance()) return false;
                if (MonoBehaviourSingleton<GUIManager>.GetInstance().IsAnyMenuActive()) return false;

                // Not during conversations
                if (Drama.isConversationOn) return false;

                // Not in combat (combat has its own cursor)
                if (MonoBehaviourSingleton<CombatManager>.HasInstance() &&
                    MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat)
                {
                    return false;
                }

                // Only when cursor mode is active (toggled by user)
                return cursorActive;
            }
        }

        public bool HandleInput()
        {
            // M key toggles cursor mode on/off
            if (Input.GetKeyDown(KeyCode.M))
            {
                ToggleCursorMode();
                return true;
            }

            // Only process movement when cursor is active
            if (!cursorActive) return false;

            bool moved = false;
            Vector3 moveDirection = Vector3.zero;

            // Get current camera forward direction (projected to XZ plane)
            Vector3 cameraForward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();

            Vector3 cameraRight = Camera.main != null ? Camera.main.transform.right : Vector3.right;
            cameraRight.y = 0;
            cameraRight.Normalize();

            // Check for held keys with repeat
            float currentTime = Time.time;
            bool canMove = (currentTime - lastMoveTime) >= MOVE_REPEAT_DELAY;

            // Arrow key movement (relative to camera orientation)
            if (Input.GetKey(KeyCode.UpArrow) && canMove)
            {
                moveDirection += cameraForward;
                moved = true;
            }
            if (Input.GetKey(KeyCode.DownArrow) && canMove)
            {
                moveDirection -= cameraForward;
                moved = true;
            }
            if (Input.GetKey(KeyCode.LeftArrow) && canMove)
            {
                moveDirection -= cameraRight;
                moved = true;
            }
            if (Input.GetKey(KeyCode.RightArrow) && canMove)
            {
                moveDirection += cameraRight;
                moved = true;
            }

            if (moved && moveDirection != Vector3.zero)
            {
                moveDirection.Normalize();
                MoveCursor(moveDirection * MOVE_STEP);
                lastMoveTime = currentTime;
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressUINavigation = true;
                return true;
            }

            // Space to scan current position
            if (Input.GetKeyDown(KeyCode.Space))
            {
                AnnounceAtCursor(detailed: true);
                return true;
            }

            // Enter to interact with nearest object
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                InteractAtCursor();
                return true;
            }

            // Home to return cursor to party position
            if (Input.GetKeyDown(KeyCode.Home))
            {
                ReturnToParty();
                return true;
            }

            // F to toggle camera follow
            if (Input.GetKeyDown(KeyCode.F))
            {
                cameraFollowsCursor = !cameraFollowsCursor;
                string status = cameraFollowsCursor ? "Camera follows cursor" : "Camera stationary";
                ScreenReaderManager.SpeakInterrupt(status);
                return true;
            }

            // Suppress game input while cursor is active
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
                Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
            {
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressUINavigation = true;
            }

            return false;
        }

        public void OnActivated()
        {
            MelonLogger.Msg("[MapCursorState] Activated");
        }

        public void OnDeactivated()
        {
            MelonLogger.Msg("[MapCursorState] Deactivated");
        }

        private void ToggleCursorMode()
        {
            cursorActive = !cursorActive;

            if (cursorActive)
            {
                // Initialize cursor at current party position
                InitializeCursorPosition();
                ScreenReaderManager.SpeakInterrupt("Map cursor on. Use arrow keys to move, Space to scan, Enter to interact, Home to return to party, M to exit.");
                MelonLogger.Msg("[MapCursorState] Cursor mode enabled");
            }
            else
            {
                ScreenReaderManager.SpeakInterrupt("Map cursor off");
                MelonLogger.Msg("[MapCursorState] Cursor mode disabled");
            }
        }

        private void InitializeCursorPosition()
        {
            // Start at selected PC or first party member position
            PC pc = null;

            if (MonoBehaviourSingleton<InputManager>.HasInstance())
            {
                pc = MonoBehaviourSingleton<InputManager>.GetInstance().GetFirstSelectedPlayer();
            }

            if (pc == null && MonoBehaviourSingleton<Game>.HasInstance())
            {
                var party = MonoBehaviourSingleton<Game>.GetInstance().party;
                if (party != null && party.Count > 0)
                {
                    pc = party[0];
                }
            }

            if (pc != null)
            {
                cursorPosition = pc.transform.position;
            }
            else
            {
                // Fallback to camera position projected to ground
                if (Camera.main != null)
                {
                    cursorPosition = Camera.main.transform.position;
                    cursorPosition.y = 0;
                }
            }

            // Snap to ground
            SnapToGround();
        }

        private void MoveCursor(Vector3 delta)
        {
            cursorPosition += delta;

            // Snap to ground level
            SnapToGround();

            // Clamp to level bounds
            ClampToLevelBounds();

            // Move camera if following
            if (cameraFollowsCursor)
            {
                SnapCameraToCursor();
            }

            // Announce what's at the new position
            AnnounceAtCursor(detailed: false);
        }

        private void SnapToGround()
        {
            // Raycast down from above cursor position to find ground
            Vector3 rayOrigin = cursorPosition + Vector3.up * 50f;
            RaycastHit hit;

            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 100f, LAYER_MASK_GROUND))
            {
                cursorPosition.y = hit.point.y;
            }
        }

        private void ClampToLevelBounds()
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

            var cameraController = MonoBehaviourSingleton<Game>.GetInstance().cameraController;
            if (cameraController == null || cameraController.levelInfo == null) return;

            // Use level bounds to clamp position
            var bounds = cameraController.levelInfo.bounds;
            if (bounds != null)
            {
                bounds.Clamp(ref cursorPosition);
            }
        }

        private void SnapCameraToCursor()
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

            var cameraController = MonoBehaviourSingleton<Game>.GetInstance().cameraController;
            if (cameraController == null) return;

            // Use the game's snap method for smooth camera movement
            cameraController.Snap(cursorPosition, instant: false, resetPreviousZoom: false,
                charSnap: false, forceCharSnap: false, noSFX: true);
        }

        private void ReturnToParty()
        {
            InitializeCursorPosition();
            SnapCameraToCursor();
            ScreenReaderManager.SpeakInterrupt("Returned to party");
            AnnounceAtCursor(detailed: false);
        }

        private void AnnounceAtCursor(bool detailed)
        {
            List<string> announcements = new List<string>();

            // Find nearby interactables
            var nearbyInteractables = FindNearbyInteractables();

            if (nearbyInteractables.Count > 0)
            {
                // Announce closest interactable
                var closest = nearbyInteractables[0];
                float distance = Vector3.Distance(cursorPosition, closest.transform.position);

                string name = GetInteractableName(closest);
                announcements.Add(name);

                if (detailed)
                {
                    announcements.Add($"{distance:F1} meters");

                    // Add more interactables if detailed
                    if (nearbyInteractables.Count > 1)
                    {
                        announcements.Add($"{nearbyInteractables.Count - 1} more nearby");
                    }
                }
            }
            else
            {
                announcements.Add("Nothing nearby");
            }

            // Find nearby mobs/enemies
            var nearbyMobs = FindNearbyMobs();
            if (nearbyMobs.Count > 0)
            {
                int enemies = 0;
                int npcs = 0;

                foreach (var mob in nearbyMobs)
                {
                    if (mob is PC) continue; // Skip party members

                    if (mob is NPC npc)
                    {
                        // Check if NPC is hostile by faction
                        if (npc.npcTemplate != null && npc.npcTemplate.faction == Faction.Bad)
                            enemies++;
                        else
                            npcs++;
                    }
                    else
                    {
                        enemies++;
                    }
                }

                if (enemies > 0)
                {
                    announcements.Add($"{enemies} {(enemies == 1 ? "enemy" : "enemies")}");
                }
                if (npcs > 0)
                {
                    announcements.Add($"{npcs} {(npcs == 1 ? "NPC" : "NPCs")}");
                }
            }

            // Announce distance from party
            if (detailed)
            {
                PC pc = GetPartyLeader();
                if (pc != null)
                {
                    float distFromParty = Vector3.Distance(cursorPosition, pc.transform.position);
                    string direction = DirectionHelper.GetDirectionDescription(pc.transform.position, cursorPosition);
                    announcements.Add($"{distFromParty:F0} meters {direction} from party");
                }
            }

            string announcement = string.Join(", ", announcements.ToArray());
            ScreenReaderManager.SpeakInterrupt(announcement);
        }

        private List<InteractableNexus> FindNearbyInteractables()
        {
            List<InteractableNexus> nearby = new List<InteractableNexus>();

            foreach (var interactable in InteractableNexus.interactables)
            {
                if (interactable == null || !interactable.isVisible) continue;
                if (interactable.isPC) continue; // Skip party members

                float distance = Vector3.Distance(cursorPosition, interactable.transform.position);
                if (distance <= SCAN_RADIUS)
                {
                    nearby.Add(interactable);
                }
            }

            // Sort by distance
            nearby.Sort((a, b) =>
            {
                float distA = Vector3.Distance(cursorPosition, a.transform.position);
                float distB = Vector3.Distance(cursorPosition, b.transform.position);
                return distA.CompareTo(distB);
            });

            return nearby;
        }

        private List<Mob> FindNearbyMobs()
        {
            List<Mob> nearby = new List<Mob>();

            // Find all mobs in scene
            Mob[] allMobs = UnityEngine.Object.FindObjectsOfType<Mob>();

            foreach (var mob in allMobs)
            {
                if (mob == null || mob.gameObject == null) continue;
                if (!mob.gameObject.activeInHierarchy) continue;

                float distance = Vector3.Distance(cursorPosition, mob.transform.position);
                if (distance <= SCAN_RADIUS)
                {
                    nearby.Add(mob);
                }
            }

            return nearby;
        }

        private string GetInteractableName(InteractableNexus interactable)
        {
            if (interactable == null) return "Unknown";

            // Try to get name from Drama
            if (interactable.drama != null)
            {
                var mob = interactable.drama.GetMob();
                if (mob != null && mob is PC pc && pc.pcTemplate != null)
                {
                    return UITextExtractor.CleanText(Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty));
                }

                // Try drama name
                string dramaName = interactable.drama.name;
                if (!string.IsNullOrEmpty(dramaName))
                {
                    return UITextExtractor.CleanText(dramaName);
                }
            }

            // Try to get from SceneLoad (exits/doors)
            var sceneLoad = interactable.GetComponent<SceneLoad>();
            if (sceneLoad != null)
            {
                return "Exit";
            }

            // Try SkillObject_Examine
            if (interactable.skobExamine != null)
            {
                return "Examinable object";
            }

            // Fallback to GameObject name
            string goName = interactable.gameObject.name;
            if (!string.IsNullOrEmpty(goName))
            {
                // Clean up common prefixes/suffixes
                goName = goName.Replace("_", " ").Replace("(Clone)", "").Trim();
                return goName;
            }

            return "Object";
        }

        private void InteractAtCursor()
        {
            var nearbyInteractables = FindNearbyInteractables();

            if (nearbyInteractables.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("Nothing to interact with");
                return;
            }

            // Select the closest interactable
            var target = nearbyInteractables[0];

            // Set as selected interactable
            if (MonoBehaviourSingleton<InputManager>.HasInstance())
            {
                MonoBehaviourSingleton<InputManager>.GetInstance().selectedInteractable = target;
            }

            string name = GetInteractableName(target);
            ScreenReaderManager.SpeakInterrupt($"Selected: {name}");

            // Optionally trigger default action
            // This would need more investigation of the game's interaction system
        }

        private PC GetPartyLeader()
        {
            if (MonoBehaviourSingleton<InputManager>.HasInstance())
            {
                var pc = MonoBehaviourSingleton<InputManager>.GetInstance().GetFirstSelectedPlayer();
                if (pc != null) return pc;
            }

            if (MonoBehaviourSingleton<Game>.HasInstance())
            {
                var party = MonoBehaviourSingleton<Game>.GetInstance().party;
                if (party != null && party.Count > 0)
                {
                    return party[0];
                }
            }

            return null;
        }
    }
}
