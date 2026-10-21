using UnityEngine;
using UnityEngine.InputSystem;

// First-person body for the flooded house. On land it walks (no jump: the house is about looking,
// not platforming). In shallow water it wades slower and splashes. In deep water it swims in 3D
// along the view: Space rises, Ctrl dives, Shift swims hard. At the surface it floats with the eyes
// just above the water and climbs out onto any ledge up to chest height in front of it. It can sit in a
// Seat: it turns, steps in front of it and sits; the body (PlayerBody) plays the sit and the camera rides
// its head, looking around within 100 degrees either side; W, Space or E stands back up.
[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonController : MonoBehaviour
{
    public enum Mode { Walking, Wading, SurfaceSwimming, Diving }

    [SerializeField]
    private Transform cameraPivot;

    // Speeds live in GameTuning (Horror > Ajustes do jogo), shared by every scene.
    [Header("Body")]
    [SerializeField]
    private float gravity = 18f;

    [SerializeField]
    private float lookSensitivity = 0.09f;

    [SerializeField]
    private float waterDrag = 3.5f;

    [Tooltip("How high the eyes float above the surface when treading water.")]
    [SerializeField]
    private float floatEyeHeight = 0.18f;

    [SerializeField]
    private float maxClimbHeight = 1.3f;

    [Tooltip("Capsule height crouched on land (Ctrl or C).")]
    [SerializeField]
    private float crouchHeight = 1.15f;

    [Tooltip("Capsule height while diving: the swimmer lies flat, so low gaps (the bedroom breach) fit.")]
    [SerializeField]
    private float swimHeight = 0.7f;

    [Header("Audio")]
    [SerializeField]
    private AudioSource bodyAudio;

    [Tooltip("The visible body; while sitting the camera rides its head.")]
    [SerializeField]
    private PlayerBody visualBody;

    private const float SeatedLookLimit = 100f;
    private const float SitSeconds = 2.2f;
    private const float StandSeconds = 2.25f;

    private CharacterController body;
    private Vector3 velocity;
    private float pitch;
    private float joltPitch;
    private float joltYaw;
    private float stepTimer;
    private bool climbing;
    private WaterVolume water;
    private float standHeight;
    private float standCenterY;
    private float standEyeY;
    private bool crouched;
    private bool onFloating;
    private readonly Collider[] overlap = new Collider[8];
    private float seatYaw;
    private float seatedSince;
    private bool goingToSeat;

    public Mode CurrentMode { get; private set; }
    public bool InputLocked { get; set; }

    // Scripted moments (the demo's finale): the feet stay, the view still answers the mouse (within the cone below),
    // the gun still fires. InputLocked locks everything, this only the walking.
    public bool MovementLocked { get; set; }

    // Crouched from code (hiding behind the counter): as if Ctrl were held.
    public bool HoldCrouch { get; set; }

    // The view kept inside a cone: half angles in degrees round a centre yaw (world) and pitch; 0 = free.
    public float LookConeYaw { get; set; }
    public float LookConePitch { get; set; }
    public float LookConeCentreYaw { get; set; }
    public float LookConeCentrePitch { get; set; }

    // A blow landed on the player (Jolt): 0 to 1.
    public event System.Action<float> Hurt;
    public bool IsSprinting { get; private set; }
    public bool IsCrouched => crouched;
    public Transform CameraPivot => cameraPivot;
    public bool IsSeated { get; private set; }

    // The seat being sat in (from the walk over until the stand-up ends); PlayerBody sets the hips on it.
    public Seat CurrentSeat { get; private set; }
    public bool IsClimbingOut => climbing;
    public float ClimbProgress { get; private set; }
    public Vector3 ClimbLedge { get; private set; }
    public Vector3 ClimbForward { get; private set; }
    public bool IsStandingUp { get; private set; }
    public bool CanStand => IsSeated && Time.time - seatedSince > SitSeconds;
    public Vector3 EyePosition => cameraPivot.position;

    private void Awake()
    {
        body = GetComponent<CharacterController>();
        standHeight = body.height;
        standCenterY = body.center.y;
        standEyeY = cameraPivot.localPosition.y;
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Teleport(Vector3 feetPosition, float yaw)
    {
        body.enabled = false;
        body.height = standHeight;
        body.center = new Vector3(0f, standCenterY, 0f);
        transform.SetPositionAndRotation(feetPosition, Quaternion.Euler(0f, yaw, 0f));
        body.enabled = true;
        velocity = Vector3.zero;
        pitch = 0f;
        cameraPivot.localRotation = Quaternion.identity;
        cameraPivot.localPosition = new Vector3(cameraPivot.localPosition.x, standEyeY, cameraPivot.localPosition.z);
        climbing = false;
    }

    private void Update()
    {
        // Walking over to the seat counts too: the capsule is already off, so nothing may Move it.
        if (IsSeated || IsStandingUp || goingToSeat)
        {
            UpdateSeated();
            return;
        }

        if (climbing)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        Vector2 move = Vector2.zero;
        Vector2 look = Vector2.zero;
        bool up = false, down = false, sprint = false;
        if (!InputLocked && keyboard != null)
        {
            if (!MovementLocked)
            {
                move.x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                move.y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
                up = keyboard.spaceKey.isPressed;
                down = keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed || TestHoldDown;
                sprint = keyboard.leftShiftKey.isPressed;
            }

            if (mouse != null)
            {
                look = mouse.delta.ReadValue();
            }
        }

        down |= HoldCrouch;

        if (TestSprint)
        {
            // Running forward from code (automated tests without window focus).
            move.y = 1f;
            sprint = true;
        }

        Look(look);
        move = Vector2.ClampMagnitude(move, 1f);
        CurrentMode = ResolveMode(down);
        onFloating = false;
        UpdateShape(down);
        // Shift: run on land (only forward-ish), swim hard in water. Wading and crouching never run.
        IsSprinting = sprint && move.y > 0.1f && !crouched;
        GameTuning tuning = GameTuning.Instance;

        switch (CurrentMode)
        {
            case Mode.Diving:
                Dive(move, up, down, sprint);
                break;
            case Mode.SurfaceSwimming:
                TreadWater(move, down, sprint);
                break;
            case Mode.Wading:
                Walk(move, crouched ? tuning.crouchSpeed : tuning.wadeSpeed);
                break;
            default:
                Walk(move, crouched ? tuning.crouchSpeed : IsSprinting ? tuning.runSpeed : tuning.walkSpeed);
                break;
        }
    }

    // Three body shapes: standing, crouched (Ctrl/C on land) and flat while diving. Growing back only
    // happens when the taller capsule fits, so the player never stands up into a ceiling.
    private void UpdateShape(bool crouchHeld)
    {
        float height = standHeight;
        float centerY = standCenterY;
        bool wantCrouch = crouchHeld && (CurrentMode == Mode.Walking || CurrentMode == Mode.Wading);
        if (CurrentMode == Mode.Diving)
        {
            // Centred just below the eyes: the head and chest, lying along the view.
            height = swimHeight;
            centerY = standEyeY - swimHeight * 0.3f;
        }
        else if (wantCrouch)
        {
            height = crouchHeight;
            centerY = crouchHeight * 0.5f;
        }

        // Any change of shape must fit where the body is, not only growing: the swimming shape sits up by the eyes,
        // and moved into a wall the controller shoved the player through the ceiling above the pocket door.
        bool changes = Mathf.Abs(height - body.height) > 0.01f || Mathf.Abs(centerY - body.center.y) > 0.01f;
        if (changes && !Fits(height, centerY))
        {
            // No room to grow: try crouching instead (coming out of a low gap), else stay as is.
            if (crouchHeight > body.height + 0.01f && Fits(crouchHeight, crouchHeight * 0.5f))
            {
                height = crouchHeight;
                centerY = crouchHeight * 0.5f;
            }
            else
            {
                height = body.height;
                centerY = body.center.y;
            }
        }

        body.height = height;
        body.center = new Vector3(0f, centerY, 0f);
        crouched = CurrentMode != Mode.Diving && height < standHeight - 0.01f;

        // With a visible body the camera rides its head on land (PlayerBody); this only places it otherwise.
        if (visualBody != null && visualBody.isActiveAndEnabled && (CurrentMode == Mode.Walking || CurrentMode == Mode.Wading))
        {
            return;
        }

        float eyeTarget = crouched ? standEyeY - (standHeight - height) : standEyeY;
        Vector3 eye = cameraPivot.localPosition;
        float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
        eye.y = Mathf.Lerp(eye.y, eyeTarget, blend);
        // Back over the feet after riding the head through a sit.
        eye.x = Mathf.Lerp(eye.x, 0f, blend);
        eye.z = Mathf.Lerp(eye.z, 0f, blend);
        cameraPivot.localPosition = eye;
    }

    private bool Fits(float height, float centerY)
    {
        // The test capsule starts a few cm above its lowest point, or the floor it stands on counts as a hit.
        float radius = body.radius * 0.95f;
        Vector3 feet = transform.position + Vector3.up * (centerY - height * 0.5f);
        Vector3 bottom = feet + Vector3.up * (radius + 0.06f);
        Vector3 top = feet + Vector3.up * Mathf.Max(radius + 0.06f, height - radius);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (overlap[i].transform != transform && !overlap[i].transform.IsChildOf(transform))
            {
                return false;
            }
        }

        return true;
    }

    // Standing on floating furniture does not count as ground: the player is in the water, not on a raft.
    private bool Grounded => body.isGrounded && !onFloating;

    // Where the body last rested on something (the contact point under it).
    private Vector3 support;

    // Floating furniture gets shoved aside by the body instead of acting like a wall or a floor.
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.3f)
        {
            support = hit.point;
        }

        FloatingBob floating = hit.collider.GetComponentInParent<FloatingBob>();
        if (floating == null)
        {
            return;
        }

        if (hit.normal.y > 0.5f)
        {
            onFloating = true;
        }

        Vector3 shove = Vector3.ProjectOnPlane(hit.moveDirection, Vector3.up);
        floating.Push(shove * Mathf.Max(0.6f, velocity.magnitude));
    }

    private void Look(Vector2 delta)
    {
        float sensitivity = lookSensitivity * GameSettings.MouseSensitivity;
        transform.Rotate(0f, delta.x * sensitivity, 0f);
        pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -85f, 85f);
        if (LookConeYaw > 0f)
        {
            // Peeking round the counter: the view stays inside the cone round its centre.
            float yaw = Mathf.DeltaAngle(LookConeCentreYaw, transform.eulerAngles.y);
            yaw = Mathf.Clamp(yaw, -LookConeYaw, LookConeYaw);
            transform.rotation = Quaternion.Euler(0f, LookConeCentreYaw + yaw, 0f);
            pitch = Mathf.Clamp(pitch, LookConeCentrePitch - LookConePitch, LookConeCentrePitch + LookConePitch);
        }

        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // The view's pitch right now (degrees, down positive), for scripted moments that set a cone round it.
    public float Pitch => pitch;

    // Clears the look cone and the scripted locks.
    public void FreeLook()
    {
        LookConeYaw = 0f;
        LookConePitch = 0f;
        MovementLocked = false;
        HoldCrouch = false;
    }

    // A kick of the view (weapon recoil): up by pitchUp degrees, sideways by yaw.
    public void Kick(float pitchUp, float yaw)
    {
        transform.Rotate(0f, yaw, 0f);
        pitch = Mathf.Clamp(pitch - pitchUp, -85f, 85f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // Held "down" (Ctrl) from code: automated tests reproduce diving without window focus.
    public bool TestHoldDown { get; set; }

    // Running forward (W + Shift) from code, for the same tests.
    public bool TestSprint { get; set; }

    // A blow taken (a creature's hit): the same push as Kick, but the view comes back to where it was over about
    // half a second, so repeated blows shake the aim instead of leaving it on the floor.
    public void Jolt(float pitchUp, float yaw)
    {
        // Taken as a hit by the screen and sound effects (Immersion), strength about 1 for a creature's blow.
        Hurt?.Invoke(Mathf.Clamp01(Mathf.Abs(pitchUp) / 6f + Mathf.Abs(yaw) / 20f));
        Kick(pitchUp, yaw);
        joltPitch += pitchUp;
        joltYaw += yaw;
    }

    // A gun's kick on the view: like Kick, but `back` of it (0 to 1) returns on its own the way a blow does (Jolt),
    // without counting as a blow.
    public void Recoil(float pitchUp, float yaw, float back)
    {
        Kick(pitchUp, yaw);
        joltPitch += pitchUp * back;
        joltYaw += yaw * back;
    }

    // Turns the view to look at a point (scripted moments and automated tests).
    public void LookAt(Vector3 point)
    {
        Vector3 to = point - cameraPivot.position;
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        if (flat.sqrMagnitude > 1e-6f)
        {
            transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
        }

        pitch = Mathf.Clamp(-Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg, -85f, 85f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private Mode ResolveMode(bool down)
    {
        Vector3 chest = transform.position + Vector3.up * 1.1f;
        water = WaterVolume.Find(chest) ?? WaterVolume.Find(transform.position + Vector3.up * 0.1f);
        if (water == null)
        {
            return Mode.Walking;
        }

        // A volume whose surface is still under the feet (the ground floor before the flood) is dry floor.
        if (transform.position.y > water.SurfaceHeight - 0.02f && Grounded)
        {
            return Mode.Walking;
        }

        if (!water.IsDeep)
        {
            return Mode.Wading;
        }

        // Standing on something with the chest above water is still wading (stairs into the pool), unless the player
        // holds down to sink: on a narrow perch (the top of the pocket door) crouching kept him hanging on the edge.
        if (Grounded && chest.y > water.SurfaceHeight && !down)
        {
            return Mode.Wading;
        }

        return EyePosition.y < water.SurfaceHeight - 0.05f ? Mode.Diving : Mode.SurfaceSwimming;
    }

    // A body has mass: it picks up speed over a few tenths of a second and brakes a little faster, so the
    // legs (PlayerBody) can plant and settle instead of the walk cycle stopping dead.
    private void Walk(Vector2 move, float speed)
    {
        GameTuning tuning = GameTuning.Instance;
        Vector3 wish = (transform.right * move.x + transform.forward * move.y) * speed;
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        bool speedingUp = wish.sqrMagnitude > flat.sqrMagnitude && Vector3.Dot(wish, flat) >= 0f;
        flat = Vector3.MoveTowards(flat, wish, (speedingUp ? tuning.walkAcceleration : tuning.walkDeceleration) * Time.deltaTime);
        velocity.x = flat.x;
        velocity.z = flat.z;
        velocity.y = body.isGrounded ? -2f : velocity.y - gravity * Time.deltaTime;
        body.Move(velocity * Time.deltaTime);
        // With a visible body its feet make the sounds (FootstepAudio), in step with the animation.
        if (visualBody == null)
        {
            Footsteps(move, speed);
        }
    }

    private void Dive(Vector2 move, bool up, bool down, bool sprint)
    {
        GameTuning tuning = GameTuning.Instance;
        float speed = sprint ? tuning.sprintSwimSpeed : tuning.swimSpeed;
        Vector3 wish = (cameraPivot.forward * move.y + cameraPivot.right * move.x) * speed;
        wish += Vector3.up * ((up ? 1f : 0f) - (down ? 1f : 0f)) * tuning.verticalSwimSpeed;
        if (down)
        {
            wish += SlideOffPerch();
        }

        velocity = Vector3.Lerp(velocity, wish, 1f - Mathf.Exp(-waterDrag * Time.deltaTime));
        body.Move(velocity * Time.deltaTime);
    }

    private void TreadWater(Vector2 move, bool down, bool sprint)
    {
        GameTuning tuning = GameTuning.Instance;
        float speed = (sprint ? tuning.sprintSwimSpeed : tuning.swimSpeed) * 0.8f;
        Vector3 flatForward = Vector3.ProjectOnPlane(cameraPivot.forward, Vector3.up).normalized;
        Vector3 wish = (flatForward * move.y + transform.right * move.x) * speed;

        // Hold the eyes a little above the surface with a slow bob; Ctrl dives under.
        float targetEye = water.SurfaceHeight + floatEyeHeight + Mathf.Sin(Time.time * 1.7f) * 0.04f;
        wish.y = down ? -tuning.verticalSwimSpeed : (targetEye - EyePosition.y) * 4f;
        if (down)
        {
            wish += SlideOffPerch();
        }

        velocity = Vector3.Lerp(velocity, wish, 1f - Mathf.Exp(-waterDrag * Time.deltaTime));
        body.Move(velocity * Time.deltaTime);

        if (move.y > 0.1f)
        {
            TryClimbOut(flatForward);
        }
    }

    // Sinking while resting on an edge (the top of a door frame): the capsule's round bottom sits on the corner and
    // a straight push down is blocked, so drift sideways off the support until the body falls past it.
    private Vector3 SlideOffPerch()
    {
        if (!body.isGrounded)
        {
            return Vector3.zero;
        }

        Vector3 away = transform.position - support;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0004f)
        {
            // Right on top of it: on a flat floor this does nothing, the body stays standing on it.
            return Vector3.zero;
        }

        return away.normalized * 1.2f;
    }

    // Probe straight down just ahead of the swimmer for a dry, flat top within reach of the surface.
    // Looking down (not forward for a wall) also finds thin platforms on posts, like the landing.
    private void TryClimbOut(Vector3 forward)
    {
        if (water == null)
        {
            return;
        }

        Vector3 ahead = transform.position + forward * (body.radius + 0.45f);
        float probeTop = water.SurfaceHeight + maxClimbHeight + 0.3f;
        // Never probe from above a ceiling: in the air pocket (1 m of air) the probe used to start on top of
        // the slab, "found" its upper face and lifted the player out of the level.
        if (Physics.Raycast(EyePosition, Vector3.up, out RaycastHit ceiling, probeTop - EyePosition.y + 0.1f, ~0, QueryTriggerInteraction.Ignore))
        {
            probeTop = Mathf.Min(probeTop, ceiling.point.y - 0.05f);
        }

        Vector3 start = new Vector3(ahead.x, probeTop, ahead.z);
        if (!Physics.Raycast(start, Vector3.down, out RaycastHit top, maxClimbHeight + 0.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        // Floating furniture is pushed around, never climbed onto; and the ledge must be in reach, not
        // behind a wall.
        if (top.collider.GetComponentInParent<FloatingBob>() != null
            || Physics.Linecast(EyePosition, top.point + Vector3.up * 0.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        float rise = top.point.y - water.SurfaceHeight;
        bool roomToStand = !Physics.CheckCapsule(top.point + Vector3.up * (body.radius + 0.05f),
            top.point + Vector3.up * (standHeight - body.radius), body.radius * 0.9f, ~0, QueryTriggerInteraction.Ignore);
        if (top.normal.y > 0.7f && rise > 0.05f && rise < maxClimbHeight && roomToStand)
        {
            StartCoroutine(ClimbTo(top.point + Vector3.up * 0.02f, forward));
        }
    }

    // Climbing out is a slow, heavy move: the hands reach the edge (PlayerBody puts the free hand on it),
    // the body hauls itself up until the chest is over the edge, then rolls over onto it. Water pours off.
    private System.Collections.IEnumerator ClimbTo(Vector3 target, Vector3 forward)
    {
        climbing = true;
        body.enabled = false;
        Vector3 start = transform.position;
        // The edge itself: where a level ray just under the top meets the ledge's face.
        ClimbForward = forward;
        ClimbLedge = target - forward * 0.3f;
        if (Physics.Raycast(new Vector3(start.x, target.y - 0.08f, start.z), forward, out RaycastHit face, 1.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            ClimbLedge = new Vector3(face.point.x, target.y, face.point.z);
        }

        if (visualBody != null && visualBody.isActiveAndEnabled)
        {
            yield return ClimbAnimated(target, forward);
            yield break;
        }

        // Without the visible body: first drift in until the chest is against the edge, then chest over the
        // edge (feet 0.95 m below its top); never lower than where the swimmer already is.
        Vector3 close = ClimbLedge - forward * 0.26f;
        close.y = start.y;
        if (Vector3.Dot(close - start, forward) < 0f)
        {
            close = start;
        }

        Vector3 hauled = new Vector3(close.x, Mathf.Max(start.y, target.y - 0.95f), close.z) + forward * 0.1f;
        float seconds = GameTuning.Instance.climbOutSeconds;
        PlayOneShot(SoundLibrary.Instance.Splash(0.1f), 0.5f);
        bool poured = false;
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            ClimbProgress = t;
            if (t < 0.25f)
            {
                // Reaching: the hands go to the edge while the body glides in against it, bobbing.
                transform.position = Vector3.Lerp(start, close, Mathf.SmoothStep(0f, 1f, t / 0.25f)) + Vector3.up * Mathf.Sin(t / 0.25f * Mathf.PI) * 0.04f;
            }
            else if (t < 0.7f)
            {
                float e = Mathf.SmoothStep(0f, 1f, (t - 0.25f) / 0.45f);
                transform.position = Vector3.Lerp(close, hauled, e);
                if (!poured && e > 0.5f)
                {
                    poured = true;
                    PlayOneShot(SoundLibrary.Instance.Splash(0.3f), 0.35f);
                }
            }
            else
            {
                float e = Mathf.SmoothStep(0f, 1f, (t - 0.7f) / 0.3f);
                transform.position = Vector3.Lerp(hauled, target, e);
            }

            LookAtHands(t, forward);
            yield return null;
        }

        FinishClimb(target);
    }

    // With the visible body: the Mixamo climb plays with root motion (PlayerBody.BeginClimb sets it on the
    // edge), the controller rides along under it and the camera sits at the animated eyes.
    private System.Collections.IEnumerator ClimbAnimated(Vector3 target, Vector3 forward)
    {
        float seconds = GameTuning.Instance.climbOutSeconds;
        visualBody.BeginClimb(ClimbLedge, target.y, forward, seconds);
        PlayOneShot(SoundLibrary.Instance.Splash(0.1f), 0.5f);
        bool poured = false;
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            ClimbProgress = t;
            // The body is a child of the player: move the player under it, then put the body back.
            Vector3 feet = visualBody.transform.position;
            transform.position = feet;
            visualBody.transform.position = feet;
            if (!poured && t > 0.4f)
            {
                // The body leaves the water: it pours off.
                poured = true;
                PlayOneShot(SoundLibrary.Instance.Splash(0.3f), 0.4f);
            }

            LookAtHands(t, forward);
            yield return null;
        }

        FinishClimb(target);
    }

    // The eyes go to the hands on the edge (added on top of the player's own pitch, never fighting it
    // upward), easing back as the body rolls over. Capped: once the eye is over the edge the hands are right
    // under it, and looking straight down puts the view inside the chest (measured 75 to 85 degrees uncapped).
    private void LookAtHands(float t, Vector3 forward)
    {
        Vector3 toEdge = ClimbLedge + forward * 0.1f - EyePosition;
        float lookDown = Mathf.Min(-Mathf.Atan2(toEdge.y, new Vector2(toEdge.x, toEdge.z).magnitude) * Mathf.Rad2Deg + 18f, 40f);
        float lookWeight = Mathf.SmoothStep(0f, 1f, t / 0.2f) * (1f - Mathf.SmoothStep(0f, 1f, (t - 0.75f) / 0.25f));
        cameraPivot.localRotation = Quaternion.Euler(pitch + Mathf.Max(0f, lookDown - pitch) * lookWeight, 0f, 0f);
    }

    private void FinishClimb(Vector3 target)
    {
        ClimbProgress = 1f;
        transform.position = target;
        PlayOneShot(SoundLibrary.Instance.HeavyBreath(), 0.55f);
        body.enabled = true;
        velocity = Vector3.zero;
        climbing = false;
    }

    // ---------------------------------------------------------------- sitting

    public void SitOn(Seat seat)
    {
        if (IsSeated || IsStandingUp || goingToSeat || climbing || CurrentMode != Mode.Walking)
        {
            return;
        }

        StartCoroutine(SitRoutine(seat));
    }

    public void StandUp()
    {
        if (CanStand)
        {
            StartCoroutine(StandRoutine());
        }
    }

    // Turn around and step to where the sit-down animation starts: it carries the hips 0.47 m back.
    private System.Collections.IEnumerator SitRoutine(Seat seat)
    {
        goingToSeat = true;
        CurrentSeat = seat;
        body.enabled = false;
        velocity = Vector3.zero;
        Vector3 standAt = seat.SeatPoint + seat.Facing * 0.47f;
        standAt.y = seat.FloorY;
        Quaternion face = Quaternion.LookRotation(seat.Facing, Vector3.up);
        Vector3 from = transform.position;
        Quaternion fromRotation = transform.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            transform.SetPositionAndRotation(Vector3.Lerp(from, standAt, e), Quaternion.Slerp(fromRotation, face, e));
            yield return null;
        }

        transform.SetPositionAndRotation(standAt, face);
        seatYaw = 0f;
        seatedSince = Time.time;
        IsSeated = true;
        goingToSeat = false;
    }

    private System.Collections.IEnumerator StandRoutine()
    {
        IsSeated = false;
        IsStandingUp = true;
        yield return new WaitForSeconds(StandSeconds);
        // Keep looking where the player was looking: the seated head turn becomes the body's heading.
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + seatYaw, 0f);
        seatYaw = 0f;
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        body.enabled = true;
        IsStandingUp = false;
        CurrentSeat = null;
    }

    private void UpdateSeated()
    {
        if (goingToSeat)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (!InputLocked && mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            float sensitivity = lookSensitivity * GameSettings.MouseSensitivity;
            seatYaw = Mathf.Clamp(seatYaw + delta.x * sensitivity, -SeatedLookLimit, SeatedLookLimit);
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -80f, 80f);
        }

        cameraPivot.localRotation = Quaternion.Euler(pitch, seatYaw, 0f);
        if (!InputLocked && keyboard != null && (keyboard.wKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
        {
            StandUp();
        }
    }

    private void LateUpdate()
    {
        if ((Mathf.Abs(joltPitch) > 0.01f || Mathf.Abs(joltYaw) > 0.01f) && !IsSeated && !climbing)
        {
            float back = 1f - Mathf.Exp(-7f * Time.deltaTime);
            float p = joltPitch * back;
            float y = joltYaw * back;
            joltPitch -= p;
            joltYaw -= y;
            transform.Rotate(0f, -y, 0f);
            pitch = Mathf.Clamp(pitch + p, -85f, 85f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        if ((IsSeated || IsStandingUp || climbing) && visualBody != null && visualBody.isActiveAndEnabled)
        {
            cameraPivot.position = Vector3.Lerp(cameraPivot.position, visualBody.EyePoint, 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
    }

    private void Footsteps(Vector2 move, float speed)
    {
        if (!body.isGrounded || move.sqrMagnitude < 0.01f)
        {
            stepTimer = 0.2f;
            return;
        }

        stepTimer -= Time.deltaTime * speed / GameTuning.Instance.walkSpeed;
        if (stepTimer > 0f)
        {
            return;
        }

        stepTimer = 0.55f;
        if (CurrentMode == Mode.Wading)
        {
            PlayOneShot(ProceduralAudio.Splash(), Random.Range(0.25f, 0.4f));
        }
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (bodyAudio != null && clip != null)
        {
            bodyAudio.pitch = Random.Range(0.9f, 1.1f);
            bodyAudio.PlayOneShot(clip, volume);
        }
    }
}
