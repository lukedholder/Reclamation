// Naive Surface Nets mesher — turns a scalar density field into a smooth,
// dual-contoured surface (Space Engineers-like look) without the error-prone
// 256-row Marching Cubes triangle table.
//
// Input  : densityAt(cx,cy,cz) sampled at corner lattice points [0..cells]; >0 = solid.
// Output : one vertex per surface-straddling cell (placed at the average of its edge
//          crossings), quads stitched across grid edges that flip sign.
//
// Winding is made robust by comparing each quad's geometric normal against the density
// gradient direction and flipping if needed — so we never ship inside-out terrain.

using System;
using System.Collections.Generic;
using UnityEngine;

public static class SurfaceNets
{
    // 8 cell corners, indexed consistently with the edge table below.
    private static readonly Vector3Int[] Corner =
    {
        new Vector3Int(0,0,0), new Vector3Int(1,0,0), new Vector3Int(1,0,1), new Vector3Int(0,0,1),
        new Vector3Int(0,1,0), new Vector3Int(1,1,0), new Vector3Int(1,1,1), new Vector3Int(0,1,1),
    };

    // 12 edges as corner-index pairs (4 bottom, 4 top, 4 verticals).
    private static readonly int[,] Edge =
    {
        {0,1},{1,2},{2,3},{3,0}, {4,5},{5,6},{6,7},{7,4}, {0,4},{1,5},{2,6},{3,7}
    };

    // `origin` is the chunk's lattice corner. Vertices are emitted in WORLD space
    // (origin + local), so neighbouring chunks produce bit-identical boundary vertices
    // and meet without cracks — provided each chunk's GameObject transform is identity.
    public static void Build(Func<int,int,int,float> densityAt, Vector3Int origin,
                             int sizeX, int sizeY, int sizeZ,
                             float voxelSize, List<Vector3> vertices, List<int> triangles)
    {
        vertices.Clear();
        triangles.Clear();

        // cellVertex[x,y,z] = index of the vertex owned by that cell, or -1 if inactive.
        var cellVertex = new int[sizeX * sizeY * sizeZ];
        for (int i = 0; i < cellVertex.Length; i++) cellVertex[i] = -1;
        int CellIndex(int x, int y, int z) => (x * sizeY + y) * sizeZ + z;

        var d = new float[8];

        // ── Pass 1: one vertex per active cell, at the average of its edge crossings ──
        for (int x = 0; x < sizeX; x++)
        for (int y = 0; y < sizeY; y++)
        for (int z = 0; z < sizeZ; z++)
        {
            int mask = 0;
            for (int c = 0; c < 8; c++)
            {
                var o = Corner[c];
                d[c] = densityAt(x + o.x, y + o.y, z + o.z);
                if (d[c] > 0f) mask |= 1 << c;
            }
            if (mask == 0 || mask == 0xFF) continue;   // fully air or fully solid → no surface

            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int e = 0; e < 12; e++)
            {
                int a = Edge[e, 0], b = Edge[e, 1];
                bool sa = d[a] > 0f, sb = d[b] > 0f;
                if (sa == sb) continue;
                float t = d[a] / (d[a] - d[b]);        // linear zero-crossing along the edge
                sum += Vector3.Lerp(Corner[a], Corner[b], t);
                count++;
            }

            Vector3 world = ((Vector3)origin + new Vector3(x, y, z) + sum / count) * voxelSize;
            cellVertex[CellIndex(x, y, z)] = vertices.Count;
            vertices.Add(world);
        }

        // ── Pass 2: stitch quads across every grid edge that flips sign ──────────────
        for (int x = 0; x <= sizeX; x++)
        for (int y = 0; y <= sizeY; y++)
        for (int z = 0; z <= sizeZ; z++)
        {
            bool sHere = densityAt(x, y, z) > 0f;

            // Edge toward +X — quad spans the four cells sharing it (vary Y,Z).
            if (x < sizeX && y >= 1 && y < sizeY && z >= 1 && z < sizeZ)
                if (sHere != densityAt(x + 1, y, z) > 0f)
                    EmitQuad(vertices, triangles, cellVertex,
                             CellIndex(x, y - 1, z - 1), CellIndex(x, y, z - 1),
                             CellIndex(x, y, z),         CellIndex(x, y - 1, z),
                             sHere ? Vector3.right : Vector3.left);

            // Edge toward +Y (vary X,Z).
            if (y < sizeY && x >= 1 && x < sizeX && z >= 1 && z < sizeZ)
                if (sHere != densityAt(x, y + 1, z) > 0f)
                    EmitQuad(vertices, triangles, cellVertex,
                             CellIndex(x - 1, y, z - 1), CellIndex(x, y, z - 1),
                             CellIndex(x, y, z),         CellIndex(x - 1, y, z),
                             sHere ? Vector3.up : Vector3.down);

            // Edge toward +Z (vary X,Y).
            if (z < sizeZ && x >= 1 && x < sizeX && y >= 1 && y < sizeY)
                if (sHere != densityAt(x, y, z + 1) > 0f)
                    EmitQuad(vertices, triangles, cellVertex,
                             CellIndex(x - 1, y - 1, z), CellIndex(x, y - 1, z),
                             CellIndex(x, y, z),         CellIndex(x - 1, y, z),
                             sHere ? Vector3.forward : Vector3.back);
        }
    }

    // Emits the two triangles of a quad from four cell vertices, orienting the winding
    // so the surface normal points the same way as `outward` (solid → air).
    private static void EmitQuad(List<Vector3> verts, List<int> tris, int[] cellVertex,
                                 int c0, int c1, int c2, int c3, Vector3 outward)
    {
        int i0 = cellVertex[c0], i1 = cellVertex[c1], i2 = cellVertex[c2], i3 = cellVertex[c3];
        if (i0 < 0 || i1 < 0 || i2 < 0 || i3 < 0) return;   // boundary edge — neighbour meshes it

        Vector3 geo = Vector3.Cross(verts[i2] - verts[i0], verts[i3] - verts[i1]);
        if (Vector3.Dot(geo, outward) >= 0f)
        {
            tris.Add(i0); tris.Add(i1); tris.Add(i2);
            tris.Add(i0); tris.Add(i2); tris.Add(i3);
        }
        else
        {
            tris.Add(i0); tris.Add(i2); tris.Add(i1);
            tris.Add(i0); tris.Add(i3); tris.Add(i2);
        }
    }
}
