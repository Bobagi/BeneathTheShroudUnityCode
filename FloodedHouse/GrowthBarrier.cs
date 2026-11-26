using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A doorway the growth has closed (Dayse, 26/11: "a casa tá toda aberta... usar os fungos e as coisas do próprio
// cenário para ir bloqueando a passagem, aos poucos"). A solid stop behind a mass of her fungus; E on it says what
// is missing, or, once the player carries what clears it (an item id: the hook tears silk, the suit lets them push
// through spores, the power dries the damp ones), opens the way: the growth shrivels and the stop is gone. Which
// item clears which doorway is the builder's (FloodedHouseBarriers). The director saves the open ones.
public sealed class GrowthBarrier : Interactable
{
    [Tooltip("Item id the player needs to clear it (PlayerItems).")]
    [SerializeField]
    private string needsItem;

    [Tooltip("Loc key (or text) shown when the player does not have it yet.")]
    [SerializeField]
    private string blockedMessage = "msg.growth_blocked";

    [Tooltip("Loc key of the prompt once it can be cleared.")]
    [SerializeField]
    private string clearPrompt = "prompt.clear_growth";

    [Tooltip("The growth that shrivels (children of this object).")]
    [SerializeField]
    private Transform growth;

    [Tooltip("What stops the player.")]
    [SerializeField]
    private Collider stop;

    [SerializeField]
    private AudioClip tearClip;

    private static readonly List<GrowthBarrier> all = new List<GrowthBarrier>();
    private bool open;
    private bool opening;
    private PlayerItems items;

    public static IReadOnlyList<GrowthBarrier> All => all;
    public bool IsOpen => open || opening;
    public string NeedsItem => needsItem;
    public override bool CanInteract => !open && !opening;

    public override string Prompt => Has() ? Loc.Resolve(clearPrompt) : Loc.T("prompt.examine");

    public void Configure(string item, string message, string prompt, Transform growthRoot, Collider stopCollider, AudioClip tear)
    {
        needsItem = item;
        blockedMessage = message;
        clearPrompt = prompt;
        growth = growthRoot;
        stop = stopCollider;
        tearClip = tear;
    }

    private void OnEnable()
    {
        if (!all.Contains(this))
        {
            all.Add(this);
        }
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    private bool Has()
    {
        if (items == null)
        {
            items = FindAnyObjectByType<PlayerItems>();
        }

        return items != null && (string.IsNullOrEmpty(needsItem) || items.HasItem(needsItem));
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (open || opening)
        {
            return;
        }

        items = interactor.GetComponent<PlayerItems>();
        if (Has())
        {
            Open();
        }
        else
        {
            interactor.Hud.ShowMessage(Loc.Resolve(blockedMessage), 4f);
        }

        interactor.NotifyUsed(this);
    }

    // The player (or a test) clears it: the growth shrivels over two seconds, the stop goes halfway through.
    public void Open()
    {
        if (!open && !opening && isActiveAndEnabled)
        {
            StartCoroutine(Shrivel());
        }
    }

    // Loading a checkpoint: already gone, at once and in silence.
    public void RestoreOpen()
    {
        StopAllCoroutines();
        opening = false;
        open = true;
        if (stop != null)
        {
            stop.enabled = false;
        }

        if (growth != null)
        {
            growth.gameObject.SetActive(false);
        }
    }

    private IEnumerator Shrivel()
    {
        opening = true;
        if (tearClip != null)
        {
            AudioSource.PlayClipAtPoint(tearClip, transform.position + Vector3.up * 1.2f, 0.8f);
        }

        const float seconds = 2.2f;
        Vector3 from = growth != null ? growth.localScale : Vector3.one;
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            if (growth != null)
            {
                // It pulls back to the frame and down: thinner first, then shorter.
                float e = t * t * (3f - 2f * t);
                growth.localScale = new Vector3(from.x * (1f - e), from.y * (1f - e * e), from.z * (1f - 0.6f * e));
            }

            if (t > 0.55f && stop != null && stop.enabled)
            {
                stop.enabled = false;
            }

            yield return null;
        }

        RestoreOpen();
    }
}
