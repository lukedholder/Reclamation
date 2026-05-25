// Belt placement tool. Active when Hotbar slot 9 (Belt Tool, key 9) is selected.
//
// Usage:
//   Aim at a machine — orange cubes mark Output ports, green cubes mark Input ports.
//   Left-click an Output port  — selects it as the belt source (turns yellow).
//   Left-click an Input port   — connects source → destination via Logistics.Connect().
//   Right-click / slot change  — cancels the pending source.
//
// Belt length (LengthInCells) is the straight-line port-to-port distance divided by
// CellSize. The simulation only cares about slot count, not path shape.
//
// Setup: attach to the Player GameObject alongside Hotbar and Raycaster.
//        Port Material  — any solid-colour material (Unlit/Color or Standard).
//        Line Material  — same or a separate wire material.
//        (Reuse the materials already assigned to WireConnector.)

using UnityEngine;
using static ViewConstants;

public class BeltConnector : MonoBehaviour
{
    [SerializeField] private Material _portMaterial;
    [SerializeField] private Material _lineMaterial;

    private Raycaster  _raycaster;
    private Hotbar     _hotbar;
    private Simulation Sim => GameManager.Instance.Simulation;

    // ── Pending state ─────────────────────────────────────────────────────────

    private bool           _hasPending;
    private int            _pendingBlockId;
    private PortDefinition _pendingPort;
    private Vector3        _pendingWorldPos;

    // ── Visuals ───────────────────────────────────────────────────────────────

    private GameObject   _hoverCube;
    private GameObject   _pendingCube;
    private LineRenderer _preview;

    private Material _matOutput, _matInput, _matPending, _matInvalid;

    private static readonly Color COutput  = new Color(1.00f, 0.50f, 0.00f);  // orange
    private static readonly Color CInput   = new Color(0.10f, 0.80f, 0.20f);  // green
    private static readonly Color CPending = new Color(1.00f, 0.90f, 0.10f);  // yellow
    private static readonly Color CInvalid = new Color(0.80f, 0.10f, 0.10f);  // red

    private const float MarkerSize = CellSize * 0.45f;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _raycaster = GetComponent<Raycaster>();
        _hotbar    = GetComponent<Hotbar>();

        _hoverCube   = MakeCube("BeltHoverPort",   MarkerSize);
        _pendingCube = MakeCube("BeltPendingPort", MarkerSize);

        var previewGO = new GameObject("BeltPreview");
        _preview = previewGO.AddComponent<LineRenderer>();
        _preview.positionCount = 2;
        _preview.startWidth    = 0.03f;
        _preview.endWidth      = 0.03f;
        _preview.useWorldSpace = true;
        _preview.enabled       = false;

        if (_portMaterial != null)
        {
            _matOutput  = new Material(_portMaterial); _matOutput.color  = COutput;
            _matInput   = new Material(_portMaterial); _matInput.color   = CInput;
            _matPending = new Material(_portMaterial); _matPending.color = CPending;
            _matInvalid = new Material(_portMaterial); _matInvalid.color = CInvalid;

            _pendingCube.GetComponent<Renderer>().material = _matPending;
        }

        if (_lineMaterial != null)
            _preview.material = new Material(_lineMaterial);

        _hoverCube.SetActive(false);
        _pendingCube.SetActive(false);
    }

    private void Update()
    {
        if (!_hotbar.IsBeltMode) { ClearAll(); return; }

        // Right-click cancels pending.
        if (Input.GetMouseButtonDown(1)) { CancelPending(); return; }

        // Keep pending cube on the selected port.
        if (_hasPending)
        {
            _pendingCube.transform.position = _pendingWorldPos;
            _pendingCube.SetActive(true);
        }

        // No ray hit — hide hover, hide preview.
        if (!_raycaster.HasHit)
        {
            _hoverCube.SetActive(false);
            _preview.enabled = false;
            return;
        }

        var bv = _raycaster.Hit.collider.GetComponent<BlockView>();
        if (bv == null || bv.Block.Definition.Ports.Length == 0)
        {
            _hoverCube.SetActive(false);
            _preview.enabled = false;
            return;
        }

        // Find the port closest to where the ray hit.
        var port = NearestPort(bv, _raycaster.Hit.point);
        if (port == null) { _hoverCube.SetActive(false); return; }

        Vector3 portPos = PortWorldPos(bv, port);
        bool    valid   = _hasPending
            ? (port.Type == PortType.Input && bv.Block.Id != _pendingBlockId)
            : (port.Type == PortType.Output);

        // Colour hover cube.
        _hoverCube.transform.position = portPos;
        _hoverCube.SetActive(true);
        if (_matOutput != null)
        {
            Material mat = !_hasPending
                ? (port.Type == PortType.Output ? _matOutput : _matInvalid)
                : (valid                        ? _matInput  : _matInvalid);
            _hoverCube.GetComponent<Renderer>().material = mat;
        }

        // Preview line from pending source to hovered port.
        if (_hasPending)
        {
            _preview.SetPosition(0, _pendingWorldPos);
            _preview.SetPosition(1, portPos);
            _preview.enabled = true;
        }
        else
        {
            _preview.enabled = false;
        }

        // Left-click.
        if (!Input.GetMouseButtonDown(0)) return;

        if (!_hasPending)
        {
            if (port.Type == PortType.Output)
            {
                _hasPending      = true;
                _pendingBlockId  = bv.Block.Id;
                _pendingPort     = port;
                _pendingWorldPos = portPos;
            }
        }
        else
        {
            if (valid)
            {
                int cells = Mathf.Max(1, Mathf.RoundToInt(
                    Vector3.Distance(_pendingWorldPos, portPos) / CellSize));

                Sim.Logistics.Connect(
                    _pendingBlockId, _pendingPort.Index,
                    bv.Block.Id,     port.Index,
                    cells);

                CancelPending();
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void CancelPending()
    {
        _hasPending = false;
        _pendingCube.SetActive(false);
        _preview.enabled = false;
    }

    private void ClearAll()
    {
        CancelPending();
        _hoverCube.SetActive(false);
    }

    // World-space position of a port anchor on a block.
    // Called by BeltView as well so it is public static.
    public static Vector3 PortWorldPos(BlockView bv, PortDefinition port)
    {
        var     def = bv.Block.Definition;
        Vector3 c   = bv.transform.position;
        float hx = def.SizeX * CellSize * 0.5f;
        float hy = def.SizeY * CellSize * 0.5f;
        float hz = def.SizeZ * CellSize * 0.5f;
        return port.Face switch
        {
            FaceDir.PosX => c + new Vector3( hx, 0,  0),
            FaceDir.NegX => c + new Vector3(-hx, 0,  0),
            FaceDir.PosY => c + new Vector3( 0,  hy, 0),
            FaceDir.NegY => c + new Vector3( 0, -hy, 0),
            FaceDir.PosZ => c + new Vector3( 0,  0,  hz),
            FaceDir.NegZ => c + new Vector3( 0,  0, -hz),
            _            => c,
        };
    }

    private static PortDefinition NearestPort(BlockView bv, Vector3 hitPoint)
    {
        PortDefinition nearest  = null;
        float          minSqDist = float.MaxValue;
        foreach (var p in bv.Block.Definition.Ports)
        {
            float d = Vector3.SqrMagnitude(PortWorldPos(bv, p) - hitPoint);
            if (d < minSqDist) { minSqDist = d; nearest = p; }
        }
        return nearest;
    }

    private static GameObject MakeCube(string name, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.localScale = Vector3.one * size;
        return go;
    }
}
