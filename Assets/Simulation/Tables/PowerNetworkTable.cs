// Central data store for all active PowerNetwork instances in the simulation.
// Networks are wire-topology-based: each connected component of power blocks is
// one network.  Isolated power blocks each form their own Dead network.
// PowerSystem rebuilds this table lazily whenever wires or blocks change.

using System.Collections.Generic;

public class PowerNetworkTable
{
    // All active power networks, keyed by canonical network ID (= minimum block ID
    // in the component).  PowerSystem iterates ById to process every network each tick.
    public Dictionary<int, PowerNetwork> ById = new Dictionary<int, PowerNetwork>();
}
