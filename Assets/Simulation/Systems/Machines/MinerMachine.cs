// A machine that extracts resources from a node — no input items required.
// Mirrors the Miner.cs pattern from the previous SpaceCrusade codebase:
// SetResourceNode() creates a synthetic recipe so the base Tick() loop can run
// unchanged without knowing that there are no real inputs.
//
// HasInputsForOneCycle() is overridden to always return true (the "input" is the
// resource node itself, not buffered items). ConsumeInputs() is a no-op.

using System.Collections.Generic;

public class MinerMachine : BaseMachine
{
    public MinerMachine(Block block) : base(block) { }

    // Called when the miner is placed on a resource deposit.
    // Constructs a synthetic recipe from the node's properties and activates production.
    // itemId        — e.g. "iron_ore"
    // cycleTime     — seconds per extraction cycle (from MinerParams.ExtractRatePerSecond)
    // amountPerCycle— how many items produced each cycle
    public void SetResourceNode(string itemId, float cycleTime, int amountPerCycle)
    {
        var synthetic = new Recipe
        {
            Id          = $"mine_{itemId}",
            DisplayName = $"Mine {itemId}",
            MachineType = FunctionalType.Miner,
            CycleTime   = cycleTime,
            Inputs      = new List<ItemStack>(),                              // miners consume nothing
            Outputs     = new List<ItemStack> { new ItemStack(itemId, amountPerCycle) },
        };

        SetRecipe(synthetic);
    }

    // Miners always have "inputs" — the resource node is infinite for V1.
    // Base Tick() still guards on HasOutputSpace(), so a full output buffer stalls production.
    protected override bool HasInputsForOneCycle() =>
        State.ActiveRecipe != null;

    // Nothing to consume — the resource comes from the node, not the input buffer.
    protected override void ConsumeInputs() { }
}
