using UnityEngine;

// Something the player takes: a key, the lamp, oil, matches, a filter, the case. Taking it hides the
// visuals, shows one line and, with an item id, puts it in PlayerItems; the director listens through
// PlayerInteractor.Used and PlayerItems.ItemAdded.
public sealed class StoryPickup : Interactable
{
    [SerializeField]
    private GameObject[] visuals;

    [SerializeField]
    [TextArea(2, 4)]
    private string takenMessage;

    [SerializeField]
    private AudioClip pickupClip;

    [SerializeField]
    private string itemId;

    public string ItemId => itemId;

    public void Configure(string item, string message)
    {
        itemId = item;
        takenMessage = message;
    }

    private bool taken;

    public bool Taken => taken;
    public override bool CanInteract => !taken;

    // Loading a checkpoint: already taken, nothing shown or heard.
    public void RestoreTaken()
    {
        taken = true;
        foreach (GameObject visual in visuals)
        {
            if (visual != null)
            {
                visual.SetActive(false);
            }
        }
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (taken)
        {
            return;
        }

        taken = true;
        foreach (GameObject visual in visuals)
        {
            if (visual != null)
            {
                visual.SetActive(false);
            }
        }

        if (pickupClip != null)
        {
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, 0.8f);
        }

        interactor.Hud.ShowMessage(Loc.Resolve(takenMessage), 4.5f);
        PlayerItems items = interactor.GetComponent<PlayerItems>();
        if (items != null)
        {
            items.Add(itemId);
        }

        interactor.NotifyUsed(this);
    }
}
