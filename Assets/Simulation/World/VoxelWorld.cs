// Authoritative voxel field — pure C#, deterministic from a seed. Knows nothing about
// Unity meshes; it only answers "how solid is this lattice point, and what is it made of".
//
// CONVENTION: Density > 0 is solid rock, <= 0 is air; the surface is the Density = 0
// isosurface. The view layer (SurfaceNets) meshes that isosurface.
//
// SHELL MODEL (per the design): only a band around the surface is real voxel data.
//   • Far below the local surface  → solid floor (you can dig down ShellDepth metres).
//   • Far above the local surface   → open air.
//   • In between → procedural terrain; an ocean basin forms wherever the surface dips
//     below SeaLevel. (Cave/tunnel carving is deferred — see WorldGenSystem.md.)
//
// This reuses the prototype's heightmap noise design (continents/mountains/detail FBM)
// as the density source, so the planet shape is the same — just diggable.

using System.Collections.Generic;

public sealed class VoxelWorld
{
    // ── Identity / scale ────────────────────────────────────────────────────────
    public readonly int   Seed;
    public readonly float VoxelSize;      // metres per voxel (matches the 0.5 m cell lattice)

    // ── Terrain shape (metres) ──────────────────────────────────────────────────
    public float BaseHeight       = 0f;
    public float Amplitude        = 10f;  // surface height variation
    public float TerrainFrequency = 0.02f;
    public int   Octaves          = 5;
    public float Lacunarity       = 2f;
    public float Gain             = 0.5f;

    // ── Planet (M3) ─────────────────────────────────────────────────────────────
    // When Spherical, the surface is a sphere of Radius metres centred on the lattice
    // origin, displaced by the same FBM; "down" is toward the centre. When false, the
    // flat M1/M2 heightmap framing is used.
    public bool  Spherical = false;
    public float Radius    = 300f;

    // ── Shell band (metres) ─────────────────────────────────────────────────────
    public float ShellDepth = 128f;       // diggable rock below the local surface, then solid
    public float ShellSky   = 24f;        // air kept above the local surface

    // ── Oceans ──────────────────────────────────────────────────────────────────
    public float SeaLevel = -4f;          // world Y of the ocean surface

    // Caves / tunnels (enemy nests) are deferred — see WorldGenSystem.md.

    // ── Ore pockets ─────────────────────────────────────────────────────────────
    public float OreFrequency = 0.08f;

    private readonly PerlinNoise _terrain;
    private readonly PerlinNoise _ore;

    // Player edits: voxel lattice point → density override. The only thing that needs
    // saving (the rest regenerates from the seed).
    private readonly Dictionary<GridPos, float> _edits = new Dictionary<GridPos, float>();
    public IReadOnlyDictionary<GridPos, float> Edits => _edits;

    public VoxelWorld(int seed, float voxelSize = 0.5f)
    {
        Seed      = seed;
        VoxelSize = voxelSize;
        _terrain  = new PerlinNoise(seed);
        _ore      = new PerlinNoise(seed ^ 0x6C8E9CF5);
    }

    // ── Surface height (world Y) at a world XZ position ─────────────────────────
    public float SurfaceHeight(float worldX, float worldZ)
        => BaseHeight + _terrain.Fbm(worldX, 0f, worldZ,
                                     Octaves, TerrainFrequency, Lacunarity, Gain, ridge: false) * Amplitude;

    // ── Density at a voxel lattice point (integer corner) ───────────────────────
    public float Density(int gx, int gy, int gz)
    {
        if (_edits.TryGetValue(new GridPos(gx, gy, gz), out float overridden))
            return overridden;
        return BaseDensity(gx, gy, gz);
    }

    private float BaseDensity(int gx, int gy, int gz)
    {
        if (!Spherical)
            return DensityFromSurface(SurfaceHeight(gx * VoxelSize, gz * VoxelSize), gy * VoxelSize);

        float px = gx * VoxelSize, py = gy * VoxelSize, pz = gz * VoxelSize;
        float r  = (float)System.Math.Sqrt(px * px + py * py + pz * pz);
        if (r < 1e-3f) return 1f;                          // planet centre → solid
        float scale = Radius / r;                          // unit direction → point on the sphere
        float surfaceR = Radius + _terrain.Fbm(px * scale, py * scale, pz * scale,
                                               Octaves, TerrainFrequency, Lacunarity, Gain, false) * Amplitude;
        return DensityFromSurface(surfaceR, r);            // "surface" is now a radius, "wy" is |p|
    }

    // Signed density given a column's surface height and the sample's world Y.
    // Shell-clamped: solid floor far below, open air far above, signed terrain between.
    private float DensityFromSurface(float surface, float wy)
    {
        if (wy <= surface - ShellDepth) return  1f;   // solid floor
        if (wy >= surface + ShellSky)   return -1f;   // open air
        return Clamp((surface - wy) / VoxelSize, -1f, 1f);
    }

    // Batch-fills a region's density into `dest` (layout [(x*spanY + y)*spanZ + z]).
    // Evaluates the surface FBM ONCE per XZ column instead of per voxel — the bulk of the
    // generation cost — so a chunk build does ~spanX*spanZ noise evals, not spanX*spanY*spanZ.
    // Pure C# and side-effect-free over a fixed edit set, so it is safe to call off-thread.
    public void SampleDensity(int ox, int oy, int oz, int spanX, int spanY, int spanZ, float[] dest)
    {
        if (Spherical) { SampleDensitySpherical(ox, oy, oz, spanX, spanY, spanZ, dest); return; }

        bool hasEdits = _edits.Count > 0;
        for (int x = 0; x < spanX; x++)
        for (int z = 0; z < spanZ; z++)
        {
            float surface = SurfaceHeight((ox + x) * VoxelSize, (oz + z) * VoxelSize);
            int col = (x * spanY) * spanZ + z;
            for (int y = 0; y < spanY; y++)
            {
                int gy = oy + y;
                float d = DensityFromSurface(surface, gy * VoxelSize);
                if (hasEdits && _edits.TryGetValue(new GridPos(ox + x, gy, oz + z), out float e))
                    d = e;
                dest[col + y * spanZ] = d;
            }
        }
    }

    private void SampleDensitySpherical(int ox, int oy, int oz, int spanX, int spanY, int spanZ, float[] dest)
    {
        bool hasEdits = _edits.Count > 0;
        for (int x = 0; x < spanX; x++)
        for (int z = 0; z < spanZ; z++)
        {
            int col   = (x * spanY) * spanZ + z;
            float px  = (ox + x) * VoxelSize, pz = (oz + z) * VoxelSize;
            for (int y = 0; y < spanY; y++)
            {
                int gy = oy + y;
                float py = gy * VoxelSize;
                float r  = (float)System.Math.Sqrt(px * px + py * py + pz * pz);
                float d;
                if (r < 1e-3f) d = 1f;
                else
                {
                    float scale = Radius / r;
                    float surfaceR = Radius + _terrain.Fbm(px * scale, py * scale, pz * scale,
                                                           Octaves, TerrainFrequency, Lacunarity, Gain, false) * Amplitude;
                    d = DensityFromSurface(surfaceR, r);
                }
                if (hasEdits && _edits.TryGetValue(new GridPos(ox + x, gy, oz + z), out float e))
                    d = e;
                dest[col + y * spanZ] = d;
            }
        }
    }

    // True if a chunk box could contain the spherical surface (worth meshing). Lets the
    // planet streamer skip the many fully-solid (deep) and fully-air (sky) chunks.
    public bool ChunkOverlapsSurface(int ox, int oy, int oz, int spanX, int spanY, int spanZ)
    {
        if (!Spherical) return true;

        float x0 = ox * VoxelSize, x1 = (ox + spanX - 1) * VoxelSize;
        float y0 = oy * VoxelSize, y1 = (oy + spanY - 1) * VoxelSize;
        float z0 = oz * VoxelSize, z1 = (oz + spanZ - 1) * VoxelSize;

        float minR2 = NearAxis2(x0, x1) + NearAxis2(y0, y1) + NearAxis2(z0, z1);
        float maxR2 = FarAxis2(x0, x1)  + FarAxis2(y0, y1)  + FarAxis2(z0, z1);

        float surfMin = System.Math.Max(0f, Radius - Amplitude - VoxelSize);
        float surfMax = Radius + Amplitude + VoxelSize;
        return maxR2 >= surfMin * surfMin && minR2 <= surfMax * surfMax;
    }

    // Squared nearest / farthest |coordinate| of an interval [a,b] from 0.
    private static float NearAxis2(float a, float b)
    {
        if (a <= 0f && b >= 0f) return 0f;
        float m = System.Math.Min(System.Math.Abs(a), System.Math.Abs(b));
        return m * m;
    }
    private static float FarAxis2(float a, float b)
    {
        float m = System.Math.Max(System.Math.Abs(a), System.Math.Abs(b));
        return m * m;
    }

    // ── Material of a solid voxel ───────────────────────────────────────────────
    public VoxelMaterial MaterialAt(int gx, int gy, int gz)
    {
        if (Density(gx, gy, gz) <= 0f) return VoxelMaterial.Air;

        float wx = gx * VoxelSize, wy = gy * VoxelSize, wz = gz * VoxelSize;
        float depth = SurfaceHeight(wx, wz) - wy;

        float ore = _ore.Fbm(wx, wy, wz, 3, OreFrequency, 2f, 0.5f, ridge: false);
        if (depth > 4f && ore >  0.55f) return VoxelMaterial.IronOre;
        if (depth > 6f && ore >  0.45f) return VoxelMaterial.CopperOre;
        if (depth > 8f && ore < -0.60f) return VoxelMaterial.Coal;

        return depth < 1.5f ? VoxelMaterial.Dirt : VoxelMaterial.Stone;
    }

    // ── Edits: dig (carve) and build (fill) ─────────────────────────────────────
    public void Carve(float worldX, float worldY, float worldZ, float radius, float strength)
        => Edit(worldX, worldY, worldZ, radius, -strength);

    public void Fill(float worldX, float worldY, float worldZ, float radius, float strength)
        => Edit(worldX, worldY, worldZ, radius, +strength);

    private void Edit(float worldX, float worldY, float worldZ, float radius, float delta)
    {
        int cx = (int)System.Math.Round(worldX / VoxelSize);
        int cy = (int)System.Math.Round(worldY / VoxelSize);
        int cz = (int)System.Math.Round(worldZ / VoxelSize);
        int r  = (int)System.Math.Ceiling(radius / VoxelSize) + 1;

        for (int gx = cx - r; gx <= cx + r; gx++)
        for (int gy = cy - r; gy <= cy + r; gy++)
        for (int gz = cz - r; gz <= cz + r; gz++)
        {
            float dx = gx * VoxelSize - worldX;
            float dy = gy * VoxelSize - worldY;
            float dz = gz * VoxelSize - worldZ;
            float dist = (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dist > radius) continue;

            float falloff = 1f - dist / radius;
            float current = Density(gx, gy, gz);
            float updated = Clamp(current + delta * falloff, -1f, 1f);
            _edits[new GridPos(gx, gy, gz)] = updated;
        }
    }

    private static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
}
