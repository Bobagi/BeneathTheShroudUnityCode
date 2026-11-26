using UnityEngine;

// The eye of "O homem na parede" (Dayse, 25/11: a face pressed through a chrome wall, a nose and one big eye).
// While nobody is near it wanders: that is Dayse's own animation, a clip played by the Animator on the root
// (WallMan_LookAround.anim, slow on purpose: "lenta dá uma aflição a mais"). When the viewer comes within
// GameTuning.wallManNoticeRange, in front of the wall and in plain sight, the eye finds them and stares, and this
// takes over after the Animator (LateUpdate): the gaze is a real-time reaction, so it is code (Gustavo, 25/11: "apenas
// um script para fazer ele encarar algo"; and "olhos são bem rápidos": it moves in saccades, a jump at
// wallManEyeSpeed whenever the viewer has moved more than a degree and a half, then dead still).
// The pivot's rest pose looks straight out of the wall (+Z of the root); the socket limits the turn.
public sealed class WallManEye : MonoBehaviour
{
    [SerializeField]
    private Transform eyePivot;

    [Tooltip("Who it looks at; empty = the main camera, found once.")]
    [SerializeField]
    private Transform viewer;

    [Tooltip("How far the eye can turn in its socket, to the sides and up or down (degrees).")]
    [SerializeField]
    private Vector2 socketLimit = new Vector2(46f, 38f);

    [Tooltip("Seconds between seeing the viewer and the first jump of the eye, and between losing them and wandering again.")]
    [SerializeField]
    private float reaction = 0.35f;

    [Tooltip("What blocks its sight.")]
    [SerializeField]
    private LayerMask sightBlockers = ~0;

    private const float SaccadeThreshold = 1.5f;
    private const float SightCheckInterval = 0.2f;

    private Vector2 gaze;
    private Vector2 saccadeTarget;
    private bool moving;
    private float stare;
    private bool sees;
    private float seenFor;
    private float nextSightCheck;

    public bool Staring => stare > 0.5f;

    public void Configure(Transform pivot, Transform who)
    {
        eyePivot = pivot;
        viewer = who;
    }

    private void Start()
    {
        if (viewer == null && Camera.main != null)
        {
            viewer = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (eyePivot == null || viewer == null)
        {
            return;
        }

        // Where the viewer is, as the eye's yaw and pitch in the wall's frame.
        Vector3 local = transform.InverseTransformPoint(viewer.position) - transform.InverseTransformPoint(eyePivot.position);
        float distance = local.magnitude;
        Vector2 aim = new Vector2(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -Mathf.Asin(Mathf.Clamp(local.y / Mathf.Max(distance, 0.001f), -1f, 1f)) * Mathf.Rad2Deg);
        GameTuning tuning = GameTuning.Instance;
        float range = tuning != null ? tuning.wallManNoticeRange : 7f;
        float speed = tuning != null ? tuning.wallManEyeSpeed : 520f;

        if (Time.time >= nextSightCheck)
        {
            nextSightCheck = Time.time + SightCheckInterval;
            bool inFront = local.z > 0.3f && distance < range && Mathf.Abs(aim.x) < socketLimit.x + 25f;
            sees = inFront && !Blocked(viewer.position);
        }

        seenFor = sees ? seenFor + Time.deltaTime : Mathf.Max(0f, seenFor - Time.deltaTime);
        seenFor = Mathf.Min(seenFor, reaction * 2f);
        stare = Mathf.MoveTowards(stare, seenFor >= reaction ? 1f : 0f, Time.deltaTime * (seenFor >= reaction ? 14f : 1.2f));
        if (stare <= 0f)
        {
            // Dayse's clip has the eye: remember where it left it, so the first jump starts from there.
            Vector3 e = eyePivot.localEulerAngles;
            gaze = new Vector2(Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.x));
            moving = false;
            return;
        }

        aim.x = Mathf.Clamp(aim.x, -socketLimit.x, socketLimit.x);
        aim.y = Mathf.Clamp(aim.y, -socketLimit.y, socketLimit.y);
        if (!moving && (aim - gaze).magnitude > SaccadeThreshold)
        {
            moving = true;
        }

        if (moving)
        {
            saccadeTarget = aim;
            gaze = Vector2.MoveTowards(gaze, saccadeTarget, speed * Time.deltaTime);
            moving = (saccadeTarget - gaze).sqrMagnitude > 0.0004f;
        }

        Quaternion stareAt = Quaternion.Euler(gaze.y, gaze.x, 0f);
        eyePivot.localRotation = stare >= 1f ? stareAt : Quaternion.Slerp(eyePivot.localRotation, stareAt, stare);
    }

    private bool Blocked(Vector3 target)
    {
        // From just in front of the eyeball, so the wall and the ball themselves are not in the way.
        Vector3 from = eyePivot.position + transform.forward * 0.6f;
        Vector3 to = target - from;
        float length = to.magnitude;
        if (length < 0.4f)
        {
            return false;
        }

        return Physics.Raycast(from, to / length, out RaycastHit hit, length - 0.35f, sightBlockers, QueryTriggerInteraction.Ignore)
            && !hit.transform.IsChildOf(viewer.root) && !hit.transform.IsChildOf(transform);
    }
}
