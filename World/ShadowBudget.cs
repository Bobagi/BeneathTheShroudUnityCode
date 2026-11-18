using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Light through solid walls: a URP point light without a shadow map lights everything inside its range, the
// far side of a wall included (the fungus glow on the kitchen stove, the lamp of one room on the next). The
// fix is the shadow map, but a point light's costs six (Unity, "Believable visuals: indoor and local
// lighting"), so not every lamp of the house can have one at once. This gives one to the lights that weigh
// most where the player stands, ranked by distance over range (a fungus 1.2 m away with a 3 m reach comes
// before a big lamp 11 m away with a 15 m reach; ranking by distance to the edge of the range did the
// opposite and left the fungus behind the kitchen wall lighting the stove), until GameTuning > Iluminação >
// shadowMaps is spent (a point light costs 6 maps, a spot 1), and takes it from the rest: a light far from the player lights rooms the player cannot see. URP itself only
// draws the shadows of the lights in view, so the budget is an upper bound. Sampled four times a second,
// no allocation after Start.
public sealed class ShadowBudget : MonoBehaviour
{
    // Ranges below this are tiny indicator glows (a LED): never worth a shadow map.
    private const float MinRange = 0.8f;

    [SerializeField]
    [Tooltip("Luzes filhas deste objeto ficam de fora (a lanterna do jogador cuida da própria sombra).")]
    private Transform exclude;

    private Light[] lights = new Light[0];
    private bool[] fills = new bool[0];
    private float[] keys = new float[0];
    private int[] order = new int[0];
    private Transform view;
    private float nextSample;

    private void Start()
    {
        List<Light> found = new List<Light>();
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (light.type != LightType.Point && light.type != LightType.Spot)
            {
                continue;
            }

            if (light.range < MinRange || (exclude != null && light.transform.IsChildOf(exclude)))
            {
                continue;
            }

            // The lamps the builder gave a shadow (the chandelier, the sitting-room lamp: the shadows the player
            // looks at) keep the medium tier; the rest take the low one, so a full budget (96 maps) still fits
            // the 4096 atlas without URP shrinking every map.
            light.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier = light.shadows != LightShadows.None
                ? UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium
                : UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
            // Lamps hang a few centimetres from shades and walls: a near plane of 0.1 m still catches them.
            light.shadowNearPlane = Mathf.Min(light.shadowNearPlane, 0.1f);
            found.Add(light);
        }

        lights = found.ToArray();
        fills = new bool[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            // The lamps' fill lights (FloodedHouseBuilder.ElectricLight names them "<lamp>Bounce").
            fills[i] = lights[i].name.EndsWith("Bounce");
        }

        keys = new float[lights.Length];
        order = new int[lights.Length];
        Sample();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextSample)
        {
            Sample();
        }
    }

    private void Sample()
    {
        nextSample = Time.unscaledTime + 0.25f;
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
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            bool lit = light != null && light.isActiveAndEnabled && light.intensity > 0.01f;
            float distance = lit ? Vector3.Distance(eye, light.transform.position) : 0f;
            keys[i] = lit && distance < light.range + GameTuning.Instance.shadowReach ? distance / light.range : float.MaxValue;
            order[i] = i;
        }

        System.Array.Sort(keys, order);
        int maps = GameTuning.Instance.shadowMaps;
        for (int rank = 0; rank < order.Length; rank++)
        {
            Light light = lights[order[rank]];
            if (light == null)
            {
                continue;
            }

            int cost = light.type == LightType.Point ? 6 : 1;
            bool cast = cost <= maps && keys[rank] < float.MaxValue && (GameTuning.Instance.fillShadows || !fills[order[rank]]);
            if (cast)
            {
                maps -= cost;
            }

            LightShadows wanted = cast ? LightShadows.Soft : LightShadows.None;
            if (light.shadows != wanted)
            {
                light.shadows = wanted;
            }
        }
    }
}
