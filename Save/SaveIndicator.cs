using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Progresso salvo" in the lower right corner for a couple of seconds after each checkpoint, quiet, like the
// save icons of console games: the player learns the game saves by itself and never needs a save button.
public sealed class SaveIndicator : MonoBehaviour
{
    private const float ShowSeconds = 2.4f;

    private static SaveIndicator instance;

    private CanvasGroup group;
    private TextMeshProUGUI label;
    private Image mark;
    private float shownAt = -100f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        SaveGame.Saved -= Show;
        SaveGame.Saved += Show;
    }

    private static void Show()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("[SaveIndicator]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SaveIndicator>();
            instance.Build();
        }

        instance.label.text = Loc.T("save.saved");
        instance.shownAt = Time.unscaledTime;
    }

    private void Update()
    {
        float age = Time.unscaledTime - shownAt;
        float alpha = age < 0.3f ? age / 0.3f : Mathf.Clamp01((ShowSeconds - age) / 0.6f);
        group.alpha = alpha;
        // A slow turn of the mark while it shows.
        mark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -age * 90f);
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        label = HudFactory.Label("Saved", transform, 22f, TextAlignmentOptions.BottomRight, HudFactory.MutedInk);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(1f, 0f);
        label.rectTransform.anchoredPosition = new Vector2(-92f, 40f);
        label.rectTransform.sizeDelta = new Vector2(500f, 40f);
        label.characterSpacing = 6f;
        DiveHud.Readable(label);

        mark = HudFactory.Image("Mark", transform, HudSprites.RoundedPanel, HudFactory.Accent);
        mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(1f, 0f);
        mark.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        mark.rectTransform.anchoredPosition = new Vector2(-62f, 58f);
        mark.rectTransform.sizeDelta = new Vector2(16f, 16f);
    }
}
