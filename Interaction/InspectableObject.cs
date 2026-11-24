using UnityEngine;

// Looking at something up close gives one line of text (the radio, a tank). The message can be
// swapped at runtime when the story moves on, e.g. the radio going silent.
public sealed class InspectableObject : Interactable
{
    [SerializeField]
    [TextArea(2, 5)]
    private string message;

    [SerializeField]
    private float messageSeconds = 4f;

    public void SetMessage(string text)
    {
        message = text;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        interactor.Hud.ShowMessage(Loc.Resolve(message), messageSeconds);
        interactor.NotifyUsed(this);
    }
}
