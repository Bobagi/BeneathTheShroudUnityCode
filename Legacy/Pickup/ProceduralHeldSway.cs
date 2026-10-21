using UnityEngine;

// Swings a held item (the lantern) like a lamp, WITHOUT physics joints.
//
// This lives on an ANCHOR that is parented to the hand bone AT THE GRIP POINT. Because the anchor is a
// plain transform child of the hand, the grip is glued to the hand by the transform hierarchy and can
// never flicker. This component ONLY rotates that anchor around its own origin (the grip), driven by a
// movement-fed damped spring, so the item body hanging below swings and settles hanging when still.
// It never touches position, so the grip stays perfectly pinned to the hand every frame.
public sealed class ProceduralHeldSway : MonoBehaviour
{
    [Tooltip("Spring constant pulling the item back to hanging. Higher = stiffer/quicker return.")]
    public float Stiffness = 45f;

    [Tooltip("How quickly the swing settles. Higher = less wobble.")]
    public float Damping = 6f;

    [Tooltip("How strongly the hand's motion throws the swing out. Main 'amount of swing' dial.")]
    public float InertiaScale = 3f;

    [Tooltip("Maximum swing angle from vertical, in degrees.")]
    public float MaxAngleDegrees = 35f;

    [Tooltip("World-space orientation at which the item hangs correctly (its resting pose).")]
    public Vector3 RestEuler = Vector3.zero;

    private Vector3 lastPosition;
    private Vector3 lastVelocity;
    private Vector2 swayAngle;      // radians; x tilts about world X, y about world Z
    private Vector2 swayVelocity;
    private bool ready;

    private void OnEnable()
    {
        lastPosition = transform.position;
        lastVelocity = Vector3.zero;
        swayAngle = Vector2.zero;
        swayVelocity = Vector2.zero;
        ready = true;
    }

    private void LateUpdate()
    {
        if (!ready)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        // Our own position is glued to the hand grip by the parent transform; use its motion to drive
        // the pendulum.
        Vector3 position = transform.position;
        Vector3 velocity = (position - lastPosition) / dt;
        Vector3 acceleration = (velocity - lastVelocity) / dt;
        lastVelocity = velocity;
        lastPosition = position;

        // Horizontal acceleration of the grip drives the swing -- the body lags behind the motion.
        Vector2 drive = new Vector2(-acceleration.z, acceleration.x) * InertiaScale;

        float maxRad = MaxAngleDegrees * Mathf.Deg2Rad;
        swayVelocity += (drive - Stiffness * swayAngle - Damping * swayVelocity) * dt;
        swayAngle += swayVelocity * dt;
        swayAngle = Vector2.ClampMagnitude(swayAngle, maxRad);

        Quaternion sway = Quaternion.Euler(swayAngle.x * Mathf.Rad2Deg, 0f, swayAngle.y * Mathf.Rad2Deg);

        // Base orientation = the hand bone's own rotation (our parent). At rest this makes the item sit
        // exactly as a plain rigid hold would -- the grip aligned to the hand the way it is authored --
        // which is the pose that reads as correctly gripped. The world-space sway then tilts it on top
        // for the swing, and RestEuler is an optional designer offset. Per-item fine-tuning of the model
        // orientation lives in HeldRotationEuler (applied on the lantern child under this anchor).
        Quaternion handRotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
        Quaternion baseOrientation = handRotation * Quaternion.Euler(RestEuler);

        // Rotate the anchor around its origin (the glued grip). Position is deliberately never touched,
        // so the grip stays pinned to the hand and only the hanging body swings.
        transform.rotation = sway * baseOrientation;
    }
}
