using UnityEngine;

// Footsteps for a cutscene actor walking with the Starter Assets clips, which call OnFootstep / OnLand from
// animation events (without a receiver every step logged an error). The ground under the actor picks the sound:
// boards (the pier, its ramp, the porch: colliders named in `woodNames`) or wet earth (anything else, the island's
// terrain). Only the clip with most weight in a blend sounds, as the Starter Assets controller does. Level from
// GameTuning.footstepVolume, like the player's steps. No allocation per step.
public sealed class CutsceneFootsteps : MonoBehaviour
{
    [SerializeField]
    private AudioSource source;

    [SerializeField]
    private AudioClip[] wood = new AudioClip[0];

    [SerializeField]
    private AudioClip[] earth = new AudioClip[0];

    [SerializeField]
    private string[] woodNames = { "Deck", "Ramp", "Porch" };

    private readonly RaycastHit[] hits = new RaycastHit[4];
    private int last = -1;

    private void OnFootstep(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight < 0.5f || source == null)
        {
            return;
        }

        AudioClip[] clips = OnWood() ? wood : earth;
        if (clips.Length == 0)
        {
            return;
        }

        int pick = Random.Range(0, clips.Length);
        if (pick == last && clips.Length > 1)
        {
            pick = (pick + 1) % clips.Length;
        }

        last = pick;
        source.pitch = Random.Range(0.93f, 1.07f);
        source.PlayOneShot(clips[pick], GameTuning.Instance.footstepVolume);
    }

    // The Starter Assets jump/land event: nobody jumps in the cutscenes.
    private void OnLand(AnimationEvent animationEvent)
    {
    }

    private bool OnWood()
    {
        int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * 0.5f, Vector3.down, hits, 1.2f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool wooden = false;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].distance >= best)
            {
                continue;
            }

            best = hits[i].distance;
            wooden = false;
            string name = hits[i].collider.name;
            foreach (string w in woodNames)
            {
                wooden |= name == w;
            }
        }

        return wooden;
    }
}
