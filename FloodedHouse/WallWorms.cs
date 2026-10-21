using UnityEngine;

// Worms crossing a wall and sinking into the floor (Dayse's scene 3): a few thin dark bodies (line
// renderers) wriggling along a wall plane, now and then, each crawling down and disappearing at the
// skirting. Their wet squelching plays as a 3D loop while they are out (fading with the distance; GameTuning
// wormsVolume), and scratching behind the wall comes now and then. Cheap: a handful of points per worm, only
// while the wall is near the player.
public sealed class WallWorms : MonoBehaviour
{
    [SerializeField]
    private LineRenderer[] worms;

    [Tooltip("Wall plane: right along the wall, up the wall, size in metres (width, height).")]
    [SerializeField]
    private Vector3 right = Vector3.right;

    [SerializeField]
    private Vector2 size = new Vector2(3f, 2.4f);

    [SerializeField]
    private Transform player;

    [SerializeField]
    private AudioSource source;

    [SerializeField]
    private float activeDistance = 9f;

    private const int Points = 10;
    private Vector2[] heads;
    private float[] speeds;
    private float[] phases;
    private Vector2[][] bodies;
    private float scratchTimer = 3f;
    private AudioSource squelch;

    private void Start()
    {
        AudioClip wet = SoundLibrary.Instance.wormsLoop;
        if (wet != null)
        {
            squelch = gameObject.AddComponent<AudioSource>();
            squelch.clip = wet;
            squelch.loop = true;
            squelch.spatialBlend = 1f;
            squelch.rolloffMode = AudioRolloffMode.Linear;
            squelch.minDistance = 1f;
            squelch.maxDistance = activeDistance;
            squelch.volume = 0f;
            squelch.time = Random.value * wet.length;
            squelch.Play();
        }

        heads = new Vector2[worms.Length];
        speeds = new float[worms.Length];
        phases = new float[worms.Length];
        bodies = new Vector2[worms.Length][];
        for (int i = 0; i < worms.Length; i++)
        {
            worms[i].positionCount = Points;
            bodies[i] = new Vector2[Points];
            Respawn(i);
        }
    }

    private void Respawn(int i)
    {
        heads[i] = new Vector2(Random.Range(0.1f, size.x - 0.1f), Random.Range(size.y * 0.5f, size.y));
        speeds[i] = Random.Range(0.06f, 0.16f);
        phases[i] = Random.value * 10f;
        for (int k = 0; k < Points; k++)
        {
            bodies[i][k] = heads[i] + Vector2.up * 0.025f * k;
        }
    }

    private void Update()
    {
        bool near = Vector3.Distance(player.position, transform.position) < activeDistance;
        for (int i = 0; i < worms.Length; i++)
        {
            worms[i].enabled = near;
        }

        if (squelch != null)
        {
            squelch.volume = Mathf.MoveTowards(squelch.volume, near ? GameTuning.Instance.wormsVolume : 0f, Time.deltaTime * 0.8f);
        }

        if (!near)
        {
            return;
        }

        Vector3 up = Vector3.up;
        Vector3 normal = Vector3.Cross(right, up).normalized;
        float dt = Time.deltaTime;
        for (int i = 0; i < worms.Length; i++)
        {
            phases[i] += dt * 6f;
            Vector2 dir = new Vector2(Mathf.Sin(phases[i] * 0.4f) * 0.7f, -1f).normalized;
            heads[i] += dir * speeds[i] * dt;
            heads[i].x = Mathf.Clamp(heads[i].x, 0.05f, size.x - 0.05f);
            if (heads[i].y < -0.3f)
            {
                // Gone into the floor; another one comes later, elsewhere.
                Respawn(i);
            }

            bodies[i][0] = heads[i];
            for (int k = 1; k < Points; k++)
            {
                Vector2 offset = bodies[i][k] - bodies[i][k - 1];
                bodies[i][k] = bodies[i][k - 1] + (offset.sqrMagnitude > 0f ? offset.normalized : Vector2.up) * 0.022f;
            }

            for (int k = 0; k < Points; k++)
            {
                Vector2 p = bodies[i][k];
                float wiggle = Mathf.Sin(phases[i] + k * 0.9f) * 0.006f;
                // Below the skirting the body is inside the floor: hide it there.
                float y = Mathf.Max(p.y, 0f);
                worms[i].SetPosition(k, transform.position + right * (p.x + wiggle) + up * y + normal * 0.012f);
            }
        }

        scratchTimer -= dt;
        if (scratchTimer <= 0f && source != null)
        {
            scratchTimer = Random.Range(6f, 15f);
            source.pitch = Random.Range(0.8f, 1.2f);
            source.PlayOneShot(SoundLibrary.Instance.Claws(), Random.Range(0.5f, 0.9f));
        }
    }
}
