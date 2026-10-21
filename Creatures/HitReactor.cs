using System.Collections.Generic;
using UnityEngine;

// Physical-looking hit reactions on top of any animation: a bullet gives the bone it struck (and, less, the
// bones it hangs from) a kick that turns it the way the bullet pushed that point, then a damped spring brings
// it back. A shoulder shot spins the shoulder back, a head shot snaps the head, a shot to the gut folds the
// spine, a thigh shot buckles the leg, all while the walk or the attack keeps playing. Runs after the
// animation (LateUpdate) and before the claw and ragdoll tracking.
[DefaultExecutionOrder(20)]
public sealed class HitReactor : MonoBehaviour
{
    [Tooltip("Spring stiffness (1/s^2): how fast a kicked bone returns.")]
    [SerializeField]
    private float stiffness = 140f;

    [Tooltip("Damping (1/s): 2*sqrt(stiffness) is critical; a little under lets it overshoot once.")]
    [SerializeField]
    private float damping = 17f;

    [Tooltip("Degrees of kick per unit of shot impulse at the struck bone.")]
    [SerializeField]
    private float degreesPerImpulse = 9f;

    [SerializeField]
    private float maxAngle = 45f;

    private sealed class Kick
    {
        public Transform bone;
        public Vector3 localAxis;
        public float angle;
        public float speed;
    }

    private readonly List<Kick> kicks = new List<Kick>();
    private Animator animator;
    private Transform hips;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
    }

    // A shot struck 'bone' at 'point' going 'direction' with 'impulse'.
    public void React(Transform bone, Vector3 point, Vector3 direction, float impulse)
    {
        float weight = 1f;
        Transform current = bone;
        // The struck bone, then up the chain toward the hips, less each step (at most 3 bones).
        for (int step = 0; step < 3 && current != null && current != transform; step++)
        {
            Vector3 lever = point - current.position;
            Vector3 axis = Vector3.Cross(lever, direction);
            if (axis.sqrMagnitude > 1e-6f)
            {
                float kick = Mathf.Min(maxAngle, impulse * degreesPerImpulse * weight / Mathf.Max(0.15f, lever.magnitude * 3f));
                Add(current, axis.normalized, kick);
            }

            if (current == hips)
            {
                break;
            }

            current = current.parent;
            weight *= 0.5f;
        }
    }

    private void Add(Transform bone, Vector3 worldAxis, float degrees)
    {
        Vector3 localAxis = bone.InverseTransformDirection(worldAxis);
        foreach (Kick k in kicks)
        {
            if (k.bone == bone && Vector3.Dot(k.localAxis, localAxis) > 0.7f)
            {
                // The same bone hit again the same way: the kicks pile up, with a cap.
                k.speed += degrees * stiffness * 0.11f;
                return;
            }
        }

        // An impulse: the bone starts at rest and is given speed; it swings out about 'degrees'.
        kicks.Add(new Kick { bone = bone, localAxis = localAxis, angle = 0f, speed = degrees * Mathf.Sqrt(stiffness) * 1.15f });
    }

    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        for (int i = kicks.Count - 1; i >= 0; i--)
        {
            Kick k = kicks[i];
            k.speed += (-stiffness * k.angle - damping * k.speed) * dt;
            k.angle = Mathf.Clamp(k.angle + k.speed * dt, -maxAngle, maxAngle);
            if (Mathf.Abs(k.angle) < 0.05f && Mathf.Abs(k.speed) < 0.5f)
            {
                kicks.RemoveAt(i);
                continue;
            }

            k.bone.rotation = Quaternion.AngleAxis(k.angle, k.bone.TransformDirection(k.localAxis)) * k.bone.rotation;
        }
    }

    public void Clear()
    {
        kicks.Clear();
    }
}
