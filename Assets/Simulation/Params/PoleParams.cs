// Configuration for Power Pole blocks (FunctionalType.Pole).
// Poles form automatic wire connections to other poles and powered blocks within WireRangeUnits.
// Connections are bidirectional and recalculated whenever any pole is placed or removed.
// MaxConnections caps the total wire edges this pole can carry simultaneously.

public class PoleParams : IFunctionalParams
{
    // Maximum grid-unit distance to another pole for an automatic wire to form.
    // Small Power Pole: 8 units. Large Power Pole: 16 units.
    public float WireRangeUnits;

    // Maximum simultaneous wire connections.
    // Small Power Pole: 4. Large Power Pole: 6.
    public int MaxConnections;
}
