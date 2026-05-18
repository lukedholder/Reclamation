// Owns all active machine instances. Called by Simulation each tick.
//
// Registration:
//   Simulation.PlaceBlock() calls Register(block) after placement.
//   Simulation.RemoveBlock() calls Unregister(blockId) before removal.
//   Only blocks with a production FunctionalType get a machine instance —
//   structural, power, and logistics blocks are ignored here.
//
// Ticking:
//   Tick() iterates only registered machines — never the full block list.
//   At 1000 machines, this is 1000 calls at 20 Hz = 20,000 calls/sec.
//   Each call is a handful of arithmetic operations; no allocations.

using System.Collections.Generic;

public class MachineSystem
{
    // Fixed simulation step size passed to each machine each tick.
    public const float TickDelta = 1f / 20f;

    // Registered machines keyed by block ID. Only production blocks appear here.
    private readonly Dictionary<int, BaseMachine> _machines = new Dictionary<int, BaseMachine>();

    public int Count => _machines.Count;

    // Create and register the correct machine subclass for this block's FunctionalType.
    // No-op for block types that don't have production logic.
    public void Register(Block block)
    {
        BaseMachine machine = block.Definition.FunctionalType switch
        {
            FunctionalType.Miner     => new MinerMachine(block),
            FunctionalType.Assembler => new AssemblerMachine(block),
            FunctionalType.Furnace   => new FurnaceMachine(block),
            _                        => null,
        };

        if (machine != null)
            _machines[block.Id] = machine;
    }

    public void Unregister(int blockId) => _machines.Remove(blockId);

    // Retrieve the machine for a block (e.g. to assign a recipe from the UI).
    // Returns null if the block has no machine (structural, power, etc.).
    public BaseMachine Get(int blockId)
    {
        _machines.TryGetValue(blockId, out var m);
        return m;
    }

    // Also typed convenience getter — returns null if machine is wrong type.
    public T Get<T>(int blockId) where T : BaseMachine => Get(blockId) as T;

    // Advance every registered machine by one fixed simulation step.
    public void Tick()
    {
        foreach (var machine in _machines.Values)
            machine.Tick(TickDelta);
    }
}
