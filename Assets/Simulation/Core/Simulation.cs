namespace Reclamation.Simulation {
    public class Simulation {
        private SimulationState _state = new SimulationState();
        public SimulationState State => _state;

        public void Initialise() {
            _state.Tick = 0;
        }

        public void Tick(float dt) {
            _state.Tick++;
        }
    }
}