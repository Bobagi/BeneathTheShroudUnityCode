using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Test-scene panel for the player's body skins (BodySkins): a button that swaps the body on the spot (also T),
// Alt to free the mouse for clicking (the player stands still meanwhile), and the outside view of the body.
// Same HUD language as the game: dark plates, keycaps, underlaid text.
public sealed class SkinSwapPanel : MonoBehaviour
{
    [SerializeField]
    private BodySkins skins;

    [SerializeField]
    private FirstPersonController player;

    [SerializeField]
    private DiveGear gear;

    [SerializeField]
    private OutsideView outside;

    [SerializeField]
    private DiveHud hud;

    [SerializeField]
    private Respirator respirator;

    [SerializeField]
    private TMP_FontAsset font;

    private TextMeshProUGUI skinLabel;
    private TextMeshProUGUI mouseLabel;
    private TextMeshProUGUI handsLabel;
    private RecordedHandsTest recordedHands;
    private RecordedBodyTest recordedBody;
    private TextMeshProUGUI idleLabel;
    private bool idleShown;
    private bool handsShown;
    private bool mouseFree;

    private void Start()
    {
        if (font != null)
        {
            HudFactory.Font = font;
        }

        Build();
        // The game HUD starts on black (the house's intro fades it in).
        if (hud != null)
        {
            hud.FadeTo(0f, 0.6f);
        }

        skins.Changed += Refresh;
        Refresh(skins);
        // The torch in hand from the start, so the gloves are seen holding it (F switches it on).
        if (gear != null)
        {
            gear.GiveTorch();
            gear.GiveBatteries();
        }
    }

    private void OnDestroy()
    {
        if (skins != null)
        {
            skins.Changed -= Refresh;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.tKey.wasPressedThisFrame)
        {
            skins.Next();
        }
        if (recordedHands != null && recordedHands.IsOn != handsShown)
        {
            RefreshHands();
        }
        if (recordedBody != null && recordedBody.IdleOn != idleShown)
        {
            RefreshIdle();
        }

        // Visor tests: water on the glass, and a blow that cracks it (full crack starts over).
        MaskVisor visor = Camera.main != null ? Camera.main.GetComponent<MaskVisor>() : null;
        if (visor != null && keyboard.vKey.wasPressedThisFrame)
        {
            visor.Splash();
        }

        if (visor != null && keyboard.bKey.wasPressedThisFrame)
        {
            if (visor.CrackLevel >= 0.99f)
            {
                visor.ResetCrack();
            }
            else
            {
                visor.Crack(0.3f);
            }
        }

        if (keyboard.leftAltKey.wasPressedThisFrame || keyboard.rightAltKey.wasPressedThisFrame)
        {
            SetMouseFree(!mouseFree);
        }
    }

    private void SetMouseFree(bool free)
    {
        mouseFree = free;
        // The controller locks the cursor again when it is re-enabled.
        player.enabled = !free;
        if (free)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        mouseLabel.text = free ? "prender o mouse e voltar a andar" : "soltar o mouse para clicar";
    }

    private void RefreshHands()
    {
        if (handsLabel != null && recordedHands != null)
        {
            handsShown = recordedHands.IsOn;
            handsLabel.text = "Mãos gravadas (vídeo)  ·  agora: " + (recordedHands.IsOn ? "tocando" : "desligadas, jogo normal");
        }
    }

    private void RefreshIdle()
    {
        if (idleLabel != null && recordedBody != null)
        {
            idleShown = recordedBody.IdleOn;
            idleLabel.text = "idle gravado (vídeo, parado)  ·  agora: " + (idleShown ? "ligado" : "desligado, idle do jogo");
        }
    }

    private void Refresh(BodySkins changed)
    {
        skinLabel.text = "Trocar corpo  ·  agora: " + changed.CurrentName;
        // The suit comes with its mask: on with the suit, off with the bare body (MaskVisor shows it).
        if (respirator != null)
        {
            if (changed.CurrentIsBulky && !respirator.HasMask)
            {
                respirator.GiveMask(0);
            }
            else if (!changed.CurrentIsBulky && respirator.HasMask)
            {
                respirator.RemoveMask();
            }
        }
    }

    private void Build()
    {
        Canvas canvas = new GameObject("SkinSwapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform column = HudFactory.Rect("Controls", canvas.transform);
        column.anchorMin = column.anchorMax = column.pivot = new Vector2(0f, 1f);
        column.anchoredPosition = new Vector2(40f, -40f);
        VerticalLayoutGroup stack = column.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.spacing = 12f;
        stack.childAlignment = TextAnchor.UpperLeft;
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = false;
        stack.childForceExpandHeight = false;
        ContentSizeFitter fit = column.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Image swap = Row(column, "T", 30f, out skinLabel);
        swap.raycastTarget = true;
        Button button = swap.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.6f, 1.45f, 1.2f, 1f);
        colors.pressedColor = new Color(2f, 1.8f, 1.4f, 1f);
        colors.selectedColor = Color.white;
        colors.colorMultiplier = 2f;
        button.colors = colors;
        button.targetGraphic = swap;
        button.onClick.AddListener(skins.Next);

        // Recorded hands (the video clip) on and off: a button, like the body swap, or H.
        recordedHands = FindAnyObjectByType<RecordedHandsTest>();
        if (recordedHands != null)
        {
            Image hands = Row(column, "H", 30f, out handsLabel);
            hands.raycastTarget = true;
            Button handsButton = hands.gameObject.AddComponent<Button>();
            handsButton.colors = colors;
            handsButton.targetGraphic = hands;
            handsButton.onClick.AddListener(() => { recordedHands.Toggle(); RefreshHands(); });
            RefreshHands();
        }
        // Whole body recorded from video: the cough (K) and the filmed idle (I), buttons like the others.
        recordedBody = FindAnyObjectByType<RecordedBodyTest>();
        if (recordedBody != null)
        {
            Image cough = Row(column, "K", 24f, out TextMeshProUGUI coughLabel);
            coughLabel.text = "tossir (gravado do vídeo; tira o revólver da mão)";
            cough.raycastTarget = true;
            Button coughButton = cough.gameObject.AddComponent<Button>();
            coughButton.colors = colors;
            coughButton.targetGraphic = cough;
            coughButton.onClick.AddListener(recordedBody.Cough);
            Image idle = Row(column, "I", 24f, out idleLabel);
            idle.raycastTarget = true;
            Button idleButton = idle.gameObject.AddComponent<Button>();
            idleButton.colors = colors;
            idleButton.targetGraphic = idle;
            idleButton.onClick.AddListener(() => { recordedBody.ToggleIdle(); RefreshIdle(); });
            RefreshIdle();
        }
        Row(column, "Alt", 24f, out mouseLabel);
        mouseLabel.text = "soltar o mouse para clicar";
        Row(column, "1 2", 24f, out TextMeshProUGUI hand);
        hand.text = "lanterna na mão  ·  revólver nas mãos";
        Row(column, "Mouse", 24f, out TextMeshProUGUI shoot);
        shoot.text = "direito mira (lanterna: foca)  ·  esquerdo atira  ·  R recarrega";
        Row(column, "Tab", 24f, out TextMeshProUGUI backpack);
        backpack.text = "mochila (número do item usa)  ·  E pega o item no chão";
        Row(column, "F", 24f, out TextMeshProUGUI torch);
        torch.text = "lanterna";
        Row(column, "V", 24f, out TextMeshProUGUI splash);
        splash.text = "molhar o visor da máscara";
        Row(column, "B", 24f, out TextMeshProUGUI knock);
        knock.text = "trincar o visor (de novo quando todo trincado: volta)";
        Row(column, "WASD", 24f, out TextMeshProUGUI walk);
        walk.text = "andar  ·  Shift corre  ·  Ctrl agacha";

        if (outside != null)
        {
            Image frame = HudFactory.Image("OutsideView", canvas.transform, HudSprites.RoundedPanel, DiveHud.Plate);
            frame.type = Image.Type.Sliced;
            RectTransform rect = frame.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-40f, 40f);
            rect.sizeDelta = new Vector2(396f, 560f);
            RawImage picture = new GameObject("Picture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            picture.transform.SetParent(frame.transform, false);
            picture.texture = outside.Target;
            picture.raycastTarget = false;
            RectTransform pr = picture.rectTransform;
            pr.anchorMin = Vector2.zero;
            pr.anchorMax = Vector2.one;
            pr.offsetMin = new Vector2(8f, 8f);
            pr.offsetMax = new Vector2(-8f, -46f);
            TextMeshProUGUI title = HudFactory.Label("Title", frame.transform, 24f, TextAlignmentOptions.Center, HudFactory.Ink);
            DiveHud.Readable(title);
            title.text = "Vista de fora";
            RectTransform tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = Vector2.one;
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(0f, 44f);
            tr.anchoredPosition = Vector2.zero;
        }
    }

    // One plate: keycap + label.
    private static Image Row(Transform parent, string key, float size, out TextMeshProUGUI label)
    {
        Image plate = HudFactory.Image("Row_" + key, parent, HudSprites.RoundedPanel, DiveHud.Plate);
        plate.type = Image.Type.Sliced;
        HorizontalLayoutGroup layout = plate.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(16, 22, 9, 9);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        DiveHud.Keycap(plate.transform, key, size * 1.25f);
        label = HudFactory.Label("Label", plate.transform, size, TextAlignmentOptions.MidlineLeft, HudFactory.Ink);
        DiveHud.Readable(label);
        return plate;
    }
}
