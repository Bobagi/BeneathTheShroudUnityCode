using UnityEngine;

[ExecuteAlways]
public sealed class GroundSnapToSurface : MonoBehaviour
{
    [SerializeField]
    private LayerMask groundLayerMask = ~0;

    [SerializeField]
    private float raycastDistance = 3f;

    [SerializeField]
    private float surfaceOffset = 0.02f;

    [SerializeField]
    private bool alignToSurfaceNormal;

    private void OnEnable()
    {
        SnapToSurface();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            SnapToSurface();
        }
    }

    public void SnapToSurface()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up * raycastDistance;
        float maxDistance = raycastDistance * 2f;

        if (
            Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                maxDistance,
                groundLayerMask,
                QueryTriggerInteraction.Ignore
            )
        )
        {
            transform.position = hit.point + Vector3.up * surfaceOffset;

            if (alignToSurfaceNormal)
            {
                transform.up = hit.normal;
            }
        }
    }
}
