using System.Collections.Generic;

namespace Reclamation.Simulation
{
    // ── BlockRotation ────────────────────────────────────────────────────────

    /// <summary>
    /// Four cardinal orientations around the Y-axis (looking down, clockwise).
    /// Replaces Quaternion in the simulation layer — all block rotations snap to
    /// 90° increments. The view layer converts to a real Quaternion for rendering.
    /// </summary>
    public enum BlockRotation
    {
        North = 0,  //   0° — default facing, +Z forward
        East  = 1,  //  90° CW
        South = 2,  // 180°
        West  = 3,  // 270° CW
    }

    // ── Block ────────────────────────────────────────────────────────────────

    /// <summary>
    /// A single placed block in the simulation grid.
    ///
    /// Blocks are plain data. Systems (ConstructSystem, PowerSystem, etc.) read and
    /// mutate these fields — Block itself contains no system logic.
    ///
    /// Network membership IDs (-1 = not assigned) are written by the relevant system
    /// when the block is registered, and cleared when the block is removed.
    /// </summary>
    public class Block
    {
        // ── Identity ─────────────────────────────────────────────────────────

        /// <summary>Unique ID for this placed block instance (auto-assigned by BlockTable).</summary>
        public int Id;

        /// <summary>Shared type data for this block. Never null after placement.</summary>
        public BlockDefinition Definition;

        // ── Spatial ──────────────────────────────────────────────────────────

        /// <summary>
        /// Anchor cell in the world grid. For a 1×1×1 block this is the only cell it
        /// occupies. For multi-cell blocks call OccupiedPositions() to get all cells.
        /// </summary>
        public Int3 GridPosition;

        /// <summary>Orientation around Y. All blocks snap to 90° increments.</summary>
        public BlockRotation Rotation;

        /// <summary>Which chunk owns this block. Derived from GridPosition at placement time.</summary>
        public Int2 ChunkCoord;

        // ── Simulation state ─────────────────────────────────────────────────

        /// <summary>Remaining hit points. Reaches 0 when the block is destroyed.</summary>
        public int Durability;

        /// <summary>Which Construct this block belongs to. Set by ConstructSystem.</summary>
        public int ConstructId;

        // ── Network membership ───────────────────────────────────────────────

        /// <summary>Power network this block belongs to, or -1 if unregistered.</summary>
        public int PowerNetworkId = -1;

        /// <summary>Logistics network this block belongs to, or -1 if unregistered.</summary>
        public int LogisticsNetworkId = -1;

        // ── Functional state (added by later steps) ───────────────────────────
        // public MachineState   MachineState;    // Step 10 — MachineSystem
        // public GeneratorState GeneratorState;  // Step 6  — PowerSystem

        // ── Spatial helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Returns every grid cell occupied by this block, accounting for its Size and
        /// Rotation. A 1×1×1 block returns a single-element list. A 2×1×1 block facing
        /// East returns two cells.
        ///
        /// Allocates a new List each call — use only during placement / removal, not
        /// inside a per-tick hot loop.
        /// </summary>
        public List<Int3> OccupiedPositions()
        {
            var positions = new List<Int3>(Definition.Size.X * Definition.Size.Y * Definition.Size.Z);
            for (int x = 0; x < Definition.Size.X; x++)
            for (int y = 0; y < Definition.Size.Y; y++)
            for (int z = 0; z < Definition.Size.Z; z++)
                positions.Add(GridPosition + RotateLocal(new Int3(x, y, z), Rotation));
            return positions;
        }

        /// <summary>
        /// Rotates a local cell offset (relative to the block's anchor) by the given
        /// rotation around the Y-axis. Used when computing which world cells a
        /// multi-cell block occupies.
        ///
        /// Rotation is clockwise when viewed from above (+Y looking down):
        ///   North (0°)  : (x, y, z) → ( x,  y,  z)
        ///   East  (90°) : (x, y, z) → ( z,  y, -x)
        ///   South (180°): (x, y, z) → (-x,  y, -z)
        ///   West  (270°): (x, y, z) → (-z,  y,  x)
        /// </summary>
        public static Int3 RotateLocal(Int3 local, BlockRotation rotation) => rotation switch
        {
            BlockRotation.North => new Int3( local.X,  local.Y,  local.Z),
            BlockRotation.East  => new Int3( local.Z,  local.Y, -local.X),
            BlockRotation.South => new Int3(-local.X,  local.Y, -local.Z),
            BlockRotation.West  => new Int3(-local.Z,  local.Y,  local.X),
            _                   => local,
        };
    }
}
