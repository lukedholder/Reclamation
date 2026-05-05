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

        if (Input.GetKeyDown(KeyCode.Alpha1)) _selectedDefinition = BlockCatalogue.SmallCube;
        if (Input.GetKeyDown(KeyCode.Alpha2)) _selectedDefinition = BlockCatalogue.LargeCube;
        if (Input.GetKeyDown(KeyCode.Alpha3)) _selectedDefinition = BlockCatalogue.Plank;

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

                float ax = Mathf.Abs(hit.normal.x);
                float ay = Mathf.Abs(hit.normal.y);
                float az = Mathf.Abs(hit.normal.z);

                // Normal axis: use block center + combined half-extents (keeps faces flush).
                // Face-plane axes: snap to the nearest position where the new block's face
                // aligns with a cell boundary of the hit block.
                if (ay > ax && ay > az)
                    position = new Vector3(
                        SnapFacePlane(hit.point.x, hitBlock.X, hitBlock.Definition.SizeX, _selectedDefinition.SizeX),
                        hit.collider.transform.position.y + hit.normal.y * (hitHalf + newHalf),
                        SnapFacePlane(hit.point.z, hitBlock.Z, hitBlock.Definition.SizeZ, _selectedDefinition.SizeZ));
                else if (ax > az)
                    position = new Vector3(
                        hit.collider.transform.position.x + hit.normal.x * (hitHalf + newHalf),
                        SnapFacePlane(hit.point.y, hitBlock.Y, hitBlock.Definition.SizeY, _selectedDefinition.SizeY),
                        SnapFacePlane(hit.point.z, hitBlock.Z, hitBlock.Definition.SizeZ, _selectedDefinition.SizeZ));
                else
                    position = new Vector3(
                        SnapFacePlane(hit.point.x, hitBlock.X, hitBlock.Definition.SizeX, _selectedDefinition.SizeX),
                        SnapFacePlane(hit.point.y, hitBlock.Y, hitBlock.Definition.SizeY, _selectedDefinition.SizeY),
                        hit.collider.transform.position.z + hit.normal.z * (hitHalf + newHalf));
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

    // Snap a cursor position (in the face plane) to the nearest valid center for the new block.
    // "Valid" means the new block's face aligns with a cell boundary of the hit block.
    // hitCenter/hitSize describe the hit block's axis; newSize is the new block's size on that axis.
    private float SnapFacePlane(float cursor, float hitCenter, int hitSize, int newSize)
    {
        float hitLeft = hitCenter - hitSize * BlockSize * 0.5f; // hit block's near face on this axis
        float newHalf = newSize   * BlockSize * 0.5f;           // new block's half-extent on this axis
        return hitLeft + Mathf.Round((cursor - hitLeft - newHalf) / BlockSize) * BlockSize + newHalf;
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
        GUI.Label(new Rect(10, 10, 500, 20),
            $"Tick: {_simulation.Tick}   Blocks: {_simulation.Blocks.Count}   Constructs: {_simulation.Constructs.Count}   [{_selectedDefinition.DisplayName}]");
    }
}