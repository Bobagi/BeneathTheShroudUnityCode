using UnityEngine;

// Curls the right hand's fingers around whatever it holds. Runs after animation and IK, bending
// each finger segment toward the palm; bend axis and direction are derived from the live bone
// positions, so it works regardless of how the rig's bones are oriented.
[DefaultExecutionOrder(200)]
public sealed class HandGripPose : MonoBehaviour
{
    [SerializeField]
    private PlayerInventory inventory;

    [SerializeField]
    private Transform hand;

    [SerializeField]
    [Tooltip("Proximal bones of index, middle, ring and pinky (children are curled with them).")]
    private Transform[] fingerRoots;

    [SerializeField]
    private Transform thumbRoot;

    [SerializeField]
    private Vector3 segmentCurlDegrees = new Vector3(55f, 70f, 45f);

    [SerializeField]
    private float thumbCurlDegrees = 35f;

    [SerializeField]
    private float blendSpeed = 10f;

    [SerializeField]
    [Tooltip("Flip if the fingers bend backwards on this rig.")]
    private bool invertCurl;

    private float weight;

    private void LateUpdate()
    {
        bool holding = inventory != null && inventory.HeldItem != null;
        weight = Mathf.MoveTowards(weight, holding ? 1f : 0f, blendSpeed * Time.deltaTime);
        if (weight <= 0f || hand == null || fingerRoots == null || fingerRoots.Length < 2)
        {
            return;
        }

        // Palm frame from the knuckles (right hand): "across" runs index -> pinky, fingers point away
        // from the wrist; their cross product faces out of the BACK of the hand, so negate it.
        Transform first = fingerRoots[0];
        Transform last = fingerRoots[fingerRoots.Length - 1];
        Vector3 across = (last.position - first.position).normalized;
        Vector3 knuckles = (first.position + last.position) * 0.5f;
        Vector3 fingerDirection = (knuckles - hand.position).normalized;
        Vector3 palmNormal = -Vector3.Cross(fingerDirection, across).normalized;

        // Pick the rotation sign that swings fingertips toward the palm.
        Vector3 axis = across;
        Vector3 tipMotion = Quaternion.AngleAxis(30f, axis) * fingerDirection - fingerDirection;
        float sign = Vector3.Dot(tipMotion, palmNormal) >= 0f ? 1f : -1f;
        if (invertCurl)
        {
            sign = -sign;
        }

        foreach (Transform root in fingerRoots)
        {
            Transform segment = root;
            for (int i = 0; i < 3 && segment != null; i++)
            {
                float angle = segmentCurlDegrees[i] * weight * sign;
                segment.rotation = Quaternion.AngleAxis(angle, axis) * segment.rotation;
                segment = segment.childCount > 0 ? segment.GetChild(0) : null;
            }
        }

        if (thumbRoot != null)
        {
            Vector3 thumbAxis = Vector3.Cross(palmNormal, (thumbRoot.position - hand.position).normalized);
            thumbRoot.rotation = Quaternion.AngleAxis(thumbCurlDegrees * weight, thumbAxis) * thumbRoot.rotation;
        }
    }
}
