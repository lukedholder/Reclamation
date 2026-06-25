// Lets the player enter and exit a vehicle by looking at a Pilot Seat and pressing F.
//
// On enter:
//   - releases the construct from terrain (Construct.ManualUnanchored -> ApplyPhysics),
//     so a ground-built craft becomes a free Rigidbody;
//   - adds/*starts* a VehicleController that drives that Rigidbody;
//   - disables the character controller and the build/interact components, and
//     rides the player along by parenting it to the construct.
//
// On exit: reverses all of the above and drops the player beside the craft.
//
// While piloting, VehiclePilot.IsPiloting is true so other input components stay
// suppressed even though the cursor remains locked (mouse steers the craft).
//
// Setup: attach to the Player GameObject (alongside PlayerController, Raycaster,
//        Hotbar, MachineInteractor, ChestInteractor, PlayerInventory).

using UnityEngine;

public class VehiclePilot : MonoBehaviour
{
    // True while the player is piloting any vehicle. Read by input components that
    // must stay quiet during flight (they already guard on menus too).
    public static bool IsPiloting { get; private set; }

    private Raycaster          _raycaster;
    private PlayerController    _player;
    private CharacterController _cc;
    private Camera             _camera;

    // Components to switch off while seated so build/interact input is suppressed.
    private MonoBehaviour[] _suppressWhilePiloting;

    private Simulation Sim => GameManager.Instance.Simulation;

    // Active pilot session ------------------------------------------------------
    private ConstructView    _vehicle;
    private VehicleController _controller;
    private Transform        _seat;

    // Cached for the hint UI.
    private bool _aimingPilotable;

    private void Awake()
    {
        _raycaster = GetComponent<Raycaster>();
        _player    = GetComponent<PlayerController>();
        _cc        = GetComponent<CharacterController>();
        _camera    = GetComponentInChildren<Camera>();

        _suppressWhilePiloting = CollectSuppressList();
    }

    private MonoBehaviour[] CollectSuppressList()
    {
        var list = new System.Collections.Generic.List<MonoBehaviour>();
        void Add(MonoBehaviour mb) { if (mb != null) list.Add(mb); }
        Add(GetComponent<BlockPlacer>());
        Add(GetComponent<BlockDismantler>());
        Add(GetComponent<WireConnector>());
        Add(GetComponent<BeltConnector>());
        Add(GetComponent<Hotbar>());
        Add(GetComponent<MachineInteractor>());
        Add(GetComponent<ChestInteractor>());
        Add(GetComponent<PlayerInventory>());
        // Raycaster stays enabled (read-only; keeps the aim label live while flying).
        return list.ToArray();
    }

    private void Update()
    {
        if (IsPiloting)
        {
            if (GameInput.PilotDown) ExitVehicle();
            return;
        }

        _aimingPilotable = false;

        // Suppressed while any menu/panel is open (cursor unlocked covers all of them).
        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (MenuManager.IsOpen || BuildMenu.IsMenuOpen)  return;

        var seatView = AimedSeat(out var construct);
        _aimingPilotable = seatView != null && construct != null && construct.Construct.IsPilotable;

        if (_aimingPilotable && GameInput.PilotDown)
            EnterVehicle(construct, seatView.transform);
    }

    // Returns the BlockView of a Pilot Seat under the crosshair, plus its construct.
    private BlockView AimedSeat(out ConstructView construct)
    {
        construct = null;
        if (_raycaster == null || !_raycaster.HasHit || _raycaster.Hit.collider == null) return null;

        var bv = _raycaster.Hit.collider.GetComponent<BlockView>();
        if (bv == null || bv.Block.Definition.FunctionalType != FunctionalType.Seat) return null;

        construct = bv.GetComponentInParent<ConstructView>();
        return construct != null ? bv : null;
    }

    // ── Enter / exit ───────────────────────────────────────────────────────────

    private void EnterVehicle(ConstructView vehicle, Transform seat)
    {
        _vehicle = vehicle;
        _seat    = seat;

        // 1. Release from terrain and rebuild physics as a free body.
        var construct = vehicle.Construct;
        construct.ManualUnanchored = true;
        Sim.RecalcAnchor(construct.Id);
        vehicle.ApplyPhysics();

        // 2. Start the driver.
        _controller = vehicle.GetComponent<VehicleController>();
        if (_controller == null) _controller = vehicle.gameObject.AddComponent<VehicleController>();
        _controller.BeginPilot();

        // 3. Suppress player systems and ride along.
        SetSuppressed(true);
        if (_player != null) _player.enabled = false;
        if (_cc != null)     _cc.enabled     = false;

        var ctf = vehicle.transform;
        transform.SetParent(ctf, worldPositionStays: true);
        transform.localPosition = ctf.InverseTransformPoint(seat.position) + Vector3.up * 0.3f;
        transform.localRotation = Quaternion.identity;          // align with vehicle forward
        if (_camera != null) _camera.transform.localRotation = Quaternion.identity;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        IsPiloting       = true;
    }

    private void ExitVehicle()
    {
        if (_controller != null) _controller.EndPilot();

        // Drop the player beside the craft (or where the seat is, if it moved).
        var ctf      = _vehicle != null ? _vehicle.transform : null;
        Vector3 dropPos = _seat != null ? _seat.position : transform.position;
        if (ctf != null) dropPos += ctf.right * 1.5f + Vector3.up * 0.5f;
        float yaw = ctf != null ? ctf.eulerAngles.y : transform.eulerAngles.y;

        transform.SetParent(null, worldPositionStays: true);
        transform.position = dropPos;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (_camera != null) _camera.transform.localRotation = Quaternion.identity;

        if (_cc != null)     _cc.enabled     = true;            // re-enable after teleport
        if (_player != null)
        {
            _player.enabled = true;
            _player.SyncLookFromTransform();
        }
        SetSuppressed(false);

        IsPiloting       = false;
        _vehicle         = null;
        _controller      = null;
        _seat            = null;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void SetSuppressed(bool suppressed)
    {
        foreach (var mb in _suppressWhilePiloting)
            if (mb != null) mb.enabled = !suppressed;
    }

    // ── Minimal on-screen hint ──────────────────────────────────────────────────

    private void OnGUI()
    {
        string msg = IsPiloting
            ? "Piloting — WASD move, Space/Ctrl up/down, Q/E roll, Mouse steer, F exit"
            : _aimingPilotable ? "Press F to pilot" : null;
        if (msg == null) return;

        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(0, Screen.height - 70, Screen.width, 24), msg, style);
    }
}
