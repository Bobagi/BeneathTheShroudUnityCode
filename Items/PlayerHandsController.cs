using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations.Rigging;

// Drives the right arm with a TwoBoneIK constraint: reaching for items on pickup and raising the
// held item toward the reticle while aiming. Also handles the aim camera and facing the aim.
[DefaultExecutionOrder(50)]
public sealed class PlayerHandsController : MonoBehaviour
{
    [Header("Rig")]
    [SerializeField]
    private TwoBoneIKConstraint rightArmIk;

    [SerializeField]
    private Transform rightShoulder;

    [SerializeField]
    private Transform rightHandBone;

    [SerializeField]
    private Transform rightHandSocket;

    [Header("Dependencies")]
    [SerializeField]
    private PlayerInventory inventory;

    [SerializeField]
    private StarterAssets.ThirdPersonController thirdPersonController;

    [SerializeField]
    private StarterAssets.StarterAssetsInputs starterInputs;

    [SerializeField]
    private CinemachineCamera followCamera;

    [Header("Reach")]
    [SerializeField]
    private float reachSeconds = 0.28f;

    [SerializeField]
    private float returnSeconds = 0.22f;

    [SerializeField]
    private float maxReachDistance = 0.62f;

    [Header("Aim")]
    [SerializeField]
    private float raiseBlendSeconds = 0.12f;

    [SerializeField]
    private float aimFieldOfView = 38f;

    [SerializeField]
    private float aimCameraDistance = 1.9f;

    [SerializeField]
    private float cameraBlendSeconds = 0.15f;

    [SerializeField]
    [Tooltip("CameraSide while aiming: 1 = fully over the right shoulder, clear of the body.")]
    private float aimCameraSide = 0.9f;

    [SerializeField]
    private float bodyTurnSpeed = 14f;

    [SerializeField]
    private GameObject reticle;

    private CinemachineThirdPersonFollow thirdPersonFollow;
    private float defaultFieldOfView;
    private float defaultCameraDistance;
    private float defaultCameraSide;
    private float fovVelocity;
    private float distanceVelocity;
    private float sideVelocity;

    private float aimWeight;
    private float aimWeightVelocity;
    private float reachWeight;
    private Vector3 reachTargetPosition;
    private Quaternion reachTargetRotation;
    private Vector3 smoothedAimPoint;

    private Transform ikTarget;

    // Added to the lens target, e.g. by DreadDirector to squeeze the view under stress.
    public float ExtraFieldOfView { get; set; }
    private Transform ikHint;

    private void Awake()
    {
        if (rightArmIk != null)
        {
            ikTarget = rightArmIk.data.target;
            ikHint = rightArmIk.data.hint;
            rightArmIk.weight = 0f;
        }

        if (followCamera != null)
        {
            thirdPersonFollow = followCamera.GetComponent<CinemachineThirdPersonFollow>();
            defaultFieldOfView = followCamera.Lens.FieldOfView;
            if (thirdPersonFollow != null)
            {
                defaultCameraDistance = thirdPersonFollow.CameraDistance;
                defaultCameraSide = thirdPersonFollow.CameraSide;
            }
        }

        if (reticle != null)
        {
            reticle.SetActive(false);
        }
    }

    // Runs after the ThirdPersonController moved/rotated the body, before animation + IK solve.
    private void Update()
    {
        bool aiming = inventory != null && inventory.IsAiming;
        ItemDefinition heldDefinition =
            inventory != null && inventory.HeldItem != null ? inventory.HeldItem.Definition : null;

        aimWeight = Mathf.SmoothDamp(aimWeight, aiming ? 1f : 0f, ref aimWeightVelocity, raiseBlendSeconds);

        if (thirdPersonController != null)
        {
            thirdPersonController.RotateOnMove = !aiming;
        }

        if (aiming && starterInputs != null)
        {
            starterInputs.sprint = false;
        }

        if (aiming)
        {
            FaceAimDirection();
        }

        UpdateCamera(aiming && (heldDefinition == null || heldDefinition.zoomCameraWhenRaised));

        if (reticle != null)
        {
            bool showReticle = aiming && (heldDefinition == null || heldDefinition.showReticleWhenRaised);
            if (reticle.activeSelf != showReticle)
            {
                reticle.SetActive(showReticle);
            }
        }
    }

    // Target placement happens late so it uses this frame's camera, then Animation Rigging solves it
    // in the next animation pass (the one-frame latency is invisible at these blend speeds).
    private void LateUpdate()
    {
        if (rightArmIk == null)
        {
            return;
        }

        float weight = Mathf.Max(aimWeight, reachWeight);
        rightArmIk.weight = weight;
        if (weight <= 0.001f)
        {
            return;
        }

        if (reachWeight >= aimWeight)
        {
            ikTarget.SetPositionAndRotation(reachTargetPosition, reachTargetRotation);
        }
        else
        {
            PlaceAimTarget();
        }

        // Elbow hint: below and outside the shoulder, slightly behind, so the arm bends naturally.
        Vector3 hint = rightShoulder.position
            + transform.right * 0.35f
            - transform.up * 0.45f
            - transform.forward * 0.15f;
        ikHint.position = hint;
    }

    private void PlaceAimTarget()
    {
        WorldItem held = inventory.HeldItem;
        if (held == null)
        {
            return;
        }

        ItemDefinition definition = held.Definition;
        Ray aimRay = inventory.GetAimRay();
        Vector3 aimPoint = inventory.GetAimPoint(aimRay);
        smoothedAimPoint = aimWeight < 0.05f ? aimPoint : Vector3.Lerp(smoothedAimPoint, aimPoint, 0.5f);

        Vector3 shoulder = rightShoulder.position;
        Vector3 aimDirection = (smoothedAimPoint - shoulder).normalized;
        float reach = definition != null ? definition.raisedReach : 0.55f;
        float heightOffset = definition != null ? definition.raisedHeightOffset : 0f;
        Vector3 handPosition = shoulder + aimDirection * reach + Vector3.up * heightOffset;

        // Desired item orientation: its aim axis on the aim line, its up axis upright.
        Vector3 forwardLocal = definition != null ? definition.aimForwardLocal : Vector3.forward;
        Vector3 upLocal = definition != null ? definition.aimUpLocal : Vector3.up;
        Vector3 itemAimDirection = (smoothedAimPoint - handPosition).normalized;
        if (definition != null && definition.keepUprightWhenRaised)
        {
            itemAimDirection = Vector3.ProjectOnPlane(itemAimDirection, Vector3.up).normalized;
        }

        Quaternion desiredItemRotation =
            Quaternion.LookRotation(itemAimDirection, Vector3.up)
            * Quaternion.Inverse(Quaternion.LookRotation(forwardLocal, upLocal));

        // Item rotation = handBone * (bone->item). Solve for the hand bone rotation the IK must reach.
        Quaternion boneToItem = Quaternion.Inverse(rightHandBone.rotation) * held.transform.rotation;
        Quaternion desiredHandRotation = desiredItemRotation * Quaternion.Inverse(boneToItem);

        // The IK positions the hand bone; shift it so the item's grip lands on the aim line.
        Vector3 boneToGrip = held.Grip.position - rightHandBone.position;
        Vector3 boneToGripAtTarget = desiredHandRotation * (Quaternion.Inverse(rightHandBone.rotation) * boneToGrip);
        ikTarget.SetPositionAndRotation(handPosition - boneToGripAtTarget, desiredHandRotation);
    }

    private void FaceAimDirection()
    {
        Vector3 cameraForward = inventory.ViewCamera.transform.forward;
        cameraForward.y = 0f;
        if (cameraForward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(cameraForward.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            1f - Mathf.Exp(-bodyTurnSpeed * Time.deltaTime)
        );
    }

    private void UpdateCamera(bool zoomed)
    {
        if (followCamera == null)
        {
            return;
        }

        LensSettings lens = followCamera.Lens;
        lens.FieldOfView = Mathf.SmoothDamp(
            lens.FieldOfView,
            (zoomed ? aimFieldOfView : defaultFieldOfView) + ExtraFieldOfView,
            ref fovVelocity,
            cameraBlendSeconds
        );
        followCamera.Lens = lens;

        if (thirdPersonFollow != null)
        {
            thirdPersonFollow.CameraDistance = Mathf.SmoothDamp(
                thirdPersonFollow.CameraDistance,
                zoomed ? aimCameraDistance : defaultCameraDistance,
                ref distanceVelocity,
                cameraBlendSeconds
            );
            thirdPersonFollow.CameraSide = Mathf.SmoothDamp(
                thirdPersonFollow.CameraSide,
                zoomed ? aimCameraSide : defaultCameraSide,
                ref sideVelocity,
                cameraBlendSeconds
            );
        }
    }

    // ---------- Reach (pickup) ----------

    public IEnumerator PlayReach(Transform target)
    {
        if (rightArmIk == null || target == null)
        {
            yield return new WaitForSeconds(reachSeconds);
            yield break;
        }

        Vector3 shoulder = rightShoulder.position;
        Vector3 toTarget = target.position - shoulder;
        // Clamp so the arm reaches toward far/low items without the IK snapping to full stretch.
        reachTargetPosition = shoulder + Vector3.ClampMagnitude(toTarget, maxReachDistance);
        Vector3 palmDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
        reachTargetRotation = Quaternion.LookRotation(palmDirection, transform.forward) * Quaternion.Euler(0f, 0f, 90f);
        // Keep the current hand orientation as the base so the wrist does not spin.
        reachTargetRotation = Quaternion.Slerp(rightHandBone.rotation, reachTargetRotation, 0.35f);

        float elapsed = 0f;
        while (elapsed < reachSeconds)
        {
            elapsed += Time.deltaTime;
            reachWeight = Mathf.SmoothStep(0f, 1f, elapsed / reachSeconds);
            yield return null;
        }

        reachWeight = 1f;
    }

    public IEnumerator PlayReturn()
    {
        float elapsed = 0f;
        while (elapsed < returnSeconds)
        {
            elapsed += Time.deltaTime;
            reachWeight = 1f - Mathf.SmoothStep(0f, 1f, elapsed / returnSeconds);
            yield return null;
        }

        reachWeight = 0f;
    }
}
