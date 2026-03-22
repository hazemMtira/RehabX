using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Builds the VR instruction screen in the Loading Scene.
/// Layout:
///   Row 1 — 4 gesture/objective cards (icons + text)
///   Row 2 — 4 mole type cards (3D models + name + score)
///
/// SETUP:
///   1. Attach to LoadingManager GameObject
///   2. Assign a World Space Canvas (wide format recommended: 1600x600)
///   3. Assign TMP Font Assets (Fredoka One for titles, Nunito for body)
///   4. Assign the 4 mole prefabs in the inspector
/// </summary>
public class InstructionsBuilder : MonoBehaviour
{
    [Header("Canvas")]
    public Canvas targetCanvas;

    [Header("Fonts")]
    public TMP_FontAsset titleFont;   // Fredoka One SDF
    public TMP_FontAsset bodyFont;    // Nunito Regular SDF

    [Header("Mole Prefabs")]
    public GameObject goodMole1;  // Apple_Basic
    public GameObject goodMole2;  // Jasper_Basic
    public GameObject goodMole3;  // Shanks_Basic
    public GameObject badMole;    // Sydney_Basic

    [Header("Mole Display Settings")]
    [Tooltip("Scale applied to mole models when placed in the instruction cards")]
    public float moleDisplayScale = 0.08f;
    [Tooltip("Height offset of mole model above its card center in world units")]
    public float moleVerticalOffset = 0.04f;

    // ---------------------------------------------------------------
    // Colors
    // ---------------------------------------------------------------

    static readonly Color BgDark       = new Color(0.04f, 0.07f, 0.12f, 0.97f);
    static readonly Color CardBg       = new Color(0.08f, 0.13f, 0.20f, 1f);
    static readonly Color CardBgBad    = new Color(0.18f, 0.06f, 0.06f, 1f);
    static readonly Color AccentCyan   = new Color(0f,    0.90f, 1f,    1f);
    static readonly Color AccentOrange = new Color(1f,    0.42f, 0.21f, 1f);
    static readonly Color AccentGold   = new Color(1f,    0.85f, 0f,    1f);
    static readonly Color AccentPurple = new Color(0.70f, 0.50f, 1f,    1f);
    static readonly Color AccentGreen  = new Color(0f,    1f,    0.60f, 1f);
    static readonly Color AccentRed    = new Color(1f,    0.30f, 0.30f, 1f);
    static readonly Color TextMuted    = new Color(0.75f, 0.82f, 0.90f, 1f);
    static readonly Color White        = Color.white;

    // ---------------------------------------------------------------
    // Gesture card data
    // ---------------------------------------------------------------

    struct GestureCard
    {
        public string icon;
        public string title;
        public string description;
        public Color  accent;
    }

    readonly GestureCard[] _gestureCards = new GestureCard[]
    {
        new GestureCard {
            icon        = "🤚",
            title       = "Raise Your Arm",
            description = "Lift your hand to shoulder height with your elbow bent to get ready.",
            accent      = AccentCyan
        },
        new GestureCard {
            icon        = "💥",
            title       = "Strike Down",
            description = "Swing your arm downward fast to hit a mole. Slow touches don't count!",
            accent      = AccentOrange
        },
        new GestureCard {
            icon        = "🏆",
            title       = "Reach the Score",
            description = "Hit enough good moles to reach the target score and win the round.",
            accent      = AccentGold
        },
        new GestureCard {
            icon        = "⏱",
            title       = "Beat the Clock",
            description = "If time runs out before you reach the score, the round ends. Stay focused!",
            accent      = AccentPurple
        }
    };

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    void Start()
    {
        if (targetCanvas == null)
        {
            Debug.LogError("[InstructionsBuilder] No target canvas assigned.");
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

        // Clear existing children
        foreach (Transform child in root)
            Destroy(child.gameObject);

        // Full background
        var bg = CreateRect("Background", root);
        bg.anchorMin = Vector2.zero;
        bg.anchorMax = Vector2.one;
        bg.offsetMin = Vector2.zero;
        bg.offsetMax = Vector2.zero;
        bg.gameObject.AddComponent<Image>().color = BgDark;

        // Main vertical container
        var container = CreateRect("Container", root);
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = new Vector2(30, 20);
        container.offsetMax = new Vector2(-30, -20);

        var vlg = container.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 16f;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 0, 10, 10);

        // Header
        BuildHeader(container);

        // Section 1 — How to play
        BuildSectionLabel(container, "HOW TO PLAY");
        BuildGestureRow(container);

        // Section 2 — Mole types
        BuildSectionLabel(container, "MOLE TYPES");
        BuildMoleRow(container);
    }

    // ---------------------------------------------------------------
    // Header
    // ---------------------------------------------------------------

    void BuildHeader(RectTransform parent)
    {
        var header = CreateRect("Header", parent);
        header.sizeDelta = new Vector2(0, 60);

        var title = CreateTMP("Title", header,
            "GET READY TO PLAY!",
            titleFont, 42f, FontStyles.Bold, AccentCyan);
        title.alignment = TextAlignmentOptions.Center;
        FillRect(title.rectTransform);
    }

    // ---------------------------------------------------------------
    // Section label
    // ---------------------------------------------------------------

    void BuildSectionLabel(RectTransform parent, string text)
    {
        var row = CreateRect($"Label_{text}", parent);
        row.sizeDelta = new Vector2(0, 28);

        var tmp = CreateTMP("LabelText", row,
            text, bodyFont, 15f, FontStyles.Bold,
            new Color(0.4f, 0.55f, 0.7f));
        tmp.alignment        = TextAlignmentOptions.Left;
        tmp.characterSpacing = 3f;
        FillRect(tmp.rectTransform);
    }

    // ---------------------------------------------------------------
    // Row 1 — Gesture cards
    // ---------------------------------------------------------------

    void BuildGestureRow(RectTransform parent)
    {
        var row = CreateRect("GestureRow", parent);
        row.sizeDelta = new Vector2(0, 175);

        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing              = 14f;
        hlg.childControlWidth    = true;
        hlg.childControlHeight   = true;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;

        foreach (var card in _gestureCards)
            BuildGestureCard(row, card);
    }

    void BuildGestureCard(RectTransform parent, GestureCard data)
    {
        var card = CreateRect($"GCard_{data.title}", parent);
        card.gameObject.AddComponent<Image>().color = CardBg;

        // Left accent bar
        var bar = CreateRect("Bar", card);
        bar.anchorMin = new Vector2(0, 0);
        bar.anchorMax = new Vector2(0, 1);
        bar.offsetMin = Vector2.zero;
        bar.offsetMax = new Vector2(5, 0);
        bar.gameObject.AddComponent<Image>().color = data.accent;

        // Content
        var content = CreateRect("Content", card);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(14, 10);
        content.offsetMax = new Vector2(-8, -10);

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 5f;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment       = TextAnchor.UpperLeft;

        // Icon
        var icon = CreateTMP("Icon", content,
            data.icon, titleFont, 30f, FontStyles.Normal, White);
        icon.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 38);

        // Title
        var title = CreateTMP("Title", content,
            data.title, titleFont, 20f, FontStyles.Bold, data.accent);
        title.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 26);

        // Description
        var desc = CreateTMP("Desc", content,
            data.description, bodyFont, 13f, FontStyles.Normal, TextMuted);
        desc.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 60);
        desc.enableWordWrapping = true;
    }

    // ---------------------------------------------------------------
    // Row 2 — Mole type cards
    // ---------------------------------------------------------------

    void BuildMoleRow(RectTransform parent)
    {
        var row = CreateRect("MoleRow", parent);
        row.sizeDelta = new Vector2(0, 255);

        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing              = 14f;
        hlg.childControlWidth    = true;
        hlg.childControlHeight   = true;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;

        BuildMoleCard(row, goodMole1, "Apple",  "+1", AccentGreen, true);
        BuildMoleCard(row, goodMole2, "Jasper", "+1", AccentGreen, true);
        BuildMoleCard(row, goodMole3, "Shanks", "+1", AccentGreen, true);
        BuildMoleCard(row, badMole,   "Sydney", "-1", AccentRed,   false);
    }

    void BuildMoleCard(RectTransform parent, GameObject molePrefab,
        string moleName, string scoreText, Color accent, bool isGood)
    {
        var card = CreateRect($"MoleCard_{moleName}", parent);
        card.gameObject.AddComponent<Image>().color = isGood ? CardBg : CardBgBad;

        // Top accent bar
        var bar = CreateRect("TopBar", card);
        bar.anchorMin = new Vector2(0, 1);
        bar.anchorMax = new Vector2(1, 1);
        bar.offsetMin = new Vector2(0, -5);
        bar.offsetMax = Vector2.zero;
        bar.gameObject.AddComponent<Image>().color = accent;

        // Content VLG
        var content = CreateRect("Content", card);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(8, 8);
        content.offsetMax = new Vector2(-8, -8);

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 4f;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment       = TextAnchor.UpperCenter;

        // Model slot — empty space where 3D model will float
        var modelSlot = CreateRect("ModelSlot", content);
        modelSlot.sizeDelta = new Vector2(0, 120);

        // Spawn 3D mole model positioned over this card
        if (molePrefab != null)
            SpawnMoleModel(molePrefab, card);

        // Score (+1 / -1)
        var score = CreateTMP("Score", content,
            scoreText, titleFont, 34f, FontStyles.Bold, accent);
        score.alignment = TextAlignmentOptions.Center;
        score.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 42);

        // Name
        var nameText = CreateTMP("Name", content,
            moleName, bodyFont, 17f, FontStyles.Bold, White);
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 22);

        // Instruction line
        string instruction = isGood ? "Hit me!" : "Don't hit me!";
        var instr = CreateTMP("Instruction", content,
            instruction, bodyFont, 14f, FontStyles.Bold, accent);
        instr.alignment = TextAlignmentOptions.Center;
        instr.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 20);
    }

    // ---------------------------------------------------------------
    // 3D Mole model placement
    // ---------------------------------------------------------------

    void SpawnMoleModel(GameObject prefab, RectTransform cardRect)
    {
        var model = Instantiate(prefab);
        model.name = $"Display_{prefab.name}";

        // Strip all game logic — display only
        foreach (var comp in model.GetComponentsInChildren<MonoBehaviour>())
            Destroy(comp);
        foreach (var col in model.GetComponentsInChildren<Collider>())
            Destroy(col);

        model.transform.localScale = Vector3.one * moleDisplayScale;

        // Parent to canvas root so it sits in world space correctly
        model.transform.SetParent(targetCanvas.transform, false);

        // Attach positioner to keep it aligned to its card every frame
        var positioner = model.AddComponent<MoleModelPositioner>();
        positioner.targetCard     = cardRect;
        positioner.verticalOffset = moleVerticalOffset;
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
}