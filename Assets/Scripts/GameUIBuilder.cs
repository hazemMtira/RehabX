using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Builds the in-game scoreboard UI programmatically.
/// Styled as a wooden farm sign with timer, score, and goal.
///
/// SETUP:
///   1. Create a World Space Canvas in your game scene
///   2. Set Canvas Width=1200 Height=300 Scale=0.0003
///   3. Attach this script to any GameObject in the game scene
///   4. Assign the canvas and font in the inspector
///   5. Call UpdateTimer() and UpdateScore() from RoundController
/// </summary>
public class GameUIBuilder : MonoBehaviour
{
    [Header("Canvas")]
    public Canvas targetCanvas;

    [Header("Font")]
    public TMP_FontAsset titleFont;  // Fredoka One SDF

    [Header("Score Settings")]
    [Tooltip("Pulled from GameSessionSettings at Start")]
    public int targetScore = 10;

    // ---------------------------------------------------------------
    // Colors — farm wood theme
    // ---------------------------------------------------------------

    static readonly Color WoodDark     = HexColor("5C3D1E");
    static readonly Color WoodMid      = HexColor("8B5E3C");
    static readonly Color WoodLight    = HexColor("A0714F");
    static readonly Color TextCream    = HexColor("FFE88A");
    static readonly Color TextWhite    = HexColor("FFF8E7");
    static readonly Color AccentGreen  = HexColor("7BC67E");
    static readonly Color AccentOrange = HexColor("FF9F43");
    static readonly Color Divider      = HexColor("5C3D1E");

    // ---------------------------------------------------------------
    // Live text references — updated by RoundController
    // ---------------------------------------------------------------

    TextMeshProUGUI _timerText;
    TextMeshProUGUI _scoreText;
    TextMeshProUGUI _goalText;

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    void Start()
    {
        // Read target score from session settings
        if (GameSessionSettings.Instance?.selectedDifficulty != null)
            targetScore = GameSessionSettings.Instance.selectedDifficulty.requiredScore;

        if (targetCanvas == null)
        {
            Debug.LogError("[GameUIBuilder] No canvas assigned.");
            return;
        }

        Build();
    }

    // ---------------------------------------------------------------
    // Build
    // ---------------------------------------------------------------

    void Build()
    {
        Transform root = targetCanvas.transform;

        // Clear existing
        foreach (Transform child in root)
            Destroy(child.gameObject);

        // ── Outer border (dark wood) ──
        var outer = CreateRect("Outer", root);
        FillRect(outer);
        var outerImg = outer.gameObject.AddComponent<Image>();
        outerImg.color = WoodDark;

        // ── Main panel (mid wood) ──
        var panel = CreateRect("Panel", root);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = new Vector2(8, 8);
        panel.offsetMax = new Vector2(-8, -8);
        var panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.color = WoodMid;

        // ── Inner highlight (lighter wood top edge) ──
        var highlight = CreateRect("Highlight", panel);
        highlight.anchorMin = new Vector2(0, 1);
        highlight.anchorMax = new Vector2(1, 1);
        highlight.offsetMin = new Vector2(4, -6);
        highlight.offsetMax = new Vector2(-4, -2);
        highlight.gameObject.AddComponent<Image>().color = WoodLight;

        // ── Top row: TIMER ──
        var topRow = CreateRect("TopRow", panel);
        topRow.anchorMin = new Vector2(0, 0.5f);
        topRow.anchorMax = new Vector2(1, 1f);
        topRow.offsetMin = new Vector2(20, 4);
        topRow.offsetMax = new Vector2(-20, -4);

        var timerLabel = CreateTMP("TimerLabel", topRow,
            "⏱  TIME:", titleFont, 28f, FontStyles.Bold,
            new Color(TextCream.r, TextCream.g, TextCream.b, 0.7f));
        timerLabel.alignment = TextAlignmentOptions.MidlineLeft;
        timerLabel.rectTransform.anchorMin = new Vector2(0, 0);
        timerLabel.rectTransform.anchorMax = new Vector2(0.4f, 1);
        timerLabel.rectTransform.offsetMin = Vector2.zero;
        timerLabel.rectTransform.offsetMax = Vector2.zero;

        _timerText = CreateTMP("TimerValue", topRow,
            "1:00", titleFont, 36f, FontStyles.Bold, TextCream);
        _timerText.alignment = TextAlignmentOptions.MidlineLeft;
        _timerText.rectTransform.anchorMin = new Vector2(0.4f, 0);
        _timerText.rectTransform.anchorMax = new Vector2(1f, 1);
        _timerText.rectTransform.offsetMin = Vector2.zero;
        _timerText.rectTransform.offsetMax = Vector2.zero;

        // ── Divider line ──
        var divider = CreateRect("Divider", panel);
        divider.anchorMin = new Vector2(0.02f, 0.5f);
        divider.anchorMax = new Vector2(0.98f, 0.5f);
        divider.offsetMin = new Vector2(0, -1);
        divider.offsetMax = new Vector2(0, 1);
        divider.gameObject.AddComponent<Image>().color = Divider;

        // ── Bottom row: SCORE + GOAL ──
        var bottomRow = CreateRect("BottomRow", panel);
        bottomRow.anchorMin = new Vector2(0, 0);
        bottomRow.anchorMax = new Vector2(1, 0.5f);
        bottomRow.offsetMin = new Vector2(20, 4);
        bottomRow.offsetMax = new Vector2(-20, -4);

        // SCORE label
        var scoreLabel = CreateTMP("ScoreLabel", bottomRow,
            "SCORE:", titleFont, 24f, FontStyles.Bold,
            new Color(TextCream.r, TextCream.g, TextCream.b, 0.7f));
        scoreLabel.alignment = TextAlignmentOptions.MidlineLeft;
        scoreLabel.rectTransform.anchorMin = new Vector2(0, 0);
        scoreLabel.rectTransform.anchorMax = new Vector2(0.22f, 1);
        scoreLabel.rectTransform.offsetMin = Vector2.zero;
        scoreLabel.rectTransform.offsetMax = Vector2.zero;

        // SCORE value
        _scoreText = CreateTMP("ScoreValue", bottomRow,
            "0", titleFont, 36f, FontStyles.Bold, AccentGreen);
        _scoreText.alignment = TextAlignmentOptions.MidlineLeft;
        _scoreText.rectTransform.anchorMin = new Vector2(0.22f, 0);
        _scoreText.rectTransform.anchorMax = new Vector2(0.45f, 1);
        _scoreText.rectTransform.offsetMin = Vector2.zero;
        _scoreText.rectTransform.offsetMax = Vector2.zero;

        // Vertical separator between score and goal
        var sep = CreateRect("Sep", bottomRow);
        sep.anchorMin = new Vector2(0.5f, 0.1f);
        sep.anchorMax = new Vector2(0.5f, 0.9f);
        sep.offsetMin = new Vector2(-1, 0);
        sep.offsetMax = new Vector2(1, 0);
        sep.gameObject.AddComponent<Image>().color = Divider;

        // GOAL label
        var goalLabel = CreateTMP("GoalLabel", bottomRow,
            "GOAL:", titleFont, 24f, FontStyles.Bold,
            new Color(TextCream.r, TextCream.g, TextCream.b, 0.7f));
        goalLabel.alignment = TextAlignmentOptions.MidlineLeft;
        goalLabel.rectTransform.anchorMin = new Vector2(0.52f, 0);
        goalLabel.rectTransform.anchorMax = new Vector2(0.74f, 1);
        goalLabel.rectTransform.offsetMin = Vector2.zero;
        goalLabel.rectTransform.offsetMax = Vector2.zero;

        // GOAL value
        _goalText = CreateTMP("GoalValue", bottomRow,
            targetScore.ToString(), titleFont, 36f, FontStyles.Bold, AccentOrange);
        _goalText.alignment = TextAlignmentOptions.MidlineLeft;
        _goalText.rectTransform.anchorMin = new Vector2(0.74f, 0);
        _goalText.rectTransform.anchorMax = new Vector2(1f, 1);
        _goalText.rectTransform.offsetMin = Vector2.zero;
        _goalText.rectTransform.offsetMax = Vector2.zero;

        // ── Corner nail details ──
        BuildNail(panel, new Vector2(0, 1), new Vector2(18, -18));
        BuildNail(panel, new Vector2(1, 1), new Vector2(-18, -18));
        BuildNail(panel, new Vector2(0, 0), new Vector2(18, 18));
        BuildNail(panel, new Vector2(1, 0), new Vector2(-18, 18));
    }

    // ---------------------------------------------------------------
    // Corner nail detail
    // ---------------------------------------------------------------

    void BuildNail(RectTransform parent, Vector2 anchor, Vector2 offset)
    {
        var nail = CreateRect("Nail", parent);
        nail.anchorMin = anchor;
        nail.anchorMax = anchor;
        nail.sizeDelta = new Vector2(16, 16);
        nail.anchoredPosition = offset;

        var img = nail.gameObject.AddComponent<Image>();
        img.color = WoodDark;

        // Inner highlight
        var inner = CreateRect("NailInner", nail);
        inner.anchorMin = new Vector2(0.2f, 0.2f);
        inner.anchorMax = new Vector2(0.6f, 0.6f);
        inner.offsetMin = Vector2.zero;
        inner.offsetMax = Vector2.zero;
        inner.gameObject.AddComponent<Image>().color =
            new Color(1f, 1f, 1f, 0.25f);
    }

    // ---------------------------------------------------------------
    // Public update methods — called by RoundController
    // ---------------------------------------------------------------

    /// <summary>Call every frame from RoundController with remaining seconds.</summary>
    public void UpdateTimer(float remainingSeconds)
    {
        if (_timerText == null) return;
        int mins = Mathf.FloorToInt(remainingSeconds / 60f);
        int secs = Mathf.FloorToInt(remainingSeconds % 60f);
        _timerText.text = $"{mins}:{secs:00}";
    }

    /// <summary>Call from ScoreManager.OnScoreChanged.</summary>
    public void UpdateScore(int score)
    {
        if (_scoreText == null) return;
        _scoreText.text = score.ToString();

        // Flash green when score increases
        StopAllCoroutines();
        StartCoroutine(FlashScore());
    }

    System.Collections.IEnumerator FlashScore()
    {
        _scoreText.color = Color.white;
        yield return new WaitForSeconds(0.15f);
        _scoreText.color = AccentGreen;
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    RectTransform CreateRect(string name, Transform parent)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    void FillRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    TextMeshProUGUI CreateTMP(string name, RectTransform parent,
        string text, TMP_FontAsset font, float size,
        FontStyles style, Color color)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = size;
        tmp.fontStyle          = style;
        tmp.color              = color;
        tmp.enableWordWrapping = false;
        if (font != null) tmp.font = font;

        return tmp;
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}