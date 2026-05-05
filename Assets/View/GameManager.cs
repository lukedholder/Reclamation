using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Material _ghostMaterial;

    private Simulation _simulation;
    private float      _accumulator;
    private GameObject _ghostBlock;
    private float      _currentRotation;
    private BlockDefinition _selectedDefinition = BlockCatalogue.SmallCube;

    private const float TickRate  = 1f / 20f;
    private const float BlockSize  = 0.5f;

    private void Start()
    {
        _simulation = new Simulation();

        _ghostBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _ghostBlock.name = "GhostBlock";
        _ghostBlock.transform.localScale = Vector3.one * BlockSize;
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
                int id       = int.Parse(hit.collider.gameObject.name.Replace("Block_", ""));
                var hitBlock = _simulation.Blocks.Find(b => b.Id == id);

                float hitHalf = GetHalfSizeForDef(hit.normal, hitBlock.Definition);
                float newHalf = GetHalfSize(hit.normal);

                position = hit.collider.transform.position + hit.normal * (hitHalf + newHalf);
            }
            else
            {
                position = new Vector3(
                        hit.point.x,
                        hit.point.y + _selectedDefinition.SizeY * BlockSize * 0.5f,
                        hit.point.z);
            }

            _ghostBlock.SetActive(true);
            _ghostBlock.transform.localScale = new Vector3(
                _selectedDefinition.SizeX * BlockSize,
                _selectedDefinition.SizeY * BlockSize,
                _selectedDefinition.SizeZ * BlockSize);
            _ghostBlock.transform.position = position;
            _ghostBlock.transform.rotation = Quaternion.Euler(0f, _currentRotation, 0f);

            if (Input.GetMouseButtonDown(0))
            {
                var block = _simulation.PlaceBlock(
                    _selectedDefinition,
                    position.x, position.y, position.z,
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
        go.transform.localScale = new Vector3(
            block.Definition.SizeX * BlockSize,
            block.Definition.SizeY * BlockSize,
            block.Definition.SizeZ * BlockSize);
        go.transform.position = new Vector3(block.X, block.Y, block.Z);
        go.transform.rotation = Quaternion.Euler(block.RotationX, block.RotationY, block.RotationZ);
    }

    private float GetHalfSize(Vector3 normal)
    {
        return GetHalfSizeForDef(normal, _selectedDefinition);
    }

    private float GetHalfSizeForDef(Vector3 normal, BlockDefinition def)
    {
        float ax = Mathf.Abs(normal.x);
        float ay = Mathf.Abs(normal.y);
        float az = Mathf.Abs(normal.z);

        if (ay > ax && ay > az) return def.SizeY * BlockSize * 0.5f;
        if (ax > az)            return def.SizeX * BlockSize * 0.5f;
        return                         def.SizeZ * BlockSize * 0.5f;
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20),
            $"Tick: {_simulation.Tick}   Blocks: {_simulation.Blocks.Count}");
    }
}