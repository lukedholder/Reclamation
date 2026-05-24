// Draws a simple crosshair at the centre of the screen while the cursor is locked.
//
// Setup: attach to the Player GameObject alongside PlayerController.

using UnityEngine;

public class Crosshair : MonoBehaviour
{
    private const int ArmLength = 8;    // pixels from centre to tip of each arm
    private const int ArmThick  = 2;    // pixel thickness of each arm
    private const int GapRadius = 2;    // blank gap around the centre point

    private void OnGUI()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float cx = Screen.width  * 0.5f;
        float cy = Screen.height * 0.5f;

        GUI.color = Color.white;

        // Horizontal arms (left and right of gap)
        GUI.DrawTexture(new Rect(cx - ArmLength, cy - ArmThick * 0.5f, ArmLength - GapRadius, ArmThick), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx + GapRadius,  cy - ArmThick * 0.5f, ArmLength - GapRadius, ArmThick), Texture2D.whiteTexture);

        // Vertical arms (above and below gap)
        GUI.DrawTexture(new Rect(cx - ArmThick * 0.5f, cy - ArmLength, ArmThick, ArmLength - GapRadius), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - ArmThick * 0.5f, cy + GapRadius,  ArmThick, ArmLength - GapRadius), Texture2D.whiteTexture);
    }
}
