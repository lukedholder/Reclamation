// M1 test harness for the WorldGenTesting scene.
//
// Generates a single voxel region from a seed, meshes it (smooth Surface Nets), and lets
// you sculpt it: LEFT-click digs, RIGHT-click adds. A flat plane marks sea level so ocean
// basins read at a glance. Tune the noise/shell fields in the Inspector and use the
// component context-menu "Regenerate" to rebuild.
//
// Setup: drop this on an empty GameObject in WorldGenTesting. Ensure the scene has a
//        Main Camera (to aim with the mouse) and a Directional Light (to see the shape).
//
// This is deliberately one region (no streaming/seams yet) — that is M2.

using UnityEngine;

public class VoxelWorldDriver : MonoBehaviour
{
    [Header("World")]
    public int seed = 1337;

    [Tooltip("Region width/depth in cells (1 cell = 0.5 m). 64 ≈ 32 m across.")]
    public int regionXZ = 64;
    [Tooltip("Metres of diggable rock kept below the surface (room for tunnels).")]
    public float metresBelowSurface = 20f;
    [Tooltip("Metres of air kept above the highest surface.")]
    public float skyMargin = 6f;

    [Header("Terrain shape")]
    public float amplitude = 10f;
    public float terrainFrequency = 0.02f;
    public float seaLevel = -4f;

    [Header("Brush")]
    public float brushRadius = 2f;
    [Range(0.1f, 2f)] public float brushStrength = 1f;

    [Header("Visual")]
    public Material terrainMaterial;
    public Material waterMaterial;
    public bool showWater = true;

    private VoxelWorld     _world;
    private VoxelChunkView _chunk;
    private GameObject     _water;
    private Camera         _cam;

    private void Start()
    {
        _cam = Camera.main;
        Generate();
    }

    [ContextMenu("Regenerate")]
    public void Generate()
    {
        _world = new VoxelWorld(seed)
        {
            Amplitude        = amplitude,
            TerrainFrequency = terrainFrequency,
            SeaLevel         = seaLevel,
        };

        float vs = _world.VoxelSize;
        // Box region: regionXZ wide/deep; tall enough to cover the surface band plus
        // diggable rock below and air above.
        int sizeY   = Mathf.CeilToInt((2f * amplitude + metresBelowSurface + skyMargin) / vs);
        int originY = Mathf.FloorToInt((_world.BaseHeight - amplitude - metresBelowSurface) / vs);
        int half    = regionXZ / 2;
        var origin  = new GridPos(-half, originY, -half);

        if (_chunk == null)
        {
            var go = new GameObject("VoxelChunk");
            go.transform.SetParent(transform, false);
            _chunk = go.AddComponent<VoxelChunkView>();
        }
        _chunk.GetComponent<MeshRenderer>().sharedMaterial =
            terrainMaterial != null ? terrainMaterial : DefaultMaterial(new Color(0.55f, 0.55f, 0.58f));
        _chunk.Init(_world, origin, regionXZ, sizeY, regionXZ);

        BuildWater(half * vs);
    }

    private void BuildWater(float halfMetres)
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
        // Unity's plane is 10 m; scale to span the region.
        float scale = (halfMetres * 2f) / 10f;
        _water.transform.localScale    = new Vector3(scale, 1f, scale);
        _water.transform.localPosition = new Vector3(0f, seaLevel, 0f);
    }

    private void Update()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null || _world == null) return;

        bool dig = GameInput.PrimaryDown;
        bool add = GameInput.SecondaryDown;
        if (!dig && !add) return;

        var ray = _cam.ScreenPointToRay(GameInput.MousePosition);
        if (!Physics.Raycast(ray, out var hit, 1000f)) return;

        var p = hit.point;
        if (dig) _world.Carve(p.x, p.y, p.z, brushRadius, brushStrength);
        else     _world.Fill (p.x, p.y, p.z, brushRadius, brushStrength);
        _chunk.Rebuild();
    }

    private static Material DefaultMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = color;
        return mat;
    }
}
