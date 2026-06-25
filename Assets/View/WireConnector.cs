// Wire-tool hotbar slot — click a power block to start a connection, click a
// second power block to complete it.  Click the same block or right-click to cancel.
// Clicking two already-connected blocks disconnects them.
//
// Any block with BlockDefinition.MaxWireConnections > 0 can be wired.
// Wire range is read from BlockDefinition.WireRangeUnits; the shorter of the two
// endpoints wins.  Connection limits also come from BlockDefinition.MaxWireConnections.
//
// Behaviour mirrors Satisfactory's power-line placement:
//   • Hovered block  — coloured box (green = connectable, red = out of range / full)
//   • Pending block  — yellow box, grey preview wire follows the cursor
//   • Left-click     — pick first block → then pick second block to connect/disconnect
//   • Right-click    — cancel pending connection
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
    private int       _pendingId   = -1;
    private BlockView _pendingView;

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
        if (GameInput.Context != InputContext.Gameplay) { ClearAll(); return; }
        if (!_hotbar.IsWireMode)                        { ClearAll(); return; }

        BlockView hovered = HoveredPowerBlock();
        UpdateHoverBox(hovered);
        UpdatePendingBox();
        UpdatePreview(hovered);
        HandleInput(hovered);
    }

    // ── Visuals ───────────────────────────────────────────────────────────────

    private void UpdateHoverBox(BlockView hovered)
    {
        // Don't double-draw on the pending block — the pending box covers it.
        if (hovered == null || (_pendingId >= 0 && hovered.Block.Id == _pendingId))
        {
            _hoverBox.SetActive(false);
            return;
        }

        _hoverBox.SetActive(true);
        _hoverBox.transform.position   = hovered.transform.position;
        _hoverBox.transform.rotation   = hovered.transform.rotation;
        _hoverBox.transform.localScale = hovered.transform.localScale + Vector3.one * HighlightBias;

        bool valid = _pendingId < 0 || CanConnect(_pendingView, hovered);
        _hoverRend.sharedMaterial = valid ? _matValid : _matInvalid;
    }

    private void UpdatePendingBox()
    {
        if (_pendingId < 0) { _pendingBox.SetActive(false); return; }

        // The pending block may have been dismantled.
        if (_pendingView == null) { _pendingId = -1; _pendingBox.SetActive(false); return; }

        _pendingBox.SetActive(true);
        _pendingBox.transform.position   = _pendingView.transform.position;
        _pendingBox.transform.rotation   = _pendingView.transform.rotation;
        _pendingBox.transform.localScale = _pendingView.transform.localScale + Vector3.one * HighlightBias;
        _pendingRend.sharedMaterial      = _matPending;
    }

    private void UpdatePreview(BlockView hovered)
    {
        if (_pendingId < 0 || _pendingView == null)
        {
            _preview.gameObject.SetActive(false);
            return;
        }

        _preview.gameObject.SetActive(true);
        Vector3 from = _pendingView.transform.position;
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
        if (GameInput.SecondaryDown) { ClearSelection(); return; }

        if (!GameInput.PrimaryDown || hovered == null) return;

        if (_pendingId < 0)
        {
            // Start a new connection at this block.
            _pendingId   = hovered.Block.Id;
            _pendingView = hovered;
            return;
        }

        // Clicked the pending block again — cancel.
        if (hovered.Block.Id == _pendingId) { ClearSelection(); return; }

        // Out of range or connection limit reached — red highlight is the feedback.
        if (!CanConnect(_pendingView, hovered)) return;

        // Toggle: connect if disconnected, disconnect if already connected.
        if (Sim.Power.HasConnection(_pendingId, hovered.Block.Id))
            Sim.Power.DisconnectBlocks(_pendingId, hovered.Block.Id);
        else
            Sim.Power.ConnectBlocks(_pendingId, hovered.Block.Id);

        ClearSelection();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Returns the hovered BlockView only when it can accept wire connections.
    private BlockView HoveredPowerBlock()
    {
        if (!_raycaster.HasHit) return null;
        var bv = _raycaster.Hit.collider.GetComponent<BlockView>();
        return bv != null && bv.Block.Definition.MaxWireConnections > 0 ? bv : null;
    }

    private float WireRange(Block b) => b.Definition.WireRangeUnits * CellSize;

    // True if placing a wire from a to b is legal.
    // Always returns true for an existing connection so it can be toggled off.
    private bool CanConnect(BlockView a, BlockView b)
    {
        if (a == null || b == null) return false;

        float maxRange = Mathf.Min(WireRange(a.Block), WireRange(b.Block));
        if (Vector3.Distance(a.transform.position, b.transform.position) > maxRange) return false;

        // Disconnection is always allowed regardless of limits.
        if (Sim.Power.HasConnection(a.Block.Id, b.Block.Id)) return true;

        // New connection: respect each block's MaxWireConnections.
        return Sim.Power.ConnectionCount(a.Block.Id) < a.Block.Definition.MaxWireConnections &&
               Sim.Power.ConnectionCount(b.Block.Id) < b.Block.Definition.MaxWireConnections;
    }

    private void ClearSelection()
    {
        _pendingId   = -1;
        _pendingView = null;
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
