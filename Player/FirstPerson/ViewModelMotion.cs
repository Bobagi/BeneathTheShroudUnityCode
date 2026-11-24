using UnityEngine;

// The procedural layer of the first person arms, on the pivot between the camera and the rig (CameraPivot >
// ViewModel). The clips are fixed; this adds what reacts to the game, all in the eye's space and all tunable in
// GameTuning ("Revólver em primeira pessoa"): the arms lag behind the view and spring back when it stops (look
// sway); bob in a figure of eight with the steps (in step with the legs' own walk cycle, PlayerBody.StepCycle);
// lean against sideways movement; breathe when still; dip on landing; drop and cross the chest when sprinting; come
// up from below when something is drawn; and take the gun's kick (Kick). Aiming damps all of it.
[DefaultExecutionOrder(-30)]
public sealed class ViewModelMotion : MonoBehaviour
{
    [SerializeField]
    private FirstPersonController controller;

    [SerializeField]
    private PlayerBody body;

    [SerializeField]
    private FirstPersonArms arms;

    private CharacterController characterController;
    private Quaternion lastEye;
    private Vector2 sway;
    private Vector2 swaySpeed;
    private float bobPhase;
    private float lastStepCycle = -1f;
    private float bobWeight;
    private float breathPhase;
    private float strafe;
    private float sprint;
    private float draw = 1f;
    private float land;
    private float landSpeed;
    private float lastFallSpeed;
    private bool wasGrounded = true;
    private float aim;
    private float kick;
    private float kickSpeed;
    private Vector2 kickTwist;
    private float kickScale = 1f;
    private FirstPersonArms.Hands lastHands;

    // 0 to 1: aimed (sights or the torch held up); the motion calms down with it.
    public float Aim => aim;

    // The arms held still (the clip editor, a captured frame): no sway, bob, breath or kick.
    public bool Still { get; set; }

    // A gun's kick: the arms go back, up and the muzzle flips, on a spring that comes back with a little bounce.
    public void Kick(float scale)
    {
        kickScale = scale;
        kickSpeed += 40f * Random.Range(0.9f, 1.1f);
        GameTuning tuning = GameTuning.Instance;
        kickTwist += new Vector2(Random.Range(-1f, 1f) * tuning.revolverRecoilRoll * 0.4f, Random.Range(-1f, 1f) * tuning.revolverRecoilRoll);
    }

    private void Awake()
    {
        characterController = controller != null ? controller.GetComponent<CharacterController>() : null;
        lastEye = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
    }

    private void Update()
    {
        if (controller == null)
        {
            return;
        }

        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        Transform eye = transform.parent;
        GameTuning tuning = GameTuning.Instance;
        bool aiming = arms != null && arms.Aim;
        aim = Mathf.MoveTowards(aim, aiming ? 1f : 0f, dt / 0.18f);
        float a = Smooth(aim);
        float calm = Mathf.Lerp(1f, 0.25f, a);

        // Drawn: whatever comes into the hands rises from below.
        FirstPersonArms.Hands hands = arms != null ? arms.Current : FirstPersonArms.Hands.Empty;
        if (hands != lastHands)
        {
            lastHands = hands;
            draw = hands == FirstPersonArms.Hands.Empty ? 1f : 0f;
        }

        draw = Mathf.MoveTowards(draw, 1f, dt / Mathf.Max(0.05f, tuning.revolverDrawSeconds));

        // The kick's spring: out at once, back with a little overshoot.
        float stiffness = tuning.revolverRecoilSpring;
        float damping = 2f * Mathf.Sqrt(stiffness) * Mathf.Lerp(1f, 0.35f, tuning.revolverRecoilBounce);
        for (int step = 0; step < 4; step++)
        {
            float h = dt / 4f;
            kickSpeed += (-stiffness * kick - damping * kickSpeed) * h;
            kick += kickSpeed * h;
        }

        kickTwist = Vector2.Lerp(kickTwist, Vector2.zero, 1f - Mathf.Exp(-9f * dt));

        // Look sway: the eye's turn this frame, in degrees per second about its own axes, drives a spring.
        Quaternion turn = eye.rotation * Quaternion.Inverse(lastEye);
        lastEye = eye.rotation;
        turn.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f)
        {
            angle -= 360f;
        }

        Vector3 rate = float.IsNaN(axis.x) || dt <= 0f ? Vector3.zero : eye.InverseTransformDirection(axis) * (angle / dt);
        Vector2 swayTarget = Vector2.ClampMagnitude(new Vector2(-rate.y, -rate.x) * 0.02f * tuning.revolverSway * calm, 7f);
        swaySpeed += ((swayTarget - sway) * 160f - swaySpeed * 15f) * dt;
        sway += swaySpeed * dt;

        // Movement, from the body (the eye itself bobs with the head).
        Vector3 velocity = characterController != null ? characterController.velocity : Vector3.zero;
        Vector3 local = eye.InverseTransformDirection(velocity);
        float flatSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        bool grounded = characterController == null || characterController.isGrounded;
        strafe = Mathf.Lerp(strafe, Mathf.Clamp(local.x / 3f, -1f, 1f), 1f - Mathf.Exp(-8f * dt));
        float walk = grounded ? Mathf.Clamp01(flatSpeed / 1.5f) : 0f;
        bobWeight = Mathf.Lerp(bobWeight, walk, 1f - Mathf.Exp(-6f * dt));
        float cycle = tuning.revolverBobSyncSteps && body != null ? body.StepCycle : -1f;
        float advance = cycle >= 0f && lastStepCycle >= 0f
            ? Mathf.Repeat(cycle - lastStepCycle, 1f)
            : flatSpeed / tuning.revolverBobStride * dt;
        lastStepCycle = cycle;
        bobPhase = Mathf.Repeat(bobPhase + Mathf.Min(advance, 0.25f) * tuning.revolverBobRate, 1f);
        float phase = (bobPhase + tuning.revolverBobPhase) * Mathf.PI * 2f;
        float bob = bobWeight * tuning.revolverBob * calm;
        float sx = Mathf.Sin(phase);
        float s2 = Mathf.Cos(phase * 2f);

        breathPhase += dt * Mathf.PI * 2f * 0.22f;
        float breath = (1f - bobWeight) * tuning.revolverBreath * Mathf.Lerp(1f, 0.5f, a);

        if (grounded && !wasGrounded && lastFallSpeed < -1.5f)
        {
            landSpeed += Mathf.Clamp(lastFallSpeed, -12f, 0f) * 0.06f;
        }

        wasGrounded = grounded;
        lastFallSpeed = velocity.y;
        float airLift = grounded ? 0f : Mathf.Clamp(-velocity.y * 0.006f, -0.03f, 0.03f);
        landSpeed += (-land * 220f - landSpeed * 18f) * dt;
        land += landSpeed * dt;

        // Running: the revolver has a pose of its own on the rig (Revolver_Sprint, a clip) and a bigger bob so the arms
        // pump with the steps; the torch, without a clip yet, is tucked down and across by these offsets.
        bool sprinting = controller.IsSprinting && flatSpeed > 1f && aim < 0.1f && grounded && hands != FirstPersonArms.Hands.Empty;
        sprint = Mathf.MoveTowards(sprint, sprinting ? 1f : 0f, dt / 0.25f);
        if (arms != null)
        {
            arms.Sprint = sprinting;
        }

        float sp = Smooth(sprint);
        float tuck = hands == FirstPersonArms.Hands.Torch ? sp : 0f;
        Vector2 bobMove = Vector2.Lerp(tuning.revolverBobMove, tuning.revolverSprintBobMove, sp);
        Vector2 bobTurn = Vector2.Lerp(tuning.revolverBobTurn, tuning.revolverSprintBobTurn, sp);
        float away = 1f - Smooth(draw);
        float recoil = kick * Mathf.Lerp(1f, tuning.revolverRecoilAimed, a) * kickScale;

        Vector3 position = new Vector3(
                sx * bobMove.x * bob - strafe * 0.012f * calm + sway.x * 0.0025f,
                s2 * bobMove.y * bob + Mathf.Sin(breathPhase) * 0.0025f * breath + land + airLift + sway.y * 0.0025f
                    + recoil * tuning.revolverRecoilUp,
                -Mathf.Clamp01(local.z / 3f) * 0.012f * calm - recoil * tuning.revolverRecoilBack)
            + tuning.torchSprintOffset * tuck
            + new Vector3(0.02f, -0.22f, -0.08f) * away;
        Vector3 euler = new Vector3(
                sway.y + s2 * bobTurn.x * bob + Mathf.Sin(breathPhase + 0.8f) * 0.5f * breath + land * 120f
                    + tuning.torchSprintEuler.x * tuck + 55f * away - recoil * tuning.revolverRecoilFlip * 0.5f,
                sway.x + tuning.torchSprintEuler.y * tuck + kickTwist.x * recoil,
                sx * bobTurn.y * bob - strafe * 5f * calm - sway.x * 0.6f + tuning.torchSprintEuler.z * tuck
                    + 25f * away + kickTwist.y * recoil);

        if (Still)
        {
            position = Vector3.zero;
            euler = Vector3.zero;
        }

        transform.localPosition = position;
        transform.localRotation = Quaternion.Euler(euler);
    }

    private static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
