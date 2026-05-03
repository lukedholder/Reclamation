namespace Reclamation.Simulation
{
    /// <summary>
    /// Top-level simulation object. Owns the state and all systems.
    /// Created and ticked by GameManager (view layer) via the accumulator pattern.
    ///
    /// All external mutations (placing/removing blocks) go through the public API
    /// on this class so that systems are notified in the correct order.
    /// </summary>
    public class Simulation
    {
        // ── State and systems ─────────────────────────────────────────────────

        private readonly SimulationState _state = new();
        private readonly ConstructSystem _constructs = new();

        // PowerSystem      _power      — Step 6
        // LogisticsSystem  _logistics  — Step 9
        // MachineSystem    _machines   — Step 10
        // EnemySystem      _enemies    — Step 11
        // CombatSystem     _combat     — Step 11
        // ProgressionSystem _progression — Step 13
        // WorldStreamer    _streamer   — Step 8

        public SimulationState State => _state;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public void Initialise()
        {
            _state.Tick = 0;
            EventBus.Clear(); // ensure no stale handlers from a previous session
        }

        /// <summary>
        /// Advances the simulation by one fixed timestep (dt = 1/20 s at 20 Hz).
        /// Called by GameManager via the accumulator pattern.
        /// Systems run in the explicit order defined in SDD §4.2.
        /// </summary>
        public void Tick(float dt)
        {
            _state.Tick++;

            // 1. World streaming  — Step 8
            // 2. Construct membership — reactive only, Tick is no-op
            _constructs.Tick(_state, dt);
            // 3. Power            — Step 6
            // 4. Logistics        — Step 9
            // 5. Fluids           — future
            // 6. Machines         — Step 10
            // 7. Enemies          — Step 11
            // 8. Combat           — Step 11
            // 9. Progression      — Step 13
        }

        // ── Block placement API ───────────────────────────────────────────────

        /// <summary>
        /// Validates and places a block, then updates construct membership.
        /// Returns the new Block on success, or null if placement is invalid
        /// (use BlockTable.CanPlace beforehand to get a specific failure reason).
        /// </summary>
        public Block PlaceBlock(
            BlockDefinition def,
            Int3            position,
            BlockRotation   rotation = BlockRotation.North)
        {
            var block = _state.Blocks.Place(def, position, rotation);
            if (block == null) return null;

            _constructs.OnBlockPlaced(block, _state);
            return block;
        }

        /// <summary>
        /// Removes a block by ID, updating construct membership and firing events.
        /// Returns false if no block with that ID exists.
        /// </summary>
        public bool RemoveBlock(int blockId)
        {
            var block = _state.Blocks.Get(blockId);
            if (block == null) return false;

            // ConstructSystem reads the block's neighbours before it is removed,
            // then seeds the BFS with the block's own ID to treat it as gone.
            _constructs.OnBlockRemoved(block, _state);

            // Now evict it from the position index.
            _state.Blocks.Remove(blockId);
            return true;
        }
    }
}
