using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Material _ghostMaterial;

    private Simulation _simulation;
    private float      _accumulator;
    private GameObject _ghostBlock;
    private float      _currentRotation;

    private const float TickRate  = 1f / 20f;
    private const float SnapSize  = 0.5f;

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
            Vector3 position;

            if (hit.collider.gameObject.name.StartsWith("Block_"))
            {
                // Hit an existing block — snap to its surface on the hit face
                position = hit.collider.transform.position + hit.normal * SnapSize * 2f;
            }
            else
            {
                // Hit terrain — place freely
                position = new Vector3(hit.point.x, hit.point.y + SnapSize, hit.point.z);
            }

            _ghostBlock.SetActive(true);
            _ghostBlock.transform.position = position;
            _ghostBlock.transform.rotation = Quaternion.Euler(0f, _currentRotation, 0f);

            if (Input.GetMouseButtonDown(0))
            {
                var block = _simulation.PlaceBlock(
                    position.x, position.y - SnapSize, position.z,
                    0f, _currentRotation, 0f);
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
        go.transform.position = new Vector3(block.X, block.Y + SnapSize, block.Z);
        go.transform.rotation = Quaternion.Euler(block.RotationX, block.RotationY, block.RotationZ);
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20),
            $"Tick: {_simulation.Tick}   Blocks: {_simulation.Blocks.Count}");
    }
}