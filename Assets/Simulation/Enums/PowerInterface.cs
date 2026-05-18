// How a block participates in power network graph construction.
// PowerNetworkManager checks this field when a block is placed to decide
// whether to add a new graph node, add a wire endpoint, or skip the block entirely.

public enum PowerInterface
{
    None,          // Block is invisible to the power system entirely.
    Node,          // Direct consumer or producer — added as a leaf node in the network graph.
    WireEndpoint,  // Power pole — auto-forms wire edges to nearby WireEndpoints within range.
}
