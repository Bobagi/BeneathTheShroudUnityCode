using UnityEngine;

// An item that exists in the world. The same GameObject travels between the ground, the hand and
// the belt, so per-instance state (ammo in the revolver, lantern on/off) survives pickup and drop.
[RequireComponent(typeof(Rigidbody))]
public sealed class WorldItem : MonoBehaviour
{
    [SerializeField]
    private ItemDefinition definition;

    [SerializeField]
    [Tooltip("Point the hand closes around. Its local frame defines how the item sits in the hand.")]
    private Transform grip;

    [SerializeField]
    [Tooltip("Where the interaction prompt / reach animation aims. Defaults to the grip.")]
    private Transform interactionPoint;

    private Rigidbody body;
    private Collider[] colliders;
    private int worldLayer;

    public ItemDefinition Definition => definition;
    public Transform Grip => grip != null ? grip : transform;
    public Transform InteractionPoint => interactionPoint != null ? interactionPoint : Grip;
    public bool IsHeld { get; private set; }
    public string DisplayName => definition != null ? definition.displayName : name;
    public ItemKind Kind => definition != null ? definition.kind : ItemKind.Generic;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>(true);
        worldLayer = gameObject.layer;
    }

    public bool CanBePickedUp => !IsHeld && isActiveAndEnabled;

    // Glues the item to a socket so the item's grip lands exactly on the socket plus the given offset.
    // Pure transform parenting: the hand bone is posed by the Animator and Animation Rigging, and only
    // the hierarchy follows it without lag or flicker.
    public void AttachTo(Transform socket, Vector3 localPosition, Quaternion localRotation)
    {
        IsHeld = true;
        SetPhysicsEnabled(false);

        transform.SetParent(socket, false);
        Transform gripTransform = Grip;
        Quaternion gripLocalRotation = Quaternion.Inverse(transform.rotation) * gripTransform.rotation;
        Vector3 gripLocalPosition = transform.InverseTransformPoint(gripTransform.position);
        Vector3 itemScale = transform.localScale;

        // Rotation: grip frame == requested rotation. Position: grip point == requested position.
        transform.localRotation = localRotation * Quaternion.Inverse(gripLocalRotation);
        transform.localPosition =
            localPosition - transform.localRotation * Vector3.Scale(gripLocalPosition, itemScale);
    }

    public void ReleaseToWorld(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        IsHeld = false;
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, rotation);
        gameObject.SetActive(true);
        SetPhysicsEnabled(true);
        body.linearVelocity = velocity;
        body.angularVelocity = Random.insideUnitSphere * 2f;
    }

    public void SetVisible(bool visible)
    {
        foreach (Renderer itemRenderer in GetComponentsInChildren<Renderer>(true))
        {
            itemRenderer.enabled = visible;
        }
    }

    private void SetPhysicsEnabled(bool enabledInWorld)
    {
        if (body == null)
        {
            Awake();
        }

        // Held items must not collide with the player's CharacterController, and a kinematic body
        // with interpolation would trail behind the fast-moving hand bone.
        body.isKinematic = !enabledInWorld;
        body.interpolation = enabledInWorld
            ? RigidbodyInterpolation.Interpolate
            : RigidbodyInterpolation.None;
        body.detectCollisions = enabledInWorld;
        foreach (Collider itemCollider in colliders)
        {
            itemCollider.enabled = enabledInWorld;
        }

        gameObject.layer = worldLayer;
    }
}
