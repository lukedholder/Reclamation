// A single item slot: coloured background, icon image, quantity badge, and click detection.
//
// Not a MonoBehaviour — created via ItemSlotWidget.Create() and owned by whichever
// panel (PlayerInventory, MachineInteractor, ChestInteractor) instantiates it.
// Destroy the slot by calling widget.Destroy() or destroying its Root GameObject.
//
// Usage:
//   var w = ItemSlotWidget.Create(panelTransform, "Slot_0");
//   w.SetGridCell(col, row);                           // position in a grid
//   w.OnSlotClicked = _ => DDC.HandleClick(_, take, place);
//   // each frame:
//   w.Refresh(stack);                                  // update visuals
//
// Drag-drop state is managed externally by DragDropController; this widget is purely
// visual + click-detection.  Current holds the last ItemStack written by Refresh().

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemSlotWidget
{
    // ── Layout ─────────────────────────────────────────────────────────────────

    public const float Size    = 52f;
    public const float SlotGap =  4f;

    // ── Colours ────────────────────────────────────────────────────────────────

    private static readonly Color ColEmpty    = new Color(0.18f, 0.18f, 0.18f, 1.00f);
    private static readonly Color ColHeld     = new Color(0.45f, 0.38f, 0.08f, 1.00f);
    private static readonly Color ColHover    = new Color(0.28f, 0.28f, 0.28f, 1.00f);
    private static readonly Color ColPressed  = new Color(0.55f, 0.45f, 0.10f, 1.00f);
    private static readonly Color ColQtyBg    = new Color(0.00f, 0.00f, 0.00f, 0.70f);

    // ── Public fields ──────────────────────────────────────────────────────────

    public GameObject             Root;
    public RectTransform          RT;

    /// <summary>Last stack written by Refresh().</summary>
    public ItemStack Current;

    /// <summary>Invoked on a single left-click.</summary>
    public Action<ItemSlotWidget> OnSlotClicked;

    /// <summary>Invoked when the slot is double-clicked (second click within DoubleClickThresh seconds).</summary>
    public Action<ItemSlotWidget> OnSlotDoubleClicked;

    private float _lastClickTime;
    private const float DoubleClickThresh = 0.30f;

    // ── Private visuals ────────────────────────────────────────────────────────

    private Image           _bg;
    private Image           _icon;
    private GameObject      _qtyBadge;
    private TextMeshProUGUI _qtyText;

    // ── Factory ────────────────────────────────────────────────────────────────

    /// <summary>Creates and parents a slot widget.  Anchored top-left of <paramref name="parent"/>.</summary>
    public static ItemSlotWidget Create(Transform parent, string goName)
    {
        var w  = new ItemSlotWidget();
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);

        w.Root = go;
        w.RT   = go.AddComponent<RectTransform>();
        w.RT.anchorMin = new Vector2(0f, 1f);
        w.RT.anchorMax = new Vector2(0f, 1f);
        w.RT.pivot     = new Vector2(0f, 1f);
        w.RT.sizeDelta = new Vector2(Size, Size);

        // ── Background + Button ───────────────────────────────────────────────
        w._bg               = go.AddComponent<Image>();
        w._bg.color         = ColEmpty;
        w._bg.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.image         = w._bg;
        btn.targetGraphic = w._bg;
        var cb = btn.colors;
        cb.normalColor      = ColEmpty;
        cb.highlightedColor = ColHover;
        cb.pressedColor     = ColPressed;
        cb.selectedColor    = ColEmpty;
        cb.fadeDuration     = 0.05f;
        btn.colors = cb;
        btn.onClick.AddListener(() =>
        {
            float now = Time.unscaledTime;
            if (now - w._lastClickTime < DoubleClickThresh)
            {
                w.OnSlotDoubleClicked?.Invoke(w);
                w._lastClickTime = 0f;   // reset so triple-click ≠ another double
            }
            else
            {
                w.OnSlotClicked?.Invoke(w);
                w._lastClickTime = now;
            }
        });

        // ── Icon ──────────────────────────────────────────────────────────────
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        w._icon                  = iconGO.AddComponent<Image>();
        w._icon.raycastTarget    = false;
        w._icon.preserveAspect   = true;
        var iconRT = w._icon.GetComponent<RectTransform>();
        UIRoot.StretchToParent(iconRT);
        iconRT.offsetMin = new Vector2(5f,  5f);
        iconRT.offsetMax = new Vector2(-5f, -5f);
        iconGO.SetActive(false);

        // ── Quantity badge (bottom strip) ─────────────────────────────────────
        w._qtyBadge = new GameObject("QtyBadge");
        w._qtyBadge.transform.SetParent(go.transform, false);
        var badgeBg = w._qtyBadge.AddComponent<Image>();
        badgeBg.color         = ColQtyBg;
        badgeBg.raycastTarget = false;
        var badgeRT = badgeBg.GetComponent<RectTransform>();
        badgeRT.anchorMin = new Vector2(0f, 0f);
        badgeRT.anchorMax = new Vector2(1f, 0f);
        badgeRT.offsetMin = new Vector2(0f,  0f);
        badgeRT.offsetMax = new Vector2(0f, 14f);

        w._qtyText = UIRoot.MakeText(w._qtyBadge.transform, "QtyText", 9f, TextAlignmentOptions.Right);
        var qtyRT = w._qtyText.GetComponent<RectTransform>();
        UIRoot.StretchToParent(qtyRT);
        qtyRT.offsetMin = new Vector2(2f, 1f);
        qtyRT.offsetMax = new Vector2(-2f, 0f);
        w._qtyBadge.SetActive(false);

        return w;
    }

    // ── Positioning ────────────────────────────────────────────────────────────

    /// <summary>Places the slot in a row-major grid (col 0, row 0 = top-left).</summary>
    public void SetGridCell(int col, int row)
    {
        RT.anchoredPosition = new Vector2(
             col * (Size + SlotGap),
            -row * (Size + SlotGap));
    }

    /// <summary>Places the slot at an explicit pixel offset from the parent's top-left anchor.</summary>
    public void SetOffset(float x, float y)
    {
        RT.anchoredPosition = new Vector2(x, -y);
    }

    // ── Visuals ────────────────────────────────────────────────────────────────

    /// <summary>Updates the slot visuals to match <paramref name="stack"/>.</summary>
    public void Refresh(ItemStack stack)
    {
        Current = stack;

        if (stack.IsEmpty)
        {
            _bg.color = ColEmpty;
            _icon.gameObject.SetActive(false);
            _qtyBadge.SetActive(false);
            return;
        }

        // Background tint
        var def = ItemLibrary.Get(stack.ItemId);
        _bg.color = def != null ? def.UiTint : new Color(0.24f, 0.26f, 0.22f, 1f);

        // Icon
        if (def?.Icon != null)
        {
            _icon.sprite = def.Icon;
            _icon.color  = Color.white;
            _icon.gameObject.SetActive(true);
        }
        else
        {
            _icon.gameObject.SetActive(false);
        }

        // Quantity badge (hidden for single items)
        if (stack.Quantity > 1)
        {
            _qtyText.text = stack.Quantity.ToString("N0");
            _qtyBadge.SetActive(true);
        }
        else
        {
            _qtyBadge.SetActive(false);
        }
    }

    /// <summary>
    /// Tints the background to show this slot is the drag source.
    /// Call with held=true when picking up, held=false when returning/placing.
    /// </summary>
    public void SetHeldAppearance(bool held)
    {
        if (held)
        {
            _bg.color = ColHeld;
        }
        else
        {
            // Restore normal colour based on current content
            if (Current.IsEmpty)
            {
                _bg.color = ColEmpty;
            }
            else
            {
                var def = ItemLibrary.Get(Current.ItemId);
                _bg.color = def != null ? def.UiTint : new Color(0.24f, 0.26f, 0.22f, 1f);
            }
        }
    }

    /// <summary>Destroys the root GameObject.</summary>
    public void Destroy() => UnityEngine.Object.Destroy(Root);
}
