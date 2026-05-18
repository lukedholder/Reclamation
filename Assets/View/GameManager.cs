using UnityEngine;

public class GameManager : MonoBehaviour
{
    private Simulation _simulation;
    private float      _accumulator;

    private const float TickRate = 1f / 20f;

    private void Start()
    {
        _simulation = new Simulation();
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

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 400, 20),
            $"Tick: {_simulation.Tick}   " +
            $"Blocks: {_simulation.Blocks.ById.Count}   " +
            $"Constructs: {_simulation.Constructs.ById.Count}");
    }
}
