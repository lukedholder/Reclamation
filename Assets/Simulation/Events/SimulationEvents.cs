namespace Reclamation.Simulation
{
    // All event types are readonly structs — value types with no heap allocation.
    // Fields are readonly so handlers cannot mutate the event data.
    // New events are added here as each system is implemented.

    // ── Step 3: Construct membership ─────────────────────────────────────────

    /// <summary>
    /// Fired after a block is placed and assigned to a construct.
    /// View layer: spawn block GameObject, play place sound.
    /// </summary>
    public readonly struct BlockPlacedEvent
    {
        public readonly Block     Block;
        public readonly Construct Construct;
        public BlockPlacedEvent(Block block, Construct construct)
        {
            Block     = block;
            Construct = construct;
        }
    }

    /// <summary>
    /// Fired after a block is removed from the world.
    /// View layer: despawn block GameObject, spawn debris particles.
    /// </summary>
    public readonly struct BlockDestroyedEvent
    {
        public readonly Block     Block;
        public readonly Construct PreviousConstruct;
        public BlockDestroyedEvent(Block block, Construct previousConstruct)
        {
            Block             = block;
            PreviousConstruct = previousConstruct;
        }
    }

    /// <summary>
    /// Fired when removing a block causes a construct to split into two or more
    /// separate connected components.
    /// View layer: update construct name labels, re-evaluate pilotability UI.
    /// </summary>
    public readonly struct ConstructSplitEvent
    {
        public readonly Construct   Original;       // kept first component
        public readonly Construct[] NewConstructs;  // one entry per extra component
        public ConstructSplitEvent(Construct original, Construct[] newConstructs)
        {
            Original      = original;
            NewConstructs = newConstructs;
        }
    }

    /// <summary>
    /// Fired when two or more separate constructs are merged into one by placing
    /// a block that bridges them.
    /// View layer: collapse construct name labels.
    /// </summary>
    public readonly struct ConstructMergedEvent
    {
        public readonly Construct Primary;              // surviving construct
        public readonly int[]     AbsorbedConstructIds; // IDs that no longer exist
        public ConstructMergedEvent(Construct primary, int[] absorbedIds)
        {
            Primary              = primary;
            AbsorbedConstructIds = absorbedIds;
        }
    }

    // ── Step 6: Power ────────────────────────────────────────────────────────

    /// <summary>
    /// Fired each tick a power network cannot meet its demand.
    /// View layer: flash power shortage indicator on HUD.
    /// </summary>
    public readonly struct PowerShortageEvent
    {
        public readonly int   NetworkId;
        public readonly float DeficitKW;
        public PowerShortageEvent(int networkId, float deficitKW)
        {
            NetworkId = networkId;
            DeficitKW = deficitKW;
        }
    }

    // ── Step 9: Progression ──────────────────────────────────────────────────

    /// <summary>
    /// Fired when the player unlocks a new research tier.
    /// View layer: play fanfare, unlock build menu entries.
    /// </summary>
    public readonly struct TierUnlockedEvent
    {
        public readonly int Tier;
        public TierUnlockedEvent(int tier) { Tier = tier; }
    }

    // Step 11 — EnemyKilledEvent added when EnemySystem is implemented.
    // Step  8 — ChunkLoadedEvent / ChunkUnloadedEvent added with WorldStreamer.
}
