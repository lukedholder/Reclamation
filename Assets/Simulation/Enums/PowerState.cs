// The current power supply/demand balance of a network, computed each tick by PowerSystem.
// Drives throttling behaviour: in Deficit, non-turret consumers are proportionally throttled.
// Turrets always receive full power before others are throttled.

public enum PowerState
{
    Nominal,       // Supply meets or exceeds demand. All consumers at full rate. Surplus charges batteries.
    BatteryAssist, // Supply below demand but batteries covering the gap. All consumers still at full rate.
    Deficit,       // Supply + battery discharge insufficient. Non-turret consumers proportionally throttled.
    Dead,          // No supply at all. All consumers offline, including turrets.
}
