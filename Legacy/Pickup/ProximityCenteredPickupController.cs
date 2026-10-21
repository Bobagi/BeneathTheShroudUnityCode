using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ProximityCenteredPickupController : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField]
    private LayerMask interactableLayerMask;

    [SerializeField]
    private float pickupSearchRadiusMeters = 2.2f;

    [Header("Selection Weights")]
    [SerializeField]
    private float viewCenterDotWeight = 0.85f;

    [SerializeField]
    private float distanceWeight = 0.15f;

    [Header("UI")]
    [SerializeField]
    private TMP_Text pickupPromptText;

    [SerializeField]
    private string pickupPromptFormat = "Press {0} to pick up {1}";

    [SerializeField]
    private string pickupInputDisplayName = "E";

    [Header("Input System")]
    [SerializeField]
    private InputActionReference pickupInputActionReference;

    [Header("Pickup Animation")]
    [SerializeField]
    private float defaultPickupAnimationSeconds = 0.35f;

    [SerializeField]
    private HandIkPickupAnimatorBase rightHandIkPickupAnimator;

    [SerializeField]
    private HandIkPickupAnimatorBase leftHandIkPickupAnimator;

    [Header("Pickup Result Handling")]
    [SerializeField]
    private PickedUpItemTypeHandler pickedUpItemTypeHandler;

    [Header("Camera")]
    [SerializeField]
    private Camera playerCamera;

    private readonly Collider[] overlapResults = new Collider[32];

    private InteractablePickupItem currentlySelectedPickupItem;
    private bool isPickupInProgress;

    private void OnEnable()
    {
        if (pickupInputActionReference != null)
        {
            pickupInputActionReference.action.Enable();
            pickupInputActionReference.action.started += HandlePickupActionStarted;
        }

        UpdatePromptVisibility(false);
    }

    private void OnDisable()
    {
        if (pickupInputActionReference != null)
        {
            pickupInputActionReference.action.started -= HandlePickupActionStarted;
            pickupInputActionReference.action.Disable();
        }

        UpdatePromptVisibility(false);
    }

    private void Update()
    {
        if (isPickupInProgress)
        {
            currentlySelectedPickupItem = null;
            UpdatePromptVisibility(false);
            return;
        }

        currentlySelectedPickupItem = FindBestPickupItemCandidate();
        UpdatePromptForCurrentCandidate();
    }

    public void TryPickup(float pickupAnimationSeconds)
    {
        if (isPickupInProgress)
        {
            return;
        }

        InteractablePickupItem selectedPickupItem = currentlySelectedPickupItem;
        if (selectedPickupItem == null)
        {
            return;
        }

        if (!selectedPickupItem.CanBePickedUp())
        {
            return;
        }

        float clampedPickupAnimationSeconds = Mathf.Max(0.01f, pickupAnimationSeconds);
        PickupHandSide pickupHandSide = ResolvePickupHandSide();
        StartCoroutine(
            PerformPickupSequenceCoroutine(
                selectedPickupItem,
                clampedPickupAnimationSeconds,
                pickupHandSide
            )
        );
    }

    private void HandlePickupActionStarted(InputAction.CallbackContext callbackContext)
    {
        TryPickup(defaultPickupAnimationSeconds);
    }

    private System.Collections.IEnumerator PerformPickupSequenceCoroutine(
        InteractablePickupItem pickupItem,
        float pickupAnimationSeconds,
        PickupHandSide pickupHandSide
    )
    {
        isPickupInProgress = true;
        UpdatePromptVisibility(false);

        HandIkPickupAnimatorBase pickupAnimator = ResolvePickupAnimator(pickupHandSide);
        if (pickupAnimator != null)
        {
            yield return pickupAnimator.PlayReachToTargetCoroutine(
                pickupItem.PickupInteractionPoint,
                pickupAnimationSeconds
            );
        }
        else
        {
            yield return new WaitForSeconds(pickupAnimationSeconds);
        }

        // Notify the picked-up item type handler
        if (pickedUpItemTypeHandler != null && pickupItem != null)
        {
            pickedUpItemTypeHandler.HandlePickedUpItem(pickupItem, pickupHandSide);
        }

        // Destroy/disable the pickup item
        if (pickupItem != null)
        {
            pickupItem.OnPickedUpBy();
        }

        isPickupInProgress = false;
    }

    private PickupHandSide ResolvePickupHandSide()
    {
        if (pickedUpItemTypeHandler == null)
        {
            return PickupHandSide.Right;
        }

        if (!pickedUpItemTypeHandler.IsRightHandItemEquipped)
        {
            return PickupHandSide.Right;
        }

        return PickupHandSide.Left;
    }

    private HandIkPickupAnimatorBase ResolvePickupAnimator(PickupHandSide pickupHandSide)
    {
        return pickupHandSide == PickupHandSide.Left
            ? leftHandIkPickupAnimator
            : rightHandIkPickupAnimator;
    }

    private InteractablePickupItem FindBestPickupItemCandidate()
    {
        if (playerCamera == null)
        {
            return null;
        }

        Vector3 pickupSearchCenterPosition = transform.position;
        int overlapCount = Physics.OverlapSphereNonAlloc(
            pickupSearchCenterPosition,
            pickupSearchRadiusMeters,
            overlapResults,
            interactableLayerMask,
            QueryTriggerInteraction.Collide
        );

        if (overlapCount <= 0)
        {
            return null;
        }

        Vector3 cameraPosition = playerCamera.transform.position;
        Vector3 cameraForward = playerCamera.transform.forward;

        InteractablePickupItem bestPickupItem = null;
        float bestScore = float.NegativeInfinity;

        for (int index = 0; index < overlapCount; index++)
        {
            Collider candidateCollider = overlapResults[index];
            if (candidateCollider == null)
            {
                continue;
            }

            InteractablePickupItem candidatePickupItem =
                candidateCollider.GetComponentInParent<InteractablePickupItem>();
            if (candidatePickupItem == null)
            {
                continue;
            }

            if (!candidatePickupItem.CanBePickedUp())
            {
                continue;
            }

            Transform interactionPoint = candidatePickupItem.PickupInteractionPoint;
            if (interactionPoint == null)
            {
                continue;
            }

            Vector3 directionFromCameraToCandidate = interactionPoint.position - cameraPosition;
            float distanceFromCameraToCandidate = directionFromCameraToCandidate.magnitude;
            if (distanceFromCameraToCandidate <= 0.0001f)
            {
                continue;
            }

            Vector3 normalizedDirection =
                directionFromCameraToCandidate / distanceFromCameraToCandidate;
            float viewCenterDot = Vector3.Dot(cameraForward, normalizedDirection);
            if (viewCenterDot <= 0.05f)
            {
                continue;
            }

            float normalizedDistanceScore =
                1f - Mathf.Clamp01(distanceFromCameraToCandidate / pickupSearchRadiusMeters);
            float candidateScore =
                (viewCenterDot * viewCenterDotWeight) + (normalizedDistanceScore * distanceWeight);

            if (candidateScore > bestScore)
            {
                bestScore = candidateScore;
                bestPickupItem = candidatePickupItem;
            }
        }

        return bestPickupItem;
    }

    private void UpdatePromptForCurrentCandidate()
    {
        if (pickupPromptText == null)
        {
            return;
        }

        if (currentlySelectedPickupItem == null)
        {
            UpdatePromptVisibility(false);
            return;
        }

        string itemDisplayName = currentlySelectedPickupItem.ItemDisplayName;
        if (string.IsNullOrWhiteSpace(itemDisplayName))
        {
            itemDisplayName = currentlySelectedPickupItem.ItemType.ToString();
        }

        pickupPromptText.text = string.Format(
            pickupPromptFormat,
            pickupInputDisplayName,
            itemDisplayName
        );
        UpdatePromptVisibility(true);
    }

    private void UpdatePromptVisibility(bool isVisible)
    {
        if (pickupPromptText == null)
        {
            return;
        }

        pickupPromptText.enabled = isVisible;
    }
}
