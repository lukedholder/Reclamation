// A single electrical power network within a construct.
// A construct may own multiple independent networks — they do NOT automatically merge
// when two constructs join. Power infrastructure must be explicitly connected via poles.
// PowerSystem processes each network independently on every tick.
//
// Network graph edges are formed automatically by Power Pole blocks:
// two poles within PoleParams.WireRangeUnits of each other become connected.
// Consumers and producers (Nodes) connect directly to the nearest pole in range.

using System.Collections.Generic;

public class PowerNetwork
{
    // Unique identifier for this network (separate sequence from construct IDs).
    public int Id;

    // Block IDs of all Generator blocks supplying power to this network.
    // PowerSystem sums their GeneratorState.CurrentOutputKW for TotalSupplyKW.
    public List<int> GeneratorIds = new List<int>();

    // Block IDs of all Battery blocks on this network.
    // PowerSystem charges/discharges them according to the current balance.
    public List<int> BatteryIds   = new List<int>();

    // Block IDs of all blocks drawing power from this network.
    // Includes machines, turrets, lights, and any block with PowerDrawKW > 0.
    public List<int> ConsumerIds  = new List<int>();

    // Block IDs of all Power Pole blocks whose wire topology defines this network.
    // Used by PowerNetworkManager to rebuild the graph when poles are added/removed.
    public List<int> PoleIds      = new List<int>();

    // Aggregate supply this tick in kilowatts. Computed by PowerSystem before balancing.
    public float TotalSupplyKW;

    // Aggregate demand this tick in kilowatts. Computed by PowerSystem before balancing.
    public float TotalDemandKW;

    // Current power balance state. Updated by PowerSystem after each tick's balance pass.
    // Drives consumer throttling and battery charge/discharge behaviour.
    public PowerState State = PowerState.Dead;
}
