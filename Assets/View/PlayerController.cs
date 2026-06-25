// First-person character controller.
//
// Setup: attach to a GameObject that has a CharacterController component.
// The camera should be a child of this GameObject.
//
// Controls:
//   WASD       — move
//   Mouse      — look (cursor locked on start)
//   Space      — jump
//   Escape     — handled by MenuManager (opens pause menu)

using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed       = 8f;
    [SerializeField] private float _lookSensitivity = 2f;

    // Exposed so SettingsPanel can update it at runtime.
    public float LookSensitivity
    {
        get => _lookSensitivity;
        set => _lookSensitivity = value;
    }
    [SerializeField] private float _gravity         = -20f;
    [SerializeField] private float _jumpSpeed       = 7f;

    private CharacterController _cc;
    private Transform           _cameraTransform;

    private float _yaw;
    private float _pitch;
    private float _velocityY;

    private void Awake()
    {
        _cc              = GetComponent<CharacterController>();
        _cameraTransform = GetComponentInChildren<Camera>().transform;

        LockCursor(true);
    }

    private void Update()
    {
        // Only walk / look during on-foot gameplay (not in menus, panels, or while piloting).
        if (GameInput.Context != InputContext.Gameplay) return;
        HandleLook();
        HandleMove();
    }

    // Re-seeds the internal yaw/pitch from the current transforms so look control
    // resumes smoothly after an external system (e.g. VehiclePilot) repositioned
    // the player. Without this, HandleLook would snap back to the old angles.
    public void SyncLookFromTransform()
    {
        _yaw = transform.eulerAngles.y;
        _pitch = _cameraTransform != null ? _cameraTransform.localEulerAngles.x : 0f;
        if (_pitch > 180f) _pitch -= 360f;
        _pitch = Mathf.Clamp(_pitch, -85f, 85f);
    }

    // ── Look ──────────────────────────────────────────────────────────────────

    private void HandleLook()
    {
        Vector2 look = GameInput.Look;
        _yaw   += look.x * _lookSensitivity;
        _pitch -= look.y * _lookSensitivity;
        _pitch  = Mathf.Clamp(_pitch, -85f, 85f);

        // Yaw rotates the whole body so the forward vector stays correct for movement.
        transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        // Pitch rotates only the camera so the body stays upright.
        _cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    // ── Move ──────────────────────────────────────────────────────────────────

    private void HandleMove()
    {
        Vector2 m = GameInput.Move;
        var move = transform.forward * m.y
                 + transform.right   * m.x;

        if (move.sqrMagnitude > 1f) move.Normalize();

        if (_cc.isGrounded)
        {
            _velocityY = -1f; // small constant keeps isGrounded reliable next frame
            if (GameInput.JumpDown) _velocityY = _jumpSpeed;
        }
        else
        {
            _velocityY += _gravity * Time.deltaTime;
        }

        _cc.Move((move * _moveSpeed + Vector3.up * _velocityY) * Time.deltaTime);
    }

    // ── Cursor ────────────────────────────────────────────────────────────────

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }
}
