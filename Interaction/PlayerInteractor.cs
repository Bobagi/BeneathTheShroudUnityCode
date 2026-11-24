using UnityEngine;
using UnityEngine.InputSystem;

// Looks for an Interactable under the crosshair, shows its prompt and uses it with E. While a note is open, E or Esc
// closes it and the body stays frozen. Sitting, E stands back up.
public sealed class PlayerInteractor : MonoBehaviour
{
    [SerializeField]
    private FirstPersonController controller;

    [SerializeField]
    private DiveHud hud;

    [SerializeField]
    private float reach = 2.3f;

    private Interactable focused;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    public DiveHud Hud => hud;
    public FirstPersonController Controller => controller;

    public event System.Action<Interactable> Used;

    public void NotifyUsed(Interactable interactable)
    {
        Used?.Invoke(interactable);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (hud.IsNoteOpen)
        {
            if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                hud.CloseNote();
                controller.InputLocked = false;
            }

            return;
        }

        if (controller.IsSeated || controller.IsStandingUp)
        {
            focused = null;
            if (controller.CanStand && !controller.InputLocked)
            {
                hud.SetPrompt("E", Loc.T("prompt.stand_up"));
                if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                {
                    controller.StandUp();
                }
            }
            else
            {
                hud.ClearPrompt();
            }

            return;
        }

        focused = controller.InputLocked ? null : FindFocus();
        if (focused != null)
        {
            hud.SetPrompt("E", focused.Prompt);
        }
        else
        {
            hud.ClearPrompt();
        }

        if (focused != null && keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            Use(focused);
        }
    }

    private void Use(Interactable target)
    {
        if (target == null || !target.CanInteract)
        {
            return;
        }

        target.Interact(this);
        if (hud.IsNoteOpen)
        {
            controller.InputLocked = true;
        }
    }

    // The first thing under the crosshair that is not the player's own body decides: no using
    // things through walls or furniture.
    private Interactable FindFocus()
    {
        Transform eye = controller.CameraPivot;
        int count = Physics.SphereCastNonAlloc(eye.position, 0.08f, eye.forward, hits, reach, ~0, QueryTriggerInteraction.Collide);
        RaycastHit nearest = default;
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].distance < best && !hits[i].collider.transform.IsChildOf(controller.transform))
            {
                best = hits[i].distance;
                nearest = hits[i];
            }
        }

        if (nearest.collider == null)
        {
            return null;
        }

        Interactable interactable = nearest.collider.GetComponentInParent<Interactable>();
        return interactable != null && interactable.CanInteract ? interactable : null;
    }
}
