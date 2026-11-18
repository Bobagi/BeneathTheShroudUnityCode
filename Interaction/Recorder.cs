using System.Collections;
using UnityEngine;

// A small tape recorder: E plays it. The voice comes out of the recorder itself (3D, so it is heard where
// it lies) with timed subtitles; the player keeps control and can walk while it plays. Once heard it goes
// into the diary (PlayerItems) and can be played again from there. A red light blinks until it is played.
// An optional comment by Erick follows the last line.
// Title, speaker, each line's text and the comment hold Loc keys (story.*, voice.*), resolved when shown: the voice
// stays Portuguese, the subtitle follows the language.
public sealed class Recorder : Interactable
{
    [System.Serializable]
    public struct Line
    {
        public string text;
        public float start;
        public float end;
    }

    [SerializeField]
    private string id = "rec";

    [SerializeField]
    private string title = "hud.diary.recording";

    [SerializeField]
    private string speaker = "ALEX";

    [SerializeField]
    private AudioClip clip;

    [SerializeField]
    private Line[] lines = new Line[0];

    [SerializeField]
    private AudioClip commentClip;

    [SerializeField]
    [TextArea(1, 3)]
    private string comment;

    // The LED: a tiny unlit dot, and nothing more. It used to carry a point light, which in a lit room lit the
    // table and shone through the recorder's own body (Gustavo, 01/10); a real LED lights nothing around it.
    [SerializeField]
    private Renderer indicatorDot;

    private MaterialPropertyBlock dotBlock;
    private bool ledOn;
    private Color ledColor;
    private static readonly Color Waiting = new Color(1f, 0.08f, 0.05f);
    private static readonly Color Playing = new Color(0.25f, 1f, 0.3f);

    [SerializeField]
    private AudioSource source;

    [SerializeField]
    private AudioClip buttonClip;

    [SerializeField]
    private AudioClip stopClip;

    private Coroutine playing;
    private bool heard;

    public bool IsPlaying => playing != null;
    public bool Heard => heard;
    public string Id => id;
    public string Title => Loc.Resolve(title);
    public override bool CanInteract => playing == null;

    public event System.Action<Recorder> Finished;

    public void Configure(string recorderId, string recorderTitle, AudioClip voice, Line[] voiceLines, AudioClip afterClip, string afterText)
    {
        id = recorderId;
        title = recorderTitle;
        clip = voice;
        lines = voiceLines;
        commentClip = afterClip;
        comment = afterText;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (playing != null)
        {
            return;
        }

        interactor.NotifyUsed(this);
        PlayerItems items = interactor.GetComponent<PlayerItems>();
        if (items != null)
        {
            RememberIn(items);
        }

        playing = StartCoroutine(Play(interactor.Hud, source, true));
    }

    // Into the diary (also when a checkpoint is loaded).
    public void RememberIn(PlayerItems items)
    {
        items.Remember(new PlayerItems.Entry
        {
            id = id,
            title = title,
            kind = PlayerItems.EntryKind.Recording,
            body = Transcript(),
            recorder = this,
        });
    }

    // Loading a checkpoint: already heard, the LED stays off.
    public void MarkHeard()
    {
        heard = true;
    }

    // Replay from the diary: through a 2D source so it is heard wherever the player is now.
    public void Replay(DiveHud hud, AudioSource where)
    {
        if (playing != null)
        {
            StopCoroutine(playing);
            source.Stop();
        }

        playing = StartCoroutine(Play(hud, where, false));
    }

    public string Transcript()
    {
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        foreach (Line line in lines)
        {
            text.Append(Loc.Resolve(line.text)).Append("\n\n");
        }

        return text.ToString().TrimEnd();
    }

    private IEnumerator Play(DiveHud hud, AudioSource output, bool firstTime)
    {
        if (buttonClip != null && source != null)
        {
            source.PlayOneShot(buttonClip, 0.6f);
        }

        ledOn = true;
        ledColor = Playing;

        float startedAt = Time.time;
        if (clip != null && output != null)
        {
            output.clip = clip;
            output.Play();
        }

        int shown = -1;
        float length = clip != null ? clip.length : lines.Length > 0 ? lines[lines.Length - 1].end + 0.5f : 0f;
        while (Time.time - startedAt < length)
        {
            float t = Time.time - startedAt;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (t >= lines[i].start)
                {
                    if (i != shown)
                    {
                        shown = i;
                        hud.ShowSubtitle(Loc.Resolve(speaker) + ": " + Loc.Resolve(lines[i].text), lines[i].end - lines[i].start + 0.6f);
                    }

                    break;
                }
            }

            yield return null;
        }

        if (firstTime && !string.IsNullOrEmpty(comment))
        {
            yield return new WaitForSeconds(0.8f);
            if (commentClip != null && output != null)
            {
                output.PlayOneShot(commentClip, 1f);
            }

            hud.ShowSubtitle("ERICK: " + Loc.Resolve(comment), (commentClip != null ? commentClip.length : 3f) + 0.6f);
            yield return new WaitForSeconds(commentClip != null ? commentClip.length : 3f);
        }

        ledOn = false;
        if (stopClip != null && source != null)
        {
            source.PlayOneShot(stopClip, 0.6f);
        }

        heard = true;
        playing = null;
        if (firstTime)
        {
            Finished?.Invoke(this);
        }
    }

    private void Update()
    {
        // Unheard: the red LED blinks, so the recorder is noticed.
        if (!heard && playing == null)
        {
            ledOn = Mathf.Repeat(Time.time, 1.4f) < 0.25f;
            ledColor = Waiting;
        }

        if (indicatorDot == null)
        {
            return;
        }

        dotBlock ??= new MaterialPropertyBlock();
        indicatorDot.enabled = ledOn;
        // Under the bloom threshold even after exposure: a bright red dot, not a glow.
        dotBlock.SetColor(BaseColorId, ledColor * 0.7f);
        indicatorDot.SetPropertyBlock(dotBlock);
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
}
