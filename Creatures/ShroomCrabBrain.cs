using UnityEngine;

// The creature of the egg (GDD, "Modelos da Dayse no Meshy": the green arachnid with mushrooms on its back, the adult
// of the embryo in the red fungus's eggs). It lies closed by its nest, a lump among the silk. When the player comes
// near in plain sight (or shoots it) it unfolds and scuttles at them, rears and strikes with the front pair. It
// guards the nest: past its leash, or once the player is on another floor, it goes back and closes again. It does
// not walk into the light of the blue fungus (FungusZone), like the other creatures; the torch in its eyes makes it
// close up for a while (TorchStun); the magnum kills it (GameTuning.crabShotsToKill).
// Every motion is a clip of its Animator (ShroomCrab.controller: Curled, Unfold, Idle, Walk, Attack, Hit, Death, made
// in Blender by SourceAssets/Tools/shroom_crab_rig.py); this only picks the state and moves the body over the floor.
// The Walk state's speed follows the ground speed so the feet do not slide (clip speed from the rig report).
public sealed class ShroomCrabBrain : MonoBehaviour, IBulletTarget, ITorchStunnable
{
    private enum State
    {
        Curled,
        Unfolding,
        Chase,
        Attack,
        Recover,
        Hit,
        Return,
        Closing,
        Stunned,
        Dead,
    }

    private static readonly int CurledState = Animator.StringToHash("Curled");
    private static readonly int UnfoldState = Animator.StringToHash("Unfold");
    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int WalkState = Animator.StringToHash("Walk");
    private static readonly int AttackState = Animator.StringToHash("Attack");
    private static readonly int HitState = Animator.StringToHash("Hit");
    private static readonly int DeathState = Animator.StringToHash("Death");
    private static readonly int WalkSpeedParameter = Animator.StringToHash("WalkSpeed");
    private static readonly RaycastHit[] Casts = new RaycastHit[12];

    private const float UnfoldSeconds = 1.1f;
    private const float AttackSeconds = 0.7f;
    private const float AttackLands = 0.3f;
    private const float HitSeconds = 0.3f;
    // After a strike it gathers itself before the next (16/11: striking back to back it killed a standing player in
    // five seconds, with no time to shoot or step away).
    private const float RecoverSeconds = 1.1f;
    private const float SenseInterval = 0.2f;

    [SerializeField]
    private Animator animator;

    [Tooltip("The body bone: where its eyes are for the torch, and where it is hit from.")]
    [SerializeField]
    private Transform eyes;

    [SerializeField]
    private FirstPersonController player;

    [SerializeField]
    private AirSupply playerHealth;

    [SerializeField]
    private AudioSource voice;

    [SerializeField]
    private AudioSource steps;

    [SerializeField]
    private Material fluidMaterial;

    [Tooltip("Ground speed of the Walk clip at scale 1, metres per second (ShroomCrab_rig.json: walk_speed_mps).")]
    [SerializeField]
    private float clipWalkSpeed = 0.51f;

    [Tooltip("Half the width of the body with its legs at scale 1, metres: what it needs free to pass.")]
    [SerializeField]
    private float bodyRadius = 0.42f;

    private State state = State.Curled;
    private float stateTime;
    private float nextSense;
    private float immuneUntil;
    private bool struck;
    private int shotsTaken;
    private int lastCry = -1;
    private Vector3 home;
    private Quaternion homeFacing;
    private ParticleSystem spray;
    private float Scale => transform.lossyScale.x;

    public Transform Root => transform;
    public Transform Eyes => eyes != null ? eyes : transform;
    public bool Stunnable => (state == State.Chase || state == State.Attack || state == State.Recover) && Time.time >= immuneUntil;
    public bool Threatening => state == State.Chase || state == State.Attack || state == State.Recover || state == State.Unfolding;
    public bool IsDead => state == State.Dead;

    public void Configure(Animator crabAnimator, Transform body, AudioSource voiceSource, AudioSource stepSource, Material fluid, float walkClipSpeed)
    {
        animator = crabAnimator;
        eyes = body;
        voice = voiceSource;
        steps = stepSource;
        fluidMaterial = fluid;
        clipWalkSpeed = walkClipSpeed;
    }

    public void SetPlayer(FirstPersonController controller, AirSupply health)
    {
        player = controller;
        playerHealth = health;
    }

    private void OnEnable()
    {
        TorchStun.Register(this);
        home = transform.position;
        homeFacing = transform.rotation;
        Enter(State.Curled, 0f);
    }

    private void OnDisable()
    {
        TorchStun.Unregister(this);
    }

    private void Update()
    {
        if (state == State.Dead || player == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        stateTime += dt;
        GameTuning tuning = GameTuning.Instance;
        Vector3 here = transform.position;
        Vector3 target = player.transform.position;
        Vector3 toPlayer = target - here;
        float climb = Mathf.Abs(toPlayer.y);
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        float reach = (bodyRadius + 0.5f) * Scale + 0.35f;
        bool moving = false;

        switch (state)
        {
            case State.Curled:
                if (Time.time >= nextSense)
                {
                    nextSense = Time.time + SenseInterval;
                    if (climb < 1.5f && distance < tuning.crabWakeDistance && Sees(target))
                    {
                        Wake();
                    }
                }

                break;
            case State.Unfolding:
                if (stateTime >= UnfoldSeconds)
                {
                    Enter(State.Chase, 0.12f);
                }

                break;
            case State.Chase:
                if (climb > 1.8f || HorizontalDistance(here, home) > tuning.crabLeash || HorizontalDistance(target, home) > tuning.crabLeash + 3f)
                {
                    Enter(State.Return, 0.15f);
                }
                else if (distance <= reach)
                {
                    Face(toPlayer, 400f * dt);
                    struck = false;
                    Enter(State.Attack, 0.08f);
                }
                else
                {
                    moving = Move(toPlayer / Mathf.Max(distance, 0.001f), tuning.crabSpeed, dt);
                }

                break;
            case State.Attack:
                Face(toPlayer, 200f * dt);
                if (!struck && stateTime >= AttackLands)
                {
                    struck = true;
                    if (distance <= reach + 0.35f && climb < 1.2f && Vector3.Dot(transform.forward, toPlayer.normalized) > 0.3f)
                    {
                        player.Jolt(-4.5f, Random.Range(-6f, 6f));
                        if (playerHealth != null)
                        {
                            playerHealth.Damage(tuning.crabBlowDamage);
                        }
                    }
                }

                if (stateTime >= AttackSeconds)
                {
                    Enter(State.Recover, 0.15f);
                }

                break;
            case State.Recover:
                Face(toPlayer, 120f * dt);
                if (stateTime >= RecoverSeconds)
                {
                    Enter(State.Chase, 0.12f);
                }

                break;
            case State.Hit:
                if (stateTime >= HitSeconds)
                {
                    Enter(State.Chase, 0.1f);
                }

                break;
            case State.Return:
            {
                Vector3 toHome = home - here;
                toHome.y = 0f;
                if (toHome.magnitude < 0.25f)
                {
                    Enter(State.Closing, 0.4f);
                }
                else
                {
                    moving = Move(toHome.normalized, tuning.crabSpeed * 0.6f, dt);
                    // The player came back within reach of the nest: at them again.
                    if (Time.time >= nextSense)
                    {
                        nextSense = Time.time + SenseInterval;
                        if (climb < 1.5f && distance < tuning.crabWakeDistance && HorizontalDistance(target, home) < tuning.crabLeash && Sees(target))
                        {
                            Enter(State.Chase, 0.12f);
                        }
                    }
                }

                break;
            }
            case State.Closing:
                transform.rotation = Quaternion.RotateTowards(transform.rotation, homeFacing, 180f * dt);
                if (stateTime >= 0.6f)
                {
                    Enter(State.Curled, 0f);
                }

                break;
            case State.Stunned:
                if (Time.time >= immuneUntil - 2f)
                {
                    immuneUntil = Time.time + 2f;
                    Enter(State.Unfolding, 0.1f);
                }

                break;
        }

        if (steps != null)
        {
            float wanted = moving ? 0.55f : 0f;
            steps.volume = Mathf.MoveTowards(steps.volume, wanted, dt * 4f);
            if (moving && !steps.isPlaying)
            {
                steps.Play();
            }
            else if (!moving && steps.isPlaying && steps.volume <= 0.001f)
            {
                steps.Stop();
            }
        }
    }

    public void TakeShot(Shot shot)
    {
        if (spray == null)
        {
            spray = CreatureVitals.BuildSpray(new Color(0.2f, 0.26f, 0.05f), fluidMaterial);
        }

        spray.transform.SetPositionAndRotation(shot.point, Quaternion.LookRotation(-shot.direction));
        spray.Emit(18);
        SoundLibrary sounds = SoundLibrary.Instance;
        int ignored = -1;
        AudioClip thud = SoundLibrary.Pick(sounds.bulletFlesh, ref ignored);
        if (thud != null)
        {
            AudioSource.PlayClipAtPoint(thud, shot.point, 0.9f);
        }

        if (state == State.Dead)
        {
            return;
        }

        shotsTaken++;
        if (shotsTaken >= GameTuning.Instance.crabShotsToKill)
        {
            Cry(sounds.smallCreatureDeaths, 1f);
            Enter(State.Dead, 0.08f);
            if (steps != null)
            {
                steps.Stop();
            }

            return;
        }

        Cry(sounds.smallCreatureSqueals, 0.9f);
        // Shot while closed: it opens already hurt, and comes.
        Enter(state == State.Curled || state == State.Closing || state == State.Stunned ? State.Unfolding : State.Hit, 0.05f);
    }

    public void Stun(float seconds)
    {
        if (state == State.Dead)
        {
            return;
        }

        // The light hurts: it closes up where it stands, then opens again with a short immunity.
        immuneUntil = Time.time + seconds + 2f;
        Cry(SoundLibrary.Instance.smallCreatureSqueals, 0.8f);
        Enter(State.Stunned, 0.25f);
    }

    private void Wake()
    {
        Cry(SoundLibrary.Instance.smallCreatureGrunts, 0.9f);
        Enter(State.Unfolding, 0.05f);
    }

    private void Enter(State next, float blend)
    {
        state = next;
        stateTime = 0f;
        if (animator == null)
        {
            return;
        }

        int hash;
        switch (next)
        {
            case State.Curled:
            case State.Closing:
            case State.Stunned:
                hash = CurledState;
                break;
            case State.Unfolding:
                hash = UnfoldState;
                break;
            case State.Chase:
            case State.Return:
                hash = WalkState;
                break;
            case State.Attack:
                hash = AttackState;
                break;
            case State.Hit:
                hash = HitState;
                break;
            case State.Dead:
                hash = DeathState;
                break;
            default:
                hash = IdleState;
                break;
        }

        if (blend <= 0f)
        {
            animator.Play(hash, 0, 0f);
        }
        else
        {
            animator.CrossFadeInFixedTime(hash, blend, 0, 0f);
        }
    }

    // Turns toward `direction` and walks; steers round what stands in the way (the first clear heading among the
    // straight one and 40, 80 degrees to each side). Returns whether it moved.
    private bool Move(Vector3 direction, float speed, float dt)
    {
        float radius = bodyRadius * Scale;
        Vector3 origin = transform.position + Vector3.up * (radius + 0.08f);
        Vector3 heading = direction;
        bool clear = false;
        for (int i = 0; i < 5; i++)
        {
            float angle = i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f) * 40f * ((i + 1) / 2);
            Vector3 probe = Quaternion.AngleAxis(angle, Vector3.up) * direction;
            if (!Blocked(origin, probe, radius, radius + speed * 0.35f) && !FungusZone.IsSafe(transform.position + probe * (radius + 0.6f)))
            {
                heading = probe;
                clear = true;
                break;
            }
        }

        Face(heading, 300f * dt);
        if (!clear)
        {
            animator.SetFloat(WalkSpeedParameter, 0.6f);
            return false;
        }

        // Full speed only when it faces where it goes.
        float aligned = Mathf.Clamp01(Vector3.Dot(transform.forward, heading));
        float ground = speed * aligned;
        Vector3 next = transform.position + transform.forward * (ground * dt);
        if (Physics.Raycast(next + Vector3.up * 0.6f, Vector3.down, out RaycastHit floor, 1.6f, ~0, QueryTriggerInteraction.Ignore) && !floor.transform.IsChildOf(transform))
        {
            next.y = Mathf.MoveTowards(transform.position.y, floor.point.y, 3f * dt);
        }

        transform.position = next;
        animator.SetFloat(WalkSpeedParameter, Mathf.Clamp(ground / Mathf.Max(clipWalkSpeed * Scale, 0.01f), 0.5f, 6f));
        return ground > 0.05f;
    }

    private bool Blocked(Vector3 origin, Vector3 direction, float radius, float distance)
    {
        int count = Physics.SphereCastNonAlloc(origin, radius * 0.8f, direction, Casts, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = Casts[i].transform;
            if (hit.IsChildOf(transform) || (player != null && hit.IsChildOf(player.transform)))
            {
                continue;
            }

            // The floor under it is not in the way.
            if (Casts[i].normal.y > 0.7f)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool Sees(Vector3 target)
    {
        Vector3 from = Eyes.position;
        Vector3 to = target + Vector3.up * 1.2f - from;
        int count = Physics.RaycastNonAlloc(from, to.normalized, Casts, to.magnitude, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = Casts[i].transform;
            if (!hit.IsChildOf(transform) && !hit.IsChildOf(player.transform))
            {
                return false;
            }
        }

        return true;
    }

    private void Face(Vector3 direction, float degrees)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), degrees);
        }
    }

    private void Cry(AudioClip[] set, float volume)
    {
        AudioClip clip = SoundLibrary.Pick(set, ref lastCry);
        if (clip != null && voice != null)
        {
            // A smaller throat than the Drowned's.
            voice.pitch = Random.Range(1.25f, 1.5f);
            voice.PlayOneShot(clip, volume);
        }
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x, z = a.z - b.z;
        return Mathf.Sqrt(x * x + z * z);
    }
}
