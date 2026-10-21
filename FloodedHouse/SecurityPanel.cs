using System.Collections;
using TMPro;
using UnityEngine;

// The house security system: a wall screen blinking red with the emergency list (Dayse's scene 1) and a
// voice through the house speakers. The countdown to dawn moves with the story, not with a clock (GDD:
// a limit that is not time). Runs on its own battery, so it survives the blackout.
// Status lines and announcements come in as Loc keys (sys.*) and are resolved when drawn or spoken.
public sealed class SecurityPanel : MonoBehaviour
{
    [SerializeField]
    private TextMeshPro screen;

    [SerializeField]
    private Renderer frame;

    [SerializeField]
    private Light glow;

    [SerializeField]
    private AudioSource speaker;

    [SerializeField]
    private DiveHud hud;

    private string[] lines = new string[0];
    private int hoursToDawn = 5;
    private int humidity = 92;
    private bool alarm = true;
    private MaterialPropertyBlock block;

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    public int HoursToDawn => hoursToDawn;

    public void SetState(int hours, int humidityPercent, params string[] status)
    {
        hoursToDawn = hours;
        humidity = humidityPercent;
        lines = status;
        Redraw();
    }

    // Beeps, then the voice line through the speakers with its subtitle. Heard everywhere in the house.
    public Coroutine Announce(AudioClip voice, string text)
    {
        return StartCoroutine(AnnounceRoutine(voice, text));
    }

    private IEnumerator AnnounceRoutine(AudioClip voice, string text)
    {
        for (int i = 0; i < 3; i++)
        {
            speaker.PlayOneShot(ProceduralAudio.Beep(1320f), 0.35f);
            yield return new WaitForSeconds(0.22f);
        }

        yield return new WaitForSeconds(0.25f);
        if (voice != null)
        {
            speaker.PlayOneShot(voice, 0.8f);
        }

        hud.ShowSubtitle(Loc.T("hud.speaker.system") + ": " + Loc.Resolve(text), (voice != null ? voice.length : 4f) + 0.5f);
        yield return new WaitForSeconds(voice != null ? voice.length : 4f);
    }

    private void Start()
    {
        block = new MaterialPropertyBlock();
        Redraw();
    }

    private void OnEnable()
    {
        Loc.Changed += Redraw;
    }

    private void OnDisable()
    {
        Loc.Changed -= Redraw;
    }

    private void Redraw()
    {
        if (screen == null)
        {
            return;
        }

        System.Text.StringBuilder text = new System.Text.StringBuilder();
        text.Append("<size=150%><b>").Append(Loc.T("sys.panel.title")).Append("</b></size>\n\n");
        foreach (string line in lines)
        {
            text.Append(Loc.Resolve(line)).Append('\n');
        }

        text.Append('\n').Append(Loc.F("sys.panel.humidity", humidity)).Append('\n');
        text.Append("\n<b>").Append(Loc.F(hoursToDawn == 1 ? "sys.panel.hour" : "sys.panel.hours", hoursToDawn)).Append("</b>\n");
        text.Append(Loc.F("sys.panel.heat", hoursToDawn + 6));
        screen.text = text.ToString();
    }

    private void Update()
    {
        if (!alarm)
        {
            return;
        }

        // The red blink of the emergency.
        float blink = Mathf.Repeat(Time.time, 1.2f) < 0.6f ? 1f : 0.25f;
        if (glow != null)
        {
            glow.intensity = 0.6f * blink;
        }

        if (frame != null)
        {
            frame.GetPropertyBlock(block);
            block.SetColor(EmissionId, new Color(1f, 0.08f, 0.05f) * 2.2f * blink);
            frame.SetPropertyBlock(block);
        }
    }
}
