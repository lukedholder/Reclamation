// Configuration for a Propulsion (thruster) block.
// Shared and immutable across all placed thrusters of this type.
//
// ThrustKN is the thruster's maximum push. The view layer (VehicleController)
// converts it to a Unity force when the construct is piloted.
//
// ExhaustDir is the block-local face the exhaust points out of; thrust pushes in
// the OPPOSITE direction. It is reserved for the directional-thrust model
// (summing only thrusters that can push a desired way) and is not yet used by the
// basic total-thrust prototype.

public class ThrusterParams : IFunctionalParams
{
    // Maximum thrust in kilonewtons (scaled to a Unity force by VehicleController).
    public float ThrustKN = 8f;

    // Block-local face the exhaust fires from; thrust is the opposite direction.
    public FaceDir ExhaustDir = FaceDir.NegZ;
}
