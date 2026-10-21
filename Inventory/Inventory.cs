using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// What the player carries and what is in the hands (Gustavo, 06/10: "uma UI mostrando os itens que o usuário tem e
// pode carregar, o que ele tem equipado"). A few slots of stackable items (InventoryItem), used from the backpack
// (InventoryHudPanel: Tab, then the slot's number), and the hand: the dive torch or the revolver (1 and 2), a state
// of the first person arms rig (FirstPersonArms.SetHands). With the revolver in both hands the torch rides the chest
// (ChestTorchMount); with the torch in hand the revolver is away.
// Story items (keys, the suit, recordings) stay in PlayerItems; this is the consumables and tools.
// In the house (17/10): Erick arrives with the revolver holstered (startingTools = Revolver, startingHand = None,
// 2 draws it); the torch is handed in by the story (GiveTool when "lanterna" is found). With the attic suit on, the
// torch is clipped to the chest for good (TorchOnChest: 1 no longer takes it in hand, the hands are free or on the gun).
public sealed class Inventory : MonoBehaviour
{
    public enum Hand
    {
        None,
        Torch,
        Revolver,
    }

    [System.Serializable]
    public sealed class Stack
    {
        public InventoryItem item;
        public int count;
    }

    [SerializeField]
    private int slots = 8;

    [SerializeField]
    private DiveHud hud;

    [SerializeField]
    private DiveGear gear;

    [SerializeField]
    private AirSupply air;

    [SerializeField]
    private Respirator respirator;

    [SerializeField]
    private Revolver revolver;

    [SerializeField]
    private ChestTorchMount chestMount;

    [Tooltip("The first person arms rig: what is in the hands is one of its states.")]
    [SerializeField]
    private FirstPersonArms arms;

    [Tooltip("Tools the player starts with (the torch is found in the house; the test scene gives both).")]
    [SerializeField]
    private Hand[] startingTools = new Hand[0];

    [SerializeField]
    private Hand startingHand = Hand.None;

    private readonly List<Stack> stacks = new List<Stack>();
    private readonly HashSet<Hand> tools = new HashSet<Hand>();

    public IReadOnlyList<Stack> Stacks => stacks;
    public int Slots => slots;
    public Hand InHand { get; private set; } = Hand.None;
    public bool Owns(Hand tool) => tools.Contains(tool);

    // The suit's chest clip holds the torch (mechanic "Duas mãos"): the torch is never in a hand while this is on.
    public bool TorchOnChest { get; private set; }

    public event System.Action Changed;

    // Scripted moments: 1 and 2 do nothing (the hands are the script's).
    public bool Locked { get; set; }

    private void Awake()
    {
        foreach (Hand tool in startingTools)
        {
            tools.Add(tool);
        }
    }

    private void Start()
    {
        Equip(startingHand, true);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || Time.timeScale == 0f || Locked || (arms != null && arms.TakePlaying))
        {
            return;
        }
        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            if (TorchOnChest && tools.Contains(Hand.Torch))
            {
                hud?.ShowMessage(Loc.T("msg.torch_chest"), 2.5f);
            }
            else if (tools.Contains(Hand.Torch))
            {
                Equip(InHand == Hand.Torch ? Hand.None : Hand.Torch, false);
            }
            else
            {
                hud?.ShowMessage(Loc.T("msg.no_torch"), 2f);
            }
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            if (tools.Contains(Hand.Revolver))
            {
                Equip(InHand == Hand.Revolver ? (tools.Contains(Hand.Torch) && !TorchOnChest ? Hand.Torch : Hand.None) : Hand.Revolver, false);
            }
            else
            {
                hud?.ShowMessage(Loc.T("msg.no_gun"), 2f);
            }
        }
    }

    // A tool handed in by the story (the dive torch found in the hall): goes to the hand if the hands are free.
    public void GiveTool(Hand tool)
    {
        if (tool == Hand.None || tools.Contains(tool))
        {
            return;
        }

        tools.Add(tool);
        if (InHand == Hand.None && !(tool == Hand.Torch && TorchOnChest))
        {
            Equip(tool, true);
        }

        Changed?.Invoke();
    }

    // The attic suit put on (or taken off): the torch rides the chest from now on, the hands are free.
    public void SetTorchOnChest(bool on)
    {
        TorchOnChest = on;
        if (on && InHand == Hand.Torch)
        {
            Equip(Hand.None, true);
        }
        else
        {
            Equip(InHand, true);
        }
    }

    // How many did not fit.
    public int Add(InventoryItem item, int count)
    {
        if (item == null || count <= 0)
        {
            return count;
        }
        if (item.kind == InventoryItem.Kind.Tool)
        {
            tools.Add(item.tool);
            if (InHand == Hand.None)
            {
                Equip(item.tool, false);
            }
            Changed?.Invoke();
            return 0;
        }
        int left = count;
        foreach (Stack stack in stacks)
        {
            if (stack.item == item && stack.count < item.stackMax)
            {
                int take = Mathf.Min(left, item.stackMax - stack.count);
                stack.count += take;
                left -= take;
                if (left == 0)
                {
                    break;
                }
            }
        }
        while (left > 0 && stacks.Count < slots)
        {
            int take = Mathf.Min(left, item.stackMax);
            stacks.Add(new Stack { item = item, count = take });
            left -= take;
        }
        Changed?.Invoke();
        return left;
    }

    public bool Has(string id)
    {
        foreach (Stack stack in stacks)
        {
            if (stack.item != null && stack.item.id == id && stack.count > 0)
            {
                return true;
            }
        }
        return false;
    }

    // Uses one of the slot's items. Returns what happened, for the HUD.
    public string Use(int slot)
    {
        if (slot < 0 || slot >= stacks.Count)
        {
            return null;
        }
        Stack stack = stacks[slot];
        InventoryItem item = stack.item;
        string result;
        switch (item.kind)
        {
            case InventoryItem.Kind.Heal:
                if (air != null && air.Health >= 0.999f)
                {
                    return Loc.T("msg.not_hurt");
                }
                air?.Heal(item.amount);
                result = Loc.F("msg.heal", item.Name, Mathf.RoundToInt(item.amount * 100f));
                break;
            case InventoryItem.Kind.Ammo:
                if (revolver == null)
                {
                    return Loc.T("msg.no_gun_ammo");
                }
                revolver.AddSpare(Mathf.RoundToInt(item.amount));
                result = Loc.F("msg.ammo", item.Name, Mathf.RoundToInt(item.amount), revolver.Spare);
                break;
            case InventoryItem.Kind.Battery:
                if (gear == null || gear.HasBatteries)
                {
                    return Loc.T("msg.has_batteries");
                }
                gear.GiveBatteries();
                result = Loc.T("msg.batteries_in");
                break;
            case InventoryItem.Kind.Filter:
                if (respirator == null)
                {
                    return Loc.T("msg.no_mask_filter");
                }
                respirator.AddFilters(Mathf.Max(1, Mathf.RoundToInt(item.amount)));
                result = Loc.F("msg.filter_stored", respirator.SpareFilters);
                break;
            default:
                return null;
        }
        stack.count--;
        if (stack.count <= 0)
        {
            stacks.RemoveAt(slot);
        }
        Changed?.Invoke();
        return result;
    }

    public void Equip(Hand hand, bool silent)
    {
        if (hand != Hand.None && !tools.Contains(hand))
        {
            hand = Hand.None;
        }
        if (hand == Hand.Torch && TorchOnChest)
        {
            hand = Hand.None;
        }
        InHand = hand;
        bool revolverOut = hand == Hand.Revolver;
        if (arms != null)
        {
            arms.SetHands(hand == Hand.Revolver ? FirstPersonArms.Hands.Revolver : hand == Hand.Torch ? FirstPersonArms.Hands.Torch : FirstPersonArms.Hands.Empty);
        }

        if (revolver != null && revolver.gameObject.activeSelf != revolverOut)
        {
            revolver.gameObject.SetActive(revolverOut);
        }
        if (chestMount != null)
        {
            // The torch rides the chest while both hands hold the gun, and always with the suit on.
            if ((revolverOut || TorchOnChest) && tools.Contains(Hand.Torch))
            {
                chestMount.Mount();
            }
            else
            {
                chestMount.Unmount();
            }
        }
        if (!silent && hud != null)
        {
            bool torchOwned = tools.Contains(Hand.Torch);
            hud.ShowMessage(Loc.T(hand == Hand.Revolver ? (torchOwned ? "msg.hand.revolver_chest" : "msg.hand.revolver")
                : hand == Hand.Torch ? "msg.hand.torch" : TorchOnChest ? "msg.hand.free_chest" : "msg.hand.free"), 2.5f);
        }
        Changed?.Invoke();
    }

    // The hand as it was, after something else (a recorded hands clip) put it away.
    public void Refresh()
    {
        Equip(InHand, true);
    }
}
