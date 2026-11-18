using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The main menu (Scenes/MainMenu, prefab Prefabs/UI/MainMenu.prefab, made once by Horror > Menu principal and
// editable from then on). Pages are CanvasGroups under the canvas, one shown at a time with a short fade:
//   Main: Continuar (only with a save), Jogar / Novo jogo, Carregar, Opções, Idioma, Créditos, Sair.
//   Load: the checkpoint (chapter, when, play time) with Carregar and Apagar.
//   Options: Controles, Som, Gráficos (SettingRow lines, SettingsCatalog), Language, Credits, a confirmation box.
// Esc, Backspace or the gamepad's B go back. On opening: black, the storm fades in, then the title, then the
// entries. Texts are LocalizedText / Loc, so a language change redraws everything at once.
public sealed class MainMenu : MonoBehaviour
{
    [Header("Pages")]
    [SerializeField] private CanvasGroup mainPage;
    [SerializeField] private CanvasGroup loadPage;
    [SerializeField] private CanvasGroup optionsPage;
    [SerializeField] private CanvasGroup controlsPage;
    [SerializeField] private CanvasGroup soundPage;
    [SerializeField] private CanvasGroup graphicsPage;
    [SerializeField] private CanvasGroup languagePage;
    [SerializeField] private CanvasGroup creditsPage;
    [SerializeField] private CanvasGroup confirmPage;

    [Header("Main")]
    [SerializeField] private MenuButton continueButton;
    [SerializeField] private MenuButton playButton;
    [SerializeField] private MenuButton loadButton;
    [SerializeField] private MenuButton optionsButton;
    [SerializeField] private MenuButton languageButton;
    [SerializeField] private MenuButton creditsButton;
    [SerializeField] private MenuButton quitButton;

    [Header("Load")]
    [SerializeField] private TMP_Text loadChapter;
    [SerializeField] private TMP_Text loadDetails;
    [SerializeField] private MenuButton loadGo;
    [SerializeField] private MenuButton loadDelete;
    [SerializeField] private MenuButton loadBack;

    [Header("Options")]
    [SerializeField] private MenuButton controlsButton;
    [SerializeField] private MenuButton soundButton;
    [SerializeField] private MenuButton graphicsButton;
    [SerializeField] private MenuButton optionsBack;
    [SerializeField] private MenuButton controlsBack;
    [SerializeField] private MenuButton soundBack;
    [SerializeField] private MenuButton graphicsBack;

    [Header("Language")]
    [SerializeField] private MenuButton[] languageButtons = new MenuButton[0];
    [SerializeField] private TMP_Text[] languageMarks = new TMP_Text[0];
    [SerializeField] private MenuButton languageBack;

    [Header("Credits")]
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private MenuButton creditsBack;

    [Header("Confirm")]
    [SerializeField] private TMP_Text confirmTitle;
    [SerializeField] private TMP_Text confirmBody;
    [SerializeField] private MenuButton confirmYes;
    [SerializeField] private MenuButton confirmNo;

    [Header("Opening")]
    [SerializeField] private CanvasGroup blackout;
    [SerializeField] private CanvasGroup title;
    [SerializeField] private CanvasGroup hints;
    [SerializeField] private float blackSeconds = 2.2f;
    [SerializeField] private float titleSeconds = 2.6f;

    private const float FadeSeconds = 0.28f;
    // The photosensitivity warning: shown once per run, before the storm fades in, for this long or until a key.
    private const float WarningSeconds = 8f;
    // The credits turn their own pages (TMP page overflow) with the arrows, the wheel, or by themselves.
    private const float CreditsPageSeconds = 11f;

    private static bool warningShown;
    private TMP_Text creditsPager;
    private float creditsPageTimer;
    private int creditsPagesShown;

    private readonly Dictionary<CanvasGroup, GameObject> firstOf = new Dictionary<CanvasGroup, GameObject>();
    private readonly Dictionary<CanvasGroup, CanvasGroup> parentOf = new Dictionary<CanvasGroup, CanvasGroup>();
    private readonly Dictionary<CanvasGroup, Vector2> homeOf = new Dictionary<CanvasGroup, Vector2>();
    private CanvasGroup current;
    private CanvasGroup confirmReturn;
    private System.Action confirmAction;
    private GameObject lastSelected;
    private bool ready;

    private void Awake()
    {
        // The menu runs at 60 at most (the player's own lower cap still applies); lifted when it closes.
        GameSettings.SetSceneFrameRateLimit(60);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Wire(continueButton, () => SceneFlow.Continue());
        Wire(playButton, OnPlay);
        Wire(loadButton, () => Open(loadPage));
        Wire(optionsButton, () => Open(optionsPage));
        Wire(languageButton, () => Open(languagePage));
        Wire(creditsButton, () => Open(creditsPage));
        Wire(quitButton, () => Confirm("menu.quit.title", null, "menu.quit.yes", Quit, mainPage));

        Wire(loadGo, () => SceneFlow.Continue());
        Wire(loadDelete, () => Confirm("menu.delete.title", null, "menu.delete.yes", () =>
        {
            SaveGame.Delete();
            RefreshMain();
            Open(loadPage);
        }, loadPage));
        Wire(loadBack, Back);

        Wire(controlsButton, () => Open(controlsPage));
        Wire(soundButton, () => Open(soundPage));
        Wire(graphicsButton, () => Open(graphicsPage));
        Wire(optionsBack, Back);
        Wire(controlsBack, Back);
        Wire(soundBack, Back);
        Wire(graphicsBack, Back);

        for (int i = 0; i < languageButtons.Length; i++)
        {
            int index = i;
            Wire(languageButtons[i], () => Loc.SetLanguage(index));
        }

        Wire(languageBack, Back);
        Wire(creditsBack, Back);
        Wire(confirmYes, () => confirmAction?.Invoke());
        Wire(confirmNo, Back);

        parentOf[loadPage] = mainPage;
        parentOf[optionsPage] = mainPage;
        parentOf[languagePage] = mainPage;
        parentOf[creditsPage] = mainPage;
        parentOf[controlsPage] = optionsPage;
        parentOf[soundPage] = optionsPage;
        parentOf[graphicsPage] = optionsPage;

        foreach (CanvasGroup page in Pages())
        {
            if (page != null)
            {
                homeOf[page] = ((RectTransform)page.transform).anchoredPosition;
                Hide(page);
            }
        }
    }

    private void OnEnable()
    {
        Loc.Changed += OnLanguage;
    }

    private void OnDisable()
    {
        Loc.Changed -= OnLanguage;
    }

    private void OnDestroy()
    {
        GameSettings.SetSceneFrameRateLimit(0);
    }

    private IEnumerator Start()
    {
        RefreshMain();
        RefreshLanguage();
        RefreshCredits();
        if (EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        // The opening: the storm out of black, then the title, then the menu. Any key skips it.
        title.alpha = 0f;
        hints.alpha = 0f;
        blackout.alpha = 1f;
        blackout.blocksRaycasts = true;
        if (!warningShown)
        {
            warningShown = true;
            yield return ShowWarning();
        }

        float t = 0f;
        while (t < blackSeconds + titleSeconds && !AnyKey())
        {
            t += Time.unscaledDeltaTime;
            blackout.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / blackSeconds);
            title.alpha = Mathf.SmoothStep(0f, 1f, (t - blackSeconds * 0.55f) / titleSeconds);
            yield return null;
        }

        blackout.alpha = 0f;
        blackout.blocksRaycasts = false;
        title.alpha = 1f;
        ready = true;
        Open(mainPage);
        for (float h = 0f; h < 1f; h += Time.unscaledDeltaTime / 0.8f)
        {
            hints.alpha = h;
            yield return null;
        }

        hints.alpha = 1f;
    }

    private void Update()
    {
        if (!ready || SceneFlow.Busy)
        {
            return;
        }

        if (BackPressed())
        {
            if (current != mainPage)
            {
                MenuSounds.Back();
                Back();
            }
        }

        if (current == creditsPage)
        {
            TurnCreditsPages();
        }

        // Keyboard or gamepad with nothing selected (the mouse moved off): bring the selection back.
        EventSystem events = EventSystem.current;
        if (events == null)
        {
            return;
        }

        GameObject selected = events.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy)
        {
            lastSelected = selected;
        }
        else if (NavigationPressed())
        {
            MenuSounds.SilenceThisFrame();
            events.SetSelectedGameObject(lastSelected != null && lastSelected.activeInHierarchy ? lastSelected : First(current));
        }
    }

    // ---------------------------------------------------------------- pages

    private IEnumerable<CanvasGroup> Pages()
    {
        yield return mainPage;
        yield return loadPage;
        yield return optionsPage;
        yield return controlsPage;
        yield return soundPage;
        yield return graphicsPage;
        yield return languagePage;
        yield return creditsPage;
        yield return confirmPage;
    }

    private void Open(CanvasGroup page)
    {
        if (page == loadPage)
        {
            RefreshLoad();
        }
        else if (page == creditsPage && creditsText != null)
        {
            // From the first page, with the page count measured once the layout has sized the text.
            creditsText.pageToDisplay = 1;
            creditsPageTimer = 0f;
            StartCoroutine(RefreshCreditsPagerLater());
        }

        CanvasGroup previous = current;
        current = page;
        if (previous != null && previous != page)
        {
            StartCoroutine(FadeOut(previous));
        }

        page.gameObject.SetActive(true);
        StartCoroutine(FadeIn(page));
        MenuSounds.SilenceThisFrame();
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(First(page));
        }
    }

    private void Back()
    {
        if (current == confirmPage)
        {
            Open(confirmReturn != null ? confirmReturn : mainPage);
        }
        else if (current != null && parentOf.TryGetValue(current, out CanvasGroup parent))
        {
            Open(parent);
        }
        else
        {
            Open(mainPage);
        }
    }

    private void Confirm(string titleKey, string bodyKey, string yesKey, System.Action onYes, CanvasGroup returnTo)
    {
        confirmTitle.text = Loc.T(titleKey);
        confirmBody.text = string.IsNullOrEmpty(bodyKey) ? string.Empty : Loc.T(bodyKey);
        confirmYes.Label.text = Loc.T(yesKey).ToUpperInvariant();
        confirmNo.Label.text = Loc.T("menu.confirm.no").ToUpperInvariant();
        confirmAction = onYes;
        confirmReturn = returnTo;
        Open(confirmPage);
        // "No" first: a stray Enter never destroys anything.
        if (EventSystem.current != null)
        {
            MenuSounds.SilenceThisFrame();
            EventSystem.current.SetSelectedGameObject(confirmNo.gameObject);
        }
    }

    private GameObject First(CanvasGroup page)
    {
        if (page == null)
        {
            return null;
        }

        if (firstOf.TryGetValue(page, out GameObject first) && first != null && first.activeInHierarchy)
        {
            return first;
        }

        foreach (Selectable s in page.GetComponentsInChildren<Selectable>(false))
        {
            if (s.interactable && s.navigation.mode != Navigation.Mode.None)
            {
                return s.gameObject;
            }
        }

        return null;
    }

    private IEnumerator FadeIn(CanvasGroup page)
    {
        page.interactable = true;
        page.blocksRaycasts = true;
        RectTransform rect = (RectTransform)page.transform;
        Vector2 home = homeOf.TryGetValue(page, out Vector2 h) ? h : rect.anchoredPosition;
        for (float t = page.alpha; t < 1f; t += Time.unscaledDeltaTime / FadeSeconds)
        {
            if (current != page)
            {
                rect.anchoredPosition = home;
                yield break;
            }

            page.alpha = t;
            rect.anchoredPosition = home + new Vector2(0f, -10f * (1f - t));
            yield return null;
        }

        page.alpha = 1f;
        rect.anchoredPosition = home;
    }

    private IEnumerator FadeOut(CanvasGroup page)
    {
        page.interactable = false;
        page.blocksRaycasts = false;
        for (float t = page.alpha; t > 0f; t -= Time.unscaledDeltaTime / (FadeSeconds * 0.6f))
        {
            // Opened again before it finished fading: leave it to FadeIn.
            if (current == page)
            {
                yield break;
            }

            page.alpha = t;
            yield return null;
        }

        if (current != page)
        {
            Hide(page);
        }
    }

    private static void Hide(CanvasGroup page)
    {
        page.alpha = 0f;
        page.interactable = false;
        page.blocksRaycasts = false;
        page.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- content

    private void OnPlay()
    {
        if (SaveGame.Exists)
        {
            Confirm("menu.confirm.title", "menu.confirm.body", "menu.confirm.yes", SceneFlow.NewGame, mainPage);
        }
        else
        {
            SceneFlow.NewGame();
        }
    }

    // With a save: Continuar on top and "Novo jogo"; without: just "Jogar".
    private void RefreshMain()
    {
        bool saved = SaveGame.Exists;
        continueButton.gameObject.SetActive(saved);
        LocalizedText playLabel = playButton.Label.GetComponent<LocalizedText>();
        if (playLabel != null)
        {
            playLabel.Key = saved ? "menu.newgame" : "menu.play";
        }

        firstOf[mainPage] = saved ? continueButton.gameObject : playButton.gameObject;
    }

    private void RefreshLoad()
    {
        SaveGame.Data data = SaveGame.Read();
        loadGo.gameObject.SetActive(data != null);
        loadDelete.gameObject.SetActive(data != null);
        if (data == null)
        {
            loadChapter.text = Loc.T("menu.load.title").ToUpperInvariant();
            loadDetails.text = Loc.T("menu.load.empty");
            firstOf[loadPage] = loadBack.gameObject;
            return;
        }

        string chapter = Loc.Has("stage." + data.stage) ? Loc.T("stage." + data.stage) : data.stage;
        loadChapter.text = chapter.ToUpperInvariant();
        string when = data.savedAt;
        if (System.DateTime.TryParse(data.savedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out System.DateTime saved))
        {
            when = saved.ToString("g", Culture());
        }

        loadDetails.text = Loc.F("menu.load.saved", when) + "\n" + Loc.F("menu.load.time", SaveGame.FormatPlayTime(data.playSeconds));
        firstOf[loadPage] = loadGo.gameObject;
    }

    private static System.Globalization.CultureInfo Culture()
    {
        try
        {
            return new System.Globalization.CultureInfo(Loc.Code);
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            return System.Globalization.CultureInfo.InvariantCulture;
        }
    }

    private void RefreshLanguage()
    {
        for (int i = 0; i < languageMarks.Length; i++)
        {
            if (languageMarks[i] != null)
            {
                languageMarks[i].text = i == Loc.Language ? "◆" : string.Empty;
            }
        }

        if (languageButtons.Length > Loc.Language && languageButtons[Loc.Language] != null)
        {
            firstOf[languagePage] = languageButtons[Loc.Language].gameObject;
        }
    }

    // The credits: who made it, and the third-party assets whose licence asks for a credit in the game
    // (CC BY 4.0: author, title and licence), then the rest by source (CREDITS.md has the full list).
    private void RefreshCredits()
    {
        if (creditsText == null)
        {
            return;
        }

        const string head = "<size=70%><color=#B89A6E>";
        const string end = "</color></size>";
        System.Text.StringBuilder text = new System.Text.StringBuilder(4096);
        text.Append(head).Append(Loc.T("credits.dev").ToUpperInvariant()).Append(end).Append("\nGustavo Perin\n\n")
            .Append(head).Append(Loc.T("credits.story").ToUpperInvariant()).Append(end).Append("\nDayse\n\n")
            .Append(head).Append(Loc.T("credits.third").ToUpperInvariant()).Append(end).Append("\n<size=62%>")
            .Append("MetaHuman · Epic Games    Quixel Megascans · Fab\n")
            .Append("Poly Haven · ambientCG · Kenney · Freesound (CC0)\n")
            .Append("Cooper Hewitt, Smithsonian Design Museum (CC0)    AllSky Free · rpgwhitelock    Texturelabs\n")
            .Append("Oswald (SIL OFL) · Special Elite (Apache 2.0)\n")
            .Append("Unity URP Volumetric Light · Cristian Qiu (MIT)    SSGI URP · jiaozi158 (MIT)\n\n")
            .Append(Loc.T("credits.licenses")).Append('\n');
        // Every CC BY asset (title, author, licence, source; from CREDITS.md) must be named in the game itself.
        TextAsset list = Resources.Load<TextAsset>("Credits/thirdparty");
        if (list != null)
        {
            foreach (string line in list.text.Split('\n'))
            {
                string row = line.Trim();
                if (row.Length == 0 || row[0] == '#')
                {
                    continue;
                }

                string[] cols = row.Split('\t');
                text.Append('"').Append(cols[0]).Append('"');
                if (cols.Length > 1)
                {
                    text.Append(" · ").Append(cols[1]);
                }

                if (cols.Length > 2)
                {
                    text.Append(" · ").Append(cols[2]);
                }

                text.Append('\n');
            }
        }

        text.Append("</size>\n<size=80%>").Append(Loc.T("credits.thanks")).Append("</size>");
        creditsText.text = text.ToString();
        creditsText.overflowMode = TextOverflowModes.Page;
        creditsText.pageToDisplay = 1;
        creditsPageTimer = 0f;
        if (creditsPager == null)
        {
            // A small line under the text: page x of y and how to turn them, in the credits' own font.
            creditsPager = Instantiate(creditsText, creditsText.transform.parent);
            creditsPager.name = "CreditsPager";
            creditsPager.transform.SetSiblingIndex(creditsText.transform.GetSiblingIndex() + 1);
            creditsPager.overflowMode = TextOverflowModes.Overflow;
            creditsPager.fontSize = creditsText.fontSize * 0.7f;
            creditsPager.color = new Color(0.72f, 0.6f, 0.43f, 1f);
            creditsPager.alignment = TextAlignmentOptions.Center;
            Destroy(creditsPager.GetComponent<LocalizedText>());
            LayoutElement pagerLayout = creditsPager.GetComponent<LayoutElement>();
            if (pagerLayout != null)
            {
                pagerLayout.preferredHeight = 34f;
                pagerLayout.minHeight = 34f;
            }
        }

        RefreshCreditsPager();
    }

    // The page count comes from TMP's own layout, which only knows the text's real height once the layout group has
    // sized it (a frame or two after the page opens): the label is also re-read while the page is up.
    private void RefreshCreditsPager()
    {
        int pages = Mathf.Max(1, creditsText.textInfo.pageCount);
        creditsText.pageToDisplay = Mathf.Clamp(creditsText.pageToDisplay, 1, pages);
        creditsPagesShown = pages;
        creditsPager.text = pages > 1 ? Loc.F("credits.pages", creditsText.pageToDisplay, pages) : string.Empty;
    }

    private IEnumerator RefreshCreditsPagerLater()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        if (creditsPager != null)
        {
            creditsText.ForceMeshUpdate(true, true);
            RefreshCreditsPager();
        }
    }

    private void TurnCreditsPages()
    {
        if (creditsText.textInfo.pageCount != creditsPagesShown)
        {
            RefreshCreditsPager();
        }

        int step = 0;
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        Gamepad g = Gamepad.current;
        if ((k != null && (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame || k.pageDownKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) ||
            (g != null && (g.dpad.right.wasPressedThisFrame || g.leftStick.right.wasPressedThisFrame)) ||
            (m != null && m.scroll.ReadValue().y < -0.01f))
        {
            step = 1;
        }
        else if ((k != null && (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame || k.pageUpKey.wasPressedThisFrame)) ||
                 (g != null && (g.dpad.left.wasPressedThisFrame || g.leftStick.left.wasPressedThisFrame)) ||
                 (m != null && m.scroll.ReadValue().y > 0.01f))
        {
            step = -1;
        }

        creditsPageTimer += Time.unscaledDeltaTime;
        if (step == 0 && creditsPageTimer >= CreditsPageSeconds)
        {
            step = 1;
        }

        if (step == 0)
        {
            return;
        }

        creditsPageTimer = 0f;
        int pages = Mathf.Max(1, creditsText.textInfo.pageCount);
        int page = creditsText.pageToDisplay + step;
        creditsText.pageToDisplay = page > pages ? 1 : (page < 1 ? pages : page);
        RefreshCreditsPager();
    }

    // Black screen with the photosensitivity notice (flashing lights, lightning, loud sounds), built here so the
    // menu prefab does not change; fades in, waits for a key or WarningSeconds, fades out and is destroyed.
    private IEnumerator ShowWarning()
    {
        Transform canvas = blackout.transform.parent;
        RectTransform panel = new GameObject("Warning", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(canvas, false);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        Image back = panel.gameObject.AddComponent<Image>();
        back.color = Color.black;
        back.raycastTarget = true;
        CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        TMP_Text heading = WarningLabel(panel, "Title", 44f, new Color(0.86f, 0.62f, 0.3f, 1f), new Vector2(0f, 210f), new Vector2(1200f, 70f));
        heading.text = Loc.T("warn.title");
        heading.characterSpacing = 12f;
        TMP_Text body = WarningLabel(panel, "Body", 27f, new Color(0.86f, 0.82f, 0.74f, 1f), new Vector2(0f, 20f), new Vector2(1100f, 320f));
        body.text = Loc.T("warn.body").Replace("\\n", "\n");
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = 8f;
        TMP_Text hint = WarningLabel(panel, "Hint", 22f, new Color(0.6f, 0.55f, 0.47f, 1f), new Vector2(0f, -230f), new Vector2(1100f, 40f));
        hint.text = Loc.T("warn.continue");

        float t = 0f;
        while (t < 0.7f)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / 0.7f);
            yield return null;
        }

        t = 0f;
        while (t < WarningSeconds && !AnyKey())
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / 0.5f);
            yield return null;
        }

        Destroy(panel.gameObject);
    }

    private TMP_Text WarningLabel(Transform parent, string name, float size, Color colour, Vector2 at, Vector2 box)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = at;
        rect.sizeDelta = box;
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = creditsText.font;
        label.fontSharedMaterial = creditsText.fontSharedMaterial;
        label.fontSize = size;
        label.color = colour;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private void OnLanguage()
    {
        RefreshMain();
        RefreshLanguage();
        RefreshCredits();
        if (current == loadPage)
        {
            RefreshLoad();
        }
    }

    // ---------------------------------------------------------------- input

    private static bool AnyKey()
    {
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        Gamepad g = Gamepad.current;
        return (k != null && k.anyKey.wasPressedThisFrame) || (m != null && m.leftButton.wasPressedThisFrame) ||
               (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame));
    }

    private static bool BackPressed()
    {
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        Mouse m = Mouse.current;
        return (k != null && (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame)) ||
               (g != null && g.buttonEast.wasPressedThisFrame) || (m != null && m.rightButton.wasPressedThisFrame);
    }

    private static bool NavigationPressed()
    {
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        return (k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame ||
                              k.sKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame)) ||
               (g != null && (g.dpad.ReadValue() != Vector2.zero || g.leftStick.ReadValue().sqrMagnitude > 0.25f));
    }

    private static void Wire(MenuButton button, System.Action action)
    {
        if (button != null)
        {
            button.Button.onClick.AddListener(() => action());
        }
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
