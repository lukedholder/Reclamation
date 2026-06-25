// Configuration for a Seat block — the block a player occupies to pilot a vehicle.
// Shared and immutable across all placed seats of this type.
//
// Facing is the block-local forward the pilot looks down when seated; it is
// reserved for the view-layer pilot camera and is not yet used by the basic
// prototype (which uses the construct's forward axis as the vehicle frame).

public class SeatParams : IFunctionalParams
{
    // Block-local forward direction the pilot faces when seated.
    public FaceDir Facing = FaceDir.PosZ;
}
