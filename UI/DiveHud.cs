using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// HUD of the flooded house, built at runtime like InventoryHud: a dot crosshair, the use prompt,
// one-line messages, radio subtitles, the current objective, the air gauge (only while it matters),
// the note reader (the world pauses while it is open), the red rim of suffocation, the end and death
// cards, the skip hint and a full-screen fade.
//
// Legibility over any background, the way current games do it: every line sits on a dark,
// translucent rounded plate sized to its text, keys are drawn as bright keycaps (not "[E]" in the
// same font), and all text carries a soft SDF underlay so it survives even where a plate would be
// too heavy (the objective in the corner).
public sealed class DiveHud : MonoBehaviour
{
    [SerializeField]
    private TMP_FontAsset font;

    [SerializeField]
    private AirSupply air;

    private static readonly Color Paper = new Color(0.86f, 0.82f, 0.7f, 1f);
    private static readonly Color PaperInk = new Color(0.16f, 0.13f, 0.1f, 1f);
    private static readonly Color SlateFace = new Color(0.86f, 0.88f, 0.86f, 1f);
    private static readonly Color SlateInk = new Color(0.27f, 0.28f, 0.3f, 1f);
    public static readonly Color Plate = new Color(0.02f, 0.03f, 0.03f, 0.72f);
    private static readonly Color KeycapFace = new Color(0.93f, 0.9f, 0.82f, 1f);
    private static readonly Color KeycapInk = new Color(0.08f, 0.07f, 0.06f, 1f);
    private static readonly Color AirColor = new Color(0.62f, 0.86f, 0.84f, 1f);
    private static readonly Color HeatColor = new Color(1f, 0.52f, 0.25f, 1f);
    private static readonly Color LowColor = new Color(0.95f, 0.25f, 0.2f, 1f);

    private Image dot;
    private Line prompt;
    private TextMeshProUGUI promptKey;
    private Line message;
    private Line subtitle;
    private TextMeshProUGUI objective;
    private CanvasGroup objectiveGroup;
    private CanvasGroup airGroup;
    private Image airFill;
    private TextMeshProUGUI airLabel;
    private GameObject notePanel;
    private TextMeshProUGUI noteBody;
    private Image notePaper;
    private TextMeshProUGUI noteSignature;
    private RawImage notePhoto;
    private Image sporeEdge;
    private CanvasGroup diveControls;
    private bool diveControlsVisible;
    private CanvasGroup endCard;
    private GameObject endButtons;
    private GameObject deathButtons;
    private System.Action retryAction;
    private Image damageEdge;
    private TextMeshProUGUI endTitle;
    private TextMeshProUGUI endLine;
    private CanvasGroup skipGroup;
    private Image skipFill;
    private Image fade;
    private bool promptVisible;
    private float messageUntil;
    private float subtitleUntil;
    private float skipVisibleUntil;
    private Coroutine fadeRoutine;

    // The fixed labels, refreshed when the language changes (never resolved per frame).
    private string objectiveKey;
    private TextMeshProUGUI objectiveHeader;
    private TextMeshProUGUI diveHeader;
    private readonly TextMeshProUGUI[] diveLabels = new TextMeshProUGUI[DiveRows.Length];
    private readonly TextMeshProUGUI[] diveKeys = new TextMeshProUGUI[DiveRows.Length];
    private TextMeshProUGUI skipLabel;
    private TextMeshProUGUI skipKey;
    private TextMeshProUGUI closeLabel;
    private string labelAir;
    private string labelAirHeat;
    private string labelNoAir;
    private string labelLife;

    // The dive controls panel: (key cap, action) as Loc keys; a cap key that is not in the table shows as written.
    private static readonly string[,] DiveRows = { { "hud.key.space", "hud.dive.up" }, { "Ctrl", "hud.dive.down" }, { "Shift", "hud.dive.fast" }, { "F", "hud.dive.torch" } };

    // One line of text on its own auto-sized plate, faded as a unit.
    private sealed class Line
    {
        public CanvasGroup group;
        public TextMeshProUGUI text;
        public LayoutElement width;
    }

    // Widest a message or subtitle plate gets (reference 1920 px wide): longer text wraps onto more lines
    // instead of running off both edges of the screen.
    private const float MaxLineWidth = 1400f;

    private static void Fit(Line line, string text, float maxWidth)
    {
        line.text.text = text;
        float natural = line.text.GetPreferredValues(text, 100000f, 0f).x;
        line.width.preferredWidth = Mathf.Min(natural + 2f, maxWidth);
    }

    public bool IsNoteOpen => notePanel != null && notePanel.activeSelf;

    public event System.Action NoteClosed;

    private void Awake()
    {
        HudFactory.Font = font;
        Build();
        ApplyLanguage();
    }

    private void OnEnable()
    {
        Loc.Changed += ApplyLanguage;
    }

    private void OnDisable()
    {
        Loc.Changed -= ApplyLanguage;
    }

    // Everything that stays on screen follows the language at once; passing messages and subtitles are resolved by
    // whoever shows them.
    private void ApplyLanguage()
    {
        if (objectiveHeader == null)
        {
            return;
        }

        objectiveHeader.text = Loc.T("hud.objective").ToUpperInvariant();
        if (!string.IsNullOrEmpty(objectiveKey))
        {
            objective.text = Loc.T(objectiveKey);
        }

        diveHeader.text = Loc.T("hud.dive.title").ToUpperInvariant();
        for (int i = 0; i < DiveRows.GetLength(0); i++)
        {
            diveKeys[i].text = Loc.Resolve(DiveRows[i, 0]);
            diveLabels[i].text = Loc.T(DiveRows[i, 1]);
        }

        skipLabel.text = Loc.T("hud.skip");
        skipKey.text = Loc.T("hud.key.space");
        closeLabel.text = Loc.T("hud.close");
        labelAir = Loc.T("hud.air");
        labelAirHeat = Loc.T("hud.air.heat");
        labelNoAir = Loc.T("hud.noair");
        labelLife = Loc.T("hud.life");
    }

    public void SetPrompt(string key, string label)
    {
        promptVisible = !string.IsNullOrEmpty(label);
        if (promptVisible)
        {
            promptKey.text = key;
            prompt.text.text = label;
        }

        dot.color = new Color(1f, 1f, 1f, promptVisible ? 0.9f : 0.35f);
        dot.rectTransform.sizeDelta = Vector2.one * (promptVisible ? 9f : 6f);
    }

    // A weapon's own crosshair is up (RevolverCrosshair): the dot here would sit on top of it.
    public void HideDot(bool hidden)
    {
        if (dot != null)
        {
            dot.enabled = !hidden;
        }
    }

    public void ClearPrompt()
    {
        SetPrompt(null, null);
    }

    public void ShowMessage(string text, float seconds)
    {
        Fit(message, text, MaxLineWidth);
        messageUntil = Time.time + seconds;
    }

    public void ShowSubtitle(string text, float seconds)
    {
        Fit(subtitle, text, MaxLineWidth);
        subtitleUntil = Time.time + seconds;
    }

    public void ClearSubtitle()
    {
        subtitleUntil = 0f;
    }

    // Plain text (or empty to hide); it does not follow a language change.
    public void SetObjective(string text)
    {
        objectiveKey = null;
        objective.text = string.IsNullOrEmpty(text) ? string.Empty : text;
    }

    // A Loc key: the objective stays in the corner, so it is re-resolved when the language changes.
    public void SetObjectiveKey(string key)
    {
        objectiveKey = key;
        objective.text = string.IsNullOrEmpty(key) ? string.Empty : Loc.T(key);
    }

    // Paper for letters in the dry house; a diver's slate (white plastic, pencil) for what was written
    // under water, where paper would have dissolved.
    // A photo (clippings, the graduation picture) sits on the upper half of the paper, the text below it.
    public void ShowNote(string body, string signature, bool slate = false, Texture photo = null)
    {
        notePhoto.texture = photo;
        notePhoto.gameObject.SetActive(photo != null);
        // The picture keeps its own proportions: prints are 3:2 across the top; a page in portrait (the newspaper)
        // gets a taller sheet so the text still fits under it.
        float textTop = 64f;
        float paperHeight = 760f;
        float paperLift = 0f;
        float bodySize = photo != null ? 26f : 30f;
        if (photo != null)
        {
            float aspect = photo.width / (float)photo.height;
            bool portrait = aspect < 1f;
            Vector2 size = portrait ? new Vector2(520f * aspect, 520f) : new Vector2(480f, 480f / aspect);
            notePhoto.rectTransform.sizeDelta = size;
            textTop = 56f + size.y + 24f;
            // The tall sheet is lifted so its foot stays clear of the "close" keycap at the bottom of the screen.
            paperHeight = portrait ? 940f : 760f;
            paperLift = portrait ? 60f : 0f;
            bodySize = portrait ? 22f : 26f;
        }

        notePaper.rectTransform.sizeDelta = new Vector2(760f, paperHeight);
        notePaper.rectTransform.anchoredPosition = new Vector2(0f, paperLift);
        noteBody.rectTransform.offsetMax = new Vector2(-64f, -textTop);
        noteBody.fontSize = bodySize;
        notePaper.color = slate ? SlateFace : Paper;
        noteBody.color = slate ? SlateInk : PaperInk;
        noteSignature.color = noteBody.color;
        noteBody.text = body;
        noteSignature.text = signature;
        notePanel.SetActive(true);
        // Reading is safe: time, air and sound stop until the note is closed, like the pause menu.
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    public void CloseNote()
    {
        notePanel.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        NoteClosed?.Invoke();
    }

    // Sickly yellow-green at the rim while spores are in the lungs; 0 hides it.
    public void SetSporeEdge(float alpha)
    {
        if (sporeEdge != null)
        {
            sporeEdge.color = new Color(0.55f, 0.65f, 0.12f, Mathf.Clamp01(alpha) * 0.6f);
        }
    }

    // Red at the rim of the view while the body is out of air; 0 hides it.
    // Two sources share the red edge: the air running out (SuffocationEffects, every frame) and blows and fear
    // (Immersion); the stronger shows.
    public void SetDamageEdge(float alpha)
    {
        edgeBase = alpha;
        ApplyDamageEdge();
    }

    public void SetDamageEdgeExtra(float alpha)
    {
        edgeExtra = alpha;
        ApplyDamageEdge();
    }

    private float edgeBase;
    private float edgeExtra;

    private void ApplyDamageEdge()
    {
        if (damageEdge != null)
        {
            damageEdge.color = new Color(LowColor.r * 0.8f, 0f, 0f, Mathf.Clamp01(Mathf.Max(edgeBase, edgeExtra)));
        }
    }

    // Swim controls in the corner while the player is in deep water: how to rise and sink.
    public void SetDiveControls(bool visible)
    {
        diveControlsVisible = visible;
    }

    public bool IsEndCardShown { get; private set; }

    // The demo is over: title, one line, and two buttons (play again / quit) for the mouse.
    public void ShowEndCard(string title, string line)
    {
        endTitle.color = HudFactory.Ink;
        ShowCard(title, line, endButtons);
    }

    // Drowned: the same card in red, with "try again" (back to the checkpoint, progress kept) and
    // "start over".
    public void ShowDeathCard(string title, string line, System.Action retry)
    {
        retryAction = retry;
        endTitle.color = LowColor;
        ShowCard(title, line, deathButtons);
    }

    public void HideCard()
    {
        endCard.alpha = 0f;
        endCard.blocksRaycasts = false;
        endButtons.SetActive(false);
        deathButtons.SetActive(false);
        IsEndCardShown = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ShowCard(string title, string line, GameObject buttons)
    {
        endTitle.text = title;
        endLine.text = line;
        endCard.alpha = 1f;
        endCard.blocksRaycasts = true;
        buttons.SetActive(true);
        IsEndCardShown = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Retry()
    {
        retryAction?.Invoke();
    }

    // From the beginning of the house (the checkpoint file is overwritten at the first step of the story).
    private static void RestartDemo()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SaveGame.PendingLoad = null;
        SaveGame.PlaySeconds = 0f;
        SceneFlow.Load(SceneManager.GetActiveScene().name);
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Called every frame while a skippable sequence runs; progress is how long the key has been held.
    public void ShowSkipHint(float progress)
    {
        skipVisibleUntil = Time.time + 0.1f;
        skipFill.fillAmount = Mathf.Clamp01(progress);
    }

    public Coroutine FadeTo(float alpha, float seconds)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(Fade(alpha, seconds));
        return fadeRoutine;
    }

    public void SetFade(float alpha)
    {
        fade.color = new Color(0f, 0f, 0f, alpha);
    }

    private IEnumerator Fade(float target, float seconds)
    {
        float start = fade.color.a;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
        {
            SetFade(Mathf.Lerp(start, target, t));
            yield return null;
        }

        SetFade(target);
        fadeRoutine = null;
    }

    private void Update()
    {
        float step = Time.deltaTime * 8f;
        prompt.group.alpha = Mathf.MoveTowards(prompt.group.alpha, promptVisible ? 1f : 0f, step);
        message.group.alpha = Mathf.Clamp01((messageUntil - Time.time) / 0.6f);
        subtitle.group.alpha = Mathf.Clamp01((subtitleUntil - Time.time) / 0.4f);
        skipGroup.alpha = Mathf.MoveTowards(skipGroup.alpha, Time.time < skipVisibleUntil ? 1f : 0f, step);
        diveControls.alpha = Mathf.MoveTowards(diveControls.alpha, diveControlsVisible ? 1f : 0f, Time.deltaTime * 3f);
        objectiveGroup.alpha = Mathf.MoveTowards(objectiveGroup.alpha, string.IsNullOrEmpty(objective.text) ? 0f : 1f, Time.deltaTime * 2f);
        UpdateAir();
    }

    private void UpdateAir()
    {
        if (air == null)
        {
            return;
        }

        float value = air.Normalized;
        bool show = air.IsUnderwater || value < 0.999f || air.IsSuffocating || air.IsHurt;
        airGroup.alpha = Mathf.MoveTowards(airGroup.alpha, show ? 1f : 0f, Time.deltaTime * (show ? 4f : 1f));
        if (air.IsSuffocating)
        {
            // No air left: the bar becomes what is left of the body, draining in red.
            float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
            airFill.fillAmount = air.Health;
            airFill.color = Color.Lerp(LowColor, Color.white, beat * 0.35f);
            airLabel.text = labelNoAir;
            return;
        }

        if (air.IsHurt && !air.IsUnderwater)
        {
            // Struck by a creature: the same bar shows what is left of the body, in red, until it heals.
            airFill.fillAmount = air.Health;
            airFill.color = Color.Lerp(LowColor, Color.white, 0.15f);
            airLabel.text = labelLife;
            return;
        }

        airFill.fillAmount = value;

        Color color = air.NearHeat ? HeatColor : AirColor;
        if (value < 0.25f)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
            color = Color.Lerp(color, LowColor, 0.5f + 0.5f * pulse);
        }

        airFill.color = color;
        airLabel.text = air.NearHeat ? labelAirHeat : labelAir;
    }

    // ---------------------------------------------------------------- construction

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        // Only the end card's buttons take clicks; every other HUD graphic has raycasts off.
        gameObject.AddComponent<GraphicRaycaster>();
        Transform root = transform;

        dot = HudFactory.Image("Dot", root, HudSprites.Circle, new Color(1f, 1f, 1f, 0.35f));
        dot.rectTransform.sizeDelta = Vector2.one * 6f;

        prompt = PlateLine("Prompt", root, 26f, HudFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0f, -86f));
        promptKey = Keycap(prompt.group.transform, "E", 38f);
        promptKey.transform.parent.SetAsFirstSibling();

        BuildObjective(root);
        BuildAirGauge(root);
        BuildDiveControls(root);
        BuildSkipHint(root);

        damageEdge = HudFactory.Image("DamageEdge", root, HudSprites.EdgeGlow, Color.clear);
        HudFactory.Stretch(damageEdge.rectTransform);
        sporeEdge = HudFactory.Image("SporeEdge", root, HudSprites.EdgeGlow, Color.clear);
        HudFactory.Stretch(sporeEdge.rectTransform);

        fade = HudFactory.Image("Fade", root, HudSprites.Square, Color.black);
        HudFactory.Stretch(fade.rectTransform);

        // After the fade so the radio and wake-up lines read over black; before the note so an open
        // note is never covered by a passing message.
        message = PlateLine("Message", root, 27f, HudFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0f, -250f));
        subtitle = PlateLine("Subtitle", root, 29f, new Color(0.84f, 0.9f, 0.88f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 150f));
        subtitle.text.fontStyle = FontStyles.Italic;
        skipGroup.transform.SetAsLastSibling();
        BuildNote(root);
        BuildEndCard(root);
    }

    private void BuildDiveControls(Transform root)
    {
        Image panel = HudFactory.Image("DiveControls", root, HudSprites.RoundedPanel, Plate);
        panel.type = Image.Type.Sliced;
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(40f, 40f);
        VerticalLayoutGroup column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(18, 22, 14, 14);
        column.spacing = 8f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = false;
        column.childForceExpandHeight = false;
        ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI header = HudFactory.Label("Header", panel.transform, 16f, TextAlignmentOptions.Left, HudFactory.Accent);
        diveHeader = header;
        header.characterSpacing = 12f;
        Readable(header);
        string[,] rows = DiveRows;
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            RectTransform row = HudFactory.Rect("Row", panel.transform);
            HorizontalLayoutGroup line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 12f;
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            // Same cap width on every row so the actions line up in a column.
            diveKeys[i] = Keycap(row, rows[i, 0], 30f);
            diveKeys[i].transform.parent.GetComponent<LayoutElement>().minWidth = 92f;
            TextMeshProUGUI label = HudFactory.Label("Label", row, 21f, TextAlignmentOptions.Left, HudFactory.Ink);
            diveLabels[i] = label;
            Readable(label);
        }

        diveControls = panel.gameObject.AddComponent<CanvasGroup>();
        diveControls.alpha = 0f;
    }

    private void BuildEndCard(Transform root)
    {
        RectTransform card = HudFactory.Rect("EndCard", root);
        HudFactory.Stretch(card);
        endCard = card.gameObject.AddComponent<CanvasGroup>();
        endCard.alpha = 0f;
        endTitle = HudFactory.Label("Title", card, 72f, TextAlignmentOptions.Center, HudFactory.Ink);
        endTitle.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        endTitle.rectTransform.sizeDelta = new Vector2(1600f, 100f);
        endTitle.characterSpacing = 10f;
        endLine = HudFactory.Label("Line", card, 28f, TextAlignmentOptions.Center, HudFactory.MutedInk);
        endLine.rectTransform.anchoredPosition = new Vector2(0f, -30f);
        endLine.rectTransform.sizeDelta = new Vector2(1600f, 60f);
        endLine.textWrappingMode = TextWrappingModes.Normal;
        endCard.blocksRaycasts = false;

        RectTransform row = ButtonRow("Buttons", card);
        // The wishlist button only exists once the Steam page does (StoreLinks.SteamAppId).
        if (StoreLinks.HasSteamPage)
        {
            EndButton(row, Loc.T("end.wishlist"), StoreLinks.OpenWishlist);
        }

        EndButton(row, Loc.T("end.again"), RestartDemo);
        EndButton(row, Loc.T("pause.mainmenu"), SceneFlow.ToMainMenu);
        EndButton(row, Loc.T("end.quit"), QuitGame);
        endButtons = row.gameObject;

        RectTransform deathRow = ButtonRow("DeathButtons", card);
        EndButton(deathRow, Loc.T("end.retry"), Retry);
        EndButton(deathRow, Loc.T("end.restart"), RestartDemo);
        deathButtons = deathRow.gameObject;
    }

    private static RectTransform ButtonRow(string name, Transform card)
    {
        RectTransform row = HudFactory.Rect(name, card);
        row.anchoredPosition = new Vector2(0f, -150f);
        row.sizeDelta = new Vector2(1040f, 64f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        row.gameObject.SetActive(false);
        return row;
    }

    private static readonly Color EndButtonIdle = new Color(0.07f, 0.08f, 0.08f, 0.9f);
    private static readonly Color EndButtonHover = new Color(0.22f, 0.19f, 0.14f, 0.95f);

    private static void EndButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        Image plate = HudFactory.Image(label, parent, HudSprites.RoundedPanel, EndButtonIdle);
        plate.type = Image.Type.Sliced;
        plate.raycastTarget = true;
        Button button = plate.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = EndButtonIdle;
        colors.highlightedColor = EndButtonHover;
        colors.selectedColor = EndButtonHover;
        colors.pressedColor = EndButtonHover;
        colors.colorMultiplier = 1f;
        plate.color = Color.white;
        button.colors = colors;
        button.targetGraphic = plate;
        button.onClick.AddListener(action);
        TextMeshProUGUI text = HudFactory.Label("Label", plate.transform, 28f, TextAlignmentOptions.Center, HudFactory.Ink);
        HudFactory.Stretch(text.rectTransform);
        text.text = label;
        Readable(text);
    }

    private Line PlateLine(string name, Transform parent, float size, Color color, Vector2 anchor, Vector2 position)
    {
        Image plate = HudFactory.Image(name, parent, HudSprites.RoundedPanel, Plate);
        plate.type = Image.Type.Sliced;
        RectTransform rect = plate.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        HorizontalLayoutGroup layout = plate.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 10, 10);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = plate.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI label = HudFactory.Label("Text", plate.transform, size, TextAlignmentOptions.Center, color);
        Readable(label);
        label.textWrappingMode = TextWrappingModes.Normal;
        LayoutElement width = label.gameObject.AddComponent<LayoutElement>();
        CanvasGroup group = plate.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        return new Line { group = group, text = label, width = width };
    }

    public static TextMeshProUGUI Keycap(Transform parent, string key, float height)
    {
        Image cap = HudFactory.Image("Keycap", parent, HudSprites.RoundedPanel, KeycapFace);
        cap.type = Image.Type.Sliced;
        HorizontalLayoutGroup layout = cap.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(11, 11, 2, 2);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        // Force-expand (the default) makes the cap flexible, so it swallowed the spare width of its row.
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        LayoutElement element = cap.gameObject.AddComponent<LayoutElement>();
        element.flexibleWidth = 0f;
        element.minWidth = height;
        element.minHeight = height;
        element.preferredHeight = height;
        TextMeshProUGUI glyph = HudFactory.Label("Key", cap.transform, height * 0.62f, TextAlignmentOptions.Center, KeycapInk);
        glyph.fontStyle = FontStyles.Bold;
        glyph.text = key;
        return glyph;
    }

    // Soft dark halo behind the glyphs (TMP SDF underlay): readable on snow, on black, on fire.
    public static void Readable(TMP_Text label)
    {
        Material material = label.fontMaterial;
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.85f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.3f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.3f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.6f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.55f);
    }

    private void BuildObjective(Transform root)
    {
        RectTransform box = HudFactory.Rect("Objective", root);
        box.anchorMin = box.anchorMax = new Vector2(0f, 1f);
        box.pivot = new Vector2(0f, 1f);
        box.anchoredPosition = new Vector2(48f, -36f);
        box.sizeDelta = new Vector2(900f, 80f);
        objectiveGroup = box.gameObject.AddComponent<CanvasGroup>();

        TextMeshProUGUI header = HudFactory.Label("Header", box, 17f, TextAlignmentOptions.TopLeft, HudFactory.Accent);
        objectiveHeader = header;
        header.characterSpacing = 12f;
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(1f, 1f);
        header.rectTransform.pivot = new Vector2(0f, 1f);
        header.rectTransform.sizeDelta = new Vector2(0f, 24f);
        Readable(header);

        objective = HudFactory.Label("Text", box, 25f, TextAlignmentOptions.TopLeft, HudFactory.Ink);
        objective.rectTransform.anchorMin = new Vector2(0f, 1f);
        objective.rectTransform.anchorMax = new Vector2(1f, 1f);
        objective.rectTransform.pivot = new Vector2(0f, 1f);
        objective.rectTransform.anchoredPosition = new Vector2(0f, -26f);
        objective.rectTransform.sizeDelta = new Vector2(0f, 40f);
        Readable(objective);
    }

    private void BuildAirGauge(Transform root)
    {
        RectTransform gauge = HudFactory.Rect("Air", root);
        gauge.anchorMin = gauge.anchorMax = new Vector2(0.5f, 0f);
        gauge.anchoredPosition = new Vector2(0f, 90f);
        gauge.sizeDelta = new Vector2(420f, 40f);
        airGroup = gauge.gameObject.AddComponent<CanvasGroup>();
        airGroup.alpha = 0f;

        Image back = HudFactory.Image("Back", gauge, HudSprites.Square, new Color(0f, 0f, 0f, 0.55f));
        back.rectTransform.anchorMin = new Vector2(0f, 0f);
        back.rectTransform.anchorMax = new Vector2(1f, 0f);
        back.rectTransform.sizeDelta = new Vector2(0f, 8f);
        back.rectTransform.anchoredPosition = new Vector2(0f, 4f);

        airFill = HudFactory.Image("Fill", back.transform, HudSprites.Square, AirColor);
        HudFactory.Stretch(airFill.rectTransform);
        airFill.type = Image.Type.Filled;
        airFill.fillMethod = Image.FillMethod.Horizontal;
        airFill.fillOrigin = (int)Image.OriginHorizontal.Left;

        airLabel = HudFactory.Label("Label", gauge, 20f, TextAlignmentOptions.Bottom, HudFactory.Ink);
        airLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        airLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        airLabel.rectTransform.offsetMin = new Vector2(0f, 16f);
        airLabel.rectTransform.offsetMax = Vector2.zero;
        Readable(airLabel);
    }

    // "Hold Space to skip": holding (not tapping) so nobody skips the opening by accident.
    private void BuildSkipHint(Transform root)
    {
        Line line = PlateLine("SkipHint", root, 21f, HudFactory.MutedInk, new Vector2(1f, 0f), new Vector2(-230f, 70f));
        skipLabel = line.text;
        TextMeshProUGUI cap = Keycap(line.group.transform, "", 32f);
        skipKey = cap;
        cap.transform.parent.SetAsFirstSibling();
        skipGroup = line.group;

        skipFill = HudFactory.Image("Progress", line.group.transform, HudSprites.Square, HudFactory.Accent);
        LayoutElement ignore = skipFill.gameObject.AddComponent<LayoutElement>();
        ignore.ignoreLayout = true;
        skipFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        skipFill.rectTransform.anchorMax = new Vector2(1f, 0f);
        skipFill.rectTransform.offsetMin = new Vector2(14f, 3f);
        skipFill.rectTransform.offsetMax = new Vector2(-14f, 6f);
        skipFill.type = Image.Type.Filled;
        skipFill.fillMethod = Image.FillMethod.Horizontal;
        skipFill.fillAmount = 0f;
    }

    private void BuildNote(Transform root)
    {
        Image dim = HudFactory.Image("Note", root, HudSprites.Square, new Color(0f, 0f, 0f, 0.72f));
        HudFactory.Stretch(dim.rectTransform);
        notePanel = dim.gameObject;

        Image paper = HudFactory.Image("Paper", dim.transform, HudSprites.Square, Paper);
        notePaper = paper;
        paper.rectTransform.sizeDelta = new Vector2(760f, 760f);
        paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -1.2f);

        noteBody = HudFactory.Label("Body", paper.transform, 30f, TextAlignmentOptions.TopLeft, PaperInk);
        noteBody.textWrappingMode = TextWrappingModes.Normal;
        noteBody.lineSpacing = 12f;
        HudFactory.Stretch(noteBody.rectTransform);
        noteBody.rectTransform.offsetMin = new Vector2(64f, 110f);
        noteBody.rectTransform.offsetMax = new Vector2(-64f, -64f);

        notePhoto = new GameObject("Photo", typeof(RectTransform)).AddComponent<RawImage>();
        notePhoto.transform.SetParent(paper.transform, false);
        notePhoto.raycastTarget = false;
        RectTransform photoRect = notePhoto.rectTransform;
        photoRect.anchorMin = photoRect.anchorMax = new Vector2(0.5f, 1f);
        photoRect.pivot = new Vector2(0.5f, 1f);
        photoRect.anchoredPosition = new Vector2(0f, -56f);
        photoRect.sizeDelta = new Vector2(480f, 320f);
        photoRect.localRotation = Quaternion.Euler(0f, 0f, 1.6f);
        notePhoto.gameObject.SetActive(false);

        noteSignature = HudFactory.Label("Signature", paper.transform, 28f, TextAlignmentOptions.BottomRight, PaperInk);
        HudFactory.Stretch(noteSignature.rectTransform);
        noteSignature.rectTransform.offsetMin = new Vector2(64f, 56f);
        noteSignature.rectTransform.offsetMax = new Vector2(-64f, -64f);

        Line close = PlateLine("Close", dim.transform, 22f, HudFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0f, -440f));
        closeLabel = close.text;
        Keycap(close.group.transform, "E", 32f).transform.parent.SetAsFirstSibling();
        close.group.alpha = 1f;

        notePanel.SetActive(false);
    }
}
