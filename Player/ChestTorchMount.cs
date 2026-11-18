using UnityEngine;

// With the suit the dive torch is clipped to the chest (mechanic "Duas mãos", GDD): the torch (its light and model,
// normally hanging on the first person rig's torch socket) is moved onto the body's upper chest, pointing where the
// chest points, so the beam goes only where the body turns and both hands are free for the weapon.
// Mount() turns it on; Unmount() gives it back to the hand's socket.
public sealed class ChestTorchMount : MonoBehaviour
{
    [SerializeField]
    private FirstPersonController controller;

    [SerializeField]
    private Animator body;

    [Tooltip("The TorchHand object (light, model, TorchHold).")]
    [SerializeField]
    private TorchHold torch;

    [Tooltip("Where the torch sits on the chest, in the chest bone's frame turned to the body (right, up, forward).")]
    [SerializeField]
    private Vector3 offset = new Vector3(0.11f, 0.04f, 0.16f);

    [Tooltip("Degrees the beam is tipped down from the chest's forward.")]
    [SerializeField]
    private float tilt = 6f;

    private Transform torchHand;
    private Transform chest;
    private Transform originalParent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;

    public bool IsMounted { get; private set; }

    private void Awake()
    {
        chest = body.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null)
        {
            chest = body.GetBoneTransform(HumanBodyBones.Chest);
        }

        if (torch != null)
        {
            torchHand = torch.transform;
            originalParent = torchHand.parent;
            originalPosition = torchHand.localPosition;
            originalRotation = torchHand.localRotation;
            originalScale = torchHand.localScale;
        }
    }

    public void Mount()
    {
        if (torchHand == null || chest == null || IsMounted)
        {
            return;
        }

        IsMounted = true;
        torch.enabled = false;
        torchHand.SetParent(null, true);
        torchHand.localScale = Vector3.one;
    }

    public void Unmount()
    {
        if (!IsMounted)
        {
            return;
        }

        IsMounted = false;
        torchHand.SetParent(originalParent, false);
        torchHand.localPosition = originalPosition;
        torchHand.localRotation = originalRotation;
        torchHand.localScale = originalScale;
        torch.enabled = true;
    }

    // After the body's animation and IK: placed on the chest each frame (not parented to the bone, whose axes depend
    // on the rig). It sits where the chest is but points where the CAMERA points (yaw and pitch), tipped down a
    // little: the beam has to land where the player looks. Pointing it along the body's heading left the light on the
    // wall beside the view while the torso lagged the turn (Gustavo, 22/10: everything is driven by the camera).
    private void LateUpdate()
    {
        if (!IsMounted)
        {
            return;
        }

        Transform eye = controller != null ? controller.CameraPivot : null;
        Quaternion view = eye != null ? Quaternion.LookRotation(eye.forward, Vector3.up) : Quaternion.LookRotation(body.transform.forward, Vector3.up);
        Quaternion yawOnly = Quaternion.Euler(0f, view.eulerAngles.y, 0f);
        torchHand.position = chest.position + yawOnly * offset;
        torchHand.rotation = view * Quaternion.Euler(tilt, 0f, 0f);
    }
}
