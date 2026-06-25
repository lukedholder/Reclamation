// Drives a construct's Rigidbody while the player is piloting it.
// Added at runtime by VehiclePilot to a ConstructView that has become a vehicle.
//
// BASIC PROTOTYPE MODEL (vehicle milestone M3):
//   - Total-thrust: the craft's translational force is the sum of every thruster's
//     ThrustKN, applied in the vehicle's own reference frame (construct axes).
//     Directional thruster summing (only thrust where you have thrusters pointing)
//     is a documented refinement, not done here.
//   - Thrust is NOT power-gated yet (thrusters draw no power in the basic blocks).
//   - Hover assist (on by default) cancels gravity so the craft is easy to fly;
//     Space / Ctrl then raise / lower altitude.
//
// Controls while piloted (read here in FixedUpdate):
//   W / S            forward / back
//   A / D            strafe left / right
//   Space / LeftCtrl up / down
//   Q / E            roll
//   Mouse            yaw (X) and pitch (Y)
//
// Setup: no manual setup — VehiclePilot adds and configures this component.

using UnityEngine;

[RequireComponent(typeof(ConstructView))]
public class VehicleController : MonoBehaviour
{
    [Header("Translation")]
    [Tooltip("Newtons of force per kN of thruster ThrustKN.")]
    [SerializeField] private float _forceScale = 1000f;
    [Tooltip("Extra multiplier on total translational thrust.")]
    [SerializeField] private float _thrustMultiplier = 1f;

    [Header("Rotation")]
    [SerializeField] private float _yawTorque   = 1200f;
    [SerializeField] private float _pitchTorque = 1200f;
    [SerializeField] private float _rollTorque  = 800f;
    [SerializeField] private float _mouseSensitivity = 2f;

    [Header("Assist")]
    [Tooltip("Cancel gravity while piloted so the craft hovers when idle.")]
    [SerializeField] private bool  _hoverAssist = true;
    [Tooltip("Linear drag applied while piloted (arcade damping / terminal speed).")]
    [SerializeField] private float _pilotedDrag = 1.2f;
    [Tooltip("Angular drag applied while piloted (rotation settles when you stop steering).")]
    [SerializeField] private float _pilotedAngularDrag = 4f;

    private ConstructView _constructView;
    private Rigidbody     _rb;
    private bool          _piloted;
    private float         _restDrag, _restAngularDrag;

    private Simulation Sim => GameManager.Instance != null ? GameManager.Instance.Simulation : null;

    private void Awake()
    {
        _constructView = GetComponent<ConstructView>();
    }

    public bool IsPiloted => _piloted;

    // Called by VehiclePilot after the construct has been released (ApplyPhysics
    // has added the Rigidbody).
    public void BeginPilot()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null) return;   // not a floating construct — cannot pilot

        _restDrag        = _rb.drag;
        _restAngularDrag = _rb.angularDrag;
        _rb.drag         = _pilotedDrag;
        _rb.angularDrag  = _pilotedAngularDrag;
        _piloted         = true;
    }

    public void EndPilot()
    {
        _piloted = false;
        if (_rb != null)
        {
            _rb.drag        = _restDrag;
            _rb.angularDrag = _restAngularDrag;
        }
    }

    private void FixedUpdate()
    {
        if (!_piloted || _rb == null) return;

        // Drive only while the piloting context is active (a pause/menu flips the
        // context away) — but keep hovering so the craft doesn't drop mid-menu.
        if (GameInput.Context == InputContext.Piloting)
        {
            ApplyTranslation();
            ApplyRotation();
        }

        if (_hoverAssist)
            _rb.AddForce(-Physics.gravity * _rb.mass, ForceMode.Force);
    }

    private void ApplyTranslation()
    {
        // Input in the vehicle's local frame (forward = construct +Z, right = +X, up = +Y).
        Vector2 m = GameInput.Move;                                       // A/D strafe, W/S forward
        float   y = (GameInput.AscendHeld  ?  1f : 0f)
                  + (GameInput.DescendHeld ? -1f : 0f);                   // up / down

        var localDir = new Vector3(m.x, y, m.y);
        if (localDir.sqrMagnitude < 1e-4f) return;
        if (localDir.sqrMagnitude > 1f) localDir.Normalize();

        float totalForce = TotalThrustForce();
        Vector3 worldForce = _rb.transform.TransformDirection(localDir) * totalForce;
        _rb.AddForce(worldForce, ForceMode.Force);
    }

    private void ApplyRotation()
    {
        Vector2 look = GameInput.Look;
        float yaw   = look.x * _mouseSensitivity;
        float pitch = look.y * _mouseSensitivity;
        float roll  = (GameInput.RollLeftHeld  ?  1f : 0f)
                    + (GameInput.RollRightHeld ? -1f : 0f);

        // Body-axis torque: pitch about local X, yaw about local Y, roll about local Z.
        var torque = new Vector3(
            -pitch * _pitchTorque,
             yaw   * _yawTorque,
             roll  * _rollTorque);

        if (torque.sqrMagnitude > 1e-4f)
            _rb.AddRelativeTorque(torque, ForceMode.Force);
    }

    // Sum of every thruster's force on this construct (basic total-thrust model).
    private float TotalThrustForce()
    {
        var sim = Sim;
        var construct = _constructView != null ? _constructView.Construct : null;
        if (sim == null || construct == null) return 0f;

        float kn = 0f;
        foreach (int bid in construct.BlockIds)
        {
            if (!sim.Blocks.ById.TryGetValue(bid, out var b)) continue;
            if (b.Definition.FunctionalType != FunctionalType.Propulsion) continue;
            if (b.Definition.Params is ThrusterParams tp)
                kn += tp.ThrustKN;
            // TODO (vehicle M4): scale by b.MachineState.OperatingRate once thrusters
            // are wired into the power system as consumers.
        }
        return kn * _forceScale * _thrustMultiplier;
    }
}
