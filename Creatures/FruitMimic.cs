using UnityEngine;

// "A criatura disfarçada de fruta" (GDD; Gustavo, 15/11, seeing Dayse's closed embryo: "uma manga... a gente pode
// deixar uma na bancada da casa, o cara chega perto e pensa que ainda tem frutas aqui, o bicho abre as pernas e sai
// andando"). A young of the egg's creature lies closed among the fruit, belly down, looking like a green mango.
// When the player comes close while looking at it, it opens its legs and runs: along the counter, down to the floor
// and away to a hole, where it is gone. Harmless; a shot kills it where it is.
// The closed shape is Dayse's embryo model; the one that runs is the adult's rigged model at the young's size (the
// same species), swapped in at the moment it stirs. Its motion is the Animator's clips (Curled, Unfold, Walk,
// Death); this only picks the state and carries it along the path.
public sealed class FruitMimic : MonoBehaviour, IBulletTarget
{
    private static readonly int CurledState = Animator.StringToHash("Curled");
    private static readonly int UnfoldState = Animator.StringToHash("Unfold");
    private static readonly int WalkState = Animator.StringToHash("Walk");
    private static readonly int DeathState = Animator.StringToHash("Death");
    private static readonly int WalkSpeedParameter = Animator.StringToHash("WalkSpeed");
    private static readonly RaycastHit[] Casts = new RaycastHit[8];

    private const float UnfoldSeconds = 1.1f;
    // The young unfolds faster than the adult.
    private const float UnfoldRate = 1.6f;

    [Tooltip("The closed embryo (Dayse's model), shown until it stirs.")]
    [SerializeField]
    private GameObject closed;

    [Tooltip("The rigged young's Animator (on the model inside its root).")]
    [SerializeField]
    private Animator runner;

    [Tooltip("The young's root: hidden until it stirs, and what is carried along the path.")]
    [SerializeField]
    private GameObject runnerRoot;

    [SerializeField]
    private FirstPersonController player;

    [SerializeField]
    private AudioSource voice;

    [Tooltip("Where it runs, in world space: along the counter, the floor below, the hole it vanishes into.")]
    [SerializeField]
    private Vector3[] path = new Vector3[0];

    [Tooltip("Ground speed of the Walk clip at scale 1, metres per second.")]
    [SerializeField]
    private float clipWalkSpeed = 0.51f;

    private enum State
    {
        Waiting,
        Unfolding,
        Running,
        Gone,
        Dead,
    }

    private State state = State.Waiting;
    private float stateTime;
    private float nextSense;
    private int leg;
    private float legProgress;
    private Vector3 legStart;
    private Transform view;
    // The middle of the closed shape: what the eyes have to be on.
    private Vector3 aim;

    public bool HasRun => state == State.Running || state == State.Gone;

    public void Configure(GameObject closedShape, GameObject youngRoot, Animator young, FirstPersonController controller, AudioSource source, Vector3[] route, float walkClipSpeed)
    {
        closed = closedShape;
        runnerRoot = youngRoot;
        runner = young;
        player = controller;
        voice = source;
        path = route;
        clipWalkSpeed = walkClipSpeed;
    }

    private void Start()
    {
        if (runnerRoot != null)
        {
            runnerRoot.SetActive(false);
        }

        if (player != null)
        {
            view = player.CameraPivot != null ? player.CameraPivot : player.transform;
        }

        Renderer shape = closed != null ? closed.GetComponentInChildren<Renderer>() : null;
        aim = shape != null ? shape.bounds.center : transform.position;
    }

    private void Update()
    {
        if (state == State.Gone || state == State.Dead || runner == null || runnerRoot == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        stateTime += dt;
        switch (state)
        {
            case State.Waiting:
                if (view != null && Time.time >= nextSense)
                {
                    nextSense = Time.time + 0.1f;
                    Vector3 to = aim - view.position;
                    float distance = to.magnitude;
                    // Close, on screen (inside 35 degrees of where the eyes point) and nothing between.
                    if (distance < GameTuning.Instance.mimicTriggerDistance && Vector3.Dot(view.forward, to / Mathf.Max(distance, 0.001f)) > 0.82f && Seen(to, distance))
                    {
                        Stir();
                    }
                }

                break;
            case State.Unfolding:
                if (stateTime >= UnfoldSeconds / UnfoldRate)
                {
                    state = State.Running;
                    stateTime = 0f;
                    leg = 0;
                    legProgress = 0f;
                    legStart = runnerRoot.transform.position;
                    runner.speed = 1f;
                    runner.CrossFadeInFixedTime(WalkState, 0.08f, 0, 0f);
                }

                break;
            case State.Running:
                Run(dt);
                break;
        }
    }

    public void TakeShot(Shot shot)
    {
        if (state == State.Dead || state == State.Gone || runner == null || runnerRoot == null)
        {
            return;
        }

        Show();
        state = State.Dead;
        runner.speed = 1f;
        runner.CrossFadeInFixedTime(DeathState, 0.05f, 0, 0f);
        Cry(SoundLibrary.Instance.smallCreatureDeaths, 0.8f);
    }

    private void Stir()
    {
        Show();
        state = State.Unfolding;
        stateTime = 0f;
        runner.speed = UnfoldRate;
        runner.Play(UnfoldState, 0, 0f);
        Cry(SoundLibrary.Instance.smallCreatureSqueals, 0.7f);
    }

    private void Show()
    {
        if (closed != null)
        {
            closed.SetActive(false);
        }

        if (!runnerRoot.activeSelf)
        {
            runnerRoot.SetActive(true);
            runner.Play(CurledState, 0, 0f);
        }
    }

    // Along the path at the tuned speed; a leg of the path that drops (counter to floor) is a short jump.
    private void Run(float dt)
    {
        if (leg >= path.Length)
        {
            state = State.Gone;
            runnerRoot.SetActive(false);
            return;
        }

        Transform body = runnerRoot.transform;
        Vector3 goal = path[leg];
        float speed = GameTuning.Instance.mimicSpeed;
        float length = Mathf.Max(Vector3.Distance(legStart, goal), 0.01f);
        float drop = legStart.y - goal.y;
        legProgress = Mathf.Min(1f, legProgress + speed * dt / length);
        Vector3 at = Vector3.Lerp(legStart, goal, legProgress);
        if (drop > 0.15f)
        {
            // Off the edge: forward first, falling under gravity's curve.
            at.y = legStart.y - drop * legProgress * legProgress + Mathf.Sin(legProgress * Mathf.PI) * 0.06f;
        }

        Vector3 heading = goal - legStart;
        heading.y = 0f;
        if (heading.sqrMagnitude > 0.0001f)
        {
            body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(heading, Vector3.up), 900f * dt);
        }

        body.position = at;
        runner.SetFloat(WalkSpeedParameter, Mathf.Clamp(speed / Mathf.Max(clipWalkSpeed * body.lossyScale.x, 0.01f), 0.5f, 5f));
        if (legProgress >= 1f)
        {
            leg++;
            legProgress = 0f;
            legStart = goal;
        }
    }

    private bool Seen(Vector3 to, float distance)
    {
        int count = Physics.RaycastNonAlloc(view.position, to / distance, Casts, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = Casts[i].transform;
            // What it lies on or against (the board under it, the basket: within a hand of it) does not hide it
            // (16/11: the edge of the cutting board's box stood in the ray and it never stirred).
            if (distance - Casts[i].distance < 0.3f)
            {
                continue;
            }

            if (!hit.IsChildOf(transform) && !hit.IsChildOf(player.transform))
            {
                return false;
            }
        }

        return true;
    }

    private void Cry(AudioClip[] set, float volume)
    {
        int last = -1;
        AudioClip clip = SoundLibrary.Pick(set, ref last);
        if (clip != null && voice != null)
        {
            // A small animal: the squeals of the small creatures, an octave up.
            voice.pitch = Random.Range(1.8f, 2.1f);
            voice.PlayOneShot(clip, volume);
        }
    }
}
