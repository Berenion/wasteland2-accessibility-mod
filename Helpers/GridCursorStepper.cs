using System;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    /// <summary>
    /// Shared grid-cursor stepping for the combat (CombatState) and exploration
    /// (MapCursorState) tile cursors. Both walk the same A* node grid identically;
    /// the only differences are passed in:
    ///   - getNode: which map to query (combat uses combatMap, exploration uses fullMap).
    ///   - describeOffGrid: how to describe a blocked / off-grid tile.
    ///   - snapSingleStepToNode: on a single-tile move, combat snaps the grid id to the
    ///     resolved node's canonical id; exploration keeps the raw stepped id.
    /// The tile *announcement* deliberately stays in each state — it is genuinely
    /// context-specific (combat reports AP / movement cost / line-of-sight, exploration
    /// reports interactables and party-relative navigation).
    /// </summary>
    internal static class GridCursorStepper
    {
        public struct StepResult
        {
            public bool Moved;          // false => fully blocked; caller speaks BlockReason and bails
            public bool SingleStep;     // true => single-tile move (no count prefix)
            public Vector3 GridId;      // new cursor grid id (only meaningful when Moved)
            public Vector3 Position;    // new cursor world position (only meaningful when Moved)
            public int ActualSteps;     // tiles actually advanced
            public string BlockReason;  // why the move stopped early / was blocked (may be null)
        }

        /// <param name="getNode">Resolves a grid id to its walkable node, or null if none.</param>
        /// <param name="describeOffGrid">(gridId, worldPos) => reason a tile is blocked/off-grid.</param>
        /// <param name="snapSingleStepToNode">Single-step only: snap the grid id to the node's id.</param>
        /// <param name="blockAtObstacles">
        /// When true, a single step onto a tile with no walkable node is refused (the cursor
        /// stops at walls/terrain instead of passing through to inspect them). Multi-tile
        /// moves already stop before the first non-walkable tile regardless.
        /// </param>
        public static StepResult Step(
            Vector3 cursorGridId,
            Vector3 cursorPosition,
            int directionIndex,
            int tilesToMove,
            Func<Vector3, CombatAStarNode> getNode,
            Func<Vector3, Vector3, string> describeOffGrid,
            bool snapSingleStepToNode,
            bool blockAtObstacles = false)
        {
            Vector3 direction = CardinalDirections.Vectors[directionIndex];
            var result = new StepResult();

            // Single-step moves onto the target tile even when it has no walkable node
            // (terrain, cover, wall, or outside the area), so the user can inspect a
            // blocked tile with the cursor — unless blockAtObstacles confines the cursor
            // to walkable ground.
            if (tilesToMove <= 1)
            {
                Vector3 stepGridId = new Vector3(
                    cursorGridId.x + direction.x,
                    cursorGridId.y,
                    cursorGridId.z + direction.z);

                CombatAStarNode stepNode = getNode(stepGridId);
                if (stepNode != null)
                {
                    result.GridId = snapSingleStepToNode ? stepNode.id : stepGridId;
                    result.Position = stepNode.position;
                }
                else
                {
                    Vector3 blockedWorldPos = new Vector3(
                        stepGridId.x * TileCoordinateSystem.SquareSize,
                        cursorPosition.y,
                        stepGridId.z * TileCoordinateSystem.SquareSize);

                    if (blockAtObstacles)
                    {
                        // Confined mode: refuse the step and report why.
                        result.Moved = false;
                        result.BlockReason = describeOffGrid(stepGridId, blockedWorldPos);
                        return result;
                    }

                    result.GridId = stepGridId;
                    result.Position = blockedWorldPos;
                }

                result.Moved = true;
                result.SingleStep = true;
                result.ActualSteps = 1;
                return result;
            }

            // Multi-tile move: walk tile-by-tile and stop before the first non-walkable
            // tile so the cursor never flies through walls.
            Vector3 currentGridId = cursorGridId;
            Vector3 currentPosition = cursorPosition;
            int actualSteps = 0;
            string blockReason = null;

            for (int i = 0; i < tilesToMove; i++)
            {
                Vector3 newGridId = new Vector3(
                    currentGridId.x + direction.x,
                    currentGridId.y,
                    currentGridId.z + direction.z);

                CombatAStarNode node = getNode(newGridId);
                if (node != null)
                {
                    currentGridId = node.id;
                    currentPosition = node.position;
                    actualSteps++;
                    continue;
                }

                Vector3 blockedWorldPos = new Vector3(
                    newGridId.x * TileCoordinateSystem.SquareSize,
                    currentPosition.y,
                    newGridId.z * TileCoordinateSystem.SquareSize);
                blockReason = describeOffGrid(newGridId, blockedWorldPos);
                break;
            }

            result.ActualSteps = actualSteps;
            result.BlockReason = blockReason;

            if (actualSteps == 0)
            {
                result.Moved = false;
                return result;
            }

            result.Moved = true;
            result.SingleStep = false;
            result.GridId = currentGridId;
            result.Position = currentPosition;
            return result;
        }

        /// <summary>Builds the "N tile(s)[, reason]" prefix announced for multi-tile moves.</summary>
        public static string BuildMovePrefix(int actualSteps, string blockReason)
        {
            string prefix = actualSteps + (actualSteps == 1 ? " tile" : " tiles");
            if (!string.IsNullOrEmpty(blockReason))
                prefix += ", " + blockReason;
            return prefix;
        }
    }
}
