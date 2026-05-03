using UnityEngine;

public class GameManager : MonoBehaviour
{
    private Simulation _simulation;
    private float      _accumulator;

    private const float TickRate = 1f / 20f;

    private void Start()
    {
        _simulation = new Simulation();
        _simulation.PlaceBlock(0, 0, 0);
        _simulation.PlaceBlock(1, 0, 0);
        _simulation.PlaceBlock(2, 0, 0);
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
        GUI.Label(new Rect(10, 10, 200, 20),
            $"Tick: {_simulation.Tick}   Blocks: {_simulation.Blocks.Count}");
    }

    private void OnDrawGizmos()
    {
        if (_simulation == null) return;

        Gizmos.color = Color.cyan;
        foreach (var block in _simulation.Blocks)
        {
            Gizmos.DrawWireCube(
                new Vector3(block.X, block.Y, block.Z),
                Vector3.one);
        }
    }
}