// M3 (stage 1): streams a spherical voxel planet around a target.
//
// Same machinery as VoxelChunkStreamer (pool, queue, per-frame budget, dig/add, world-
// space seam-free meshing) but with 3D cube chunks and a spherical density field. The
// planet is centred on the world origin; most chunks are fully solid (deep) or fully air
// (sky), so the streamer only loads chunks whose box overlaps the surface shell
// (VoxelWorld.ChunkOverlapsSurface) — a thin shell of chunks around the player.
//
// NOT YET (stage 2/3): the player-centric "rotate the planet underneath" frame (planet is
// rendered at the world origin here) and a sphere-walking character controller. Radial
// gravity is provided separately by RadialGravityBody for vehicles/props.
//
// Setup: put this on a GameObject AT THE WORLD ORIGIN. Place the Main Camera just above
//        the surface (e.g. y ≈ Radius + 10) looking at the planet, plus a Directional
//        Light. Fly the camera to stream; LEFT-click digs, RIGHT-click adds.

using System.Collections.Generic;
using UnityEngine;

public class VoxelPlanetStreamer : MonoBehaviour
{
    [Header("Planet")]
    public int   seed = 1337;
    public float radius = 300f;
    public float amplitude = 18f;
    [Tooltip("Lower = bigger continents. ~0.02 gives a few dozen features around a 300 m planet.")]
    public float terrainFrequency = 0.02f;
    public float seaLevel = -3f;   // metres relative to mean radius

    [Header("Chunks")]
    public int chunkSize = 32;
    [Tooltip("Load chunks within this many chunks of the target (3D ball).")]
    public int viewRadius = 5;
    public int unloadPadding = 1;
    public int maxBuildsPerFrame = 1;

    [Header("Origin")]
    public Transform streamTarget;
    [Tooltip("If the target starts inside the planet, lift it above the surface on Start.")]
    public bool liftTargetOnStart = true;

    [Header("Brush")]
    public float brushRadius = 2f;
    [Range(0.1f, 2f)] public float brushStrength = 1f;

    [Header("Visual")]
    public Material terrainMaterial;
    public Material waterMaterial;
    public bool     showSea = true;

    private VoxelWorld _world;
    private Camera     _cam;
    private Material   _terrainMat;
    private GameObject _sea;

    private readonly Dictionary<Vector3Int, VoxelChunkView> _loaded  = new Dictionary<Vector3Int, VoxelChunkView>();
    private readonly Stack<VoxelChunkView>                  _pool    = new Stack<VoxelChunkView>();
    private readonly List<Vector3Int>                       _queue   = new List<Vector3Int>();
    private readonly HashSet<Vector3Int>                    _queued  = new HashSet<Vector3Int>();
    private readonly List<Vector3Int>                       _scratch = new List<Vector3Int>();

    private Vector3Int _center;

    private void Start()
    {
        _cam = Camera.main;
        BuildWorld();

        // So the test doesn't open to an empty scene with the camera buried in rock.
        if (liftTargetOnStart)
        {
            var t = ResolveTarget();
            if (t != null && (t.position - PlanetMath.Centre).magnitude < radius)
                t.position = PlanetMath.Centre + Vector3.up * (radius + 10f);
        }
    }

    [ContextMenu("Rebuild World")]
    public void BuildWorld()
    {
        _world = new VoxelWorld(seed)
        {
            Spherical        = true,
            Radius           = radius,
            Amplitude        = amplitude,
            TerrainFrequency = terrainFrequency,
        };
        _terrainMat = terrainMaterial != null ? terrainMaterial : DefaultMaterial(new Color(0.55f, 0.55f, 0.58f));

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
        UpdateSea();
    }

    // ── Streaming ────────────────────────────────────────────────────────────────

    private void UpdateLoadSet()
    {
        int unloadR = viewRadius + unloadPadding;
        _scratch.Clear();
        foreach (var kv in _loaded)
            if ((kv.Key - _center).sqrMagnitude > unloadR * unloadR)
                _scratch.Add(kv.Key);
        foreach (var c in _scratch) { Recycle(_loaded[c]); _loaded.Remove(c); }

        int span = chunkSize + 1;
        for (int dx = -viewRadius; dx <= viewRadius; dx++)
        for (int dy = -viewRadius; dy <= viewRadius; dy++)
        for (int dz = -viewRadius; dz <= viewRadius; dz++)
        {
            if (dx * dx + dy * dy + dz * dz > viewRadius * viewRadius) continue;   // 3D ball
            var c = new Vector3Int(_center.x + dx, _center.y + dy, _center.z + dz);
            if (_loaded.ContainsKey(c) || _queued.Contains(c)) continue;
            if (!_world.ChunkOverlapsSurface(c.x * chunkSize, c.y * chunkSize, c.z * chunkSize, span, span, span))
                continue;   // deep solid or open sky — never has geometry
            _queued.Add(c);
            _queue.Add(c);
        }
    }

    private void ProcessQueue()
    {
        int builds = 0;
        while (builds < maxBuildsPerFrame && _queue.Count > 0)
        {
            Vector3Int bestCoord = default;
            int bestDist = int.MaxValue;
            bool found = false;
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                int d = (_queue[i] - _center).sqrMagnitude;
                if (d > viewRadius * viewRadius)
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
            if (!_loaded.ContainsKey(bestCoord)) { LoadChunk(bestCoord); builds++; }
        }
    }

    private void LoadChunk(Vector3Int coord)
    {
        var view = _pool.Count > 0 ? _pool.Pop() : NewChunkView();
        view.gameObject.SetActive(true);
        view.gameObject.name = $"Chunk_{coord.x}_{coord.y}_{coord.z}";
        view.GetComponent<MeshRenderer>().sharedMaterial = _terrainMat;

        var origin = new GridPos(coord.x * chunkSize, coord.y * chunkSize, coord.z * chunkSize);
        view.Init(_world, origin, chunkSize + 1, chunkSize + 1, chunkSize + 1);   // +1 overlap → seamless
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
        bool dig = GameInput.PrimaryDown, add = GameInput.SecondaryDown;
        if (!dig && !add) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        var ray = _cam.ScreenPointToRay(GameInput.MousePosition);
        if (!Physics.Raycast(ray, out var hit, 5000f)) return;

        var p = hit.point;
        if (dig) _world.Carve(p.x, p.y, p.z, brushRadius, brushStrength);
        else     _world.Fill (p.x, p.y, p.z, brushRadius, brushStrength);
        RebuildAround(p, brushRadius);
    }

    private void RebuildAround(Vector3 center, float radius)
    {
        float m = chunkSize * _world.VoxelSize;
        float pad = radius + _world.VoxelSize;
        int x0 = Mathf.FloorToInt((center.x - pad) / m) - 1, x1 = Mathf.FloorToInt((center.x + pad) / m);
        int y0 = Mathf.FloorToInt((center.y - pad) / m) - 1, y1 = Mathf.FloorToInt((center.y + pad) / m);
        int z0 = Mathf.FloorToInt((center.z - pad) / m) - 1, z1 = Mathf.FloorToInt((center.z + pad) / m);

        for (int cx = x0; cx <= x1; cx++)
        for (int cy = y0; cy <= y1; cy++)
        for (int cz = z0; cz <= z1; cz++)
            if (_loaded.TryGetValue(new Vector3Int(cx, cy, cz), out var view))
                view.Rebuild();
    }

    // ── Sea (a translucent sphere at the sea radius) ────────────────────────────────

    private void UpdateSea()
    {
        if (!showSea)
        {
            if (_sea != null) _sea.SetActive(false);
            return;
        }
        if (_sea == null)
        {
            _sea = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sea.name = "Sea";
            _sea.transform.SetParent(transform, false);
            Destroy(_sea.GetComponent<Collider>());
            _sea.GetComponent<MeshRenderer>().sharedMaterial =
                waterMaterial != null ? waterMaterial : DefaultMaterial(new Color(0.15f, 0.35f, 0.6f));
        }
        _sea.SetActive(true);
        float seaR = radius + seaLevel;
        _sea.transform.position   = Vector3.zero;
        _sea.transform.localScale = Vector3.one * (seaR * 2f);   // primitive sphere diameter = 1
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private Transform ResolveTarget()
    {
        if (streamTarget != null) return streamTarget;
        if (_cam == null) _cam = Camera.main;
        return _cam != null ? _cam.transform : null;
    }

    private Vector3Int WorldToChunk(Vector3 p)
    {
        float m = chunkSize * _world.VoxelSize;
        return new Vector3Int(Mathf.FloorToInt(p.x / m), Mathf.FloorToInt(p.y / m), Mathf.FloorToInt(p.z / m));
    }

    private static Material DefaultMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        return new Material(shader) { color = color };
    }
}
