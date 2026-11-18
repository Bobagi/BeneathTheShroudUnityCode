using Unity.Cinemachine;
using UnityEngine;

// Adds short bursts of shake on top of the follow camera's Perlin noise (gunshots, artillery).
public sealed class CameraShake : MonoBehaviour
{
    [SerializeField]
    private CinemachineBasicMultiChannelPerlin noise;

    [SerializeField]
    private float decayPerSecond = 1.6f;

    private static CameraShake instance;
    private float baseAmplitude;
    private float baseFrequency;
    private float extraAmplitude;

    public static void Shake(float amplitude, float durationSeconds)
    {
        if (instance == null)
        {
            return;
        }

        // Duration is folded into the decay: stronger/longer shakes start higher.
        instance.extraAmplitude = Mathf.Max(instance.extraAmplitude, amplitude * (1f + durationSeconds));
    }

    private void Awake()
    {
        instance = this;
        if (noise != null)
        {
            baseAmplitude = noise.AmplitudeGain;
            baseFrequency = noise.FrequencyGain;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (noise == null)
        {
            return;
        }

        extraAmplitude = Mathf.MoveTowards(extraAmplitude, 0f, decayPerSecond * Time.deltaTime);
        noise.AmplitudeGain = baseAmplitude + extraAmplitude * 3f;
        noise.FrequencyGain = baseFrequency + extraAmplitude * 6f;
    }
}
