using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The backpack on screen (Gustavo, 06/10: "uma UI do inventário", 07/10: "você fez uma lista"): a grid of slots like
// any game inventory. Top: the hand, two equipment slots (torch, revolver) with the one in use lit. Below: the
// backpack, 4 x 2 slots with the item's icon, count and name; the chosen slot lit, its description under the grid.
// Keys: I opens and closes (Tab is the diary of documents, PlayerItems/MansionHud), a slot's number or a click uses it (1 and 2 switch the hand while it is closed), the
// mouse is free while it is open. A strip at the bottom left shows the hand and the Tab hint while it is closed.
// Same HUD language as the game: dark plates, keycaps, underlaid text. Built at run time by the builders.
public sealed class InventoryHudPanel : MonoBehaviour
{
    [SerializeField]
    private Inventory inventory;

    [SerializeField]
    private DiveHud hud;

    [SerializeField]
    private FirstPersonController controller;

    [SerializeField]
    private TMP_FontAsset font;

    private const int Columns = 4;
    private const float Cell = 112f;
    private static readonly Color SlotFace = new Color(0.1f, 0.11f, 0.1f, 0.85f);
    private static readonly Color SlotLit = new Color(0.86f, 0.62f, 0.3f, 0.55f);
    private static readonly Color SlotEmpty = new Color(0.08f, 0.08f, 0.08f, 0.5f);

    private sealed class Slot
    {
        public Image frame;
        public Image lit;
        public Image icon;
        public TextMeshProUGUI count;
        public TextMeshProUGUI name;
        public Button button;
    }

    private RectTransform panel;
    private Image strip;
    private TextMeshProUGUI handLabel;
    private TextMeshProUGUI handTitle;
    private TextMeshProUGUI bagTitle;
    private TextMeshProUGUI title;
    private TextMeshProUGUI description;
    private readonly Slot[] handSlots = new Slot[2];
    private readonly List<Slot> slots = new List<Slot>();
    private int selected = -1;
    private bool open;
    private bool controllerWasEnabled;

    public bool IsOpen => open;

    private void Start()
    {
        if (font != null)
        {
            HudFactory.Font = font;
        }
        Build();
        inventory.Changed += Refresh;
        Loc.Changed += OnLanguage;
        Refresh();
    }

    private void OnDestroy()
    {
        Loc.Changed -= OnLanguage;
        if (inventory != null)
        {
            inventory.Changed -= Refresh;
        }
    }

    // The fixed labels follow the language; the slots are rebuilt by Refresh.
    private void OnLanguage()
    {
        if (title == null)
        {
            return;
        }

        title.text = Loc.T("hud.bag.title").ToUpperInvariant();
        bagTitle.text = Loc.T("hud.bag.help");
        Refresh();
    }

    private void Update()
    {
        // The strip goes with the end card.
        if (hud != null && strip != null && strip.gameObject.activeSelf == hud.IsEndCardShown)
        {
            strip.gameObject.SetActive(!hud.IsEndCardShown);
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || Time.timeScale == 0f)
        {
            return;
        }
        if (keyboard.iKey.wasPressedThisFrame && (open || (controller == null || !controller.InputLocked)))
        {
            SetOpen(!open);
        }
        if (!open)
        {
            return;
        }
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            SetOpen(false);
            return;
        }
        Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        for (int i = 0; i < digits.Length && i < inventory.Slots; i++)
        {
            if (keyboard[digits[i]].wasPressedThisFrame)
            {
                UseSlot(i);
            }
        }
        if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
        {
            int step = keyboard.leftArrowKey.wasPressedThisFrame ? -1 : keyboard.rightArrowKey.wasPressedThisFrame ? 1 : keyboard.upArrowKey.wasPressedThisFrame ? -Columns : Columns;
            Select(Mathf.Clamp(selected + step, 0, Mathf.Max(0, inventory.Stacks.Count - 1)));
        }
        if (keyboard.enterKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
        {
            UseSlot(selected);
        }
    }

    private void SetOpen(bool value)
    {
        if (open == value)
        {
            return;
        }
        open = value;
        panel.gameObject.SetActive(open);
        // The mouse is free over the backpack; the player stands still meanwhile (the controller locks the cursor
        // again when it comes back).
        if (controller != null)
        {
            if (open)
            {
                controllerWasEnabled = controller.enabled;
                controller.enabled = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                controller.enabled = controllerWasEnabled;
            }
        }
        if (open && selected < 0 && inventory.Stacks.Count > 0)
        {
            selected = 0;
        }
        Refresh();
    }

    private void UseSlot(int index)
    {
        if (index < 0 || index >= inventory.Stacks.Count)
        {
            return;
        }
        selected = index;
        string result = inventory.Use(index);
        if (result != null && hud != null)
        {
            hud.ShowMessage(result, 2.5f);
        }
        if (selected >= inventory.Stacks.Count)
        {
            selected = inventory.Stacks.Count - 1;
        }
        Refresh();
    }

    private void Select(int index)
    {
        selected = index;
        Refresh();
    }

    private void Build()
    {
        Canvas canvas = new GameObject("InventoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Bottom left, closed: the hand and the hint.
        strip = HudFactory.Image("HandStrip", canvas.transform, HudSprites.RoundedPanel, DiveHud.Plate);
        strip.type = Image.Type.Sliced;
        RectTransform stripRect = strip.rectTransform;
        stripRect.anchorMin = stripRect.anchorMax = stripRect.pivot = new Vector2(0f, 0f);
        stripRect.anchoredPosition = new Vector2(40f, 76f);
        Horizontal(strip.gameObject, 10f, new RectOffset(16, 18, 8, 8));
        Fit(strip.gameObject);
        DiveHud.Keycap(strip.transform, "I", 26f);
        handLabel = HudFactory.Label("Hand", strip.transform, 24f, TextAlignmentOptions.Left, HudFactory.Ink);
        DiveHud.Readable(handLabel);

        // The backpack, centred.
        Image back = HudFactory.Image("Backpack", canvas.transform, HudSprites.RoundedPanel, new Color(0.02f, 0.03f, 0.03f, 0.9f));
        back.type = Image.Type.Sliced;
        back.raycastTarget = true;
        panel = back.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, -20f);
        VerticalLayoutGroup layout = Vertical(back.gameObject, 14f, new RectOffset(28, 28, 20, 22));
        layout.childAlignment = TextAnchor.UpperLeft;
        Fit(back.gameObject);

        title = HudFactory.Label("Title", back.transform, 32f, TextAlignmentOptions.Left, HudFactory.Accent);
        title.text = Loc.T("hud.bag.title").ToUpperInvariant();
        DiveHud.Readable(title);

        // The hand: two equipment slots.
        handTitle = HudFactory.Label("HandTitle", back.transform, 20f, TextAlignmentOptions.Left, HudFactory.MutedInk);
        DiveHud.Readable(handTitle);
        RectTransform handRow = HudFactory.Rect("HandRow", back.transform);
        Horizontal(handRow.gameObject, 12f, new RectOffset());
        handSlots[0] = MakeSlot(handRow, "1", () => inventory.Equip(inventory.InHand == Inventory.Hand.Torch ? Inventory.Hand.None : Inventory.Hand.Torch, false));
        handSlots[1] = MakeSlot(handRow, "2", () => inventory.Equip(inventory.InHand == Inventory.Hand.Revolver ? Inventory.Hand.Torch : Inventory.Hand.Revolver, false));

        bagTitle = HudFactory.Label("BagTitle", back.transform, 20f, TextAlignmentOptions.Left, HudFactory.MutedInk);
        bagTitle.text = Loc.T("hud.bag.help");
        DiveHud.Readable(bagTitle);
        RectTransform grid = HudFactory.Rect("Grid", back.transform);
        GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(Cell, Cell + 30f);
        gridLayout.spacing = new Vector2(12f, 10f);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = Columns;
        LayoutElement gridSize = grid.gameObject.AddComponent<LayoutElement>();
        int rows = Mathf.CeilToInt(inventory.Slots / (float)Columns);
        gridSize.preferredWidth = Columns * Cell + (Columns - 1) * 12f;
        gridSize.preferredHeight = rows * (Cell + 30f) + (rows - 1) * 10f;
        for (int i = 0; i < inventory.Slots; i++)
        {
            int index = i;
            slots.Add(MakeSlot(grid, (i + 1).ToString(), () => UseSlot(index)));
        }

        description = HudFactory.Label("Description", back.transform, 22f, TextAlignmentOptions.Left, HudFactory.Ink);
        description.textWrappingMode = TextWrappingModes.Normal;
        LayoutElement descriptionSize = description.gameObject.AddComponent<LayoutElement>();
        descriptionSize.preferredWidth = Columns * Cell + (Columns - 1) * 12f;
        descriptionSize.minHeight = 56f;
        DiveHud.Readable(description);

        panel.gameObject.SetActive(false);
    }

    // A square slot: plate, highlight, icon, count badge, the key, and the name under it.
    private Slot MakeSlot(Transform parent, string key, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform cell = HudFactory.Rect("Slot" + key, parent);
        LayoutElement size = cell.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = Cell;
        size.preferredHeight = Cell + 30f;
        Image frame = HudFactory.Image("Frame", cell, HudSprites.RoundedPanel, SlotFace);
        frame.type = Image.Type.Sliced;
        frame.raycastTarget = true;
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = new Vector2(0f, 1f);
        frameRect.anchorMax = new Vector2(1f, 1f);
        frameRect.pivot = new Vector2(0.5f, 1f);
        frameRect.anchoredPosition = Vector2.zero;
        frameRect.sizeDelta = new Vector2(0f, Cell);
        Image lit = HudFactory.Image("Lit", frame.transform, HudSprites.RoundedPanel, SlotLit);
        lit.type = Image.Type.Sliced;
        HudFactory.Stretch(lit.rectTransform);
        lit.rectTransform.offsetMin = new Vector2(-4f, -4f);
        lit.rectTransform.offsetMax = new Vector2(4f, 4f);
        lit.enabled = false;
        Image icon = HudFactory.Image("Icon", frame.transform, null, Color.white);
        icon.preserveAspect = true;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 4f);
        iconRect.sizeDelta = new Vector2(Cell * 0.62f, Cell * 0.62f);
        icon.enabled = false;
        TextMeshProUGUI keycap = DiveHud.Keycap(frame.transform, key, 22f);
        RectTransform capRect = keycap.transform.parent.GetComponent<RectTransform>();
        capRect.anchorMin = capRect.anchorMax = new Vector2(0f, 1f);
        capRect.pivot = new Vector2(0f, 1f);
        capRect.anchoredPosition = new Vector2(6f, -6f);
        capRect.sizeDelta = new Vector2(30f, 24f);
        TextMeshProUGUI count = HudFactory.Label("Count", frame.transform, 22f, TextAlignmentOptions.BottomRight, HudFactory.Ink);
        count.fontStyle = FontStyles.Bold;
        RectTransform countRect = count.rectTransform;
        countRect.anchorMin = countRect.anchorMax = new Vector2(1f, 0f);
        countRect.pivot = new Vector2(1f, 0f);
        countRect.anchoredPosition = new Vector2(-8f, 6f);
        countRect.sizeDelta = new Vector2(60f, 26f);
        DiveHud.Readable(count);
        TextMeshProUGUI name = HudFactory.Label("Name", cell, 18f, TextAlignmentOptions.Center, HudFactory.Ink);
        RectTransform nameRect = name.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0f);
        nameRect.pivot = new Vector2(0.5f, 0f);
        nameRect.anchoredPosition = Vector2.zero;
        nameRect.sizeDelta = new Vector2(0f, 28f);
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.overflowMode = TextOverflowModes.Ellipsis;
        DiveHud.Readable(name);
        Button button = frame.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.5f, 1.4f, 1.2f, 1f);
        colors.pressedColor = new Color(2f, 1.8f, 1.4f, 1f);
        colors.selectedColor = Color.white;
        colors.colorMultiplier = 2f;
        button.colors = colors;
        button.targetGraphic = frame;
        button.onClick.AddListener(onClick);
        return new Slot { frame = frame, lit = lit, icon = icon, count = count, name = name, button = button };
    }

    private static HorizontalLayoutGroup Horizontal(GameObject target, float spacing, RectOffset padding)
    {
        HorizontalLayoutGroup layout = target.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return layout;
    }

    private static VerticalLayoutGroup Vertical(GameObject target, float spacing, RectOffset padding)
    {
        VerticalLayoutGroup layout = target.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return layout;
    }

    private static void Fit(GameObject target)
    {
        ContentSizeFitter fit = target.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void Refresh()
    {
        if (handLabel == null)
        {
            return;
        }
        string hand = Loc.T(inventory.InHand == Inventory.Hand.Revolver ? "hud.hand.revolver" : inventory.InHand == Inventory.Hand.Torch ? "hud.hand.torch" : "hud.hand.free");
        handLabel.text = Loc.F("hud.bag.strip", inventory.Stacks.Count, inventory.Slots, hand);
        if (!open)
        {
            return;
        }
        handTitle.text = Loc.F("hud.bag.hand", hand);
        FillSlot(handSlots[0], inventory.Owns(Inventory.Hand.Torch) ? ItemIcons.For("Torch", new Color(0.75f, 0.75f, 0.7f)) : null, Loc.T("item.torch.name"), 0, inventory.InHand == Inventory.Hand.Torch, inventory.Owns(Inventory.Hand.Torch));
        FillSlot(handSlots[1], inventory.Owns(Inventory.Hand.Revolver) ? ItemIcons.For("Revolver", new Color(0.55f, 0.55f, 0.6f)) : null, Loc.T("item.revolver.name"), 0, inventory.InHand == Inventory.Hand.Revolver, inventory.Owns(Inventory.Hand.Revolver));
        IReadOnlyList<Inventory.Stack> stacks = inventory.Stacks;
        for (int i = 0; i < slots.Count; i++)
        {
            if (i < stacks.Count)
            {
                FillSlot(slots[i], ItemIcons.For(stacks[i].item), stacks[i].item.Name, stacks[i].count, i == selected, true);
            }
            else
            {
                FillSlot(slots[i], null, "", 0, false, false);
            }
        }
        description.text = selected >= 0 && selected < stacks.Count
            ? "<color=#DBA04D>" + stacks[selected].item.Name + "</color>  " + stacks[selected].item.Description
            : stacks.Count == 0 ? Loc.T("hud.bag.empty") : "";
    }

    private static void FillSlot(Slot slot, Sprite icon, string name, int count, bool lit, bool filled)
    {
        slot.frame.color = filled ? SlotFace : SlotEmpty;
        slot.lit.enabled = lit;
        slot.icon.enabled = icon != null;
        slot.icon.sprite = icon;
        slot.count.text = count > 1 ? "×" + count : "";
        slot.name.text = name;
        slot.button.interactable = filled;
    }
}
