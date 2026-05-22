// First-person character controller.
//
// Setup: attach to a GameObject that has a CharacterController component.
// The camera should be a child of this GameObject.
//
// Controls:
//   WASD       — move
//   Mouse      — look (cursor locked on start)
//   Space      — jump
//   Escape     — unlock / re-lock cursor

using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed       = 8f;
    [SerializeField] private float _lookSensitivity = 2f;
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
        HandleCursorToggle();
        HandleLook();
        HandleMove();
    }

    // ── Look ──────────────────────────────────────────────────────────────────

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        _yaw   += Input.GetAxis("Mouse X") * _lookSensitivity;
        _pitch -= Input.GetAxis("Mouse Y") * _lookSensitivity;
        _pitch  = Mathf.Clamp(_pitch, -85f, 85f);

        // Yaw rotates the whole body so the forward vector stays correct for movement.
        transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        // Pitch rotates only the camera so the body stays upright.
        _cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    // ── Move ──────────────────────────────────────────────────────────────────

    private void HandleMove()
    {
        var move = transform.forward * Input.GetAxisRaw("Vertical")
                 + transform.right   * Input.GetAxisRaw("Horizontal");

        if (move.sqrMagnitude > 1f) move.Normalize();

        if (_cc.isGrounded)
        {
            _velocityY = -1f; // small constant keeps isGrounded reliable next frame
            if (Input.GetButtonDown("Jump")) _velocityY = _jumpSpeed;
        }
        else
        {
            _velocityY += _gravity * Time.deltaTime;
        }

        _cc.Move((move * _moveSpeed + Vector3.up * _velocityY) * Time.deltaTime);
    }

    // ── Cursor ────────────────────────────────────────────────────────────────

    private void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            LockCursor(Cursor.lockState != CursorLockMode.Locked);
    }

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }
}
