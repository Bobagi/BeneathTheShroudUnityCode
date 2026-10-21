using System.Collections;
using UnityEngine;

// Going in and out of the water, felt and heard. In: a splash scaled by how fast the body hits the surface
// (a slow wade down the steps is a low slosh, a drop is a big splash with a burst of bubbles), droplets
// thrown up at the surface. Out: water runs off the body, drips thinning out over several seconds, and
// the next footsteps are wet. Stepping into shallow water from the dry floor sloshes. Level in
// GameTuning.waterFxVolume.
public sealed class WaterTransitions : MonoBehaviour
{
    [SerializeField]
    private FirstPersonController controller;

    [SerializeField]
    private AudioSource source;

    [Tooltip("World-space burst at the surface where the body goes in.")]
    [SerializeField]
    private ParticleSystem splash;

    [Tooltip("Drips falling off the body after climbing out (a child of the player, emission driven from here).")]
    [SerializeField]
    private ParticleSystem runoff;

    [SerializeField]
    private FootstepAudio footsteps;

    private const float RunoffSeconds = 7f;

    private Vector3 lastPosition;
    private Vector3 velocity;
    private bool wasSwimming;
    private bool wasWading;
    private Coroutine running;

    private void Start()
    {
        lastPosition = controller.transform.position;
        wasSwimming = Swimming;
    }

    private bool Swimming => controller.CurrentMode == FirstPersonController.Mode.Diving || controller.CurrentMode == FirstPersonController.Mode.SurfaceSwimming;

    private void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 position = controller.transform.position;
        velocity = Vector3.Lerp(velocity, (position - lastPosition) / dt, 1f - Mathf.Exp(-12f * dt));
        lastPosition = position;
        if (controller.IsClimbingOut)
        {
            return;
        }

        bool swimming = Swimming;
        bool wading = controller.CurrentMode == FirstPersonController.Mode.Wading;
        if (swimming && !wasSwimming)
        {
            float hit = Mathf.Abs(velocity.y) + new Vector3(velocity.x, 0f, velocity.z).magnitude * 0.3f;
            GoIn(Mathf.InverseLerp(0.4f, 4f, hit));
        }
        else if (!swimming && wasSwimming)
        {
            ComeOut();
        }
        else if (wading && !wasWading && !wasSwimming)
        {
            GoIn(0.1f);
        }

        wasSwimming = swimming;
        wasWading = wading;
    }

    private void GoIn(float strength)
    {
        float level = GameTuning.Instance.waterFxVolume;
        Vector3 at = controller.transform.position;
        WaterVolume water = WaterVolume.Find(controller.EyePosition) ?? WaterVolume.Find(at + Vector3.up * 0.5f);
        if (water != null)
        {
            at.y = water.SurfaceHeight;
        }

        source.pitch = Random.Range(0.94f, 1.06f);
        source.PlayOneShot(SoundLibrary.Instance.Splash(strength), Mathf.Lerp(0.35f, 1f, strength) * level);
        if (strength > 0.45f)
        {
            source.PlayOneShot(ProceduralAudio.Bubbles(), strength * 0.6f * level);
        }

        if (splash != null)
        {
            splash.transform.position = at;
            ParticleSystem.MainModule main = splash.main;
            main.startSpeedMultiplier = Mathf.Lerp(0.6f, 3f, strength);
            splash.Emit(Mathf.RoundToInt(Mathf.Lerp(6f, 70f, strength)));
        }
    }

    private void ComeOut()
    {
        if (running != null)
        {
            StopCoroutine(running);
        }

        running = StartCoroutine(Runoff());
        if (footsteps != null)
        {
            footsteps.SetWet(10f);
        }
    }

    // Water pours off, then drips less and less.
    private IEnumerator Runoff()
    {
        float level = GameTuning.Instance.waterFxVolume;
        source.pitch = 1f;
        source.PlayOneShot(SoundLibrary.Instance.Splash(0.1f), 0.35f * level);
        ParticleSystem.EmissionModule emission = runoff != null ? runoff.emission : default;
        if (runoff != null)
        {
            emission.enabled = true;
        }

        float nextDrip = 0.1f;
        for (float t = 0f; t < RunoffSeconds; t += Time.deltaTime)
        {
            float left = 1f - t / RunoffSeconds;
            if (runoff != null)
            {
                emission.rateOverTime = 90f * left * left;
            }

            nextDrip -= Time.deltaTime;
            if (nextDrip <= 0f)
            {
                nextDrip = Mathf.Lerp(1.4f, 0.12f, left) * Random.Range(0.6f, 1.4f);
                source.pitch = Random.Range(0.85f, 1.25f);
                source.PlayOneShot(SoundLibrary.Instance.Drip(), Random.Range(0.15f, 0.35f) * level * (0.4f + left * 0.6f));
            }

            yield return null;
        }

        if (runoff != null)
        {
            emission.enabled = false;
        }

        running = null;
    }
}
