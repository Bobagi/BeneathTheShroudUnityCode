using UnityEngine;

// Holds the lantern with REAL physics while equipped, ported from the Lamparina sandbox
// project. The key idea that earlier joint attempts here were missing: the lantern is
// never parented to the animated hand bone. Instead a kinematic anchor rigidbody chases
// the hand socket with MovePosition every FixedUpdate (PhysX sees proper velocities, not
// teleports), the handle is glued to that anchor by a FixedJoint, and the body keeps
// hanging from the prefab's own HingeJoint. A zero-target motor on the hinge acts as
// bearing friction and extra damping while held plays the role of the hand absorbing
// the swing, so the body settles plumb a couple of seconds after each move.
public sealed class HeldLanternPhysicsRig : MonoBehaviour
{
    private const string GripMarkerName = "LanternHandleGrip";

    private const float HingeFrictionTorque = 0.3f;
    private const float HeldLinearDamping = 1.3f;
    private const float HeldAngularDamping = 2.5f;
    private const float HandleHeldAngularDamping = 4f;

    private Transform handSocketTransform;
    private Vector3 gripLocalOffsetInSocket;
    private Quaternion heldRotation;
    private Rigidbody anchorRigidbody;
    private GameObject lanternInstance;

    // Builds the whole held rig and returns the anchor game object, which acts as the
    // root of the held item for the caller: destroying it tears the lantern down too.
    public static GameObject Attach(
        GameObject lanternGameObject,
        InteractablePickupItem spawnedItem,
        Transform handSocketTransform,
        Transform playerRoot
    )
    {
        // The world-item snapper re-snaps to the floor on every OnEnable; the held copy
        // must never do that.
        GroundSnapToSurface groundSnap = lanternGameObject.GetComponent<GroundSnapToSurface>();
        if (groundSnap != null)
        {
            groundSnap.enabled = false;
        }

        Rigidbody handleRigidbody = spawnedItem.PickupHandleRigidbodyToConnectToHandFixedJoint;
        Transform gripTransform = FindGripMarker(lanternGameObject.transform);
        if (gripTransform == null)
        {
            gripTransform = handleRigidbody.transform;
        }

        // Pose the lantern so the grip lands exactly where the kinematic version puts it:
        // (0, YAxisOffSet, 0) in the hand socket's frame, rotated by HeldRotationEuler.
        Quaternion heldRotation = Quaternion.Euler(spawnedItem.HeldRotationEuler);
        Vector3 gripLocalOffsetInSocket = new Vector3(0f, spawnedItem.YAxisOffSet, 0f);
        Vector3 gripTargetPosition = handSocketTransform.TransformPoint(gripLocalOffsetInSocket);
        Quaternion targetRotation = handSocketTransform.rotation * heldRotation;

        Transform lantern = lanternGameObject.transform;
        lantern.rotation = targetRotation;
        lantern.position += gripTargetPosition - gripTransform.position;
        Physics.SyncTransforms();

        // The lantern stays a live physics assembly: dynamic bodies, gravity, and the
        // stability settings that keep the Lamparina rig calm (solver iterations,
        // interpolation, capped spin, damping standing in for the hand's absorption).
        foreach (Rigidbody itemRigidbody in lanternGameObject.GetComponentsInChildren<Rigidbody>(true))
        {
            itemRigidbody.isKinematic = false;
            itemRigidbody.useGravity = true;
            itemRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            itemRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            itemRigidbody.solverIterations = 16;
            itemRigidbody.solverVelocityIterations = 8;
            itemRigidbody.maxAngularVelocity = 10f;
            itemRigidbody.linearDamping = Mathf.Max(itemRigidbody.linearDamping, HeldLinearDamping);
            itemRigidbody.angularDamping = Mathf.Max(itemRigidbody.angularDamping, HeldAngularDamping);
            itemRigidbody.linearVelocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
        }
        handleRigidbody.angularDamping = Mathf.Max(
            handleRigidbody.angularDamping,
            HandleHeldAngularDamping
        );

        // Free hinge + zero-target motor: the motor with targetVelocity 0 and a small
        // torque budget behaves like bearing friction, which is what actually makes the
        // pendulum STOP instead of swinging forever (dampers alone never kill it).
        foreach (HingeJoint hinge in lanternGameObject.GetComponentsInChildren<HingeJoint>(true))
        {
            hinge.useLimits = false;
            hinge.useSpring = false;
            hinge.useMotor = true;
            hinge.motor = new JointMotor
            {
                force = HingeFrictionTorque,
                targetVelocity = 0f,
                freeSpin = false,
            };
        }

        // Colliders stay on so the lantern can bump into the world, but never against
        // the player (CharacterController included), which is what used to shove the
        // character around on the shared hand layer.
        if (playerRoot != null)
        {
            Collider[] playerColliders = playerRoot.GetComponentsInChildren<Collider>(true);
            foreach (Collider lanternCollider in lanternGameObject.GetComponentsInChildren<Collider>(true))
            {
                lanternCollider.enabled = true;
                foreach (Collider playerCollider in playerColliders)
                {
                    Physics.IgnoreCollision(lanternCollider, playerCollider, true);
                }
            }
        }

        GameObject anchorGameObject = new GameObject("LanternPhysicsAnchor");
        anchorGameObject.transform.SetPositionAndRotation(gripTargetPosition, targetRotation);

        Rigidbody anchorRigidbody = anchorGameObject.AddComponent<Rigidbody>();
        anchorRigidbody.isKinematic = true;

        HeldLanternPhysicsRig rig = anchorGameObject.AddComponent<HeldLanternPhysicsRig>();
        rig.handSocketTransform = handSocketTransform;
        rig.gripLocalOffsetInSocket = gripLocalOffsetInSocket;
        rig.heldRotation = heldRotation;
        rig.anchorRigidbody = anchorRigidbody;
        rig.lanternInstance = lanternGameObject;

        FixedJoint grabJoint = handleRigidbody.gameObject.AddComponent<FixedJoint>();
        grabJoint.connectedBody = anchorRigidbody;

        return anchorGameObject;
    }

    private void FixedUpdate()
    {
        if (handSocketTransform == null)
        {
            return;
        }

        anchorRigidbody.MovePosition(handSocketTransform.TransformPoint(gripLocalOffsetInSocket));
        anchorRigidbody.MoveRotation(handSocketTransform.rotation * heldRotation);
    }

    private void OnDestroy()
    {
        // The caller treats the anchor as the held item's root, so dropping the item
        // destroys this object; take the free-floating lantern instance down with it.
        if (lanternInstance != null)
        {
            Destroy(lanternInstance);
        }
    }

    private static Transform FindGripMarker(Transform rootTransform)
    {
        foreach (Transform childTransform in rootTransform.GetComponentsInChildren<Transform>(true))
        {
            if (childTransform.name == GripMarkerName)
            {
                return childTransform;
            }
        }

        return null;
    }
}
