// An immutable production recipe: the set of items consumed and produced over one cycle.
// One Recipe instance is shared across all machines currently executing it — never mutate it.
// MachineType restricts which block types can execute this recipe
// (e.g. a Fabricator recipe cannot be run on an Assembler Mk1).

using System.Collections.Generic;

public class Recipe
{
    public string          Id;           // unique recipe identifier (e.g. "iron_gear_wheel")
    public string          DisplayName;
    public FunctionalType  MachineType;  // block type that executes this (Assembler, Fabricator, etc.)
    public float           CycleTime;    // seconds to complete one production cycle at 1.0× speed

    // Items consumed from the machine's InputBuffer each cycle.
    public List<ItemStack> Inputs  = new List<ItemStack>();

    // Items deposited into the machine's OutputBuffer each cycle.
    public List<ItemStack> Outputs = new List<ItemStack>();
}
