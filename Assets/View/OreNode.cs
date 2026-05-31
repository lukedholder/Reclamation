// A resource deposit in the world. Exists independently of the block/construct system.
//
// Miners auto-detect any OreNode whose centre falls within their footprint when placed.
// No setup required — simply drop this component on a GameObject in the scene and fill
// the Inspector fields, or spawn it at runtime via OreNode.Spawn().
//
// Visual: a flat coloured cube (placeholder mesh) scaled to 1.2×0.25×1.2 cells.
//         Colour is driven by ResourceId via the static palette below.
//         A world-space Canvas label floats above it showing the resource name.
//
// Detection: BlockPlacer calls OreNode.FindUnder(worldCenter, sizeX, sizeZ) after
//            placing a miner to get the nearest matching deposit.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static ViewConstants;

public class OreNode : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Tooltip("Resource ID this deposit yields, e.g. \"iron_ore\", \"copper_ore\", \"coal\".")]
    public string ResourceId = "iron_ore";

    [Tooltip("Items produced per second when a miner sits on this node.")]
    public float ExtractRate = 1f;

    // ── Registry ──────────────────────────────────────────────────────────────

    private static readonly List<OreNode> _all = new List<OreNode>();

    /// <summary>
    /// All live OreNodes in the scene. Read-only by callers.
    /// </summary>
    public static IReadOnlyList<OreNode> All => _all;

    // ── Resource colours ──────────────────────────────────────────────────────

    private static readonly Dictionary<string, Color> _palette = new Dictionary<string, Color>
    {
        { "iron_ore",   new Color(0.55f, 0.50f, 0.45f) },   // warm grey
        { "copper_ore", new Color(0.80f, 0.45f, 0.15f) },   // copper orange
        { "coal",       new Color(0.18f, 0.18f, 0.18f) },   // near-black
        { "gold_ore",   new Color(0.90f, 0.75f, 0.10f) },   // gold
        { "stone",      new Color(0.60f, 0.58f, 0.55f) },   // pale grey
    };

    private static readonly Color _fallbackColor = new Color(0.70f, 0.30f, 0.70f); // purple

    // ── Visual sizes ─────────────────────────────────────────────────────────

    private const float NodeW = 1.2f * CellSize;   // world-space width  (X)
    private const float NodeH = 0.25f * CellSize;  // world-space height (Y)
    private const float NodeD = 1.2f * CellSize;   // world-space depth  (Z)

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _all.Add(this);
        BuildVisual();
    }

    private void OnDestroy()
    {
        _all.Remove(this);
    }

    // ── Factory helper ────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a new OreNode at the given world XZ position (Y is always 0).
    /// </summary>
    public static OreNode Spawn(string resourceId, float worldX, float worldZ, float extractRate = 1f)
    {
        var go   = new GameObject($"OreNode_{resourceId}");
        go.transform.position = new Vector3(worldX, 0f, worldZ);
        var node = go.AddComponent<OreNode>();
        node.ResourceId   = resourceId;
        node.ExtractRate  = extractRate;
        return node;
    }

    // ── Spatial query ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the nearest OreNode whose centre is within <paramref name="radius"/> world-units
    /// of <paramref name="worldPos"/> (XZ plane only). Used for ghost snapping.
    /// </summary>
    public static OreNode FindNearest(Vector3 worldPos, float radius)
    {
        OreNode best        = null;
        float   bestSqDist  = radius * radius;

        foreach (var node in _all)
        {
            float dx = node.transform.position.x - worldPos.x;
            float dz = node.transform.position.z - worldPos.z;
            float sq = dx * dx + dz * dz;
            if (sq <= bestSqDist) { bestSqDist = sq; best = node; }
        }

        return best;
    }

    /// <summary>
    /// Returns the nearest OreNode whose centre lies within the rectangular footprint
    /// of a machine centred at <paramref name="worldCenter"/> with the given cell dimensions.
    /// Returns null if none qualifies.
    /// </summary>
    public static OreNode FindUnder(Vector3 worldCenter, int sizeX, int sizeZ)
    {
        float halfX = sizeX * CellSize * 0.5f;
        float halfZ = sizeZ * CellSize * 0.5f;

        OreNode best     = null;
        float   bestDist = float.MaxValue;

        foreach (var node in _all)
        {
            float dx = Mathf.Abs(node.transform.position.x - worldCenter.x);
            float dz = Mathf.Abs(node.transform.position.z - worldCenter.z);

            if (dx <= halfX && dz <= halfZ)
            {
                float dist = dx * dx + dz * dz;
                if (dist < bestDist) { bestDist = dist; best = node; }
            }
        }

        return best;
    }

    // ── Visual construction ───────────────────────────────────────────────────

    private void BuildVisual()
    {
        // Flat rock cube.
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Mesh";
        cube.transform.SetParent(transform, false);
        cube.transform.localPosition = new Vector3(0f, NodeH * 0.5f, 0f);
        cube.transform.localScale    = new Vector3(NodeW, NodeH, NodeD);
        Object.Destroy(cube.GetComponent<Collider>());

        Color col = _palette.TryGetValue(ResourceId, out var c) ? c : _fallbackColor;
        var mat = new Material(Shader.Find("Standard"));
        mat.color = col;
        cube.GetComponent<Renderer>().material = mat;

        // Collider on the parent so raycasts can hit the node (e.g. future scanning).
        var bc       = gameObject.AddComponent<BoxCollider>();
        bc.center    = new Vector3(0f, NodeH * 0.5f, 0f);
        bc.size      = new Vector3(NodeW, NodeH, NodeD);

        // World-space label.
        BuildLabel(col);
    }

    private void BuildLabel(Color nodeColor)
    {
        // A small world-space Canvas pinned above the node.
        var canvasGO = new GameObject("Label");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = new Vector3(0f, NodeH + 0.12f, 0f);
        canvasGO.transform.localScale    = Vector3.one * 0.004f;   // canvas units → world scale

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 40f);

        // Billboard: face the camera every frame.
        canvasGO.AddComponent<Billboard>();

        // Text.
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(canvasGO.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = ToDisplayName(ResourceId);
        tmp.fontSize  = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        if (UIRoot.Font != null) tmp.font = UIRoot.Font;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // "iron_ore" → "Iron Ore"
    private static string ToDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        var parts = id.Split('_');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0)
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
        return string.Join(" ", parts);
    }
}
