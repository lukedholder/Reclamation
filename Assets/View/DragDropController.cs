// Click-to-hold drag-and-drop for item slots.
//
// Click an occupied slot to pick it up (holds the item + shows a ghost under the cursor).
// Click another slot to place the held item there.
// Right-click to cancel (returns item to the source slot).
// ESC is handled by MenuManager which closes panels → ClosePanel() → CancelDrag().
//
// HandleClick signature:
//   takeFn  — called when nothing is held; returns the ItemStack taken from the slot.
//   placeFn — called when something is held; receives the held stack, returns the leftover
//             (items that didn't fit).  Return held unchanged to fully reject.
//
// Widget may be null for slots that don't support the held-appearance highlight (e.g. gear rows).
//
// Attach to any persistent scene object (GameManager recommended).
// UIRoot must be in the scene so the ghost can be parented to the canvas.

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DragDropController : MonoBehaviour
{
    public static DragDropController Instance { get; private set; }

    // ── Drag state ─────────────────────────────────────────────────────────────

    private ItemStack                  _held;
    private Func<ItemStack, ItemStack> _sourceReturn;  // place-back callback for cancel
    private ItemSlotWidget             _sourceWidget;  // may be null

    public bool IsHolding => !_held.IsEmpty;

    // ── Ghost visual ───────────────────────────────────────────────────────────

    private GameObject      _ghost;
    private RectTransform   _ghostRT;
    private Image           _ghostBg;
    private Image           _ghostIcon;
    private TextMeshProUGUI _ghostQty;

    // ── Unity ──────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        BuildGhost();
    }

    private void Update()
    {
        if (!IsHolding) return;

        // Ghost follows the cursor in canvas-local space.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                UIRoot.Canvas.GetComponent<RectTransform>(),
                Input.mousePosition,
                null,   // Screen Space Overlay — no camera
                out var localPos))
        {
            _ghostRT.anchoredPosition = localPos;
        }

        // Right-click cancels drag.
        if (Input.GetMouseButtonDown(1))
            CancelDrag();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when a slot is left-clicked.
    /// <paramref name="widget"/> may be null for slots without visual held-state support.
    /// </summary>
    public void HandleClick(
        ItemSlotWidget             widget,
        Func<ItemStack>            takeFn,
        Func<ItemStack, ItemStack> placeFn)
    {
        if (!IsHolding)
        {
            // ── Pick up ───────────────────────────────────────────────────────
            var taken = takeFn();
            if (taken.IsEmpty) return;

            _held         = taken;
            _sourceReturn = placeFn;
            _sourceWidget = widget;

            widget?.SetHeldAppearance(true);
            UpdateGhost();
            _ghost.SetActive(true);
        }
        else
        {
            // ── Put down ──────────────────────────────────────────────────────
            var leftover = placeFn(_held);

            if (leftover.IsEmpty)
            {
                // All placed
                ClearHeldState();
            }
            else if (leftover.ItemId == _held.ItemId && leftover.Quantity == _held.Quantity)
            {
                // Fully rejected — click on same source slot acts as cancel
                if (widget == _sourceWidget)
                    CancelDrag();
                // else: stay holding, try a different slot
            }
            else
            {
                // Partially placed, or swapped (different item returned)
                _held = leftover;
                UpdateGhost();
            }
        }
    }

    /// <summary>Returns the held item to its source slot and clears drag state.</summary>
    public void CancelDrag()
    {
        if (!IsHolding) return;
        _sourceReturn?.Invoke(_held);
        ClearHeldState();
    }

    // ── Private ────────────────────────────────────────────────────────────────

    private void ClearHeldState()
    {
        _sourceWidget?.SetHeldAppearance(false);
        _sourceWidget = null;
        _held         = default;
        _sourceReturn = null;
        _ghost.SetActive(false);
    }

    private void UpdateGhost()
    {
        var def = ItemLibrary.Get(_held.ItemId);
        _ghostBg.color = def != null ? def.UiTint : new Color(0.24f, 0.26f, 0.22f, 0.85f);

        if (def?.Icon != null)
        {
            _ghostIcon.sprite = def.Icon;
            _ghostIcon.color  = Color.white;
            _ghostIcon.gameObject.SetActive(true);
        }
        else
        {
            _ghostIcon.gameObject.SetActive(false);
        }

        _ghostQty.text = _held.Quantity > 1 ? _held.Quantity.ToString("N0") : "";

        // Render on top of all other UI
        _ghost.transform.SetAsLastSibling();
    }

    private void BuildGhost()
    {
        _ghost = new GameObject("DragGhost");
        _ghost.transform.SetParent(UIRoot.Canvas.transform, false);

        _ghostRT           = _ghost.AddComponent<RectTransform>();
        _ghostRT.anchorMin = _ghostRT.anchorMax = _ghostRT.pivot = new Vector2(0.5f, 0.5f);
        _ghostRT.sizeDelta = new Vector2(ItemSlotWidget.Size, ItemSlotWidget.Size);

        // Semi-transparent background
        _ghostBg               = _ghost.AddComponent<Image>();
        _ghostBg.color         = new Color(0.22f, 0.22f, 0.22f, 0.85f);
        _ghostBg.raycastTarget = false;

        // Icon
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(_ghost.transform, false);
        _ghostIcon               = iconGO.AddComponent<Image>();
        _ghostIcon.raycastTarget  = false;
        _ghostIcon.preserveAspect = true;
        var iconRT = _ghostIcon.GetComponent<RectTransform>();
        UIRoot.StretchToParent(iconRT);
        iconRT.offsetMin = new Vector2(5f,  5f);
        iconRT.offsetMax = new Vector2(-5f, -5f);

        // Quantity text
        _ghostQty = UIRoot.MakeText(_ghost.transform, "GhostQty", 9f, TextAlignmentOptions.BottomRight);
        var qtyRT = _ghostQty.GetComponent<RectTransform>();
        UIRoot.StretchToParent(qtyRT);
        qtyRT.offsetMin = new Vector2(2f, 2f);
        qtyRT.offsetMax = new Vector2(-2f, -2f);

        _ghost.SetActive(false);
    }
}
