using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    /// <summary>
    /// Result of a "could this ranger walk there" probe.
    /// </summary>
    public struct PathProbe
    {
        /// <summary>
        /// False when there is no walkable ground within reach of the target at all — a
        /// rooftop, a ledge across a gap, the middle of deep water. Nothing to walk to.
        /// </summary>
        public bool ApproachFound;

        /// <summary>
        /// The spot a ranger would actually stand to reach the target: the target itself
        /// when it is walkable, otherwise the nearest walkable ground beside it.
        /// Meaningful only when ApproachFound.
        /// </summary>
        public Vector3 Approach;

        /// <summary>
        /// False when no path query could be run at all (no agent on the navmesh and the
        /// positional fallback failed too). Callers must stay silent rather than report a
        /// verdict — "not answerable" is not the same as "not reachable".
        /// </summary>
        public bool Queried;

        /// <summary>True when the mover can get to the target.</summary>
        public bool Complete;

        /// <summary>True when the computed path produced at least one corner to inspect.</summary>
        public bool HasFrontier;

        /// <summary>
        /// Furthest point along the route the mover could actually reach. When the route is
        /// cut this is where it was cut, so it is the place to look for whatever is blocking.
        /// Meaningful only when HasFrontier.
        /// </summary>
        public Vector3 Frontier;

        // --- Diagnostics, for the debug log ---

        /// <summary>Raw status Unity reported, before the arrival-tolerance allowance.</summary>
        public NavMeshPathStatus Status;

        /// <summary>True when the verdict came from the mover's own agent, false from the positional fallback.</summary>
        public bool UsedAgentQuery;

        /// <summary>Distance from the target to the approach spot. Zero when the target is itself walkable.</summary>
        public float SampleOffset;

        /// <summary>Distance from the furthest reached point to the target. Large means genuinely cut off.</summary>
        public float FrontierGap;

        /// <summary>Corner count of the last computed path segment.</summary>
        public int CornerCount;

        /// <summary>How many times the query had to be resumed past an exhausted node pool.</summary>
        public int Hops;

        /// <summary>
        /// True when the route was still advancing when the hop budget ran out, so neither
        /// "reachable" nor "no route" is known to be true. Callers must stay silent.
        /// </summary>
        public bool Inconclusive;

        /// <summary>
        /// Where a route walked backwards from the target gives up. On a local blockage this
        /// lands on the far side of the very thing that stopped the forward route, which
        /// makes it the second place worth searching for a shut door.
        /// Meaningful only when HasReverseFrontier.
        /// </summary>
        public Vector3 ReverseFrontier;

        /// <summary>Whether the backwards route produced anything.</summary>
        public bool HasReverseFrontier;

        /// <summary>
        /// How far apart the two frontiers are. Small means both routes died against the
        /// same obstacle. Large means they died against nothing — they are simply the edges
        /// of two pieces of navmesh with no connection between them.
        /// </summary>
        public float RegionGap;

        /// <summary>
        /// True when the target sits on a piece of navmesh with no walking connection to the
        /// mover at all, rather than behind something that blocks an otherwise-open route.
        /// </summary>
        public bool SeparateRegion;

        /// <summary>Navmesh area mask the queries ran with. Pass it back to <see cref="Connected"/>.</summary>
        public int AreaMask;
    }

    /// <summary>
    /// "Can this ranger actually walk to that tile?" for the exploration tile cursor.
    ///
    /// Mirrors the two tests the game itself runs, so the answer matches what happens on a
    /// real move order:
    ///
    /// 1. <c>NavMesh.SamplePosition</c> within 10 m on the Walkable area, then reject the
    ///    destination if the snapped point is more than 0.8 m away in XZ. That is exactly
    ///    the check in InputManager.Update's non-combat click branch which swaps the move
    ///    cursor for the blocked cursor.
    /// 2. <c>NavMeshAgent.CalculatePath</c> and require <c>PathComplete</c> — the same test
    ///    InputManager runs on the gamepad move path before committing to a destination.
    ///
    /// Closed doors really do break navmesh connectivity, so an incomplete path is a
    /// trustworthy "something is shut in the way" rather than a pathfinder quirk:
    /// InteractableObject.Deactivate calls SetOffMeshLinksActive(false), which deactivates
    /// the door's OffMeshLink (navmesh area "Door") and re-enables its NavMeshObstacles;
    /// Activate, Forced and Exploded all call SetOffMeshLinksActive() to open it back up.
    /// </summary>
    public static class PathReachabilityHelper
    {
        // How far out to look for walkable ground. InputManager's own number for a move click.
        private const float SampleRadius = 10f;

        // How far from the target that ground may be and still count as reaching it.
        //
        // The question being answered is "can a ranger get to this thing", not "can one
        // stand on this exact tile" — and the things worth pointing the cursor at (a
        // container, a corpse, a statue, a toaster) all sit on tiles you walk up beside
        // rather than onto. So this is the game's own interact range: AIBehaviour_PC's
        // handler for a use command calls AddMoveIfTooFarAway(destination, 3f), i.e. it
        // walks the ranger to within 3 m and considers them arrived. Measured in 3D, not
        // XZ, so the ground under a second-storey tile cannot masquerade as an approach to
        // it — the 10 m search would otherwise happily snap through a floor.
        private const float ReachTolerance = 3f;

        // How close the end of a path has to land to the target before we call it arrival.
        //
        // Unity reports PathPartial for more than "the way is blocked". A destination that
        // sits against a carved NavMeshObstacle — which is exactly where the interesting
        // things are, since containers, desks and corpses all carve one — often resolves to
        // a polygon the agent cannot quite occupy, so the path terminates a few centimetres
        // short and comes back Partial even though the ranger walks right up to it. Treating
        // that as "no route" is wrong, and it is the common case when jumping the cursor to
        // a scanner hit. So a path that gets this close counts as arriving.
        //
        // It stays small on purpose: a door's leaf plus the agent's own radius always leaves
        // a bigger gap than this, so a target genuinely shut behind one still reads as
        // blocked. This is the tolerance on the path's end point, not on the destination —
        // approaching from beside the target is handled by ReachTolerance above.
        private const float ArrivalTolerance = 0.8f;

        // Unity's navmesh query runs off a fixed node pool that is not configurable on this
        // engine version. A long or geometry-dense route exhausts it and comes back Partial
        // even when a complete route exists, so a single CalculatePath is not a usable
        // reachability test at range: measured in-game, a straight run across open desert
        // completed at 166 m while a cluttered route gave up after 94 m. The fix is to
        // resume the query from where it ran out and keep going, which is bounded work per
        // hop. Eight hops covers the largest levels several times over.
        private const int MaxHops = 8;

        // A hop has to close at least this much of the remaining distance to count as
        // progress. A hop that does not is the signal we want: the route is genuinely cut
        // there, and that point is where whatever is blocking sits.
        private const float MinHopProgress = 1f;

        // How far apart the forward and backward frontiers have to be before the two ends
        // count as separate pieces of navmesh rather than two sides of one obstacle. A shut
        // door leaves them a metre or two apart; the levels put their sub-areas hundreds of
        // metres apart, so anything in between is comfortably decided.
        private const float SeparateRegionThreshold = 10f;

        // Reused between probes — NavMeshPath wraps a native allocation, and the cursor
        // only ever probes one tile at a time.
        private static NavMeshPath scratchPath;

        /// <summary>
        /// Probes whether <paramref name="mover"/> can walk to <paramref name="target"/>.
        /// Returns a default (not standable, not queried) probe when there is no mover.
        /// </summary>
        public static PathProbe Probe(Mob mover, Vector3 target)
        {
            PathProbe probe = new PathProbe();
            if (mover == null) return probe;

            int walkableOnly = 1 << InputManager.navMeshLayerIndex_Default;

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(target, out hit, SampleRadius, walkableOnly))
                return probe;

            probe.SampleOffset = Vector3.Distance(hit.position, target);
            if (probe.SampleOffset > ReachTolerance)
                return probe;

            probe.ApproachFound = true;
            probe.Approach = hit.position;

            if (scratchPath == null) scratchPath = new NavMeshPath();

            // The agent's own walkableMask covers the Door and Ladder areas as well as
            // Walkable, so routes through open doors and up ladders count — as they should.
            // CalculatePath returns false without touching the path when the agent is not
            // on the navmesh (mid-cutscene, on a ladder, being warped), and scratchPath is
            // reused, so a false return must never be read as a result: fall back to a
            // positional query instead, and if that fails too say so rather than guess.
            NavMeshAgent agent = mover.navMeshAgent;
            bool queried = false;

            if (agent != null && agent.enabled)
            {
                queried = agent.CalculatePath(probe.Approach, scratchPath);
                probe.UsedAgentQuery = queried;
            }

            if (!queried)
            {
                queried = NavMesh.CalculatePath(mover.transform.position, probe.Approach, -1, scratchPath);
                probe.UsedAgentQuery = false;
            }

            if (!queried) return probe;

            probe.Queried = true;
            probe.Status = scratchPath.status;
            probe.CornerCount = ReadFrontier(ref probe, probe.Approach);

            // Subsequent hops start from the frontier rather than the mover, so they cannot
            // go through the agent. Borrow the agent's own area mask so open doors and
            // ladders still count as routes; -1 (every area) only when there is no agent.
            int hopMask = (agent != null) ? agent.areaMask : -1;
            probe.AreaMask = hopMask;

            while (probe.Status != NavMeshPathStatus.PathComplete
                && probe.HasFrontier
                && probe.FrontierGap > ArrivalTolerance
                && probe.Hops < MaxHops)
            {
                Vector3 resumeFrom = probe.Frontier;
                float gapBefore = probe.FrontierGap;

                if (!NavMesh.CalculatePath(resumeFrom, probe.Approach, hopMask, scratchPath))
                    break;

                probe.Hops++;

                PathProbe hop = new PathProbe();
                int hopCorners = ReadFrontier(ref hop, probe.Approach);
                if (!hop.HasFrontier) break;

                // No meaningful progress means the pool was not the problem — the route is
                // cut here. Keep the frontier where it stopped, which is what the caller
                // searches for a shut door.
                if (hop.FrontierGap > gapBefore - MinHopProgress)
                    break;

                probe.Frontier = hop.Frontier;
                probe.FrontierGap = hop.FrontierGap;
                probe.CornerCount = hopCorners;
                probe.Status = scratchPath.status;
            }

            probe.Complete = probe.Status == NavMeshPathStatus.PathComplete
                || (probe.HasFrontier && probe.FrontierGap <= ArrivalTolerance);

            // Ran out of hops while still closing on the target: the route may well exist,
            // we just stopped looking. Neither verdict is earned.
            probe.Inconclusive = !probe.Complete && probe.Hops >= MaxHops;

            // Blocked. Walk the route backwards from the target to find where it gives up
            // from that side. Connectivity is symmetric, so this cannot change the verdict —
            // it locates the other end of the break, which is what tells local blockage
            // ("both routes died against the same door") apart from two pieces of navmesh
            // that were never joined ("the ends are 200 m apart, there is nothing between
            // them to blame"). It also gives the caller a second place to look for the door.
            if (!probe.Complete && !probe.Inconclusive && probe.HasFrontier
                && NavMesh.CalculatePath(probe.Approach, mover.transform.position, hopMask, scratchPath))
            {
                PathProbe reverse = new PathProbe();
                ReadFrontier(ref reverse, mover.transform.position);
                if (reverse.HasFrontier)
                {
                    probe.HasReverseFrontier = true;
                    probe.ReverseFrontier = reverse.Frontier;
                    probe.RegionGap = Vector3.Distance(probe.Frontier, reverse.Frontier);
                    probe.SeparateRegion = probe.RegionGap > SeparateRegionThreshold;
                }
            }

            return probe;
        }

        /// <summary>
        /// Reads the current scratchPath's end point into <paramref name="probe"/> and
        /// returns its corner count.
        /// </summary>
        private static int ReadFrontier(ref PathProbe probe, Vector3 destination)
        {
            Vector3[] corners = scratchPath.corners;
            if (corners == null || corners.Length == 0)
            {
                probe.HasFrontier = false;
                probe.FrontierGap = float.PositiveInfinity;
                return 0;
            }

            probe.HasFrontier = true;
            probe.Frontier = corners[corners.Length - 1];
            probe.FrontierGap = Vector3.Distance(probe.Frontier, destination);
            return corners.Length;
        }

        /// <summary>
        /// True when a walking route exists between two world positions. Used to ask "does
        /// this teleporter drop the party in the same pocket of navmesh as the thing they
        /// are pointing at" — a question the forward probe cannot answer, because both ends
        /// of that route are on the far side of the break.
        ///
        /// Single query, no hop chaining: the caller tries several candidates and this has
        /// to stay cheap. Candidates are ordered nearest-destination-first so the query that
        /// matters is a short one.
        /// </summary>
        public static bool Connected(Vector3 from, Vector3 to, int areaMask)
        {
            NavMeshHit fromHit;
            if (!NavMesh.SamplePosition(from, out fromHit, SampleRadius, areaMask))
                return false;

            if (scratchPath == null) scratchPath = new NavMeshPath();

            if (!NavMesh.CalculatePath(fromHit.position, to, areaMask, scratchPath))
                return false;

            return scratchPath.status == NavMeshPathStatus.PathComplete;
        }

    }
}
