// Material stored per solid voxel — drives terrain colouring and (later) what mining
// a voxel yields. Kept small for M1; expand alongside the resource/biome system.

public enum VoxelMaterial : byte
{
    Air       = 0,
    Dirt      = 1,
    Stone     = 2,
    IronOre   = 3,
    CopperOre = 4,
    Coal      = 5,
}
