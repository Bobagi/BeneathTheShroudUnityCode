using System.Collections.Generic;
using UnityEngine;

// Keeps a humanoid creature's claws out of the floor. Its hands are rigid (no finger bones) and the claws reach
// well past the palm, so a clip made for a flat human hand (Mixamo) pushes them into the ground whenever the palm
// is down. The real claw tips are read once from the skin (the vertices bound to each hand that lie farthest from
// it, in the hand's own space); after the animation (LateUpdate) only the wrist bends, about the axis across the
// hand, until the lowest tip is back above the ground, never more than maxBend degrees.
// Not humanoid IK: Unity's hand IK is full-body and, lifting the hands, it pitched the whole torso and threw the
// legs up in the crawls (02/10). A first version guessed the tip along the forearm and missed whenever the wrist
// was already flexed (claws 11 to 16 cm into the floor in the crawls, measured on real frames).
// After HitReactor (20) so a kicked arm is still kept out of the floor, before Ragdoll tracking (200).
[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(50)]
public sealed class HandGroundContact : MonoBehaviour
{
    [SerializeField]
    private float clearance = 0.01f;

    [SerializeField]
    private float maxBend = 75f;

    [SerializeField]
    private LayerMask ground = ~0;

    private sealed class Hand
    {
        public Transform bone;
        public Vector3[] tips;
    }

    private readonly List<Hand> hands = new List<Hand>();

    private void Awake()
    {
        Animator animator = GetComponent<Animator>();
        SkinnedMeshRenderer skin = GetComponentInChildren<SkinnedMeshRenderer>();
        foreach (HumanBodyBones id in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
        {
            Transform bone = animator.GetBoneTransform(id);
            if (bone == null || skin == null)
            {
                continue;
            }

            hands.Add(new Hand { bone = bone, tips = ClawTips(skin, bone) });
        }
    }

    // The 8 vertices bound mostly to this hand that lie farthest from the wrist, in the hand bone's space (from the
    // bind pose), spread out: one per claw, roughly.
    private static Vector3[] ClawTips(SkinnedMeshRenderer skin, Transform bone)
    {
        int index = System.Array.IndexOf(skin.bones, bone);
        Mesh mesh = skin.sharedMesh;
        if (index < 0 || mesh == null)
        {
            return new Vector3[0];
        }

        Matrix4x4 bind = mesh.bindposes[index];
        Vector3[] vertices = mesh.vertices;
        BoneWeight[] weights = mesh.boneWeights;
        List<Vector3> local = new List<Vector3>();
        for (int i = 0; i < vertices.Length; i++)
        {
            if (weights[i].boneIndex0 == index && weights[i].weight0 > 0.5f)
            {
                local.Add(bind.MultiplyPoint3x4(vertices[i]));
            }
        }

        local.Sort((a, b) => b.sqrMagnitude.CompareTo(a.sqrMagnitude));
        List<Vector3> tips = new List<Vector3>();
        foreach (Vector3 p in local)
        {
            bool apart = true;
            foreach (Vector3 t in tips)
            {
                if ((t - p).sqrMagnitude < 0.02f * 0.02f)
                {
                    apart = false;
                    break;
                }
            }

            if (apart)
            {
                tips.Add(p);
            }

            if (tips.Count == 8 || p.sqrMagnitude < 0.6f * (local.Count > 0 ? local[0].sqrMagnitude : 0f))
            {
                break;
            }
        }

        return tips.ToArray();
    }

    private void LateUpdate()
    {
        foreach (Hand hand in hands)
        {
            // Two passes: after the first bend another tip can become the lowest.
            for (int pass = 0; pass < 2; pass++)
            {
                if (!Bend(hand))
                {
                    break;
                }
            }
        }
    }

    private bool Bend(Hand hand)
    {
        if (hand.tips.Length == 0)
        {
            return false;
        }

        Vector3 wrist = hand.bone.position;
        Vector3 lowestTip = Vector3.zero;
        float worst = 0f;
        foreach (Vector3 local in hand.tips)
        {
            Vector3 tip = hand.bone.TransformPoint(local);
            float depth = GroundHeight(tip) + clearance - tip.y;
            if (depth > worst)
            {
                worst = depth;
                lowestTip = tip;
            }
        }

        if (worst <= 0f)
        {
            return false;
        }

        Vector3 reach = lowestTip - wrist;
        Vector3 side = Vector3.Cross(reach, Vector3.up);
        if (side.sqrMagnitude < 1e-6f || reach.magnitude < 0.02f)
        {
            return false;
        }

        side.Normalize();
        // Angle that raises this tip by 'worst', from its current pitch about the wrist.
        float length = reach.magnitude;
        float current = Mathf.Asin(Mathf.Clamp(reach.y / length, -1f, 1f));
        float wanted = Mathf.Asin(Mathf.Clamp((reach.y + worst) / length, -1f, 1f));
        float bend = Mathf.Min(maxBend, (wanted - current) * Mathf.Rad2Deg);
        if (bend <= 0.1f)
        {
            return false;
        }

        Quaternion lift = Quaternion.AngleAxis(bend, side);
        if ((lift * reach).y < reach.y)
        {
            lift = Quaternion.AngleAxis(-bend, side);
        }

        hand.bone.rotation = lift * hand.bone.rotation;
        return true;
    }

    private float GroundHeight(Vector3 at)
    {
        Vector3 from = new Vector3(at.x, transform.position.y + 1.2f, at.z);
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 3f, ground, QueryTriggerInteraction.Ignore)
            && !hit.transform.IsChildOf(transform))
        {
            return hit.point.y;
        }

        return transform.position.y;
    }
}
