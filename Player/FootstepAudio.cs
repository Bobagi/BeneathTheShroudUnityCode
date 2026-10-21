using System.Collections.Generic;
using UnityEngine;

// Footsteps from the feet themselves: a step sounds when a foot bone comes down to the floor, so it is in
// sync with any animation (walk, run, crouch, the little steps of turning in place) instead of a timer.
// The floor under the foot picks the sound from its material name (wood, concrete/tile, carpet); in shallow
// water it splashes, and for a while after leaving the water the steps are wet. Louder running, softer
// crouched. Level in GameTuning.footstepVolume. Sits on the visible body (it reads its humanoid feet).
[RequireComponent(typeof(Animator))]
public sealed class FootstepAudio : MonoBehaviour
{
    [SerializeField]
    private FirstPersonController controller;

    [SerializeField]
    private AudioSource source;

    [SerializeField]
    private AudioClip[] wood = new AudioClip[0];

    [SerializeField]
    private AudioClip[] concrete = new AudioClip[0];

    [SerializeField]
    private AudioClip[] carpet = new AudioClip[0];

    // Foot bone height above the floor: a foot at rest sits ~8 cm up (the ankle); lifted, well above that.
    private const float LiftedAbove = 0.13f;
    private const float DownBelow = 0.1f;

    private enum Surface { Wood, Concrete, Carpet }

    private readonly Dictionary<Collider, Surface> surfaces = new Dictionary<Collider, Surface>();
    private readonly RaycastHit[] hits = new RaycastHit[6];
    private Transform leftFoot;
    private Transform rightFoot;
    private bool leftUp;
    private bool rightUp;
    private float wetUntil;
    private int lastClip = -1;
    private int lastCreak = -1;
    private int lastWater = -1;
    private Vector3 lastPosition;
    private float speed;

    // Steps squelch for this long (after climbing out of the water).
    public void SetWet(float seconds)
    {
        wetUntil = Time.time + seconds;
    }

    private void Awake()
    {
        Animator animator = GetComponent<Animator>();
        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        lastPosition = controller.transform.position;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 position = controller.transform.position;
        Vector3 moved = position - lastPosition;
        lastPosition = position;
        speed = Mathf.Lerp(speed, new Vector3(moved.x, 0f, moved.z).magnitude / dt, 1f - Mathf.Exp(-10f * dt));

        FirstPersonController.Mode mode = controller.CurrentMode;
        if (controller.IsSeated || controller.IsStandingUp || mode == FirstPersonController.Mode.Diving || mode == FirstPersonController.Mode.SurfaceSwimming)
        {
            leftUp = rightUp = false;
            return;
        }

        Track(leftFoot, ref leftUp, mode);
        Track(rightFoot, ref rightUp, mode);
    }

    private void Track(Transform foot, ref bool up, FirstPersonController.Mode mode)
    {
        if (!Floor(foot.position, out RaycastHit floor))
        {
            return;
        }

        float height = foot.position.y - floor.point.y;
        if (height > LiftedAbove)
        {
            up = true;
        }
        else if (up && height < DownBelow)
        {
            up = false;
            Step(floor.collider, mode);
        }
    }

    private bool Floor(Vector3 from, out RaycastHit floor)
    {
        floor = default;
        int count = Physics.RaycastNonAlloc(from + Vector3.up * 0.3f, Vector3.down, hits, 0.9f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].distance < best && !hits[i].collider.transform.IsChildOf(controller.transform))
            {
                best = hits[i].distance;
                floor = hits[i];
            }
        }

        return best < float.MaxValue;
    }

    private void Step(Collider ground, FirstPersonController.Mode mode)
    {
        GameTuning tuning = GameTuning.Instance;
        float volume = tuning.footstepVolume * Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(0.3f, tuning.runSpeed, speed)) * (controller.IsCrouched ? 0.5f : 1f);
        source.pitch = Random.Range(0.92f, 1.08f);
        SoundLibrary library = SoundLibrary.Instance;
        if (mode == FirstPersonController.Mode.Wading)
        {
            AudioClip wade = SoundLibrary.Pick(library.waterSteps, ref lastWater);
            source.PlayOneShot(wade != null ? wade : ProceduralAudio.Splash(), volume * 0.9f);
            return;
        }

        Surface surface = SurfaceOf(ground);
        AudioClip[] set = Clips(surface);
        if (set.Length > 0)
        {
            int index = Random.Range(0, set.Length);
            if (index == lastClip && set.Length > 1)
            {
                index = (index + 1) % set.Length;
            }

            lastClip = index;
            source.PlayOneShot(set[index], volume);
        }

        // Old boards: now and then one gives under the weight.
        if (surface == Surface.Wood && Random.value < 0.22f)
        {
            AudioClip creak = SoundLibrary.Pick(library.woodCreaks, ref lastCreak);
            if (creak != null)
            {
                source.PlayOneShot(creak, volume * Random.Range(0.35f, 0.7f));
            }
        }

        if (Time.time < wetUntil)
        {
            AudioClip wet = SoundLibrary.Pick(library.waterSteps, ref lastWater);
            source.PlayOneShot(wet != null ? wet : ProceduralAudio.Splash(), volume * 0.3f * Mathf.Clamp01((wetUntil - Time.time) / 4f));
        }
    }

    private AudioClip[] Clips(Surface surface)
    {
        switch (surface)
        {
            case Surface.Concrete: return concrete.Length > 0 ? concrete : wood;
            case Surface.Carpet: return carpet.Length > 0 ? carpet : wood;
            default: return wood;
        }
    }

    // From the name of the floor's material, cached per collider (no allocation after the first step).
    private Surface SurfaceOf(Collider ground)
    {
        if (surfaces.TryGetValue(ground, out Surface known))
        {
            return known;
        }

        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer == null)
        {
            renderer = ground.GetComponentInParent<Renderer>();
        }

        string name = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name.ToLowerInvariant() : string.Empty;
        Surface surface = Surface.Wood;
        if (name.Contains("rug") || name.Contains("carpet"))
        {
            surface = Surface.Carpet;
        }
        else if (name.Contains("tile") || name.Contains("concrete") || name.Contains("stone") || name.Contains("metal") || name.Contains("plaster"))
        {
            surface = Surface.Concrete;
        }

        surfaces[ground] = surface;
        return surface;
    }
}
