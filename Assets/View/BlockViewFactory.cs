// Creates the scene GameObject for a placed block.
//
// If a designer prefab is registered for the block id (via BlockRegistry / a
// BlockLibrary), it is instantiated; otherwise a sized primitive cube is used as a
// placeholder — the same cube the game shipped before designer blocks existed.
//
// The caller is responsible for parenting the result, setting localPosition, and
// attaching/initialising the BlockView.

using UnityEngine;
using static ViewConstants;

public static class BlockViewFactory
{
    public static GameObject Create(BlockDefinition def, int rotSteps)
    {
        bool swap = (rotSteps & 1) == 1;
        int sx = swap ? def.SizeZ : def.SizeX;
        int sy = def.SizeY;
        int sz = swap ? def.SizeX : def.SizeZ;

        var prefab = BlockRegistry.GetPrefab(def.Id);
        if (prefab != null)
        {
            var go = Object.Instantiate(prefab);
            go.name = def.DisplayName;
            // Footprint swap is expressed as a Y rotation so non-symmetric meshes face right.
            go.transform.localRotation = Quaternion.Euler(0f, rotSteps * 90f, 0f);
            EnsureCollider(go, def);
            return go;
        }

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = def.DisplayName;
        cube.transform.localScale = new Vector3(sx, sy, sz) * CellSize;
        return cube;
    }

    // Designer prefabs are expected to carry their own collider; add a footprint-sized
    // box as a fallback so placement/dismantle raycasts still work if one is missing.
    private static void EnsureCollider(GameObject go, BlockDefinition def)
    {
        if (go.GetComponentInChildren<Collider>() != null) return;
        var bc = go.AddComponent<BoxCollider>();
        bc.size = new Vector3(def.SizeX, def.SizeY, def.SizeZ) * CellSize;
    }
}
