using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The loading screen between scenes (Gustavo, 22/10: "um ícone no canto, com temática do jogo, como nos jogos de
// PlayStation 2"): a darkened still of the hall under the stained glass, and in the corner a cassette tape, the
// demo's recorders, whose reels turn while the game loads and whose tape runs from the left reel to the right one
// as the loading progresses; under it the word, breathing, and a thin bar. Built from code and the HUD's generated
// sprites (no prefab to break); the background comes from Art/UI/Resources/Loading/loading_bg.jpg.
// SceneFlow creates one, sets Progress (0 to 1) and Alpha, and keeps it on top of everything (sorting order 1000).
public sealed class LoadingScreen : MonoBehaviour
{
    private const float ReelSpeed = -150f;

    private CanvasGroup group;
    private Image black;
    private Image photo;
    private RectTransform leftReel, rightReel;
    private RectTransform leftTape, rightTape;
    private TextMeshProUGUI label;
    private Image barFill;
    private float progress;
    private float shownProgress;

    public float Progress
    {
        get => progress;
        set => progress = Mathf.Clamp01(value);
    }

    public float Alpha
    {
        get => group.alpha;
        set => group.alpha = value;
    }

    public bool BlocksRaycasts
    {
        set => group.blocksRaycasts = value;
    }

    public static LoadingScreen Create(GameObject host)
    {
        LoadingScreen screen = host.AddComponent<LoadingScreen>();
        screen.Build();
        return screen;
    }

    public void SetLabel(string text)
    {
        label.text = text;
    }

    private void Build()
    {
        // Explicit null checks: Unity's fake-null components defeat the ?? operator (the canvas was never added).
        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 0f;

        black = HudFactory.Image("Black", transform, HudSprites.Square, Color.black);
        HudFactory.Stretch(black.rectTransform);
        black.raycastTarget = true;

        // The still, cropped to the screen (the image is 16:9; on other shapes it is scaled to cover).
        Texture2D still = Resources.Load<Texture2D>("Loading/loading_bg");
        if (still != null)
        {
            photo = HudFactory.Image("Still", transform, Sprite.Create(still, new Rect(0f, 0f, still.width, still.height), new Vector2(0.5f, 0.5f), 100f), Color.white);
            HudFactory.Stretch(photo.rectTransform);
            AspectRatioFitter fitter = photo.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = still.width / (float)still.height;
        }

        // A dark wash at the bottom so the corner reads on any photo.
        Image wash = HudFactory.Image("Wash", transform, HudSprites.VerticalFade, new Color(0f, 0f, 0f, 0.75f));
        wash.rectTransform.anchorMin = new Vector2(0f, 0f);
        wash.rectTransform.anchorMax = new Vector2(1f, 0f);
        wash.rectTransform.pivot = new Vector2(0.5f, 0f);
        wash.rectTransform.anchoredPosition = Vector2.zero;
        wash.rectTransform.sizeDelta = new Vector2(0f, 360f);

        // The cassette in the bottom right corner.
        RectTransform corner = HudFactory.Rect("Corner", transform);
        corner.anchorMin = corner.anchorMax = corner.pivot = new Vector2(1f, 0f);
        corner.anchoredPosition = new Vector2(-72f, 64f);
        corner.sizeDelta = new Vector2(236f, 150f);
        Cassette(corner);

        // "CARREGANDO" to the left of the tape, and the bar under both.
        label = HudFactory.Label("Loading", transform, 24f, TextAlignmentOptions.BottomRight, HudFactory.MutedInk);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(1f, 0f);
        label.rectTransform.anchoredPosition = new Vector2(-330f, 96f);
        label.rectTransform.sizeDelta = new Vector2(600f, 40f);
        label.characterSpacing = 14f;
        HudFactory.Shadow(label.gameObject);

        Image barBack = HudFactory.Image("BarBack", transform, HudSprites.Square, new Color(1f, 1f, 1f, 0.12f));
        barBack.rectTransform.anchorMin = barBack.rectTransform.anchorMax = barBack.rectTransform.pivot = new Vector2(1f, 0f);
        barBack.rectTransform.anchoredPosition = new Vector2(-72f, 44f);
        barBack.rectTransform.sizeDelta = new Vector2(560f, 3f);
        barFill = HudFactory.Image("BarFill", barBack.transform, HudSprites.Square, HudFactory.Accent);
        barFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        barFill.rectTransform.anchoredPosition = Vector2.zero;
        barFill.rectTransform.sizeDelta = new Vector2(0f, 0f);
    }

    // A compact cassette seen from the front: dark shell with a lighter label across the top, a window in the middle
    // showing the two reels and the tape packs, five guide holes along the bottom edge.
    private void Cassette(RectTransform at)
    {
        Color shell = new Color(0.13f, 0.11f, 0.09f, 1f);
        Color shellEdge = new Color(0.32f, 0.27f, 0.2f, 1f);
        Color paper = new Color(0.62f, 0.55f, 0.42f, 1f);
        Color tape = new Color(0.36f, 0.22f, 0.12f, 1f);
        Color hub = HudFactory.Ink;

        Image edge = Panel("Edge", at, shellEdge, Vector2.zero, new Vector2(236f, 150f));
        _ = edge;
        Panel("Shell", at, shell, Vector2.zero, new Vector2(230f, 144f));
        Panel("Label", at, paper, new Vector2(0f, 34f), new Vector2(206f, 52f));
        Panel("LabelLine", at, new Color(0.2f, 0.16f, 0.12f, 0.6f), new Vector2(0f, 42f), new Vector2(150f, 3f));
        Panel("LabelLine2", at, new Color(0.2f, 0.16f, 0.12f, 0.45f), new Vector2(-14f, 28f), new Vector2(110f, 3f));
        Panel("Window", at, new Color(0.05f, 0.045f, 0.04f, 1f), new Vector2(0f, -8f), new Vector2(150f, 50f));
        // The tape packs (grow and shrink with the progress) behind the hubs.
        leftTape = Disc("TapeL", at, tape, new Vector2(-44f, -8f), 60f).rectTransform;
        rightTape = Disc("TapeR", at, tape, new Vector2(44f, -8f), 22f).rectTransform;
        leftReel = Reel("ReelL", at, hub, new Vector2(-44f, -8f));
        rightReel = Reel("ReelR", at, hub, new Vector2(44f, -8f));
        // The tape path between the reels, seen in the window.
        Panel("TapeRun", at, tape, new Vector2(0f, -8f), new Vector2(30f, 4f));
        // Guide holes along the bottom.
        foreach (float x in new[] { -70f, -36f, 0f, 36f, 70f })
        {
            Disc("Hole", at, new Color(0.05f, 0.045f, 0.04f, 1f), new Vector2(x, -56f), 9f);
        }
    }

    private static Image Panel(string name, RectTransform parent, Color color, Vector2 at, Vector2 size)
    {
        Image image = HudFactory.Image(name, parent, HudSprites.RoundedPanel, color);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1.6f;
        Place(image.rectTransform, at, size);
        return image;
    }

    private static Image Disc(string name, RectTransform parent, Color color, Vector2 at, float diameter)
    {
        Image image = HudFactory.Image(name, parent, HudSprites.Circle, color);
        Place(image.rectTransform, at, new Vector2(diameter, diameter));
        return image;
    }

    // A hub: a ring with three spokes, turned each frame.
    private static RectTransform Reel(string name, RectTransform parent, Color color, Vector2 at)
    {
        Image ring = HudFactory.Image(name, parent, HudSprites.Ring, color);
        Place(ring.rectTransform, at, new Vector2(34f, 34f));
        for (int i = 0; i < 3; i++)
        {
            Image spoke = HudFactory.Image("Spoke", ring.transform, HudSprites.Square, color);
            Place(spoke.rectTransform, Vector2.zero, new Vector2(4f, 26f));
            spoke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
        }

        return ring.rectTransform;
    }

    private static void Place(RectTransform rect, Vector2 at, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = at;
        rect.sizeDelta = size;
    }

    private void Update()
    {
        if (group.alpha <= 0.001f)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float turn = ReelSpeed * dt;
        leftReel.Rotate(0f, 0f, turn);
        rightReel.Rotate(0f, 0f, turn);
        shownProgress = Mathf.MoveTowards(shownProgress, progress, dt * 0.8f);
        float left = Mathf.Lerp(60f, 22f, shownProgress);
        float right = Mathf.Lerp(22f, 60f, shownProgress);
        leftTape.sizeDelta = new Vector2(left, left);
        rightTape.sizeDelta = new Vector2(right, right);
        barFill.rectTransform.sizeDelta = new Vector2(560f * shownProgress, 0f);
        label.alpha = 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 2.6f);
    }
}
