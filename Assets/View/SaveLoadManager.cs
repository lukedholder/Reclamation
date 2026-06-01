// Saves and loads the full scene to a JSON file.
//
// What is saved:
//   • Constructs — world position + Y-axis rotation
//   • Blocks     — block type, grid position, rotation steps, recipe ID
//   • Wires      — every explicit wire connection in PowerSystem
//   • Belts      — every active BeltSegment in LogisticsSystem
//
// What is NOT saved (V1): machine buffer contents, battery charge, cycle progress.
// These reset on load: miners re-bind to ore nodes by world position, other machines
// restart their production cycle empty.
//
// Block IDs are reassigned on reload, so wires and belts reference blocks by
// (constructIndex in file, gridX, gridY, gridZ) — a stable key regardless of ID churn.
//
// Controls:
//   F5 — save to persistentDataPath/save.json
//   F9 — load from save.json

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static ViewConstants;

public class SaveLoadManager : MonoBehaviour
{
    private static string SavePath =>
        Path.Combine(Application.persistentDataPath, "save.json");

    // Built once at startup from the catalogues — automatically covers new entries.
    private static readonly Dictionary<string, BlockDefinition> DefById    = BuildDefById();
    private static readonly Dictionary<string, Recipe>          RecipeById = BuildRecipeById();

    private static Dictionary<string, BlockDefinition> BuildDefById()
    {
        var d = new Dictionary<string, BlockDefinition>();
        foreach (var def in BlockCatalogue.All()) d[def.Id] = def;
        return d;
    }

    private static Dictionary<string, Recipe> BuildRecipeById()
    {
        var d = new Dictionary<string, Recipe>();
        foreach (var r in RecipeCatalogue.All()) d[r.Id] = r;
        return d;
    }

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (MenuManager.IsOpen) return;
        if (Input.GetKeyDown(KeyCode.F5)) SaveGame();
        if (Input.GetKeyDown(KeyCode.F9)) LoadGame();
    }

    // ── Serialisable data types ───────────────────────────────────────────────

    [System.Serializable]
    private class SaveFile
    {
        public List<ConstructData> constructs = new List<ConstructData>();
        public List<WireData>      wires      = new List<WireData>();
        public List<BeltData>      belts      = new List<BeltData>();
    }

    [System.Serializable]
    private class ConstructData
    {
        public float posX, posY, posZ;
        public float rotY;                 // Y-axis Euler angle in degrees
        public List<BlockData> blocks = new List<BlockData>();
    }

    [System.Serializable]
    private class BlockData
    {
        public string defId;
        public int    gx, gy, gz;
        public int    rot;
        public string recipeId;            // empty for structural/power blocks and miners
    }

    // Wires and belts store block references as (constructIndex, gridPos) so they
    // survive the ID reassignment that happens on every reload.
    [System.Serializable]
    private class WireData
    {
        public int conA, gxA, gyA, gzA;
        public int conB, gxB, gyB, gzB;
    }

    [System.Serializable]
    private class BeltData
    {
        public int   srcCon, srcGx, srcGy, srcGz;
        public int   srcPort;
        public int   dstCon, dstGx, dstGy, dstGz;
        public int   dstPort;
        public int   lengthInCells;
        public float throughput;
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    public void SaveGame()
    {
        var sim  = GameManager.Instance.Simulation;
        var file = new SaveFile();

        // Stable block reference: blockId → (constructIndex in file, gridPos).
        var blockRef = new Dictionary<int, (int con, int gx, int gy, int gz)>();

        var cvList = new List<ConstructView>(FindObjectsOfType<ConstructView>());

        for (int ci = 0; ci < cvList.Count; ci++)
        {
            var cv  = cvList[ci];
            var cd  = new ConstructData
            {
                posX = cv.transform.position.x,
                posY = cv.transform.position.y,
                posZ = cv.transform.position.z,
                rotY = cv.transform.eulerAngles.y,
            };

            foreach (Transform child in cv.transform)
            {
                var bv = child.GetComponent<BlockView>();
                if (bv == null) continue;

                var b = bv.Block;
                cd.blocks.Add(new BlockData
                {
                    defId    = b.Definition.Id,
                    gx       = b.GridPosition.X,
                    gy       = b.GridPosition.Y,
                    gz       = b.GridPosition.Z,
                    rot      = b.RotationSteps,
                    recipeId = b.MachineState?.ActiveRecipe?.Id ?? "",
                });

                blockRef[b.Id] = (ci, b.GridPosition.X, b.GridPosition.Y, b.GridPosition.Z);
            }

            file.constructs.Add(cd);
        }

        // Wires.
        foreach (var (idA, idB) in sim.Power.WireConnections)
        {
            if (!blockRef.TryGetValue(idA, out var rA)) continue;
            if (!blockRef.TryGetValue(idB, out var rB)) continue;
            file.wires.Add(new WireData
            {
                conA = rA.con, gxA = rA.gx, gyA = rA.gy, gzA = rA.gz,
                conB = rB.con, gxB = rB.gx, gyB = rB.gy, gzB = rB.gz,
            });
        }

        // Belts.
        foreach (var belt in sim.Logistics.Belts.Values)
        {
            if (!blockRef.TryGetValue(belt.SourceBlockId, out var rS)) continue;
            if (!blockRef.TryGetValue(belt.DestBlockId,   out var rD)) continue;
            file.belts.Add(new BeltData
            {
                srcCon = rS.con, srcGx = rS.gx, srcGy = rS.gy, srcGz = rS.gz,
                srcPort = belt.SourcePortIndex,
                dstCon = rD.con, dstGx = rD.gx, dstGy = rD.gy, dstGz = rD.gz,
                dstPort = belt.DestPortIndex,
                lengthInCells = belt.LengthInCells,
                throughput    = belt.ThroughputPerMin,
            });
        }

        File.WriteAllText(SavePath, JsonUtility.ToJson(file, prettyPrint: true));
        Debug.Log($"[Save] {file.constructs.Count} construct(s), " +
                  $"{file.wires.Count} wire(s), {file.belts.Count} belt(s) → {SavePath}");
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    public void LoadGame()
    {
        if (!File.Exists(SavePath))
        {
            Debug.LogWarning("[Load] No save file found at " + SavePath);
            return;
        }

        // Destroy existing scene constructs (ore nodes and the terrain stay).
        foreach (var cv in FindObjectsOfType<ConstructView>())
            Destroy(cv.gameObject);

        var sim  = GameManager.Instance.ResetSimulation();
        var file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(SavePath));

        // Reverse lookup: (constructIndex, gridPos) → new block ID.
        var blockLookup = new Dictionary<(int con, int gx, int gy, int gz), int>();
        int blockCount  = 0;

        for (int ci = 0; ci < file.constructs.Count; ci++)
        {
            var cd = file.constructs[ci];

            var simConstruct = sim.CreateConstruct();
            var cvGO = new GameObject();
            cvGO.transform.position = new Vector3(cd.posX, cd.posY, cd.posZ);
            cvGO.transform.rotation = Quaternion.Euler(0f, cd.rotY, 0f);
            var cv = cvGO.AddComponent<ConstructView>();
            cv.Init(simConstruct);

            foreach (var bd in cd.blocks)
            {
                if (!DefById.TryGetValue(bd.defId, out var def))
                {
                    Debug.LogWarning($"[Load] Unknown block def '{bd.defId}' — skipped.");
                    continue;
                }

                var block = sim.PlaceBlock(def, simConstruct.Id,
                                           new GridPos(bd.gx, bd.gy, bd.gz), bd.rot);

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

                blockLookup[(ci, bd.gx, bd.gy, bd.gz)] = block.Id;

                // Miners re-bind to the ore node under their world-space footprint.
                if (def.FunctionalType == FunctionalType.Miner)
                    TryBindMiner(sim, block, def, go.transform.position);

                // Assemblers / Furnaces restore their recipe by ID.
                else if (!string.IsNullOrEmpty(bd.recipeId)
                         && RecipeById.TryGetValue(bd.recipeId, out var recipe))
                    sim.Machines.Get(block.Id)?.SetRecipe(recipe);

                blockCount++;
            }
        }

        // Restore wire connections.
        int wireCount = 0;
        foreach (var wd in file.wires)
        {
            if (!blockLookup.TryGetValue((wd.conA, wd.gxA, wd.gyA, wd.gzA), out int idA)) continue;
            if (!blockLookup.TryGetValue((wd.conB, wd.gxB, wd.gyB, wd.gzB), out int idB)) continue;
            sim.Power.ConnectBlocks(idA, idB);
            wireCount++;
        }

        // Restore belt connections.
        int beltCount = 0;
        foreach (var bd in file.belts)
        {
            if (!blockLookup.TryGetValue((bd.srcCon, bd.srcGx, bd.srcGy, bd.srcGz), out int srcId)) continue;
            if (!blockLookup.TryGetValue((bd.dstCon, bd.dstGx, bd.dstGy, bd.dstGz), out int dstId)) continue;
            sim.Logistics.Connect(srcId, bd.srcPort, dstId, bd.dstPort,
                                  bd.lengthInCells, bd.throughput);
            beltCount++;
        }

        Debug.Log($"[Load] {file.constructs.Count} construct(s), {blockCount} block(s), " +
                  $"{wireCount} wire(s), {beltCount} belt(s) restored.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Mirrors BlockPlacer.TryBindMinerToNode — called on load to restore miner→node binding.
    private static void TryBindMiner(Simulation sim, Block block, BlockDefinition def, Vector3 worldCenter)
    {
        var node = OreNode.FindUnder(worldCenter, def.SizeX, def.SizeZ);
        if (node == null) return;
        var miner = sim.Machines.Get<MinerMachine>(block.Id);
        if (miner == null) return;
        var mp    = def.Params as MinerParams;
        float rate = mp != null ? mp.ExtractRatePerSecond : node.ExtractRate;
        miner.SetResourceNode(node.ResourceId, 1f / rate, 1);
    }
}
