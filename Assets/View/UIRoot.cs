// Creates the single screen-space HUD Canvas shared by all HUD components.
// Must be attached to any persistent scene object (e.g. GameManager) so its
// Awake() runs before every component's Start().
//
// Other HUD components access the Canvas and Font through the static properties:
//   UIRoot.Canvas — parent transform for all HUD elements
//   UIRoot.Font   — TMP font asset (LiberationSans SDF by default)
//
// Requires: TextMeshPro package installed.
//           If Font is null at runtime, do:
//             Window → TextMeshPro → Import TMP Essential Resources

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIRoot : MonoBehaviour
{
    [Tooltip("Optional — leave blank to load LiberationSans SDF from TMP resources.")]
    [SerializeField] private TMP_FontAsset _font;

    public static Canvas        Canvas { get; private set; }
    public static TMP_FontAsset Font   { get; private set; }

    private void Awake()
    {
        // Canvas
        var go = new GameObject("HUD Canvas");
        Canvas = go.AddComponent<Canvas>();
        Canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        Canvas.sortingOrder = 100;

        // Scale relative to a 1920 × 1080 reference so elements stay the same
        // visual size regardless of screen resolution.
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        // EventSystem — required for Canvas Button clicks.  Create only if absent.
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Font: Inspector-assigned → TMP LiberationSans SDF (after Essential Resources import).
        Font = _font
            ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        if (Font == null)
            Debug.LogWarning("UIRoot: TMP font not found. " +
                "Run Window → TextMeshPro → Import TMP Essential Resources.");
    }

    // ── Shared factory helpers ────────────────────────────────────────────────

    // Creates a TextMeshProUGUI child with the shared font and common defaults.
    public static TextMeshProUGUI MakeText(Transform parent, string goName,
                                           float fontSize,
                                           TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (Font != null) t.font = Font;
        t.fontSize           = fontSize;
        t.color              = Color.white;
        t.alignment          = alignment;
        t.raycastTarget      = false;
        t.enableWordWrapping = false;
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
