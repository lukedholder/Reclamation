using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Material _ghostMaterial;

    private Simulation _simulation;
    private float      _accumulator;
    private GameObject _ghostBlock;
    private float      _currentRotation;

    private const float TickRate = 1f / 20f;

    private void Start()
    {
        _simulation = new Simulation();

        _ghostBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _ghostBlock.name = "GhostBlock";
        _ghostBlock.GetComponent<Collider>().enabled = false;
        _ghostBlock.GetComponent<Renderer>().material = _ghostMaterial;
    }

    private void Update()
    {
        _accumulator += Time.deltaTime;

        while (_accumulator >= TickRate)
        {
            _simulation.Update();
            _accumulator -= TickRate;
        }

        _currentRotation += Input.mouseScrollDelta.y * 15f;

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            _ghostBlock.SetActive(true);
            _ghostBlock.transform.position = new Vector3(hit.point.x, hit.point.y + 0.5f, hit.point.z);
            _ghostBlock.transform.rotation = Quaternion.Euler(0f, _currentRotation, 0f);

            if (Input.GetMouseButtonDown(0))
            {
                var block = _simulation.PlaceBlock(hit.point.x, hit.point.y, hit.point.z, _currentRotation);
                SpawnBlockObject(block);
            }

            if (Input.GetMouseButtonDown(1))
            {
                var hitObject = hit.collider.gameObject;
                if (hitObject.name.StartsWith("Block_"))
                {
                    int id = int.Parse(hitObject.name.Replace("Block_", ""));
                    _simulation.RemoveBlock(id);
                    Destroy(hitObject);
                }
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
        go.transform.position = new Vector3(block.X, block.Y + 0.5f, block.Z);
        go.transform.rotation = Quaternion.Euler(0f, block.RotationY, 0f);
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20),
            $"Tick: {_simulation.Tick}   Blocks: {_simulation.Blocks.Count}");
    }
}