using System;

namespace Reclamation.Simulation
{
    /// <summary>
    /// Immutable 3D integer coordinate. Used for block grid positions.
    /// Replaces UnityEngine.Vector3Int so the simulation layer stays engine-agnostic.
    /// </summary>
    public readonly struct Int3 : IEquatable<Int3>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public Int3(int x, int y, int z) { X = x; Y = y; Z = z; }

        public static Int3 Zero => new(0, 0, 0);

        /// <summary>All six face-adjacent directions in a 3D grid.</summary>
        public static readonly Int3[] CardinalDirections =
        {
            new( 1,  0,  0), new(-1,  0,  0),
            new( 0,  1,  0), new( 0, -1,  0),
            new( 0,  0,  1), new( 0,  0, -1),
        };

        public static Int3 operator +(Int3 a, Int3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Int3 operator -(Int3 a, Int3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Int3 operator *(Int3 a, int s)  => new(a.X * s,   a.Y * s,   a.Z * s);

        public bool Equals(Int3 other)          => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Int3 other && Equals(other);
        public override int  GetHashCode()      => HashCode.Combine(X, Y, Z);

        public static bool operator ==(Int3 a, Int3 b) =>  a.Equals(b);
        public static bool operator !=(Int3 a, Int3 b) => !a.Equals(b);

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
