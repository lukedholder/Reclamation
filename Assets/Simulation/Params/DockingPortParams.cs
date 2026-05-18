// Configuration for Docking Port blocks (FunctionalType.DockingPort).
// PortType determines which network systems are bridged when two ports make contact.
// Both the vehicle's port and the base's port must be present and touching to connect.
// Undocking severs all bridged connections; items mid-transfer remain at the source.

public class DockingPortParams : IFunctionalParams
{
    public DockingPortType PortType;
}
