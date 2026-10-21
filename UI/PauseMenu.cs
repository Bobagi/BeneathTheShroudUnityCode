using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Esc pauses the game (time, audio and input stop) and opens a menu built at runtime like the HUD:
// Continuar, Controles, Som, Gráficos, Idioma, Menu principal and Sair. The options are SettingsCatalog's (the same
// list as the main menu's), as left/right selectors instead of dropdowns: readable, quick with a mouse, and ready
// for a gamepad later. Everything is saved by GameSettings; texts come from Loc in the chosen language.
public sealed class PauseMenu : MonoBehaviour
{
    [SerializeField]
    private FirstPersonController player;

    [SerializeField]
    private DiveHud hud;

    [SerializeField]
    private TMP_FontAsset font;

    private static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.78f);
    private static readonly Color ButtonIdle = new Color(0.07f, 0.08f, 0.08f, 0.88f);
    private static readonly Color ButtonHover = new Color(0.22f, 0.19f, 0.14f, 0.95f);

    private GameObject root;
    private RectTransform content;
    private TextMeshProUGUI title;
    private readonly List<System.Action> refreshers = new List<System.Action>();
    private bool paused;
    private bool wasLocked;

    public bool IsPaused => paused;

    private void Awake()
    {
        HudFactory.Font = font;
        Build();
        root.SetActive(false);
    }

    private void OnEnable()
    {
        Loc.Changed += OnLanguage;
    }

    private void OnDisable()
    {
        Loc.Changed -= OnLanguage;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || (hud != null && (hud.IsNoteOpen || hud.IsEndCardShown)) || MansionHud.PanelOpen || MansionHud.ClosedFrame == Time.frameCount)
        {
            return;
        }

        if (paused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    // The Steam overlay opened (Shift+Tab): pause like Esc would, where Esc would (not over a note, the end card or
    // the keypad, not when already paused).
    public void PauseFromOverlay()
    {
        if (!paused && root != null && !(hud != null && (hud.IsNoteOpen || hud.IsEndCardShown)) && !MansionHud.PanelOpen)
        {
            Pause();
        }
    }

    public void Pause()
    {
        paused = true;
        wasLocked = player.InputLocked;
        player.InputLocked = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        root.SetActive(true);
        ShowMain();
    }

    public void Resume()
    {
        paused = false;
        player.InputLocked = wasLocked;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        root.SetActive(false);
    }

    // ---------------------------------------------------------------- pages

    private System.Action current;

    private void ShowMain()
    {
        Page("pause.title", ShowMain);
        AddButton("pause.resume", Resume);
        AddButton("menu.controls", ShowControls);
        AddButton("menu.sound", ShowSound);
        AddButton("menu.graphics", ShowGraphics);
        AddButton("menu.language", ShowLanguage);
        AddButton("pause.mainmenu", ToMainMenu);
        AddButton("menu.quit", Quit);
    }

    private void ShowControls()
    {
        Page("menu.controls", ShowControls);
        foreach ((string keys, string actionKey) in SettingsCatalog.Bindings)
        {
            AddBinding(keys, Loc.T(actionKey));
        }

        AddSettings(SettingsCatalog.Page.Controls);
        AddButton("menu.back", ShowMain);
    }

    private void ShowSound()
    {
        Page("menu.sound", ShowSound);
        AddSettings(SettingsCatalog.Page.Sound);
        AddButton("menu.back", ShowMain);
    }

    private void ShowGraphics()
    {
        Page("menu.graphics", ShowGraphics);
        AddSettings(SettingsCatalog.Page.Graphics);
        AddButton("menu.back", ShowMain);
    }

    private void ShowLanguage()
    {
        Page("menu.language", ShowLanguage);
        AddSettings(SettingsCatalog.Page.Language);
        AddButton("menu.back", ShowMain);
    }

    private void AddSettings(SettingsCatalog.Page page)
    {
        foreach (SettingsCatalog.Setting setting in SettingsCatalog.On(page))
        {
            AddSelector(Loc.T(setting.labelKey), setting.value, setting.step);
        }
    }

    // A new language redraws the page in it.
    private void OnLanguage()
    {
        if (paused)
        {
            current?.Invoke();
        }
    }

    private void ToMainMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneFlow.ToMainMenu();
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------- widgets

    private void Page(string headingKey, System.Action redraw)
    {
        current = redraw;
        refreshers.Clear();
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        title.text = Loc.T(headingKey).ToUpperInvariant();
    }

    private void AddButton(string labelKey, System.Action action)
    {
        string label = Loc.T(labelKey);
        Button button = Plate(content, 60f).gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        button.colors = colors;
        Image image = button.GetComponent<Image>();
        button.targetGraphic = image;
        AddHover(button.gameObject, image);
        TextMeshProUGUI text = HudFactory.Label("Label", button.transform, 28f, TextAlignmentOptions.Left, HudFactory.Ink);
        HudFactory.Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(28f, 0f);
        text.text = label;
        button.onClick.AddListener(() => action());
    }

    private void AddSelector(string label, System.Func<string> value, System.Action<int> change)
    {
        Image row = Plate(content, 60f);
        TextMeshProUGUI name = HudFactory.Label("Label", row.transform, 26f, TextAlignmentOptions.Left, HudFactory.Ink);
        HudFactory.Stretch(name.rectTransform);
        name.rectTransform.offsetMin = new Vector2(28f, 0f);
        name.text = label;

        TextMeshProUGUI shown = HudFactory.Label("Value", row.transform, 26f, TextAlignmentOptions.Center, HudFactory.Accent);
        shown.rectTransform.anchorMin = new Vector2(1f, 0f);
        shown.rectTransform.anchorMax = new Vector2(1f, 1f);
        shown.rectTransform.pivot = new Vector2(1f, 0.5f);
        shown.rectTransform.sizeDelta = new Vector2(220f, 0f);
        shown.rectTransform.anchoredPosition = new Vector2(-70f, 0f);

        System.Action refresh = () => shown.text = value();
        refreshers.Add(refresh);
        refresh();
        Arrow(row.transform, "<", -300f, () => { change(-1); RefreshAll(); });
        Arrow(row.transform, ">", -20f, () => { change(1); RefreshAll(); });
    }

    private void AddBinding(string key, string action)
    {
        RectTransform row = HudFactory.Rect("Binding", content);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
        TextMeshProUGUI keyText = HudFactory.Label("Key", row, 24f, TextAlignmentOptions.Left, HudFactory.Accent);
        HudFactory.Stretch(keyText.rectTransform);
        keyText.rectTransform.offsetMin = new Vector2(28f, 0f);
        keyText.text = key;
        TextMeshProUGUI actionText = HudFactory.Label("Action", row, 24f, TextAlignmentOptions.Left, HudFactory.Ink);
        HudFactory.Stretch(actionText.rectTransform);
        actionText.rectTransform.offsetMin = new Vector2(230f, 0f);
        actionText.text = action;
    }

    private void Arrow(Transform parent, string glyph, float x, System.Action onClick)
    {
        Image image = HudFactory.Image("Arrow" + glyph, parent, HudSprites.RoundedPanel, ButtonIdle);
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        image.rectTransform.pivot = new Vector2(1f, 0.5f);
        image.rectTransform.sizeDelta = new Vector2(44f, 44f);
        image.rectTransform.anchoredPosition = new Vector2(x, 0f);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        AddHover(image.gameObject, image);
        TextMeshProUGUI text = HudFactory.Label("Glyph", image.transform, 26f, TextAlignmentOptions.Center, HudFactory.Ink);
        HudFactory.Stretch(text.rectTransform);
        text.text = glyph;
        button.onClick.AddListener(() => onClick());
    }

    private void RefreshAll()
    {
        foreach (System.Action refresh in refreshers)
        {
            refresh();
        }
    }

    private static Image Plate(Transform parent, float height)
    {
        Image image = HudFactory.Image("Row", parent, HudSprites.RoundedPanel, ButtonIdle);
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;
        image.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return image;
    }

    private static void AddHover(GameObject target, Image image)
    {
        EventTrigger trigger = target.AddComponent<EventTrigger>();
        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => image.color = ButtonHover);
        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => image.color = ButtonIdle);
        trigger.triggers.Add(enter);
        trigger.triggers.Add(exit);
    }

    private void Build()
    {
        root = new GameObject("PauseMenuCanvas", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        Image dim = HudFactory.Image("Backdrop", root.transform, HudSprites.Square, Backdrop);
        HudFactory.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        RectTransform column = HudFactory.Rect("Column", root.transform);
        column.anchorMin = new Vector2(0f, 0.5f);
        column.anchorMax = new Vector2(0f, 0.5f);
        column.pivot = new Vector2(0f, 0.5f);
        column.anchoredPosition = new Vector2(140f, 0f);
        column.sizeDelta = new Vector2(760f, 860f);

        title = HudFactory.Label("Title", column, 64f, TextAlignmentOptions.BottomLeft, HudFactory.Ink);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.sizeDelta = new Vector2(0f, 90f);
        title.characterSpacing = 8f;

        content = HudFactory.Rect("Content", column);
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(1f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = new Vector2(0f, -110f);
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft;

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
        }
    }
}
