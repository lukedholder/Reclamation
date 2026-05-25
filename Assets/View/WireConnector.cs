// Wire-tool hotbar slot — click a power pole to start a connection, click a
// second pole to complete it.  Click the same pole or right-click to cancel.
// Clicking two already-connected poles disconnects them.
//
// Behaviour mirrors Satisfactory's power-line placement:
//   • Hovered pole  — coloured box (green = connectable, red = out of range / full)
//   • Pending pole  — yellow box, grey preview wire follows the cursor
//   • Left-click    — pick first pole → then pick second pole to connect/disconnect
//   • Right-click   — cancel pending connection
//
// Setup: attach to the Player GameObject alongside Hotbar, Raycaster,
//        BlockPlacer, BlockDismantler, and BlockHighlight.
//
// Inspector:
//   Wire Material      — assign the same unlit material used by PowerWireView.
//   Highlight Material — any opaque material; green / yellow / red variants are
//                        created automatically at runtime.

using UnityEngine;
using static ViewConstants;

public class WireConnector : MonoBehaviour
{
    [SerializeField] private Material _wireMaterial;
    [SerializeField] private Material _highlightMaterial;
    [SerializeField] private float    _wireWidth = 0.04f;

    // Highlight colours
    private static readonly Color ColValid   = new Color(0.20f, 1.00f, 0.20f); // green
    private static readonly Color ColInvalid = new Color(1.00f, 0.20f, 0.20f); // red
    private static readonly Color ColPending = new Color(1.00f, 0.80f, 0.10f); // yellow-orange
    private static readonly Color ColPreview = new Color(0.75f, 0.75f, 0.75f); // grey

    private const float HighlightBias = 0.06f;

    private Hotbar    _hotbar;
    private Raycaster _raycaster;

    // Visuals
    private LineRenderer _preview;
    private GameObject   _hoverBox;
    private GameObject   _pendingBox;
    private Renderer     _hoverRend;
    private Renderer     _pendingRend;
    private Material     _matValid;
    private Material     _matInvalid;
    private Material     _matPending;

    // Connection state
    private int       _pendingPoleId   = -1;
    private BlockView _pendingPoleView;

    private Simulation Sim => GameManager.Instance.Simulation;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _hotbar    = GetComponent<Hotbar>();
        _raycaster = GetComponent<Raycaster>();

        BuildMaterials();
        _preview                  = BuildPreviewWire();
        (_hoverBox,   _hoverRend)   = BuildHighlightBox("WireHoverHighlight");
        (_pendingBox, _pendingRend) = BuildHighlightBox("WirePendingHighlight");
    }

    private void Update()
    {
        if (!_hotbar.IsWireMode) { ClearAll(); return; }

        BlockView hovered = HoveredPole();
        UpdateHoverBox(hovered);
        UpdatePendingBox();
        UpdatePreview(hovered);
        HandleInput(hovered);
    }

    // ── Visuals ───────────────────────────────────────────────────────────────

    private void UpdateHoverBox(BlockView hovered)
    {
        // Don't double-draw on the pending pole — the pending box covers it.
        if (hovered == null || (_pendingPoleId >= 0 && hovered.Block.Id == _pendingPoleId))
        {
            _hoverBox.SetActive(false);
            return;
        }

        _hoverBox.SetActive(true);
        _hoverBox.transform.position   = hovered.transform.position;
        _hoverBox.transform.localScale = hovered.transform.localScale + Vector3.one * HighlightBias;

        bool valid = _pendingPoleId < 0 || CanConnect(_pendingPoleView, hovered);
        _hoverRend.sharedMaterial = valid ? _matValid : _matInvalid;
    }

    private void UpdatePendingBox()
    {
        if (_pendingPoleId < 0) { _pendingBox.SetActive(false); return; }

        // The pending pole may have been dismantled.
        if (_pendingPoleView == null) { _pendingPoleId = -1; _pendingBox.SetActive(false); return; }

        _pendingBox.SetActive(true);
        _pendingBox.transform.position   = _pendingPoleView.transform.position;
        _pendingBox.transform.localScale = _pendingPoleView.transform.localScale + Vector3.one * HighlightBias;
        _pendingRend.sharedMaterial      = _matPending;
    }

    private void UpdatePreview(BlockView hovered)
    {
        if (_pendingPoleId < 0 || _pendingPoleView == null)
        {
            _preview.gameObject.SetActive(false);
            return;
        }

        _preview.gameObject.SetActive(true);
        Vector3 from = _pendingPoleView.transform.position;
        Vector3 to   = hovered         != null ? hovered.transform.position
                     : _raycaster.HasHit       ? _raycaster.Hit.point
                     :                           from;
        _preview.SetPosition(0, from);
        _preview.SetPosition(1, to);
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void HandleInput(BlockView hovered)
    {
        // Right-click always cancels the pending connection.
        if (Input.GetMouseButtonDown(1)) { ClearSelection(); return; }

        if (!Input.GetMouseButtonDown(0) || hovered == null) return;

        if (_pendingPoleId < 0)
        {
            // Start a new connection at this pole.
            _pendingPoleId   = hovered.Block.Id;
            _pendingPoleView = hovered;
            return;
        }

        // Clicked the pending pole again — cancel.
        if (hovered.Block.Id == _pendingPoleId) { ClearSelection(); return; }

        // Out of range or connection limit reached — red highlight is the feedback.
        if (!CanConnect(_pendingPoleView, hovered)) return;

        // Toggle: connect if disconnected, disconnect if already connected.
        if (Sim.Power.HasConnection(_pendingPoleId, hovered.Block.Id))
            Sim.Power.DisconnectPoles(_pendingPoleId, hovered.Block.Id);
        else
            Sim.Power.ConnectPoles(_pendingPoleId, hovered.Block.Id);

        ClearSelection();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Returns the hovered BlockView only when it is a power pole.
    private BlockView HoveredPole()
    {
        if (!_raycaster.HasHit) return null;
        var bv = _raycaster.Hit.collider.GetComponent<BlockView>();
        return bv != null && bv.Block.Definition.PowerInterface == PowerInterface.WireEndpoint
            ? bv : null;
    }

    private float PoleRange(Block b)
    {
        var p = b.Definition.Params as PoleParams;
        return (p?.WireRangeUnits ?? 8f) * CellSize;
    }

    // True if placing a wire from a to b is legal.
    // Always returns true for an existing connection so it can be toggled off.
    private bool CanConnect(BlockView a, BlockView b)
    {
        if (a == null || b == null) return false;

        float maxRange = Mathf.Min(PoleRange(a.Block), PoleRange(b.Block));
        if (Vector3.Distance(a.transform.position, b.transform.position) > maxRange) return false;

        // Disconnection is always allowed regardless of limits.
        if (Sim.Power.HasConnection(a.Block.Id, b.Block.Id)) return true;

        // New connection: respect each pole's MaxConnections.
        int maxA = (a.Block.Definition.Params as PoleParams)?.MaxConnections ?? 4;
        int maxB = (b.Block.Definition.Params as PoleParams)?.MaxConnections ?? 4;
        return Sim.Power.ConnectionCount(a.Block.Id) < maxA &&
               Sim.Power.ConnectionCount(b.Block.Id) < maxB;
    }

    private void ClearSelection()
    {
        _pendingPoleId   = -1;
        _pendingPoleView = null;
    }

    private void ClearAll()
    {
        ClearSelection();
        _preview.gameObject.SetActive(false);
        _hoverBox.SetActive(false);
        _pendingBox.SetActive(false);
    }

    // ── Builders ──────────────────────────────────────────────────────────────

    private void BuildMaterials()
    {
        _matValid   = HighlightMat(ColValid);
        _matInvalid = HighlightMat(ColInvalid);
        _matPending = HighlightMat(ColPending);
    }

    private Material HighlightMat(Color c)
    {
        Material mat = _highlightMaterial != null
            ? new Material(_highlightMaterial)
            : new Material(Shader.Find("Unlit/Color"));
        mat.color = c;
        return mat;
    }

    private LineRenderer BuildPreviewWire()
    {
        var go = new GameObject("WirePreview");
        go.transform.SetParent(transform);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount     = 2;
        lr.useWorldSpace     = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows    = false;
        lr.startWidth        = _wireWidth;
        lr.endWidth          = _wireWidth;
        lr.startColor        = ColPreview;
        lr.endColor          = ColPreview;
        if (_wireMaterial != null) lr.sharedMaterial = _wireMaterial;
        go.SetActive(false);
        return lr;
    }

    private (GameObject go, Renderer rend) BuildHighlightBox(string boxName)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = boxName;
        Object.Destroy(go.GetComponent<Collider>());
        var rend = go.GetComponent<Renderer>();
        go.SetActive(false);
        return (go, rend);
    }
}
