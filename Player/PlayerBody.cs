using UnityEngine;

// The player's own body in first person (the MetaHuman, see game-3d-assets): look down and there are chest, legs and
// feet; the arms are what the first person rig animates (FirstPersonArms, under the camera), copied onto this
// skeleton by ArmsFromRig while an arm is in use (torch, revolver, an action, a recorded take).
//
// True first person: on land the CAMERA sits at the eyes of the animated head (smoothed), and the body stands exactly
// on the player's feet, never dragged around to meet the camera. Looking down shows the chest and belly from outside,
// and the head's own sway becomes the camera's. (Anchoring the body to the camera made the feet skate.)
//
// The body keeps its own heading, apart from the view:
// - On land, standing, it stays put until the view has turned more than 75 degrees (15 looking down at the feet,
//   GameTuning turnInPlaceAt / lookDownTurnAt), then turns toward it while the feet step one at a time. Walking, it
//   faces where it walks (up to 60 degrees off the view); walking backwards it faces the view and the walk plays in
//   reverse. With a weapon out it keeps square to the view.
// - The moment the body stops, the feet plant where they are and the body settles over them (stepping if a foot is
//   left too far out), instead of the walk cycle sliding them into the idle pose.
// - In the water the body is purely visual: the controller swims by the view alone; the body follows the view with a
//   lag (yaw and pitch, at most 100 degrees behind). The head stays just behind the eye.
// - Sitting and climbing out of the water, the clips move the body (root motion) and the camera rides the head.
//
// IK (10/10, Gustavo: "IK só em pontos chave e só estético"): the feet (laid on the floor, planted when stopped,
// sitting) and the look-at of the spine and head. Nothing else. The feet never move the camera: the hips drop so both
// feet reach uneven ground, and the camera is held at the height it would have without the drop.
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(ArmsFromRig))]
[DefaultExecutionOrder(100)]
public sealed class PlayerBody : MonoBehaviour
{
    [SerializeField]
    private FirstPersonController controller;

    [Tooltip("The first person arms rig under the camera: what the arms of this body copy while a hand is busy.")]
    [SerializeField]
    private FirstPersonArms arms;

    private const float WalkFacingLimit = 60f;
    private const float SwimMaxLag = 100f;
    private const float PlantBelowSpeed = 0.3f;
    private const float StepDistance = 0.12f;
    private const float StepAngle = 25f;
    private const float StepSeconds = 0.26f;

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int ModeId = Animator.StringToHash("Mode");
    private static readonly int CrouchId = Animator.StringToHash("Crouch");
    private static readonly int SeatedId = Animator.StringToHash("Seated");
    private static readonly int ClimbSpeedId = Animator.StringToHash("ClimbSpeed");
    private static readonly int LandStateHash = Animator.StringToHash("Land");
    private static readonly int CrouchStateHash = Animator.StringToHash("Crouch");

    // "Climbing To Top", measured on this body in its own space (unscaled): at the start the hands rest on a ledge
    // 1.48 m above the feet and 0.37 m ahead (the face just beyond, 0.42); at the end it stands on top.
    private const string ClimbState = "ClimbOut";
    private const float ClimbLedgeHeight = 1.48f;
    private const float ClimbEdgeAhead = 0.42f;

    private sealed class Foot
    {
        public AvatarIKGoal goal;
        public Vector3 planted;
        public Quaternion plantedRotation;
        public bool hasPlant;
        public bool stepping;
        public float step;
        public Vector3 from;
        public Quaternion fromRotation;
        public float groundOffset;
        public Vector3 seatPlant;
        public Quaternion seatPlantRotation;
        public bool hasSeatPlant;
    }

    private Animator animator;
    private BodySkins skins;
    private ArmsFromRig armsFromRig;
    private Transform headBone;
    private Transform hipsBone;
    private Vector3 eyeInHead;
    private Vector3 lastPosition;
    private Vector3 velocity;
    private float speed;
    private float bodyYaw;
    private float bodyPitch;
    private bool turning;
    private float plantWeight;
    private float seatedFeetWeight;
    private float hipsDrop;
    private float viewApart;
    private readonly Foot leftFoot = new Foot { goal = AvatarIKGoal.LeftFoot };
    private readonly Foot rightFoot = new Foot { goal = AvatarIKGoal.RightFoot };
    private readonly RaycastHit[] hits = new RaycastHit[8];

    public FirstPersonArms Arms => arms;
    public bool HoldsWeapon => arms != null && arms.Current == FirstPersonArms.Hands.Revolver;

    // Eye position on the animated head.
    public Vector3 EyePoint => headBone.TransformPoint(eyeInHead);

    // Where the legs are in their walk or run loop, 0 to 1 (one loop = two steps), read from the base layer's
    // locomotion blend tree; negative when the body is in no such state (water, seat, climb, in a transition).
    public float StepCycle
    {
        get
        {
            if (animator == null || animator.IsInTransition(0))
            {
                return -1f;
            }

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            return state.shortNameHash == LandStateHash || state.shortNameHash == CrouchStateHash
                ? Mathf.Repeat(state.normalizedTime, 1f)
                : -1f;
        }
    }

    // Whole-body clips recorded from video (Editor/HandMocap/RecordedBody), each on its own masked layer with an IK
    // pass, so the look-at and the feet still apply on top:
    //   "Corpo gravado" (spine and head): actions like the cough, played once by PlayBodyClip; the arm of the action
    //   is the first person rig's (FirstPersonArms.Cough), see Cough().
    //   "Idle gravado" (the whole body but the fingers): the idle filmed standing, an alternative to the pack idle,
    //   on with UseRecordedIdle while standing still on land (its weight follows the stillness).
    public const string BodyClipLayer = "Corpo gravado";
    public const string BodyClipEmpty = "Empty";
    public const string CoughState = "Tosse";
    public const string RecordedIdleLayer = "Idle gravado";
    private int bodyClipLayer = -1;
    private int recordedIdleLayer = -1;
    private float recordedIdleWeight;

    // Off by default: the pack idle stays until Gustavo picks one (09/10).
    public bool UseRecordedIdle { get; set; }

    public float RecordedIdleWeight => recordedIdleWeight;

    // 0 with "Corpo gravado" on "Empty", 1 while an action clip plays, in between during the crossfades.
    public float BodyClipWeight => ClipLayerWeight(bodyClipLayer, BodyClipEmpty);

    public bool PlayBodyClip(string state)
    {
        if (bodyClipLayer < 0 || !animator.HasState(bodyClipLayer, Animator.StringToHash(state)))
        {
            return false;
        }

        animator.CrossFadeInFixedTime(state, 0.2f, bodyClipLayer, 0f);
        return true;
    }

    // The cough: the spine and head bow on this body (clip "Tosse") and the left hand comes to the mouth on the
    // first person rig (clip Cough), the two started together.
    public bool Cough()
    {
        bool spine = PlayBodyClip(CoughState);
        bool hand = arms != null && arms.Cough();
        return spine || hand;
    }

    private float ClipLayerWeight(int layer, string empty)
    {
        if (layer < 0 || animator.GetLayerWeight(layer) <= 0.01f)
        {
            return 0f;
        }

        float current = animator.GetCurrentAnimatorStateInfo(layer).IsName(empty) ? 0f : 1f;
        if (!animator.IsInTransition(layer))
        {
            return current;
        }

        float next = animator.GetNextAnimatorStateInfo(layer).IsName(empty) ? 0f : 1f;
        return Mathf.Lerp(current, next, animator.GetAnimatorTransitionInfo(layer).normalizedTime);
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        bodyClipLayer = animator.runtimeAnimatorController != null ? animator.GetLayerIndex(BodyClipLayer) : -1;
        recordedIdleLayer = animator.runtimeAnimatorController != null ? animator.GetLayerIndex(RecordedIdleLayer) : -1;
        skins = GetComponent<BodySkins>();
        armsFromRig = GetComponent<ArmsFromRig>();
        if (armsFromRig.Rig == null)
        {
            armsFromRig.Rig = arms;
        }

        headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
        // Bind pose: eyes 9 cm above the skull base and 10 cm forward of it.
        eyeInHead = headBone.InverseTransformPoint(headBone.position + transform.up * 0.09f + transform.forward * 0.1f);
        lastPosition = controller.transform.position;
        bodyYaw = controller.transform.eulerAngles.y;
    }

    // ---------------------------------------------------------------- heading and animator

    private bool InWater
    {
        get
        {
            FirstPersonController.Mode mode = controller.CurrentMode;
            return mode == FirstPersonController.Mode.Diving || mode == FirstPersonController.Mode.SurfaceSwimming;
        }
    }

    private bool InSitSequence => controller.IsSeated || controller.IsStandingUp;

    // Root motion moves the body (sitting, standing up, climbing out); nothing else places it then.
    private bool Animated => InSitSequence || controller.IsClimbingOut;

    // Starts the climb out of the water: the body faces the edge, set so the clip's hands rest on it, and the clip is
    // stretched to last `seconds`.
    public void BeginClimb(Vector3 edge, float topY, Vector3 forward, float seconds)
    {
        float scale = transform.lossyScale.y;
        bodyYaw = Quaternion.LookRotation(forward).eulerAngles.y;
        bodyPitch = 0f;
        transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
        Vector3 at = edge - forward * (ClimbEdgeAhead * scale);
        transform.position = new Vector3(at.x, topY - ClimbLedgeHeight * scale, at.z);
        AnimationClip clip = null;
        foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
        {
            if (c.length > 3.5f && c.averageSpeed.y > 0.2f)
            {
                clip = c;
            }
        }

        animator.SetFloat(ClimbSpeedId, (clip != null ? clip.length : 4f) / Mathf.Max(0.5f, seconds));
        animator.CrossFadeInFixedTime(ClimbState, 0.25f);
    }

    private void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 position = controller.transform.position;
        velocity = Vector3.Lerp(velocity, (position - lastPosition) / dt, 1f - Mathf.Exp(-15f * dt));
        lastPosition = position;
        Transform eye = controller.CameraPivot;
        float viewYaw = eye.eulerAngles.y;
        float signedSpeed = Animated ? 0f : InWater ? SwimHeading(eye, dt) : LandHeading(viewYaw, dt);
        if (controller.CurrentSeat != null && !InSitSequence)
        {
            // Walking to a seat: turn the body to face out of it before the sit clip starts, or the clip's root
            // motion sits it down along its own heading (it sat facing the backrest, legs through it).
            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, controller.transform.eulerAngles.y, 720f * dt);
        }

        viewApart = Mathf.Abs(Mathf.DeltaAngle(bodyYaw, viewYaw));

        speed = Mathf.Lerp(speed, signedSpeed, 1f - Mathf.Exp(-12f * dt));
        animator.SetFloat(SpeedId, speed);
        animator.SetInteger(ModeId, (int)controller.CurrentMode);
        animator.SetBool(CrouchId, controller.IsCrouched);
        animator.SetBool(SeatedId, controller.IsSeated);
        animator.applyRootMotion = Animated;

        // The arms follow the first person rig except while a root motion clip owns the whole body (the climb, the
        // sit-down and stand-up): then the clip's arms do their work.
        armsFromRig.Allow = Mathf.MoveTowards(armsFromRig.Allow, Animated ? 0f : 1f, dt * 4f);

        if (recordedIdleLayer >= 0)
        {
            // The filmed idle only standing still on land; it fades out as the body starts to walk.
            bool canIdle = UseRecordedIdle && !InWater && !Animated && !controller.IsCrouched && !controller.IsClimbingOut;
            float still = canIdle ? 1f - Mathf.Clamp01(Mathf.Abs(speed) / 0.6f) : 0f;
            recordedIdleWeight = Mathf.MoveTowards(recordedIdleWeight, still, dt * 3f);
            animator.SetLayerWeight(recordedIdleLayer, recordedIdleWeight);
        }
    }

    // On land: returns the animator speed (negative walks backwards) and turns bodyYaw.
    private float LandHeading(float viewYaw, float dt)
    {
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        float moving = flat.magnitude;
        bodyPitch = 0f;
        if (moving > 0.15f)
        {
            turning = false;
            float moveYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float fromView = Mathf.DeltaAngle(viewYaw, moveYaw);
            bool backwards = Mathf.Abs(fromView) > 110f;
            // The legs turn toward where the body goes (up to WalkFacingLimit): there is no strafe clip, and this is
            // what makes a sideways step read as one. Kept with the gun out too (limiting it to 20 degrees made the
            // body walk forward while moving sideways; Gustavo, 07/10): the arms hang from the camera, not the torso.
            float target = backwards
                ? viewYaw + Mathf.Clamp(Mathf.DeltaAngle(viewYaw, moveYaw + 180f), -WalkFacingLimit, WalkFacingLimit)
                : viewYaw + Mathf.Clamp(fromView, -WalkFacingLimit, WalkFacingLimit);
            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, target, 300f * dt);
            return backwards ? -moving : moving;
        }

        // Looking down at the feet, the torso has to stay under the view: the more the view drops, the sooner the
        // body turns (at 75 degrees apart and looking straight down, the head was twisted sideways into the chest).
        GameTuning tuning = GameTuning.Instance;
        float down = Mathf.InverseLerp(20f, 45f, Mathf.Asin(Mathf.Clamp(-controller.CameraPivot.forward.y, -1f, 1f)) * Mathf.Rad2Deg);
        float turnAt = Mathf.Lerp(tuning.turnInPlaceAt, tuning.lookDownTurnAt, down);
        if (HoldsWeapon)
        {
            // With the gun out the torso keeps square to the view all the time (no waiting for a turn in place), so
            // the shoulders the arms hang from never have far to go.
            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, viewYaw, 360f * Time.deltaTime);
            turning = false;
            return 0f;
        }

        if (!turning && Mathf.Abs(Mathf.DeltaAngle(bodyYaw, viewYaw)) > turnAt)
        {
            turning = true;
        }

        if (turning)
        {
            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, viewYaw, 200f * dt);
            turning = Mathf.Abs(Mathf.DeltaAngle(bodyYaw, viewYaw)) > 4f;
        }

        return 0f;
    }

    // In the water the body only follows the view, with a lag: pitched along it while swimming, upright while
    // treading. It never steers anything.
    private float SwimHeading(Transform eye, float dt)
    {
        turning = false;
        float moving = velocity.magnitude;
        Vector3 e = eye.eulerAngles;
        float viewPitch = e.x > 180f ? e.x - 360f : e.x;
        bool swimming = moving > 0.4f;
        bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, e.y, 110f * dt);
        float lag = Mathf.DeltaAngle(e.y, bodyYaw);
        if (Mathf.Abs(lag) > SwimMaxLag)
        {
            bodyYaw = e.y + Mathf.Sign(lag) * SwimMaxLag;
        }

        bodyPitch = Mathf.MoveTowardsAngle(bodyPitch, swimming ? viewPitch : 0f, 90f * dt);
        return swimming ? moving : moving * 0.3f;
    }

    // ---------------------------------------------------------------- IK: look-at and feet

    private void OnAnimatorIK(int layerIndex)
    {
        Transform eye = controller.CameraPivot;
        GameTuning tuning = GameTuning.Instance;

        // Spine and head follow the view. In the water the chest twists further round as the body lags behind the
        // view.
        float bodyLook = tuning.bodyLookWeight;
        if (InWater)
        {
            bodyLook = Mathf.Lerp(bodyLook, 1f, Mathf.InverseLerp(20f, SwimMaxLag, viewApart));
        }

        // A recorded action owns the spine and the head while it plays (the cough bows the head; the camera rides the
        // head, so the view dips with it).
        float own = 1f - BodyClipWeight;
        animator.SetLookAtWeight(own, bodyLook * own, own, 0f, 0.5f);
        Vector3 look = eye.forward;
        if (!InWater)
        {
            // The camera keeps the full pitch; the head (and the eye that rides it) bows less, or it sinks into the
            // chest (measured: eye 11 cm from the suit's chest pack looking down at 80 degrees, 2026-10-02).
            float pitch = Mathf.Asin(Mathf.Clamp(-look.y, -1f, 1f)) * Mathf.Rad2Deg;
            float limit = skins != null && skins.CurrentIsBulky ? tuning.bulkyHeadPitch : tuning.headPitch;
            if (pitch > limit)
            {
                Vector3 flat = new Vector3(look.x, 0f, look.z).normalized;
                look = Quaternion.AngleAxis(limit, Vector3.Cross(Vector3.up, flat)) * flat;
            }
        }

        animator.SetLookAtPosition(eye.position + look * 6f);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
        Feet();
    }

    // Feet on the floor. Moving, the animation places them and IK only lays them on the ground. The moment the body
    // stops, each foot stays where it was put down and steps (a short arc, one foot at a time) only when the settling
    // body leaves it too far out or twisted. Purely visual: the camera does not follow the hips' drop (LateUpdate).
    private void Feet()
    {
        if (SeatedFeet())
        {
            return;
        }

        bool onLand = !InWater && !Animated;
        float moving = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        bool planted = onLand && moving < PlantBelowSpeed;
        float dt = Time.deltaTime;
        plantWeight = Mathf.MoveTowards(plantWeight, planted ? 1f : 0f, dt * (planted ? 14f : 10f));
        if (!onLand)
        {
            SetFootWeight(leftFoot, 0f);
            SetFootWeight(rightFoot, 0f);
            leftFoot.hasPlant = rightFoot.hasPlant = false;
            hipsDrop = Mathf.MoveTowards(hipsDrop, 0f, dt);
            return;
        }

        float drop = Mathf.Min(Ground(leftFoot), Ground(rightFoot));
        hipsDrop = Mathf.Lerp(hipsDrop, Mathf.Min(0f, drop), 1f - Mathf.Exp(-10f * dt));
        PlaceFoot(leftFoot, rightFoot, dt);
        PlaceFoot(rightFoot, leftFoot, dt);
    }

    // How far the floor under this foot is from the level the body stands on (negative: lower).
    private float Ground(Foot foot)
    {
        Vector3 animated = animator.GetIKPosition(foot.goal);
        float rootY = controller.transform.position.y;
        foot.groundOffset = 0f;
        int count = Physics.RaycastNonAlloc(animated + Vector3.up * 0.5f, Vector3.down, hits, 1.1f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].distance < best && !hits[i].collider.transform.IsChildOf(controller.transform))
            {
                best = hits[i].distance;
                foot.groundOffset = Mathf.Clamp(hits[i].point.y - rootY, -0.45f, 0.45f);
            }
        }

        return foot.groundOffset;
    }

    private void PlaceFoot(Foot foot, Foot other, float dt)
    {
        Vector3 desired = animator.GetIKPosition(foot.goal) + Vector3.up * (foot.groundOffset - hipsDrop);
        Quaternion desiredRotation = animator.GetIKRotation(foot.goal);
        if (plantWeight <= 0f || !foot.hasPlant)
        {
            // Plant where the foot is right now, mid-stride included: that is what stops the skating.
            foot.planted = desired;
            foot.plantedRotation = desiredRotation;
            foot.hasPlant = plantWeight > 0f;
            foot.stepping = false;
        }
        else if (!foot.stepping && !other.stepping
            && (Vector3.Distance(foot.planted, desired) > StepDistance || Quaternion.Angle(foot.plantedRotation, desiredRotation) > StepAngle))
        {
            foot.stepping = true;
            foot.step = 0f;
            foot.from = foot.planted;
            foot.fromRotation = foot.plantedRotation;
        }

        Vector3 position = foot.planted;
        Quaternion rotation = foot.plantedRotation;
        if (foot.stepping)
        {
            foot.step += dt / StepSeconds;
            float s = Mathf.SmoothStep(0f, 1f, foot.step);
            position = Vector3.Lerp(foot.from, desired, s) + Vector3.up * Mathf.Sin(s * Mathf.PI) * 0.07f;
            rotation = Quaternion.Slerp(foot.fromRotation, desiredRotation, s);
            if (foot.step >= 1f)
            {
                foot.stepping = false;
                foot.planted = desired;
                foot.plantedRotation = desiredRotation;
            }
        }

        // Moving: the animation's feet, only laid on the floor. Stopped: the planted foot.
        animator.SetIKPositionWeight(foot.goal, 1f);
        animator.SetIKRotationWeight(foot.goal, plantWeight);
        animator.SetIKPosition(foot.goal, Vector3.Lerp(desired, position, plantWeight));
        animator.SetIKRotation(foot.goal, Quaternion.Slerp(desiredRotation, rotation, plantWeight));
    }

    // Seated: the body is slid onto the cushion (LateUpdate), which carries the animated feet off the floor with it,
    // and the idle clip lets them drift. Each foot is put back on the floor under where the clip has it, once, and
    // held there for as long as the player sits; the weight eases in after the sit-down clip.
    private bool SeatedFeet()
    {
        // CanStand: seated and the sit-down clip is over.
        bool settled = controller.CanStand && controller.CurrentSeat != null && !InWater;
        seatedFeetWeight = Mathf.MoveTowards(seatedFeetWeight, settled ? 1f : 0f, Time.deltaTime * 2.5f);
        if (seatedFeetWeight <= 0f)
        {
            leftFoot.hasSeatPlant = rightFoot.hasSeatPlant = false;
            return false;
        }

        SeatFoot(leftFoot);
        SeatFoot(rightFoot);
        return true;
    }

    private void SeatFoot(Foot foot)
    {
        Vector3 animated = animator.GetIKPosition(foot.goal);
        Quaternion animatedRotation = animator.GetIKRotation(foot.goal);
        if (!foot.hasSeatPlant && controller.CanStand)
        {
            // The clip's feet are right for a root standing on the floor: shift by how far the root is off it.
            float floor = controller.transform.position.y;
            int count = Physics.RaycastNonAlloc(animated + Vector3.up * 0.3f, Vector3.down, hits, 1.2f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].distance < best && !hits[i].collider.transform.IsChildOf(controller.transform))
                {
                    best = hits[i].distance;
                    floor = hits[i].point.y;
                }
            }

            Vector3 forward = Vector3.ProjectOnPlane(animatedRotation * Vector3.forward, Vector3.up);
            // + 3 cm: measured, the flattened foot otherwise sank its toes 1 cm into the floor.
            foot.seatPlant = animated + Vector3.up * (floor - transform.position.y + 0.03f);
            foot.seatPlantRotation = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, Vector3.up) : animatedRotation;
            foot.hasSeatPlant = true;
        }

        if (!foot.hasSeatPlant)
        {
            SetFootWeight(foot, 0f);
            return;
        }

        animator.SetIKPositionWeight(foot.goal, seatedFeetWeight);
        animator.SetIKRotationWeight(foot.goal, seatedFeetWeight);
        animator.SetIKPosition(foot.goal, foot.seatPlant);
        animator.SetIKRotation(foot.goal, foot.seatPlantRotation);
    }

    private void SetFootWeight(Foot foot, float weight)
    {
        animator.SetIKPositionWeight(foot.goal, weight);
        animator.SetIKRotationWeight(foot.goal, weight);
    }

    // ---------------------------------------------------------------- placement

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        Transform eye = controller.CameraPivot;
        if (controller.IsClimbingOut)
        {
            // Root motion carries the body; the controller follows it and puts the camera at the eyes.
        }
        else if (InWater)
        {
            GameTuning tuning = GameTuning.Instance;
            float swimming = Mathf.Clamp01((Mathf.Abs(speed) - 0.4f) / 0.6f);
            transform.rotation = Quaternion.Euler(bodyPitch, bodyYaw, 0f);
            // The head just behind and below the eye; the body trails behind it along its own heading.
            Vector3 delta = eye.position - transform.forward * tuning.bodyBehindEye - Vector3.up * Mathf.Lerp(0.1f, tuning.swimHeadBelowEye, swimming) - headBone.position;
            transform.position += Vector3.Lerp(Vector3.zero, delta, 1f - Mathf.Exp(-20f * dt));
        }
        else if (controller.IsSeated && controller.CurrentSeat != null && hipsBone != null)
        {
            // The sit clip leaves the hips about 0.5 m short of the seat (measured on the armchair and the rocking
            // chair: the body floated in front of them). Slide the body until the hips are over the seat the builder
            // measured; the stand-up clip then carries it forward to the player again.
            Seat seat = controller.CurrentSeat;
            Vector3 target = seat.SeatPoint + seat.Facing * 0.04f;
            Vector3 offset = target - hipsBone.position;
            offset.y = Mathf.Clamp(offset.y + 0.06f, -0.2f, 0.2f);
            transform.position += offset * (1f - Mathf.Exp(-6f * dt));
        }
        else if (!InSitSequence)
        {
            // Standing on the player's feet (dropped by the feet IK so both reach the floor). The camera is NOT moved
            // from here: it belongs to the controller (fixed eye height plus its own step bob). Riding the animated head
            // made the view wobble with the idle animation when standing still (Gustavo, 22/10: the camera leads, the
            // body only follows it).
            transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
            Vector3 player = controller.transform.position;
            Vector3 target = new Vector3(player.x, player.y + hipsDrop, player.z);
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-30f * dt));
        }
    }

    // The Starter Assets walk and run clips fire these events; the steps are already heard from the body
    // (FootstepAudio), so they only need a receiver.
    private void OnFootstep(AnimationEvent animationEvent)
    {
    }

    private void OnLand(AnimationEvent animationEvent)
    {
    }
}
