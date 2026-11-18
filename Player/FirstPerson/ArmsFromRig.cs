using System.Collections.Generic;
using UnityEngine;

// Copies the first person arms rig (FirstPersonArms, under the camera) onto the player's body, bone by bone, after
// the body's own animation: plain FK, no IK. For each arm in use (FirstPersonArms.RightArmWeight / LeftArmWeight):
//   clavicle    turned toward the rig's shoulder joint (the least stretch of the shoulder's skin);
//   upper arm   moved onto the rig's shoulder joint (the rig hangs from the camera, the body from its spine, so the
//               joint shifts a few centimetres as the view pitches: the arms stay on the screen, like any shooter)
//               and turned as the rig's;
//   forearm, hand and the fifteen finger bones  turned as the rig's.
// With the weight at 0 the arm is the body's own (walk swing, swim stroke, climbing) and the shoulder goes back to
// its rest offset. Runs in the editor too, so scrubbing a clip on the rig in the Animation window moves the body's
// arms in the Scene view.
[ExecuteAlways]
[DefaultExecutionOrder(110)]
public sealed class ArmsFromRig : MonoBehaviour
{
    [SerializeField]
    private FirstPersonArms rig;

    private sealed class Arm
    {
        public Transform clavicle;
        public Transform upper;
        public Transform[] chain = new Transform[0];
        public Transform[] rigChain = new Transform[0];
        public Transform rigUpper;
        public Vector3 upperRest;
        public float applied;
    }

    private readonly Arm right = new Arm();
    private readonly Arm left = new Arm();
    private bool ready;

    // Set by PlayerBody each frame: whether the body's state allows the copy at all (not while a root motion clip
    // such as the climb owns the arms). 1 by default.
    public float Allow { get; set; } = 1f;

    public FirstPersonArms Rig
    {
        get => rig;
        set
        {
            rig = value;
            ready = false;
        }
    }

    private void OnEnable()
    {
        ready = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
        UnityEditor.EditorApplication.update += EditorTick;
#endif
    }

#if UNITY_EDITOR
    private void OnDisable()
    {
        UnityEditor.EditorApplication.update -= EditorTick;
    }

    // Outside Play, every editor tick while the Animation window previews a clip (AnimationMode) or while the rig (or
    // one of its bones) is selected: the body's arms follow the rig being scrubbed or posed by hand. The player loop
    // does not run reliably in the editor, so this does not wait for LateUpdate.
    private void EditorTick()
    {
        if (this == null || Application.isPlaying || rig == null)
        {
            return;
        }

        bool rigSelected = UnityEditor.Selection.activeTransform != null && UnityEditor.Selection.activeTransform.IsChildOf(rig.transform);
        if (!UnityEditor.AnimationMode.InAnimationMode() && !rigSelected)
        {
            return;
        }

        if (!ready)
        {
            Prepare();
            if (!ready)
            {
                return;
            }
        }

        Apply(right, 1f);
        Apply(left, 1f);
    }
#endif

    private void Prepare()
    {
        if (rig == null)
        {
            return;
        }

        Animator animator = GetComponent<Animator>();
        if (animator == null || animator.avatar == null)
        {
            return;
        }

        Bind(right, animator, false);
        Bind(left, animator, true);
        ready = right.upper != null && left.upper != null;
    }

    private void Bind(Arm arm, Animator animator, bool isLeft)
    {
        arm.clavicle = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder);
        arm.upper = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        if (arm.upper == null)
        {
            return;
        }

        arm.upperRest = arm.upper.localPosition;
        arm.rigUpper = rig.Bone(arm.upper.name);
        List<Transform> body = new List<Transform>();
        List<Transform> mirror = new List<Transform>();
        foreach (Transform t in arm.upper.GetComponentsInChildren<Transform>(true))
        {
            Transform r = rig.Bone(t.name);
            if (r != null && t != arm.upper)
            {
                body.Add(t);
                mirror.Add(r);
            }
        }

        arm.chain = body.ToArray();
        arm.rigChain = mirror.ToArray();
    }

    private void LateUpdate()
    {
        if (!ready)
        {
            Prepare();
            if (!ready)
            {
                return;
            }
        }

        float allow = Application.isPlaying ? Allow : 1f;
#if UNITY_EDITOR
        // In the editor only while the Animation window previews a clip on the rig (or in Play): a body at rest is
        // left alone.
        if (!Application.isPlaying && !UnityEditor.AnimationMode.InAnimationMode())
        {
            return;
        }
#endif
        Apply(right, (Application.isPlaying ? rig.RightArmWeight : 1f) * allow);
        Apply(left, (Application.isPlaying ? rig.LeftArmWeight : 1f) * allow);
    }

    private static void Apply(Arm arm, float weight)
    {
        weight = Mathf.Clamp01(weight);
        if (weight <= 0.0005f)
        {
            if (arm.applied > 0f)
            {
                arm.upper.localPosition = arm.upperRest;
                arm.applied = 0f;
            }

            return;
        }

        arm.applied = weight;
        Vector3 shoulder = Vector3.Lerp(arm.upper.position, arm.rigUpper.position, weight);
        if (arm.clavicle != null)
        {
            Vector3 from = arm.upper.position - arm.clavicle.position;
            Vector3 to = shoulder - arm.clavicle.position;
            if (from.sqrMagnitude > 1e-8f && to.sqrMagnitude > 1e-8f)
            {
                arm.clavicle.rotation = Quaternion.FromToRotation(from, to) * arm.clavicle.rotation;
            }
        }

        arm.upper.position = shoulder;
        arm.upper.rotation = Quaternion.Slerp(arm.upper.rotation, arm.rigUpper.rotation, weight);
        for (int i = 0; i < arm.chain.Length; i++)
        {
            arm.chain[i].localRotation = Quaternion.Slerp(arm.chain[i].localRotation, arm.rigChain[i].localRotation, weight);
        }
    }
}
