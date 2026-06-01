// Settings panel — Mouse Sensitivity, Master Volume, Fullscreen, Graphics Quality.
// All values persisted to PlayerPrefs.
// BackTarget is set to PauseMenuPanel after construction (via SetBackTarget).
//
// Each continuous setting uses ◄ / ► step buttons.
// Discrete settings use a single cycle button.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPanel : MenuPanel
{
    // ── Layout ────────────────────────────────────────────────────────────────

    private const float CardW     = 340f;
    private const float PadV      =  16f;
    private const float PadH      =  16f;
    private const float TitleH    =  28f;
    private const float SectionGap=  10f;
    private const float RowH      =  32f;
    private const float RowGap    =   8f;
    private const float BackH     =  36f;
    private const float BackGap   =  12f;
    private const float InnerW    = CardW - PadH * 2f;  // 308 px
    private const float LabelFrac = 0.52f;
    private const float LabelW    = InnerW * LabelFrac;
    private const float CtrlW     = InnerW * (1f - LabelFrac);
    private const float ArrowW    = 28f;

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColCard    = new Color(0.08f, 0.08f, 0.08f, 0.97f);
    private static readonly Color ColBtn     = new Color(0.18f, 0.18f, 0.18f, 1.00f);
    private static readonly Color ColBtnHov  = new Color(0.28f, 0.28f, 0.28f, 1.00f);
    private static readonly Color ColBtnPrs  = new Color(0.40f, 0.32f, 0.06f, 1.00f);
    private static readonly Color ColBack    = new Color(0.15f, 0.15f, 0.15f, 1.00f);
    private static readonly Color ColRow     = new Color(0.12f, 0.12f, 0.12f, 1.00f);

    // ── PlayerPrefs keys ──────────────────────────────────────────────────────

    public const string PrefSensStep  = "sens_step";   // int 0-19
    public const string PrefVolStep   = "vol_step";    // int 0-10
    public const string PrefFullscreen= "fullscreen";  // int 0/1
    public const string PrefQuality   = "quality";     // int quality level index

    // Sensitivity steps: 0.25, 0.50, 0.75 … 5.00 (20 steps)
    private static float SensFromStep(int s) => (s + 1) * 0.25f;
    private static int   SensToStep(float v) =>
        Mathf.Clamp(Mathf.RoundToInt(v / 0.25f) - 1, 0, 19);

    // Volume steps: 0 % – 100 % (11 steps)
    private static float VolFromStep(int s) => s / 10f;

    // ── Factory ───────────────────────────────────────────────────────────────

    public static SettingsPanel Create(MenuManager menu)
    {
        var p = new SettingsPanel();
        p.Init(menu, "SettingsPanel");
        return p;
    }

    public void SetBackTarget(MenuPanel target) => BackTarget = target;

    // ── Apply saved settings to game systems (call on startup) ────────────────

    public static void ApplySaved()
    {
        int  sensStep = PlayerPrefs.GetInt(PrefSensStep, SensToStep(2f));
        int  volStep  = PlayerPrefs.GetInt(PrefVolStep,  10);
        bool full     = PlayerPrefs.GetInt(PrefFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        int  qual     = PlayerPrefs.GetInt(PrefQuality, QualitySettings.GetQualityLevel());

        var pc = Object.FindObjectOfType<PlayerController>();
        if (pc != null) pc.LookSensitivity = SensFromStep(sensStep);

        AudioListener.volume = VolFromStep(volStep);
        Screen.fullScreen    = full;
        QualitySettings.SetQualityLevel(qual, true);
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    protected override void Build()
    {
        const int rows = 4;
        float cardH = PadV + TitleH + SectionGap
                    + rows * RowH + (rows - 1) * RowGap
                    + BackGap + BackH + PadV;

        var card = MakeCard(CardW, cardH, ColCard);
        card.transform.SetParent(_root.transform, false);

        // Title
        var title = UIRoot.MakeText(card.transform, "Title", 15f, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        var tRT = title.GetComponent<RectTransform>();
        tRT.anchorMin        = new Vector2(0f, 1f);
        tRT.anchorMax        = new Vector2(1f, 1f);
        tRT.pivot            = new Vector2(0.5f, 1f);
        tRT.anchoredPosition = new Vector2(0f, -PadV);
        tRT.sizeDelta        = new Vector2(0f, TitleH);
        title.text = "Settings";

        float y = -(PadV + TitleH + SectionGap);

        // ── Mouse Sensitivity ─────────────────────────────────────────────────
        int sensStep = PlayerPrefs.GetInt(PrefSensStep, SensToStep(2f));
        AddStepRow(card.transform, "Mouse Sensitivity", y,
            getStep:    () => PlayerPrefs.GetInt(PrefSensStep, SensToStep(2f)),
            minStep:    0,
            maxStep:    19,
            formatVal:  s  => $"{SensFromStep(s):F2}x",
            onChanged:  s  =>
            {
                PlayerPrefs.SetInt(PrefSensStep, s);
                var pc = Object.FindObjectOfType<PlayerController>();
                if (pc != null) pc.LookSensitivity = SensFromStep(s);
            });
        y -= RowH + RowGap;

        // ── Master Volume ─────────────────────────────────────────────────────
        AddStepRow(card.transform, "Master Volume", y,
            getStep:    () => PlayerPrefs.GetInt(PrefVolStep, 10),
            minStep:    0,
            maxStep:    10,
            formatVal:  s  => $"{s * 10}%",
            onChanged:  s  =>
            {
                PlayerPrefs.SetInt(PrefVolStep, s);
                AudioListener.volume = VolFromStep(s);
            });
        y -= RowH + RowGap;

        // ── Fullscreen ────────────────────────────────────────────────────────
        AddCycleRow(card.transform, "Fullscreen", y,
            getLabel:  () => Screen.fullScreen ? "On" : "Off",
            onCycle:   () =>
            {
                bool f = !Screen.fullScreen;
                Screen.fullScreen = f;
                PlayerPrefs.SetInt(PrefFullscreen, f ? 1 : 0);
            });
        y -= RowH + RowGap;

        // ── Graphics Quality ──────────────────────────────────────────────────
        AddCycleRow(card.transform, "Graphics Quality", y,
            getLabel:  () => QualitySettings.names[QualitySettings.GetQualityLevel()],
            onCycle:   () =>
            {
                int next = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
                QualitySettings.SetQualityLevel(next, true);
                PlayerPrefs.SetInt(PrefQuality, next);
            });
        y -= RowH + RowGap;

        // ── Back ──────────────────────────────────────────────────────────────
        y -= BackGap - RowGap;   // replace last RowGap with bigger BackGap
        var backBtn = MakeButton(card.transform, "Back", ColBack, ColBtnHov, ColBtnPrs,
                                  13f, () => _menu.Open(BackTarget ?? _menu.Pause));
        var bRT = backBtn.GetComponent<RectTransform>();
        bRT.anchorMin        = new Vector2(0f, 1f);
        bRT.anchorMax        = new Vector2(1f, 1f);
        bRT.pivot            = new Vector2(0.5f, 1f);
        bRT.anchoredPosition = new Vector2(0f, y);
        bRT.sizeDelta        = new Vector2(-(PadH * 2f), BackH);
    }

    // ── Row builders ─────────────────────────────────────────────────────────

    // Step row: label | ◄  value  ► (step through discrete steps)
    // The row background is already inset by PadH on each side, so all
    // child positions are relative to the inset row — no extra PadH needed.
    private void AddStepRow(Transform parent, string labelText, float topY,
                             System.Func<int> getStep, int minStep, int maxStep,
                             System.Func<int, string> formatVal, System.Action<int> onChanged)
    {
        var row = MakeRowBackground(parent, topY);

        // Label (left portion)
        var lbl = UIRoot.MakeText(row.transform, "Label", 12f, TextAlignmentOptions.Left);
        var lRT = lbl.GetComponent<RectTransform>();
        lRT.anchorMin        = new Vector2(0f, 0f);
        lRT.anchorMax        = new Vector2(0f, 1f);
        lRT.pivot            = new Vector2(0f, 0.5f);
        lRT.anchoredPosition = new Vector2(0f, 0f);
        lRT.sizeDelta        = new Vector2(LabelW, 0f);
        lbl.text = labelText;

        // Control area starts where the label ends.
        float ctrlX = LabelW;

        // Value label (centre of control area, between the two arrows)
        var valLbl = UIRoot.MakeText(row.transform, "Value", 12f, TextAlignmentOptions.Center);
        var vRT = valLbl.GetComponent<RectTransform>();
        vRT.anchorMin        = new Vector2(0f, 0f);
        vRT.anchorMax        = new Vector2(0f, 1f);
        vRT.pivot            = new Vector2(0f, 0.5f);
        vRT.anchoredPosition = new Vector2(ctrlX + ArrowW, 0f);
        vRT.sizeDelta        = new Vector2(CtrlW - ArrowW * 2f, 0f);
        valLbl.text = formatVal(getStep());

        // ◄ button
        MakeArrowButton(row.transform, "◄", ctrlX, () =>
        {
            int s = Mathf.Max(minStep, getStep() - 1);
            onChanged(s);
            valLbl.text = formatVal(s);
        });

        // ► button
        MakeArrowButton(row.transform, "►", ctrlX + CtrlW - ArrowW, () =>
        {
            int s = Mathf.Min(maxStep, getStep() + 1);
            onChanged(s);
            valLbl.text = formatVal(s);
        });
    }

    // Cycle row: label | [  value  ] (single button cycles through options)
    private void AddCycleRow(Transform parent, string labelText, float topY,
                              System.Func<string> getLabel, System.Action onCycle)
    {
        var row = MakeRowBackground(parent, topY);

        var lbl = UIRoot.MakeText(row.transform, "Label", 12f, TextAlignmentOptions.Left);
        var lRT = lbl.GetComponent<RectTransform>();
        lRT.anchorMin        = new Vector2(0f, 0f);
        lRT.anchorMax        = new Vector2(0f, 1f);
        lRT.pivot            = new Vector2(0f, 0.5f);
        lRT.anchoredPosition = new Vector2(0f, 0f);
        lRT.sizeDelta        = new Vector2(LabelW, 0f);
        lbl.text = labelText;

        float ctrlX = LabelW;

        // Cycle button — also acts as value display
        var btn = MakeButton(row.transform, getLabel(), ColBtn, ColBtnHov, ColBtnPrs, 12f, null);
        var bRT = btn.GetComponent<RectTransform>();
        bRT.anchorMin        = new Vector2(0f, 0f);
        bRT.anchorMax        = new Vector2(0f, 1f);
        bRT.pivot            = new Vector2(0f, 0.5f);
        bRT.anchoredPosition = new Vector2(ctrlX, 0f);
        bRT.sizeDelta        = new Vector2(CtrlW, 0f);

        // Re-read the label from getLabel() after each toggle so it stays current.
        var cycleText = btn.GetComponentInChildren<TextMeshProUGUI>();
        btn.onClick.AddListener(() =>
        {
            onCycle();
            if (cycleText != null) cycleText.text = getLabel();
        });
    }

    // ── Small helpers ─────────────────────────────────────────────────────────

    private RectTransform MakeRowBackground(Transform parent, float topY)
    {
        var go = new GameObject("Row");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, topY);
        rt.sizeDelta        = new Vector2(-(PadH * 2f), RowH);

        var img = go.AddComponent<Image>();
        img.color         = ColRow;
        img.raycastTarget = false;
        return rt;
    }

    private static UnityEngine.UI.Button MakeArrowButton(Transform parent,
                                                          string arrow, float leftX,
                                                          System.Action onClick)
    {
        var go = new GameObject(arrow == "◄" ? "Dec" : "Inc");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(leftX, 0f);
        rt.sizeDelta        = new Vector2(ArrowW, 0f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.22f, 0.22f, 1f);

        var btn = go.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor      = img.color;
        cb.highlightedColor = new Color(0.32f, 0.32f, 0.32f, 1f);
        cb.pressedColor     = new Color(0.45f, 0.38f, 0.08f, 1f);
        cb.selectedColor    = img.color;
        cb.fadeDuration     = 0.05f;
        btn.colors          = cb;
        btn.onClick.AddListener(() => onClick());

        var lbl = UIRoot.MakeText(go.transform, "Lbl", 12f, TextAlignmentOptions.Center);
        UIRoot.StretchToParent(lbl.GetComponent<RectTransform>());
        lbl.text = arrow;
        return btn;
    }
}
