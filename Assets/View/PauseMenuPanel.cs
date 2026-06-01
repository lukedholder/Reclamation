// Pause overlay shown when the player presses ESC during gameplay.
// Buttons: Resume, Settings, Save, Load, Quit.
// BackTarget = null  →  ESC closes entirely (returns to game).

using UnityEngine;
using UnityEngine.UI;

public class PauseMenuPanel : MenuPanel
{
    // ── Layout ────────────────────────────────────────────────────────────────

    private const float CardW  = 220f;
    private const float BtnH   =  38f;
    private const float BtnGap =   6f;
    private const float PadV   =  20f;
    private const float PadH   =  18f;

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColCard    = new Color(0.08f, 0.08f, 0.08f, 0.97f);
    private static readonly Color ColBtn     = new Color(0.18f, 0.18f, 0.18f, 1.00f);
    private static readonly Color ColBtnHov  = new Color(0.28f, 0.28f, 0.28f, 1.00f);
    private static readonly Color ColBtnPrs  = new Color(0.40f, 0.32f, 0.06f, 1.00f);
    private static readonly Color ColQuit    = new Color(0.30f, 0.08f, 0.08f, 1.00f);

    // ── Factory ───────────────────────────────────────────────────────────────

    public static PauseMenuPanel Create(MenuManager menu)
    {
        var p = new PauseMenuPanel();
        p.BackTarget = null;
        p.Init(menu, "PauseMenuPanel");
        return p;
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    protected override void Build()
    {
        const int N      = 5;   // number of buttons
        float     cardH  = PadV * 2f + N * BtnH + (N - 1) * BtnGap;

        var card = MakeCard(CardW, cardH, ColCard);
        card.transform.SetParent(_root.transform, false);

        string[] labels = { "Resume", "Settings", "Save", "Load", "Quit" };
        Color[]  colors = { ColBtn, ColBtn, ColBtn, ColBtn, ColQuit };

        for (int i = 0; i < N; i++)
        {
            float topY      = -(PadV + i * (BtnH + BtnGap));
            int   captured  = i;

            var btn = MakeButton(card.transform, labels[i], colors[i], ColBtnHov, ColBtnPrs,
                                  13f, () => OnButton(captured));

            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, topY);
            rt.sizeDelta        = new Vector2(-(PadH * 2f), BtnH);
        }
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void OnButton(int index)
    {
        switch (index)
        {
            case 0:                                                   // Resume
                _menu.CloseAll();
                break;
            case 1:                                                   // Settings
                _menu.Open(_menu.Settings);
                break;
            case 2:                                                   // Save
                Object.FindObjectOfType<SaveLoadManager>()?.SaveGame();
                _menu.CloseAll();
                break;
            case 3:                                                   // Load
                Object.FindObjectOfType<SaveLoadManager>()?.LoadGame();
                _menu.CloseAll();
                break;
            case 4:                                                   // Quit
                Application.Quit();
                break;
        }
    }
}
