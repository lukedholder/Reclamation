// Draws a simple crosshair at the centre of the screen while the cursor is locked.
//
// Setup: attach to the Player GameObject alongside PlayerController.
//        UIRoot must be in the scene.

using UnityEngine;
using UnityEngine.UI;

public class Crosshair : MonoBehaviour
{
    // All values are in Canvas pixels at the 1920 × 1080 reference resolution.
    private const float ArmLength = 8f;   // centre-to-tip of each arm
    private const float ArmThick  = 2f;   // arm thickness
    private const float GapRadius = 2f;   // blank gap around the centre point

    private GameObject _root;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Start()
    {
        BuildCrosshair();
    }

    private void Update()
    {
        _root.SetActive(Cursor.lockState == CursorLockMode.Locked);
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void BuildCrosshair()
    {
        // Root — pinned to screen centre with zero size so children position themselves.
        _root = new GameObject("Crosshair");
        _root.transform.SetParent(UIRoot.Canvas.transform, false);
        var rootRT = _root.AddComponent<RectTransform>();
        rootRT.anchorMin = rootRT.anchorMax = rootRT.pivot = new Vector2(0.5f, 0.5f);
        rootRT.sizeDelta = Vector2.zero;

        // Arm dimensions: the visible segment runs from GapRadius to ArmLength.
        float segLen   = ArmLength - GapRadius;         // 6 px
        float segCentre = GapRadius + segLen * 0.5f;    // 5 px from origin

        MakeArm("Left",   new Vector2(-segCentre, 0f), new Vector2(segLen, ArmThick));
        MakeArm("Right",  new Vector2( segCentre, 0f), new Vector2(segLen, ArmThick));
        MakeArm("Top",    new Vector2(0f,  segCentre), new Vector2(ArmThick, segLen));
        MakeArm("Bottom", new Vector2(0f, -segCentre), new Vector2(ArmThick, segLen));
    }

    private void MakeArm(string armName, Vector2 position, Vector2 size)
    {
        var go = new GameObject(armName);
        go.transform.SetParent(_root.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta        = size;

        var img = go.AddComponent<Image>();
        img.color         = Color.white;
        img.raycastTarget = false;
    }
}
