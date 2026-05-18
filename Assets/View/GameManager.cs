using System.Text;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private Simulation _simulation;
    private float      _accumulator;

    private const float TickRate = 1f / 20f;

    private void Start()
    {
        _simulation = new Simulation();
        RunDebugScenario();
    }

    private void Update()
    {
        _accumulator += Time.deltaTime;
        while (_accumulator >= TickRate)
        {
            _simulation.Update();
            _accumulator -= TickRate;
        }
    }

    // ── Debug scenario ────────────────────────────────────────────────────────
    // Chain: Miner → Belt(6 slots) → Furnace → Belt(4 slots) → Assembler
    //
    // Belt lengths here are stand-ins for what the view layer would compute
    // from the spline arc length (floor(arcLength / CellSize)).
    // In the final game the view layer calls Connect() after routing the spline.

    private void RunDebugScenario()
    {
        var construct = _simulation.CreateConstruct();

        // Miner — 1 iron_ore every 2 seconds, output on port 0 (PosZ face)
        var miner = _simulation.PlaceBlock(BlockCatalogue.BasicMiner, construct.Id, new GridPos(0, 0, 0));
        _simulation.Machines.Get<MinerMachine>(miner.Id)
            ?.SetResourceNode("iron_ore", cycleTime: 2f, amountPerCycle: 1);

        // Furnace — iron_ore → iron_plate (3.5 s), input port 0 (NegZ), output port 0 (PosZ)
        var furnace = _simulation.PlaceBlock(BlockCatalogue.ElectricFurnace, construct.Id, new GridPos(6, 0, 0));
        _simulation.Machines.Get<BaseMachine>(furnace.Id)
            ?.SetRecipe(RecipeCatalogue.SmeltIron);

        // Assembler — 2× iron_plate → 1× iron_gear (0.5 s), input port 0 (NegX)
        var assembler = _simulation.PlaceBlock(BlockCatalogue.AssemblerMk1, construct.Id, new GridPos(14, 0, 0));
        _simulation.Machines.Get<BaseMachine>(assembler.Id)
            ?.SetRecipe(RecipeCatalogue.IronGearWheel);

        // Miner.out[0] → Furnace.in[0]
        // 6-slot belt: simulates a ~3 m spline path between the two machines
        _simulation.Logistics.Connect(
            sourceBlockId: miner.Id,    sourcePort: 0,
            destBlockId:   furnace.Id,  destPort:   0,
            lengthInCells: 6);

        // Furnace.out[0] → Assembler.in[0]
        // 4-slot belt: simulates a ~2 m spline path
        _simulation.Logistics.Connect(
            sourceBlockId: furnace.Id,   sourcePort: 0,
            destBlockId:   assembler.Id, destPort:   0,
            lengthInCells: 4);
    }

    // ── Debug GUI ─────────────────────────────────────────────────────────────

    private readonly StringBuilder _sb = new StringBuilder();

    private void OnGUI()
    {
        _sb.Clear();

        _sb.AppendLine(
            $"Tick {_simulation.Tick,-6} | " +
            $"Blocks: {_simulation.Blocks.ById.Count}  " +
            $"Machines: {_simulation.Machines.Count}  " +
            $"Belts: {_simulation.Logistics.Count}");
        _sb.AppendLine(new string('─', 66));

        // Machines
        foreach (var block in _simulation.Blocks.ById.Values)
        {
            var ms = block.MachineState;
            if (ms == null) continue;

            string type   = block.Definition.FunctionalType.ToString();
            string recipe = ms.ActiveRecipe?.DisplayName ?? "no recipe";
            string mode   = ms.Mode.ToString();
            int    pct    = Mathf.RoundToInt(ms.CycleProgress * 100f);

            _sb.AppendLine($"[{block.Id}] {type,-14} {mode,-10} \"{recipe}\"  {pct,3}%");

            foreach (var slot in ms.InputBuffer.Slots)
                if (!string.IsNullOrEmpty(slot.ItemId))
                    _sb.AppendLine($"       IN  {slot.Quantity,4}× {slot.ItemId}");

            foreach (var slot in ms.OutputBuffer.Slots)
                if (!string.IsNullOrEmpty(slot.ItemId))
                    _sb.AppendLine($"       OUT {slot.Quantity,4}× {slot.ItemId}");
        }

        // Belts — show each slot as a filled or empty cell
        if (_simulation.Logistics.Count > 0)
        {
            _sb.AppendLine(new string('─', 66));
            foreach (var belt in _simulation.Logistics.Belts.Values)
            {
                // Slot visualisation: [■] = item present, [ ] = empty
                _sb.Append($"Belt {belt.Id}  Blk{belt.SourceBlockId}.out[{belt.SourcePortIndex}]→" +
                           $"Blk{belt.DestBlockId}.in[{belt.DestPortIndex}]  " +
                           $"{belt.ThroughputPerMin}/min  ");

                for (int i = 0; i < belt.Slots.Length; i++)
                    _sb.Append(belt.Slots[i] != null ? "[■]" : "[ ]");

                // Show what item type is on the belt (first non-null slot)
                string itemOnBelt = null;
                foreach (var s in belt.Slots) if (s != null) { itemOnBelt = s; break; }
                if (itemOnBelt != null) _sb.Append($"  {itemOnBelt}");

                _sb.AppendLine();
            }
        }

        GUI.Label(new Rect(10, 10, 600, 900), _sb.ToString());
    }
}
