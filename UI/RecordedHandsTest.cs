using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

// Test scene only: H plays or stops a take recorded from video on the first person rig (layer "Mãos gravadas" of
// FirstPersonArms, clips baked by Editor/FirstPerson/FirstPersonTakes), J switches to the next take at the same
// moment. The animation itself is the clip; this only picks the state, empties the hands while it plays (the torch
// and the gun go away, as the hands are the take's) and keeps the reference video on the screen on the clip's clock.
public sealed class RecordedHandsTest : MonoBehaviour
{
    [SerializeField]
    private FirstPersonArms arms;

    [SerializeField]
    private string[] states = new string[0];

    [SerializeField]
    private TMP_Text label;

    [SerializeField]
    private VideoPlayer video;

    [SerializeField]
    private bool playOnStart;

    private int layer = -1;
    private int current;
    private bool on;
    private float lastSeek = -10f;
    private Inventory inventory;
    private Inventory.Hand handBefore = Inventory.Hand.None;

    private string State => states.Length > 0 ? states[current] : FirstPersonArms.EmptyState;

    public bool IsOn => on;

    // The panel's button (and H): the take on or off; off puts back what the hands held.
    public void Toggle()
    {
        Set(!on);
    }

    private void Start()
    {
        inventory = Object.FindAnyObjectByType<Inventory>();
        if (arms != null)
        {
            layer = arms.Animator.GetLayerIndex(FirstPersonArms.TakesLayer);
            // Every take the rig knows, if none was listed.
            if (states.Length == 0)
            {
                List<string> found = new List<string>();
                foreach (AnimationClip clip in arms.Animator.runtimeAnimatorController.animationClips)
                {
                    if (arms.HasTake(clip.name) && !found.Contains(clip.name))
                    {
                        found.Add(clip.name);
                    }
                }

                states = found.ToArray();
            }
        }

        Set(playOnStart, true);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.hKey.wasPressedThisFrame)
        {
            Set(!on);
        }

        if (keyboard != null && keyboard.jKey.wasPressedThisFrame && states.Length > 0)
        {
            Next();
        }

        if (!on || layer < 0 || video == null || !video.isPlaying || video.length <= 0.0)
        {
            return;
        }

        AnimatorStateInfo info = arms.Animator.GetCurrentAnimatorStateInfo(layer);
        if (!info.IsName(State))
        {
            return;
        }

        float wanted = Mathf.Repeat(info.normalizedTime, 1f) * info.length;
        // At most once a second: a seek takes a moment to land.
        if (Mathf.Abs((float)video.time - wanted) > 0.3f && Time.unscaledTime - lastSeek > 1f)
        {
            video.time = wanted;
            lastSeek = Time.unscaledTime;
        }
    }

    private void Set(bool play, bool initial = false)
    {
        on = play && arms != null && arms.HasTake(State);
        // With an inventory, the hands empty for the clip and come back as they were (torch or revolver).
        if (inventory != null && !(initial && !on))
        {
            if (on)
            {
                handBefore = inventory.InHand;
                inventory.Equip(Inventory.Hand.None, true);
            }
            else
            {
                inventory.Equip(handBefore == Inventory.Hand.None ? Inventory.Hand.Torch : handBefore, true);
            }
        }

        if (arms != null)
        {
            if (on)
            {
                arms.PlayTake(State);
            }
            else
            {
                arms.StopTake();
            }
        }

        ShowLabel();
        if (video != null)
        {
            if (on)
            {
                video.Play();
            }
            else
            {
                video.Pause();
            }
        }
    }

    // The next take starts where the current one was, so the same moment of the video can be compared.
    private void Next()
    {
        float at = 0f;
        if (on && layer >= 0)
        {
            AnimatorStateInfo info = arms.Animator.GetCurrentAnimatorStateInfo(layer);
            at = Mathf.Repeat(info.normalizedTime, 1f) * info.length;
        }

        current = (current + 1) % states.Length;
        if (on && layer >= 0)
        {
            arms.Animator.CrossFadeInFixedTime(State, 0.1f, layer, at);
        }

        ShowLabel();
    }

    private void ShowLabel()
    {
        if (label != null)
        {
            label.text = "Tomada " + (current + 1) + " de " + states.Length + ": " + State + (on ? "" : " (parada)") +
                "\nH liga e desliga as mãos gravadas   ·   J troca a tomada";
        }
    }
}
