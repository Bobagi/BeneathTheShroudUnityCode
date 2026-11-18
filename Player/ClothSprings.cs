using UnityEngine;

// Spring bones for loose cloth (04/10, Gustavo: "the clothes have no weight, they are glued to his body"): the
// jacket's skirt hangs on short bone chains (jacket_hem_*, made by SourceAssets/Tools/metahuman_outfit.py) that the
// animation does not move. Every frame, after the animation and the IK, each chain's joints are simulated as points:
// they keep their speed (inertia, damped), sag a little, are pulled back toward where the pose would put them, stay
// at the bones' lengths and are pushed out of capsules round the thighs and the hips. Then the bones are turned to
// the points. The hem lags behind a turn, swings with the step and rides over the thigh as it comes forward,
// instead of stretching with the leg like skin. No physics engine, a few dozen points: a fraction of a microsecond.
[DefaultExecutionOrder(20000)]
public sealed class ClothSprings : MonoBehaviour
{
    [System.Serializable]
    public struct Capsule
    {
        public Transform start;
        public Transform end;
        public float radius;
    }

    [Tooltip("The first bone of each chain; the chain follows the first child down.")]
    [SerializeField]
    private Transform[] roots;

    [SerializeField]
    private Capsule[] colliders;

    [Tooltip("How hard the cloth is pulled back to the pose each frame (0 floppy, 1 rigid).")]
    [Range(0f, 1f)]
    [SerializeField]
    private float stiffness = 0.12f;

    [Tooltip("Share of its speed a point loses each frame (air, the cloth's own friction).")]
    [Range(0f, 1f)]
    [SerializeField]
    private float damping = 0.22f;

    [Tooltip("Sag of the cloth (m/s^2), straight down.")]
    [SerializeField]
    private float gravity = 2.5f;

    private Transform[][] chains;
    private Quaternion[][] rest;
    private Vector3[][] points;
    private Vector3[][] previous;
    private float[][] lengths;
    private Vector3 lastRoot;
    private bool started;

    public void Configure(Transform[] chainRoots, Capsule[] capsules)
    {
        roots = chainRoots;
        colliders = capsules;
    }

    // Capsules the size of the clothes the hem hangs over (measured on ErickOutfit.fbx, 07/10: trousers and T-shirt
    // 0.16 to 0.17 m from the pelvis axis, the thigh in trousers about 0.11 at the top). Built here from the bones by
    // name, so a scene saved with other values gets these without a rebuild.
    public const float HipsRadius = 0.175f;
    public const float ThighRadius = 0.115f;

    private void Awake()
    {
        BuildCapsules();
        int n = roots != null ? roots.Length : 0;
        chains = new Transform[n][];
        rest = new Quaternion[n][];
        points = new Vector3[n][];
        previous = new Vector3[n][];
        lengths = new float[n][];
        for (int c = 0; c < n; c++)
        {
            System.Collections.Generic.List<Transform> list = new System.Collections.Generic.List<Transform>();
            for (Transform t = roots[c]; t != null; t = t.childCount > 0 ? t.GetChild(0) : null)
            {
                list.Add(t);
            }

            chains[c] = list.ToArray();
            rest[c] = new Quaternion[list.Count];
            lengths[c] = new float[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                rest[c][i] = list[i].localRotation;
            }

            points[c] = new Vector3[list.Count];
            previous[c] = new Vector3[list.Count];
        }
    }

    private void OnEnable()
    {
        started = false;
    }

    private void BuildCapsules()
    {
        System.Collections.Generic.Dictionary<string, Transform> bones = new System.Collections.Generic.Dictionary<string, Transform>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            bones[t.name] = t;
        }
        System.Collections.Generic.List<Capsule> list = new System.Collections.Generic.List<Capsule>();
        Transform pelvis, spine;
        if (bones.TryGetValue("pelvis", out pelvis) && bones.TryGetValue("spine_03", out spine))
        {
            list.Add(new Capsule { start = pelvis, end = spine, radius = HipsRadius });
        }
        foreach (string side in new[] { "l", "r" })
        {
            Transform thigh, calf;
            if (bones.TryGetValue("thigh_" + side, out thigh) && bones.TryGetValue("calf_" + side, out calf))
            {
                list.Add(new Capsule { start = thigh, end = calf, radius = ThighRadius });
            }
        }
        if (list.Count > 0)
        {
            colliders = list.ToArray();
        }
    }

    // The tip of bone i in the pose: its child's position, or for the last bone its own length on along it (the
    // length of the bone before it).
    private Vector3 Tip(Transform[] chain, int i)
    {
        if (i + 1 < chain.Length)
        {
            return chain[i + 1].position;
        }

        Vector3 along = i > 0 ? chain[i].position - chain[i - 1].position : chain[i].up * 0.1f;
        return chain[i].position + along;
    }

    private void LateUpdate()
    {
        if (chains == null || chains.Length == 0)
        {
            return;
        }

        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        if (dt <= 0f)
        {
            return;
        }

        // A jump (teleport, a cut in a cutscene): no swing from across the map.
        Vector3 root = transform.position;
        bool reset = !started || (root - lastRoot).sqrMagnitude > 1f;
        lastRoot = root;
        started = true;

        for (int c = 0; c < chains.Length; c++)
        {
            Transform[] chain = chains[c];
            for (int i = 0; i < chain.Length; i++)
            {
                chain[i].localRotation = rest[c][i];
            }

            for (int i = 0; i < chain.Length; i++)
            {
                Vector3 target = Tip(chain, i);
                Vector3 from = i == 0 ? chain[0].position : points[c][i - 1];
                lengths[c][i] = Vector3.Distance(i == 0 ? chain[0].position : Tip(chain, i - 1), target);
                if (reset)
                {
                    points[c][i] = target;
                    previous[c][i] = target;
                    continue;
                }

                Vector3 p = points[c][i];
                Vector3 velocity = (p - previous[c][i]) * (1f - damping);
                previous[c][i] = p;
                p += velocity + Vector3.down * gravity * dt * dt;
                p += (target - p) * stiffness;
                // The bone's length from the point above it.
                Vector3 d = p - from;
                float len = d.magnitude;
                if (len > 1e-5f)
                {
                    p = from + d * (lengths[c][i] / len);
                }

                for (int k = 0; k < colliders.Length; k++)
                {
                    p = PushOut(p, colliders[k]);
                }

                // Never deeper into the body than the pose itself: the skinned pose does not penetrate (the weights
                // came from MetaHuman), so the part of the deviation that points from the posed tip toward the body's
                // axis is removed and the hem only lags outward, sideways and down. Capsules alone left it inside the
                // trousers when walking (07/10).
                Vector3 axis = AxisPoint(target);
                Vector3 outward = target - axis;
                outward.y = 0f;
                if (outward.sqrMagnitude > 1e-6f)
                {
                    outward.Normalize();
                    float inward = Vector3.Dot(p - target, outward);
                    if (inward < 0f)
                    {
                        p -= outward * inward;
                    }
                }

                points[c][i] = p;
            }

            // Turn each bone from its posed direction to the simulated one.
            for (int i = 0; i < chain.Length; i++)
            {
                Vector3 head = chain[i].position;
                Vector3 posed = Tip(chain, i) - head;
                Vector3 now = points[c][i] - head;
                if (posed.sqrMagnitude > 1e-8f && now.sqrMagnitude > 1e-8f)
                {
                    chain[i].rotation = Quaternion.FromToRotation(posed, now) * chain[i].rotation;
                }
            }
        }
    }

    // The point of the body's axis (the hips capsule, else this transform's up axis) nearest to p.
    private Vector3 AxisPoint(Vector3 p)
    {
        for (int k = 0; k < colliders.Length; k++)
        {
            Capsule capsule = colliders[k];
            if (capsule.start == null || capsule.end == null || capsule.start.name != "pelvis")
            {
                continue;
            }
            Vector3 a = capsule.start.position;
            Vector3 ab = capsule.end.position - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return a + ab * t;
        }
        return transform.position + Vector3.up * (p.y - transform.position.y);
    }

    private static Vector3 PushOut(Vector3 p, Capsule capsule)
    {
        if (capsule.start == null || capsule.end == null)
        {
            return p;
        }

        Vector3 a = capsule.start.position;
        Vector3 ab = capsule.end.position - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        Vector3 closest = a + ab * t;
        Vector3 out_ = p - closest;
        float d = out_.magnitude;
        return d < capsule.radius && d > 1e-5f ? closest + out_ * (capsule.radius / d) : p;
    }
}
