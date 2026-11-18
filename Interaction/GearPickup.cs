using UnityEngine;

// The dive kit laid out on the basement bench. Taking it equips the player and removes the props.
public sealed class GearPickup : Interactable
{
    [SerializeField]
    private GameObject[] visuals;

    [SerializeField]
    private AudioClip pickupClip;

    private bool taken;

    public override bool CanInteract => !taken;
    public bool Taken => taken;

    // Loading a checkpoint: the kit is already on the player.
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

        interactor.GetComponent<DiveGear>().Equip();
        interactor.Hud.ShowMessage(Loc.T("msg.gear_taken"), 4f);
        interactor.NotifyUsed(this);
    }
}
