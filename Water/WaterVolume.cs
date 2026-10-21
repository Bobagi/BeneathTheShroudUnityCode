using System.Collections.Generic;
using UnityEngine;

// A box of water with its own surface height. The box can reach above the surface: that part is air
// (the air pocket in the lab is a room whose volume stops 1 m under the ceiling). Queries go through
// a static registry instead of physics triggers, so the camera, the player body and effects can all
// ask "am I in water, and how deep?" cheaply every frame.
public sealed class WaterVolume : MonoBehaviour
{
    [SerializeField]
    private Vector3 size = new Vector3(4f, 2f, 4f);

    [Tooltip("World height of the water surface. Can sit above the box top when the room is flooded to the ceiling.")]
    [SerializeField]
    private float surfaceHeight;

    [Tooltip("Deep water lets the player swim; shallow water only slows and splashes.")]
    [SerializeField]
    private bool deep = true;

    [Tooltip("Turbidity: metres after which nothing can be seen at all (murky lab water is short).")]
    [SerializeField]
    private float visibility = 12f;

    private static readonly List<WaterVolume> Active = new List<WaterVolume>();

    public float SurfaceHeight => surfaceHeight;
    public bool IsDeep => deep;
    public float Visibility => visibility;

    public void Configure(Vector3 boxSize, float surface, bool isDeep, float visibilityMetres)
    {
        size = boxSize;
        surfaceHeight = surface;
        deep = isDeep;
        visibility = visibilityMetres;
    }

    // For water that rises during play (the ground floor flooding at the end of the demo).
    public void SetSurface(float height)
    {
        surfaceHeight = height;
    }

    public bool Contains(Vector3 point)
    {
        Vector3 local = point - transform.position;
        Vector3 half = size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    // The volume that holds this point, if any. Deep volumes win when two overlap.
    public static WaterVolume Find(Vector3 point)
    {
        WaterVolume found = null;
        foreach (WaterVolume volume in Active)
        {
            if (volume.Contains(point) && (found == null || (volume.deep && !found.deep)))
            {
                found = volume;
            }
        }

        return found;
    }

    public static bool IsUnderwater(Vector3 point)
    {
        WaterVolume volume = Find(point);
        return volume != null && point.y < volume.surfaceHeight;
    }

    private void OnEnable()
    {
        Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.6f, 0.7f, 0.35f);
        Gizmos.DrawWireCube(transform.position, size);
        float top = Mathf.Min(surfaceHeight, transform.position.y + size.y * 0.5f);
        Gizmos.DrawCube(new Vector3(transform.position.x, top, transform.position.z), new Vector3(size.x, 0.02f, size.z));
    }
}
