// Abstract base for all menu panels managed by MenuManager.
// Each panel owns its root GameObject and all child UI.
// Subclasses implement Build() to populate children under _root.

using UnityEngine;
using UnityEngine.UI;

public abstract class MenuPanel
{
    // ESC routing while this panel is visible:
    //   null     → CloseAll (return to gameplay)
    //   non-null → flat-swap to that panel
    public MenuPanel BackTarget { get; protected set; }

    protected GameObject  _root;
    protected MenuManager _menu;

    // Call from subclass Create() factory before Build().
    protected void Init(MenuManager menu, string name)
    {
        _menu = menu;

        var go = new GameObject(name);
        go.transform.SetParent(UIRoot.Canvas.transform, false);

        var rt = go.AddComponent<RectTransform>();
        UIRoot.StretchToParent(rt);

        // Full-screen raycast-blocking backdrop.
        var bg = go.AddComponent<Image>();
        bg.color         = new Color(0f, 0f, 0f, 0.65f);
        bg.raycastTarget = true;

        _root = go;
        Build();
        Hide();
    }

    protected abstract void Build();

    public void Show() => _root.SetActive(true);
    public void Hide() => _root.SetActive(false);

    // ── Shared UI helpers ─────────────────────────────────────────────────────

    // Dark card centred on screen.
    protected static GameObject MakeCard(float width, float height, Color color)
    {
        var go = new GameObject("Card");
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        =
        rt.anchorMax        =
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(width, height);

        var img = go.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = true;
        return go;
    }

    // Styled button child of parent.
    protected static UnityEngine.UI.Button MakeButton(
        Transform parent, string label, Color normalCol, Color hoverCol, Color pressCol,
        float fontSize, System.Action onClick)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);

        go.AddComponent<RectTransform>();   // caller positions

        var img = go.AddComponent<Image>();
        img.color = normalCol;

        var btn = go.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor      = normalCol;
        cb.highlightedColor = hoverCol;
        cb.pressedColor     = pressCol;
        cb.selectedColor    = normalCol;
        cb.fadeDuration     = 0.06f;
        btn.colors          = cb;
        if (onClick != null) btn.onClick.AddListener(() => onClick());

        var lbl = UIRoot.MakeText(go.transform, "Label", fontSize,
                                   TMPro.TextAlignmentOptions.Center);
        UIRoot.StretchToParent(lbl.GetComponent<RectTransform>());
        lbl.text = label;

        return btn;
    }
}
