using System.Collections;
using UnityEngine;

// The house mains: the electric light fittings (ceiling lamps, the chandelier, wall sconces) and their
// glowing bulbs. Since 29/10 (decision of Gustavo with Dayse: the house is abandoned) the house starts dark
// (StartDark); the security panel, the climate system (its own generators, GDD) and the lab's red emergency
// lights run apart and stay on. In the generator room the player starts the emergency generator (Emergency):
// the fittings come back at a third of their light, browning out now and then. Cut() is the stutter and death
// of the light (the finale, or the old blackout of Dayse's scene 2).
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

    // On the emergency generator: weak and unsteady.
    public bool OnEmergency { get; private set; }

    private const float EmergencyLevel = 0.35f;
    private Coroutine brownouts;

    // The game's start: the fittings dark, silent.
    public void StartDark()
    {
        TunedLamp.SetGridLevel(1f);
        OnEmergency = false;
        IsOn = true;
        RestoreCut();
    }

    // The emergency generator: the lights stutter on at a third, and dip now and then while it runs.
    public void Emergency(bool silent = false)
    {
        if (IsOn && OnEmergency)
        {
            return;
        }

        IsOn = true;
        OnEmergency = true;
        TunedLamp.SetGridLevel(EmergencyLevel);
        if (silent)
        {
            SetLights(true);
            SwitchGlow(true);
        }
        else
        {
            StartCoroutine(PowerUp());
        }

        if (brownouts != null)
        {
            StopCoroutine(brownouts);
        }

        brownouts = StartCoroutine(Brownouts());
    }

    private IEnumerator PowerUp()
    {
        for (int flicker = 0; flicker < 9; flicker++)
        {
            SetLights(flicker % 2 == 1);
            yield return new WaitForSeconds(Random.Range(0.06f, 0.3f));
        }

        SetLights(true);
        SwitchGlow(true);
    }

    // A tired motor: every 8 to 25 s the light sags for a moment, sometimes goes out for a blink.
    private IEnumerator Brownouts()
    {
        while (IsOn && OnEmergency)
        {
            yield return new WaitForSeconds(Random.Range(8f, 25f));
            if (!IsOn)
            {
                break;
            }

            int dips = Random.Range(1, 4);
            for (int i = 0; i < dips; i++)
            {
                TunedLamp.SetGridLevel(EmergencyLevel * Random.Range(0.15f, 0.6f));
                yield return new WaitForSeconds(Random.Range(0.05f, 0.25f));
                TunedLamp.SetGridLevel(EmergencyLevel);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.4f));
            }
        }
    }

    public void Cut()
    {
        if (IsOn)
        {
            IsOn = false;
            OnEmergency = false;
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
        OnEmergency = false;
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

    private void SwitchOffGlow() => SwitchGlow(false);

    // The shades and bulbs that glow with the light: lit materials and bulbs on, or the dark ones. The old oil
    // lamps' glass is only ever switched off (nothing lights them again).
    private void SwitchGlow(bool on)
    {
        for (int i = 0; i < litFixtures.Length && i < litFixturesOff.Length && i < litFixturesOn.Length; i++)
        {
            Renderer r = litFixtures[i];
            if (r == null || litFixturesOff[i] == null || litFixturesOn[i] == null)
            {
                continue;
            }

            Material from = on ? litFixturesOff[i] : litFixturesOn[i], to = on ? litFixturesOn[i] : litFixturesOff[i];
            Material[] mats = r.sharedMaterials;
            for (int k = 0; k < mats.Length; k++)
            {
                if (mats[k] == from)
                {
                    mats[k] = to;
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
                r.enabled = on;
                continue;
            }

            if (on)
            {
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

    // The supply level is static (TunedLamp): back to the mains for whatever scene comes next.
    private void OnDestroy()
    {
        TunedLamp.ResetGridLevel();
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
