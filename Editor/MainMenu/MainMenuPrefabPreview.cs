using UnityEditor;
using UnityEngine;

// The main menu prefab as it looks in the game, also outside Play (Gustavo 10/10: "quando saio do play a camera
// desliga"): the opening's black (Blackout) stays transparent in the prefab (MainMenu.Start makes it black and fades
// it), only the main page is active (MainMenu.Open turns the others on). Made like this by MainMenuBuilder; prefabs
// built before are brought into line once here, when the editor loads.
[InitializeOnLoad]
public static class MainMenuPrefabPreview
{
    // The logo a fifth darker than its file (TitleLogo).
    public static readonly Color LogoTint = new Color(0.8f, 0.79f, 0.77f, 1f);

    static MainMenuPrefabPreview()
    {
        EditorApplication.delayCall += Fix;
    }

    [MenuItem("Horror/Menu principal/Mostrar o menu fora do Play")]
    public static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuBuilder.PrefabPath) == null)
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(MainMenuBuilder.PrefabPath);
        bool changed = Apply(root);
        if (changed)
        {
            PrefabUtility.SaveAsPrefabAsset(root, MainMenuBuilder.PrefabPath);
            Debug.Log("[MainMenu] prefab ajustado para aparecer fora do Play");
        }

        PrefabUtility.UnloadPrefabContents(root);
    }

    // True if something had to change.
    public static bool Apply(GameObject root)
    {
        bool changed = false;
        Transform blackout = root.transform.Find("Blackout");
        if (blackout != null && blackout.TryGetComponent(out CanvasGroup black) && black.alpha > 0f)
        {
            black.alpha = 0f;
            black.blocksRaycasts = false;
            changed = true;
        }

        Transform pages = root.transform.Find("Pages");
        if (pages != null)
        {
            foreach (Transform page in pages)
            {
                bool main = page.name == "MainPage";
                if (page.gameObject.activeSelf != main)
                {
                    page.gameObject.SetActive(main);
                    changed = true;
                }

                if (main && page.TryGetComponent(out CanvasGroup group) && group.alpha < 1f)
                {
                    group.alpha = 1f;
                    changed = true;
                }
            }
        }

        // No underlines under the entries (Gustavo 10/10: "ficou muito ruim").
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name == "Underline")
            {
                Object.DestroyImmediate(t.gameObject);
                changed = true;
            }
        }

        // Logo still at the file's full brightness, and the old slow glow: toned down once (10/10).
        Transform logo = root.transform.Find("Title/Logo");
        if (logo != null && logo.TryGetComponent(out UnityEngine.UI.Image logoImage) && logoImage.color == Color.white)
        {
            logoImage.color = LogoTint;
            changed = true;
        }

        TitleLogo glow = root.GetComponentInChildren<TitleLogo>(true);
        if (glow != null)
        {
            SerializedObject so = new SerializedObject(glow);
            SerializedProperty breath = so.FindProperty("breath");
            if (breath != null && Mathf.Approximately(breath.floatValue, 0.06f))
            {
                breath.floatValue = 0f;
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
        }

        // "Continuar" only shows with a save (MainMenu.RefreshMain turns it on in Play).
        Transform cont = root.transform.Find("Pages/MainPage/Button_menu.continue");
        if (cont != null && cont.gameObject.activeSelf)
        {
            cont.gameObject.SetActive(false);
            changed = true;
        }

        // Review 10/10: credits top-aligned and compact, option pages narrower (MainMenuBuilder.UI).
        Transform credits = root.transform.Find("Pages/CreditsPage/Credits");
        if (credits != null && credits.TryGetComponent(out TMPro.TMP_Text creditsText) && creditsText.alignment != TMPro.TextAlignmentOptions.Top)
        {
            creditsText.alignment = TMPro.TextAlignmentOptions.Top;
            creditsText.fontSize = 26f;
            creditsText.lineSpacing = -10f;
            UnityEngine.UI.LayoutElement element = credits.GetComponent<UnityEngine.UI.LayoutElement>();
            if (element != null)
            {
                element.preferredHeight = element.minHeight = 470f;
            }

            changed = true;
        }

        foreach (string name in new[] { "ControlsPage", "SoundPage", "GraphicsPage" })
        {
            RectTransform page = root.transform.Find("Pages/" + name) as RectTransform;
            if (page != null && page.sizeDelta.x > MainMenuBuilder.OptionPageWidth + 1f)
            {
                page.sizeDelta = new Vector2(MainMenuBuilder.OptionPageWidth, page.sizeDelta.y);
                changed = true;
            }
        }

        // UI scaled by the screen's height (21:9 overlapped the title at 0.5).
        if (root.TryGetComponent(out UnityEngine.UI.CanvasScaler scaler) && scaler.matchWidthOrHeight < 1f)
        {
            scaler.matchWidthOrHeight = 1f;
            changed = true;
        }

        Transform title = root.transform.Find("Title");
        if (title != null && title.TryGetComponent(out CanvasGroup titleGroup) && titleGroup.alpha < 1f)
        {
            titleGroup.alpha = 1f;
            changed = true;
        }

        return changed;
    }
}
