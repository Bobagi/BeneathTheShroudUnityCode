using UnityEngine;

// Anything the player can use with E. The collider can sit on this object or on a child. The prompt field holds a
// Loc key (or plain text in a scene built before the table); Prompt gives it in the chosen language.
public abstract class Interactable : MonoBehaviour
{
    [SerializeField]
    private string prompt = "prompt.examine";

    public virtual string Prompt => Loc.Resolve(prompt);
    public virtual bool CanInteract => true;

    public void SetPrompt(string text)
    {
        prompt = text;
    }

    public abstract void Interact(PlayerInteractor interactor);
}
