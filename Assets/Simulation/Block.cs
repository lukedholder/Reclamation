// A single placed block instance in the world.
// Position is stored as GridPos (integer, construct-local) — not world-space floats.
// World center = constructOrigin + GridPosition * CellSize + Size * CellSize * 0.5

public class Block
{
    // --- Identity ---

    public int Id;
    public BlockDefinition Definition;

    // --- Membership ---

    // -1 if unassigned (should not persist after placement)
    public int ConstructId      = -1;
    public int PowerNetworkId   = -1;
    public int LogisticsNetworkId = -1;

    // --- Position ---

    // Minimum corner of the block in the construct's local grid (bottom-left-back).
    public GridPos GridPosition;

    // 0–3, each step is 90° around Y-axis.
    public int RotationSteps;

    // --- Health ---

    public int Durability;

    // --- Runtime State (null if block doesn't have this function) ---

    public MachineState  MachineState;
    public GeneratorState GeneratorState;
    public BatteryState  BatteryState;
}
