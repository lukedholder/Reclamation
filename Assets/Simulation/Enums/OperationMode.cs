// The current operating state of a functional machine block (Assembler, Miner, etc.).
// MachineSystem reads this to decide whether to advance a production cycle this tick.
// PowerSystem reads this to determine the block's effective power draw:
//   Idle / NoPower = 0 kW,  Waiting = 25% of operating draw,  Operating = 100%.

public enum OperationMode
{
    Idle,      // No recipe set or block awaiting configuration. Zero power draw.
    Waiting,   // Recipe set but inputs missing or output buffer full. Draws 25% of operating power.
    Operating, // All inputs present, output has space, power available. Full production and power draw.
    NoPower,   // Not connected to a power network, or network PowerState is Dead. Zero power draw.
}
