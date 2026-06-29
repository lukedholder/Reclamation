// Renders one rectangular region of a VoxelWorld as a smooth Surface Nets mesh with a
// matching MeshCollider. For M1 the driver uses a single large region (no inter-chunk
// seams); the same component becomes the per-chunk view when streaming arrives (M2).
//
// Density is sampled once into a local array each Rebuild so re-meshing after a dig is
// fast (no repeated noise evaluation inside the mesher).

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VoxelChunkView : MonoBehaviour
{
    private VoxelWorld   _world;
    private GridPos      _origin;     // lattice corner this region starts at
    private int          _sx, _sy, _sz;  // cells per axis
    private float[]      _density;    // cached corner samples, (sx+1)(sy+1)(sz+1)
    private Mesh         _mesh;
    private MeshCollider _collider;

    private readonly List<Vector3> _verts = new List<Vector3>();
    private readonly List<int>     _tris  = new List<int>();

    public void Init(VoxelWorld world, GridPos origin, int sizeX, int sizeY, int sizeZ)
    {
        _world  = world;
        _origin = origin;
        _sx = sizeX; _sy = sizeY; _sz = sizeZ;

        // Reuse the mesh across Init calls so pooled chunks don't leak a Mesh each reload.
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "VoxelChunk", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        _collider = GetComponent<MeshCollider>();
        if (_collider == null) _collider = gameObject.AddComponent<MeshCollider>();

        // Vertices are emitted in world space (seam-precise across chunks), so the
        // GameObject transform stays at the world origin. Streamer should be at (0,0,0).
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        Rebuild();
    }

    public void Rebuild()
    {
        int spanX = _sx + 1, spanY = _sy + 1, spanZ = _sz + 1;
        int total = spanX * spanY * spanZ;
        if (_density == null || _density.Length != total)
            _density = new float[total];

        for (int x = 0; x < spanX; x++)
        for (int y = 0; y < spanY; y++)
        for (int z = 0; z < spanZ; z++)
            _density[(x * spanY + y) * spanZ + z] =
                _world.Density(_origin.X + x, _origin.Y + y, _origin.Z + z);

        SurfaceNets.Build(
            (cx, cy, cz) => _density[(cx * spanY + cy) * spanZ + cz],
            new Vector3Int(_origin.X, _origin.Y, _origin.Z),
            _sx, _sy, _sz, _world.VoxelSize, _verts, _tris);

        _mesh.Clear();
        _mesh.SetVertices(_verts);
        _mesh.SetTriangles(_tris, 0);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        // Reassign so the collider picks up the new mesh.
        _collider.sharedMesh = null;
        _collider.sharedMesh = _mesh;
    }
}
