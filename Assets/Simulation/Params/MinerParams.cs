// Configuration for Miner blocks (FunctionalType.Miner).
// The miner must be placed so that its footprint covers at least one resource node cell
// whose type is listed in ResourceTypes. Items are produced without any input items.
// Resource nodes are infinite in V1; finite depletion is a post-V1 consideration.

public class MinerParams : IFunctionalParams
{
    // Items produced per second at full power (Basic Miner: 1.0).
    public float ExtractRatePerSecond;

    // Resource node IDs this miner can extract from (e.g. "iron_ore", "coal").
    // Must match the resource node definition IDs used by the terrain generator.
    public string[] ResourceTypes;

    // Which face of the miner block items exit from (typically NegY into a belt below).
    public FaceDir OutputFace;
}
