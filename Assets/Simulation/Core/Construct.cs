using System;
using System.Collections.Generic;

namespace Reclamation.Simulation
{
    // ── ConstructType ─────────────────────────────────────────────────────────

    public enum ConstructType
    {
        Structure,  // Connected blocks with no qualifying function
        Base,       // Anchored (Foundation) + has Production blocks
        Outpost,    // Anchored, no production
        Vehicle,    // Pilotable (Seat + Propulsion + Power) and not anchored
        Droid,      // Added in Step 11 — autonomous unit
        Orbital,    // Future — off-planet constructs
    }

    // ── AABB ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Axis-aligned bounding box in grid-cell space.
    /// Recomputed by Construct.Reclassify() whenever membership changes.
    /// </summary>
    public struct AABB
    {
        public Int3 Min;
        public Int3 Max;

        public AABB(Int3 min, Int3 max) { Min = min; Max = max; }

        /// <summary>Sentinel value representing an empty (uninitialised) box.</summary>
        public static AABB Empty => new(
            new Int3(int.MaxValue, int.MaxValue, int.MaxValue),
            new Int3(int.MinValue, int.MinValue, int.MinValue));

        public bool IsEmpty =>
            Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;

        /// <summary>Returns a new AABB expanded to contain <paramref name="point"/>.</summary>
        public AABB Encapsulate(Int3 point) => new(
            new Int3(Math.Min(Min.X, point.X), Math.Min(Min.Y, point.Y), Math.Min(Min.Z, point.Z)),
            new Int3(Math.Max(Max.X, point.X), Math.Max(Max.Y, point.Y), Math.Max(Max.Z, point.Z)));

        public override string ToString() => $"AABB[{Min}..{Max}]";
    }

    // ── Construct ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A connected set of blocks that acts as a single logical unit.
    ///
    /// Membership is maintained by ConstructSystem via flood-fill. Classification
    /// (Type, IsAnchored, IsPilotable) is derived from the block composition and
    /// recomputed by Reclassify() whenever membership changes.
    ///
    /// Constructs are plain data — ConstructSystem contains all the logic.
    /// </summary>
    public class Construct
    {
        // ── Identity ─────────────────────────────────────────────────────────

        /// <summary>Unique ID assigned by ConstructTable. Never 0.</summary>
        public int Id;

        /// <summary>Player-assigned label, or an auto-generated name if not set.</summary>
        public string Name;

        // ── Membership ────────────────────────────────────────────────────────

        /// <summary>
        /// IDs of every block in this construct. Written directly by ConstructSystem.
        /// Do not mutate from outside that system.
        /// </summary>
        public HashSet<int> BlockIds = new();

        // ── Derived classification (updated by Reclassify) ────────────────────

        public ConstructType Type;

        /// <summary>True when at least one block has FunctionalType.Foundation.</summary>
        public bool IsAnchored;

        /// <summary>True when the construct has a Seat, Propulsion, and a power source.</summary>
        public bool IsPilotable;

        /// <summary>World-space bounding box over all occupied cells. Updated by Reclassify.</summary>
        public AABB Bounds;

        // ── Classification logic ──────────────────────────────────────────────

        /// <summary>
        /// Recomputes Type, IsAnchored, IsPilotable, and Bounds from the current
        /// block composition. Call this after any membership change.
        ///
        /// Mirrors the SDD §3.2 classification rules exactly.
        /// </summary>
        public void Reclassify(BlockTable blocks)
        {
            bool hasSeat       = blocks.AnyInSet(BlockIds,
                b => b.Definition.FunctionalType == FunctionalType.Seat);
            bool hasPropulsion = blocks.AnyInSet(BlockIds,
                b => b.Definition.FunctionalType == FunctionalType.Propulsion);
            bool hasPower      = blocks.AnyInSet(BlockIds,
                b => b.Definition.PowerOutputKW > 0f);
            bool hasFoundation = blocks.AnyInSet(BlockIds,
                b => b.Definition.FunctionalType == FunctionalType.Foundation);
            bool hasProduction = blocks.AnyInSet(BlockIds,
                b => b.Definition.Category == BlockCategory.Production);

            IsAnchored  = hasFoundation;
            IsPilotable = hasSeat && hasPropulsion && hasPower;

            Type = (IsPilotable && !IsAnchored) ? ConstructType.Vehicle
                 : (IsAnchored && hasProduction) ? ConstructType.Base
                 : IsAnchored                    ? ConstructType.Outpost
                 :                                 ConstructType.Structure;

            // Recompute world-space bounds over every occupied cell
            var bounds = AABB.Empty;
            foreach (var id in BlockIds)
            {
                var block = blocks.Get(id);
                if (block == null) continue;
                foreach (var cell in block.OccupiedPositions())
                    bounds = bounds.Encapsulate(cell);
            }
            Bounds = bounds;
        }
    }
}
