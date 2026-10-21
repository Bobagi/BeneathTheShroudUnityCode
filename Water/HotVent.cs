using System.Collections.Generic;
using UnityEngine;

// A crack in the floor venting scalding water: air runs out faster near it. The bubbles are a child
// particle system; this component only answers "how close to heat is this point?".
public sealed class HotVent : MonoBehaviour
{
    [SerializeField]
    private float radius = 2.5f;

    private static readonly List<HotVent> Active = new List<HotVent>();

    public void Configure(float heatRadius)
    {
        radius = heatRadius;
    }

    public static bool IsNearHeat(Vector3 point)
    {
        foreach (HotVent vent in Active)
        {
            if ((vent.transform.position - point).sqrMagnitude <= vent.radius * vent.radius)
            {
                return true;
            }
        }

        return false;
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
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
