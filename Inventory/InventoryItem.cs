using UnityEngine;

// A thing the player can carry (Inventory). One asset per item type, in Assets/_Project/Items/Inventory. What the
// item does when used comes from its kind and amount; what it looks like on the ground is the pickup's (ItemPickup).
[CreateAssetMenu(menuName = "Horror/Item de inventário", fileName = "Item")]
public sealed class InventoryItem : ScriptableObject
{
    public enum Kind
    {
        // Heals `amount` (0..1 of the health bar) and is spent.
        Heal,
        // Adds `amount` rounds to the revolver's spare and is spent.
        Ammo,
        // The torch's batteries (DiveGear) and is spent.
        Battery,
        // One spare filter for the respirator (Respirator.AddFilters) and is spent.
        Filter,
        // A tool kept in the backpack: owning it lets the hand equip it (Inventory.Hand).
        Tool,
    }

    public string id = "item";
    public string displayName = "Item";
    [TextArea]
    public string description;
    public Kind kind = Kind.Heal;
    [Tooltip("Heal: fraction of the health bar. Ammo: rounds. Filter: filters. Others: unused.")]
    public float amount = 0.5f;
    [Tooltip("How many of this fit in one slot.")]
    public int stackMax = 5;
    [Tooltip("Tool only: which hand item this is (Torch, Revolver).")]
    public Inventory.Hand tool = Inventory.Hand.None;
    [Tooltip("Placeholder look: the colour of the pickup and of the icon.")]
    public Color colour = new Color(0.8f, 0.75f, 0.6f);
    public Sprite icon;

    // The name and description in the chosen language (keys item.<id>.name and item.<id>.desc in the Loc table); the
    // asset's own Portuguese fields are the fallback. The keys are built once per asset.
    private string nameKey;
    private string descriptionKey;

    public string Name
    {
        get
        {
            nameKey ??= "item." + id + ".name";
            return Loc.Has(nameKey) ? Loc.T(nameKey) : displayName;
        }
    }

    public string Description
    {
        get
        {
            descriptionKey ??= "item." + id + ".desc";
            return Loc.Has(descriptionKey) ? Loc.T(descriptionKey) : description;
        }
    }
}
