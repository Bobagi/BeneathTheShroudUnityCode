using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

// Runs a cutscene: the PlayableDirector (Timeline) plays the shots, and its cue track (CutsceneCue) asks this for
// what is drawn over the picture: the "presents" line, the game's title, subtitles, the black screen and the
// lightning. Cinema bars top and bottom while it plays.
// The cards follow film practice (notes and sources in Docs/Cinema/titulos.md): small light type with wide tracking
// over a dark part of the frame, long fades, and slow movement while on screen: the "presents" line opens its
// letter spacing as it holds; the title tightens from wide spacing as it fades in (on the lightning) and pushes in
// very slowly.
// Every cutscene can be skipped (house rule): hold Space or Enter for 0.6 s; the hint with the filling bar shows
// as soon as either is pressed. At the end (or skipped) it fires Finished; with nobody listening (the prototype
// scene) it shows an end card where Enter plays it again.
public sealed class CutsceneDirector : MonoBehaviour
{
    [SerializeField]
    private PlayableDirector timeline;

    [SerializeField]
    private LightningStorm storm;

    [SerializeField]
    private TMP_FontAsset font;

    [SerializeField]
    private TMP_FontAsset titleFont;

    [Tooltip("Fraction of the screen height each cinema bar takes (2.39:1 on a 16:9 screen is about 0.12).")]
    [SerializeField]
    private float barHeight = 0.12f;

    private const float SkipHoldSeconds = 0.6f;

    private TextMeshProUGUI presents;
    private TextMeshProUGUI title;
    private TextMeshProUGUI subtitle;
    private CanvasGroup subtitlePlate;
    private Image black;
    private CanvasGroup skipGroup;
    private Image skipFill;
    private CanvasGroup endCard;
    private float skipHeld;
    private bool finished;

    public event System.Action Finished;

    public bool IsPlaying => !finished;

    private void Awake()
    {
        BuildCanvas();
        SetBlack(1f);
    }

    private void Start()
    {
        Cursor.visible = false;
        Play();
    }

    public void Play()
    {
        finished = false;
        endCard.alpha = 0f;
        timeline.time = 0;
        timeline.Play();
    }

    private void Update()
    {
        if (finished)
        {
            Keyboard k = Keyboard.current;
            if (Finished == null && k != null && k.enterKey.wasPressedThisFrame)
            {
                Play();
            }

            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool holding = keyboard != null && (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed);
        skipHeld = holding ? skipHeld + Time.unscaledDeltaTime : Mathf.MoveTowards(skipHeld, 0f, Time.unscaledDeltaTime * 2f);
        skipGroup.alpha = Mathf.MoveTowards(skipGroup.alpha, holding || skipHeld > 0f ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        skipFill.fillAmount = skipHeld / SkipHoldSeconds;
        // The timeline holds its last frame (wrap mode Hold): the end is its time reaching the duration.
        if (skipHeld >= SkipHoldSeconds || timeline.time >= timeline.duration - 0.02)
        {
            Finish();
        }
    }

    private void Finish()
    {
        finished = true;
        skipHeld = 0f;
        skipGroup.alpha = 0f;
        timeline.Stop();
        SetCaption(CutsceneCue.CaptionStyle.Presents, "", 0f);
        SetCaption(CutsceneCue.CaptionStyle.Title, "", 0f);
        SetCaption(CutsceneCue.CaptionStyle.Subtitle, "", 0f);
        SetBlack(1f);
        if (Finished != null)
        {
            Finished();
        }
        else
        {
            endCard.alpha = 1f;
        }
    }

    // ---- what the cue track drives ------------------------------------------------------------------------------

    // progress: 0 to 1 over the card's clip, for its movement.
    public void SetCaption(CutsceneCue.CaptionStyle style, string text, float alpha, float progress = 1f)
    {
        EnsureCanvas();
        switch (style)
        {
            case CutsceneCue.CaptionStyle.Presents:
                Show(presents, text, alpha);
                presents.characterSpacing = Mathf.Lerp(14f, 26f, progress);
                break;
            case CutsceneCue.CaptionStyle.Title:
                Show(title, text, alpha);
                float settle = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress * 2.5f), 3f);
                title.characterSpacing = Mathf.Lerp(34f, 10f, settle);
                title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.035f, progress);
                break;
            default:
                Show(subtitle, text, alpha);
                subtitlePlate.alpha = alpha;
                break;
        }
    }

    public void SetBlack(float alpha)
    {
        EnsureCanvas();
        black.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
    }

    public void Lightning(bool close)
    {
        if (storm != null)
        {
            storm.Strike(close);
        }
    }

    private static void Show(TextMeshProUGUI label, string text, float alpha)
    {
        if (alpha > 0f && label.text != text)
        {
            label.text = text;
        }

        label.alpha = alpha;
    }

    // ---- the overlay ----------------------------------------------------------------------------------------------

    private void EnsureCanvas()
    {
        if (black == null)
        {
            BuildCanvas();
        }
    }

    private void BuildCanvas()
    {
        if (black != null)
        {
            return;
        }

        if (font != null)
        {
            HudFactory.Font = font;
        }

        Canvas canvas = new GameObject("CutsceneOverlay").AddComponent<Canvas>();
        canvas.gameObject.hideFlags = Application.isPlaying ? HideFlags.None : HideFlags.DontSave;
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        Bar("TopBar", canvas.transform, true);
        Bar("BottomBar", canvas.transform, false);

        presents = HudFactory.Label("Presents", canvas.transform, 28f, TextAlignmentOptions.Center, new Color(0.78f, 0.8f, 0.78f));
        HudFactory.Stretch(presents.rectTransform);
        // A little under the centre: over the dark sea of the aerial shot, not over the island on the horizon.
        presents.rectTransform.anchoredPosition = new Vector2(0f, -70f);
        presents.characterSpacing = 18f;
        presents.fontStyle = FontStyles.UpperCase;
        DiveHud.Readable(presents);

        title = HudFactory.Label("Title", canvas.transform, 100f, TextAlignmentOptions.Center, new Color(0.88f, 0.9f, 0.86f));
        if (titleFont != null)
        {
            title.font = titleFont;
        }

        HudFactory.Stretch(title.rectTransform);
        title.characterSpacing = 6f;
        DiveHud.Readable(title);

        RectTransform plate = HudFactory.Rect("SubtitlePlate", canvas.transform);
        plate.anchorMin = new Vector2(0.5f, 0f);
        plate.anchorMax = new Vector2(0.5f, 0f);
        plate.pivot = new Vector2(0.5f, 0f);
        plate.anchoredPosition = new Vector2(0f, 1080f * barHeight + 28f);
        plate.sizeDelta = new Vector2(1400f, 92f);
        plate.gameObject.AddComponent<Image>().color = DiveHud.Plate;
        subtitlePlate = plate.gameObject.AddComponent<CanvasGroup>();
        subtitle = HudFactory.Label("Subtitle", plate, 30f, TextAlignmentOptions.Center, HudFactory.Ink);
        HudFactory.Stretch(subtitle.rectTransform);
        subtitle.margin = new Vector4(28f, 8f, 28f, 8f);
        DiveHud.Readable(subtitle);

        black = HudFactory.Image("Black", canvas.transform, null, Color.black);
        HudFactory.Stretch(black.rectTransform);
        black.raycastTarget = false;

        RectTransform skip = HudFactory.Rect("SkipHint", canvas.transform);
        skip.anchorMin = skip.anchorMax = skip.pivot = new Vector2(1f, 0f);
        skip.anchoredPosition = new Vector2(-48f, 1080f * barHeight + 20f);
        skip.sizeDelta = new Vector2(420f, 52f);
        skip.gameObject.AddComponent<Image>().color = DiveHud.Plate;
        skipGroup = skip.gameObject.AddComponent<CanvasGroup>();
        skipGroup.alpha = 0f;
        TextMeshProUGUI skipText = HudFactory.Label("Text", skip, 22f, TextAlignmentOptions.Center, HudFactory.Ink);
        HudFactory.Stretch(skipText.rectTransform);
        skipText.text = Loc.T("hud.cutscene.skip");
        DiveHud.Readable(skipText);
        skipFill = HudFactory.Image("Fill", skip, null, HudFactory.Accent);
        skipFill.type = Image.Type.Filled;
        skipFill.fillMethod = Image.FillMethod.Horizontal;
        skipFill.fillAmount = 0f;
        skipFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        skipFill.rectTransform.anchorMax = new Vector2(1f, 0f);
        skipFill.rectTransform.pivot = new Vector2(0.5f, 0f);
        skipFill.rectTransform.sizeDelta = new Vector2(0f, 4f);

        RectTransform end = HudFactory.Rect("EndCard", canvas.transform);
        HudFactory.Stretch(end);
        endCard = end.gameObject.AddComponent<CanvasGroup>();
        endCard.alpha = 0f;
        TextMeshProUGUI endText = HudFactory.Label("Text", end, 30f, TextAlignmentOptions.Center, HudFactory.MutedInk);
        HudFactory.Stretch(endText.rectTransform);
        endText.text = Loc.T("hud.cutscene.end");
        DiveHud.Readable(endText);

        SetCaption(CutsceneCue.CaptionStyle.Presents, "", 0f);
        SetCaption(CutsceneCue.CaptionStyle.Title, "", 0f);
        SetCaption(CutsceneCue.CaptionStyle.Subtitle, "", 0f);
    }

    private RectTransform Bar(string name, Transform parent, bool top)
    {
        Image bar = HudFactory.Image(name, parent, null, Color.black);
        RectTransform rect = bar.rectTransform;
        rect.anchorMin = new Vector2(0f, top ? 1f - barHeight : 0f);
        rect.anchorMax = new Vector2(1f, top ? 1f : barHeight);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        bar.raycastTarget = false;
        return rect;
    }
}
