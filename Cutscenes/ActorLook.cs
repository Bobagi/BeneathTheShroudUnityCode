using UnityEngine;

// A cutscene actor looks at something (the lit window from the boat, the door at the end of the path): humanoid
// look-at IK over whatever clip plays, head and a little of the chest, eased in and out so it never snaps. A small
// slow drift of the aim keeps him from staring like a statue while the shot holds. The animator's layer needs its
// IK pass on (the arrival builder makes the controller that way).
[RequireComponent(typeof(Animator))]
public sealed class ActorLook : MonoBehaviour
{
    [SerializeField]
    private Transform target;

    [Range(0f, 1f)]
    [SerializeField]
    private float weight = 0.8f;

    [SerializeField]
    private float easeSeconds = 1.2f;

    private Animator animator;
    private float current;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        float want = target != null ? weight : 0f;
        current = Mathf.MoveTowards(current, want, Time.deltaTime / Mathf.Max(0.05f, easeSeconds));
        if (current <= 0.001f || target == null)
        {
            animator.SetLookAtWeight(0f);
            return;
        }

        float t = Time.time;
        Vector3 drift = new Vector3(Mathf.PerlinNoise(t * 0.15f, 1.3f) - 0.5f, (Mathf.PerlinNoise(2.7f, t * 0.12f) - 0.5f) * 0.6f, 0f) * 1.5f;
        animator.SetLookAtWeight(current, 0.15f, 0.9f, 1f, 0.6f);
        animator.SetLookAtPosition(target.position + drift);
    }
}
