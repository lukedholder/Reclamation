namespace Reclamation.Simulation
{
    /// <summary>
    /// Central shared state passed to every system on each tick.
    /// Systems read from and write to this object; they never hold private copies.
    /// Tables are added here as each system is implemented (see SDD §11 Implementation Order).
    /// </summary>
    public class SimulationState
    {
        // ── Tick counter ──────────────────────────────────────────────────────
        /// <summary>Incremented every simulation tick (20 Hz). Never reset.</summary>
        public ulong Tick;

        // ── Core tables ───────────────────────────────────────────────────────
        /// <summary>All placed blocks. Step 2.</summary>
        public BlockTable Blocks = new();

        /// <summary>All live constructs. Step 3.</summary>
        public ConstructTable Constructs = new();

        // ChunkTable      Chunks      — Step 8
        // PowerNetworkTable           — Step 6
        // LogisticsNetworkTable       — Step 9
        // EnemyTable                  — Step 11
    }
}