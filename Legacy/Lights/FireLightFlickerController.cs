using UnityEngine;

public sealed class FireLightFlickerController : MonoBehaviour
{
    [SerializeField]
    private Light targetLight;

    [Header("Intensity")]
    [SerializeField]
    private float baseIntensity = 1.2f;

    [SerializeField]
    private float intensityAmplitude = 0.35f;

    [SerializeField]
    private float intensityNoiseSpeed = 6f;

    [Header("Range")]
    [SerializeField]
    private float baseRange = 8f;

    [SerializeField]
    private float rangeAmplitude = 0.4f;

    [SerializeField]
    private float rangeNoiseSpeed = 4f;

    [Header("Smoothing")]
    [SerializeField]
    private float smoothingSpeed = 12f;

    [Header("Noise Seed")]
    [SerializeField]
    private float noiseOffset = 0.17f;

    private float currentIntensity;
    private float currentRange;

    private void Awake()
    {
        if (targetLight == null)
        {
            targetLight = GetComponent<Light>();
        }

        currentIntensity = baseIntensity;
        currentRange = baseRange;
    }

    private void Update()
    {
        float timeSeconds = Time.time;

        float intensityNoise = Mathf.PerlinNoise(noiseOffset, timeSeconds * intensityNoiseSpeed);
        float rangeNoise = Mathf.PerlinNoise(noiseOffset + 10f, timeSeconds * rangeNoiseSpeed);

        float targetIntensityValue =
            baseIntensity + ((intensityNoise * 2f) - 1f) * intensityAmplitude;
        float targetRangeValue = baseRange + ((rangeNoise * 2f) - 1f) * rangeAmplitude;

        currentIntensity = Mathf.Lerp(
            currentIntensity,
            targetIntensityValue,
            Time.deltaTime * smoothingSpeed
        );
        currentRange = Mathf.Lerp(currentRange, targetRangeValue, Time.deltaTime * smoothingSpeed);

        targetLight.intensity = Mathf.Max(0f, currentIntensity);
        targetLight.range = Mathf.Max(0.01f, currentRange);
    }
}
