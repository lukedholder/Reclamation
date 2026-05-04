// Moves the camera based on keyboard and mouse input.
// WASD to move, hold right mouse button to look around.

using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 10f;
    [SerializeField] private float _lookSpeed = 2f;

    private float _yaw;
    private float _pitch;

    private void Start()
    {
        _yaw   = transform.eulerAngles.y;
        _pitch = transform.eulerAngles.x;
    }

    private void Update()
    {
        if (Input.GetMouseButton(1))
        {
            _yaw   += Input.GetAxis("Mouse X") * _lookSpeed;
            _pitch -= Input.GetAxis("Mouse Y") * _lookSpeed;
            _pitch  = Mathf.Clamp(_pitch, -80f, 80f);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        var forward = transform.forward;
        var right   = transform.right;

        var move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += forward;
        if (Input.GetKey(KeyCode.S)) move -= forward;
        if (Input.GetKey(KeyCode.D)) move += right;
        if (Input.GetKey(KeyCode.A)) move -= right;

        transform.position += move * _moveSpeed * Time.deltaTime;
    }
}