using UnityEngine;

// Emergency lamp on a dying battery: a slow red pulse that now and then drops out for a moment.
[RequireComponent(typeof(Light))]
public sealed class PulsingLight : MonoBehaviour
{
    [SerializeField]
    private float period = 2.6f;

    [SerializeField]
    private float dropoutChancePerSecond = 0.08f;

    private Light target;
    private float baseIntensity;
    private float phase;
    private float dropoutUntil;

    private void Awake()
    {
        target = GetComponent<Light>();
        baseIntensity = target.intensity;
        phase = Random.value * period;
    }

    private void Update()
    {
        if (Time.time < dropoutUntil)
        {
            target.intensity = baseIntensity * 0.05f;
            return;
        }

        if (Random.value < dropoutChancePerSecond * Time.deltaTime)
        {
            dropoutUntil = Time.time + Random.Range(0.08f, 0.5f);
        }

        float wave = 0.5f + 0.5f * Mathf.Sin((Time.time + phase) / period * Mathf.PI * 2f);
        target.intensity = baseIntensity * Mathf.Lerp(0.35f, 1f, wave * wave);
    }
}
