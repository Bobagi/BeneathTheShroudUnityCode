using UnityEngine;
using UnityEngine.InputSystem;

// Test helper (not used in the demo): plays every clip of an Animator Controller in turn, each for its own
// length (loops twice), so a newly rigged creature can be judged in every animation. N skips to the next clip;
// a small label names the clip playing. Built by Horror > Testes > Afogado na cena aberta.
[RequireComponent(typeof(Animator))]
public sealed class AnimationShowcase : MonoBehaviour
{
    [SerializeField]
    private string[] states = new string[0];

    [SerializeField]
    private float minimumSeconds = 3f;

    private Animator animator;
    private int index = -1;
    private float nextAt;
    private GUIStyle style;

    public string Current => index >= 0 && index < states.Length ? states[index] : string.Empty;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        bool skip = keyboard != null && keyboard.nKey.wasPressedThisFrame;
        if (states.Length > 0 && (skip || Time.time >= nextAt))
        {
            Play((index + 1) % states.Length);
        }
    }

    public void Play(int i)
    {
        index = i;
        animator.CrossFadeInFixedTime(states[index], 0.25f);
        float length = minimumSeconds;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == states[index])
            {
                length = Mathf.Max(minimumSeconds, clip.isLooping ? clip.length * 2f : clip.length + 1f);
            }
        }

        nextAt = Time.time + length;
    }

    private void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = Color.white;
        }

        GUI.Box(new Rect(16, Screen.height - 52, 420, 36), "  Afogado: " + Current + "   [N] próxima", style);
    }
}
