// Central data store for logistics (belt/inserter) networks.
// Structure mirrors PowerNetworkTable for consistency.
//
// PLACEHOLDER — LogisticsSystem is not yet implemented.
// This file reserves the data shape so other systems can reference
// Block.LogisticsNetworkId without compilation errors.

using System.Collections.Generic;

// Represents one connected belt/inserter network within a construct.
// Will be populated when LogisticsSystem is implemented.
public class LogisticsNetwork
{
    // Unique identifier for this logistics network.
    public int Id;

    // The construct this network belongs to.
    public int ConstructId;

    // Block IDs of all belts, inserters, and splitters in this network.
    public List<int> BlockIds = new List<int>();
}

public class LogisticsNetworkTable
{
    // All active logistics networks, keyed by network ID.
    public Dictionary<int, LogisticsNetwork> ById = new Dictionary<int, LogisticsNetwork>();

    // Secondary index: construct ID → list of logistics network IDs for that construct.
    public Dictionary<int, List<int>> ByConstruct = new Dictionary<int, List<int>>();
}
