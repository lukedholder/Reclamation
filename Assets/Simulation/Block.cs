// A single placed block in the world.
// Position is in world space. Rotation is Euler angles in degrees.
// Definition describes what type of block this is.

public class Block
{
    public int             Id;
    public BlockDefinition Definition;
    public float           X;
    public float           Y;
    public float           Z;
    public float           RotationX;
    public float           RotationY;
    public float           RotationZ;
    public int             ConstructId = -1;
}