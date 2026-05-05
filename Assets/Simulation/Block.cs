// A single placed block in the world.
// Position is in world space (any value, not snapped to a grid).
// Rotation is stored as Euler angles in degrees on all three axes.
// Y rotation is used for normal building. X and Z will be used by vehicles.

public class Block
{
    public int   Id;
    public float X;
    public float Y;
    public float Z;
    public float RotationX;
    public float RotationY;
    public float RotationZ;
}