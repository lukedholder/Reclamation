// Runtime state for a functional machine block (Assembler, Miner, Furnace, etc.).
// One instance is created per Block when the block is registered with MachineSystem.
// MachineSystem reads and writes this each tick to advance the production cycle.
//
// Separation from BlockDefinition is intentional: Params (on BlockDefinition) are
// shared and immutable; State is per-instance and changes every tick.

public class MachineState
{
    // Current operating mode — drives power draw calculation and whether to advance the cycle.
    public OperationMode Mode = OperationMode.Idle;

    // The recipe currently selected for this machine. Null means no recipe / Idle state.
    // Clearing the recipe ejects internal buffer items to an adjacent belt or the player.
    public Recipe ActiveRecipe;

    // Fraction of the current production cycle completed [0, 1].
    // Advances by (deltaTime / Recipe.CycleTime) × OperatingRate each tick.
    // Resets to 0 after outputs are deposited.
    public float CycleProgress;

    // Throttle multiplier applied by PowerSystem during a network power deficit [0, 1].
    // 1.0 = full speed. 0.0 = completely stopped. Written by PowerSystem; read by MachineSystem.
    public float OperatingRate = 1f;

    // Items waiting to be consumed by the next production cycle.
    // LogisticsSystem fills this from adjacent belts via inserters.
    public ItemBuffer InputBuffer  = new ItemBuffer();

    // Items produced and waiting for a logistics connection to collect.
    // LogisticsSystem drains this to adjacent belts via inserters.
    public ItemBuffer OutputBuffer = new ItemBuffer();
}
