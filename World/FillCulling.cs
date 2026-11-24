using UnityEngine;

// The lamps' fill lights (TunedLamp bounce: a shadowless point light hung mid-room to fill it) only where the player
// can see them. Measured in a build 13/10 (Docs/Screenshots/FloodedHouse/2026-10-13_perf): the Luna mansion has 45 of
// them with 8 to 12 m of reach, and from one room the next rooms' fills lit it through the walls (wrong light) and
// cost the most of the frame: GPU 19.1 -> 10.5 ms in the library and 14.2 -> 9.6 in the hall with every fill off.
// A fill is kept while a line from the eye reaches it or a point around it (a ring of `SpreadMetres`, so a room seen
// through a doorway keeps its fill) and fades out over GameTuning fillFadeSeconds when none does. Fading is done by
// switching the light's GameObject, not Light.enabled (the PowerGrid owns that), with the intensity eased through
// TunedLamp.SetVisibility. Sampled four times a second, no allocation after Start.
public sealed class FillCulling : MonoBehaviour
{
    private const float SpreadMetres = 0.9f;
    private const float SampleSeconds = 0.25f;

    private static readonly Vector3[] Offsets =
    {
        Vector3.zero,
        new Vector3(SpreadMetres, 0f, 0f), new Vector3(-SpreadMetres, 0f, 0f),
        new Vector3(0f, 0f, SpreadMetres), new Vector3(0f, 0f, -SpreadMetres),
        new Vector3(0f, -1.1f, 0f),
    };

    private TunedLamp[] fills = new TunedLamp[0];
    private float[] weights = new float[0];
    private bool[] visible = new bool[0];
    private Transform view;
    private float nextSample;

    private void Start()
    {
        int count = 0;
        TunedLamp[] all = FindObjectsByType<TunedLamp>(FindObjectsInactive.Include);
        foreach (TunedLamp lamp in all)
        {
            if (lamp.IsBounce)
            {
                count++;
            }
        }

        fills = new TunedLamp[count];
        count = 0;
        foreach (TunedLamp lamp in all)
        {
            if (lamp.IsBounce)
            {
                fills[count++] = lamp;
            }
        }

        weights = new float[fills.Length];
        visible = new bool[fills.Length];
        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] = 1f;
            visible[i] = true;
        }
    }

    private void Update()
    {
        if (!GameTuning.Instance.fillCulling)
        {
            for (int i = 0; i < fills.Length; i++)
            {
                Set(i, 1f);
            }

            return;
        }

        if (Time.unscaledTime >= nextSample)
        {
            Sample();
        }

        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, GameTuning.Instance.fillFadeSeconds);
        for (int i = 0; i < fills.Length; i++)
        {
            Set(i, Mathf.MoveTowards(weights[i], visible[i] ? 1f : 0f, step));
        }
    }

    private void Set(int i, float weight)
    {
        TunedLamp lamp = fills[i];
        if (lamp == null || (weight == weights[i] && lamp.gameObject.activeSelf == weight > 0f))
        {
            return;
        }

        weights[i] = weight;
        bool on = weight > 0f;
        if (lamp.gameObject.activeSelf != on)
        {
            lamp.gameObject.SetActive(on);
        }

        if (on)
        {
            lamp.SetVisibility(weight);
        }
    }

    private void Sample()
    {
        nextSample = Time.unscaledTime + SampleSeconds;
        if (view == null)
        {
            Camera main = Camera.main;
            if (main == null)
            {
                return;
            }

            view = main.transform;
        }

        Vector3 eye = view.position;
        for (int i = 0; i < fills.Length; i++)
        {
            TunedLamp lamp = fills[i];
            if (lamp == null)
            {
                continue;
            }

            Vector3 at = lamp.transform.position;
            float reach = lamp.Range + SpreadMetres;
            if ((at - eye).sqrMagnitude > reach * reach)
            {
                visible[i] = false;
                continue;
            }

            bool seen = false;
            for (int k = 0; k < Offsets.Length && !seen; k++)
            {
                seen = !Physics.Linecast(eye, at + Offsets[k], Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            }

            visible[i] = seen;
        }
    }
}
