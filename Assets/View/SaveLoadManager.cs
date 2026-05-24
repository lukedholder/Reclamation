// Saves and loads the full scene (all constructs and their blocks) to a JSON file.
//
// Setup: attach to any persistent GameObject (e.g. GameManager).
//
// Controls:
//   F5   — save to persistentDataPath/save.json
//   F9   — load from save.json (clears the current scene first)

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static ViewConstants;

public class SaveLoadManager : MonoBehaviour
{
    private static string SavePath =>
        Path.Combine(Application.persistentDataPath, "save.json");

    // Built once from BlockCatalogue.All() — automatically includes any new block types.
    private static readonly Dictionary<string, BlockDefinition> DefById = BuildDefById();

    private static Dictionary<string, BlockDefinition> BuildDefById()
    {
        var dict = new Dictionary<string, BlockDefinition>();
        foreach (var def in BlockCatalogue.All())
            dict[def.Id] = def;
        return dict;
    }

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) Save();
        if (Input.GetKeyDown(KeyCode.F9)) Load();
    }

    // ── Serialisable data structures ──────────────────────────────────────────

    [System.Serializable]
    private class SaveFile
    {
        public List<ConstructData> constructs = new List<ConstructData>();
    }

    [System.Serializable]
    private class ConstructData
    {
        public float originX, originY, originZ;
        public List<BlockData> blocks = new List<BlockData>();
    }

    [System.Serializable]
    private class BlockData
    {
        public string defId;
        public int gx, gy, gz, rot;
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    private void Save()
    {
        var file = new SaveFile();

        foreach (var cv in FindObjectsOfType<ConstructView>())
        {
            var cd = new ConstructData
            {
                originX = cv.transform.position.x,
                originY = cv.transform.position.y,
                originZ = cv.transform.position.z,
            };

            foreach (Transform child in cv.transform)
            {
                var bv = child.GetComponent<BlockView>();
                if (bv == null) continue;

                var b = bv.Block;
                cd.blocks.Add(new BlockData
                {
                    defId = b.Definition.Id,
                    gx    = b.GridPosition.X,
                    gy    = b.GridPosition.Y,
                    gz    = b.GridPosition.Z,
                    rot   = b.RotationSteps,
                });
            }

            file.constructs.Add(cd);
        }

        File.WriteAllText(SavePath, JsonUtility.ToJson(file, prettyPrint: true));
        Debug.Log($"[Save] {file.constructs.Count} construct(s) → {SavePath}");
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    private void Load()
    {
        if (!File.Exists(SavePath))
        {
            Debug.LogWarning("[Load] No save file found at " + SavePath);
            return;
        }

        // Clear the scene.
        foreach (var cv in FindObjectsOfType<ConstructView>())
            Destroy(cv.gameObject);

        // Fresh simulation — old block/construct IDs are discarded.
        var sim = GameManager.Instance.ResetSimulation();

        // Rebuild from file.
        var file       = JsonUtility.FromJson<SaveFile>(File.ReadAllText(SavePath));
        int blockCount = 0;

        foreach (var cd in file.constructs)
        {
            var simConstruct = sim.CreateConstruct();

            var cvGO = new GameObject();
            cvGO.transform.position = new Vector3(cd.originX, cd.originY, cd.originZ);
            var cv = cvGO.AddComponent<ConstructView>();
            cv.Init(simConstruct);

            foreach (var bd in cd.blocks)
            {
                if (!DefById.TryGetValue(bd.defId, out var def))
                {
                    Debug.LogWarning($"[Load] Unknown block id '{bd.defId}' — skipped.");
                    continue;
                }

                var gridPos = new GridPos(bd.gx, bd.gy, bd.gz);
                var block   = sim.PlaceBlock(def, simConstruct.Id, gridPos, bd.rot);

                bool swap = (bd.rot & 1) == 1;
                int sx = swap ? def.SizeZ : def.SizeX;
                int sy = def.SizeY;
                int sz = swap ? def.SizeX : def.SizeZ;

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = def.DisplayName;
                go.transform.SetParent(cv.transform, worldPositionStays: false);
                go.transform.localPosition = new Vector3(
                    (bd.gx + sx * 0.5f) * CellSize,
                    (bd.gy + sy * 0.5f) * CellSize,
                    (bd.gz + sz * 0.5f) * CellSize);
                go.transform.localScale = new Vector3(sx * CellSize, sy * CellSize, sz * CellSize);
                go.AddComponent<BlockView>().Init(block);
                blockCount++;
            }
        }

        Debug.Log($"[Load] {file.constructs.Count} construct(s), {blockCount} block(s) restored.");
    }
}
