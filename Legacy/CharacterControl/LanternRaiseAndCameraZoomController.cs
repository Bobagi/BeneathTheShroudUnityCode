using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class LanternRaiseAndCameraZoomController : MonoBehaviour
{
    [System.Serializable]
    private struct AimProfile
    {
        public InteractablePickupItemType itemType;
        public PickupHandSide handSide;
        public bool overrideZoom;
        public float zoomedFieldOfView;
        public float zoomedCameraDistance;
        public GameObject reticleGameObject;
    }

    [Header("Dependencies")]
    [SerializeField]
    private PickedUpItemTypeHandler pickedUpItemTypeHandler;

    [SerializeField]
    private StarterAssets.ThirdPersonController thirdPersonController;

    [Header("Input")]
    [SerializeField]
    private bool keepHandRaised = false;

    [SerializeField]
    private InputActionReference raiseLanternInputActionReference;

    [Header("Item Types")]
    [SerializeField]
    private InteractablePickupItemType[] supportedItemTypes;

    [Header("Hold IK Transforms")]
    [SerializeField]
    private HandIkPickupAnimatorBase rightHandIkPickupAnimator;

    [SerializeField]
    private HandIkPickupAnimatorBase leftHandIkPickupAnimator;

    [Header("Force max IK weight")]
    [SerializeField]
    private bool forceMaxWeight = false;

    [Header("Cinemachine")]
    [SerializeField]
    private CinemachineCamera playerFollowCinemachineCamera;

    [SerializeField]
    private float zoomedFieldOfView = 32f;

    [SerializeField]
    private float zoomedCameraDistance = 3.2f;

    [SerializeField]
    private float cameraZoomSmoothingSeconds = 0.10f;

    [Header("Hold IK Transition")]
    [SerializeField]
    private float holdTargetTransitionSeconds = 0.12f;

    [Header("Aim Rotation")]
    [SerializeField]
    private bool forceLookAtCameraTarget = true;

    [SerializeField]
    private float aimRotationSmoothingSeconds = 0.06f;

    [Header("Aim Profiles")]
    [SerializeField]
    private AimProfile[] aimProfiles;

    private bool isRaiseActive;
    private InteractablePickupItemType currentRaiseItemType;
    private PickupHandSide currentRaiseHandSide;

    private float defaultCameraFieldOfView;
    private float defaultCameraDistance;

    private float cameraFieldOfViewVelocity;
    private float cameraDistanceVelocity;
    private float aimRotationVelocity;

    private CinemachineThirdPersonFollow cachedThirdPersonFollow;

    private bool raiseInputHeld;
    private bool hasActiveAimProfile;
    private AimProfile activeAimProfile;
    private GameObject activeReticle;
    private void Awake()
    {
        CacheCameraDefaults();
    }

    private void OnEnable()
    {
        if (raiseLanternInputActionReference != null)
        {
            raiseLanternInputActionReference.action.Enable();
            raiseLanternInputActionReference.action.started += HandleRaiseStarted;
            raiseLanternInputActionReference.action.canceled += HandleRaiseCanceled;
        }
    }

    private void OnDisable()
    {
        if (raiseLanternInputActionReference != null)
        {
            raiseLanternInputActionReference.action.started -= HandleRaiseStarted;
            raiseLanternInputActionReference.action.canceled -= HandleRaiseCanceled;
            raiseLanternInputActionReference.action.Disable();
        }

        raiseInputHeld = false;
        if (isRaiseActive)
        {
            TriggerAimCanceled(currentRaiseItemType);
        }

        isRaiseActive = false;
        DeactivateAimProfile();
        MoveHoldTargetsToNormal();
        UpdateCameraZoom(false);
    }

    private void Update()
    {
        InteractablePickupItemType itemType = default;
        PickupHandSide handSide = PickupHandSide.Right;
        bool hasEquippedItemType =
            pickedUpItemTypeHandler != null
            && pickedUpItemTypeHandler.TryGetEquippedItemInfo(out itemType, out handSide);

        bool isSupportedItemType = hasEquippedItemType && IsSupportedItemType(itemType);

        if (!isSupportedItemType)
        {
            if (isRaiseActive)
            {
                TriggerAimCanceled(currentRaiseItemType, currentRaiseHandSide);
            }

            isRaiseActive = false;
            MoveHoldTargetsToNormal();
            UpdateCameraZoom(false);
            return;
        }

        if (forceMaxWeight)
        {
            pickedUpItemTypeHandler.setWeight(1f);
        }

        bool shouldRaise = raiseInputHeld || (keepHandRaised && isRaiseActive);

        if (shouldRaise != isRaiseActive)
        {
            isRaiseActive = shouldRaise;
            if (isRaiseActive)
            {
                currentRaiseItemType = itemType;
                currentRaiseHandSide = handSide;
                TriggerAimStarted(itemType, handSide);
            }
            else
            {
                TriggerAimCanceled(currentRaiseItemType, currentRaiseHandSide);
            }
        }
        else if (
            isRaiseActive
            && (
                currentRaiseItemType != itemType
                || currentRaiseHandSide != handSide
            )
        )
        {
            TriggerAimCanceled(currentRaiseItemType, currentRaiseHandSide);
            currentRaiseItemType = itemType;
            currentRaiseHandSide = handSide;
            TriggerAimStarted(itemType, handSide);
        }

        if (isRaiseActive)
        {
            MoveHoldTargetsToRaised(itemType);
        }
        else
        {
            MoveHoldTargetsToNormal(itemType);
        }

        UpdateCameraZoom(isRaiseActive);
    }

    private void LateUpdate()
    {
        if (!isRaiseActive || thirdPersonController == null)
        {
            return;
        }

        RotatePlayerTowardsCameraYaw();
    }

    private void HandleRaiseStarted(InputAction.CallbackContext _)
    {
        raiseInputHeld = true;
    }

    private void HandleRaiseCanceled(InputAction.CallbackContext _)
    {
        if (!keepHandRaised)
            raiseInputHeld = false;
    }

    private void MoveHoldTargetsToNormal()
    {
        MoveHoldTargetTowards(
            rightHandIkPickupAnimator != null
                ? rightHandIkPickupAnimator.HandIkTargetTransform
                : null,
            rightHandIkPickupAnimator != null
                ? rightHandIkPickupAnimator.HandNormalPositionReferenceTransform
                : null
        );

        MoveHoldTargetTowards(
            leftHandIkPickupAnimator != null
                ? leftHandIkPickupAnimator.HandIkTargetTransform
                : null,
            leftHandIkPickupAnimator != null
                ? leftHandIkPickupAnimator.HandNormalPositionReferenceTransform
                : null
        );
    }

    private void MoveHoldTargetsToNormal(InteractablePickupItemType itemType)
    {
        MoveHoldTargetTowards(
            rightHandIkPickupAnimator != null
                ? rightHandIkPickupAnimator.HandIkTargetTransform
                : null,
            ResolveRelaxedReference(rightHandIkPickupAnimator, itemType)
        );

        MoveHoldTargetTowards(
            leftHandIkPickupAnimator != null
                ? leftHandIkPickupAnimator.HandIkTargetTransform
                : null,
            ResolveRelaxedReference(leftHandIkPickupAnimator, itemType)
        );
    }

    private void MoveHoldTargetsToRaised(InteractablePickupItemType itemType)
    {
        Transform rightRaisedReference = ResolveRaisedReference(rightHandIkPickupAnimator, itemType);
        Transform leftRaisedReference = ResolveRaisedReference(leftHandIkPickupAnimator, itemType);

        MoveHoldTargetTowards(
            rightHandIkPickupAnimator != null
                ? rightHandIkPickupAnimator.HandIkTargetTransform
                : null,
            rightRaisedReference
        );

        MoveHoldTargetTowards(
            leftHandIkPickupAnimator != null
                ? leftHandIkPickupAnimator.HandIkTargetTransform
                : null,
            leftRaisedReference
        );
    }

    private Transform ResolveRaisedReference(
        HandIkPickupAnimatorBase handIkPickupAnimator,
        InteractablePickupItemType itemType
    )
    {
        if (handIkPickupAnimator == null)
        {
            return null;
        }

        Transform raisedReference = handIkPickupAnimator.GetRaisedReferenceForItem(itemType);
        return raisedReference != null
            ? raisedReference
            : handIkPickupAnimator.HandRaisedPositionReferenceTransform;
    }

    private Transform ResolveRelaxedReference(
        HandIkPickupAnimatorBase handIkPickupAnimator,
        InteractablePickupItemType itemType
    )
    {
        if (handIkPickupAnimator == null)
        {
            return null;
        }

        Transform relaxedReference = handIkPickupAnimator.GetRelaxedReferenceForItem(itemType);
        return relaxedReference != null
            ? relaxedReference
            : handIkPickupAnimator.HandNormalPositionReferenceTransform;
    }

    private bool IsSupportedItemType(InteractablePickupItemType itemType)
    {
        if (supportedItemTypes == null || supportedItemTypes.Length == 0)
        {
            return true;
        }

        foreach (InteractablePickupItemType supportedType in supportedItemTypes)
        {
            if (supportedType == itemType)
            {
                return true;
            }
        }

        return false;
    }

    private void MoveHoldTargetTowards(Transform holdTargetTransform, Transform referenceTransform)
    {
        if (holdTargetTransform == null || referenceTransform == null)
        {
            return;
        }

        float smoothing = Mathf.Max(0.01f, holdTargetTransitionSeconds);
        float t = 1f - Mathf.Exp(-Time.deltaTime / smoothing);

        holdTargetTransform.position = Vector3.Lerp(
            holdTargetTransform.position,
            referenceTransform.position,
            t
        );

        holdTargetTransform.rotation = Quaternion.Slerp(
            holdTargetTransform.rotation,
            referenceTransform.rotation,
            t
        );
    }

    private void CacheCameraDefaults()
    {
        if (playerFollowCinemachineCamera == null)
        {
            return;
        }

        defaultCameraFieldOfView = playerFollowCinemachineCamera.Lens.FieldOfView;

        cachedThirdPersonFollow =
            playerFollowCinemachineCamera.GetComponent<CinemachineThirdPersonFollow>();

        if (cachedThirdPersonFollow != null)
        {
            defaultCameraDistance = cachedThirdPersonFollow.CameraDistance;
        }
    }

    private void UpdateCameraZoom(bool isZoomed)
    {
        if (playerFollowCinemachineCamera == null)
        {
            return;
        }

        float desiredFieldOfView = isZoomed ? zoomedFieldOfView : defaultCameraFieldOfView;

        float smoothing = Mathf.Max(0.01f, cameraZoomSmoothingSeconds);

        if (isZoomed && hasZoomOverride)
        {
            desiredFieldOfView = zoomOverrideFieldOfView;
        }

        float blendedFov = Mathf.SmoothDamp(
            playerFollowCinemachineCamera.Lens.FieldOfView,
            desiredFieldOfView,
            ref cameraFieldOfViewVelocity,
            smoothing
        );

        LensSettings lens = playerFollowCinemachineCamera.Lens;
        lens.FieldOfView = blendedFov;
        playerFollowCinemachineCamera.Lens = lens;

        if (cachedThirdPersonFollow == null)
        {
            return;
        }

        float desiredDistance = isZoomed ? zoomedCameraDistance : defaultCameraDistance;

        if (isZoomed && hasZoomOverride)
        {
            desiredDistance = zoomOverrideCameraDistance;
        }

        float blendedDistance = Mathf.SmoothDamp(
            cachedThirdPersonFollow.CameraDistance,
            desiredDistance,
            ref cameraDistanceVelocity,
            smoothing
        );

        cachedThirdPersonFollow.CameraDistance = blendedDistance;
    }

    private void RotatePlayerTowardsCameraYaw()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Vector3 planarForward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up);

        if (planarForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float desiredYaw = Quaternion.LookRotation(planarForward, Vector3.up).eulerAngles.y;

        float currentYaw = thirdPersonController.transform.eulerAngles.y;

        if (forceLookAtCameraTarget)
        {
            float smoothedYaw = Mathf.SmoothDampAngle(
                currentYaw,
                desiredYaw,
                ref aimRotationVelocity,
                Mathf.Max(0.01f, aimRotationSmoothingSeconds)
            );

            thirdPersonController.transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
        }
    }

    private void TriggerAimStarted(InteractablePickupItemType itemType)
    {
        TriggerAimStarted(itemType, currentRaiseHandSide);
    }

    private void TriggerAimCanceled(InteractablePickupItemType itemType)
    {
        TriggerAimCanceled(itemType, currentRaiseHandSide);
    }

    private void TriggerAimStarted(InteractablePickupItemType itemType, PickupHandSide handSide)
    {
        ActivateAimProfile(itemType, handSide);
    }

    private void TriggerAimCanceled(InteractablePickupItemType itemType, PickupHandSide handSide)
    {
        DeactivateAimProfile();
    }

    private void ActivateAimProfile(InteractablePickupItemType itemType, PickupHandSide handSide)
    {
        DeactivateAimProfile();

        if (aimProfiles == null || aimProfiles.Length == 0)
        {
            return;
        }

        foreach (AimProfile profile in aimProfiles)
        {
            if (profile.itemType == itemType && profile.handSide == handSide)
            {
                hasActiveAimProfile = true;
                activeAimProfile = profile;

                if (profile.reticleGameObject != null)
                {
                    activeReticle = profile.reticleGameObject;
                    activeReticle.SetActive(true);
                }

                return;
            }
        }
    }

    private void DeactivateAimProfile()
    {
        if (activeReticle != null)
        {
            activeReticle.SetActive(false);
            activeReticle = null;
        }

        hasActiveAimProfile = false;
        activeAimProfile = default;
    }

    private bool hasZoomOverride
    {
        get { return hasActiveAimProfile && activeAimProfile.overrideZoom; }
    }

    private float zoomOverrideFieldOfView
    {
        get { return activeAimProfile.zoomedFieldOfView; }
    }

    private float zoomOverrideCameraDistance
    {
        get { return activeAimProfile.zoomedCameraDistance; }
    }
}
