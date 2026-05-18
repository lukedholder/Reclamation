// Central data store for all placed Block instances in the simulation.
// ById is the primary lookup — O(1) access by block ID.
// ByConstruct is a secondary index for fast per-construct iteration,
// kept in sync whenever a block's ConstructId changes.
//
// Intended to replace the raw List<Block> in Simulation.cs once the
// grid-based position system is fully adopted.

using System.Collections.Generic;

public class BlockTable
{
    // All placed blocks, keyed by block ID. The authoritative record.
    public Dictionary<int, Block> ById = new Dictionary<int, Block>();

    // Secondary index: construct ID → list of block IDs belonging to that construct.
    // Allows O(n_construct) iteration without scanning all blocks globally.
    // Must be kept in sync whenever Block.ConstructId is written.
    public Dictionary<int, List<int>> ByConstruct = new Dictionary<int, List<int>>();

    // Secondary index: power network ID → list of block IDs on that network.
    // Allows PowerSystem to iterate only blocks relevant to each network.
    public Dictionary<int, List<int>> ByPowerNetwork = new Dictionary<int, List<int>>();
}
