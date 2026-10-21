using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The mansion's own HUD on top of DiveHud, built at runtime the same way (plates, keycaps, underlay):
//  - the mask filter gauge with the spare count (only once the mask is on)
//  - the keypad of the coded doors: digits by keyboard or mouse, Enter, Backspace, Esc, with the hint
//  - the house map (M, once found), with a dot where the player is
//  - the diary (Tab): everything read or heard, to read or play again
// Keypad, map and diary stop the world while open, like the notes.
public sealed class MansionHud : MonoBehaviour
{
    [SerializeField]
    private TMP_FontAsset font;

    [SerializeField]
    private FirstPersonController player;

    [SerializeField]
    private DiveHud hud;

    [SerializeField]
    private Respirator respirator;

    [SerializeField]
    private PlayerItems items;

    [SerializeField]
    private AudioSource uiAudio;

    [System.Serializable]
    public struct MapRoom
    {
        public string name;
        [Tooltip("World rectangle: x, z, width (x), depth (z).")]
        public Rect area;
        public bool closed;
        [Tooltip("0 = ground floor, 1 = upper floor: each floor is drawn as its own plan.")]
        public int floor;
    }

    [Tooltip("The rooms drawn on the house map, from the builder's own layout.")]
    [SerializeField]
    private MapRoom[] rooms = new MapRoom[0];

    [Tooltip("World rectangle (x, z, width, depth) the map covers.")]
    [SerializeField]
    private Rect mapWorld = new Rect(-7f, -6f, 26f, 18f);

    [Tooltip("The title over each floor's plan, by floor number (Loc keys, or plain text in an older scene).")]
    [SerializeField]
    private string[] floorNames = { "map.floor0" };

    [Tooltip("Feet above this height (world) are on the upper floor: the dot moves to that plan.")]
    [SerializeField]
    private float upperFloorY = 3f;

    [SerializeField]
    private string mapItem = "mapa";

    private static readonly Color Plate = new Color(0.02f, 0.03f, 0.03f, 0.86f);
    private static readonly Color FilterColor = new Color(0.78f, 0.86f, 0.55f, 1f);
    private static readonly Color LowColor = new Color(0.95f, 0.25f, 0.2f, 1f);
    private static readonly Color ButtonIdle = new Color(0.1f, 0.11f, 0.11f, 0.95f);
    private static readonly Color ButtonHover = new Color(0.26f, 0.22f, 0.16f, 1f);

    public static MansionHud Instance { get; private set; }
    public static bool PanelOpen { get; private set; }

    // The frame a panel closed on Esc, so the pause menu does not open on the same key press.
    public static int ClosedFrame { get; private set; } = -1;

    private CanvasGroup filterGroup;
    private Image filterFill;
    private TextMeshProUGUI filterLabel;
    private GameObject filterKey;

    private GameObject keypad;
    private TextMeshProUGUI keypadHint;
    private TextMeshProUGUI keypadDisplay;
    private string entered = string.Empty;
    private string expected;
    private System.Action onCorrect;
    private float wrongUntil;
    private bool showingWrong;

    private GameObject map;
    private RectTransform mapImage;
    private RectTransform mapDot;
    private RectTransform[] mapPlans;

    private GameObject diary;
    private RectTransform diaryList;
    private TextMeshProUGUI diaryEmpty;

    private AudioSource replayAudio;

    // The fixed labels of the panels, re-resolved when the language changes (the panels are built once).
    private readonly List<(TextMeshProUGUI label, string key, bool upper)> fixedLabels = new List<(TextMeshProUGUI, string, bool)>();
    private int filterSpareShown = -1;
    private int filterLanguageShown = -1;

    private void Awake()
    {
        Instance = this;
        HudFactory.Font = font;
        Build();
        ApplyLanguage();
        replayAudio = gameObject.AddComponent<AudioSource>();
        replayAudio.spatialBlend = 0f;
        replayAudio.playOnAwake = false;
    }

    private void OnEnable()
    {
        Loc.Changed += ApplyLanguage;
    }

    private void OnDisable()
    {
        Loc.Changed -= ApplyLanguage;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            PanelOpen = false;
        }
    }

    private void ApplyLanguage()
    {
        foreach ((TextMeshProUGUI label, string key, bool upper) in fixedLabels)
        {
            string value = Loc.Resolve(key);
            label.text = upper ? value.ToUpperInvariant() : value;
        }

        filterSpareShown = -1;
    }

    private TextMeshProUGUI Fixed(TextMeshProUGUI label, string key, bool upper = false)
    {
        fixedLabels.Add((label, key, upper));
        return label;
    }

    // ---------------------------------------------------------------- keypad

    public void OpenKeypad(string hint, string code, System.Action correct)
    {
        expected = code;
        onCorrect = correct;
        entered = string.Empty;
        keypadHint.text = string.IsNullOrEmpty(hint) ? string.Empty : Loc.F("hud.keypad.hint", hint);
        RefreshDisplay();
        OpenPanel(keypad);
    }

    private void Press(string key)
    {
        if (key == "<")
        {
            if (entered.Length > 0)
            {
                entered = entered.Substring(0, entered.Length - 1);
            }
        }
        else if (key == "OK")
        {
            Submit();
            return;
        }
        else if (entered.Length < expected.Length)
        {
            entered += key;
        }

        Beep(1100f);
        RefreshDisplay();
        if (entered.Length == expected.Length)
        {
            Submit();
        }
    }

    private void Submit()
    {
        if (entered == expected)
        {
            Beep(1760f);
            keypadDisplay.color = new Color(0.5f, 1f, 0.6f);
            keypadDisplay.text = Loc.T("hud.keypad.granted");
            System.Action done = onCorrect;
            onCorrect = null;
            ClosePanel();
            done?.Invoke();
            return;
        }

        if (uiAudio != null)
        {
            uiAudio.PlayOneShot(ProceduralAudio.Buzz(), 0.5f);
        }

        wrongUntil = Time.unscaledTime + 1f;
        entered = string.Empty;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (Time.unscaledTime < wrongUntil)
        {
            keypadDisplay.color = LowColor;
            keypadDisplay.text = Loc.T("hud.keypad.wrong");
            showingWrong = true;
            return;
        }

        showingWrong = false;
        keypadDisplay.color = new Color(1f, 0.55f, 0.3f);
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        for (int i = 0; i < expected.Length; i++)
        {
            text.Append(i < entered.Length ? entered[i] : '_').Append(' ');
        }

        keypadDisplay.text = text.ToString().TrimEnd();
    }

    private void Beep(float frequency)
    {
        if (uiAudio != null)
        {
            uiAudio.PlayOneShot(ProceduralAudio.Beep(frequency), 0.3f);
        }
    }

    // ---------------------------------------------------------------- panels

    private void OpenPanel(GameObject panel)
    {
        keypad.SetActive(panel == keypad);
        map.SetActive(panel == map);
        diary.SetActive(panel == diary);
        PanelOpen = true;
        player.InputLocked = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ClosePanel()
    {
        ClosedFrame = Time.frameCount;
        keypad.SetActive(false);
        map.SetActive(false);
        diary.SetActive(false);
        PanelOpen = false;
        player.InputLocked = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        UpdateFilter();
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keypad.activeSelf)
        {
            for (int d = 0; d <= 9; d++)
            {
                if (keyboard[(Key)((int)Key.Digit0 + d)].wasPressedThisFrame || keyboard[(Key)((int)Key.Numpad0 + d)].wasPressedThisFrame)
                {
                    Press(d.ToString());
                }
            }

            if (keyboard.backspaceKey.wasPressedThisFrame)
            {
                Press("<");
            }
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                Submit();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                ClosePanel();
            }

            if (Time.unscaledTime > wrongUntil && showingWrong)
            {
                RefreshDisplay();
            }

            return;
        }

        bool busy = hud.IsNoteOpen || hud.IsEndCardShown;
        if (map.activeSelf)
        {
            UpdateMapDot();
            if (keyboard.mKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                ClosePanel();
            }

            return;
        }

        if (diary.activeSelf)
        {
            if (keyboard.tabKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                ClosePanel();
            }

            return;
        }

        if (busy || player.InputLocked)
        {
            return;
        }

        if (keyboard.mKey.wasPressedThisFrame)
        {
            if (items.HasItem(mapItem))
            {
                OpenPanel(map);
                UpdateMapDot();
            }
            else
            {
                hud.ShowMessage(Loc.T("hud.map.none"), 2.5f);
            }
        }
        else if (keyboard.tabKey.wasPressedThisFrame)
        {
            FillDiary();
            OpenPanel(diary);
        }
    }

    private void UpdateFilter()
    {
        bool show = respirator != null && respirator.HasMask && !hud.IsEndCardShown;
        filterGroup.alpha = Mathf.MoveTowards(filterGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        if (!show)
        {
            return;
        }

        float value = respirator.Filter;
        filterFill.fillAmount = value;
        Color color = FilterColor;
        if (value < 0.25f)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
            color = Color.Lerp(color, LowColor, 0.5f + 0.5f * pulse);
        }

        filterFill.color = color;
        // Rebuilt only when the count or the language changes (no string per frame).
        if (filterSpareShown != respirator.SpareFilters || filterLanguageShown != Loc.Language)
        {
            filterSpareShown = respirator.SpareFilters;
            filterLanguageShown = Loc.Language;
            filterLabel.text = respirator.SpareFilters > 0 ? Loc.F("hud.filter.spare", respirator.SpareFilters) : Loc.T("hud.filter.none");
            filterKey.SetActive(respirator.SpareFilters > 0);
        }
    }

    private void UpdateMapDot()
    {
        if (mapDot == null)
        {
            return;
        }

        Vector3 p = player.transform.position;
        // The dot sits on the plan of the floor the player is on.
        int floor = mapPlans != null && mapPlans.Length > 1 && p.y > upperFloorY ? 1 : 0;
        if (mapPlans != null && mapDot.parent != mapPlans[floor])
        {
            mapDot.SetParent(mapPlans[floor], false);
            mapImage = mapPlans[floor];
        }

        Vector2 uv = new Vector2((p.x - mapWorld.x) / mapWorld.width, (p.z - mapWorld.y) / mapWorld.height);
        Vector2 size = mapImage.rect.size;
        mapDot.anchoredPosition = new Vector2((uv.x - 0.5f) * size.x, (uv.y - 0.5f) * size.y);
        mapDot.GetComponent<Image>().color = new Color(1f, 0.35f, 0.2f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f));
    }

    private void FillDiary()
    {
        for (int i = diaryList.childCount - 1; i >= 0; i--)
        {
            Destroy(diaryList.GetChild(i).gameObject);
        }

        diaryEmpty.gameObject.SetActive(items.Archive.Count == 0);
        foreach (PlayerItems.Entry entry in items.Archive)
        {
            string kind = Loc.T(entry.kind == PlayerItems.EntryKind.Recording ? "hud.diary.recording" : entry.kind == PlayerItems.EntryKind.Clipping ? "hud.diary.clipping"
                : entry.kind == PlayerItems.EntryKind.Photo ? "hud.diary.photo" : entry.kind == PlayerItems.EntryKind.Notice ? "hud.diary.notice" : "hud.diary.note");
            PlayerItems.Entry captured = entry;
            // Titles are Loc keys (or plain text in an older scene).
            MenuButton(diaryList, kind + "   " + Loc.Resolve(entry.title), () => OpenEntry(captured));
        }
    }

    private void OpenEntry(PlayerItems.Entry entry)
    {
        ClosePanel();
        if (entry.kind == PlayerItems.EntryKind.Recording && entry.recorder != null)
        {
            entry.recorder.Replay(hud, replayAudio);
            return;
        }

        hud.ShowNote(Loc.Resolve(entry.body), Loc.Resolve(entry.signature), false, entry.photo);
        player.InputLocked = true;
    }

    // ---------------------------------------------------------------- construction

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 22;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        BuildFilterGauge();
        keypad = BuildKeypad();
        map = BuildMap();
        diary = BuildDiary();
        keypad.SetActive(false);
        map.SetActive(false);
        diary.SetActive(false);
    }

    private void BuildFilterGauge()
    {
        RectTransform gauge = HudFactory.Rect("Filter", transform);
        // Bottom right, clear of the air gauge, the subtitles and the dive controls.
        gauge.anchorMin = gauge.anchorMax = new Vector2(1f, 0f);
        gauge.pivot = new Vector2(1f, 0f);
        gauge.anchoredPosition = new Vector2(-60f, 50f);
        gauge.sizeDelta = new Vector2(380f, 40f);
        filterGroup = gauge.gameObject.AddComponent<CanvasGroup>();
        filterGroup.alpha = 0f;
        filterGroup.blocksRaycasts = false;

        Image back = HudFactory.Image("Back", gauge, HudSprites.Square, new Color(0f, 0f, 0f, 0.55f));
        back.rectTransform.anchorMin = new Vector2(0f, 0f);
        back.rectTransform.anchorMax = new Vector2(1f, 0f);
        back.rectTransform.sizeDelta = new Vector2(0f, 8f);
        back.rectTransform.anchoredPosition = new Vector2(0f, 4f);
        filterFill = HudFactory.Image("Fill", back.transform, HudSprites.Square, FilterColor);
        HudFactory.Stretch(filterFill.rectTransform);
        filterFill.type = Image.Type.Filled;
        filterFill.fillMethod = Image.FillMethod.Horizontal;

        filterLabel = HudFactory.Label("Label", gauge, 20f, TextAlignmentOptions.BottomRight, HudFactory.Ink);
        filterLabel.rectTransform.anchorMin = Vector2.zero;
        filterLabel.rectTransform.anchorMax = Vector2.one;
        filterLabel.rectTransform.offsetMin = new Vector2(-200f, 16f);
        filterLabel.rectTransform.offsetMax = new Vector2(0f, 0f);
        Readable(filterLabel);
        // The swap key as a keycap, to the right of "trocar" (keys are never written as "[R]").
        Image cap = HudFactory.Image("Keycap", gauge, HudSprites.RoundedPanel, new Color(0.93f, 0.9f, 0.82f, 1f));
        cap.type = Image.Type.Sliced;
        cap.rectTransform.anchorMin = cap.rectTransform.anchorMax = new Vector2(1f, 0f);
        cap.rectTransform.pivot = new Vector2(0f, 0f);
        cap.rectTransform.anchoredPosition = new Vector2(10f, 14f);
        cap.rectTransform.sizeDelta = new Vector2(30f, 30f);
        TextMeshProUGUI glyph = HudFactory.Label("Key", cap.transform, 19f, TextAlignmentOptions.Center, new Color(0.08f, 0.07f, 0.06f, 1f));
        HudFactory.Stretch(glyph.rectTransform);
        glyph.fontStyle = FontStyles.Bold;
        glyph.text = "G";
        filterKey = cap.gameObject;
    }

    private GameObject BuildKeypad()
    {
        Image dim = Backdrop("Keypad");
        Image panel = Panel(dim.transform, new Vector2(520f, 640f));
        TextMeshProUGUI title = Fixed(HudFactory.Label("Title", panel.transform, 26f, TextAlignmentOptions.Center, HudFactory.Accent), "hud.keypad.title", true);
        Place(title.rectTransform, new Vector2(0f, 270f), new Vector2(460f, 40f));
        title.characterSpacing = 8f;
        keypadHint = HudFactory.Label("Hint", panel.transform, 24f, TextAlignmentOptions.Center, HudFactory.Ink);
        Place(keypadHint.rectTransform, new Vector2(0f, 225f), new Vector2(460f, 40f));
        Image screen = HudFactory.Image("Screen", panel.transform, HudSprites.RoundedPanel, new Color(0.05f, 0.02f, 0.02f, 1f));
        screen.type = Image.Type.Sliced;
        Place(screen.rectTransform, new Vector2(0f, 160f), new Vector2(380f, 74f));
        keypadDisplay = HudFactory.Label("Display", screen.transform, 40f, TextAlignmentOptions.Center, new Color(1f, 0.55f, 0.3f));
        HudFactory.Stretch(keypadDisplay.rectTransform);

        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "<", "0", "OK" };
        for (int i = 0; i < keys.Length; i++)
        {
            int column = i % 3, row = i / 3;
            string key = keys[i];
            RectTransform button = MenuButton(panel.transform, key, () => Press(key));
            if (key == "<")
            {
                Fixed(button.GetComponentInChildren<TextMeshProUGUI>(), "hud.keypad.delete");
            }

            Place(button, new Vector2((column - 1) * 130f, 60f - row * 84f), new Vector2(116f, 70f));
        }

        TextMeshProUGUI help = Fixed(HudFactory.Label("Help", panel.transform, 19f, TextAlignmentOptions.Center, HudFactory.MutedInk), "hud.keypad.help");
        Place(help.rectTransform, new Vector2(0f, -290f), new Vector2(480f, 30f));
        return dim.gameObject;
    }

    private GameObject BuildMap()
    {
        Image dim = Backdrop("Map");
        Image panel = Panel(dim.transform, new Vector2(1240f, 900f));
        TextMeshProUGUI title = Fixed(HudFactory.Label("Title", panel.transform, 30f, TextAlignmentOptions.Center, HudFactory.Accent), "hud.map.title", true);
        Place(title.rectTransform, new Vector2(0f, 405f), new Vector2(1100f, 44f));
        title.characterSpacing = 6f;
        // A plan in ink on old paper per floor, side by side: every room a pale block with a dark outline and its
        // name; the rooms closed in the demo hatched darker.
        int floors = 1;
        foreach (MapRoom room in rooms)
        {
            floors = Mathf.Max(floors, room.floor + 1);
        }

        mapPlans = new RectTransform[floors];
        float aspect = mapWorld.width / mapWorld.height;
        float gap = 40f;
        float width = Mathf.Min((1160f - gap * (floors - 1)) / floors, 720f * aspect);
        Color ink = new Color(0.16f, 0.13f, 0.1f, 1f);
        for (int f = 0; f < floors; f++)
        {
            Image paper = HudFactory.Image("Plan" + f, panel.transform, HudSprites.Square, new Color(0.8f, 0.76f, 0.64f, 1f));
            mapPlans[f] = paper.rectTransform;
            float x = (f - (floors - 1) * 0.5f) * (width + gap);
            Place(paper.rectTransform, new Vector2(x, -30f), new Vector2(width, width / aspect));
            TextMeshProUGUI floorName = HudFactory.Label("Floor", panel.transform, 22f, TextAlignmentOptions.Center, HudFactory.MutedInk);
            Place(floorName.rectTransform, new Vector2(x, -30f + width / aspect * 0.5f + 22f), new Vector2(width, 30f));
            if (f < floorNames.Length)
            {
                Fixed(floorName, floorNames[f], true);
            }
            else
            {
                floorName.text = Loc.F("hud.map.floor", f).ToUpperInvariant();
            }

            floorName.characterSpacing = 6f;
        }

        mapImage = mapPlans[0];
        foreach (MapRoom room in rooms)
        {
            RectTransform plan = mapPlans[Mathf.Clamp(room.floor, 0, floors - 1)];
            Vector2 min = new Vector2((room.area.xMin - mapWorld.x) / mapWorld.width, (room.area.yMin - mapWorld.y) / mapWorld.height);
            Vector2 max = new Vector2((room.area.xMax - mapWorld.x) / mapWorld.width, (room.area.yMax - mapWorld.y) / mapWorld.height);
            Image block = HudFactory.Image(room.name, plan, HudSprites.Square, room.closed ? new Color(0.45f, 0.4f, 0.33f, 1f) : new Color(0.9f, 0.86f, 0.74f, 1f));
            block.rectTransform.anchorMin = min;
            block.rectTransform.anchorMax = max;
            block.rectTransform.offsetMin = Vector2.zero;
            block.rectTransform.offsetMax = Vector2.zero;
            Outline outline = block.gameObject.AddComponent<Outline>();
            outline.effectColor = ink;
            outline.effectDistance = new Vector2(2f, -2f);
            TextMeshProUGUI label = HudFactory.Label("Name", block.transform, room.closed ? 16f : 14f, TextAlignmentOptions.Center, ink);
            HudFactory.Stretch(label.rectTransform);
            label.textWrappingMode = TextWrappingModes.Normal;
            // Small rooms get a smaller name rather than a word broken in half.
            label.enableAutoSizing = true;
            label.fontSizeMin = 7f;
            label.fontSizeMax = room.closed ? 16f : 14f;
            label.margin = new Vector4(3f, 1f, 3f, 1f);
            // Room names are Loc keys (map.*), drawn in upper case.
            Fixed(label, room.name, true);
        }

        Image dot = HudFactory.Image("You", mapImage, HudSprites.Circle, new Color(1f, 0.35f, 0.2f));
        dot.rectTransform.sizeDelta = Vector2.one * 22f;
        mapDot = dot.rectTransform;
        TextMeshProUGUI help = Fixed(HudFactory.Label("Help", panel.transform, 20f, TextAlignmentOptions.Center, HudFactory.MutedInk), "hud.map.help");
        Place(help.rectTransform, new Vector2(0f, -415f), new Vector2(1100f, 30f));
        return dim.gameObject;
    }

    private GameObject BuildDiary()
    {
        Image dim = Backdrop("Diary");
        Image panel = Panel(dim.transform, new Vector2(900f, 760f));
        TextMeshProUGUI title = Fixed(HudFactory.Label("Title", panel.transform, 30f, TextAlignmentOptions.Center, HudFactory.Accent), "hud.diary.title", true);
        Place(title.rectTransform, new Vector2(0f, 330f), new Vector2(820f, 44f));
        title.characterSpacing = 10f;
        diaryList = HudFactory.Rect("List", panel.transform);
        Place(diaryList, new Vector2(0f, -20f), new Vector2(800f, 600f));
        VerticalLayoutGroup layout = diaryList.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        diaryEmpty = Fixed(HudFactory.Label("Empty", panel.transform, 24f, TextAlignmentOptions.Center, HudFactory.MutedInk), "hud.diary.empty");
        Place(diaryEmpty.rectTransform, Vector2.zero, new Vector2(800f, 40f));
        TextMeshProUGUI help = Fixed(HudFactory.Label("Help", panel.transform, 20f, TextAlignmentOptions.Center, HudFactory.MutedInk), "hud.diary.help");
        Place(help.rectTransform, new Vector2(0f, -350f), new Vector2(820f, 30f));
        return dim.gameObject;
    }

    private Image Backdrop(string name)
    {
        Image dim = HudFactory.Image(name, transform, HudSprites.Square, new Color(0f, 0f, 0f, 0.7f));
        HudFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        return dim;
    }

    private static Image Panel(Transform parent, Vector2 size)
    {
        Image panel = HudFactory.Image("Panel", parent, HudSprites.RoundedPanel, Plate);
        panel.type = Image.Type.Sliced;
        panel.rectTransform.sizeDelta = size;
        return panel;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static RectTransform MenuButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        Image plate = HudFactory.Image(label, parent, HudSprites.RoundedPanel, Color.white);
        plate.type = Image.Type.Sliced;
        plate.raycastTarget = true;
        plate.rectTransform.sizeDelta = new Vector2(780f, 56f);
        Button button = plate.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonIdle;
        colors.highlightedColor = ButtonHover;
        colors.selectedColor = ButtonIdle;
        colors.pressedColor = ButtonHover;
        button.colors = colors;
        button.targetGraphic = plate;
        button.onClick.AddListener(action);
        TextMeshProUGUI text = HudFactory.Label("Label", plate.transform, 26f, TextAlignmentOptions.Center, HudFactory.Ink);
        HudFactory.Stretch(text.rectTransform);
        text.text = label;
        Readable(text);
        return plate.rectTransform;
    }

    private static void Readable(TMP_Text label)
    {
        Material material = label.fontMaterial;
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.85f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.3f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.3f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.6f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.55f);
    }
}
