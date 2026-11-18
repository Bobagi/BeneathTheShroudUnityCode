using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The scripted chase of Dayse's scene 4 (no AI, as the GDD asks): a creature comes through the broken wall
// and follows the player's own footsteps (a trail recorded while running, so it goes through the same
// doors), low along the floor. It never enters a FungusZone that is safe: at the edge of the glowing
// fungus it rears, screams and goes back out the way it came. Reaching the player is Caught (the director
// shows the death card). Speed in GameTuning.
// The body is the Drowned (`body`, its Animator; Gustavo 03/10: the eel had to go): it screams in the hole, runs
// on all fours along the trail ("running_crawl", played at the rate that matches the chase speed, so the hands
// do not skate), screams again at the fungus edge and runs back. Shot dead on the way (CreatureVitals), the chase
// ends there and its ragdoll stays. Without `body`, the old chain of Meshy pieces (`segments`) is used.
// While it moves, a wet crawl loops at its head (SoundLibrary.creatureCrawlLoop), louder the faster it goes.
// Torch held up into its eyes (TorchStun): it stops dead and screams for GameTuning.torchStunSeconds, then runs on
// (immune for torchStunImmunity seconds after).
public sealed class CreatureChase : MonoBehaviour, ITorchStunnable
{
    [SerializeField]
    private Animator body;

    [SerializeField]
    private Transform[] segments;

    [SerializeField]
    private float segmentSpacing = 0.55f;

    [SerializeField]
    private Transform player;

    [SerializeField]
    private Vector3 hole;

    [SerializeField]
    private AudioSource voice;

    [SerializeField]
    private AudioClip roar;

    [SerializeField]
    private AudioClip screech;

    [SerializeField]
    private DreadDirector dread;

    private const float CatchDistance = 1.05f;
    private const float StopShortOfSafety = 1.6f;
    private const float TrailStep = 0.25f;

    private readonly List<Vector3> trail = new List<Vector3>();
    private Vector3[] chain;
    private AudioSource crawl;
    private CreatureVitals vitals;
    // How fast "running_crawl" covers ground at rate 1, measured from the clip (03/10): a planted hand sweeps back
    // about 0.69 m in 0.31 s. The clip's own averageSpeed is useless here (the root barely moves, even backwards).
    private const float CrawlClipSpeed = 2.2f;
    private string playing;

    private const string RunClip = "running_crawl";
    private const string ScreamClip = "zombie_scream";

    public bool Running { get; private set; }

    private float stunUntil;
    private float immuneUntil;

    public Transform Root => body != null ? body.transform : transform;
    public Transform Eyes => body != null ? body.GetBoneTransform(HumanBodyBones.Head) : null;
    public bool Threatening => Running && body != null && !Dead();
    public bool Stunnable => Running && body != null && body.gameObject.activeInHierarchy && Time.time > immuneUntil && !Dead();

    public void Stun(float seconds)
    {
        stunUntil = Time.time + seconds;
        immuneUntil = stunUntil + GameTuning.Instance.torchStunImmunity;
        if (screech != null && voice != null)
        {
            voice.PlayOneShot(screech, 1f);
        }
    }

    private void OnEnable()
    {
        TorchStun.Register(this);
    }

    private void OnDisable()
    {
        TorchStun.Unregister(this);
    }

    public event System.Action Caught;
    public event System.Action Repelled;

    private void Awake()
    {
        if (body != null)
        {
            vitals = body.GetComponent<CreatureVitals>();
            body.applyRootMotion = false;
        }

        SetVisible(false);
        AudioClip crawlClip = SoundLibrary.Instance.creatureCrawlLoop;
        if (crawlClip != null && voice != null)
        {
            // Same place and falloff as the voice (Place moves the voice to the head).
            crawl = voice.gameObject.AddComponent<AudioSource>();
            crawl.clip = crawlClip;
            crawl.loop = true;
            crawl.playOnAwake = false;
            crawl.spatialBlend = voice.spatialBlend;
            crawl.minDistance = voice.minDistance;
            crawl.maxDistance = voice.maxDistance;
            crawl.rolloffMode = voice.rolloffMode;
            crawl.outputAudioMixerGroup = voice.outputAudioMixerGroup;
        }
    }

    private void Crawl(float speed)
    {
        if (crawl == null)
        {
            return;
        }

        float level = Mathf.Clamp01(speed / Mathf.Max(0.1f, GameTuning.Instance.chaseSpeed));
        crawl.volume = level;
        crawl.pitch = Mathf.Lerp(0.85f, 1.1f, level);
        if (level > 0.02f && !crawl.isPlaying)
        {
            crawl.time = Random.Range(0f, crawl.clip.length);
            crawl.Play();
        }
        else if (level <= 0.02f && crawl.isPlaying)
        {
            crawl.Stop();
        }
    }

    public void Begin()
    {
        // Killed in an earlier try: nothing left to come through the wall.
        if (vitals != null && vitals.IsDead)
        {
            return;
        }

        if (!Running)
        {
            StartCoroutine(Chase());
        }
    }

    public void Stop()
    {
        StopAllCoroutines();
        Running = false;
        Crawl(0f);
        SetVisible(false);
    }

    private IEnumerator Chase()
    {
        Running = true;
        trail.Clear();
        trail.Add(hole);
        chain = new Vector3[Mathf.Max(2, segments != null ? segments.Length : 0)];
        Vector3 outward = (hole - player.position);
        outward.y = 0f;
        outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.forward;
        for (int i = 0; i < chain.Length; i++)
        {
            chain[i] = hole + outward * segmentSpacing * i;
        }

        SetVisible(true);
        Place(Vector3.zero);
        voice.pitch = 0.9f;
        voice.PlayOneShot(roar, 1f);
        if (dread != null)
        {
            dread.Provoke(1f);
        }

        // It pours through the hole first; the player has this long to start running.
        if (body != null)
        {
            Face(-outward);
            Animate(ScreamClip, 0.1f, 1f);
            yield return new WaitForSeconds(1.4f);
        }
        else
        {
            yield return new WaitForSeconds(0.9f);
        }

        float speed = 0f;
        int next = 1;
        Vector3 head = hole;
        while (true)
        {
            RecordTrail();
            GameTuning tuning = GameTuning.Instance;
            if (Time.time < stunUntil)
            {
                // Blinded: stops where it is and screams; the chase picks up from a standstill.
                speed = 0f;
                Crawl(0f);
                Animate(ScreamClip, 0.12f, 1f);
                yield return null;
                continue;
            }

            speed = Mathf.MoveTowards(speed, tuning.chaseSpeed, Time.deltaTime * 4f);
            Vector3 feet = player.position;
            bool safe = FungusZone.IsSafe(feet + Vector3.up * 0.5f);
            if (next < trail.Count)
            {
                Vector3 target = trail[next];
                if (safe && Vector3.Distance(target, feet) < StopShortOfSafety + 1.5f)
                {
                    // Too close to the light: it will not follow.
                    yield return Repel(head);
                    yield break;
                }

                head = Vector3.MoveTowards(head, target, speed * Time.deltaTime);
                if (Vector3.Distance(head, target) < 0.05f)
                {
                    next++;
                }
            }
            else if (safe)
            {
                yield return Repel(head);
                yield break;
            }

            if (Dead())
            {
                yield break;
            }

            Place(head);
            Crawl(speed);
            Run(speed);
            if (Vector3.Distance(Flat(head), Flat(feet)) < CatchDistance && !safe)
            {
                Running = false;
                Crawl(0f);
                Caught?.Invoke();
                yield return new WaitForSeconds(0.4f);
                SetVisible(false);
                yield break;
            }

            yield return null;
        }
    }

    private void RecordTrail()
    {
        Vector3 feet = player.position;
        if (trail.Count == 0 || Vector3.Distance(Flat(trail[trail.Count - 1]), Flat(feet)) > TrailStep)
        {
            trail.Add(feet);
        }
    }

    // At the edge of the fungus light: it rears, screams, and slides back out through the hole.
    private IEnumerator Repel(Vector3 head)
    {
        Crawl(0f);
        voice.pitch = 1f;
        voice.PlayOneShot(screech != null ? screech : roar, 1f);
        if (body != null)
        {
            // Up on its knees at the edge of the light, screaming at it.
            Animate(ScreamClip, 0.2f, 1f);
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                if (Dead())
                {
                    yield break;
                }

                yield return null;
            }
        }
        else
        {
            Vector3 reared = head + Vector3.up * 0.9f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
            {
                Place(Vector3.Lerp(head, reared, Mathf.Sin(t * Mathf.PI)));
                yield return null;
            }
        }

        // Back the way it came, faster.
        List<Vector3> back = new List<Vector3>(trail);
        back.Reverse();
        Vector3 at = head;
        foreach (Vector3 point in back)
        {
            while (Vector3.Distance(at, point) > 0.05f)
            {
                if (Dead())
                {
                    yield break;
                }

                at = Vector3.MoveTowards(at, point, GameTuning.Instance.chaseSpeed * 1.6f * Time.deltaTime);
                Place(at);
                Crawl(GameTuning.Instance.chaseSpeed);
                Run(GameTuning.Instance.chaseSpeed * 1.6f);
                yield return null;
            }
        }

        Crawl(0f);
        SetVisible(false);
        Running = false;
        Repelled?.Invoke();
    }

    // Shot dead during the chase: the ragdoll has the body now; the chase is over, as if it had been driven off.
    private bool Dead()
    {
        if (vitals == null || !vitals.IsDead)
        {
            return false;
        }

        Crawl(0f);
        Running = false;
        Repelled?.Invoke();
        return true;
    }

    // The Drowned on all fours: the run cycle sped up or slowed down to the speed it is carried at.
    private void Run(float speed)
    {
        if (body == null)
        {
            return;
        }

        Animate(RunClip, 0.25f, Mathf.Clamp(speed / CrawlClipSpeed, 0.3f, 2f));
    }

    private void Animate(string state, float fade, float rate)
    {
        body.speed = rate;
        if (playing != state)
        {
            playing = state;
            body.CrossFadeInFixedTime(state, fade);
        }
    }

    private void Face(Vector3 direction)
    {
        direction.y = 0f;
        if (body != null && direction.sqrMagnitude > 1e-4f)
        {
            body.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    // Puts the chain on the floor: the head low and ahead, the rest trailing behind it. With the Drowned, the
    // point is where it stands and the body turns, smoothly, to where it is going.
    private void Place(Vector3 head)
    {
        if (body != null)
        {
            Vector3 at = head == Vector3.zero ? hole : head;
            Vector3 ground = OnFloor(at) - Vector3.up * 0.45f;
            Vector3 step = ground - body.transform.position;
            step.y = 0f;
            if (step.sqrMagnitude > 1e-6f && head != Vector3.zero)
            {
                Quaternion want = Quaternion.LookRotation(step.normalized, Vector3.up);
                body.transform.rotation = Quaternion.RotateTowards(body.transform.rotation, want, 360f * Time.deltaTime);
            }

            body.transform.position = ground;
            voice.transform.position = ground + Vector3.up * 0.5f;
            return;
        }

        if (head != Vector3.zero)
        {
            chain[0] = OnFloor(head);
        }

        for (int i = 1; i < chain.Length; i++)
        {
            Vector3 offset = chain[i] - chain[i - 1];
            if (offset.sqrMagnitude < 0.0001f)
            {
                offset = Vector3.back * 0.01f;
            }

            chain[i] = chain[i - 1] + offset.normalized * segmentSpacing;
            chain[i].y = Mathf.Lerp(chain[i].y, chain[0].y - 0.05f * i, 0.5f);
        }

        for (int i = 0; i < segments.Length; i++)
        {
            Vector3 ahead = i == 0 ? chain[0] - chain[1] : chain[i - 1] - chain[i];
            if (ahead.sqrMagnitude < 0.0001f)
            {
                ahead = Vector3.forward;
            }

            segments[i].SetPositionAndRotation(chain[i], Quaternion.LookRotation(ahead, Vector3.up));
        }

        voice.transform.position = chain[0];
    }

    private static readonly RaycastHit[] Hits = new RaycastHit[12];

    // The highest floor under the point (the creature's own colliders and the player do not count).
    private Vector3 OnFloor(Vector3 point)
    {
        int count = Physics.RaycastNonAlloc(new Vector3(point.x, point.y + 1.2f, point.z), Vector3.down, Hits, 3f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            Transform t = Hits[i].transform;
            if ((body != null && t.IsChildOf(body.transform)) || t.IsChildOf(player))
            {
                continue;
            }

            best = Mathf.Max(best, Hits[i].point.y);
        }

        return float.IsNegativeInfinity(best) ? point : new Vector3(point.x, best + 0.45f, point.z);
    }

    private static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }

    private void SetVisible(bool visible)
    {
        if (body != null)
        {
            // Dead, it stays where it fell.
            if (visible || vitals == null || !vitals.IsDead)
            {
                body.gameObject.SetActive(visible);
            }

            playing = null;
            return;
        }

        foreach (Transform segment in segments)
        {
            segment.gameObject.SetActive(visible);
        }
    }
}
