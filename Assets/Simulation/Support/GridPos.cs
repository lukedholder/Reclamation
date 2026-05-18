// Integer grid coordinate within a construct's local coordinate space.
// (0,0,0) is the construct's grid origin, established by its first Foundation block.
// All blocks in a construct store their position as integer GridPos offsets from this origin.
//
// Using integers instead of world-space floats eliminates floating-point drift
// in placement, adjacency checks, and occupancy map lookups.
//
// Cell size in world units is defined in Simulation as CellSize (currently 0.5m).
// World position = construct.WorldOrigin + GridPos * CellSize (with construct rotation applied).

using System;

public struct GridPos : IEquatable<GridPos>
{
    public int X;
    public int Y;
    public int Z;

    public GridPos(int x, int y, int z) { X = x; Y = y; Z = z; }

    // Canonical unit offsets for each of the six cardinal faces.
    public static readonly GridPos Zero = new GridPos(0, 0, 0);
    public static readonly GridPos PosX = new GridPos( 1,  0,  0);
    public static readonly GridPos NegX = new GridPos(-1,  0,  0);
    public static readonly GridPos PosY = new GridPos( 0,  1,  0);
    public static readonly GridPos NegY = new GridPos( 0, -1,  0);
    public static readonly GridPos PosZ = new GridPos( 0,  0,  1);
    public static readonly GridPos NegZ = new GridPos( 0,  0, -1);

    // All six neighbour offsets — used by adjacency and flood-fill checks.
    public static readonly GridPos[] Neighbours = { PosX, NegX, PosY, NegY, PosZ, NegZ };

    public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static GridPos operator -(GridPos a, GridPos b) => new GridPos(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static bool    operator ==(GridPos a, GridPos b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    public static bool    operator !=(GridPos a, GridPos b) => !(a == b);

    public bool Equals(GridPos other) => this == other;
    public override bool Equals(object obj) => obj is GridPos p && this == p;

    // Spatial hash chosen to minimise collisions for small integer coordinates.
    public override int GetHashCode() => X * 73856093 ^ Y * 19349663 ^ Z * 83492791;
    public override string ToString() => $"({X},{Y},{Z})";
}
