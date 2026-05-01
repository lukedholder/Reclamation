using UnityEngine;
using Reclamation.Simulation;

public class GameManager : MonoBehaviour {
    private Simulation _sim;
    private float _accumulator;
    private const float TICK_RATE = 1f / 20f; // 20Hz

    void Start() {
        _sim = new Simulation();
        _sim.Initialise();
        Debug.Log("Simulation initialised.");
    }

    void Update() {
        _accumulator += Time.deltaTime;
        while (_accumulator >= TICK_RATE) {
            _sim.Tick(TICK_RATE);
            _accumulator -= TICK_RATE;
        }
    }

    void OnGUI() {
        GUI.Label(new Rect(10, 10, 300, 20),
            $"Simulation tick: {_sim.State.Tick}");
    }
}