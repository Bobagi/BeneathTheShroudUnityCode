using UnityEngine;

public enum ItemKind
{
    Generic = 0,
    Lantern = 1,
    Revolver = 2,
}

// Static data shared by every instance of an item type: how it is named, how it sits in the
// hand and on the belt, and what it sounds like when handled.
[CreateAssetMenu(menuName = "Horror/Item Definition", fileName = "ItemDefinition")]
public sealed class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Item";
    public ItemKind kind = ItemKind.Generic;
    [Tooltip("Hotbar icon (rendered by Horror > Prototype > Render Item Icons).")]
    public Sprite icon;

    [Header("Held in the hand (relative to the hand socket)")]
    [Tooltip("Local offset of the item's grip from the hand socket, in the socket's space.")]
    public Vector3 heldPositionOffset = Vector3.zero;
    [Tooltip("Local rotation of the item relative to the hand socket.")]
    public Vector3 heldRotationEuler = Vector3.zero;

    [Header("Stowed on the belt (relative to the hip socket)")]
    public bool visibleWhenStowed = true;
    public Vector3 stowedPositionOffset = Vector3.zero;
    public Vector3 stowedRotationEuler = Vector3.zero;

    [Header("Aiming")]
    [Tooltip("Item-local direction that should point at the aim target when the item is raised.")]
    public Vector3 aimForwardLocal = Vector3.forward;
    [Tooltip("Item-local up direction kept upright while aiming.")]
    public Vector3 aimUpLocal = Vector3.up;
    [Tooltip("How far in front of the shoulder the hand goes when the item is raised.")]
    public float raisedReach = 0.55f;
    [Tooltip("Vertical offset of the raised hand relative to the aim line (negative lowers it).")]
    public float raisedHeightOffset = 0f;
    [Tooltip("Keep the item level while raised (a lantern stays upright instead of pitching with the aim).")]
    public bool keepUprightWhenRaised = false;
    public bool zoomCameraWhenRaised = true;
    public bool showReticleWhenRaised = true;

    [Header("Audio")]
    public AudioClip pickupSound;
    public AudioClip dropSound;
    public AudioClip equipSound;
}
