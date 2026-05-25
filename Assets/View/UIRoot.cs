// Creates the single screen-space HUD Canvas shared by all HUD components.
// Must be attached to any persistent scene object (e.g. GameManager) so its
// Awake() runs before every component's Start().
//
// Other HUD components access the Canvas and Font through the static properties:
//   UIRoot.Canvas — parent transform for all HUD elements
//   UIRoot.Font   — built-in font, or the one assigned in the Inspector

using UnityEngine;
using UnityEngine.UI;

public class UIRoot : MonoBehaviour
{
    [Tooltip("Optional — leave blank to use the built-in Unity font.")]
    [SerializeField] private Font _font;

    public static Canvas Canvas { get; private set; }
    public static Font   Font   { get; private set; }

    private void Awake()
    {
        // Canvas
        var go = new GameObject("HUD Canvas");
        Canvas = go.AddComponent<Canvas>();
        Canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        Canvas.sortingOrder = 100;

        // Scale relative to a 1920 × 1080 reference so elements stay the same
        // visual size regardless of screen resolution.
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        // Font: prefer Inspector-assigned → LegacyRuntime (Unity 2022+) → Arial.
        Font = _font
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    // ── Shared factory helpers ────────────────────────────────────────────────

    // Creates a Text child with the shared font and common defaults.
    public static Text MakeText(Transform parent, string goName,
                                int fontSize, TextAnchor alignment = TextAnchor.UpperLeft)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        if (Font != null) t.font = Font;
        t.fontSize       = fontSize;
        t.color          = Color.white;
        t.alignment      = alignment;
        t.raycastTarget  = false;
        t.supportRichText = false;
        return t;
    }

    // Creates an Image child (plain colour rectangle, no sprite).
    public static Image MakeImage(Transform parent, string goName, Color color)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;
        return img;
    }

    // Fills the RectTransform to cover its parent exactly.
    public static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
