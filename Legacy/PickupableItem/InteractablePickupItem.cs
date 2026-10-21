using UnityEngine;

public enum InteractablePickupItemType
{
    Lantern = 0,
    Revolver = 1,
}

public sealed class InteractablePickupItem : MonoBehaviour
{
    [SerializeField]
    private InteractablePickupItemType itemType = InteractablePickupItemType.Lantern;

    [SerializeField]
    private string itemDisplayName = "Lantern";

    [SerializeField]
    private Transform pickupInteractionPoint;

    [SerializeField]
    private Rigidbody pickupHandleRigidbodyToConnectToHandFixedJoint;

    [SerializeField]
    private float yAxisOffSet = 0f;

    [SerializeField]
    [Tooltip("Extra rotation (Euler degrees) applied to the item while held in the hand. Tweak this to fix the orientation of the item in the hand.")]
    private Vector3 heldRotationEuler = Vector3.zero;

    [SerializeField]
    private GameObject pickupItemPrefab;

    [SerializeField]
    private bool destroyGameObjectAfterPickup = true;

    private bool hasBeenPickedUp;

    public InteractablePickupItemType ItemType => itemType;
    public string ItemDisplayName => itemDisplayName;
    public Transform PickupInteractionPoint =>
        pickupInteractionPoint != null ? pickupInteractionPoint : transform;
    public Rigidbody PickupHandleRigidbodyToConnectToHandFixedJoint =>
        pickupHandleRigidbodyToConnectToHandFixedJoint;
    public float YAxisOffSet => yAxisOffSet;
    public Vector3 HeldRotationEuler => heldRotationEuler;
    public GameObject PickupItemPrefab => pickupItemPrefab;

    public bool CanBePickedUp()
    {
        return !hasBeenPickedUp && gameObject.activeInHierarchy;
    }

    public void OnPickedUpBy()
    {
        if (hasBeenPickedUp)
        {
            return;
        }

        hasBeenPickedUp = true;

        if (destroyGameObjectAfterPickup)
        {
            Destroy(gameObject);
            return;
        }

        gameObject.SetActive(false);
    }
}
