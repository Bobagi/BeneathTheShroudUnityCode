using System.Collections.Generic;
using UnityEngine;

// A stretch of the house taken by the bioluminescent fungus: threat and protection at once (Dayse's demo
// script). The creatures never come into it (CreatureChase stops at its edge), and its spores burn the
// lungs: Respirator reads the density here. Light density near the glowing walls only makes the player
// cough; dense spores (the north corridor) need the mask and use up its filter. Zones are world boxes kept
// in a static registry, so asking costs no physics.
public sealed class FungusZone : MonoBehaviour
{
    [SerializeField]
    private Bounds area;

    [Tooltip("Spores in the air: 0 none, 0.3 a cough, 1 nobody breathes this without a mask.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float sporeDensity = 0.3f;

    [Tooltip("The creatures do not come in here.")]
    [SerializeField]
    private bool safeFromCreatures = true;

    private static readonly List<FungusZone> Zones = new List<FungusZone>();

    public Bounds Area => area;

    public void Configure(Bounds box, float density, bool safe)
    {
        area = box;
        sporeDensity = density;
        safeFromCreatures = safe;
    }

    private void OnEnable()
    {
        Zones.Add(this);
    }

    private void OnDisable()
    {
        Zones.Remove(this);
    }

    public static float SporeDensityAt(Vector3 point)
    {
        float density = 0f;
        for (int i = 0; i < Zones.Count; i++)
        {
            if (Zones[i].area.Contains(point))
            {
                density = Mathf.Max(density, Zones[i].sporeDensity);
            }
        }

        return density;
    }

    public static bool IsSafe(Vector3 point)
    {
        for (int i = 0; i < Zones.Count; i++)
        {
            if (Zones[i].safeFromCreatures && Zones[i].area.Contains(point))
            {
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = safeFromCreatures ? new Color(0.3f, 1f, 0.6f) : new Color(1f, 0.5f, 0.9f);
        Gizmos.DrawWireCube(area.center, area.size);
    }
}
