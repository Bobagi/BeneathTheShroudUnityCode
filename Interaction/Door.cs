using System.Collections;
using UnityEngine;

// A door on a hinge. The old wooden ones open with a key (an item id the player must carry); the ones the
// Lunas fitted later have a keypad (MansionHud asks for the code, with a hint); some stay shut until the
// story opens them (Unlock), saying why. Once open it can be closed again with E (closable), and a double door's
// two leaves are linked: using either swings both (the partner follows in silence). The hinge is this object's
// transform; the leaf (with its collider) is a child, so it swings with it.
public sealed class Door : Interactable
{
    public enum Lock { None, Key, Code, Story }

    [SerializeField]
    private Lock lockType = Lock.None;

    [SerializeField]
    private string keyItem;

    [SerializeField]
    private string code;

    [SerializeField]
    private string codeHint;

    [SerializeField]
    [TextArea(1, 3)]
    private string lockedMessage = "msg.locked";

    [SerializeField]
    private float openAngle = 100f;

    [SerializeField]
    private float openSeconds = 1.1f;

    [Tooltip("Once open, E closes it again.")]
    [SerializeField]
    private bool closable = true;

    [Tooltip("The other leaf of a double door: opens and closes together with this one.")]
    [SerializeField]
    private Door linked;

    [Tooltip("Built already swung open (a house left in a hurry): the closed pose is openAngle back from where it starts.")]
    [SerializeField]
    private bool startOpen;

    [SerializeField]
    private AudioSource source;

    [SerializeField]
    private AudioClip openClip;

    [SerializeField]
    private AudioClip closeClip;

    [SerializeField]
    private AudioClip unlockClip;

    [SerializeField]
    private AudioClip lockedClip;

    private bool open;
    private bool moving;
    private Quaternion closedRotation;
    private bool closedKnown;

    public bool IsOpen => open;
    public override bool CanInteract => !moving && (!open || closable);

    public override string Prompt
    {
        get
        {
            if (open)
            {
                return Loc.T("prompt.close");
            }

            switch (lockType)
            {
                case Lock.Code: return Loc.T("prompt.enter_code");
                default: return Loc.T("prompt.open");
            }
        }
    }

    public event System.Action<Door> Opened;

    public void Configure(Lock type, string item, string doorCode, string hint, string message)
    {
        lockType = type;
        keyItem = item;
        code = doorCode;
        codeHint = hint;
        lockedMessage = message;
    }

    // The two leaves of a double door, each the other's partner.
    public static void Link(Door a, Door b)
    {
        a.linked = b;
        b.linked = a;
    }

    public void SetClosable(bool value)
    {
        closable = value;
    }

    // The story lets it open (and a key door no longer needs its key once it is unlocked this way).
    public void Unlock()
    {
        lockType = Lock.None;
    }

    private void Awake()
    {
        RememberClosed();
    }

    private void RememberClosed()
    {
        if (!closedKnown)
        {
            closedRotation = startOpen ? transform.localRotation * Quaternion.Euler(0f, -openAngle, 0f) : transform.localRotation;
            open = startOpen;
            closedKnown = true;
        }
    }

    // Builders: the leaf is placed in its open pose and remembers that closed is openAngle back.
    public void SetStartOpen(float angle)
    {
        openAngle = angle;
        startOpen = true;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (moving)
        {
            return;
        }

        if (open)
        {
            if (closable)
            {
                Close();
                interactor.NotifyUsed(this);
            }

            return;
        }

        switch (lockType)
        {
            case Lock.None:
                Open();
                break;
            case Lock.Key:
                PlayerItems items = interactor.GetComponent<PlayerItems>();
                if (items != null && items.HasItem(keyItem))
                {
                    Play(unlockClip, 0.9f);
                    lockType = Lock.None;
                    Open(0.5f);
                }
                else
                {
                    Refuse(interactor);
                }

                break;
            case Lock.Code:
                MansionHud.Instance.OpenKeypad(Loc.Resolve(codeHint), code, () =>
                {
                    lockType = Lock.None;
                    Open(0.3f);
                });
                break;
            default:
                Refuse(interactor);
                break;
        }

        interactor.NotifyUsed(this);
    }

    public void Open(float delay = 0f)
    {
        Open(delay, true);
    }

    public void Close()
    {
        Close(true);
    }

    private void Open(float delay, bool withPartner)
    {
        if (!open && !moving)
        {
            StartCoroutine(Swing(delay, true, withPartner));
        }

        if (withPartner && linked != null)
        {
            linked.lockType = Lock.None;
            linked.Open(delay, false);
        }
    }

    private void Close(bool withPartner)
    {
        if (open && !moving)
        {
            StartCoroutine(Swing(0f, false, withPartner));
        }

        if (withPartner && linked != null)
        {
            linked.Close(false);
        }
    }

    // Loading a checkpoint: already open, at once and in silence.
    public void RestoreOpen()
    {
        if (open || moving)
        {
            return;
        }

        RememberClosed();
        lockType = Lock.None;
        transform.localRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        open = true;
    }

    private void Refuse(PlayerInteractor interactor)
    {
        Play(lockedClip, 0.8f);
        interactor.Hud.ShowMessage(Loc.Resolve(lockedMessage), 3f);
    }

    private IEnumerator Swing(float delay, bool toOpen, bool loud)
    {
        RememberClosed();
        moving = true;
        yield return new WaitForSeconds(delay);
        if (loud)
        {
            Play(toOpen ? openClip : (closeClip != null ? closeClip : openClip), 0.9f);
        }

        Quaternion from = transform.localRotation;
        Quaternion target = toOpen ? closedRotation * Quaternion.Euler(0f, openAngle, 0f) : closedRotation;
        float seconds = toOpen ? openSeconds : openSeconds * 0.8f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            // Heavy at first, then it swings.
            float e = 1f - Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, t), 2f);
            transform.localRotation = Quaternion.Slerp(from, target, e);
            yield return null;
        }

        transform.localRotation = target;
        moving = false;
        open = toOpen;
        if (toOpen)
        {
            Opened?.Invoke(this);
        }
    }

    private void Play(AudioClip clip, float volume)
    {
        if (source != null && clip != null)
        {
            source.pitch = Random.Range(0.92f, 1.05f);
            source.PlayOneShot(clip, volume);
        }
    }
}
