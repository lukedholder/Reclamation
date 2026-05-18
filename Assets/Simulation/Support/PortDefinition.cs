// Defines one logistics connection point on a block.
// Ports are declared on BlockDefinition and are fixed for the lifetime of the block type.
//
// SIMULATION use: port Index maps 1:1 to a buffer slot.
//   Input  port N  →  Block.MachineState.InputBuffer.Slots[N]
//   Output port N  →  Block.MachineState.OutputBuffer.Slots[N]
//
// VIEW LAYER use: Face + Index give the view layer an anchor for the belt spline endpoint.
//   The spline starts/ends at the world-space centre of the named face on the block,
//   offset by Index if multiple ports share the same face (e.g. three input faces
//   spaced evenly across a wide machine face).
//
// Belt length (LengthInCells) is NOT derived from grid distance between blocks.
// It is floor(splinePathLength / CellSize), where splinePathLength is the arc length
// of the curve the view layer fits between two port anchors (optionally via belt stands).

public class PortDefinition
{
    // 0-based port number on this block. Matches the InputBuffer or OutputBuffer slot index.
    public int      Index;

    // Whether items flow into or out of this port.
    public PortType Type;

    // Which face of the block this port is located on.
    // The view layer uses this to position the spline endpoint in world space.
    // Also constrains belt placement — a port on FaceDir.NegZ needs clear line-of-sight
    // on that face for a belt pole or machine to connect from that direction.
    public FaceDir  Face;
}
