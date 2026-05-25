// Always-on belt visualization. Two responsibilities:
//
//   Port markers — small coloured cubes at each machine port, always visible.
//                  Orange = Output, Green = Input.
//                  Created when a block is placed, destroyed when it is dismantled.
//
//   Belt lines   — yellow LineRenderers drawn between the two port anchors of every
//                  active BeltSegment. Created when a belt is connected, destroyed
//                  when it is disconnected or either block is removed.
//
// Setup: attach to any persistent GameObject (e.g. GameManager).
//        Assign the same solid-colour material used by BeltConnector/WireConnector
//        to both Inspector slots.

using System.Collections.Generic;
using UnityEngine;
using static ViewConstants;

public class BeltView : MonoBehaviour
{
    [SerializeField] private Material _portMaterial;
    [SerializeField] private Material _beltMaterial;

    private Simulation Sim => GameManager.Instance.Simulation;

    // blockId → list of port-marker GameObjects
    private readonly Dictionary<int, List<GameObject>> _portMarkers =
        new Dictionary<int, List<GameObject>>();

    // beltId → belt-line GameObject (holds a LineRenderer)
    private readonly Dictionary<int, GameObject> _beltLines =
        new Dictionary<int, GameObject>();

    // blockId → BlockView cache; rebuilt whenever block count changes
    private readonly Dictionary<int, BlockView> _bvCache =
        new Dictionary<int, BlockView>();
    private int _cachedBlockCount = -1;

    private Material _matOutput, _matInput, _matBelt;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_portMaterial != null)
        {
            _matOutput = new Material(_portMaterial); _matOutput.color = new Color(1.0f, 0.5f, 0.0f);
            _matInput  = new Material(_portMaterial); _matInput.color  = new Color(0.1f, 0.8f, 0.2f);
        }
        if (_beltMaterial != null)
        {
            _matBelt = new Material(_beltMaterial); _matBelt.color = new Color(1.0f, 0.8f, 0.0f);
        }
    }

    private void Update()
    {
        RebuildCacheIfNeeded();
        SyncPortMarkers();
        SyncBeltLines();
    }

    // ── BlockView cache ───────────────────────────────────────────────────────

    private void RebuildCacheIfNeeded()
    {
        int n = Sim.Blocks.ById.Count;
        if (n == _cachedBlockCount) return;

        _bvCache.Clear();
        foreach (var bv in FindObjectsOfType<BlockView>())
            if (bv.Block != null)
                _bvCache[bv.Block.Id] = bv;
        _cachedBlockCount = n;
    }

    // ── Port markers ──────────────────────────────────────────────────────────

    private void SyncPortMarkers()
    {
        // Create markers for blocks that have ports and no marker yet.
        foreach (var kvp in Sim.Blocks.ById)
        {
            int bid   = kvp.Key;
            var block = kvp.Value;
            if (_portMarkers.ContainsKey(bid)) continue;
            if (block.Definition.Ports.Length == 0) continue;
            if (!_bvCache.TryGetValue(bid, out var bv)) continue;

            var list = new List<GameObject>();
            foreach (var port in block.Definition.Ports)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"PortMarker_{bid}_{port.Type}{port.Index}";
                Object.Destroy(cube.GetComponent<Collider>());
                cube.transform.localScale = Vector3.one * CellSize * 0.3f;
                cube.transform.position   = BeltConnector.PortWorldPos(bv, port);

                if (_portMaterial != null)
                    cube.GetComponent<Renderer>().material =
                        port.Type == PortType.Output ? _matOutput : _matInput;

                list.Add(cube);
            }
            _portMarkers[bid] = list;
        }

        // Destroy markers for blocks that no longer exist.
        var dead = new List<int>();
        foreach (var bid in _portMarkers.Keys)
            if (!Sim.Blocks.ById.ContainsKey(bid)) dead.Add(bid);
        foreach (var bid in dead)
        {
            foreach (var go in _portMarkers[bid]) Object.Destroy(go);
            _portMarkers.Remove(bid);
        }
    }

    // ── Belt lines ────────────────────────────────────────────────────────────

    private void SyncBeltLines()
    {
        // Create a line for each new belt segment.
        foreach (var kvp in Sim.Logistics.Belts)
        {
            int beltId = kvp.Key;
            if (_beltLines.ContainsKey(beltId)) continue;

            var belt = kvp.Value;
            if (!_bvCache.TryGetValue(belt.SourceBlockId, out var srcBv)) continue;
            if (!_bvCache.TryGetValue(belt.DestBlockId,   out var dstBv)) continue;

            var srcPort = FindPort(srcBv.Block.Definition, belt.SourcePortIndex, PortType.Output);
            var dstPort = FindPort(dstBv.Block.Definition, belt.DestPortIndex,   PortType.Input);
            if (srcPort == null || dstPort == null) continue;

            var go = new GameObject($"BeltLine_{beltId}");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth    = 0.06f;
            lr.endWidth      = 0.06f;
            lr.useWorldSpace = true;
            lr.SetPosition(0, BeltConnector.PortWorldPos(srcBv, srcPort));
            lr.SetPosition(1, BeltConnector.PortWorldPos(dstBv, dstPort));
            if (_matBelt != null) lr.material = _matBelt;

            _beltLines[beltId] = go;
        }

        // Destroy lines for belts that were disconnected.
        var dead = new List<int>();
        foreach (var beltId in _beltLines.Keys)
            if (!Sim.Logistics.Belts.ContainsKey(beltId)) dead.Add(beltId);
        foreach (var beltId in dead)
        {
            Object.Destroy(_beltLines[beltId]);
            _beltLines.Remove(beltId);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PortDefinition FindPort(BlockDefinition def, int portIndex, PortType type)
    {
        foreach (var p in def.Ports)
            if (p.Index == portIndex && p.Type == type) return p;
        return null;
    }
}
