using System;
using System.Collections.Generic;

namespace Reclamation.Simulation
{
    /// <summary>
    /// Stores all placed blocks and exposes placement validation.
    ///
    /// Internally keeps two indices:
    ///   _byId       — O(1) lookup by block ID (used by every system)
    ///   _byPosition — O(1) lookup by world cell (used for adjacency and collision checks)
    ///
    /// Multi-cell blocks (Size > 1×1×1) register every occupied cell in _byPosition,
    /// so collision and adjacency checks are always a simple dictionary probe.
    /// </summary>
    public class BlockTable
    {
        // ── Internal state ────────────────────────────────────────────────────

        private readonly Dictionary<int,   Block> _byId       = new();
        private readonly Dictionary<Int3,  int>   _byPosition = new(); // cell → block ID
        private int _nextId = 1;

        // ── Chunk sizing (mirrors WorldStreamer constants) ─────────────────────

        private const int ChunkSize = 32; // cells per side — must match WorldStreamer

        // ── Public queries ────────────────────────────────────────────────────

        /// <summary>Total number of blocks currently placed.</summary>
        public int Count => _byId.Count;

        /// <summary>Returns the block with the given ID, or null if not found.</summary>
        public Block Get(int id) => _byId.TryGetValue(id, out var b) ? b : null;

        /// <summary>Returns the block occupying the given cell, or null if the cell is empty.</summary>
        public Block GetAt(Int3 position) =>
            _byPosition.TryGetValue(position, out var id) ? _byId[id] : null;

        /// <summary>True if any block occupies the given cell.</summary>
        public bool IsOccupied(Int3 position) => _byPosition.ContainsKey(position);

        /// <summary>
        /// True if at least one of the six face-adjacent cells is occupied.
        /// Used internally by CanPlace and also by ConstructSystem.
        /// </summary>
        public bool HasAdjacentBlock(Int3 position)
        {
            foreach (var dir in Int3.CardinalDirections)
                if (_byPosition.ContainsKey(position + dir))
                    return true;
            return false;
        }

        /// <summary>
        /// Iterates every placed block. Safe to read; do not modify the table during enumeration.
        /// </summary>
        public IEnumerable<Block> All => _byId.Values;

        // ── Placement validation ──────────────────────────────────────────────

        /// <summary>
        /// Returns a <see cref="PlacementResult"/> describing whether a block of the given
        /// definition can be placed at <paramref name="position"/> facing <paramref name="rotation"/>.
        ///
        /// Rules (in order):
        ///   1. Every cell the block would occupy must be empty.
        ///   2. If the table is empty, the first block is always valid (no adjacency required).
        ///   3. At least one cell of the block must be face-adjacent to an already-placed block.
        ///
        /// Extra rules declared in <see cref="BlockDefinition.PlacementRules"/> are evaluated
        /// after the base checks — return <see cref="PlacementResult.RuleFailed"/> if any fail.
        /// </summary>
        public PlacementResult CanPlace(
            BlockDefinition def,
            Int3            position,
            BlockRotation   rotation = BlockRotation.North)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            var cells = ComputeCells(position, def.Size, rotation);

            // Rule 1 — no overlap
            foreach (var cell in cells)
                if (_byPosition.ContainsKey(cell))
                    return PlacementResult.Occupied;

            // Rule 2 — first block: always allowed
            if (_byId.Count == 0)
                return PlacementResult.Valid;

            // Rule 3 — at least one face-adjacent occupied cell
            bool adjacent = false;
            foreach (var cell in cells)
            {
                foreach (var dir in Int3.CardinalDirections)
                {
                    if (_byPosition.ContainsKey(cell + dir))
                    {
                        adjacent = true;
                        break;
                    }
                }
                if (adjacent) break;
            }

            if (!adjacent)
                return PlacementResult.NotAdjacent;

            // Future: evaluate PlacementRules tokens here
            // e.g. "requires_foundation", "ground_level", "outdoor_only"

            return PlacementResult.Valid;
        }

        // ── Mutation ──────────────────────────────────────────────────────────

        /// <summary>
        /// Validates and places a block. Returns the new <see cref="Block"/> on success,
        /// or null if <see cref="CanPlace"/> fails.
        ///
        /// The caller (typically ConstructSystem or a command handler) is responsible for
        /// firing <c>BlockPlacedEvent</c> after placement.
        /// </summary>
        public Block Place(
            BlockDefinition def,
            Int3            position,
            BlockRotation   rotation = BlockRotation.North)
        {
            if (CanPlace(def, position, rotation) != PlacementResult.Valid)
                return null;

            var block = new Block
            {
                Id           = _nextId++,
                Definition   = def,
                GridPosition = position,
                Rotation     = rotation,
                Durability   = def.MaxDurability,
                ChunkCoord   = ToChunkCoord(position),
            };

            _byId[block.Id] = block;
            foreach (var cell in block.OccupiedPositions())
                _byPosition[cell] = block.Id;

            return block;
        }

        /// <summary>
        /// Removes a block by ID. Returns true on success.
        ///
        /// The caller is responsible for firing <c>BlockDestroyedEvent</c> and triggering
        /// ConstructSystem to recalculate membership.
        /// </summary>
        public bool Remove(int blockId)
        {
            if (!_byId.TryGetValue(blockId, out var block))
                return false;

            foreach (var cell in block.OccupiedPositions())
                _byPosition.Remove(cell);

            _byId.Remove(blockId);
            return true;
        }

        // ── Utility used by other systems ─────────────────────────────────────

        /// <summary>
        /// Returns true if any block in <paramref name="ids"/> satisfies
        /// <paramref name="predicate"/>. Used by Construct.Reclassify.
        /// </summary>
        public bool AnyInSet(HashSet<int> ids, Func<Block, bool> predicate)
        {
            foreach (var id in ids)
                if (_byId.TryGetValue(id, out var block) && predicate(block))
                    return true;
            return false;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Computes every world cell a block would occupy, given its anchor position,
        /// size, and rotation. Matches the logic in <see cref="Block.OccupiedPositions"/>
        /// but does not require an allocated Block instance.
        /// </summary>
        private static List<Int3> ComputeCells(Int3 position, Int3 size, BlockRotation rotation)
        {
            var cells = new List<Int3>(size.X * size.Y * size.Z);
            for (int x = 0; x < size.X; x++)
            for (int y = 0; y < size.Y; y++)
            for (int z = 0; z < size.Z; z++)
                cells.Add(position + Block.RotateLocal(new Int3(x, y, z), rotation));
            return cells;
        }

        /// <summary>
        /// Converts a world grid position to a chunk coordinate using floor division,
        /// so negative positions map correctly (e.g. cell -1 → chunk -1, not chunk 0).
        /// </summary>
        private static Int2 ToChunkCoord(Int3 position)
        {
            int cx = FloorDiv(position.X, ChunkSize);
            int cz = FloorDiv(position.Z, ChunkSize);
            return new Int2(cx, cz);
        }

        private static int FloorDiv(int a, int b) =>
            a >= 0 ? a / b : (a - b + 1) / b;
    }

    // ── PlacementResult ───────────────────────────────────────────────────────

    /// <summary>
    /// Detailed outcome of a placement validation check.
    /// Commands and UI can use this to give the player specific feedback.
    /// </summary>
    public enum PlacementResult
    {
        /// <summary>All rules passed — the block can be placed.</summary>
        Valid,

        /// <summary>One or more cells the block would occupy are already taken.</summary>
        Occupied,

        /// <summary>
        /// No cell of the block is face-adjacent to an existing block.
        /// (Inapplicable to the very first block placed.)
        /// </summary>
        NotAdjacent,

        /// <summary>A BlockDefinition.PlacementRules token was not satisfied.</summary>
        RuleFailed,
    }
}
