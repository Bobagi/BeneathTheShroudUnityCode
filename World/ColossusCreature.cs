using System.Collections;
using UnityEngine;

// The giant on the horizon. It wanders slowly between far waypoints, groans now and then, and when
// the player fires it stops, turns its head toward the sound and answers.
public sealed class ColossusCreature : MonoBehaviour
{
    [SerializeField]
    private Transform player;

    [Header("Wandering")]
    [SerializeField]
    private Transform[] waypoints;

    [SerializeField]
    private float walkSpeed = 2.2f;

    [SerializeField]
    private float turnDegreesPerSecond = 4f;

    [SerializeField]
    private float bobAmplitude = 1.2f;

    [SerializeField]
    private float bobPeriodSeconds = 7f;

    [Header("Head")]
    [SerializeField]
    private Transform head;

    [SerializeField]
    private float maxHeadYaw = 70f;

    [Header("Voice")]
    [SerializeField]
    private AudioSource voice;

    [SerializeField]
    private AudioClip[] groanClips;

    [SerializeField]
    private Vector2 secondsBetweenGroans = new Vector2(25f, 55f);

    [SerializeField]
    private Vector2 answerDelaySeconds = new Vector2(1.5f, 3.5f);

    [SerializeField]
    private float noiseHearingMultiplier = 20f;

    private int waypointIndex;

    public bool IsAlerted => alerted;

    private float baseHeight;
    private bool alerted;
    private float headWeight;
    private Quaternion headRestLocal;
    private Coroutine answerRoutine;
    private Animator animator;
    private Animation legacyAnimation;
    private string idleClip;
    private string walkClip;

    private void Awake()
    {
        baseHeight = transform.position.y;
        animator = GetComponentInChildren<Animator>();
        legacyAnimation = GetComponentInChildren<Animation>();
        if (legacyAnimation != null)
        {
            // FBX takes arrive as "Rig|Idle" / "Rig|Walk".
            foreach (AnimationState state in legacyAnimation)
            {
                state.wrapMode = WrapMode.Loop;
                if (state.name.EndsWith("Idle"))
                {
                    idleClip = state.name;
                }
                else if (state.name.EndsWith("Walk"))
                {
                    walkClip = state.name;
                }
            }
        }
        if (head != null)
        {
            headRestLocal = head.localRotation;
        }
    }

    private void OnEnable()
    {
        WorldEvents.Noise += OnNoise;
        StartCoroutine(GroanLoop());
    }

    private void OnDisable()
    {
        WorldEvents.Noise -= OnNoise;
    }

    private void Update()
    {
        if (!alerted)
        {
            Wander();
        }
        else if (player != null)
        {
            TurnToward(player.position);
        }

        if (animator != null)
        {
            animator.speed = alerted ? 0.55f : 1f;
        }

        if (legacyAnimation != null)
        {
            string wanted = alerted || walkClip == null ? idleClip : walkClip;
            if (wanted != null && !legacyAnimation.IsPlaying(wanted))
            {
                legacyAnimation.CrossFade(wanted, 2.5f);
            }
        }

        // Slow heave of the whole body: breathing at a titanic scale.
        Vector3 position = transform.position;
        position.y = baseHeight + Mathf.Sin(Time.time * Mathf.PI * 2f / bobPeriodSeconds) * bobAmplitude;
        transform.position = position;
    }

    private void LateUpdate()
    {
        if (head == null || player == null)
        {
            return;
        }

        headWeight = Mathf.MoveTowards(headWeight, alerted ? 1f : 0f, Time.deltaTime * 0.4f);
        if (headWeight <= 0f)
        {
            return;
        }

        // Rotate the head (after animation) so its forward swings toward the player within a cone.
        Vector3 toPlayer = player.position - head.position;
        Vector3 flatForward = transform.forward;
        float yaw = Vector3.SignedAngle(flatForward, Vector3.ProjectOnPlane(toPlayer, Vector3.up), Vector3.up);
        yaw = Mathf.Clamp(yaw, -maxHeadYaw, maxHeadYaw);
        Quaternion look = Quaternion.AngleAxis(yaw * headWeight, Vector3.up);
        head.rotation = look * head.rotation;
    }

    private void Wander()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            return;
        }

        Transform target = waypoints[waypointIndex];
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude < 15f)
        {
            waypointIndex = (waypointIndex + 1) % waypoints.Length;
            return;
        }

        TurnToward(target.position);
        transform.position += transform.forward * walkSpeed * Time.deltaTime;
    }

    private void TurnToward(Vector3 worldPoint)
    {
        Vector3 direction = worldPoint - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 1f)
        {
            return;
        }

        Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            target,
            turnDegreesPerSecond * Time.deltaTime
        );
    }

    private void OnNoise(Vector3 position, float radius)
    {
        // A gunshot heard across the whole battlefield: it is always listening.
        if (Vector3.Distance(position, transform.position) > radius * noiseHearingMultiplier)
        {
            return;
        }

        if (answerRoutine != null)
        {
            StopCoroutine(answerRoutine);
        }

        answerRoutine = StartCoroutine(Answer());
    }

    private IEnumerator Answer()
    {
        alerted = true;
        yield return new WaitForSeconds(Random.Range(answerDelaySeconds.x, answerDelaySeconds.y));
        Groan(0.8f);
        CameraShake.Shake(0.25f, 1.5f);
        yield return new WaitForSeconds(14f);
        alerted = false;
        answerRoutine = null;
    }

    private IEnumerator GroanLoop()
    {
        yield return new WaitForSeconds(Random.Range(8f, 15f));
        while (true)
        {
            if (!alerted)
            {
                Groan(Random.Range(0.62f, 0.78f));
            }

            yield return new WaitForSeconds(Random.Range(secondsBetweenGroans.x, secondsBetweenGroans.y));
        }
    }

    private void Groan(float pitch)
    {
        if (voice == null || groanClips == null || groanClips.Length == 0)
        {
            return;
        }

        voice.pitch = pitch;
        voice.PlayOneShot(groanClips[Random.Range(0, groanClips.Length)]);
    }
}
