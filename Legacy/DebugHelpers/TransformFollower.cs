using UnityEngine;

public sealed class TransformFollower : MonoBehaviour
{
    [SerializeField]
    private Transform transformToFollowTarget;

    [SerializeField]
    private Transform transformToMove;

    [SerializeField]
    private bool followPosition = true;

    [SerializeField]
    private bool followRotation = true;

    [SerializeField]
    private bool followScale = false;

    [SerializeField]
    private bool useLocalSpace = false;

    private void LateUpdate()
    {
        if (transformToFollowTarget == null || transformToMove == null)
        {
            return;
        }

        if (useLocalSpace)
        {
            if (followPosition)
            {
                transformToMove.localPosition = transformToFollowTarget.localPosition;
            }

            if (followRotation)
            {
                transformToMove.localRotation = transformToFollowTarget.localRotation;
            }

            if (followScale)
            {
                transformToMove.localScale = transformToFollowTarget.localScale;
            }

            return;
        }

        if (followPosition)
        {
            transformToMove.position = transformToFollowTarget.position;
        }

        if (followRotation)
        {
            transformToMove.rotation = transformToFollowTarget.rotation;
        }

        if (followScale)
        {
            transformToMove.localScale = transformToFollowTarget.localScale;
        }
    }

    public void SetFollowConfiguration(
        Transform newTransformToMove,
        Transform newTransformToFollowTarget,
        bool shouldFollowPosition,
        bool shouldFollowRotation,
        bool shouldFollowScale,
        bool shouldUseLocalSpace
    )
    {
        transformToMove = newTransformToMove;
        transformToFollowTarget = newTransformToFollowTarget;
        followPosition = shouldFollowPosition;
        followRotation = shouldFollowRotation;
        followScale = shouldFollowScale;
        useLocalSpace = shouldUseLocalSpace;
    }
}
