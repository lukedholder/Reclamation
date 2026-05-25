// Draws wire lines between Power Pole blocks that share a power network and
// are within each other's WireRangeUnits.  Wire colour reflects network state.
//
// Setup: attach to any persistent GameObject (e.g. GameManager).
//        Assign a simple unlit material to Wire Material in the Inspector.

using System.Collections.Generic;
using UnityEngine;
using static ViewConstants;

public class PowerWireView : MonoBehaviour
{
    [SerializeField] private Material _wireMaterial;
    [SerializeField] private float    _wireWidth = 0.04f;

    // Wire colours per network state.
    private static readonly Color ColNominal      = new Color(1.00f, 0.90f, 0.20f); // yellow
    private static readonly Color ColBatteryAssist = new Color(1.00f, 0.60f, 0.10f); // orange
    private static readonly Color ColDeficit       = new Color(0.90f, 0.20f, 0.20f); // red
    private static readonly Color ColDead          = new Color(0.35f, 0.35f, 0.35f); // grey

    // Pooled LineRenderer GameObjects — grown on demand, reused every frame.
    private readonly List<LineRenderer> _pool = new List<LineRenderer>();
    private int _activeWires;

    private Simulation Sim => GameManager.Instance.Simulation;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        _activeWires = 0;
        DrawAllNetworkWires();

        // Hide any pooled wires that weren't needed this frame.
        for (int i = _activeWires; i < _pool.Count; i++)
            _pool[i].gameObject.SetActive(false);
    }

    // ── Wire drawing ──────────────────────────────────────────────────────────

    private void DrawAllNetworkWires()
    {
        var blocks = Sim.Blocks;

        // Only draw wires the player has explicitly placed.
        foreach (var (idA, idB) in Sim.Power.WireConnections)
        {
            if (!blocks.ById.TryGetValue(idA, out var blockA)) continue;
            if (!blocks.ById.TryGetValue(idB, out var blockB)) continue;

            var bvA = FindBlockView(blockA);
            var bvB = FindBlockView(blockB);
            if (bvA == null || bvB == null) continue;

            // Color driven by the shared power network (poles on the same construct
            // share a network; cross-construct wiring will be handled by docking).
            Color color = ColDead;
            if (blockA.PowerNetworkId >= 0 &&
                Sim.Power.Networks.TryGetValue(blockA.PowerNetworkId, out var network))
                color = NetworkColor(network.State);

            SetWire(bvA.transform.position, bvB.transform.position, color);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Looks up the BlockView for a sim block by searching scene objects.
    // Moderately expensive — acceptable for the small pole counts expected.
    private static BlockView FindBlockView(Block block)
    {
        foreach (var bv in FindObjectsOfType<BlockView>())
            if (bv.Block == block) return bv;
        return null;
    }

    private static Color NetworkColor(PowerState state) => state switch
    {
        PowerState.Nominal       => ColNominal,
        PowerState.BatteryAssist => ColBatteryAssist,
        PowerState.Deficit       => ColDeficit,
        _                        => ColDead,
    };

    // Activates the next pooled LineRenderer (growing the pool if needed).
    private void SetWire(Vector3 from, Vector3 to, Color color)
    {
        LineRenderer lr;

        if (_activeWires < _pool.Count)
        {
            lr = _pool[_activeWires];
            lr.gameObject.SetActive(true);
        }
        else
        {
            var go = new GameObject("PowerWire");
            go.transform.SetParent(transform);
            lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace  = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows    = false;
            if (_wireMaterial != null)
                lr.sharedMaterial = _wireMaterial;
            _pool.Add(lr);
        }

        lr.startWidth  = _wireWidth;
        lr.endWidth    = _wireWidth;
        lr.startColor  = color;
        lr.endColor    = color;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);

        _activeWires++;
    }
}
