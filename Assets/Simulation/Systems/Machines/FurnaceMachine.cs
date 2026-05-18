// Smelting machine. Converts raw ore/materials into refined outputs.
// Typically 1 input → 1 output, but uses the same base production loop as Assembler.
// Exists as a separate type so furnace-only recipes are rejected by other machine types
// and vice versa.

public class FurnaceMachine : BaseMachine
{
    public FurnaceMachine(Block block) : base(block) { }

    // Future: fuel slot support (coal/wood consumed as energy, not via PowerSystem).
    // Future: temperature-based speed scaling.
}
