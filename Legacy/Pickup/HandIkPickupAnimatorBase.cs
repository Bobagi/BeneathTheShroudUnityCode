using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Serialization;

public abstract class HandIkPickupAnimatorBase : MonoBehaviour
{
    [Header("Rigging")]
    [SerializeField]
    [FormerlySerializedAs("rightHandIkTargetTransform")]
    private Transform handIkTargetTransform;

    public Transform HandIkTargetTransform => handIkTargetTransform;

    [SerializeField]
    private Rig reachRigLayer;

    [Header("Position References")]
    [SerializeField]
    private Transform handNormalPositionReferenceTransform;

    public Transform HandNormalPositionReferenceTransform => handNormalPositionReferenceTransform;

    [System.Serializable]
    private struct ItemRelaxedReference
    {
        public InteractablePickupItemType itemType;
        public Transform relaxedReferenceTransform;
    }

    [SerializeField]
    private Transform handRaisedPositionReferenceTransform;

    public Transform HandRaisedPositionReferenceTransform => handRaisedPositionReferenceTransform;

    [System.Serializable]
    private struct ItemRaisedReference
    {
        public InteractablePickupItemType itemType;
        public Transform raisedReferenceTransform;
    }

    [SerializeField]
    private ItemRelaxedReference[] itemRelaxedReferences;

    [SerializeField]
    private ItemRaisedReference[] itemRaisedReferences;

    [SerializeField]
    [FormerlySerializedAs("rightArmReachTwoBoneIkConstraint")]
    private TwoBoneIKConstraint handReachTwoBoneIkConstraint;

    [Header("Reach Settings")]
    [SerializeField]
    private float reachRigWeightWhenActive = 1f;

    [SerializeField]
    private float returnToOriginalSeconds = 0.12f;

    [Header("Target Smoothing")]
    [SerializeField]
    private float targetFollowSmoothingSeconds = 0.06f;

    private Coroutine currentReachCoroutine;
    private Vector3 handIkTargetPositionVelocity;

    public Transform GetRaisedReferenceForItem(InteractablePickupItemType itemType)
    {
        if (itemRaisedReferences != null)
        {
            foreach (ItemRaisedReference reference in itemRaisedReferences)
            {
                if (reference.itemType == itemType && reference.raisedReferenceTransform != null)
                {
                    return reference.raisedReferenceTransform;
                }
            }
        }

        return handRaisedPositionReferenceTransform;
    }

    public Transform GetRelaxedReferenceForItem(InteractablePickupItemType itemType)
    {
        if (itemRelaxedReferences != null)
        {
            foreach (ItemRelaxedReference reference in itemRelaxedReferences)
            {
                if (reference.itemType == itemType && reference.relaxedReferenceTransform != null)
                {
                    return reference.relaxedReferenceTransform;
                }
            }
        }

        return handNormalPositionReferenceTransform;
    }

    public IEnumerator PlayReachToTargetCoroutine(
        Transform worldTargetTransform,
        float animationSeconds
    )
    {
        if (worldTargetTransform == null)
        {
            yield break;
        }

        if (handIkTargetTransform == null)
        {
            yield break;
        }

        if (reachRigLayer == null || handReachTwoBoneIkConstraint == null)
        {
            yield break;
        }

        float clampedAnimationSeconds = Mathf.Max(0.01f, animationSeconds);

        if (currentReachCoroutine != null)
        {
            StopCoroutine(currentReachCoroutine);
        }

        currentReachCoroutine = StartCoroutine(
            ReachCoroutine(worldTargetTransform, clampedAnimationSeconds)
        );
        yield return currentReachCoroutine;
        currentReachCoroutine = null;
    }

    private IEnumerator ReachCoroutine(Transform worldTargetTransform, float animationSeconds)
    {
        Vector3 originalTargetPosition = handIkTargetTransform.position;
        Quaternion originalTargetRotation = handIkTargetTransform.rotation;

        reachRigLayer.weight = reachRigWeightWhenActive;
        handReachTwoBoneIkConstraint.weight = 1f;

        Vector3 reachEndPosition = worldTargetTransform.position;
        Quaternion reachEndRotation = worldTargetTransform.rotation;

        float elapsedSeconds = 0f;

        while (elapsedSeconds < animationSeconds)
        {
            elapsedSeconds += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedSeconds / animationSeconds);
            float smoothedTime = normalizedTime * normalizedTime * (3f - (2f * normalizedTime));

            Vector3 desiredPosition = Vector3.Lerp(
                originalTargetPosition,
                reachEndPosition,
                smoothedTime
            );
            Quaternion desiredRotation = Quaternion.Slerp(
                originalTargetRotation,
                reachEndRotation,
                smoothedTime
            );

            float clampedFollowSeconds = Mathf.Max(0.01f, targetFollowSmoothingSeconds);
            handIkTargetTransform.position = Vector3.SmoothDamp(
                handIkTargetTransform.position,
                desiredPosition,
                ref handIkTargetPositionVelocity,
                clampedFollowSeconds
            );

            handIkTargetTransform.rotation = Quaternion.Slerp(
                handIkTargetTransform.rotation,
                desiredRotation,
                1f - Mathf.Exp(-Time.deltaTime / clampedFollowSeconds)
            );

            yield return null;
        }

        handIkTargetTransform.position = reachEndPosition;
        handIkTargetTransform.rotation = reachEndRotation;

        float clampedReturnSeconds = Mathf.Max(0.01f, returnToOriginalSeconds);
        float returnElapsedSeconds = 0f;
        handIkTargetPositionVelocity = Vector3.zero;

        while (returnElapsedSeconds < clampedReturnSeconds)
        {
            returnElapsedSeconds += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(returnElapsedSeconds / clampedReturnSeconds);
            float smoothedTime = normalizedTime * normalizedTime * (3f - (2f * normalizedTime));

            Vector3 desiredPosition = Vector3.Lerp(
                reachEndPosition,
                originalTargetPosition,
                smoothedTime
            );
            Quaternion desiredRotation = Quaternion.Slerp(
                reachEndRotation,
                originalTargetRotation,
                smoothedTime
            );

            float clampedFollowSeconds = Mathf.Max(0.01f, targetFollowSmoothingSeconds);
            handIkTargetTransform.position = Vector3.SmoothDamp(
                handIkTargetTransform.position,
                desiredPosition,
                ref handIkTargetPositionVelocity,
                clampedFollowSeconds
            );

            handIkTargetTransform.rotation = Quaternion.Slerp(
                handIkTargetTransform.rotation,
                desiredRotation,
                1f - Mathf.Exp(-Time.deltaTime / clampedFollowSeconds)
            );

            yield return null;
        }

        handIkTargetTransform.position = originalTargetPosition;
        handIkTargetTransform.rotation = originalTargetRotation;

        handReachTwoBoneIkConstraint.weight = 0f;
        reachRigLayer.weight = 0f;
    }
}
