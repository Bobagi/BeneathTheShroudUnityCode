using System.Collections;
using UnityEngine;

// The attic hatch in the hall ceiling. Its folding ladder is pulled down with the hooked pole (an item);
// then E climbs up (or, from the attic side, down): a short fade with the ladder creaking, the player
// arrives at the other end. There is no ladder-climbing animation yet (see the asset list in the GDD).
public sealed class AtticHatch : Interactable
{
    [SerializeField]
    private string toolItem = "gancho";

    [SerializeField]
    private Transform panel;

    [SerializeField]
    private GameObject ladder;

    [Tooltip("The ladder model carries its own hanging door: the closed panel hides once the ladder is down.")]
    [SerializeField]
    private bool ladderHasDoor;

    [SerializeField]
    private Transform destination;

    [Tooltip("The hatch at the other end; its ladder state follows this one.")]
    [SerializeField]
    private AtticHatch otherSide;

    [SerializeField]
    private bool needsTool = true;

    [SerializeField]
    private string climbPrompt = "prompt.attic_up";

    [SerializeField]
    private AudioSource source;

    [SerializeField]
    private AudioClip creakClip;

    private bool down;
    private bool busy;

    public bool IsDown => down;
    public override bool CanInteract => !busy;
    public override string Prompt => down ? Loc.Resolve(climbPrompt) : Loc.T("prompt.attic_pull");

    public event System.Action<AtticHatch> Climbed;

    public override void Interact(PlayerInteractor interactor)
    {
        if (busy)
        {
            return;
        }

        if (!down)
        {
            PlayerItems items = interactor.GetComponent<PlayerItems>();
            if (needsTool && (items == null || !items.HasItem(toolItem)))
            {
                interactor.Hud.ShowMessage(Loc.T("msg.hatch_tool"), 4f);
                return;
            }

            StartCoroutine(Lower());
            return;
        }

        StartCoroutine(Climb(interactor));
    }

    public void SetDown()
    {
        down = true;
        if (ladder != null)
        {
            ladder.SetActive(true);
        }

        if (panel != null)
        {
            panel.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            if (ladderHasDoor && ladder != null)
            {
                panel.gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator Lower()
    {
        busy = true;
        Play(0.9f);
        Quaternion closed = panel != null ? panel.localRotation : Quaternion.identity;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
        {
            if (panel != null)
            {
                panel.localRotation = Quaternion.Slerp(closed, Quaternion.Euler(-80f, 0f, 0f), Mathf.SmoothStep(0f, 1f, t));
            }

            yield return null;
        }

        SetDown();
        if (otherSide != null)
        {
            otherSide.SetDown();
        }

        busy = false;
    }

    private IEnumerator Climb(PlayerInteractor interactor)
    {
        busy = true;
        FirstPersonController player = interactor.Controller;
        player.InputLocked = true;
        Play(1f);
        yield return interactor.Hud.FadeTo(1f, 0.45f);
        player.Teleport(destination.position, destination.eulerAngles.y);
        yield return new WaitForSeconds(0.35f);
        Play(0.7f);
        yield return interactor.Hud.FadeTo(0f, 0.6f);
        player.InputLocked = false;
        busy = false;
        Climbed?.Invoke(this);
    }

    private void Play(float volume)
    {
        if (source != null && creakClip != null)
        {
            source.pitch = Random.Range(0.85f, 1.05f);
            source.PlayOneShot(creakClip, volume);
        }
    }
}
