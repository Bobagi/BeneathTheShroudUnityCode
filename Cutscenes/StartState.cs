using UnityEngine;

// The Animator state an actor of a cutscene starts in (sitting in the boat, standing), before any cue of the
// timeline asks for another. Applied every time the actor is switched on: the timeline's activation track turns
// the actors on and off, and a re-enabled Animator restarts in its default state (Idle), which left Erick standing
// in the boat (03/10) when this ran only in Start.
[RequireComponent(typeof(Animator))]
public sealed class StartState : MonoBehaviour
{
    public string state = "Idle";

    private void OnEnable()
    {
        GetComponent<Animator>().Play(state, 0, Random.value);
    }
}
