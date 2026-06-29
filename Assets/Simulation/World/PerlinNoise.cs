// Deterministic 3D Perlin noise (Ken Perlin's "improved noise"), pure C#.
//
// Replaces the prototype's NoiseUtil, which used UnityEngine.Mathf.PerlinNoise — that
// is 2D-only and not guaranteed identical across platforms. This version is seeded from
// an int (stable permutation) and lives in the simulation layer so world generation is
// fully deterministic and Unity-free: same seed → same planet, everywhere, always.

public sealed class PerlinNoise
{
    private readonly int[] _perm = new int[512];

    public PerlinNoise(int seed)
    {
        var p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;

        // Deterministic Fisher–Yates shuffle of the permutation table.
        var rng = new System.Random(seed);
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
    }

    // ── Single-octave Perlin, result in roughly [-1, 1] ─────────────────────────
    public float Noise(float x, float y, float z)
    {
        int X = (int)System.Math.Floor(x) & 255;
        int Y = (int)System.Math.Floor(y) & 255;
        int Z = (int)System.Math.Floor(z) & 255;

        x -= (float)System.Math.Floor(x);
        y -= (float)System.Math.Floor(y);
        z -= (float)System.Math.Floor(z);

        float u = Fade(x), v = Fade(y), w = Fade(z);

        int A  = _perm[X] + Y,  AA = _perm[A] + Z,  AB = _perm[A + 1] + Z;
        int B  = _perm[X + 1] + Y, BA = _perm[B] + Z, BB = _perm[B + 1] + Z;

        return Lerp(
            Lerp(Lerp(Grad(_perm[AA],     x,     y,     z),
                      Grad(_perm[BA],     x - 1, y,     z), u),
                 Lerp(Grad(_perm[AB],     x,     y - 1, z),
                      Grad(_perm[BB],     x - 1, y - 1, z), u), v),
            Lerp(Lerp(Grad(_perm[AA + 1], x,     y,     z - 1),
                      Grad(_perm[BA + 1], x - 1, y,     z - 1), u),
                 Lerp(Grad(_perm[AB + 1], x,     y - 1, z - 1),
                      Grad(_perm[BB + 1], x - 1, y - 1, z - 1), u), v),
            w);
    }

    // ── Fractal Brownian motion (octave sum), result in roughly [-1, 1] ─────────
    // ridge = true folds each octave into ridged features (|n| flipped) — e.g. mountains.
    public float Fbm(float x, float y, float z,
                     int octaves, float frequency, float lacunarity, float gain, bool ridge)
    {
        float sum = 0f, amp = 1f, norm = 0f, f = frequency;
        for (int i = 0; i < octaves; i++)
        {
            float n = Noise(x * f, y * f, z * f);
            if (ridge)
            {
                n = 1f - System.Math.Abs(n);
                n = n * 2f - 1f;
            }
            sum  += n * amp;
            norm += amp;
            amp  *= gain;
            f    *= lacunarity;
        }
        return norm > 0f ? sum / norm : 0f;
    }

    // ── Perlin helpers ──────────────────────────────────────────────────────────
    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    private static float Lerp(float a, float b, float t) => a + t * (b - a);

    private static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }
}
