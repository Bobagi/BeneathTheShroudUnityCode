using System.Collections;
using UnityEngine;

// The storm outside: steady rain on the roof and lightning every so often. A strike is two or three
// hard flickers of a cold directional light (and the lights behind the windows), then thunder
// arrives 0.4 to 2.5 s later, as if the bolt landed somewhere across the water. The scripted strikes
// (Strike(true): the blackout, the box) land right over the house: the thunder comes at once and loud.
// Thunder is recorded (SoundLibrary.Thunder), synthesised only if the library has none.
public sealed class LightningStorm : MonoBehaviour
{
    [SerializeField]
    private Light[] flashLights;

    // 2.2 (13/11, measured on the courtyard): at 3.5 the strike bleached the facade and the dead tree to white; with
    // the sky lit in the same frame (skyFlashExposure) the clouds carry the strike and the walls keep their texture.
    [SerializeField]
    private float flashIntensity = 2.2f;

    [Tooltip("Quanto a exposição do skybox sobe no clarão (somado à exposição de base vezes o clarão): as nuvens acendem com o raio.")]
    [SerializeField]
    private float skyFlashExposure = 1.3f;

    [SerializeField]
    private Vector2 intervalSeconds = new Vector2(9f, 22f);

    [SerializeField]
    private AudioSource rainSource;

    [SerializeField]
    private AudioSource thunderSource;

    private float[] baseIntensities;
    private float skyBaseExposure = -1f;
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

    // Every change of the flash (0 = dark, 1 = full strike), for what else lights up with it (the menu's bolt and
    // title). close = the bolt lands right over the house.
    public static event System.Action<float> Flashed;
    public static event System.Action<bool> Struck;

    private void Start()
    {
        baseIntensities = new float[flashLights.Length];
        for (int i = 0; i < flashLights.Length; i++)
        {
            baseIntensities[i] = flashLights[i].intensity;
        }

        if (rainSource != null)
        {
            rainSource.clip = ProceduralAudio.RainLoop();
            rainSource.loop = true;
            rainSource.Play();
        }

        StartCoroutine(Run());
    }

    public void Strike(bool close = false)
    {
        StartCoroutine(StrikeRoutine(close));
    }

    private IEnumerator Run()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(intervalSeconds.x, intervalSeconds.y));
            yield return StrikeRoutine(false);
        }
    }

    private IEnumerator StrikeRoutine(bool close)
    {
        Struck?.Invoke(close);
        int flickers = Random.Range(2, 4);
        for (int f = 0; f < flickers; f++)
        {
            SetFlash(Random.Range(0.6f, 1f));
            yield return new WaitForSeconds(Random.Range(0.04f, 0.1f));
            SetFlash(0f);
            yield return new WaitForSeconds(Random.Range(0.05f, 0.18f));
        }

        float delay = close ? 0.05f : Random.Range(0.4f, 2.5f);
        yield return new WaitForSeconds(delay);
        if (thunderSource != null)
        {
            thunderSource.pitch = close ? 1f : Random.Range(0.85f, 1.05f);
            thunderSource.PlayOneShot(SoundLibrary.Instance.Thunder(close), close ? 1f : Mathf.Lerp(0.9f, 0.5f, delay / 2.5f));
        }
    }

    private static readonly int FlashId = Shader.PropertyToID("_LightningFlash");

    private void SetFlash(float amount)
    {
        // The storm sea shader lifts its whole surface with the strike.
        Shader.SetGlobalFloat(FlashId, amount);
        Flashed?.Invoke(amount);
        for (int i = 0; i < flashLights.Length; i++)
        {
            flashLights[i].intensity = baseIntensities[i] + flashIntensity * amount;
        }

        // The storm sky lights up with the bolt (clouds lit from inside); back to the dark base when it is over.
        Material sky = RenderSettings.skybox;
        if (sky != null && sky.HasProperty(ExposureId))
        {
            if (skyBaseExposure < 0f)
            {
                skyBaseExposure = sky.GetFloat(ExposureId);
            }

            sky.SetFloat(ExposureId, skyBaseExposure + skyFlashExposure * amount);
        }
    }

    private void OnDestroy()
    {
        // The skybox is a shared asset: leave it at its base exposure (the editor keeps material edits made in Play).
        Material sky = RenderSettings.skybox;
        if (sky != null && skyBaseExposure >= 0f && sky.HasProperty(ExposureId))
        {
            sky.SetFloat(ExposureId, skyBaseExposure);
        }
    }
}
