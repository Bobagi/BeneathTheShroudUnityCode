using UnityEngine;

// Fire-like flicker for a point light (braziers, burning wrecks, glow under smoke plumes).
[RequireComponent(typeof(Light))]
public sealed class FlickerLight : MonoBehaviour
{
    [SerializeField]
    private float amount = 0.35f;

    [SerializeField]
    private float speed = 7f;

    private Light target;
    private float baseIntensity;
    private float seed;

    private void Awake()
    {
        target = GetComponent<Light>();
        baseIntensity = target.intensity;
        seed = Random.value * 100f;
    }

    // For lights whose level is tuned at runtime (TunedLamp): flicker around the new value.
    public void SetBaseIntensity(float intensity)
    {
        baseIntensity = intensity;
    }

    private void Update()
    {
        float t = Time.time * speed + seed;
        float noise = Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 2.7f, seed + 3f) * 0.3f;
        target.intensity = baseIntensity * (1f + (noise - 0.5f) * 2f * amount);
    }
}
