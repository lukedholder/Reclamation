// The six cardinal faces of a block in its local coordinate space.
// Used to specify which face a port, input, or output is on for functional blocks
// (miners, generators, docking ports, etc.).
// PosY = top face, NegY = bottom face (default miner output).

public enum FaceDir
{
    PosX, // right
    NegX, // left
    PosY, // top
    NegY, // bottom
    PosZ, // front
    NegZ, // back
}
