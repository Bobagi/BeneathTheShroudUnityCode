using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Minimal, reactive HUD in the spirit of horror games: nothing on screen unless it matters.
// Hotbar with item icons that punches on selection and fades out when idle, a name card on switch,
// a pickup prompt pinned to the item in the world, the revolver's cylinder as the ammo counter, a
// dynamic crosshair while aiming, and a controls card that fades away on its own.
public sealed class InventoryHud : MonoBehaviour
{
    [SerializeField]
    private PlayerInventory inventory;

    [SerializeField]
    private Canvas canvas;

    [SerializeField]
    private TMP_FontAsset font;

    [SerializeField]
    private float hotbarIdleFadeSeconds = 4f;

    [SerializeField]
    private float controlsVisibleSeconds = 14f;

    private RectTransform hudRoot;
    private CanvasGroup hotbarGroup;
    private RectTransform[] slotRoots;
    private Image[] slotFrames;
    private Image[] slotIcons;
    private TextMeshProUGUI[] slotNumbers;
    private float[] slotPunch;

    private CanvasGroup nameGroup;
    private TextMeshProUGUI nameLabel;
    private TextMeshProUGUI statusLabel;
    private float nameTimer;

    private RectTransform promptRoot;
    private CanvasGroup promptGroup;
    private TextMeshProUGUI promptLabel;
    private WorldItem promptItem;

    private CanvasGroup controlsGroup;
    private float controlsTimer;
    private bool controlsPinned;

    private float hotbarAwakeTimer;
    private int lastSelected = -1;

    private static readonly Color FrameIdle = new Color(0.08f, 0.07f, 0.06f, 0.55f);
    private static readonly Color FrameSelected = new Color(0.86f, 0.62f, 0.3f, 0.95f);

    private void Start()
    {
        HudFactory.Font = font;
        Build();
        inventory.Changed += OnInventoryChanged;
        controlsTimer = controlsVisibleSeconds;
        OnInventoryChanged();
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.Changed -= OnInventoryChanged;
        }
    }

    private void Build()
    {
        hudRoot = HudFactory.Rect("HorrorHud", canvas.transform);
        HudFactory.Stretch(hudRoot);

        BuildHotbar();
        BuildNameCard();
        BuildPrompt();
        BuildControls();

        CharacterController body = inventory.GetComponent<CharacterController>();
        gameObject.AddComponent<DynamicCrosshair>().Initialize(inventory, body, hudRoot);
        gameObject.AddComponent<AmmoCylinderWidget>().Initialize(inventory, hudRoot);
    }

    private void BuildHotbar()
    {
        RectTransform bar = HudFactory.Rect("Hotbar", hudRoot);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, 26f);
        hotbarGroup = bar.gameObject.AddComponent<CanvasGroup>();

        int count = inventory.SlotCount;
        const float size = 74f;
        const float spacing = 14f;
        float width = count * size + (count - 1) * spacing;
        bar.sizeDelta = new Vector2(width, size + 20f);

        Image wash = HudFactory.Image("Wash", bar, HudSprites.SoftGlow, new Color(0f, 0f, 0f, 0.6f));
        wash.rectTransform.sizeDelta = new Vector2(width + 180f, 170f);
        wash.rectTransform.anchoredPosition = new Vector2(0f, 10f);

        slotRoots = new RectTransform[count];
        slotFrames = new Image[count];
        slotIcons = new Image[count];
        slotNumbers = new TextMeshProUGUI[count];
        slotPunch = new float[count];
        for (int i = 0; i < count; i++)
        {
            RectTransform slot = HudFactory.Rect("Slot" + (i + 1), bar);
            slot.sizeDelta = new Vector2(size, size);
            slot.anchoredPosition = new Vector2(-width * 0.5f + size * 0.5f + i * (size + spacing), 0f);

            Image frame = HudFactory.Image("Frame", slot, HudSprites.Ring, FrameIdle);
            frame.rectTransform.sizeDelta = new Vector2(size, size);
            Image fill = HudFactory.Image("Fill", slot, HudSprites.Circle, new Color(0f, 0f, 0f, 0.45f));
            fill.rectTransform.sizeDelta = new Vector2(size - 8f, size - 8f);

            Image icon = HudFactory.Image("Icon", slot, null, Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.sizeDelta = new Vector2(size - 20f, size - 20f);

            TextMeshProUGUI number = HudFactory.Label("Key", slot, 15f, TextAlignmentOptions.Center, HudFactory.MutedInk);
            number.rectTransform.sizeDelta = new Vector2(24f, 20f);
            number.rectTransform.anchoredPosition = new Vector2(0f, -size * 0.5f - 6f);
            number.text = (i + 1).ToString();

            slotRoots[i] = slot;
            slotFrames[i] = frame;
            slotIcons[i] = icon;
            slotNumbers[i] = number;
        }
    }

    private void BuildNameCard()
    {
        RectTransform card = HudFactory.Rect("ItemName", hudRoot);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0f);
        card.pivot = new Vector2(0.5f, 0f);
        card.sizeDelta = new Vector2(600f, 60f);
        card.anchoredPosition = new Vector2(0f, 132f);
        nameGroup = card.gameObject.AddComponent<CanvasGroup>();
        nameGroup.alpha = 0f;

        nameLabel = HudFactory.Label("Name", card, 30f, TextAlignmentOptions.Center, HudFactory.Ink);
        nameLabel.rectTransform.sizeDelta = new Vector2(600f, 36f);
        nameLabel.rectTransform.anchoredPosition = new Vector2(0f, 14f);
        nameLabel.characterSpacing = 6f;
        HudFactory.Shadow(nameLabel.gameObject);

        statusLabel = HudFactory.Label("Status", card, 17f, TextAlignmentOptions.Center, HudFactory.MutedInk);
        statusLabel.rectTransform.sizeDelta = new Vector2(600f, 22f);
        statusLabel.rectTransform.anchoredPosition = new Vector2(0f, -14f);
    }

    private void BuildPrompt()
    {
        promptRoot = HudFactory.Rect("Prompt", hudRoot);
        promptRoot.anchorMin = promptRoot.anchorMax = Vector2.zero;
        promptRoot.sizeDelta = new Vector2(320f, 40f);
        promptGroup = promptRoot.gameObject.AddComponent<CanvasGroup>();
        promptGroup.alpha = 0f;

        Image marker = HudFactory.Image("Marker", promptRoot, HudSprites.Ring, HudFactory.Accent);
        marker.rectTransform.sizeDelta = new Vector2(16f, 16f);
        marker.rectTransform.anchoredPosition = new Vector2(0f, 0f);

        Image key = HudFactory.Image("KeyCap", promptRoot, HudSprites.Square, new Color(0.92f, 0.88f, 0.78f, 0.95f));
        key.rectTransform.sizeDelta = new Vector2(30f, 30f);
        key.rectTransform.anchoredPosition = new Vector2(34f, 30f);
        HudFactory.Shadow(key.gameObject);
        TextMeshProUGUI keyLabel = HudFactory.Label("Key", key.rectTransform, 20f, TextAlignmentOptions.Center, new Color(0.1f, 0.08f, 0.06f));
        HudFactory.Stretch(keyLabel.rectTransform);
        keyLabel.text = "E";

        promptLabel = HudFactory.Label("Text", promptRoot, 22f, TextAlignmentOptions.MidlineLeft, HudFactory.Ink);
        promptLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
        promptLabel.rectTransform.sizeDelta = new Vector2(300f, 30f);
        promptLabel.rectTransform.anchoredPosition = new Vector2(58f, 30f);
        HudFactory.Shadow(promptLabel.gameObject);
    }

    private void BuildControls()
    {
        RectTransform card = HudFactory.Rect("Controls", hudRoot);
        card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
        card.pivot = new Vector2(0f, 1f);
        card.sizeDelta = new Vector2(460f, 250f);
        card.anchoredPosition = new Vector2(34f, -30f);
        controlsGroup = card.gameObject.AddComponent<CanvasGroup>();

        Image wash = HudFactory.Image("Wash", card, HudSprites.SoftGlow, new Color(0f, 0f, 0f, 0.55f));
        wash.rectTransform.sizeDelta = new Vector2(620f, 380f);
        wash.rectTransform.anchoredPosition = new Vector2(200f, -110f);

        TextMeshProUGUI text = HudFactory.Label("Text", card, 18f, TextAlignmentOptions.TopLeft, HudFactory.Ink);
        HudFactory.Stretch(text.rectTransform);
        text.lineSpacing = 12f;
        text.text =
            "<size=24><color=#d89e4c>ORDENS</color></size>\n"
            + "<color=#9a8f7a>WASD</color>  mover   <color=#9a8f7a>Shift</color>  correr   <color=#9a8f7a>Espaço</color>  pular\n"
            + "<color=#9a8f7a>E</color>  pegar      <color=#9a8f7a>G</color>  soltar no chão\n"
            + "<color=#9a8f7a>1 2 3 / roda</color>  trocar o que está na mão\n"
            + "<color=#9a8f7a>Botão dir.</color>  erguer · mirar   <color=#9a8f7a>Botão esq.</color>  atirar\n"
            + "<color=#9a8f7a>R</color>  recarregar     <color=#9a8f7a>F</color>  lamparina\n"
            + "<size=15><color=#7d7462>H  mostrar / ocultar</color></size>";
    }

    private void OnInventoryChanged()
    {
        if (slotRoots == null)
        {
            return;
        }

        for (int i = 0; i < slotRoots.Length; i++)
        {
            WorldItem item = inventory.GetSlot(i);
            Sprite icon = item != null && item.Definition != null ? item.Definition.icon : null;
            slotIcons[i].sprite = icon;
            slotIcons[i].enabled = icon != null;
        }

        if (inventory.SelectedSlot != lastSelected)
        {
            lastSelected = inventory.SelectedSlot;
            slotPunch[lastSelected] = 1f;
            WorldItem held = inventory.HeldItem;
            nameLabel.text = held != null ? held.DisplayName.ToUpperInvariant() : "MÃOS LIVRES";
            nameTimer = 2.2f;
        }

        hotbarAwakeTimer = hotbarIdleFadeSeconds;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        UpdateHotbar(dt);
        UpdateNameCard(dt);
        UpdatePrompt(dt);
        UpdateControls(dt);
    }

    private void UpdateHotbar(float dt)
    {
        hotbarAwakeTimer -= dt;
        bool awake = hotbarAwakeTimer > 0f || inventory.FocusedItem != null;
        hotbarGroup.alpha = Mathf.MoveTowards(hotbarGroup.alpha, awake ? 1f : 0.25f, dt * (awake ? 6f : 0.8f));

        for (int i = 0; i < slotRoots.Length; i++)
        {
            bool selected = i == inventory.SelectedSlot;
            slotPunch[i] = Mathf.MoveTowards(slotPunch[i], 0f, dt * 3.5f);
            float punch = Mathf.Sin(slotPunch[i] * Mathf.PI) * 0.22f;
            float scale = (selected ? 1.12f : 0.92f) + punch;
            slotRoots[i].localScale = Vector3.Lerp(slotRoots[i].localScale, Vector3.one * scale, 1f - Mathf.Exp(-16f * dt));
            slotFrames[i].color = Color.Lerp(slotFrames[i].color, selected ? FrameSelected : FrameIdle, 1f - Mathf.Exp(-12f * dt));
            slotNumbers[i].color = selected ? HudFactory.Accent : HudFactory.MutedInk;
            slotIcons[i].color = new Color(1f, 1f, 1f, selected ? 1f : 0.55f);
        }
    }

    private void UpdateNameCard(float dt)
    {
        HeldItemBehaviour behaviour = inventory.HeldItem != null ? inventory.HeldItem.GetComponent<HeldItemBehaviour>() : null;
        bool isRevolver = behaviour is RevolverWeapon;
        statusLabel.text = behaviour != null && !isRevolver ? behaviour.StatusText : string.Empty;

        nameTimer -= dt;
        float target = nameTimer > 0f ? 1f : 0f;
        nameGroup.alpha = Mathf.MoveTowards(nameGroup.alpha, target, dt * (target > 0f ? 5f : 1.5f));
        nameGroup.transform.localScale = Vector3.one * (1f + Mathf.Clamp01(nameTimer - 1.9f) * 0.3f);
    }

    private void UpdatePrompt(float dt)
    {
        WorldItem focused = inventory.FocusedItem;
        if (focused != null)
        {
            promptItem = focused;
            promptLabel.text = "Pegar " + focused.DisplayName;
        }

        promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, focused != null ? 1f : 0f, dt * 7f);
        if (promptItem == null || inventory.ViewCamera == null)
        {
            return;
        }

        // Pin the prompt to the item itself, clamped to stay on screen.
        Vector3 screen = inventory.ViewCamera.WorldToScreenPoint(promptItem.InteractionPoint.position);
        if (screen.z < 0f)
        {
            promptGroup.alpha = 0f;
            return;
        }

        RectTransform canvasRect = (RectTransform)canvas.transform;
        float scale = canvasRect.localScale.x;
        Vector2 local = new Vector2(screen.x, screen.y) / Mathf.Max(scale, 0.0001f);
        Vector2 size = canvasRect.sizeDelta;
        local.x = Mathf.Clamp(local.x, 40f, size.x - 360f);
        local.y = Mathf.Clamp(local.y, 60f, size.y - 60f);
        promptRoot.anchoredPosition = Vector2.Lerp(promptRoot.anchoredPosition, local, 1f - Mathf.Exp(-25f * dt));
        float pop = 1f + (1f - promptGroup.alpha) * 0.15f;
        promptRoot.localScale = Vector3.one * pop;
    }

    private void UpdateControls(float dt)
    {
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            controlsPinned = controlsGroup.alpha < 0.5f;
            controlsTimer = controlsPinned ? float.MaxValue : 0f;
        }

        controlsTimer -= dt;
        float target = controlsTimer > 0f ? 1f : 0f;
        controlsGroup.alpha = Mathf.MoveTowards(controlsGroup.alpha, target, dt * (target > 0f ? 4f : 0.6f));
    }
}
