// M2: streams voxel terrain around a moving origin.
//
// The world is tiled into COLUMN chunks: chunkSize×chunkSize cells in XZ, full shell
// height in Y (flat framing — at M3 the planet uses 3D cube-sphere chunks). Chunks within
// viewRadius of the target are meshed; chunks beyond viewRadius + unloadPadding are
// recycled to a pool. Builds are budgeted per frame to avoid hitches.
//
// SEAMLESS BOUNDARIES: each chunk meshes one extra cell on its +X/+Z sides. Because the
// density field is global and deterministic (VoxelWorld), neighbouring chunks produce
// bit-identical coincident geometry at the shared plane — no gaps, no z-fighting. Proper
// single-ownership stitching is a later optimisation.
//
// NOTE: only a view radius is modelled here (terrain meshes). The SDD's separate, larger
// SIMULATION radius (constructs/factory keep ticking) applies when the factory integrates
// with terrain (M4).
//
// Setup: replace VoxelWorldDriver with this on the WorldGenTesting GameObject. Needs a
//        Main Camera (to aim digs) and a Directional Light. Move the camera/player to
//        watch chunks stream. LEFT-click digs, RIGHT-click adds.

using System.Collections.Generic;
using UnityEngine;

public class VoxelChunkStreamer : MonoBehaviour
{
    [Header("World")]
    public int   seed = 1337;
    public float amplitude = 10f;
    public float terrainFrequency = 0.02f;
    public float seaLevel = -4f;

    [Header("Chunks")]
    [Tooltip("Cells per chunk side in XZ. 32 cells = 16 m (matches the design).")]
    public int chunkSize = 32;
    [Tooltip("Metres of diggable rock kept below the surface (column depth).")]
    public float metresBelowSurface = 20f;
    [Tooltip("Metres of air kept above the highest surface.")]
    public float skyMargin = 6f;
    [Tooltip("Load chunks within this many chunks of the target (XZ).")]
    public int viewRadius = 4;
    [Tooltip("Extra chunks of slack before unloading (hysteresis).")]
    public int unloadPadding = 1;
    [Tooltip("Max chunk (re)builds per frame.")]
    public int maxBuildsPerFrame = 2;

    [Header("Origin")]
    [Tooltip("What to stream around. Defaults to the main camera.")]
    public Transform streamTarget;

    [Header("Brush")]
    public float brushRadius = 2f;
    [Range(0.1f, 2f)] public float brushStrength = 1f;

    [Header("Visual")]
    public Material terrainMaterial;
    public Material waterMaterial;
    public bool     showWater = true;

    private VoxelWorld _world;
    private Camera     _cam;
    private Material   _terrainMat;
    private GameObject _water;

    private int _originYCells;       // lattice Y where each column starts
    private int _columnHeightCells;  // cells per column in Y

    private readonly Dictionary<Vector2Int, VoxelChunkView> _loaded = new Dictionary<Vector2Int, VoxelChunkView>();
    private readonly Stack<VoxelChunkView>                   _pool   = new Stack<VoxelChunkView>();
    private readonly List<Vector2Int>                        _queue  = new List<Vector2Int>();
    private readonly HashSet<Vector2Int>                     _queued = new HashSet<Vector2Int>();
    private readonly List<Vector2Int>                        _scratch = new List<Vector2Int>();

    private Vector2Int _center;

    private void Start()
    {
        _cam = Camera.main;
        BuildWorld();
    }

    [ContextMenu("Rebuild World")]
    public void BuildWorld()
    {
        _world = new VoxelWorld(seed)
        {
            Amplitude        = amplitude,
            TerrainFrequency = terrainFrequency,
            SeaLevel         = seaLevel,
        };

        float vs = _world.VoxelSize;
        _columnHeightCells = Mathf.CeilToInt((2f * amplitude + metresBelowSurface + skyMargin) / vs);
        _originYCells      = Mathf.FloorToInt((_world.BaseHeight - amplitude - metresBelowSurface) / vs);
        _terrainMat        = terrainMaterial != null ? terrainMaterial : DefaultMaterial(new Color(0.55f, 0.55f, 0.58f));

        // Drop everything and start fresh (e.g. after a seed change).
        foreach (var kv in _loaded) Recycle(kv.Value);
        _loaded.Clear();
        _queue.Clear();
        _queued.Clear();
    }

    private void Update()
    {
        if (_world == null) return;

        var target = ResolveTarget();
        if (target == null) return;

        _center = WorldToChunk(target.position);
        UpdateLoadSet();
        ProcessQueue();
        HandleEdit();
        UpdateWater(target.position);
    }

    // ── Streaming ────────────────────────────────────────────────────────────────

    private void UpdateLoadSet()
    {
        // Unload chunks that drifted out of range (with hysteresis).
        _scratch.Clear();
        foreach (var kv in _loaded)
            if (Chebyshev(kv.Key, _center) > viewRadius + unloadPadding)
                _scratch.Add(kv.Key);
        foreach (var c in _scratch)
        {
            Recycle(_loaded[c]);
            _loaded.Remove(c);
        }

        // Enqueue missing in-range chunks.
        for (int dx = -viewRadius; dx <= viewRadius; dx++)
        for (int dz = -viewRadius; dz <= viewRadius; dz++)
        {
            var c = new Vector2Int(_center.x + dx, _center.y + dz);
            if (!_loaded.ContainsKey(c) && _queued.Add(c))
                _queue.Add(c);
        }
    }

    private void ProcessQueue()
    {
        int builds = 0;
        while (builds < maxBuildsPerFrame && _queue.Count > 0)
        {
            // Pick the nearest still-in-range queued chunk; drop ones that drifted away.
            // Track the coord (not an index) so mid-loop removals can't invalidate it.
            Vector2Int bestCoord = default;
            int bestDist = int.MaxValue;
            bool found = false;
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                int d = Chebyshev(_queue[i], _center);
                if (d > viewRadius)
                {
                    _queued.Remove(_queue[i]);
                    _queue.RemoveAt(i);
                    continue;
                }
                if (d < bestDist) { bestDist = d; bestCoord = _queue[i]; found = true; }
            }
            if (!found) break;

            _queue.Remove(bestCoord);
            _queued.Remove(bestCoord);

            if (!_loaded.ContainsKey(bestCoord))
            {
                LoadChunk(bestCoord);
                builds++;
            }
        }
    }

    private void LoadChunk(Vector2Int coord)
    {
        var view = _pool.Count > 0 ? _pool.Pop() : NewChunkView();
        view.gameObject.SetActive(true);
        view.gameObject.name = $"Chunk_{coord.x}_{coord.y}";
        view.GetComponent<MeshRenderer>().sharedMaterial = _terrainMat;

        var origin = new GridPos(coord.x * chunkSize, _originYCells, coord.y * chunkSize);
        // +1 cell on X and Z so neighbours meet seamlessly (see header).
        view.Init(_world, origin, chunkSize + 1, _columnHeightCells, chunkSize + 1);

        _loaded[coord] = view;
    }

    private VoxelChunkView NewChunkView()
    {
        var go = new GameObject("Chunk");
        go.transform.SetParent(transform, false);
        return go.AddComponent<VoxelChunkView>();
    }

    private void Recycle(VoxelChunkView view)
    {
        view.gameObject.SetActive(false);
        _pool.Push(view);
    }

    // ── Editing ──────────────────────────────────────────────────────────────────

    private void HandleEdit()
    {
        bool dig = GameInput.PrimaryDown;
        bool add = GameInput.SecondaryDown;
        if (!dig && !add) return;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        var ray = _cam.ScreenPointToRay(GameInput.MousePosition);
        if (!Physics.Raycast(ray, out var hit, 1000f)) return;

        var p = hit.point;
        if (dig) _world.Carve(p.x, p.y, p.z, brushRadius, brushStrength);
        else     _world.Fill (p.x, p.y, p.z, brushRadius, brushStrength);

        RebuildAround(p, brushRadius);
    }

    // Re-mesh every loaded chunk whose footprint (incl. the +1 overlap and the -side
    // neighbour that overlaps into it) could contain an edited voxel.
    private void RebuildAround(Vector3 center, float radius)
    {
        float m = chunkSize * _world.VoxelSize;
        float pad = radius + _world.VoxelSize;
        int cx0 = Mathf.FloorToInt((center.x - pad) / m) - 1;
        int cx1 = Mathf.FloorToInt((center.x + pad) / m);
        int cz0 = Mathf.FloorToInt((center.z - pad) / m) - 1;
        int cz1 = Mathf.FloorToInt((center.z + pad) / m);

        for (int cx = cx0; cx <= cx1; cx++)
        for (int cz = cz0; cz <= cz1; cz++)
            if (_loaded.TryGetValue(new Vector2Int(cx, cz), out var view))
                view.Rebuild();
    }

    // ── Water ──────────────────────────────────────────────────────────────────────

    private void UpdateWater(Vector3 targetPos)
    {
        if (!showWater)
        {
            if (_water != null) _water.SetActive(false);
            return;
        }
        if (_water == null)
        {
            _water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _water.name = "SeaLevel";
            _water.transform.SetParent(transform, false);
            Destroy(_water.GetComponent<Collider>());
            _water.GetComponent<MeshRenderer>().sharedMaterial =
                waterMaterial != null ? waterMaterial : DefaultMaterial(new Color(0.15f, 0.35f, 0.6f));
        }
        _water.SetActive(true);
        float span = (viewRadius * 2 + 2) * chunkSize * _world.VoxelSize;   // covers the view area
        _water.transform.localScale = new Vector3(span / 10f, 1f, span / 10f);
        _water.transform.position   = new Vector3(targetPos.x, seaLevel, targetPos.z);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private Transform ResolveTarget()
    {
        if (streamTarget != null) return streamTarget;
        if (_cam == null) _cam = Camera.main;
        return _cam != null ? _cam.transform : null;
    }

    private Vector2Int WorldToChunk(Vector3 p)
    {
        float m = chunkSize * _world.VoxelSize;
        return new Vector2Int(Mathf.FloorToInt(p.x / m), Mathf.FloorToInt(p.z / m));
    }

    private static int Chebyshev(Vector2Int a, Vector2Int b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    private static Material DefaultMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        return new Material(shader) { color = color };
    }

    private void OnDrawGizmosSelected()
    {
        if (_world == null) return;
        float m = chunkSize * _world.VoxelSize;
        float h = _columnHeightCells * _world.VoxelSize;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        foreach (var kv in _loaded)
        {
            var c = kv.Key;
            var pos = new Vector3((c.x + 0.5f) * m, _originYCells * _world.VoxelSize + h * 0.5f, (c.y + 0.5f) * m);
            Gizmos.DrawWireCube(pos, new Vector3(m, h, m));
        }
    }
}
