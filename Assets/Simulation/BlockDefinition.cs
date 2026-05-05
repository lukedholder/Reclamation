// Describes a type of block — its name, how many grid cells it occupies,
// and anything else that's true of every block of that type.
// One BlockDefinition exists per type. Every placed Block references one.

public class BlockDefinition
{
    public string Id;
    public string DisplayName;
    public int    SizeX;
    public int    SizeY;
    public int    SizeZ;
}