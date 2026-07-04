// Per-object gravity toward the planet centre (assumed to be the world origin while the
// planet is rendered in planet space; M3 stage 1). On a sphere there is no single global
// "down", so each physics body is pulled toward the centre individually — the Space
// Engineers model.
//
// PlanetMath.Up/Down give the local surface frame for controllers (the player and, later,
// the planet-centric camera frame).
//
// Attach RadialGravityBody to any Rigidbody that should fall toward the planet (debris,
// vehicles before docking, dropped items). It disables Unity's global gravity for that
// body and applies the radial pull itself.

using UnityEngine;

public static class PlanetMath
{
    // Planet centre in world space (origin while rendering in planet space).
    public static Vector3 Centre = Vector3.zero;

    public static Vector3 Up(Vector3 worldPos)
    {
        Vector3 d = worldPos - Centre;
        return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.up;
    }

    public static Vector3 Down(Vector3 worldPos) => -Up(worldPos);
}

[RequireComponent(typeof(Rigidbody))]
public class RadialGravityBody : MonoBehaviour
{
    [Tooltip("Gravitational acceleration toward the planet centre (m/s^2).")]
    public float gravity = 9.81f;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;   // we apply radial gravity ourselves
    }

    private void FixedUpdate()
    {
        _rb.AddForce(PlanetMath.Down(_rb.position) * gravity, ForceMode.Acceleration);
    }
}
