using UnityEngine;

// Oil lantern: a warm flickering point light that keeps burning in the hand, on the belt or on the
// ground. F toggles the wick; raising it (right mouse) holds it up in front to light the way.
public sealed class LanternItem : HeldItemBehaviour
{
    [SerializeField]
    private Light flameLight;

    [SerializeField]
    private Renderer[] emissiveRenderers;

    [SerializeField]
    private bool startLit = true;

    [Header("Flicker")]
    [SerializeField]
    private float baseIntensity = 6f;

    [SerializeField]
    private float flickerAmount = 0.12f;

    [SerializeField]
    private float flickerSpeed = 9f;

    [SerializeField]
    private float raisedIntensityMultiplier = 1.35f;

    [SerializeField]
    private float baseRange = 13f;

    [Header("Audio")]
    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip toggleClip;

    [SerializeField]
    private AudioSource burnLoop;

    private bool isLit;
    private float noiseSeed;
    private float intensityScale = 1f;
    private MaterialPropertyBlock propertyBlock;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public override string StatusText => isLit ? "Acesa  [F] apagar" : "Apagada  [F] acender";

    private void Awake()
    {
        noiseSeed = Random.value * 100f;
        propertyBlock = new MaterialPropertyBlock();
        SetLit(startLit, false);
    }

    public override void OnToggleAction()
    {
        SetLit(!isLit, true);
    }

    private void SetLit(bool lit, bool playSound)
    {
        isLit = lit;
        if (flameLight != null)
        {
            flameLight.enabled = lit;
        }

        if (burnLoop != null)
        {
            if (lit && !burnLoop.isPlaying)
            {
                burnLoop.Play();
            }
            else if (!lit)
            {
                burnLoop.Stop();
            }
        }

        if (playSound && audioSource != null && toggleClip != null)
        {
            audioSource.PlayOneShot(toggleClip);
        }

        ApplyEmission(lit ? 1f : 0f);
    }

    private void Update()
    {
        if (!isLit || flameLight == null)
        {
            return;
        }

        float targetScale = IsRaised ? raisedIntensityMultiplier : 1f;
        intensityScale = Mathf.Lerp(intensityScale, targetScale, 1f - Mathf.Exp(-6f * Time.deltaTime));

        float time = Time.time * flickerSpeed + noiseSeed;
        float flicker = (Mathf.PerlinNoise(time, 0.37f) - 0.5f) * 2f;
        float gust = Mathf.PerlinNoise(time * 0.13f, 4.1f);
        float value = 1f + flicker * flickerAmount * (0.6f + gust * 0.8f);

        flameLight.intensity = baseIntensity * intensityScale * value;
        flameLight.range = baseRange * (0.95f + 0.05f * value);
        ApplyEmission(value);
    }

    private void ApplyEmission(float strength)
    {
        if (emissiveRenderers == null)
        {
            return;
        }

        Color emission = new Color(1f, 0.55f, 0.18f) * (2.5f * strength);
        foreach (Renderer emissive in emissiveRenderers)
        {
            if (emissive == null)
            {
                continue;
            }

            emissive.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, emission);
            emissive.SetPropertyBlock(propertyBlock);
        }
    }
}
