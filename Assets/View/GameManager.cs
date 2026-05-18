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
    // Power setup: 3× SteamGenerator (360 kW) + 1× SmallBattery (500 kJ, 100 kW discharge)
    // Consumer demand (Operating): Miner (30) + Furnace (60) + Assembler (75) = 165 kW
    // Consumer demand (Waiting):   same machines at 25% = 7.5 + 15 + 18.75 = 41.25 kW
    //
    // Balance at full production: 360 kW supply − 165 kW demand = +195 kW surplus → Nominal
    //   Surplus charges battery at 50 kW (MaxChargeRateKW); machines run at 100%.
    //   To exercise BatteryAssist/Deficit: swap to 1× SteamGenerator (120 kW supply,
    //   45 kW deficit covered by battery → BatteryAssist, depletes in ~222 s at 20 Hz,
    //   then Deficit at OperatingRate ≈ 72.7%).

    private void RunDebugScenario()
    {
        var construct = _simulation.CreateConstruct();

        // ── Power infrastructure ──────────────────────────────────────────────
        _simulation.PlaceBlock(BlockCatalogue.SteamGenerator, construct.Id, new GridPos(0,  0, -4));
        _simulation.PlaceBlock(BlockCatalogue.SteamGenerator, construct.Id, new GridPos(2,  0, -4));
        _simulation.PlaceBlock(BlockCatalogue.SteamGenerator, construct.Id, new GridPos(4,  0, -4));
        _simulation.PlaceBlock(BlockCatalogue.SmallBattery,   construct.Id, new GridPos(6,  0, -4));

        // ── Production chain ──────────────────────────────────────────────────
        var miner = _simulation.PlaceBlock(BlockCatalogue.BasicMiner, construct.Id, new GridPos(0, 0, 0));
        _simulation.Machines.Get<MinerMachine>(miner.Id)
            ?.SetResourceNode("iron_ore", cycleTime: 2f, amountPerCycle: 1);

        var furnace = _simulation.PlaceBlock(BlockCatalogue.ElectricFurnace, construct.Id, new GridPos(6, 0, 0));
        _simulation.Machines.Get<BaseMachine>(furnace.Id)
            ?.SetRecipe(RecipeCatalogue.SmeltIron);

        var assembler = _simulation.PlaceBlock(BlockCatalogue.AssemblerMk1, construct.Id, new GridPos(14, 0, 0));
        _simulation.Machines.Get<BaseMachine>(assembler.Id)
            ?.SetRecipe(RecipeCatalogue.IronGearWheel);

        // ── Logistics ─────────────────────────────────────────────────────────
        _simulation.Logistics.Connect(
            sourceBlockId: miner.Id,    sourcePort: 0,
            destBlockId:   furnace.Id,  destPort:   0,
            lengthInCells: 6);

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
            $"Belts: {_simulation.Logistics.Count}  " +
            $"Networks: {_simulation.Power.Networks.Count}");

        // ── Power networks ────────────────────────────────────────────────────
        foreach (var net in _simulation.Power.Networks.Values)
        {
            string stateLabel = net.State switch
            {
                PowerState.Nominal      => "NOMINAL",
                PowerState.BatteryAssist=> "BATTERY ASSIST",
                PowerState.Deficit      => $"DEFICIT  ({net.TotalSupplyKW:F0}/{net.TotalDemandKW:F0} kW)",
                PowerState.Dead         => "DEAD",
                _                       => net.State.ToString(),
            };

            _sb.AppendLine(new string('─', 66));
            _sb.AppendLine($"Network {net.Id}  [{stateLabel}]" +
                           $"  Supply {net.TotalSupplyKW:F0} kW  Demand {net.TotalDemandKW:F0} kW");

            // Generators
            foreach (var id in net.GeneratorIds)
            {
                if (!_simulation.Blocks.ById.TryGetValue(id, out var b)) continue;
                var gs = b.GeneratorState;
                _sb.AppendLine($"  GEN  [{id}] {b.Definition.DisplayName,-20} " +
                               $"{(gs.IsRunning ? gs.CurrentOutputKW + " kW" : "OFF")}");
            }

            // Batteries
            foreach (var id in net.BatteryIds)
            {
                if (!_simulation.Blocks.ById.TryGetValue(id, out var b)) continue;
                var bat = b.BatteryState;
                int pct = Mathf.RoundToInt(bat.ChargePercent * 100f);
                _sb.AppendLine($"  BAT  [{id}] {b.Definition.DisplayName,-20} " +
                               $"{bat.StoredKJ:F0}/{bat.CapacityKJ:F0} kJ  ({pct}%)");
            }

            // Consumers
            foreach (var id in net.ConsumerIds)
            {
                if (!_simulation.Blocks.ById.TryGetValue(id, out var b)) continue;
                float rate = b.MachineState?.OperatingRate ?? 1f;
                _sb.AppendLine($"  CON  [{id}] {b.Definition.DisplayName,-20} " +
                               $"{b.Definition.PowerDrawKW:F0} kW  rate {rate:P0}");
            }
        }

        // ── Machines ──────────────────────────────────────────────────────────
        _sb.AppendLine(new string('─', 66));
        foreach (var block in _simulation.Blocks.ById.Values)
        {
            var ms = block.MachineState;
            if (ms == null) continue;

            int pct = Mathf.RoundToInt(ms.CycleProgress * 100f);
            _sb.AppendLine($"[{block.Id}] {block.Definition.FunctionalType,-14} " +
                           $"{ms.Mode,-10} \"{ms.ActiveRecipe?.DisplayName ?? "no recipe"}\"  " +
                           $"{pct,3}%  rate {ms.OperatingRate:P0}");

            foreach (var slot in ms.InputBuffer.Slots)
                if (!string.IsNullOrEmpty(slot.ItemId))
                    _sb.AppendLine($"       IN  {slot.Quantity,4}× {slot.ItemId}");
            foreach (var slot in ms.OutputBuffer.Slots)
                if (!string.IsNullOrEmpty(slot.ItemId))
                    _sb.AppendLine($"       OUT {slot.Quantity,4}× {slot.ItemId}");
        }

        // ── Belts ─────────────────────────────────────────────────────────────
        if (_simulation.Logistics.Count > 0)
        {
            _sb.AppendLine(new string('─', 66));
            foreach (var belt in _simulation.Logistics.Belts.Values)
            {
                _sb.Append($"Belt {belt.Id}  " +
                           $"Blk{belt.SourceBlockId}.out[{belt.SourcePortIndex}]→" +
                           $"Blk{belt.DestBlockId}.in[{belt.DestPortIndex}]  " +
                           $"{belt.ThroughputPerMin}/min  ");

                foreach (var slot in belt.Slots)
                    _sb.Append(slot != null ? "[■]" : "[ ]");

                string itemOnBelt = null;
                foreach (var s in belt.Slots) if (s != null) { itemOnBelt = s; break; }
                if (itemOnBelt != null) _sb.Append($"  {itemOnBelt}");
                _sb.AppendLine();
            }
        }

        GUI.Label(new Rect(10, 10, 640, 1000), _sb.ToString());
    }
}
