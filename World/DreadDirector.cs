using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Drives a single 0..1 "dread" value and feeds it to the senses: staring at the colossus raises it
// slowly, the colossus answering a gunshot spikes it. Dread tightens the vignette with a pulse,
// adds chromatic fringing and camera tremor, narrows the field of view a touch and brings up a
// heartbeat that speeds up with it. Scenes without the colossus drive it through Provoke(); without
// the Cinemachine follow camera it squeezes the plain camera's field of view instead.
[DefaultExecutionOrder(100)]
public sealed class DreadDirector : MonoBehaviour
{
    [SerializeField]
    private ColossusCreature colossus;

    [SerializeField]
    private Camera viewCamera;

    [SerializeField]
    private Volume volume;

    [SerializeField]
    private CinemachineCamera followCamera;

    [SerializeField]
    private AudioSource heartbeat;

    [Header("Tuning")]
    [SerializeField]
    private float lookConeDegrees = 16f;

    [SerializeField]
    private float riseSpeed = 0.25f;

    [SerializeField]
    private float fallSpeed = 0.08f;

    [SerializeField]
    private float maxFovSqueeze = 4f;

    private float dread;
    private Vignette vignette;
    private ChromaticAberration aberration;
    private float baseVignette;
    private float baseAberration;
    private CinemachineBasicMultiChannelPerlin noise;
    private float baseNoise;
    private PlayerHandsController hands;
    private float heartbeatTimer;
    private AudioClip thump;
    private float provoked;
    private float baseFieldOfView;

    public float Dread => dread;

    // A scripted scare: dread jumps toward this level and bleeds off over the next seconds.
    public void Provoke(float amount)
    {
        provoked = Mathf.Max(provoked, Mathf.Clamp01(amount));
    }

    private void Start()
    {
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }

        if (volume != null && volume.profile != null)
        {
            // Runtime copy so play-mode tweaks never leak into the saved profile asset.
            volume.profile = Instantiate(volume.sharedProfile);
            volume.profile.TryGet(out vignette);
            volume.profile.TryGet(out aberration);
            baseVignette = vignette != null ? vignette.intensity.value : 0f;
            baseAberration = aberration != null ? aberration.intensity.value : 0f;
        }

        if (followCamera != null)
        {
            noise = followCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
            baseNoise = noise != null ? noise.AmplitudeGain : 0f;
        }

        hands = FindAnyObjectByType<PlayerHandsController>();
        baseFieldOfView = viewCamera != null ? viewCamera.fieldOfView : 60f;
        thump = ProceduralAudio.Heartbeat();
    }

    private void Update()
    {
        float target = 0f;
        if (colossus != null && viewCamera != null)
        {
            Vector3 toCreature = colossus.transform.position + Vector3.up * 90f - viewCamera.transform.position;
            float angle = Vector3.Angle(viewCamera.transform.forward, toCreature);
            if (angle < lookConeDegrees)
            {
                target = Mathf.Lerp(0.55f, 0.3f, angle / lookConeDegrees);
            }

            if (colossus.IsAlerted)
            {
                target = 1f;
            }
        }

        // A provoked scare rises much faster than a stare, then fades like any other dread.
        target = Mathf.Max(target, provoked);
        float speed = target > dread ? (provoked > 0f ? riseSpeed * 6f : riseSpeed) : fallSpeed;
        dread = Mathf.MoveTowards(dread, target, speed * Time.deltaTime);
        provoked = Mathf.MoveTowards(provoked, 0f, 0.12f * Time.deltaTime);
        Apply();
    }

    private void Apply()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(3f, 8f, dread));
        if (vignette != null)
        {
            vignette.intensity.value = baseVignette + dread * (0.14f + pulse * 0.06f);
        }

        if (aberration != null)
        {
            aberration.intensity.value = baseAberration + dread * 0.35f;
        }

        if (noise != null)
        {
            noise.AmplitudeGain = Mathf.Max(noise.AmplitudeGain, baseNoise + dread * 0.6f);
        }

        if (hands != null)
        {
            // The hands controller owns the lens; it stacks this squeeze on top of the aim zoom.
            hands.ExtraFieldOfView = -maxFovSqueeze * dread;
        }
        else if (followCamera == null && viewCamera != null)
        {
            viewCamera.fieldOfView = baseFieldOfView - maxFovSqueeze * dread + Immersion.FieldOfViewOffset;
        }

        if (heartbeat != null && dread > 0.12f)
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer <= 0f)
            {
                heartbeatTimer = Mathf.Lerp(1.25f, 0.45f, dread);
                heartbeat.PlayOneShot(thump, Mathf.Lerp(0.15f, 0.8f, dread));
            }
        }
    }
}
