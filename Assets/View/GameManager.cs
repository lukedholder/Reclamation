using UnityEngine;

public class GameManager : MonoBehaviour
{
    private Simulation _simulation;
    private float      _accumulator;
    private GameObject _ghostBlock;

    private const float TickRate = 1f / 20f;

    private void Start()
    {
        _simulation = new Simulation();

        _ghostBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _ghostBlock.name = "GhostBlock";
        _ghostBlock.GetComponent<Renderer>().material.color = new Color(0f, 1f, 0f, 0.4f);
        _ghostBlock.GetComponent<Collider>().enabled = false;
    }

    private void Update()
    {
        _accumulator += Time.deltaTime;

        while (_accumulator >= TickRate)
        {
            _simulation.Update();
            _accumulator -= TickRate;
        }

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            _ghostBlock.SetActive(true);
            _ghostBlock.transform.position = new Vector3(hit.point.x, hit.point.y, hit.point.z);

            if (Input.GetMouseButtonDown(0))
            {
                var block = _simulation.PlaceBlock(hit.point.x, hit.point.y, hit.point.z);
                SpawnBlockObject(block);
            }
        }
        else
        {
            _ghostBlock.SetActive(false);
        }
    }

    private void SpawnBlockObject(Block block)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"Block_{block.Id}";
        go.transform.position = new Vector3(block.X, block.Y, block.Z);
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20),
            $"Tick: {_simulation.Tick}   Blocks: {_simulation.Blocks.Count}");
    }
}