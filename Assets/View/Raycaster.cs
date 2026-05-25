// Fires a ray from the centre of the player camera every frame.
// Other components (placement, dismantling, interaction) read HasHit and Hit
// rather than each doing their own raycast.
//
// Setup: attach to the Player GameObject alongside PlayerController.
// Finds the child Camera automatically.

using UnityEngine;
using UnityEngine.UI;

public class Raycaster : MonoBehaviour
{
    [SerializeField] private float _reach = 10f;

    public bool       HasHit { get; private set; }
    public RaycastHit Hit    { get; private set; }

    private Camera _camera;
    private Text   _aimLabel;

    private void Awake()
    {
        _camera = GetComponentInChildren<Camera>();
    }

    private void Start()
    {
        // Aim label — top-centre of screen.
        var go = new GameObject("AimLabel");
        go.transform.SetParent(UIRoot.Canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -8f);
        rt.sizeDelta        = new Vector2(400f, 24f);

        _aimLabel = go.AddComponent<Text>();
        if (UIRoot.Font != null) _aimLabel.font = UIRoot.Font;
        _aimLabel.fontSize      = 12;
        _aimLabel.color         = Color.white;
        _aimLabel.alignment     = TextAnchor.UpperCenter;
        _aimLabel.raycastTarget = false;
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            HasHit = false;
            if (_aimLabel != null) _aimLabel.text = string.Empty;
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

        if (_aimLabel != null)
            _aimLabel.text = HasHit && Hit.collider != null
                ? $"Aim: {Hit.collider.gameObject.name}  dist {Hit.distance:F1} m"
                : "Aim: —";
    }
}
