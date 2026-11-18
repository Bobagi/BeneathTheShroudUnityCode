using System.Collections;
using UnityEngine;

// The war that never stops over the horizon: shell impacts flash in the fog and their boom arrives
// later (speed of sound), close ones shake the camera, and now and then a flare drifts down over
// no man's land lighting it in a sickly glare.
public sealed class DistantArtillery : MonoBehaviour
{
    [SerializeField]
    private Transform listener;

    [Header("Shelling")]
    [SerializeField]
    private Vector2 secondsBetweenShells = new Vector2(2.5f, 9f);

    [SerializeField]
    private Vector2 distanceRange = new Vector2(160f, 900f);

    [SerializeField]
    [Tooltip("Direction (yaw degrees) of the front line and the half-width of the arc shells land in.")]
    private float frontYaw = 0f;

    [SerializeField]
    private float arcHalfWidth = 80f;

    [SerializeField]
    private AudioClip[] explosionClips;

    [SerializeField]
    private ParticleSystem explosionPrefab;

    [SerializeField]
    private float soundDelayScale = 0.6f;

    [SerializeField]
    private float flashIntensity = 40f;

    [Header("Flares")]
    [SerializeField]
    private Vector2 secondsBetweenFlares = new Vector2(20f, 45f);

    [SerializeField]
    private float flareDurationSeconds = 14f;

    [SerializeField]
    private AudioClip flareLaunchClip;

    [SerializeField]
    private float flareIntensity = 10f;

    private void Start()
    {
        if (listener == null && Camera.main != null)
        {
            listener = Camera.main.transform;
        }

        StartCoroutine(ShellingLoop());
        StartCoroutine(FlareLoop());
    }

    private IEnumerator ShellingLoop()
    {
        yield return new WaitForSeconds(1.5f);
        while (true)
        {
            // Shells come in salvos of 1-3.
            int salvo = Random.Range(1, 4);
            for (int i = 0; i < salvo; i++)
            {
                StartCoroutine(Shell(RandomPointOnFront(distanceRange)));
                yield return new WaitForSeconds(Random.Range(0.15f, 0.9f));
            }

            yield return new WaitForSeconds(Random.Range(secondsBetweenShells.x, secondsBetweenShells.y));
        }
    }

    private Vector3 RandomPointOnFront(Vector2 distances)
    {
        float yaw = frontYaw + Random.Range(-arcHalfWidth, arcHalfWidth);
        float distance = Random.Range(distances.x, distances.y);
        Vector3 center = listener != null ? listener.position : transform.position;
        Vector3 point = center + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * distance;
        point.y = SampleGround(point);
        return point;
    }

    private static float SampleGround(Vector3 point)
    {
        if (Physics.Raycast(point + Vector3.up * 500f, Vector3.down, out RaycastHit hit, 1000f,
                ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return 0f;
    }

    private IEnumerator Shell(Vector3 position)
    {
        float distance = listener != null ? Vector3.Distance(listener.position, position) : 300f;

        // Flash: a big short-lived point light plus the dirt/smoke burst.
        GameObject flash = new GameObject("ShellFlash");
        flash.transform.position = position + Vector3.up * 6f;
        Light flashLight = flash.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = new Color(1f, 0.62f, 0.3f);
        flashLight.range = 140f;
        flashLight.intensity = 0f;
        flashLight.shadows = LightShadows.None;

        if (explosionPrefab != null)
        {
            ParticleSystem burst = Instantiate(explosionPrefab, position, Quaternion.identity);
            burst.transform.localScale = Vector3.one * Mathf.Lerp(1f, 2.5f, distance / 900f);
            burst.Play(true);
            Destroy(burst.gameObject, 25f);
        }

        float elapsed = 0f;
        const float flashSeconds = 0.35f;
        while (elapsed < flashSeconds)
        {
            elapsed += Time.deltaTime;
            float k = elapsed / flashSeconds;
            flashLight.intensity = Mathf.Lerp(flashIntensity, 0f, k) * Random.Range(0.7f, 1f);
            yield return null;
        }

        Destroy(flash);

        // The boom arrives later; scaled down so the prototype stays punchy.
        yield return new WaitForSeconds(distance / 343f * soundDelayScale);
        PlayBoom(position, distance);

        if (distance < 350f)
        {
            CameraShake.Shake(Mathf.Lerp(0.8f, 0.1f, distance / 350f), 0.6f);
        }
    }

    private void PlayBoom(Vector3 position, float distance)
    {
        if (explosionClips == null || explosionClips.Length == 0 || listener == null)
        {
            return;
        }

        // Play near the listener in the direction of the impact: real rolloff over 900 m would be
        // inaudible, but the direction still reads.
        Vector3 direction = (position - listener.position).normalized;
        GameObject source = new GameObject("ShellBoom");
        source.transform.position = listener.position + direction * 40f;
        AudioSource audio = source.AddComponent<AudioSource>();
        audio.clip = explosionClips[Random.Range(0, explosionClips.Length)];
        audio.spatialBlend = 0.85f;
        audio.minDistance = 30f;
        audio.maxDistance = 200f;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.volume = Mathf.Lerp(1f, 0.35f, distance / 900f);
        audio.pitch = Mathf.Lerp(1f, 0.7f, distance / 900f) * Random.Range(0.9f, 1.05f);
        audio.dopplerLevel = 0f;
        audio.Play();
        Destroy(source, audio.clip.length / Mathf.Max(0.1f, audio.pitch) + 0.5f);
    }

    private IEnumerator FlareLoop()
    {
        yield return new WaitForSeconds(Random.Range(6f, 12f));
        while (true)
        {
            yield return Flare();
            yield return new WaitForSeconds(Random.Range(secondsBetweenFlares.x, secondsBetweenFlares.y));
        }
    }

    private IEnumerator Flare()
    {
        Vector3 start = RandomPointOnFront(new Vector2(60f, 220f));
        start.y += 70f;

        if (flareLaunchClip != null && listener != null)
        {
            AudioSource.PlayClipAtPoint(flareLaunchClip, Vector3.Lerp(listener.position, start, 0.1f), 0.4f);
        }

        GameObject flare = new GameObject("StarShell");
        flare.transform.position = start;
        Light flareLight = flare.AddComponent<Light>();
        flareLight.type = LightType.Point;
        flareLight.color = new Color(0.92f, 0.95f, 0.8f);
        flareLight.range = 220f;
        flareLight.shadows = LightShadows.Soft;
        flareLight.shadowStrength = 0.8f;

        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(glow.GetComponent<Collider>());
        glow.transform.SetParent(flare.transform, false);
        glow.transform.localScale = Vector3.one * 1.2f;
        Renderer glowRenderer = glow.GetComponent<Renderer>();
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit != null)
        {
            glowRenderer.material = new Material(unlit) { color = new Color(1f, 1f, 0.9f) };
        }

        glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        float elapsed = 0f;
        Vector3 drift = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
        while (elapsed < flareDurationSeconds)
        {
            elapsed += Time.deltaTime;
            float k = elapsed / flareDurationSeconds;
            // Parachute flare: slow sink with sway; burns out at the end.
            flare.transform.position += (Vector3.down * 3.2f + drift * 1.5f) * Time.deltaTime;
            float burn = k < 0.08f ? k / 0.08f : (k > 0.85f ? (1f - k) / 0.15f : 1f);
            flareLight.intensity = flareIntensity * burn * Random.Range(0.85f, 1f);
            yield return null;
        }

        Destroy(flare);
    }
}
