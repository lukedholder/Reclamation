using System;

namespace Reclamation.Simulation
{
    /// <summary>
    /// Immutable 2D integer coordinate. Used for chunk grid coordinates (X, Z).
    /// Replaces UnityEngine.Vector2Int so the simulation layer stays engine-agnostic.
    /// </summary>
    public readonly struct Int2 : IEquatable<Int2>
    {
        public readonly int X;
        public readonly int Z;

        public Int2(int x, int z) { X = x; Z = z; }

        public static Int2 Zero => new(0, 0);

        public static Int2 operator +(Int2 a, Int2 b) => new(a.X + b.X, a.Z + b.Z);
        public static Int2 operator -(Int2 a, Int2 b) => new(a.X - b.X, a.Z - b.Z);

        public bool Equals(Int2 other)          => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is Int2 other && Equals(other);
        public override int  GetHashCode()      => HashCode.Combine(X, Z);

        public static bool operator ==(Int2 a, Int2 b) =>  a.Equals(b);
        public static bool operator !=(Int2 a, Int2 b) => !a.Equals(b);

        public override string ToString() => $"({X}, {Z})";
    }
}
