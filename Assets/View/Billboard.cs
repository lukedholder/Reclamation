// Rotates a GameObject to face the main camera every frame.
// Used by OreNode labels so they're readable from any angle.

using UnityEngine;

public class Billboard : MonoBehaviour
{
    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        transform.rotation = cam.transform.rotation;
    }
}
