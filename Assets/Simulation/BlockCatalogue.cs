// The list of all block types in the game.
// Add new block definitions here as they are designed.

public static class BlockCatalogue
{
    public static readonly BlockDefinition SmallCube = new BlockDefinition
    {
        Id          = "small_cube",
        DisplayName = "Small Cube",
        SizeX       = 1,
        SizeY       = 1,
        SizeZ       = 1,
    };

    public static readonly BlockDefinition LargeCube = new BlockDefinition
    {
        Id          = "large_cube",
        DisplayName = "Large Cube",
        SizeX       = 2,
        SizeY       = 2,
        SizeZ       = 2,
    };

    public static readonly BlockDefinition Plank = new BlockDefinition
    {
        Id          = "plank",
        DisplayName = "Plank",
        SizeX       = 4,
        SizeY       = 1,
        SizeZ       = 1,
    };
}