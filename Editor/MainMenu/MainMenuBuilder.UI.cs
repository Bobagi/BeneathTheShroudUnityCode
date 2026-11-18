using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// The main menu's UI as a prefab (Prefabs/UI/MainMenu.prefab): built here ONCE, then edited by hand like any prefab
// (Horror > Menu principal > Recriar o prefab da interface throws the edits away and builds it again). Laid out on a
// 1920 x 1080 canvas: the title centred in the upper third, the pages under it, centred, crossing the waterline (the
// entries under the line sit on the dark water), the key hints at the bottom. Type: Oswald (SIL OFL) in spaced
// capitals, as the title's "BENEATH THE"; every text a LocalizedText (Settings/Resources/Localization/ui.txt).
public static partial class MainMenuBuilder
{
    public const string PrefabPath = ProjectPaths.Root + "/Prefabs/UI/MainMenu.prefab";
    private const string TitlePath = ProjectPaths.Root + "/UI/Title/BeneathTheShroud_Title.png";
    private const string FontFolder = ProjectPaths.Fonts + "/Oswald";
    private const string ShadePath = ProjectPaths.Root + "/UI/Title/MenuShade.png";

    private static readonly Color Ink = new Color(0.86f, 0.82f, 0.74f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.6f, 0.55f, 0.85f);
    private static readonly Color Warm = new Color(1f, 0.8f, 0.52f, 1f);

    private static TMP_FontAsset light;
    private static TMP_FontAsset regular;
    private static Material lightShadow;
    private static Material regularShadow;

    [MenuItem("Horror/Menu principal/Recriar o prefab da interface")]
    public static void RebuildPrefab()
    {
        if (!EditorUtility.DisplayDialog("Recriar o prefab do menu", "O prefab Prefabs/UI/MainMenu.prefab será refeito do zero e as edições feitas nele à mão serão perdidas.", "Recriar", "Cancelar"))
        {
            return;
        }

        AssetDatabase.DeleteAsset(PrefabPath);
        EnsureMenuPrefab();
    }

    private static GameObject EnsureMenuPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
        {
            return existing;
        }

        EnsureFolder(ProjectPaths.Root, "Prefabs");
        EnsureFolder(ProjectPaths.Root + "/Prefabs", "UI");
        Fonts();
        Sprite titleSprite = TitleSprite();
        Sprite shade = ShadeSprite();

        GameObject rootObject = new GameObject("MainMenu", typeof(RectTransform));
        Canvas canvas = rootObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = rootObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // Scaled by the height only: with half width, on 21:9 the UI grew past the screen's height and the
        // entries rode up over the title (review 10/10). Wider screens just get more room at the sides.
        scaler.matchWidthOrHeight = 1f;
        rootObject.AddComponent<GraphicRaycaster>();
        MainMenu menu = rootObject.AddComponent<MainMenu>();
        rootObject.AddComponent<MenuSounds>();
        Transform r = rootObject.transform;

        // A soft darkening behind the title and the entries, so the type reads over the sky and the lightning.
        Image titleShade = Img("TitleShade", r, shade, new Color(0f, 0f, 0f, 0.55f));
        Place(titleShade.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(1400f, 520f));
        Image pagesShade = Img("PagesShade", r, shade, new Color(0f, 0f, 0f, 0.45f));
        Place(pagesShade.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(1100f, 760f));

        // The title.
        RectTransform title = Rect("Title", r);
        Place(title, new Vector2(0.5f, 1f), new Vector2(0f, -225f), new Vector2(800f, 280f));
        CanvasGroup titleGroup = title.gameObject.AddComponent<CanvasGroup>();
        titleGroup.interactable = false;
        titleGroup.blocksRaycasts = false;
        Image logo = Img("Logo", title, titleSprite, MainMenuPrefabPreview.LogoTint);
        Stretch(logo.rectTransform);
        logo.preserveAspect = true;
        Image flash = Img("Flash", title, titleSprite, new Color(0.72f, 0.86f, 1f, 0f));
        Stretch(flash.rectTransform);
        flash.preserveAspect = true;
        TitleLogo titleLogo = title.gameObject.AddComponent<TitleLogo>();
        Set(titleLogo, "flash", flash);
        // Under the title's right end (Gustavo 10/10), like an edition mark.
        TMP_Text demo = Text("Demo", title, light, lightShadow, 20f, Muted, TextAlignmentOptions.Right, "menu.demo", true);
        Place(demo.rectTransform, new Vector2(1f, 0f), new Vector2(-212f, -6f), new Vector2(400f, 30f));
        demo.characterSpacing = 40f;

        // Pages.
        RectTransform pages = Rect("Pages", r);
        Stretch(pages);

        CanvasGroup main = Page("MainPage", pages, 2f);
        MenuButton cont = Entry(main.transform, "menu.continue");
        MenuButton play = Entry(main.transform, "menu.play");
        MenuButton load = Entry(main.transform, "menu.load");
        MenuButton options = Entry(main.transform, "menu.options");
        MenuButton language = Entry(main.transform, "menu.language");
        MenuButton credits = Entry(main.transform, "menu.credits");
        MenuButton quit = Entry(main.transform, "menu.quit");

        CanvasGroup loadPage = Page("LoadPage", pages, 10f);
        TMP_Text chapter = Text("Chapter", loadPage.transform, regular, regularShadow, 40f, Ink, TextAlignmentOptions.Center, null, false);
        chapter.characterSpacing = 10f;
        Layout(chapter.gameObject, 60f);
        TMP_Text details = Text("Details", loadPage.transform, light, lightShadow, 24f, Muted, TextAlignmentOptions.Center, null, false);
        details.textWrappingMode = TextWrappingModes.Normal;
        Layout(details.gameObject, 92f);
        MenuButton loadGo = Entry(loadPage.transform, "menu.load.go");
        MenuButton loadDelete = Entry(loadPage.transform, "menu.load.delete");
        MenuButton loadBack = Entry(loadPage.transform, "menu.back", true);

        CanvasGroup optionsPage = Page("OptionsPage", pages, 6f);
        MenuButton controls = Entry(optionsPage.transform, "menu.controls");
        MenuButton sound = Entry(optionsPage.transform, "menu.sound");
        MenuButton graphics = Entry(optionsPage.transform, "menu.graphics");
        MenuButton optionsBack = Entry(optionsPage.transform, "menu.back", true);

        CanvasGroup controlsPage = Page("ControlsPage", pages, 2f);
        Narrow(controlsPage);
        foreach ((string keys, string actionKey) in SettingsCatalog.Bindings)
        {
            Binding(controlsPage.transform, keys, actionKey);
        }

        Spacer(controlsPage.transform, 10f);
        foreach (SettingsCatalog.Setting s in SettingsCatalog.On(SettingsCatalog.Page.Controls))
        {
            Row(controlsPage.transform, s.id);
        }

        MenuButton controlsBack = Entry(controlsPage.transform, "menu.back", true);

        CanvasGroup soundPage = Page("SoundPage", pages, 6f);
        Narrow(soundPage);
        foreach (SettingsCatalog.Setting s in SettingsCatalog.On(SettingsCatalog.Page.Sound))
        {
            Row(soundPage.transform, s.id);
        }

        MenuButton soundBack = Entry(soundPage.transform, "menu.back", true);

        CanvasGroup graphicsPage = Page("GraphicsPage", pages, 4f);
        Narrow(graphicsPage);
        foreach (SettingsCatalog.Setting s in SettingsCatalog.On(SettingsCatalog.Page.Graphics))
        {
            Row(graphicsPage.transform, s.id);
        }

        MenuButton graphicsBack = Entry(graphicsPage.transform, "menu.back", true);

        CanvasGroup languagePage = Page("LanguagePage", pages, 6f);
        MenuButton[] languageButtons = new MenuButton[Loc.Names.Length];
        TMP_Text[] marks = new TMP_Text[Loc.Names.Length];
        for (int i = 0; i < Loc.Names.Length; i++)
        {
            languageButtons[i] = Entry(languagePage.transform, null);
            languageButtons[i].name = "Language_" + Loc.Codes[i];
            languageButtons[i].Label.text = Loc.Names[i].ToUpperInvariant();
            Object.DestroyImmediate(languageButtons[i].Label.GetComponent<LocalizedText>());
            marks[i] = Text("Mark", languageButtons[i].transform, regular, regularShadow, 16f, Warm, TextAlignmentOptions.Center, null, false);
            Place(marks[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-230f, 0f), new Vector2(40f, 40f));
        }

        TMP_Text note = Text("StoryNote", languagePage.transform, light, lightShadow, 19f, Muted, TextAlignmentOptions.Center, "opt.storynote", false);
        note.textWrappingMode = TextWrappingModes.Normal;
        Layout(note.gameObject, 64f);
        MenuButton languageBack = Entry(languagePage.transform, "menu.back", true);

        CanvasGroup creditsPage = Page("CreditsPage", pages, 6f);
        // Top-aligned in a box tall enough for every line: centred, the overflow ran up into the title and down
        // into "Voltar" (review 10/10).
        TMP_Text creditsText = Text("Credits", creditsPage.transform, light, lightShadow, 26f, Ink, TextAlignmentOptions.Top, null, false);
        creditsText.textWrappingMode = TextWrappingModes.Normal;
        creditsText.lineSpacing = -10f;
        Layout(creditsText.gameObject, 470f);
        MenuButton creditsBack = Entry(creditsPage.transform, "menu.back", true);

        CanvasGroup confirmPage = Page("ConfirmPage", pages, 8f);
        TMP_Text confirmTitle = Text("Question", confirmPage.transform, regular, regularShadow, 36f, Ink, TextAlignmentOptions.Center, null, false);
        Layout(confirmTitle.gameObject, 56f);
        TMP_Text confirmBody = Text("Body", confirmPage.transform, light, lightShadow, 22f, Muted, TextAlignmentOptions.Center, null, false);
        confirmBody.textWrappingMode = TextWrappingModes.Normal;
        Layout(confirmBody.gameObject, 64f);
        MenuButton yes = Entry(confirmPage.transform, "menu.confirm.yes");
        MenuButton no = Entry(confirmPage.transform, "menu.confirm.no", true);

        // Key hints and the black of the opening.
        RectTransform hintRect = Rect("Hints", r);
        Place(hintRect, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(900f, 34f));
        CanvasGroup hints = hintRect.gameObject.AddComponent<CanvasGroup>();
        hints.blocksRaycasts = false;
        TMP_Text hint = Text("Text", hintRect, light, lightShadow, 19f, Muted, TextAlignmentOptions.Center, "menu.hint", false);
        Stretch(hint.rectTransform);
        hint.characterSpacing = 4f;

        Image black = Img("Blackout", r, null, Color.black);
        Stretch(black.rectTransform);
        black.raycastTarget = true;
        CanvasGroup blackout = black.gameObject.AddComponent<CanvasGroup>();

        Set(menu, "mainPage", main);
        Set(menu, "loadPage", loadPage);
        Set(menu, "optionsPage", optionsPage);
        Set(menu, "controlsPage", controlsPage);
        Set(menu, "soundPage", soundPage);
        Set(menu, "graphicsPage", graphicsPage);
        Set(menu, "languagePage", languagePage);
        Set(menu, "creditsPage", creditsPage);
        Set(menu, "confirmPage", confirmPage);
        Set(menu, "continueButton", cont);
        Set(menu, "playButton", play);
        Set(menu, "loadButton", load);
        Set(menu, "optionsButton", options);
        Set(menu, "languageButton", language);
        Set(menu, "creditsButton", credits);
        Set(menu, "quitButton", quit);
        Set(menu, "loadChapter", chapter);
        Set(menu, "loadDetails", details);
        Set(menu, "loadGo", loadGo);
        Set(menu, "loadDelete", loadDelete);
        Set(menu, "loadBack", loadBack);
        Set(menu, "controlsButton", controls);
        Set(menu, "soundButton", sound);
        Set(menu, "graphicsButton", graphics);
        Set(menu, "optionsBack", optionsBack);
        Set(menu, "controlsBack", controlsBack);
        Set(menu, "soundBack", soundBack);
        Set(menu, "graphicsBack", graphicsBack);
        SetArray(menu, "languageButtons", languageButtons);
        SetArray(menu, "languageMarks", marks);
        Set(menu, "languageBack", languageBack);
        Set(menu, "creditsText", creditsText);
        Set(menu, "creditsBack", creditsBack);
        Set(menu, "confirmTitle", confirmTitle);
        Set(menu, "confirmBody", confirmBody);
        Set(menu, "confirmYes", yes);
        Set(menu, "confirmNo", no);
        Set(menu, "blackout", blackout);
        Set(menu, "title", titleGroup);
        Set(menu, "hints", hints);

        MenuSounds sounds = rootObject.GetComponent<MenuSounds>();
        // Kenney's click_004 for every ordinary entry (Gustavo 10/10); starting or loading a game has its own sound.
        AudioClip click = AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/UI/menu_click.wav");
        Set(sounds, "move", click);
        Set(sounds, "confirm", click);
        Set(sounds, "back", click);
        // Starting or loading a game: candidate 19, TommasoMotteran's ghostly whoosh (Gustavo 10/10).
        Set(sounds, "start", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/UI/menu_start.wav"));
        foreach (MenuButton starter in new[] { cont, play, loadGo })
        {
            SetBool(starter, "startsGame", true);
        }

        // As it looks in the game also outside Play: no black over it, only the main page on, no Continue (no save).
        MainMenuPrefabPreview.Apply(rootObject);
        cont.gameObject.SetActive(false);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootObject, PrefabPath);
        Object.DestroyImmediate(rootObject);
        return prefab;
    }

    // ---- pieces ----------------------------------------------------------------------------------------------------

    // A page: centred under the title, its entries stacked from the top down.
    private static CanvasGroup Page(string name, Transform parent, float spacing)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(1000f, 640f));
        rect.pivot = new Vector2(0.5f, 1f);
        // Lower than at first (Gustavo moved them down by hand 10/10: too close to the title).
        rect.anchoredPosition = new Vector2(0f, PagesTop);
        VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return rect.gameObject.AddComponent<CanvasGroup>();
    }

    // Option pages a little narrower than the others, so a setting's name and its value read as one line (at
    // 1000 they sat 600 px apart).
    private static void Narrow(CanvasGroup page)
    {
        RectTransform rect = (RectTransform)page.transform;
        rect.sizeDelta = new Vector2(OptionPageWidth, rect.sizeDelta.y);
    }

    public const float OptionPageWidth = 820f;

    // Where the pages start, above the screen's centre (1080 high).
    public const float PagesTop = 96f;

    private static MenuButton Entry(Transform parent, string key, bool back = false)
    {
        RectTransform rect = Rect(key != null ? "Button_" + key : "Button", parent);
        Layout(rect.gameObject, 44f);
        Image hit = rect.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.Vertical;
        button.navigation = nav;

        TMP_Text label = Text("Label", rect, light, lightShadow, 29f, Ink, TextAlignmentOptions.Center, key, true);
        Stretch(label.rectTransform);
        label.characterSpacing = 9f;

        MenuButton entry = rect.gameObject.AddComponent<MenuButton>();
        Set(entry, "label", label);
        SetBool(entry, "backButton", back);
        return entry;
    }

    // An option line: the name left, the value right between arrows (SettingRow reads SettingsCatalog).
    private static void Row(Transform parent, string settingId)
    {
        RectTransform rect = Rect("Setting_" + settingId, parent);
        Layout(rect.gameObject, 48f);
        Image hit = rect.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.Vertical;
        button.navigation = nav;

        TMP_Text label = Text("Label", rect, light, lightShadow, 25f, Ink, TextAlignmentOptions.Left, null, false);
        Stretch(label.rectTransform);
        // Room for the longest name ("Sensibilidade do mouse") before the value's arrows (review 10/10).
        label.rectTransform.offsetMin = new Vector2(40f, 0f);
        label.characterSpacing = 6f;
        TMP_Text value = Text("Value", rect, regular, regularShadow, 25f, Warm, TextAlignmentOptions.Center, null, false);
        Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(-185f, 0f), new Vector2(240f, 44f));
        Button previous = Arrow(rect, "<", -330f);
        Button next = Arrow(rect, ">", -40f);
        SettingRow row = rect.gameObject.AddComponent<SettingRow>();
        SetString(row, "settingId", settingId);
        Set(row, "label", label);
        Set(row, "value", value);
        Set(row, "previous", previous);
        Set(row, "next", next);
    }

    private static Button Arrow(Transform parent, string glyph, float x)
    {
        RectTransform rect = Rect("Arrow" + (glyph == "<" ? "Left" : "Right"), parent);
        Place(rect, new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(44f, 44f));
        Image hit = rect.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colours = button.colors;
        colours.normalColor = Color.white;
        colours.highlightedColor = Color.white;
        button.colors = colours;
        button.targetGraphic = hit;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        TMP_Text text = Text("Glyph", rect, regular, regularShadow, 28f, Muted, TextAlignmentOptions.Center, null, false);
        Stretch(text.rectTransform);
        text.text = glyph;
        return button;
    }

    private static void Binding(Transform parent, string keys, string actionKey)
    {
        RectTransform rect = Rect("Binding", parent);
        Layout(rect.gameObject, 31f);
        TMP_Text key = Text("Keys", rect, regular, regularShadow, 21f, Warm, TextAlignmentOptions.Right, null, false);
        Place(key.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f), new Vector2(300f, 31f));
        key.text = keys;
        key.characterSpacing = 3f;
        TMP_Text action = Text("Action", rect, light, lightShadow, 21f, Ink, TextAlignmentOptions.Left, actionKey, false);
        Place(action.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(225f, 0f), new Vector2(420f, 31f));
    }

    private static void Spacer(Transform parent, float height)
    {
        Layout(Rect("Space", parent).gameObject, height);
    }

    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Material material, float size, Color colour, TextAlignmentOptions alignment, string key, bool upper)
    {
        RectTransform rect = Rect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        if (material != null)
        {
            text.fontSharedMaterial = material;
        }

        text.fontSize = size;
        text.color = colour;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        if (key != null)
        {
            string value = Loc.T(key);
            text.text = upper ? value.ToUpperInvariant() : value;
            LocalizedText localized = rect.gameObject.AddComponent<LocalizedText>();
            SetString(localized, "key", key);
            SetBool(localized, "upperCase", upper);
        }

        return text;
    }

    private static Image Img(string name, Transform parent, Sprite sprite, Color colour)
    {
        RectTransform rect = Rect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Layout(GameObject go, float height)
    {
        LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
    }

    // ---- assets ----------------------------------------------------------------------------------------------------

    private static Sprite TitleSprite()
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TitlePath);
        if (importer.textureType != TextureImporterType.Sprite || importer.maxTextureSize != 2048)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(TitlePath);
    }

    // A soft round shade (white, alpha falling off from the middle), tinted black behind the type.
    private static Sprite ShadeSprite()
    {
        if (!System.IO.File.Exists(ShadePath))
        {
            const int size = 256;
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
            }

            System.IO.File.WriteAllBytes(ShadePath, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(ShadePath);
        }

        TextureImporter shadeImporter = (TextureImporter)AssetImporter.GetAtPath(ShadePath);
        if (shadeImporter.textureType != TextureImporterType.Sprite || shadeImporter.spriteImportMode != SpriteImportMode.Single)
        {
            // Single: a new texture's importer defaults to Multiple, which yields no sprite at the path.
            shadeImporter.textureType = TextureImporterType.Sprite;
            shadeImporter.spriteImportMode = SpriteImportMode.Single;
            shadeImporter.alphaIsTransparency = true;
            shadeImporter.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(ShadePath);
    }

    private static void Fonts()
    {
        light = FontAsset("Oswald-Light");
        regular = FontAsset("Oswald-Regular");
        lightShadow = ShadowMaterial(light);
        regularShadow = ShadowMaterial(regular);
    }

    // A TextMeshPro font asset for one of the Oswald files, made once: dynamic (glyphs added as text needs them),
    // with the Latin letters and accents of the three languages already in the atlas.
    private static TMP_FontAsset FontAsset(string file)
    {
        string path = FontFolder + "/" + file + " SDF.asset";
        TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset != null)
        {
            return asset;
        }

        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "/" + file + ".ttf");
        asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        asset.name = file + " SDF";
        AssetDatabase.CreateAsset(asset, path);
        asset.material.name = file + " SDF Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        asset.atlasTexture.name = file + " SDF Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
        asset.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?¿¡'\"()-+/%<>·«»ÁÀÂÃÉÊÍÓÔÕÚÜÇÑáàâãéêíóôõúüçñ◆", out string _);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    // The same font with a soft dark underlay round each letter: readable over the sky, the lightning and the water.
    private static Material ShadowMaterial(TMP_FontAsset font)
    {
        string path = FontFolder + "/" + font.name + " Shadow.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(font.material) { name = font.name + " Shadow" };
            AssetDatabase.CreateAsset(m, path);
        }

        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.8f));
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.2f);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.5f);
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.7f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---- serialized fields -------------------------------------------------------------------------------------------

    private static void Set(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Object target, string field, Object[] values)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(Object target, string field, string value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string field, bool value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
