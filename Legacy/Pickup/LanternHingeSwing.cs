using UnityEngine;

// Swings the lantern BODY around its handle hinge, transform-driven (no physics joints).
//
// Ported from the standalone "Lamparina" sandbox project, where the lamp is a real physics rig: the
// body hangs from the handle on a HingeJoint whose zero-target motor acts as bearing friction, so it
// swings when moved and always settles hanging plumb. Physics joints are not usable on the animated
// hand (see the history comment on AttachSwingingHeldItem), so this runs the same model
// kinematically.
//
// It lives on a pivot transform placed exactly at the prefab's hinge (anchor + axis read from the
// prefab's HingeJoint at equip time). The handle above stays glued to the hand; this component only
// ROTATES the pivot about the hinge axis, driven by gravity and by the hand's motion, so the body
// below always settles hanging vertical no matter how the hand is tilted. That plumb rest pose is
// what makes it read as a lantern instead of a prop bolted to the wrist.
public sealed class LanternHingeSwing : MonoBehaviour
{
    [Tooltip("How strongly the hand's motion throws the body out. 1 = physically exact; raise it for a more theatrical swing.")]
    public float InertiaScale = 1f;

    [Tooltip("Viscous damping (air drag), per second. Higher = the swing loses energy faster.")]
    public float ViscousDamping = 0.8f;

    [Tooltip("Constant bearing friction, in deg/s^2. This is the sandbox's zero-target hinge motor: it is what makes the swing actually STOP after a few seconds instead of fading forever.")]
    public float BearingFrictionDegreesPerSecondSquared = 45f;

    [Tooltip("Swing limit either side of the authored rest pose, in degrees (the handle arc physically blocks the body).")]
    public float MaxAngleDegrees = 70f;

    [Tooltip("Ignore hand acceleration spikes above this (m/s^2), e.g. on the equip frame or a hitch.")]
    public float MaxHandAcceleration = 40f;

    private Quaternion restLocalRotation;
    private Vector3 hingeAxisLocal;     // in the pivot's rest frame
    private Vector3 hangDirectionLocal; // pivot -> body center of mass, in the pivot's rest frame
    private float pendulumLengthMeters = 0.15f;
    private float angleDegrees;
    private float angularVelocityDegreesPerSecond;
    private Vector3 lastPosition;
    private Vector3 lastVelocity;
    private bool hasLastPosition;
    private bool hasLastVelocity;
    private bool initialized;

    // Call right after AddComponent, while the pivot still sits in its authored rest pose.
    public void Initialize(Vector3 hingeAxisWorld, Vector3 bodyCenterWorld)
    {
        restLocalRotation = transform.localRotation;

        if (hingeAxisWorld.sqrMagnitude < 0.000001f)
        {
            hingeAxisWorld = transform.right;
        }

        hingeAxisLocal = transform.InverseTransformDirection(hingeAxisWorld.normalized);

        Vector3 toBody = bodyCenterWorld - transform.position;
        pendulumLengthMeters = Mathf.Max(0.05f, toBody.magnitude);
        hangDirectionLocal = transform.InverseTransformDirection(
            toBody.sqrMagnitude > 0.000001f ? toBody.normalized : Vector3.down
        );

        angleDegrees = 0f;
        angularVelocityDegreesPerSecond = 0f;
        hasLastPosition = false;
        hasLastVelocity = false;
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f)
        {
            return;
        }

        // The pivot is glued to the hand by the transform hierarchy, so its own world motion IS the
        // hand's motion; its acceleration drives the pendulum.
        Vector3 position = transform.position;
        Vector3 handAcceleration = Vector3.zero;
        if (hasLastPosition)
        {
            Vector3 velocity = (position - lastPosition) / dt;
            if (hasLastVelocity)
            {
                handAcceleration = Vector3.ClampMagnitude(
                    (velocity - lastVelocity) / dt,
                    MaxHandAcceleration
                );
            }

            lastVelocity = velocity;
            hasLastVelocity = true;
        }

        lastPosition = position;
        hasLastPosition = true;

        // The hinge axis is fixed in the pivot's own frame, so this is valid at any swing angle.
        Vector3 worldAxis = (transform.rotation * hingeAxisLocal).normalized;
        Vector3 hangWorld = transform.rotation * hangDirectionLocal;

        Vector3 plumb = Vector3.ProjectOnPlane(Vector3.down, worldAxis);
        Vector3 hang = Vector3.ProjectOnPlane(hangWorld, worldAxis);

        float angularAcceleration = 0f; // deg/s^2

        // If the hinge axis points straight down, plumb is unreachable within this degree of freedom;
        // skip gravity and drive for that frame and let damping settle whatever is left.
        if (plumb.sqrMagnitude > 0.000001f && hang.sqrMagnitude > 0.000001f)
        {
            plumb.Normalize();
            hang.Normalize();

            // Gravity: pendulum torque, proportional to the sine of the angle away from plumb.
            float angleToPlumbDegrees = Vector3.SignedAngle(hang, plumb, worldAxis);
            float gravityOmegaSquared = 9.81f / pendulumLengthMeters;
            angularAcceleration +=
                gravityOmegaSquared * Mathf.Sin(angleToPlumbDegrees * Mathf.Deg2Rad) * Mathf.Rad2Deg;

            // Hand motion: accelerating the hinge throws the body the opposite way.
            Vector3 tangent = Vector3.Cross(worldAxis, hang);
            angularAcceleration -=
                Vector3.Dot(handAcceleration, tangent) / pendulumLengthMeters
                * Mathf.Rad2Deg
                * InertiaScale;
        }

        angularAcceleration -= ViscousDamping * angularVelocityDegreesPerSecond;
        angularVelocityDegreesPerSecond += angularAcceleration * dt;

        // Bearing friction: constant deceleration with a full stop when slow. Viscous damping alone
        // never brings the swing to an actual rest; this does, like a real lamp bearing.
        float frictionStep = BearingFrictionDegreesPerSecondSquared * dt;
        if (Mathf.Abs(angularVelocityDegreesPerSecond) <= frictionStep)
        {
            angularVelocityDegreesPerSecond = 0f;
        }
        else
        {
            angularVelocityDegreesPerSecond -=
                Mathf.Sign(angularVelocityDegreesPerSecond) * frictionStep;
        }

        angleDegrees += angularVelocityDegreesPerSecond * dt;

        if (Mathf.Abs(angleDegrees) > MaxAngleDegrees)
        {
            angleDegrees = Mathf.Sign(angleDegrees) * MaxAngleDegrees;
            angularVelocityDegreesPerSecond = 0f;
        }

        transform.localRotation = restLocalRotation * Quaternion.AngleAxis(angleDegrees, hingeAxisLocal);
    }
}
