// Central data store for all active PowerNetwork instances in the simulation.
// A single construct may own multiple independent power networks — they are all stored here,
// each with a unique network ID (a separate ID sequence from construct IDs).
// PowerSystem iterates ById to process every network each tick.

using System.Collections.Generic;

public class PowerNetworkTable
{
    // All active power networks, keyed by network ID.
    public Dictionary<int, PowerNetwork> ById = new Dictionary<int, PowerNetwork>();

    // Secondary index: construct ID → list of network IDs owned by that construct.
    // A construct may have several disconnected networks; they are listed together here.
    public Dictionary<int, List<int>> ByConstruct = new Dictionary<int, List<int>>();
}
