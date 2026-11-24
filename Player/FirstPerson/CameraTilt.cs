using UnityEngine;

// The camera's part of a first person animation (13/10), as the camera bone of a shooter's view model: the reload
// keys FirstPersonArms.cameraPitch and this turns the eye down by it AFTER the player's look is set, while the view
// model turns back up by the same angle about the eye, so the arms stay where the clip put them and the eye looks down
// at the open cylinder (with the muzzle down the gun sat at the bottom edge or the right forearm came up in front of
// the face). On the ViewModel (child of the camera); the controller writes the look from its own pitch every frame,
// so nothing accumulates. Runs before ArmsFromRig (110), which copies the arms onto the body from their final place.
[DefaultExecutionOrder(100)]
public sealed class CameraTilt : MonoBehaviour
{
    [SerializeField]
    private FirstPersonArms arms;

    private void LateUpdate()
    {
        float pitch = arms != null ? arms.CameraPitch : 0f;
        if (Mathf.Abs(pitch) < 0.01f || transform.parent == null)
        {
            return;
        }

        Quaternion tilt = Quaternion.Euler(pitch, 0f, 0f);
        Transform eye = transform.parent;
        eye.localRotation = eye.localRotation * tilt;
        Quaternion back = Quaternion.Inverse(tilt);
        transform.localPosition = back * transform.localPosition;
        transform.localRotation = back * transform.localRotation;
    }
}
