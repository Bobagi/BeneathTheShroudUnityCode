using UnityEngine;

// A creature's ragdoll (rigid bodies, colliders and joints built by DrownedSetup with Unity's ragdoll builder).
// Alive, every body is kinematic and rides the animation: the colliders are the hitboxes. Each frame (after the
// animation) the bones' motion is tracked, so that when the creature dies the bodies are let go carrying the
// velocity they had in the animation (a runner falls forward, a staggering body keeps staggering) and the
// bullet's impulse is added at the point it struck. Once everything rests, the bodies go back to kinematic so
// a corpse costs nothing.
[DefaultExecutionOrder(200)]
public sealed class Ragdoll : MonoBehaviour
{
    [SerializeField]
    private Rigidbody[] bodies = new Rigidbody[0];

    [SerializeField]
    private Animator animator;

    [Tooltip("Seconds of rest (all bodies slow) before the corpse is frozen.")]
    [SerializeField]
    private float settleSeconds = 2.5f;

    private Vector3[] lastPositions;
    private Quaternion[] lastRotations;
    private Vector3[] linear;
    private Vector3[] angular;
    private float restingFor;

    public bool IsLimp { get; private set; }
    public bool IsSettled { get; private set; }
    public Rigidbody[] Bodies => bodies;

    public void Configure(Animator owner, Rigidbody[] ragdollBodies)
    {
        animator = owner;
        bodies = ragdollBodies;
    }

    private void Awake()
    {
        lastPositions = new Vector3[bodies.Length];
        lastRotations = new Quaternion[bodies.Length];
        linear = new Vector3[bodies.Length];
        angular = new Vector3[bodies.Length];
        foreach (Rigidbody body in bodies)
        {
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 12;
            body.solverVelocityIterations = 4;
            body.maxDepenetrationVelocity = 3f;
        }

        Track(0f);
    }

    private void LateUpdate()
    {
        if (!IsLimp)
        {
            Track(Time.deltaTime);
        }
    }

    // Velocity of each bone from its motion since the last frame (the animation's own speed).
    private void Track(float dt)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            Transform t = bodies[i].transform;
            if (dt > 1e-5f)
            {
                linear[i] = (t.position - lastPositions[i]) / dt;
                Quaternion delta = t.rotation * Quaternion.Inverse(lastRotations[i]);
                delta.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f)
                {
                    angle -= 360f;
                }

                angular[i] = float.IsNaN(axis.x) ? Vector3.zero : axis * (angle * Mathf.Deg2Rad / dt);
            }

            lastPositions[i] = t.position;
            lastRotations[i] = t.rotation;
        }
    }

    // Goes limp, keeping the animation's motion, then the bullet pushes the body it struck.
    public void GoLimp(Rigidbody struck, Vector3 point, Vector3 impulse)
    {
        if (IsLimp)
        {
            return;
        }

        IsLimp = true;
        if (animator != null)
        {
            animator.enabled = false;
        }

        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // Clamped: a single fast frame of the animation must not fling a limb.
            body.linearVelocity = Vector3.ClampMagnitude(linear[i], 6f);
            body.angularVelocity = Vector3.ClampMagnitude(angular[i], 12f);
        }

        if (struck != null)
        {
            struck.AddForceAtPosition(impulse, point, ForceMode.Impulse);
        }
    }

    // A later shot into the corpse still moves it.
    public void Push(Rigidbody struck, Vector3 point, Vector3 impulse)
    {
        if (!IsLimp || struck == null)
        {
            return;
        }

        if (IsSettled)
        {
            Wake();
        }

        struck.AddForceAtPosition(impulse, point, ForceMode.Impulse);
    }

    private void Wake()
    {
        IsSettled = false;
        restingFor = 0f;
        foreach (Rigidbody body in bodies)
        {
            body.isKinematic = false;
            body.WakeUp();
        }
    }

    private void FixedUpdate()
    {
        if (!IsLimp || IsSettled)
        {
            return;
        }

        float fastest = 0f;
        foreach (Rigidbody body in bodies)
        {
            fastest = Mathf.Max(fastest, body.linearVelocity.magnitude);
        }

        restingFor = fastest < 0.05f ? restingFor + Time.fixedDeltaTime : 0f;
        if (restingFor >= settleSeconds)
        {
            IsSettled = true;
            foreach (Rigidbody body in bodies)
            {
                body.isKinematic = true;
            }
        }
    }
}
