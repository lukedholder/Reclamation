using System.Collections.Generic;

namespace Reclamation.Simulation
{
    /// <summary>
    /// Maintains construct membership (which blocks belong to which construct)
    /// using flood-fill on every placement or removal.
    ///
    /// This system is reactive — its work is triggered by Simulation.PlaceBlock /
    /// RemoveBlock rather than by the per-tick loop. Tick() is a no-op.
    ///
    /// SDD §5.1 for flood-fill algorithm; SDD §11 Step 3 for acceptance criteria.
    /// </summary>
    public class ConstructSystem
    {
        // Called by Simulation.Tick — no continuous work needed here.
        public void Tick(SimulationState state, float dt) { }

        // ── Placement ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called by Simulation immediately after BlockTable.Place succeeds.
        /// Determines which construct(s) the new block touches and either joins,
        /// creates, or merges constructs accordingly.
        ///
        /// Cases:
        ///   0 neighbours  → create new construct for this block.
        ///   1 construct   → add block to that construct.
        ///   2+ constructs → merge all into the first; block bridges them.
        /// </summary>
        public void OnBlockPlaced(Block block, SimulationState state)
        {
            // Collect distinct construct IDs that are face-adjacent to the new block.
            // Use a List with manual dedup (max 6 entries) to stay allocation-light.
            var adjacentIds = new List<int>(6);
            foreach (var cell in block.OccupiedPositions())
            {
                foreach (var dir in Int3.CardinalDirections)
                {
                    var neighbour = state.Blocks.GetAt(cell + dir);
                    if (neighbour == null || neighbour.Id == block.Id) continue;
                    if (neighbour.ConstructId != 0 && !adjacentIds.Contains(neighbour.ConstructId))
                        adjacentIds.Add(neighbour.ConstructId);
                }
            }

            Construct construct;

            if (adjacentIds.Count == 0)
            {
                // ── Case 1: isolated block — start a new construct ───────────
                construct = state.Constructs.Create();
            }
            else if (adjacentIds.Count == 1)
            {
                // ── Case 2: single adjacent construct — just join it ─────────
                construct = state.Constructs.Get(adjacentIds[0]);
            }
            else
            {
                // ── Case 3: block bridges two or more constructs — merge ─────
                construct = state.Constructs.Get(adjacentIds[0]);

                var absorbedIds = new int[adjacentIds.Count - 1];
                for (int i = 1; i < adjacentIds.Count; i++)
                {
                    var other = state.Constructs.Get(adjacentIds[i]);
                    foreach (var id in other.BlockIds)
                    {
                        construct.BlockIds.Add(id);
                        state.Blocks.Get(id).ConstructId = construct.Id;
                    }
                    absorbedIds[i - 1] = other.Id;
                    state.Constructs.Remove(other.Id);
                }

                EventBus.Publish(new ConstructMergedEvent(construct, absorbedIds));
            }

            construct.BlockIds.Add(block.Id);
            block.ConstructId = construct.Id;
            construct.Reclassify(state.Blocks);

            EventBus.Publish(new BlockPlacedEvent(block, construct));
        }

        // ── Removal ───────────────────────────────────────────────────────────

        /// <summary>
        /// Called by Simulation before BlockTable.Remove.
        /// Flood-fills from every face-neighbour of the removed block to discover
        /// whether the construct has split into two or more disconnected components.
        ///
        /// The removed block remains in BlockTable during this call; its ID is
        /// pre-seeded into <c>seen</c> so the flood fill never crosses it — this
        /// is equivalent to removing it from the graph without modifying the table.
        ///
        /// After this method returns, Simulation calls BlockTable.Remove to
        /// clean up the position index.
        /// </summary>
        public void OnBlockRemoved(Block block, SimulationState state)
        {
            var construct = state.Constructs.Get(block.ConstructId);

            // Snapshot neighbours while the block is still registered in BlockTable.
            var neighbours = GetAdjacentBlocks(block, state.Blocks);

            // Remove the block from construct membership immediately.
            construct.BlockIds.Remove(block.Id);

            // ── Trivial cases ────────────────────────────────────────────────

            if (construct.BlockIds.Count == 0)
            {
                // Construct is now empty — delete it.
                state.Constructs.Remove(construct.Id);
                EventBus.Publish(new BlockDestroyedEvent(block, construct));
                return;
            }

            if (neighbours.Count == 0)
            {
                // Block had no neighbours (it was the only block, handled above,
                // or was floating — shouldn't happen in practice).
                construct.Reclassify(state.Blocks);
                EventBus.Publish(new BlockDestroyedEvent(block, construct));
                return;
            }

            // ── Connectivity check via flood-fill ────────────────────────────

            // Seed seen with the removed block's ID so the BFS never crosses it,
            // treating it as already gone even though it's still in BlockTable.
            var seen = new HashSet<int> { block.Id };
            var components = new List<HashSet<int>>();

            foreach (var neighbour in neighbours)
            {
                if (seen.Contains(neighbour.Id)) continue;
                components.Add(FloodFill(neighbour, state.Blocks, seen));
            }

            if (components.Count <= 1)
            {
                // Still one connected component — just reclassify.
                construct.Reclassify(state.Blocks);
                EventBus.Publish(new BlockDestroyedEvent(block, construct));
                return;
            }

            // ── Split ────────────────────────────────────────────────────────

            // First component keeps the original construct (preserves its ID and Name).
            construct.BlockIds = components[0];
            construct.Reclassify(state.Blocks);

            // Remaining components become new constructs.
            var newConstructs = new Construct[components.Count - 1];
            for (int i = 1; i < components.Count; i++)
            {
                var split = state.Constructs.Create();
                split.BlockIds = components[i];
                split.Reclassify(state.Blocks);

                // Repoint every block in the new construct to its new owner.
                foreach (var id in components[i])
                    state.Blocks.Get(id).ConstructId = split.Id;

                newConstructs[i - 1] = split;
            }

            EventBus.Publish(new ConstructSplitEvent(construct, newConstructs));
            EventBus.Publish(new BlockDestroyedEvent(block, construct));
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Returns all blocks that share a face with any cell occupied by
        /// <paramref name="block"/>, excluding the block itself.
        /// Handles multi-cell blocks (Size > 1×1×1) correctly.
        /// </summary>
        private static List<Block> GetAdjacentBlocks(Block block, BlockTable blocks)
        {
            // Use a seen-ID set to deduplicate when the same neighbour is reachable
            // from multiple cells of a large block.
            var seenIds = new HashSet<int> { block.Id };
            var result  = new List<Block>();

            foreach (var cell in block.OccupiedPositions())
            {
                foreach (var dir in Int3.CardinalDirections)
                {
                    var neighbour = blocks.GetAt(cell + dir);
                    if (neighbour != null && seenIds.Add(neighbour.Id))
                        result.Add(neighbour);
                }
            }

            return result;
        }

        /// <summary>
        /// BFS flood-fill from <paramref name="start"/>. Adds every reachable block ID
        /// to <paramref name="seen"/> (shared across all components) and returns the
        /// set of IDs in this component.
        /// </summary>
        private HashSet<int> FloodFill(Block start, BlockTable blocks, HashSet<int> seen)
        {
            var component = new HashSet<int>();
            var queue     = new Queue<Block>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!seen.Add(current.Id)) continue;
                component.Add(current.Id);

                foreach (var neighbour in GetAdjacentBlocks(current, blocks))
                    if (!seen.Contains(neighbour.Id))
                        queue.Enqueue(neighbour);
            }

            return component;
        }
    }
}
