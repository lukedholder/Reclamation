// Whether a logistics port accepts items into a machine (Input)
// or exposes items produced by a machine for collection (Output).
// Used by PortDefinition and validated by LogisticsSystem when connecting belts.

public enum PortType
{
    Input,   // Items flow into this port → InputBuffer
    Output,  // Items flow out of this port ← OutputBuffer
}
