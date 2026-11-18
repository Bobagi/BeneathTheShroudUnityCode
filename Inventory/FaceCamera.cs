using UnityEngine;

// A label that always faces the main camera (the test scene's item names).
public sealed class FaceCamera : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);
        }
    }
}
