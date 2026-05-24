// Fires a ray from the centre of the player camera every frame.
// Other components (placement, dismantling, interaction) read HasHit and Hit
// rather than each doing their own raycast.
//
// Setup: attach to the Player GameObject alongside PlayerController.
// Finds the child Camera automatically.

using UnityEngine;

public class Raycaster : MonoBehaviour
{
    [SerializeField] private float _reach = 10f;

    public bool       HasHit { get; private set; }
    public RaycastHit Hit    { get; private set; }

    private Camera _camera;

    private void Awake()
    {
        _camera = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            HasHit = false;
            return;
        }

        var ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        HasHit = Physics.Raycast(ray, out var hit, _reach);
        Hit    = hit;

        // A collider destroyed earlier this frame becomes Unity-null before end-of-frame cleanup.
        // Clear HasHit so every consumer that runs after a dismantle sees clean state.
        if (HasHit && Hit.collider == null)
            HasHit = false;

        if (HasHit)
            Debug.DrawLine(ray.origin, Hit.point, Color.green);
        else
            Debug.DrawRay(ray.origin, ray.direction * _reach, Color.red);
    }

    private void OnGUI()
    {
        string label = HasHit && Hit.collider != null
            ? $"Aim: {Hit.collider.gameObject.name}  dist {Hit.distance:F1} m"
            : "Aim: —";

        GUI.Label(new Rect(Screen.width / 2f - 150f, 8f, 300f, 24f), label);
    }
}
