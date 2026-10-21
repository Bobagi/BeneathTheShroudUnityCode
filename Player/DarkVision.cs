using System.Collections.Generic;
using UnityEngine;

// "Dark vision": in darkness the player still makes out what is at arm's length, as if they gave off
// a faint light of their own. The technique Frictional describes for Amnesia: The Dark Descent
// ("the player has a vague light around her to help make the closest surroundings more easy to
// see"; frictionalgames.com/2009-11-the-struggle-between-light-and-dark). A weak, short-range,
// shadowless point light below eye level (so it lights the floor and hands, never a headlamp
// hot spot), that fades in only where the nearby practical lights leave the player in the dark,
// drops almost to nothing with the torch on, and turns short and teal under water.
[RequireComponent(typeof(Light))]
public sealed class DarkVision : MonoBehaviour
{
    [SerializeField]
    private DiveGear gear;

    [SerializeField]
    private Transform eye;

    [Header("In air")]
    [SerializeField]
    // Research starting point was 0.35-0.5; under this project's grade it read as nothing.
    private float intensity = 1.0f;

    [SerializeField]
    private float range = 4f;

    [SerializeField]
    private Color color = new Color(0.75f, 0.85f, 1f);

    [Header("Under water")]
    [SerializeField]
    private float underwaterIntensity = 0.8f;

    [SerializeField]
    private float underwaterRange = 2.8f;

    [SerializeField]
    private Color underwaterColor = new Color(0.55f, 0.9f, 0.85f);

    [Header("Response")]
    [Tooltip("Summed nearby light (intensity / distance^2) above which the player needs no help.")]
    [SerializeField]
    private float litThreshold = 0.3f;

    [SerializeField]
    private float torchShare = 0.1f;

    [SerializeField]
    private float fadeSpeed = 1.5f;

    private Light fill;
    private readonly List<Light> practicals = new List<Light>();
    private float darkness = 1f;
    private float nextSample;

    private void Start()
    {
        fill = GetComponent<Light>();
        fill.type = LightType.Point;
        fill.shadows = LightShadows.None;
        fill.renderMode = LightRenderMode.ForcePixel;
        // It sits at the eye: anything a hand's width from it (the chin, the nose, the forearms raised to the face)
        // burned white in the outside view (07/10). URP ignores a light's culling mask, so rendering layers do it:
        // the player's own body is on layer 2 (FloodedHousePlayerBody), this fill lights layer 1 only, and every
        // other light in the scene lights everything (a light's default is layer 1 only, which would leave the
        // body in the dark).
        SetLayers(fill, 1u);
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light != fill)
            {
                SetLayers(light, uint.MaxValue);
            }
        }
        foreach (Light light in FindObjectsByType<Light>())
        {
            if (light != fill && light.type != LightType.Directional && !light.transform.IsChildOf(eye))
            {
                practicals.Add(light);
            }
        }
    }

    private void Update()
    {
        // Sampling five times a second is plenty for a value that fades over a second.
        if (Time.time >= nextSample)
        {
            nextSample = Time.time + 0.2f;
            darkness = 1f - Mathf.Clamp01(LocalLight() / litThreshold);
        }

        bool under = WaterVolume.IsUnderwater(eye.position);
        float target = (under ? GameTuning.Instance.darkVisionUnderwater : GameTuning.Instance.darkVisionIntensity) * darkness;
        if (gear != null && gear.TorchOn)
        {
            target *= torchShare;
        }

        fill.intensity = Mathf.MoveTowards(fill.intensity, target, fadeSpeed * intensity * Time.deltaTime);
        fill.range = under ? underwaterRange : range;
        fill.color = under ? underwaterColor : color;
    }

    private static void SetLayers(Light light, uint mask)
    {
        light.renderingLayerMask = unchecked((int)mask);
        UnityEngine.Rendering.Universal.UniversalAdditionalLightData data = light.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
        if (data != null)
        {
            data.renderingLayers = mask;
        }
    }

    private float LocalLight()
    {
        Vector3 at = eye.position;
        float sum = 0f;
        foreach (Light light in practicals)
        {
            if (light == null || !light.isActiveAndEnabled || light.intensity <= 0f)
            {
                continue;
            }

            float distance = Vector3.Distance(light.transform.position, at);
            if (distance < light.range)
            {
                float window = 1f - (distance / light.range) * (distance / light.range);
                sum += light.intensity / Mathf.Max(1f, distance * distance) * window;
            }
        }

        return sum;
    }
}
