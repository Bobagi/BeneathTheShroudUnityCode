using UnityEngine;

// Something to read: a note, a newspaper clipping, a photo with writing on the back. It opens full screen
// on the HUD until E or Esc closes it (the world waits). Read once, it goes into the diary (PlayerItems).
// A photo shows above the text. Erick can say something about it once it is closed (his voice line and a
// subtitle), like the graduation photo whose year opens the generator room.
// Body, signature, title and comment hold Loc keys (story.*, voice.*) and are resolved when shown.
public sealed class ReadableNote : Interactable
{
    [SerializeField]
    [TextArea(4, 12)]
    private string body;

    [SerializeField]
    private string signature;

    [Tooltip("Written in pencil on a diver's slate (under water) instead of on paper.")]
    [SerializeField]
    private bool slate;

    [Header("Diary")]
    [SerializeField]
    private string archiveId;

    [SerializeField]
    private string archiveTitle;

    [SerializeField]
    private PlayerItems.EntryKind kind = PlayerItems.EntryKind.Note;

    [SerializeField]
    private Texture photo;

    [Header("Erick's comment after reading")]
    [SerializeField]
    private AudioClip commentClip;

    [SerializeField]
    [TextArea(1, 3)]
    private string comment;

    private bool commented;
    private DiveHud waitingHud;

    public void SetText(string text, string signedBy, bool onSlate = false)
    {
        body = text;
        signature = signedBy;
        slate = onSlate;
    }

    public void SetArchive(string id, string title, PlayerItems.EntryKind entryKind, Texture picture = null)
    {
        archiveId = id;
        archiveTitle = title;
        kind = entryKind;
        photo = picture;
    }

    public void SetComment(AudioClip clip, string text)
    {
        commentClip = clip;
        comment = text;
    }

    public string ArchiveId => archiveId;

    public PlayerItems.EntryKind Kind => kind;

    // Into the diary (also when a checkpoint is loaded).
    public void RememberIn(PlayerItems items)
    {
        if (!string.IsNullOrEmpty(archiveId))
        {
            // Keys, not text: the diary resolves them when it opens the entry.
            items.Remember(new PlayerItems.Entry { id = archiveId, title = archiveTitle, kind = kind, body = body, signature = signature, photo = photo });
        }
    }

    public override void Interact(PlayerInteractor interactor)
    {
        interactor.Hud.ShowNote(Loc.Resolve(body), Loc.Resolve(signature), slate, photo);
        PlayerItems items = interactor.GetComponent<PlayerItems>();
        if (items != null)
        {
            RememberIn(items);
        }

        if (!commented && !string.IsNullOrEmpty(comment) && waitingHud == null)
        {
            waitingHud = interactor.Hud;
            waitingHud.NoteClosed += OnClosed;
        }

        interactor.NotifyUsed(this);
    }

    private void OnClosed()
    {
        waitingHud.NoteClosed -= OnClosed;
        commented = true;
        if (commentClip != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(commentClip, Camera.main.transform.position, 1f);
        }

        waitingHud.ShowSubtitle("ERICK: " + Loc.Resolve(comment), (commentClip != null ? commentClip.length : 3f) + 0.6f);
        waitingHud = null;
    }
}
