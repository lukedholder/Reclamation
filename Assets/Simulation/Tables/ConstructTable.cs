// Central data store for all active Construct instances in the simulation.
// Keyed by construct ID for O(1) lookup.
// A construct is created when an isolated block is placed,
// and destroyed when its last block is removed.

using System.Collections.Generic;

public class ConstructTable
{
    // All active constructs, keyed by construct ID.
    public Dictionary<int, Construct> ById = new Dictionary<int, Construct>();
}
