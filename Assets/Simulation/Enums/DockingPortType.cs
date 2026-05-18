// Determines which network systems are bridged when two docking port blocks make contact.
// Both the vehicle's port and the base's port must be present for a connection to form.
// Undocking severs all bridged connections cleanly with no item loss mid-transfer.

public enum DockingPortType
{
    Logistics,     // Bridges item logistics networks only (belt-speed transfer rate).
    Power,         // Bridges electrical power networks only (defined kW transfer rate).
    FullInterface, // Bridges both item logistics and power simultaneously.
}
