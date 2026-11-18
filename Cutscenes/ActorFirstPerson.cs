using UnityEngine;
using UnityEngine.Rendering;

// The cutscene's camera goes into an actor's eyes (third person to first person at the door): his head, hair,
// beard and eyes stop being drawn and only cast their shadow, as the game's own first-person body does, so the
// camera never shows the inside of his head. And a faint cool light under his eyes comes up, the same trick as the
// game's DarkVision (Amnesia's): under the porch roof, out of the moon, the door and his hand were black
// (mean luminance 0.008, 03/10). Switched by a FirstPerson cue near the end of the camera blend; back to normal
// whenever the actor is switched on again (watching the cutscene again).
public sealed class ActorFirstPerson : MonoBehaviour
{
    [SerializeField]
    private Renderer[] head = new Renderer[0];

    [SerializeField]
    private Light fill;

    [Tooltip("His eyes (the first-person camera's point): the fill sits behind and above them, by the actor's facing.")]
    [SerializeField]
    private Transform eyes;

    [SerializeField]
    private float fillIntensity = 3.2f;

    [SerializeField]
    private float fillFadeSeconds = 1.2f;

    private float fillTarget;

    private void OnEnable()
    {
        Set(false);
        if (fill != null)
        {
            fill.intensity = 0f;
            fill.enabled = false;
        }
    }

    public void Set(bool firstPerson)
    {
        foreach (Renderer r in head)
        {
            if (r != null)
            {
                r.shadowCastingMode = firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            }
        }

        fillTarget = firstPerson ? fillIntensity : 0f;
    }

    private void Update()
    {
        if (fill == null)
        {
            return;
        }

        fill.intensity = Mathf.MoveTowards(fill.intensity, fillTarget, fillIntensity * Time.deltaTime / Mathf.Max(0.05f, fillFadeSeconds));
        fill.enabled = fill.intensity > 0.001f;
        // Placed by the actor's root (it faces the way he walks), not by the head bone: hung on the head, the
        // clip's head turn and the bone's centimetre scale put it in front of his face, under the porch roof.
        if (fill.enabled && eyes != null)
        {
            fill.transform.position = eyes.position + Vector3.up * 0.8f - transform.forward * 1.1f;
        }
    }
}
