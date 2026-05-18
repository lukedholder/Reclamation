// Manages all belt segments in the simulation.
// Called by Simulation.Update() once per 20 Hz tick — after MachineSystem so machines
// produce outputs before belts try to move them.
//
// Belt segments are direct output-slot → input-slot connections.
// They do not require belt blocks in the world yet (V1 debug mode).
// When belt blocks are added, each placed belt block creates a BeltSegment here.

using System.Collections.Generic;

public class LogisticsSystem
{
    private int _nextId = 1;

    // All active belt segments, keyed by belt ID.
    private readonly Dictionary<int, BeltSegment> _belts = new Dictionary<int, BeltSegment>();

    // Expose for debug GUI
    public IReadOnlyDictionary<int, BeltSegment> Belts => _belts;

    public int Count => _belts.Count;

    // Connect an output slot on sourceBlock to an input slot on destBlock via a belt.
    // lengthInCells — physical belt length; determines capacity and transit time.
    //                 Each cell = 0.5 m = 1 item slot. A 4-cell belt holds 4 items
    //                 and has a transit time of 4 / (throughputPerMin / 60) seconds.
    // sourceSlot    — index into Block.MachineState.OutputBuffer.Slots
    // destSlot      — index into Block.MachineState.InputBuffer.Slots
    // sourcePort / destPort are PortDefinition.Index values on the respective blocks.
    // lengthInCells = floor(splineArcLength / CellSize) — computed by the view layer
    // from the belt spline path (including any belt-stand waypoints), NOT from grid distance.
    public BeltSegment Connect(int sourceBlockId, int sourcePort,
                               int destBlockId,   int destPort,
                               int lengthInCells,
                               float throughputPerMin = 60f)
    {
        var belt = new BeltSegment
        {
            Id               = _nextId++,
            SourceBlockId    = sourceBlockId,
            SourcePortIndex  = sourcePort,
            DestBlockId      = destBlockId,
            DestPortIndex    = destPort,
            LengthInCells    = lengthInCells,
            ThroughputPerMin = throughputPerMin,
        };

        belt.Init();
        _belts[belt.Id] = belt;
        return belt;
    }

    public void Disconnect(int beltId) => _belts.Remove(beltId);

    // Remove all belts that reference a block being destroyed.
    // Called by Simulation.RemoveBlock() before the block is deleted.
    public void DisconnectBlock(int blockId)
    {
        var toRemove = new List<int>();
        foreach (var kvp in _belts)
        {
            if (kvp.Value.SourceBlockId == blockId || kvp.Value.DestBlockId == blockId)
                toRemove.Add(kvp.Key);
        }
        foreach (var id in toRemove) _belts.Remove(id);
    }

    // Tick all belt segments. Call after MachineSystem.Tick() so outputs are populated first.
    public void Tick(float tickDelta, BlockTable blocks)
    {
        foreach (var belt in _belts.Values)
            belt.Tick(tickDelta, blocks);
    }
}
