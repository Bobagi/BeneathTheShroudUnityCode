using UnityEngine;

// The Drowned: a person the sea took and gave back changed (GDD). Small enough to be killed by the revolver.
// Plays its Mixamo clips straight from code (Animator.CrossFade on the states DrownedSetup builds, one per clip).
//   Idle until the player comes within notice range, then shambles at them (the walk's root motion moves it,
//   so the feet do not skate; it turns toward the player and steers round what is in the way).
//   Close enough: one of three attacks (swipe, punch, headbutt); the player's view is knocked at the blow.
//   Shot, not killed (CreatureVitals.Hurt): every hit already kicks the struck bone (HitReactor); a hit to the
//   head or body also plays a flinch, or a stagger back if it was a hard one or the second in a row; arms only
//   take the kick so the walk carries on; two hits to the legs bring it down and from then on it crawls. The
//   fall is "zombie_dying" cut where it lands face down (measured: 82% of the clip), so the crawl, also face down,
//   follows without the body turning over in the air ("stumble_fall" ends on its side: the blend lifted the legs).
//   Root motion is collided: a capsule cast slides it along walls and crates, and the ground under it may rise
//   only a step (it used to snap up onto anything below 0.6 m and walk through the rest).
//   Down and in reach (from the head, which lies ahead of the root), it bites at the legs ("zombie_biting_2",
//   face down like the crawl) instead of crawling on under the player.
//   Killed: the ragdoll takes over (CreatureVitals) and this stops.
//   In the house (17/10): it notices the player only with a clear line from its head to the player's eyes within
//   GameTuning.drownedNoticeRange, or within drownedHearRange through anything (steps behind a door); its blows take
//   health (AirSupply.Damage, GameTuning.drownedBlowDamage / BiteDamage). Scripted use (the demo's finale): Patrol()
//   walks a list of points pausing at each to look round, with the chase off until Alert() (it screams and comes) or
//   until the player is within reach; Calm() puts it back where it started, idle (a retry after a death).
//   Torch in the eyes (TorchStun, the torch held up with the right mouse): stunned for a few seconds. Standing, it
//   rears back screaming ("zombie_scream") and then stands dazed; down, it freezes in its crawl. Afterwards it
//   cannot be stunned again for GameTuning.torchStunImmunity seconds, so the light buys time but cannot pin it.
[RequireComponent(typeof(Animator))]
public sealed class DrownedBrain : MonoBehaviour, ITorchStunnable
{
    [SerializeField]
    private CreatureVitals vitals;

    [SerializeField]
    private AudioSource voice;

    [SerializeField]
    private float noticeRange = 14f;

    [SerializeField]
    private float attackRange = 1.35f;

    [SerializeField]
    private float turnSpeed = 140f;

    private enum State { Idle, Chase, Attack, Flinch, Falling, Crawl, CrawlBite, Stunned, Dead, Scripted }

    private static readonly string[] Attacks = { "zombie_attack", "punch", "headbutt" };
    private const string Collapse = "zombie_dying";
    private const float CollapseLanded = 0.82f;
    private const float StepHeight = 0.3f;
    private const string CrawlBiteClip = "zombie_biting_2";
    private const float CrawlReach = 0.6f;
    private static readonly RaycastHit[] Casts = new RaycastHit[12];

    private Animator animator;
    private FirstPersonController player;
    private State state = State.Idle;
    private string playing;
    private float stateTime;
    private float lastHurt = -10f;
    private int legHits;
    private bool blowLanded;
    private float nextGrunt;
    private int lastGrunt = -1;
    private int attackIndex;
    private float stunUntil;
    private float immuneUntil;
    private bool stunnedDown;
    private int lastSqueal = -1;
    private AirSupply playerHealth;
    // Scripted search (Patrol): the points, where it is in them, and how long it still stands at the current one.
    private Vector3[] patrol = new Vector3[0];
    private int patrolIndex;
    private float patrolPause;
    private float patrolWait;
    private bool patrolWalking;
    private bool patrolLoop;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    public Transform Root => transform;
    public Transform Eyes => animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
    public bool Threatening => state != State.Dead && state != State.Idle && isActiveAndEnabled;
    public bool IsDead => state == State.Dead;
    public bool IsChasing => state == State.Chase || state == State.Attack || state == State.Flinch || state == State.Crawl || state == State.CrawlBite;
    public bool IsDown => state == State.Crawl || state == State.CrawlBite || state == State.Falling;
    public bool Stunnable => state != State.Dead && state != State.Falling && state != State.Stunned && Time.time > immuneUntil;

    private void OnEnable()
    {
        TorchStun.Register(this);
    }

    private void OnDisable()
    {
        TorchStun.Unregister(this);
    }

    public void Stun(float seconds)
    {
        if (!Stunnable)
        {
            return;
        }

        stunnedDown = state == State.Crawl || state == State.CrawlBite;
        state = State.Stunned;
        stateTime = 0f;
        stunUntil = Time.time + seconds;
        immuneUntil = stunUntil + GameTuning.Instance.torchStunImmunity;
        if (stunnedDown)
        {
            // Frozen in the crawl, barely twitching.
            animator.speed = 0.12f;
        }
        else
        {
            Play("zombie_scream", 0.12f);
        }

        AudioClip squeal = SoundLibrary.Pick(SoundLibrary.Instance.smallCreatureSqueals, ref lastSqueal);
        if (squeal != null && voice != null)
        {
            voice.pitch = Random.Range(0.9f, 1.05f);
            voice.PlayOneShot(squeal, 1f);
        }
    }

    public void Configure(CreatureVitals creatureVitals, AudioSource source)
    {
        vitals = creatureVitals;
        voice = source;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = true;
        vitals.Hurt += OnHurt;
    }

    private void Start()
    {
        player = FindAnyObjectByType<FirstPersonController>();
        playerHealth = player != null ? player.GetComponent<AirSupply>() : null;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        if (state != State.Idle)
        {
            // Already told what to do before its first frame (Patrol, CrawlAfter): keep it.
            return;
        }

        Play("zombie_idle", 0.2f);
        // Not all in step: each starts its idle at a different moment.
        animator.Play("zombie_idle", 0, Random.value);
        nextGrunt = Time.time + Random.Range(1f, 4f);
    }

    private void Update()
    {
        if (state == State.Dead || player == null)
        {
            return;
        }

        stateTime += Time.deltaTime;
        Vector3 to = player.transform.position - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        bool clipDone = info.IsName(playing) && info.normalizedTime >= 0.92f && !animator.IsInTransition(0);

        switch (state)
        {
            case State.Idle:
                if (Notices(distance))
                {
                    Enter(State.Chase);
                }

                break;
            case State.Scripted:
                UpdatePatrol(distance);
                break;
            case State.Chase:
                Face(to, 1f);
                if (distance < attackRange)
                {
                    Enter(State.Attack);
                }

                break;
            case State.Crawl:
                Face(to, 0.6f);
                if (HeadDistance() < CrawlReach)
                {
                    Enter(State.CrawlBite);
                }

                break;
            case State.CrawlBite:
                Face(to, 0.25f);
                if (!blowLanded && info.IsName(playing) && info.normalizedTime > 0.45f)
                {
                    blowLanded = true;
                    if (HeadDistance() < CrawlReach + 0.3f)
                    {
                        player.Jolt(-3f, Random.Range(-4f, 4f));
                        playerHealth?.Damage(GameTuning.Instance.drownedBiteDamage);
                    }
                }

                if (clipDone)
                {
                    Enter(HeadDistance() < CrawlReach ? State.CrawlBite : State.Crawl);
                }

                break;
            case State.Attack:
                Face(to, 0.35f);
                // The blow lands around the middle of each attack clip.
                if (!blowLanded && info.IsName(playing) && info.normalizedTime > 0.42f)
                {
                    blowLanded = true;
                    if (distance < attackRange + 0.35f)
                    {
                        player.Jolt(-6f, Random.Range(-7f, 7f));
                        playerHealth?.Damage(GameTuning.Instance.drownedBlowDamage);
                    }
                }

                if (clipDone)
                {
                    Enter(distance < attackRange ? State.Attack : State.Chase);
                }

                break;
            case State.Flinch:
                if (clipDone)
                {
                    Enter(State.Chase);
                }

                break;
            case State.Stunned:
                if (!stunnedDown && clipDone && playing != "zombie_idle")
                {
                    // The scream is over: dazed on its feet until the light wears off.
                    Play("zombie_idle", 0.3f);
                }

                if (Time.time >= stunUntil)
                {
                    animator.speed = 1f;
                    Enter(stunnedDown ? State.Crawl : State.Chase);
                }

                break;
            case State.Falling:
                if (info.IsName(Collapse) && info.normalizedTime >= CollapseLanded && !animator.IsInTransition(0))
                {
                    Enter(State.Crawl);
                }

                break;
        }

        if ((state == State.Chase || state == State.Crawl) && Time.time > nextGrunt)
        {
            nextGrunt = Time.time + Random.Range(2.5f, 6f);
            AudioClip grunt = SoundLibrary.Pick(SoundLibrary.Instance.smallCreatureGrunts, ref lastGrunt);
            if (grunt != null && voice != null)
            {
                voice.pitch = Random.Range(0.8f, 1f);
                voice.PlayOneShot(grunt, 0.8f);
            }
        }
    }

    private void Enter(State next)
    {
        state = next;
        stateTime = 0f;
        switch (next)
        {
            case State.Chase:
                Play("zombie_walk", 0.3f);
                break;
            case State.Crawl:
                Play("zombie_crawl", 0.5f);
                break;
            case State.CrawlBite:
                blowLanded = false;
                Play(CrawlBiteClip, 0.25f);
                break;
            case State.Attack:
                blowLanded = false;
                attackIndex = (attackIndex + 1 + Random.Range(0, 2)) % Attacks.Length;
                Play(Attacks[attackIndex], 0.15f);
                break;
        }
    }

    // Sees the player: a clear line from its head to the player's eyes within the notice range (GameTuning), or the
    // player so close it hears them whatever is between. The range in the Inspector is the older, blind one and only
    // caps the tuned value.
    private bool Notices(float distance)
    {
        GameTuning tuning = GameTuning.Instance;
        if (distance < tuning.drownedHearRange)
        {
            return true;
        }

        if (distance > Mathf.Min(noticeRange, tuning.drownedNoticeRange))
        {
            return false;
        }

        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Vector3 from = head != null ? head.position : transform.position + Vector3.up * 1.6f;
        Vector3 to = player.EyePosition - from;
        int count = Physics.RaycastNonAlloc(from, to.normalized, Casts, to.magnitude, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = Casts[i].transform;
            if (hit.IsChildOf(transform) || hit == player.transform || hit.IsChildOf(player.transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    // Scripted search (the demo's finale): shambles from point to point, stops `pauseSeconds` at each looking round,
    // and does not chase on its own unless the player is within reach of its hands; Alert() sets it on the player.
    public void Patrol(Vector3[] points, float pauseSeconds, bool loop)
    {
        patrol = points ?? new Vector3[0];
        patrolIndex = 0;
        patrolPause = pauseSeconds;
        patrolWait = 0f;
        patrolLoop = loop;
        patrolWalking = false;
        legHits = 0;
        state = State.Scripted;
        stateTime = 0f;
        if (animator != null)
        {
            animator.speed = 1f;
            Play("zombie_idle", 0.2f);
        }
    }

    private void UpdatePatrol(float distanceToPlayer)
    {
        // Point blank: it finds the player whatever the script says.
        if (distanceToPlayer < attackRange + 0.2f)
        {
            Alert();
            return;
        }

        if (patrol.Length == 0)
        {
            return;
        }

        Vector3 target = patrol[Mathf.Clamp(patrolIndex, 0, patrol.Length - 1)];
        Vector3 to = target - transform.position;
        to.y = 0f;
        if (patrolWalking)
        {
            animator.speed = GameTuning.Instance.finaleSearchSpeed;
            Face(to, 0.8f);
            if (to.magnitude < 0.35f)
            {
                patrolWalking = false;
                patrolWait = patrolPause;
                animator.speed = 1f;
                Play("zombie_idle", 0.4f);
            }
        }
        else
        {
            patrolWait -= Time.deltaTime;
            if (patrolWait <= 0f)
            {
                if (patrolIndex >= patrol.Length - 1 && !patrolLoop)
                {
                    return;
                }

                patrolIndex = (patrolIndex + 1) % patrol.Length;
                patrolWalking = true;
                Play("zombie_walk", 0.4f);
            }
        }
    }

    // Found the player (scripted or not): screams and comes.
    public void Alert()
    {
        if (state == State.Dead || state == State.Chase || state == State.Attack)
        {
            return;
        }

        animator.speed = 1f;
        Enter(State.Chase);
        AudioClip squeal = SoundLibrary.Pick(SoundLibrary.Instance.smallCreatureSqueals, ref lastSqueal);
        if (squeal != null && voice != null)
        {
            voice.pitch = Random.Range(0.85f, 1f);
            voice.PlayOneShot(squeal, 1f);
        }
    }

    // Comes crawling after the player from where it is (the stair grab of the finale): down from the start, biting
    // when in reach.
    public void CrawlAfter()
    {
        if (state == State.Dead)
        {
            return;
        }

        animator.speed = 1f;
        legHits = 2;
        Enter(State.Crawl);
    }

    // Kicked away (the finale's stair grab): goes limp if still alive, slides to `into` (the water) and is switched
    // off after `seconds`.
    public void Dismiss(Vector3 into, float seconds)
    {
        if (state != State.Dead)
        {
            state = State.Dead;
            enabled = false;
            Ragdoll ragdoll = GetComponent<Ragdoll>();
            if (ragdoll != null)
            {
                Vector3 push = (into - transform.position).normalized * 3f;
                ragdoll.GoLimp(null, transform.position, Vector3.zero);
                foreach (Rigidbody body in ragdoll.Bodies)
                {
                    body.AddForce(push, ForceMode.VelocityChange);
                }
            }
        }

        StartCoroutine(SinkAway(into, seconds));
    }

    private System.Collections.IEnumerator SinkAway(Vector3 into, float seconds)
    {
        yield return new WaitForSeconds(seconds * 0.6f);
        Ragdoll ragdoll = GetComponent<Ragdoll>();
        for (float t = 0f; t < seconds * 0.4f; t += Time.deltaTime)
        {
            if (ragdoll != null)
            {
                foreach (Rigidbody body in ragdoll.Bodies)
                {
                    body.AddForce(Vector3.down * 25f, ForceMode.Acceleration);
                }
            }

            yield return null;
        }

        gameObject.SetActive(false);
    }

    // Back to idle where it started (a retry after the player's death): a standing one stands, a downed one crawls
    // on from its spot.
    public void Calm()
    {
        if (state == State.Dead)
        {
            return;
        }

        animator.speed = 1f;
        bool down = IsDown;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        state = down ? State.Crawl : State.Idle;
        stateTime = 0f;
        Play(down ? "zombie_crawl" : "zombie_idle", 0.3f);
    }

    // Lying down the head is well ahead of the root: reach is measured from it, on the ground plane.
    private float HeadDistance()
    {
        Vector3 head = animator.GetBoneTransform(HumanBodyBones.Head).position;
        Vector3 to = player.transform.position - head;
        to.y = 0f;
        return to.magnitude;
    }

    private void Play(string clip, float blend)
    {
        playing = clip;
        animator.CrossFadeInFixedTime(clip, blend, 0, 0f);
    }

    private void Face(Vector3 to, float rate)
    {
        if (to.sqrMagnitude < 0.01f)
        {
            return;
        }

        Vector3 want = Steer(to.normalized);
        Quaternion target = Quaternion.LookRotation(want, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * rate * Time.deltaTime);
    }

    // Around obstacles: if the way ahead is blocked within a metre, try a little left and right.
    private Vector3 Steer(Vector3 want)
    {
        Vector3 chest = transform.position + Vector3.up * 0.9f;
        if (!Blocked(chest, want))
        {
            return want;
        }

        for (int a = 25; a <= 100; a += 25)
        {
            foreach (int sign in new[] { 1, -1 })
            {
                Vector3 dir = Quaternion.Euler(0f, a * sign, 0f) * want;
                if (!Blocked(chest, dir))
                {
                    return dir;
                }
            }
        }

        return want;
    }

    private bool Blocked(Vector3 from, Vector3 dir)
    {
        int count = Physics.SphereCastNonAlloc(from, 0.25f, dir, Casts, 1f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (!Casts[i].transform.IsChildOf(transform) && Casts[i].transform != player.transform)
            {
                return true;
            }
        }

        return false;
    }

    // The clip's travel this frame, kept out of walls: the body's capsule (low while down) is swept along it; a hit
    // turns the move into a slide along the surface, a second hit stops it. Things it already overlaps (distance 0)
    // do not hold it, so it can always move out.
    private Vector3 Collide(Vector3 delta)
    {
        bool low = state == State.Crawl || state == State.CrawlBite || state == State.Falling;
        float radius = low ? 0.2f : 0.25f;
        Vector3 bottom = transform.position + Vector3.up * (StepHeight + radius);
        Vector3 top = low ? bottom + Vector3.up * 0.05f : transform.position + Vector3.up * 1.45f;
        for (int pass = 0; pass < 2; pass++)
        {
            float distance = delta.magnitude;
            if (distance < 1e-5f || !Cast(bottom, top, radius, delta / distance, distance + 0.03f, out Vector3 normal))
            {
                return delta;
            }

            normal.y = 0f;
            delta = pass == 0 && normal.sqrMagnitude > 1e-6f ? Vector3.ProjectOnPlane(delta, normal.normalized) : Vector3.zero;
        }

        return delta;
    }

    private bool Cast(Vector3 bottom, Vector3 top, float radius, Vector3 dir, float distance, out Vector3 normal)
    {
        int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, dir, Casts, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (Casts[i].distance > 0f && !Casts[i].transform.IsChildOf(transform))
            {
                normal = Casts[i].normal;
                return true;
            }
        }

        normal = Vector3.zero;
        return false;
    }

    // Root motion: the clip's own travel, kept on the ground; the turn is ours (rotation is baked in the clips).
    private void OnAnimatorMove()
    {
        if (state == State.Dead)
        {
            return;
        }

        Vector3 delta = animator.deltaPosition;
        delta.y = 0f;
        Vector3 next = transform.position + Collide(delta);
        // The ground: from a step above the feet down (higher things are walls, the cast above keeps it off them).
        if (Physics.Raycast(next + Vector3.up * StepHeight, Vector3.down, out RaycastHit ground, StepHeight + 1f, ~0, QueryTriggerInteraction.Ignore)
            && !ground.transform.IsChildOf(transform))
        {
            next.y = ground.point.y;
        }

        transform.position = next;
    }

    private void OnHurt(CreatureHitbox hitbox, Shot shot, bool fatal)
    {
        // A shot wakes it from the stun (and the crawl it was frozen in plays on).
        if (state == State.Stunned)
        {
            animator.speed = 1f;
            Enter(stunnedDown ? State.Crawl : State.Chase);
        }

        if (fatal)
        {
            state = State.Dead;
            enabled = false;
            return;
        }

        if (state == State.Idle || state == State.Scripted)
        {
            animator.speed = 1f;
            Enter(State.Chase);
        }

        bool again = Time.time - lastHurt < 1.2f;
        lastHurt = Time.time;
        switch (hitbox.region)
        {
            case BodyRegion.Leg:
                legHits++;
                if (legHits >= 2 && state != State.Falling && state != State.Crawl && state != State.CrawlBite)
                {
                    state = State.Falling;
                    stateTime = 0f;
                    Play(Collapse, 0.12f);
                }

                break;
            case BodyRegion.Head:
            case BodyRegion.Torso:
                if (state == State.Chase || state == State.Attack || state == State.Flinch)
                {
                    state = State.Flinch;
                    stateTime = 0f;
                    Play(again || shot.impulse > 6f ? "hit_stagger" : "hit_flinch", 0.08f);
                }

                break;
        }
    }
}
