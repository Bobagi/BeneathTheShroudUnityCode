using UnityEngine;
using UnityEngine.Rendering;

// The gas mask seen from inside while it is on (Respirator.HasMask), as in the Metro games: rubber rim, thick glass
// bending and blurring its edges, dirt and scratches catching the light, condensation with each breath out (faster
// and heavier running), water drops (coming out of the water, or in the rain with the sky open) that dry away, and
// cracks from an impact (the old attic mask already has a small one; Crack() adds more). Drawn by the MaskVisor full
// screen pass (Art/Shaders/MaskVisor.shader); this sets its globals only while the player's own camera renders, so
// other cameras (the test scene's outside view, the Scene view) never show it. Feel values in GameTuning ("Máscara
// vista de dentro").
[RequireComponent(typeof(Camera))]
public sealed class MaskVisor : MonoBehaviour
{
    private static readonly int VisorId = Shader.PropertyToID("_MaskVisor");
    private static readonly int FogId = Shader.PropertyToID("_MaskFog");
    private static readonly int DirtId = Shader.PropertyToID("_MaskDirt");
    private static readonly int WetId = Shader.PropertyToID("_MaskWet");
    private static readonly int RainId = Shader.PropertyToID("_MaskRain");
    private static readonly int CrackId = Shader.PropertyToID("_MaskCrack");
    private static readonly int CrackPointId = Shader.PropertyToID("_MaskCrackPoint");
    private static readonly int SheenId = Shader.PropertyToID("_MaskSheen");
    private static readonly int GlareId = Shader.PropertyToID("_MaskGlare");

    [SerializeField]
    private Respirator respirator;

    [SerializeField]
    private FirstPersonController controller;

    private Camera view;
    private UnderwaterView underwater;
    private float shown;
    private float phase;
    private float fog;
    private float wet;
    private float rain;
    private float crack = -1f;
    private float crackShown;
    private AudioSource crackSource;
    private int lastCrackSound = -1;
    // Lens space (x across, y up, about -1..1): low on the left, where a knock against a door frame would land.
    private Vector2 crackPoint = new Vector2(-0.52f, -0.38f);
    private bool wasUnder;

    public float CrackLevel => Mathf.Max(0f, crack);

    private void Awake()
    {
        view = GetComponent<Camera>();
        underwater = GetComponent<UnderwaterView>();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        Shader.SetGlobalFloat(VisorId, 0f);
    }

    // A blow to the mask: the crack grows (0..1). The first one picks where it starts.
    public void Crack(float amount)
    {
        if (crack <= 0f)
        {
            crackPoint = new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(-0.45f, 0.35f));
        }

        crack = Mathf.Clamp01(Mathf.Max(0f, crack) + amount);
        // Glass breaks at once: the new cracks are there the same frame (growing them over a few frames read as a
        // slow animation, Gustavo 2026-10-03), with the snap of the glass right at the ear.
        crackShown = crack;
        AudioClip snap = SoundLibrary.Pick(SoundLibrary.Instance.visorCracks, ref lastCrackSound);
        if (snap != null)
        {
            if (crackSource == null)
            {
                crackSource = gameObject.AddComponent<AudioSource>();
                crackSource.playOnAwake = false;
                crackSource.spatialBlend = 0f;
            }

            crackSource.pitch = Random.Range(0.92f, 1.08f);
            crackSource.PlayOneShot(snap, 0.9f);
        }
    }

    // A new mask (or a repaired one): back to the old attic mask's small crack.
    public void ResetCrack()
    {
        crack = GameTuning.Instance.maskCrackStart;
    }

    // Water thrown on the glass (a splash, a wave, a test key).
    public void Splash()
    {
        wet = 1f;
    }

    private void Update()
    {
        GameTuning tuning = GameTuning.Instance;
        float dt = Time.deltaTime;
        bool on = respirator != null && respirator.HasMask;
        shown = Mathf.MoveTowards(shown, on ? 1f : 0f, dt / Mathf.Max(0.05f, tuning.maskFadeSeconds));
        if (crack < 0f)
        {
            crack = tuning.maskCrackStart;
        }

        crackShown = Mathf.MoveTowards(crackShown, crack, dt * 4f);

        // Breathing: out fast (fog builds), in slowly (it clears). Running is short of breath and fogs more.
        bool running = controller != null && controller.IsSprinting;
        float seconds = running ? tuning.maskBreathSecondsRunning : tuning.maskBreathSeconds;
        phase = Mathf.Repeat(phase + dt / Mathf.Max(0.5f, seconds), 1f);
        float exhale = phase < 0.35f ? Mathf.SmoothStep(0f, 1f, phase / 0.35f) : Mathf.Exp(-(phase - 0.35f) * 6f);
        float strength = tuning.maskFogBreath * (running ? 1.5f : 1f);
        if (on && respirator.Filter < 0.2f)
        {
            // A clogged filter: every breath is a struggle.
            strength *= 1.4f;
        }

        fog = Mathf.Lerp(fog, tuning.maskFogBase + strength * exhale, 1f - Mathf.Exp(-8f * dt));

        // Water: under it the glass is all wet; coming up it streams with drops; rain under an open sky keeps
        // adding them; otherwise they dry away.
        bool under = underwater != null && underwater.IsUnder;
        bool raining = AmbienceSource.RainSources > 0 && !AmbienceSource.ListenerCovered();
        rain = Mathf.MoveTowards(rain, raining ? 1f : 0f, dt * 0.5f);
        if (under || (wasUnder && !under))
        {
            wet = 1f;
        }
        else if (raining)
        {
            wet = Mathf.Min(1f, wet + dt / Mathf.Max(0.5f, tuning.maskRainWetSeconds));
        }
        else
        {
            wet = Mathf.Max(0f, wet - dt / Mathf.Max(1f, tuning.maskDrySeconds));
        }

        wasUnder = under;
    }

    private void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        bool mine = camera == view;
        GameTuning tuning = GameTuning.Instance;
        Shader.SetGlobalFloat(VisorId, mine ? shown : 0f);
        if (!mine)
        {
            return;
        }

        bool under = underwater != null && underwater.IsUnder;
        Shader.SetGlobalFloat(FogId, under ? 0f : fog);
        Shader.SetGlobalFloat(DirtId, tuning.maskDirt);
        // Drops do not read under water (the whole glass is wet).
        Shader.SetGlobalFloat(WetId, under ? 0f : wet);
        Shader.SetGlobalFloat(RainId, rain);
        Shader.SetGlobalFloat(CrackId, crackShown);
        Shader.SetGlobalVector(CrackPointId, crackPoint);
        Shader.SetGlobalFloat(GlareId, tuning.maskGlare);
        // The sheen is a reflection of the room on the glass: it slides against the turn of the head.
        Vector3 angles = transform.eulerAngles;
        float yaw = Mathf.DeltaAngle(0f, angles.y) / 90f;
        float pitch = Mathf.DeltaAngle(0f, angles.x) / 90f;
        Shader.SetGlobalVector(SheenId, new Vector4(Mathf.Repeat(yaw * tuning.maskSheenFollow + 1f, 2f) - 1f, -pitch * tuning.maskSheenFollow, 0f, 0f));
    }
}
