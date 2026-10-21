using System.Collections;
using UnityEngine;

// The house mains: the electric light fittings (ceiling lamps, the chandelier, wall sconces) and their
// glowing bulbs. Cut() is the blackout of Dayse's scene 2: after a thunderclap the fittings stutter for a
// second and die, and only the bioluminescent fungus is left. The security panel and the lab's red
// emergency lights run on batteries and stay on.
public sealed class PowerGrid : MonoBehaviour
{
    [SerializeField]
    private Light[] lights;

    [SerializeField]
    private Renderer[] glowing;

    [SerializeField]
    private Material offMaterial;

    // Shades that glow while the mains are on (FloodedHouseDressing.LitFixture): each renderer with its dark material
    // and its lit one, swapped at the blackout.
    [SerializeField]
    private Renderer[] litFixtures = new Renderer[0];

    [SerializeField]
    private Material[] litFixturesOff = new Material[0];

    [SerializeField]
    private Material[] litFixturesOn = new Material[0];

    [SerializeField]
    private AudioSource source;

    public bool IsOn { get; private set; } = true;

    public void Cut()
    {
        if (IsOn)
        {
            IsOn = false;
            StartCoroutine(Blackout());
        }
    }

    // Loading a checkpoint after the blackout: dark at once, without the stutter or the sound.
    public void RestoreCut()
    {
        if (!IsOn)
        {
            return;
        }

        IsOn = false;
        SetLights(false);
        SwitchOffGlow();
    }

    private IEnumerator Blackout()
    {
        if (source != null)
        {
            source.PlayOneShot(SoundLibrary.Instance.PowerCut(), 0.85f);
        }

        float[] levels = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            levels[i] = lights[i] != null ? lights[i].intensity : 0f;
        }

        // A brown-out stutter, then nothing.
        for (int flicker = 0; flicker < 7; flicker++)
        {
            bool on = flicker % 2 == 1;
            SetLights(on);
            yield return new WaitForSeconds(Random.Range(0.05f, 0.22f));
        }

        SetLights(false);
        SwitchOffGlow();
    }

    private void SwitchOffGlow()
    {
        for (int i = 0; i < litFixtures.Length && i < litFixturesOff.Length; i++)
        {
            Renderer r = litFixtures[i];
            if (r == null || litFixturesOff[i] == null)
            {
                continue;
            }

            Material[] mats = r.sharedMaterials;
            for (int k = 0; k < mats.Length; k++)
            {
                if (mats[k] != null && i < litFixturesOn.Length && mats[k] == litFixturesOn[i])
                {
                    mats[k] = litFixturesOff[i];
                }
            }

            r.sharedMaterials = mats;
        }

        foreach (Renderer r in glowing)
        {
            if (r == null)
            {
                continue;
            }

            if (r.name == "Flame" || r.name == "Bulb")
            {
                r.enabled = false;
                continue;
            }

            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name.StartsWith("LanternGlassGlow"))
                {
                    mats[i] = offMaterial;
                }
            }

            r.sharedMaterials = mats;
        }
    }

    private void SetLights(bool on)
    {
        foreach (Light light in lights)
        {
            if (light != null)
            {
                light.enabled = on;
            }
        }
    }
}
