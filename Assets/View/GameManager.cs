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

        if (Input.GetMouseButtonDown(0))
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                _simulation.PlaceBlock(hit.point.x, hit.point.y, hit.point.z);
            }
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