using System.Collections;
using UnityEngine;

// A door on a hinge. The old wooden ones open with a key (an item id the player must carry); the ones the
// Lunas fitted later have a keypad (MansionHud asks for the code, with a hint); some stay shut until the
// story opens them (Unlock), saying why. Once open it stays open. The hinge is this object's transform; the
// leaf (with its collider) is a child, so it swings with it.
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

    [SerializeField]
    private AudioSource source;

    [SerializeField]
    private AudioClip openClip;

    [SerializeField]
    private AudioClip unlockClip;

    [SerializeField]
    private AudioClip lockedClip;

    private bool open;
    private bool opening;

    public bool IsOpen => open;
    public override bool CanInteract => !open && !opening;

    public override string Prompt
    {
        get
        {
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

    // The story lets it open (and a key door no longer needs its key once it is unlocked this way).
    public void Unlock()
    {
        lockType = Lock.None;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (open || opening)
        {
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
        if (!open && !opening)
        {
            StartCoroutine(Swing(delay));
        }
    }

    // Loading a checkpoint: already open, at once and in silence.
    public void RestoreOpen()
    {
        if (open || opening)
        {
            return;
        }

        lockType = Lock.None;
        transform.localRotation *= Quaternion.Euler(0f, openAngle, 0f);
        open = true;
    }

    private void Refuse(PlayerInteractor interactor)
    {
        Play(lockedClip, 0.8f);
        interactor.Hud.ShowMessage(Loc.Resolve(lockedMessage), 3f);
    }

    private IEnumerator Swing(float delay)
    {
        opening = true;
        yield return new WaitForSeconds(delay);
        Play(openClip, 0.9f);
        Quaternion closed = transform.localRotation;
        Quaternion target = closed * Quaternion.Euler(0f, openAngle, 0f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / openSeconds)
        {
            // Heavy at first, then it swings.
            float e = 1f - Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, t), 2f);
            transform.localRotation = Quaternion.Slerp(closed, target, e);
            yield return null;
        }

        transform.localRotation = target;
        opening = false;
        open = true;
        Opened?.Invoke(this);
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
