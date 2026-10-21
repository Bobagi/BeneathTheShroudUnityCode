using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// The body's side of the screen and the sound (Gustavo, 07/10: "imersão é o foco principal"). Sits on the camera
// next to SuffocationEffects (which keeps the air) and reads four states, each with its own global Volume whose weight
// follows the state, so they layer on whatever the scene's look is and on each other (priority 44 to 47, under the
// suffocation's 50), and nothing is touched at rest:
//   Aiming (the revolver raised or the torch held up): the edges close in, the view narrows a few degrees, the world
//     goes a little dull as the breath is held; a breath in when aiming starts, out when it ends.
//   Hurt (FirstPersonController.Hurt, a creature's blow): a red flash at the edges, colour drained, the image split
//     (chromatic aberration) and punched (lens distortion), a short ring in the ears and the world muffled, a hard
//     breath, the heart jumping, blood thrown across a corner of the screen that runs off over a few seconds.
//   Out of breath (sprinting): breathing that grows with the effort and takes its time to settle after, a touch of
//     motion blur and the view opening a few degrees while running.
//   Afraid (creatures near, more when seen, all the way during the chase): the heartbeat racing, a low drone under
//     everything, colour and contrast sinking, film grain, the edges darkening; a gasp when one shows up close
//     (once in a while, not on every glance); breathing gets shaky.
// Numbers in GameTuning ("Imersão"). The field of view goes through FieldOfViewOffset (the DreadDirector of the house
// adds it to its own squeeze); the red edge through DiveHud.SetDamageEdgeExtra. No allocation per frame.
[RequireComponent(typeof(Camera))]
public sealed class Immersion : MonoBehaviour
{
    [SerializeField]
    private FirstPersonController controller;

    [SerializeField]
    private DiveHud hud;

    [SerializeField]
    private UnderwaterView view;

    [Tooltip("Blood thrown on the screen by a hard blow (photographed spatter with alpha).")]
    [SerializeField]
    private Texture2D[] bloodScreen = new Texture2D[0];

    // Added to the camera's field of view (negative narrows). Read by DreadDirector, which owns the lens in the house.
    public static float FieldOfViewOffset { get; private set; }

    public float Fear => fear;
    public float Exertion => exertion;
    public float Hurt => hurt;

    private static readonly Color BloodRed = new Color(0.45f, 0f, 0.02f);
    private static readonly RaycastHit[] Hits = new RaycastHit[8];

    private Camera cam;
    private CharacterController body;
    private Revolver revolver;
    private TorchHold torch;
    private float baseFov;
    private bool ownsLens;

    private Volume aimVolume;
    private Volume sprintVolume;
    private Volume fearVolume;
    private Volume hurtVolume;
    private LensDistortion hurtDistortion;
    private ChromaticAberration hurtAberration;
    private VolumeProfile[] profiles;

    private AudioSource breath;
    private AudioSource panting;
    private AudioSource heart;
    private AudioSource drone;
    private AudioSource ring;
    private AudioClip thump;

    private float aim;
    private float aimHeld;
    private bool aiming;
    private float hurt;
    private float hurtPulse;
    private float ringLeft;
    private float adrenaline;
    private float exertion;
    private float sprint;
    private float fear;
    private float beatTimer;
    private float nextShakyBreath;
    private float nextStinger;
    private bool sawClose;
    private int lastBreath = -1;

    private RawImage[] splats;
    private float[] splatAlpha;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        baseFov = cam.fieldOfView;
        if (controller == null)
        {
            controller = GetComponentInParent<FirstPersonController>();
        }

        aimVolume = MakeVolume("AimVolume", 44f, out VolumeProfile aimProfile);
        Vignette(aimProfile, 0.42f, Color.black, 0.6f);
        sprintVolume = MakeVolume("SprintVolume", 45f, out VolumeProfile sprintProfile);
        Vignette(sprintProfile, 0.3f, Color.black, 0.8f);
        MotionBlur blur = sprintProfile.Add<MotionBlur>();
        blur.intensity.Override(GameTuning.Instance.sprintMotionBlur);
        blur.quality.Override(MotionBlurQuality.Low);
        fearVolume = MakeVolume("FearVolume", 46f, out VolumeProfile fearProfile);
        Vignette(fearProfile, 0.5f, new Color(0.02f, 0.01f, 0.02f), 0.9f);
        ColorAdjustments fearColor = fearProfile.Add<ColorAdjustments>();
        fearColor.saturation.Override(-45f);
        fearColor.contrast.Override(18f);
        FilmGrain grain = fearProfile.Add<FilmGrain>();
        // Light: at 0.65 (Medium4) the grain swamped the picture (Gustavo, 07/10).
        grain.type.Override(FilmGrainLookup.Thin2);
        grain.intensity.Override(GameTuning.Instance.fearGrain);
        fearProfile.Add<ChromaticAberration>().intensity.Override(0.2f);
        hurtVolume = MakeVolume("HurtVolume", 47f, out VolumeProfile hurtProfile);
        Vignette(hurtProfile, 0.55f, BloodRed, 0.85f);
        ColorAdjustments hurtColor = hurtProfile.Add<ColorAdjustments>();
        hurtColor.saturation.Override(-55f);
        hurtAberration = hurtProfile.Add<ChromaticAberration>();
        hurtAberration.intensity.Override(1f);
        hurtDistortion = hurtProfile.Add<LensDistortion>();
        hurtDistortion.intensity.Override(0f);
        profiles = new[] { aimProfile, sprintProfile, fearProfile, hurtProfile };

        GameObject sounds = new GameObject("ImmersionAudio");
        sounds.transform.SetParent(transform, false);
        breath = Inner(sounds, null);
        panting = Inner(sounds, SoundLibrary.Instance.exhaustedBreathingLoop);
        heart = Inner(sounds, null);
        drone = Inner(sounds, ProceduralAudio.DreadDroneLoop());
        ring = Inner(sounds, ProceduralAudio.RingingLoop());
        thump = ProceduralAudio.Heartbeat();
        BuildBloodScreen();
    }

    private void Start()
    {
        if (hud == null)
        {
            hud = FindAnyObjectByType<DiveHud>();
        }

        if (view == null)
        {
            view = GetComponent<UnderwaterView>();
        }

        if (controller != null)
        {
            body = controller.GetComponent<CharacterController>();
            controller.Hurt += OnHurt;
            revolver = controller.CameraPivot.GetComponentInChildren<Revolver>(true);
            torch = controller.CameraPivot.GetComponentInChildren<TorchHold>(true);
        }

        // In the house the DreadDirector writes the lens every frame and adds FieldOfViewOffset itself.
        ownsLens = FindAnyObjectByType<DreadDirector>() == null;
    }

    private void OnDestroy()
    {
        if (controller != null)
        {
            controller.Hurt -= OnHurt;
        }

        FieldOfViewOffset = 0f;
        if (profiles != null)
        {
            foreach (VolumeProfile profile in profiles)
            {
                Destroy(profile);
            }
        }
    }

    private void OnHurt(float strength)
    {
        GameTuning tuning = GameTuning.Instance;
        float s = strength * tuning.hurtStrength;
        hurt = Mathf.Clamp01(hurt + 0.4f + 0.6f * s);
        hurtPulse = 1f;
        ringLeft = Mathf.Max(ringLeft, 0.5f + 0.7f * s);
        adrenaline = 1f;
        PlayBreath(0.55f + 0.3f * s, Random.Range(0.85f, 0.95f));
        if (s > 0.35f && tuning.hurtBloodScreen > 0f)
        {
            Splat(Mathf.Clamp01(0.5f + 0.5f * s) * tuning.hurtBloodScreen);
        }
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || controller == null)
        {
            return;
        }

        GameTuning tuning = GameTuning.Instance;
        UpdateAim(tuning, dt);
        UpdateExertion(tuning, dt);
        UpdateFear(tuning, dt);

        // Hurt and its after-effects fade on their own.
        hurt = Mathf.MoveTowards(hurt, 0f, dt / Mathf.Max(0.1f, tuning.hurtSeconds));
        hurtPulse = Mathf.MoveTowards(hurtPulse, 0f, dt / 0.25f);
        ringLeft = Mathf.Max(0f, ringLeft - dt);
        adrenaline = Mathf.MoveTowards(adrenaline, 0f, dt / 6f);

        aimVolume.weight = Mathf.Clamp01(aim * tuning.aimVignette / 0.2f);
        sprintVolume.weight = sprint;
        fearVolume.weight = Mathf.Clamp01(fear * tuning.fearStrength);
        hurtVolume.weight = Mathf.Clamp01(hurt);
        hurtDistortion.intensity.value = -0.35f * hurtPulse;
        hurtAberration.intensity.value = Mathf.Clamp01(0.4f + 0.6f * hurtPulse);

        FieldOfViewOffset = -tuning.aimZoom * Smooth(aim) + tuning.sprintFov * sprint;
        if (ownsLens)
        {
            cam.fieldOfView = baseFov + FieldOfViewOffset;
        }

        if (view != null)
        {
            view.Muffle = Mathf.Max(view.Muffle, Mathf.Max(tuning.aimMuffle * aim, Mathf.Clamp01(ringLeft) * 0.6f));
        }

        if (hud != null)
        {
            hud.SetDamageEdgeExtra(hurt * 0.55f + hurtPulse * 0.3f);
        }

        Heartbeat(tuning, dt);
        drone.volume = tuning.dreadDroneVolume * Mathf.SmoothStep(0f, 1f, fear) * tuning.fearStrength;
        ring.volume = Mathf.Clamp01(ringLeft) * 0.18f * tuning.hurtStrength;
        UpdateBloodScreen(dt);
    }

    // The revolver raised or the torch held up: the breath held while it lasts.
    private void UpdateAim(GameTuning tuning, float dt)
    {
        float target = 0f;
        if (revolver != null && revolver.isActiveAndEnabled)
        {
            target = Mathf.Max(target, revolver.Aim);
        }

        if (torch != null && torch.isActiveAndEnabled)
        {
            target = Mathf.Max(target, torch.Focus);
        }

        aim = Mathf.MoveTowards(aim, target, dt / 0.2f);
        bool now = target > 0.5f;
        if (now && !aiming)
        {
            PlayBreath(tuning.aimBreathVolume, Random.Range(1.08f, 1.18f));
            aimHeld = 0f;
        }
        else if (!now && aiming && aimHeld > 0.8f)
        {
            PlayBreath(tuning.aimBreathVolume * 1.2f, Random.Range(0.82f, 0.9f));
        }

        aiming = now;
        aimHeld += now ? dt : 0f;
    }

    private void UpdateExertion(GameTuning tuning, float dt)
    {
        float speed = body != null ? new Vector2(body.velocity.x, body.velocity.z).magnitude : 0f;
        bool running = controller.IsSprinting && speed > 2.5f;
        exertion = running
            ? Mathf.MoveTowards(exertion, 1f, dt / Mathf.Max(0.5f, tuning.exertionSeconds))
            : Mathf.MoveTowards(exertion, 0f, dt / Mathf.Max(0.5f, tuning.exertionRecoverSeconds));
        sprint = Mathf.MoveTowards(sprint, running ? 1f : 0f, dt / 0.35f);
        float pant = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 1f, Mathf.Max(exertion, adrenaline * 0.5f)));
        if (panting.clip != null)
        {
            panting.volume = pant * tuning.exertionBreathVolume;
            panting.pitch = Mathf.Lerp(0.95f, 1.08f, exertion);
        }
    }

    // How close and how visible the nearest living creature is; the chase is fear at its height.
    private void UpdateFear(GameTuning tuning, float dt)
    {
        float target = 0f;
        bool seenClose = false;
        Vector3 eye = transform.position;
        for (int i = 0; i < TorchStun.Count; i++)
        {
            ITorchStunnable creature = TorchStun.Get(i);
            if (creature == null || !creature.Threatening || creature.Root == null)
            {
                continue;
            }

            Transform at = creature.Eyes != null ? creature.Eyes : creature.Root;
            Vector3 to = at.position - eye;
            float distance = to.magnitude;
            float near = Mathf.InverseLerp(tuning.fearRange, 2f, distance);
            if (near <= 0f)
            {
                continue;
            }

            bool seen = Vector3.Angle(transform.forward, to) < 38f && Visible(eye, to, distance, creature.Root);
            target = Mathf.Max(target, near * (seen ? 1f : 0.55f));
            seenClose |= seen && distance < 6f;
            if (creature is CreatureChase)
            {
                target = 1f;
            }
        }

        // A jolt of fear the moment one shows up close, then not again for a while.
        if (seenClose && !sawClose && Time.time > nextStinger)
        {
            nextStinger = Time.time + 12f;
            adrenaline = 1f;
            hurtPulse = Mathf.Max(hurtPulse, 0.35f);
            breath.pitch = 1.2f;
            breath.PlayOneShot(ProceduralAudio.Gasp(), 0.5f);
        }

        sawClose = seenClose;
        target = Mathf.Max(target, adrenaline * 0.5f);
        fear = Mathf.MoveTowards(fear, target, dt * (target > fear ? 1.2f : 0.15f));

        // Shaky breaths when afraid and not already panting.
        if (fear > 0.45f && exertion < 0.3f && Time.time > nextShakyBreath)
        {
            nextShakyBreath = Time.time + Random.Range(2.4f, 4f);
            PlayBreath(0.22f * fear, Random.Range(1.05f, 1.2f));
        }
    }

    private bool Visible(Vector3 eye, Vector3 to, float distance, Transform creature)
    {
        int count = Physics.RaycastNonAlloc(eye, to / distance, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = Hits[i].transform;
            if (!hit.IsChildOf(creature) && !hit.IsChildOf(controller.transform))
            {
                return false;
            }
        }

        return true;
    }

    // 70 beats a minute at rest is not heard; fear, a blow and the effort make it race and come up in the mix.
    private void Heartbeat(GameTuning tuning, float dt)
    {
        float drive = Mathf.Max(fear * tuning.fearStrength, Mathf.Max(adrenaline * 0.8f, exertion * 0.5f));
        if (drive < 0.15f)
        {
            return;
        }

        beatTimer -= dt;
        if (beatTimer > 0f)
        {
            return;
        }

        beatTimer = 60f / Mathf.Lerp(80f, 150f, drive);
        heart.PlayOneShot(thump, tuning.heartbeatVolume * Mathf.InverseLerp(0.15f, 1f, drive));
    }

    private void PlayBreath(float volume, float pitch)
    {
        AudioClip clip = SoundLibrary.Pick(SoundLibrary.Instance.heavyBreaths, ref lastBreath);
        if (clip == null || volume <= 0f)
        {
            return;
        }

        breath.pitch = pitch;
        breath.PlayOneShot(clip, volume);
    }

    // ---------------------------------------------------------------- blood on the screen

    private void BuildBloodScreen()
    {
        Canvas canvas = new GameObject("BloodScreen").AddComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2;
        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        Vector2[] corners = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        splats = new RawImage[corners.Length];
        splatAlpha = new float[corners.Length];
        for (int i = 0; i < corners.Length; i++)
        {
            RawImage image = new GameObject("Splat" + i).AddComponent<RawImage>();
            image.transform.SetParent(canvas.transform, false);
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = corners[i];
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1000f, 1000f);
            image.color = new Color(1f, 1f, 1f, 0f);
            image.enabled = false;
            splats[i] = image;
        }
    }

    private void Splat(float alpha)
    {
        if (bloodScreen.Length == 0)
        {
            return;
        }

        int count = alpha > 0.7f ? 2 : 1;
        for (int n = 0; n < count; n++)
        {
            int i = Random.Range(0, splats.Length);
            RawImage image = splats[i];
            image.texture = bloodScreen[Random.Range(0, bloodScreen.Length)];
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float size = Random.Range(800f, 1200f);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            // A little in from the corner, so more of it lands on the screen.
            Vector2 corner = image.rectTransform.anchorMin;
            image.rectTransform.anchoredPosition = new Vector2(
                (corner.x < 0.5f ? 1f : -1f) * Random.Range(120f, 260f), (corner.y < 0.5f ? 1f : -1f) * Random.Range(80f, 200f));
            splatAlpha[i] = Mathf.Max(splatAlpha[i], alpha);
            image.enabled = true;
        }
    }

    private void UpdateBloodScreen(float dt)
    {
        for (int i = 0; i < splats.Length; i++)
        {
            if (splatAlpha[i] <= 0f)
            {
                continue;
            }

            // Holds a moment, then runs off.
            splatAlpha[i] = Mathf.MoveTowards(splatAlpha[i], 0f, dt / 3.5f);
            RawImage image = splats[i];
            image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(splatAlpha[i] * 1.6f));
            image.rectTransform.anchoredPosition += Vector2.down * dt * 18f;
            if (splatAlpha[i] <= 0f)
            {
                image.enabled = false;
                image.rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }

    // ---------------------------------------------------------------- helpers

    private Volume MakeVolume(string name, float priority, out VolumeProfile profile)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = priority;
        volume.weight = 0f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;
        return volume;
    }

    private static void Vignette(VolumeProfile profile, float intensity, Color color, float smoothness)
    {
        Vignette vignette = profile.Add<Vignette>();
        // All four states' edges scaled together (Gustavo, 07/10: "vignette muito forte").
        vignette.intensity.Override(intensity * GameTuning.Instance.immersionVignette);
        vignette.color.Override(color);
        vignette.smoothness.Override(smoothness);
    }

    // Inner sounds: they skip the listener's low-pass, so they stay clear while the world goes dull.
    private static AudioSource Inner(GameObject host, AudioClip loop)
    {
        AudioSource source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.bypassListenerEffects = true;
        if (loop != null)
        {
            source.clip = loop;
            source.loop = true;
            source.volume = 0f;
            source.Play();
        }

        return source;
    }

    private static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
