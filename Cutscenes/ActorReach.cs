using UnityEngine;

// A cutscene actor reaches for something with the right hand and grips it (the door handle at the end of the
// arrival). Analytic two-bone IK on the arm alone, after the animation (LateUpdate), as the game's PlayerBody does:
// the humanoid IK of the Animator is whole-body and leaned him 20 cm into the door (03/10), putting the
// first-person camera against it. The hand is turned so the palm faces the grip and the fingers point along it,
// from the hand's own bones (middle finger and thumb), never from guessed axes (guessing put the hand sideways
// twice in the game). Then the fingers close round it. `Weight` (0..1) comes from the timeline (a Reach cue).
[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(50)]
public sealed class ActorReach : MonoBehaviour
{
    [Tooltip("Where the palm's centre goes: forward = the way the fingers point round the handle, up = out of the back of the hand.")]
    [SerializeField]
    private Transform grip;

    [Tooltip("Degrees each finger joint closes at full weight: base, middle, tip.")]
    [SerializeField]
    private Vector3 curl = new Vector3(50f, 65f, 40f);

    private Transform upper;
    private Transform fore;
    private Transform hand;
    private Transform middle;
    private Transform thumb;
    private readonly Transform[] fingerBones = new Transform[12];
    // Plain finger bones (not Humanoid, see HandBones): nothing poses them but this, so each frame starts from their
    // rest rotations and every joint closes about its own hinge. Null when the Animator poses the fingers.
    private Quaternion[] fingerRest;
    private readonly float[] bend = new float[4];
    private bool closed;

    public float Weight { get; set; }

    public Transform Grip
    {
        get => grip;
        set => grip = value;
    }

    private void Awake()
    {
        Animator animator = GetComponent<Animator>();
        upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        fore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        middle = HandBones.Find(animator, false, 2, 0);
        thumb = HandBones.Find(animator, false, 0, 0);
        for (int i = 0; i < fingerBones.Length; i++)
        {
            // Index to little finger, three bones each.
            fingerBones[i] = HandBones.Find(animator, false, 1 + i / 3, i % 3);
        }
        if (animator.GetBoneTransform(HumanBodyBones.RightIndexProximal) == null && System.Array.TrueForAll(fingerBones, b => b != null))
        {
            fingerRest = System.Array.ConvertAll(fingerBones, b => b.localRotation);
            for (int f = 0; f < 4; f++)
            {
                bend[f] = HandBones.BendSign(fingerBones[f * 3 + 1]);
            }
        }
    }

    private void OnEnable()
    {
        Weight = 0f;
    }

    private void LateUpdate()
    {
        float w = Mathf.Clamp01(Weight);
        if (closed && fingerRest != null)
        {
            for (int i = 0; i < fingerBones.Length; i++)
            {
                fingerBones[i].localRotation = fingerRest[i];
            }
            closed = false;
        }
        if (w <= 0.001f || grip == null || upper == null || fore == null || hand == null || middle == null || thumb == null)
        {
            return;
        }

        // The hand's own axes this frame (the clip's pose): fingers, out of the palm, and the palm's centre.
        Vector3 finger = (middle.position - hand.position).normalized;
        Vector3 palm = Vector3.Cross(finger, (thumb.position - hand.position).normalized).normalized;
        Vector3 palmCentre = (middle.position - hand.position) * 0.55f + palm * 0.025f;

        // The hand rotation that lays those axes onto the grip's (fingers along forward, back of the hand up).
        Quaternion turn = Quaternion.LookRotation(grip.forward, grip.up) * Quaternion.Inverse(Quaternion.LookRotation(finger, -palm));
        Quaternion handTarget = Quaternion.Slerp(Quaternion.identity, turn, w) * hand.rotation;
        Vector3 wristTarget = Vector3.Lerp(hand.position, grip.position - turn * palmCentre, w);

        SolveArm(wristTarget);
        hand.rotation = handTarget;

        // Fingers close as the hand arrives (the last third of the reach), toward the palm.
        float close = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, w));
        if (close <= 0f)
        {
            return;
        }

        Vector3 handFinger = (middle.position - hand.position).normalized;
        Vector3 handPalm = Vector3.Cross(handFinger, (thumb.position - hand.position).normalized).normalized;
        Vector3 axisWorld = Vector3.Cross(handFinger, handPalm).normalized;
        for (int i = 0; i < fingerBones.Length; i++)
        {
            Transform b = fingerBones[i];
            if (b == null)
            {
                continue;
            }

            float angle = (i % 3 == 0 ? curl.x : i % 3 == 1 ? curl.y : curl.z) * close;
            if (fingerRest != null)
            {
                b.localRotation = fingerRest[i] * Quaternion.Euler(0f, 0f, bend[i / 3] * angle);
                closed = true;
                continue;
            }
            b.rotation = Quaternion.AngleAxis(angle, axisWorld) * b.rotation;
        }
    }

    // Shoulder, elbow and wrist: the elbow is placed by the law of cosines in the plane of the arm's current bend
    // (so it keeps hanging down and out, as the clip has it), then the forearm is turned onto the target.
    private void SolveArm(Vector3 target)
    {
        Vector3 s = upper.position;
        float a = (fore.position - s).magnitude;
        float b = (hand.position - fore.position).magnitude;
        Vector3 toTarget = target - s;
        float c = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
        Vector3 dir = toTarget.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(fore.position - s, dir);
        if (bend.sqrMagnitude < 1e-6f)
        {
            bend = Vector3.ProjectOnPlane(Vector3.down, dir);
        }

        bend.Normalize();
        float cosA = Mathf.Clamp((a * a + c * c - b * b) / (2f * a * c), -1f, 1f);
        Vector3 elbow = s + dir * (a * cosA) + bend * (a * Mathf.Sqrt(1f - cosA * cosA));
        upper.rotation = Quaternion.FromToRotation(fore.position - s, elbow - s) * upper.rotation;
        Vector3 reach = s + dir * c;
        fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, reach - fore.position) * fore.rotation;
    }
}
