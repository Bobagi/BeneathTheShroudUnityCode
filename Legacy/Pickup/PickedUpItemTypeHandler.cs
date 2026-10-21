using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

public sealed class PickedUpItemTypeHandler : MonoBehaviour
{
    [Header("Hand Socket")]
    [SerializeField]
    private Transform rightHandSocketTransform;

    [SerializeField]
    private Transform leftHandSocketTransform;

    [Header("Hold IK")]
    [SerializeField]
    private Rig holdRigLayer;

    [SerializeField]
    private TwoBoneIKConstraint holdRightArmTwoBoneIkConstraint;

    [SerializeField]
    private Rig leftHoldRigLayer;

    [SerializeField]
    private TwoBoneIKConstraint holdLeftArmTwoBoneIkConstraint;

    [SerializeField]
    private float holdRigWeightWhenActive = 1f;

    [Header("Hold IK Weight Smoothing")]
    [SerializeField]
    private StarterAssets.ThirdPersonController thirdPersonController;

    [SerializeField]
    private float holdIkWeightWhenGrounded = 0.85f;

    [SerializeField]
    private float holdIkWeightWhenAirborne = 0.55f;

    [SerializeField]
    private float holdIkWeightSmoothingSeconds = 0.12f;

    [Header("Lantern Swing")]
    [SerializeField]
    [Tooltip(
        "Hold the lantern as a REAL physics rig ported from the Lamparina sandbox: a kinematic "
            + "anchor follows the hand socket, the handle glues to it via FixedJoint and the body "
            + "swings on the prefab's own HingeJoint. Off = kinematic LanternHingeSwing fallback."
    )]
    private bool useRealPhysicsLanternSwing = true;

    [Header("Drop Input")]
    [SerializeField]
    private InputActionReference dropInputActionReference;

    [SerializeField]
    private Transform dropSpawnTransform;

    private GameObject currentlyEquippedRightHandItemGameObject;
    private GameObject currentlyEquippedLeftHandItemGameObject;
    private InteractablePickupItem currentlyEquippedRightHandPickupItem;
    private InteractablePickupItem currentlyEquippedLeftHandPickupItem;
    private InteractablePickupItemType rightEquippedItemType;
    private InteractablePickupItemType leftEquippedItemType;
    private float rightHoldIkWeightVelocity;
    private float rightCurrentHoldIkWeight;
    private float leftHoldIkWeightVelocity;
    private float leftCurrentHoldIkWeight;
    private bool isRightHandItemEquipped;
    private bool isLeftHandItemEquipped;

    public bool IsRightHandItemEquipped => isRightHandItemEquipped;
    public bool IsLeftHandItemEquipped => isLeftHandItemEquipped;

    public bool TryGetEquippedItemInfo(
        out InteractablePickupItemType itemType,
        out PickupHandSide handSide
    )
    {
        if (isRightHandItemEquipped)
        {
            itemType = rightEquippedItemType;
            handSide = PickupHandSide.Right;
            return true;
        }

        if (isLeftHandItemEquipped)
        {
            itemType = leftEquippedItemType;
            handSide = PickupHandSide.Left;
            return true;
        }

        itemType = InteractablePickupItemType.Lantern;
        handSide = PickupHandSide.Right;
        return false;
    }

    public bool TryGetEquippedItemType(out InteractablePickupItemType itemType)
    {
        if (isRightHandItemEquipped)
        {
            itemType = rightEquippedItemType;
            return true;
        }

        if (isLeftHandItemEquipped)
        {
            itemType = leftEquippedItemType;
            return true;
        }

        itemType = InteractablePickupItemType.Lantern;
        return false;
    }

    private void Awake()
    {
        isRightHandItemEquipped = false;
        isLeftHandItemEquipped = false;
        rightEquippedItemType = InteractablePickupItemType.Lantern;
        leftEquippedItemType = InteractablePickupItemType.Lantern;
        rightHoldIkWeightVelocity = 0f;
        rightCurrentHoldIkWeight = 0f;
        leftHoldIkWeightVelocity = 0f;
        leftCurrentHoldIkWeight = 0f;

        if (holdRigLayer != null)
        {
            holdRigLayer.weight = 0f;
        }

        if (holdRightArmTwoBoneIkConstraint != null)
        {
            holdRightArmTwoBoneIkConstraint.weight = 0f;
        }

        if (leftHoldRigLayer != null)
        {
            leftHoldRigLayer.weight = 0f;
        }

        if (holdLeftArmTwoBoneIkConstraint != null)
        {
            holdLeftArmTwoBoneIkConstraint.weight = 0f;
        }
    }

    private void OnEnable()
    {
        if (dropInputActionReference != null)
        {
            dropInputActionReference.action.Enable();
            dropInputActionReference.action.started += HandleDropActionStarted;
        }
    }

    private void OnDisable()
    {
        if (dropInputActionReference != null)
        {
            dropInputActionReference.action.started -= HandleDropActionStarted;
            dropInputActionReference.action.Disable();
        }
    }

    public void setWeight(float weight)
    {
        if (holdRigLayer != null)
        {
            holdRigLayer.weight = weight;
        }

        if (holdRightArmTwoBoneIkConstraint != null)
        {
            holdRightArmTwoBoneIkConstraint.weight = weight;
        }

        if (leftHoldRigLayer != null)
        {
            leftHoldRigLayer.weight = weight;
        }

        if (holdLeftArmTwoBoneIkConstraint != null)
        {
            holdLeftArmTwoBoneIkConstraint.weight = weight;
        }
    }

    private void Update()
    {
        if (!isRightHandItemEquipped && !isLeftHandItemEquipped)
        {
            return;
        }

        if (isRightHandItemEquipped)
        {
            UpdateHoldIkWeight(
                ref rightCurrentHoldIkWeight,
                ref rightHoldIkWeightVelocity,
                holdRigLayer,
                holdRightArmTwoBoneIkConstraint
            );
        }

        if (isLeftHandItemEquipped)
        {
            UpdateHoldIkWeight(
                ref leftCurrentHoldIkWeight,
                ref leftHoldIkWeightVelocity,
                leftHoldRigLayer,
                holdLeftArmTwoBoneIkConstraint
            );
        }
    }

    public void HandlePickedUpItem(InteractablePickupItem pickedUpItem)
    {
        HandlePickedUpItem(pickedUpItem, PickupHandSide.Right);
    }

    public void HandlePickedUpItem(
        InteractablePickupItem pickedUpItem,
        PickupHandSide pickupHandSide
    )
    {
        if (pickedUpItem == null)
        {
            return;
        }

        EquipItem(pickedUpItem, pickupHandSide);
    }

    private void EquipItem(InteractablePickupItem pickedUpItem, PickupHandSide pickupHandSide)
    {
        if (pickedUpItem == null || pickedUpItem.PickupItemPrefab == null)
        {
            return;
        }

        if (pickupHandSide == PickupHandSide.Left)
        {
            EquipItemToLeftHand(pickedUpItem);
            return;
        }

        EquipItemToRightHand(pickedUpItem);
    }

    private void EquipItemToRightHand(InteractablePickupItem pickedUpItem)
    {
        if (rightHandSocketTransform == null)
        {
            return;
        }

        GameObject spawnedItemGameObject = SpawnEquippedItem(
            pickedUpItem,
            rightHandSocketTransform
        );

        if (currentlyEquippedRightHandItemGameObject != null)
        {
            Destroy(currentlyEquippedRightHandItemGameObject);
        }

        isRightHandItemEquipped = true;
        rightHoldIkWeightVelocity = 0f;
        rightCurrentHoldIkWeight = 0f;

        // Apply immediate IK weight update to avoid popping animation frame when equipping item to hand
        ApplyImmediateHoldIkWeight(
            ref rightCurrentHoldIkWeight,
            holdRigLayer,
            holdRightArmTwoBoneIkConstraint
        );

        currentlyEquippedRightHandItemGameObject = spawnedItemGameObject;
        currentlyEquippedRightHandPickupItem = pickedUpItem;
        rightEquippedItemType = pickedUpItem.ItemType;
    }

    private void EquipItemToLeftHand(InteractablePickupItem pickedUpItem)
    {
        if (leftHandSocketTransform == null)
        {
            return;
        }

        GameObject spawnedItemGameObject = SpawnEquippedItem(pickedUpItem, leftHandSocketTransform);

        if (currentlyEquippedLeftHandItemGameObject != null)
        {
            Destroy(currentlyEquippedLeftHandItemGameObject);
        }

        isLeftHandItemEquipped = true;
        leftHoldIkWeightVelocity = 0f;
        leftCurrentHoldIkWeight = 0f;

        // Apply immediate IK weight update to avoid popping animation frame when equipping item to hand
        ApplyImmediateHoldIkWeight(
            ref leftCurrentHoldIkWeight,
            leftHoldRigLayer,
            holdLeftArmTwoBoneIkConstraint
        );

        currentlyEquippedLeftHandItemGameObject = spawnedItemGameObject;
        currentlyEquippedLeftHandPickupItem = pickedUpItem;
        leftEquippedItemType = pickedUpItem.ItemType;
    }

    private GameObject SpawnEquippedItem(
        InteractablePickupItem originPickedUpItem,
        Transform handSocketTransform
    )
    {
        if (originPickedUpItem == null || originPickedUpItem.PickupItemPrefab == null)
        {
            return null;
        }

        GameObject spawnedItemGameObject = Instantiate(originPickedUpItem.PickupItemPrefab);
        InteractablePickupItem spawnedItem =
            spawnedItemGameObject.GetComponent<InteractablePickupItem>();

        spawnedItemGameObject.layer = 7; // 8 - Interactable -> 7 - hand

        // Physics-held item (the lantern): glue its grip to the hand and swing the whole item from it.
        if (spawnedItem.PickupHandleRigidbodyToConnectToHandFixedJoint)
        {
            if (useRealPhysicsLanternSwing)
            {
                Transform playerRoot =
                    thirdPersonController != null ? thirdPersonController.transform : transform.root;
                return HeldLanternPhysicsRig.Attach(
                    spawnedItemGameObject,
                    spawnedItem,
                    handSocketTransform,
                    playerRoot
                );
            }

            return AttachSwingingHeldItem(spawnedItemGameObject, spawnedItem, handSocketTransform);
        }

        // Rigidly-held item (e.g. revolver): lock it to the hand via transform parenting.
        spawnedItemGameObject.transform.SetParent(handSocketTransform, false);
        spawnedItemGameObject.transform.localPosition = new Vector3(0f, spawnedItem.YAxisOffSet, 0f);
        spawnedItemGameObject.transform.localRotation = Quaternion.Euler(spawnedItem.HeldRotationEuler);

        return spawnedItemGameObject;
    }

    // Holds a physics item (the lantern) as the user expects: the GRIP stays glued to the hand and the
    // body swings below it like a lamp. Returns the sway anchor -- the top of the held hierarchy
    // -- so the caller can dispose of the whole thing by destroying it.
    //
    // History that shaped this: hanging a dynamic joint chain off a fast, oddly-oriented animated
    // finger bone was unstable (fly-away, resting cocked past the hinge limit). Holding the item by
    // SCRIPT let the grip flicker, because the hand bone is posed by the Animator + Animation Rigging
    // and no per-frame script follows it perfectly. Splitting the prefab's "handle" body off to the
    // hand put the WRONG point in the hand (that body sits at the base of the lantern), so the item
    // hung from its bottom. The robust answer: let the transform hierarchy do the gluing. An anchor is
    // parented to the hand at the grip point and the whole lantern hangs under it at the grip offset.
    //
    // The swing itself is ported from the Lamparina sandbox project, where the lamp is a real physics
    // rig: the body hangs from the handle on a HingeJoint whose zero-target motor acts as bearing
    // friction, so it always settles hanging plumb. Here the same model runs kinematically: the
    // handle is re-parented to the anchor so it stays rigid in the hand, the body goes under a pivot
    // created at the prefab's hinge, and LanternHingeSwing rotates that pivot about the hinge axis
    // only. Nothing ever touches positions, so the grip stays pinned to the hand every frame.
    private GameObject AttachSwingingHeldItem(
        GameObject lanternGameObject,
        InteractablePickupItem spawnedItem,
        Transform handSocketTransform
    )
    {
        // The swing pivot must sit exactly where the prefab's handle/body hinge is; grab the joint
        // before the physics teardown below.
        HingeJoint prefabHingeJoint = lanternGameObject.GetComponentInChildren<HingeJoint>(true);

        // Everything is transform-driven now, so switch the prefab's physics off (kinematic, no
        // interpolation lag, no colliders that would block the player on the shared "hand" layer).
        foreach (Rigidbody itemRigidbody in lanternGameObject.GetComponentsInChildren<Rigidbody>(true))
        {
            itemRigidbody.isKinematic = true;
            itemRigidbody.useGravity = false;
            itemRigidbody.interpolation = RigidbodyInterpolation.None;
        }
        foreach (Collider itemCollider in lanternGameObject.GetComponentsInChildren<Collider>(true))
        {
            itemCollider.enabled = false;
        }
        FixedJoint staleHandJoint = handSocketTransform.GetComponent<FixedJoint>();
        if (staleHandJoint != null)
        {
            staleHandJoint.connectedBody = null;
        }

        // Anchor pinned to the hand grip point. As a plain transform child of the hand bone it tracks
        // the bone exactly, every frame -- the grip can never flicker off the hand.
        GameObject swayAnchorGameObject = new GameObject("LanternSwayAnchor");
        Transform swayAnchor = swayAnchorGameObject.transform;
        swayAnchor.SetParent(handSocketTransform, false);
        swayAnchor.localPosition = Vector3.zero;
        swayAnchor.localRotation = Quaternion.identity;

        // Hang the whole lantern under the anchor, positioned by its GRIP point (the handle rigidbody's
        // transform -- the prefab's designated hand-connection point), NOT by the lantern's own origin.
        // The lantern origin and the grip are offset (the grip sits ~0.17 below the origin), so placing
        // the origin at the offset left the grip dangling well below the hand. We instead offset the
        // lantern so its grip lands at (0, YAxisOffSet, 0) in the hand's frame -- exactly where the item
        // reads as correctly gripped.
        Transform lantern = lanternGameObject.transform;
        Quaternion heldRotation = Quaternion.Euler(spawnedItem.HeldRotationEuler);
        Vector3 gripLocalPosition = spawnedItem.PickupHandleRigidbodyToConnectToHandFixedJoint.transform.localPosition;
        lantern.SetParent(swayAnchor, false);
        lantern.localRotation = heldRotation;
        lantern.localPosition = new Vector3(0f, spawnedItem.YAxisOffSet, 0f) - heldRotation * gripLocalPosition;

        Transform handleTransform =
            spawnedItem.PickupHandleRigidbodyToConnectToHandFixedJoint.transform;

        // Without a separate handle piece or an authored hinge there is nothing to pivot the body on;
        // fall back to swaying the whole item around the grip.
        if (prefabHingeJoint == null || handleTransform == lantern)
        {
            swayAnchorGameObject.AddComponent<ProceduralHeldSway>();
            return swayAnchorGameObject;
        }

        Vector3 hingeWorldPoint = prefabHingeJoint.transform.TransformPoint(prefabHingeJoint.anchor);
        Vector3 hingeWorldAxis =
            prefabHingeJoint.transform.TransformDirection(prefabHingeJoint.axis).normalized;
        Destroy(prefabHingeJoint);

        // Handle rigid in the hand, body under a pivot at the hinge. Both re-parents keep world pose,
        // so nothing visibly moves on the equip frame.
        handleTransform.SetParent(swayAnchor, true);

        GameObject hingePivotGameObject = new GameObject("LanternHingePivot");
        Transform hingePivot = hingePivotGameObject.transform;
        hingePivot.SetPositionAndRotation(hingeWorldPoint, swayAnchor.rotation);
        hingePivot.SetParent(swayAnchor, true);
        lantern.SetParent(hingePivot, true);

        // The body's center (renderer bounds, now that the handle subtree is out) sets the pendulum
        // length and the hang direction.
        Vector3 bodyCenterWorld = ComputeRendererCenter(
            lantern,
            hingeWorldPoint + Vector3.down * 0.15f
        );

        LanternHingeSwing hingeSwing = hingePivotGameObject.AddComponent<LanternHingeSwing>();
        hingeSwing.Initialize(hingeWorldAxis, bodyCenterWorld);

        return swayAnchorGameObject;
    }

    private static Vector3 ComputeRendererCenter(Transform rootTransform, Vector3 fallbackCenter)
    {
        Renderer[] renderers = rootTransform.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return fallbackCenter;
        }

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        return combinedBounds.center;
    }

    private void HandleDropActionStarted(InputAction.CallbackContext _)
    {
        DropEquippedItem();
    }

    public void DropEquippedItem()
    {
        if (isLeftHandItemEquipped)
        {
            DropItemFromHand(
                ref currentlyEquippedLeftHandItemGameObject,
                ref currentlyEquippedLeftHandPickupItem,
                PickupHandSide.Left
            );
            return;
        }

        if (isRightHandItemEquipped)
        {
            DropItemFromHand(
                ref currentlyEquippedRightHandItemGameObject,
                ref currentlyEquippedRightHandPickupItem,
                PickupHandSide.Right
            );
        }
    }

    private void DropItemFromHand(
        ref GameObject equippedItemGameObject,
        ref InteractablePickupItem pickupItem,
        PickupHandSide handSide
    )
    {
        if (equippedItemGameObject != null)
        {
            Destroy(equippedItemGameObject);
            equippedItemGameObject = null;
        }

        SpawnDroppedItem(pickupItem);
        pickupItem = null;

        if (handSide == PickupHandSide.Left)
        {
            ClearLeftHandEquippedState();
        }
        else
        {
            ClearRightHandEquippedState();
        }
    }

    private void SpawnDroppedItem(InteractablePickupItem pickupItem)
    {
        Transform spawnTransform = ResolveDropSpawnTransform();
        if (spawnTransform == null)
        {
            return;
        }

        if (
            pickupItem != null
            && pickupItem.gameObject != null
            && !pickupItem.gameObject.activeInHierarchy
        )
        {
            pickupItem.transform.position = spawnTransform.position;
            pickupItem.transform.rotation = spawnTransform.rotation;
            pickupItem.gameObject.SetActive(true);

            GroundSnapToSurface snapper = pickupItem.GetComponent<GroundSnapToSurface>();
            if (snapper != null)
            {
                snapper.SnapToSurface();
            }

            return;
        }

        if (pickupItem == null || pickupItem.PickupItemPrefab == null)
        {
            return;
        }

        GameObject droppedItem = Instantiate(pickupItem.PickupItemPrefab);
        droppedItem.transform.position = spawnTransform.position;
        droppedItem.transform.rotation = spawnTransform.rotation;

        GroundSnapToSurface snapperComponent = droppedItem.GetComponent<GroundSnapToSurface>();
        if (snapperComponent == null)
        {
            snapperComponent = droppedItem.AddComponent<GroundSnapToSurface>();
        }

        snapperComponent.SnapToSurface();
    }

    private Transform ResolveDropSpawnTransform()
    {
        if (dropSpawnTransform != null)
        {
            return dropSpawnTransform;
        }

        return transform;
    }

    private void ClearRightHandEquippedState()
    {
        isRightHandItemEquipped = false;
        rightHoldIkWeightVelocity = 0f;
        rightCurrentHoldIkWeight = 0f;
        rightEquippedItemType = InteractablePickupItemType.Lantern;

        if (holdRigLayer != null)
        {
            holdRigLayer.weight = 0f;
        }

        if (holdRightArmTwoBoneIkConstraint != null)
        {
            holdRightArmTwoBoneIkConstraint.weight = 0f;
        }
    }

    private void ClearLeftHandEquippedState()
    {
        isLeftHandItemEquipped = false;
        leftHoldIkWeightVelocity = 0f;
        leftCurrentHoldIkWeight = 0f;
        leftEquippedItemType = InteractablePickupItemType.Lantern;

        if (leftHoldRigLayer != null)
        {
            leftHoldRigLayer.weight = 0f;
        }

        if (holdLeftArmTwoBoneIkConstraint != null)
        {
            holdLeftArmTwoBoneIkConstraint.weight = 0f;
        }
    }

    private void UpdateHoldIkWeight(
        ref float currentHoldIkWeight,
        ref float holdIkWeightVelocity,
        Rig handHoldRigLayer,
        TwoBoneIKConstraint holdArmTwoBoneIkConstraint
    )
    {
        float desiredHoldIkWeight = ResolveDesiredHoldIkWeight();
        currentHoldIkWeight = Mathf.SmoothDamp(
            currentHoldIkWeight,
            desiredHoldIkWeight,
            ref holdIkWeightVelocity,
            Mathf.Max(0.01f, holdIkWeightSmoothingSeconds)
        );

        if (handHoldRigLayer != null)
        {
            handHoldRigLayer.weight = currentHoldIkWeight;
        }

        if (holdArmTwoBoneIkConstraint != null)
        {
            holdArmTwoBoneIkConstraint.weight = currentHoldIkWeight;
        }
    }

    private void ApplyImmediateHoldIkWeight(
        ref float currentHoldIkWeight,
        Rig handHoldRigLayer,
        TwoBoneIKConstraint holdArmTwoBoneIkConstraint
    )
    {
        float desiredHoldIkWeight = ResolveDesiredHoldIkWeight();

        if (handHoldRigLayer != null)
        {
            handHoldRigLayer.weight = desiredHoldIkWeight;
        }

        if (holdArmTwoBoneIkConstraint != null)
        {
            holdArmTwoBoneIkConstraint.weight = desiredHoldIkWeight;
        }

        currentHoldIkWeight = desiredHoldIkWeight;
    }

    private float ResolveDesiredHoldIkWeight()
    {
        if (thirdPersonController == null)
        {
            return Mathf.Clamp01(holdRigWeightWhenActive);
        }

        return thirdPersonController.Grounded ? holdIkWeightWhenGrounded : holdIkWeightWhenAirborne;
    }
}

public enum PickupHandSide
{
    Right,
    Left,
}
