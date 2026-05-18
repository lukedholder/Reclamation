// Abstract base for all production machines (Miner, Assembler, Furnace, etc.).
// Pure C# — no Unity dependency.
//
// Follows the template method pattern from the previous SpaceCrusade implementation:
// Tick() drives the shared production loop; subclasses override the virtual hooks
// (HasInputsForOneCycle, ConsumeInputs, ProduceOutputs) to specialise behaviour.
//
// Tick is called by MachineSystem once per simulation step (20 Hz).
// All state lives on Block.MachineState so the view layer can read it without touching
// the machine object.

using System.Collections.Generic;

public abstract class BaseMachine
{
    protected readonly Block       _block;
    protected          MachineState State => _block.MachineState;

    protected BaseMachine(Block block)
    {
        _block = block;
        // Ensure state exists — Simulation.PlaceBlock creates the block without state
        if (block.MachineState == null)
            block.MachineState = new MachineState();
    }

    // Assign a recipe to this machine. Validates that the recipe is for this machine type,
    // then configures the input/output buffer slots to match recipe requirements.
    // Returns false if the recipe is incompatible. Pass null to clear the recipe.
    public virtual bool SetRecipe(Recipe recipe)
    {
        if (recipe == null)
        {
            State.ActiveRecipe  = null;
            State.Mode          = OperationMode.Idle;
            State.CycleProgress = 0f;
            State.InputBuffer.Clear();
            State.OutputBuffer.Clear();
            return true;
        }

        if (recipe.MachineType != _block.Definition.FunctionalType) return false;

        State.ActiveRecipe  = recipe;
        State.CycleProgress = 0f;

        // One buffer slot per recipe input/output, preserving any items already in matching slots
        State.InputBuffer.MaxSlots  = recipe.Inputs.Count;
        State.OutputBuffer.MaxSlots = recipe.Outputs.Count;

        for (int i = 0; i < recipe.Inputs.Count;  i++) State.InputBuffer.ConfigureSlot(i,  recipe.Inputs[i].ItemId);
        for (int i = 0; i < recipe.Outputs.Count; i++) State.OutputBuffer.ConfigureSlot(i, recipe.Outputs[i].ItemId);

        return true;
    }

    // Called once per simulation tick by MachineSystem.
    // tickDelta = fixed simulation step in seconds (1 / 20 Hz = 0.05s).
    public void Tick(float tickDelta)
    {
        if (State.ActiveRecipe == null)    { State.Mode = OperationMode.Idle;    return; }
        if (State.OperatingRate <= 0f)     { State.Mode = OperationMode.NoPower; return; }
        if (!HasInputsForOneCycle()
         || !HasOutputSpace())             { State.Mode = OperationMode.Waiting; return; }

        State.Mode = OperationMode.Operating;

        // Advance cycle — carry over any overshoot so high-speed machines don't lose time
        State.CycleProgress += tickDelta / State.ActiveRecipe.CycleTime * State.OperatingRate;

        if (State.CycleProgress >= 1f)
        {
            ConsumeInputs();
            ProduceOutputs();
            State.CycleProgress -= 1f;
        }
    }

    // --- Virtual hooks — override in subclasses for specialised behaviour ---

    // True if the input buffer contains enough of every ingredient for one cycle.
    protected virtual bool HasInputsForOneCycle()
    {
        foreach (var input in State.ActiveRecipe.Inputs)
            if (State.InputBuffer.CountOf(input.ItemId) < input.Quantity) return false;
        return true;
    }

    // True if the output buffer can accept the full output of one cycle.
    protected virtual bool HasOutputSpace()
    {
        foreach (var output in State.ActiveRecipe.Outputs)
        {
            int current = State.OutputBuffer.CountOf(output.ItemId);
            if (current + output.Quantity > State.OutputBuffer.CapacityPerSlot) return false;
        }
        return true;
    }

    // Deduct one cycle's worth of inputs from the input buffer.
    protected virtual void ConsumeInputs()
    {
        foreach (var input in State.ActiveRecipe.Inputs)
            State.InputBuffer.TryRemove(input.ItemId, input.Quantity);
    }

    // Deposit one cycle's worth of outputs into the output buffer.
    protected virtual void ProduceOutputs()
    {
        foreach (var output in State.ActiveRecipe.Outputs)
            State.OutputBuffer.Add(output.ItemId, output.Quantity);
    }
}
