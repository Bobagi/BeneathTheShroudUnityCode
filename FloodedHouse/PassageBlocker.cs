using System.Collections;
using UnityEngine;

// The cabinet M. shoved against the breach into the sunken bedroom. Until the story gives a reason
// (the slate in the tank room says what is behind it), it only answers with a line; after Unlock()
// E pushes it: it tips away into the silt with a groan and the gap opens.
public sealed class PassageBlocker : Interactable
{
    [SerializeField]
    private Transform body;

    [Tooltip("Solid box filling the gap; switched off once the cabinet is pushed.")]
    [SerializeField]
    private Collider blocker;

    [SerializeField]
    private Vector3 pushedOffset = new Vector3(-0.9f, -0.35f, 0.9f);

    [SerializeField]
    private Vector3 pushedEuler = new Vector3(0f, 25f, -70f);

    [SerializeField]
    private string lockedMessage = "msg.blocker_default";

    [SerializeField]
    private AudioClip pushClip;

    private bool unlocked;
    private bool pushed;

    public override string Prompt => Loc.T(unlocked ? "prompt.push_cabinet" : "prompt.examine_cabinet");
    public override bool CanInteract => !pushed;

    public bool IsUnlocked => unlocked;
    public bool IsPushed => pushed;

    // Loading a checkpoint: already pushed aside, at once.
    public void RestorePushed()
    {
        unlocked = true;
        pushed = true;
        body.SetPositionAndRotation(body.position + pushedOffset, Quaternion.Euler(pushedEuler) * body.rotation);
        if (blocker != null)
        {
            blocker.enabled = false;
        }
    }

    public void Unlock()
    {
        unlocked = true;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (pushed)
        {
            return;
        }

        if (!unlocked)
        {
            interactor.Hud.ShowMessage(Loc.Resolve(lockedMessage), 3.5f);
            return;
        }

        pushed = true;
        StartCoroutine(Push());
        interactor.NotifyUsed(this);
    }

    private IEnumerator Push()
    {
        if (pushClip != null)
        {
            AudioSource.PlayClipAtPoint(pushClip, transform.position, 1f);
        }

        Vector3 from = body.position;
        Quaternion fromRotation = body.rotation;
        Vector3 to = from + pushedOffset;
        Quaternion toRotation = Quaternion.Euler(pushedEuler) * fromRotation;
        // Slow under water: a heavy piece of furniture through mud.
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.6f)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            body.SetPositionAndRotation(Vector3.Lerp(from, to, e), Quaternion.Slerp(fromRotation, toRotation, e));
            yield return null;
        }

        body.SetPositionAndRotation(to, toRotation);
        if (blocker != null)
        {
            blocker.enabled = false;
        }
    }
}
