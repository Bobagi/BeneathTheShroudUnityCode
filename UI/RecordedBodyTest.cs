using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

// Test scene only: K plays the cough recorded from video (the spine and head on the body's layer "Corpo gravado",
// Editor/HandMocap/RecordedBody; the left hand to the mouth on the first person rig's clip Cough) and I switches the
// filmed idle on and off (layer "Idle gravado", only while standing still). The reference video (the fit's preview)
// plays on the screen next to the start: the cough from its first frame at K, the idle in a loop. With the revolver
// in the hands the cough first swaps it for the torch (both hands are on the gun otherwise, and the cough is the free
// left hand's).
public sealed class RecordedBodyTest : MonoBehaviour
{
    [SerializeField]
    private PlayerBody body;

    [SerializeField]
    private VideoPlayer video;

    [SerializeField]
    private VideoClip coughVideo;

    [SerializeField]
    private VideoClip idleVideo;

    [SerializeField]
    private TMP_Text label;

    private Inventory inventory;

    public bool IdleOn => body != null && body.UseRecordedIdle;

    public void Cough()
    {
        if (body == null)
        {
            return;
        }
        if (inventory != null && inventory.InHand == Inventory.Hand.Revolver)
        {
            inventory.Equip(Inventory.Hand.Torch, true);
        }
        bool played = body.Cough();
        Show(coughVideo, false);
        SetLabel(played ? "Tosse (K)" : "Tosse: falta o clip (Horror > Animação > Assar corpo gravado)");
    }

    public void ToggleIdle()
    {
        if (body == null)
        {
            return;
        }
        body.UseRecordedIdle = !body.UseRecordedIdle;
        if (body.UseRecordedIdle)
        {
            Show(idleVideo, true);
        }
        else if (video != null)
        {
            video.Stop();
        }
        SetLabel(body.UseRecordedIdle ? "Idle gravado: ligado (I)" : "Idle gravado: desligado (I)");
    }

    private void Start()
    {
        inventory = Object.FindAnyObjectByType<Inventory>();
        SetLabel("Corpo gravado: K tosse, I idle");
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }
        if (keyboard.kKey.wasPressedThisFrame)
        {
            Cough();
        }
        if (keyboard.iKey.wasPressedThisFrame)
        {
            ToggleIdle();
        }
    }

    private void Show(VideoClip clip, bool loop)
    {
        if (video == null || clip == null)
        {
            return;
        }
        video.Stop();
        video.clip = clip;
        video.isLooping = loop;
        video.time = 0;
        video.Play();
    }

    private void SetLabel(string text)
    {
        if (label != null)
        {
            label.text = text;
        }
    }
}
