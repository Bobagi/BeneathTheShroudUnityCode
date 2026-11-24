using UnityEngine;

// An item lying in the world. E puts it in the backpack (Inventory) and the object goes away; if the backpack is
// full it stays, with a message. The look is whatever is under this object (a model, or the test scene's
// placeholder shapes).
public sealed class ItemPickup : Interactable
{
    [SerializeField]
    private InventoryItem item;

    [SerializeField]
    private int count = 1;

    [SerializeField]
    private AudioClip pickupClip;

    private bool taken;
    // Not serialized: the base class already serializes a field named "prompt".
    [System.NonSerialized] private string cachedPrompt;
    [System.NonSerialized] private int promptLanguage = -1;
    [System.NonSerialized] private int promptCount = -1;

    public InventoryItem Item => item;
    public int Count => count;

    // Built once per language and count (the prompt is read every frame while the item is under the crosshair).
    public override string Prompt
    {
        get
        {
            if (item == null)
            {
                return base.Prompt;
            }

            if (cachedPrompt == null || promptLanguage != Loc.Language || promptCount != count)
            {
                promptLanguage = Loc.Language;
                promptCount = count;
                cachedPrompt = Loc.F("prompt.take_item", item.Name.ToLowerInvariant()) + (count > 1 ? " (" + count + ")" : "");
            }

            return cachedPrompt;
        }
    }
    public override bool CanInteract => !taken && item != null;

    public void Configure(InventoryItem definition, int amount)
    {
        item = definition;
        count = Mathf.Max(1, amount);
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (taken || item == null)
        {
            return;
        }
        Inventory inventory = interactor.GetComponentInParent<Inventory>() ?? interactor.GetComponentInChildren<Inventory>();
        if (inventory == null)
        {
            interactor.Hud.ShowMessage(Loc.T("msg.nowhere"), 2f);
            return;
        }
        int left = inventory.Add(item, count);
        if (left == count)
        {
            interactor.Hud.ShowMessage(Loc.T("msg.bag_full"), 2.5f);
            return;
        }
        count = left;
        if (pickupClip != null)
        {
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, 0.8f);
        }
        interactor.Hud.ShowMessage(Loc.F(left > 0 ? "msg.item_partial" : "msg.item_stored", item.Name), 2.5f);
        if (left == 0)
        {
            taken = true;
            interactor.NotifyUsed(this);
            gameObject.SetActive(false);
        }
    }
}
