using UnityEngine;

// A colony of anemone worms (GDD gallery, Dayse 11/10): 8 to 32 glassy tubes standing out of the floor, the rest of
// each worm buried. "Sentem a aproximação pela vibração do solo": when the player walks near, they pull down into the
// ground at once and come back up slowly once it is quiet; whoever then stands right over them gets bitten from below
// ("se dispersam e atacam"). The tubes sway on their own (Horror/AnemoneTube); this only moves the colony up and down.
public sealed class AnemoneColony : MonoBehaviour
{
    [SerializeField]
    private Transform tubes;

    [SerializeField]
    private Transform player;

    [SerializeField]
    private float feelRadius = 3.2f;

    [SerializeField]
    private float biteRadius = 0.9f;

    private FirstPersonController controller;
    private AudioSource source;
    private Vector3 lastPlayer;
    private float height = 1f;
    private float stillTime;
    private float biteCooldown;

    public void Configure(Transform tubeMesh, Transform playerTransform)
    {
        tubes = tubeMesh;
        player = playerTransform;
    }

    private void Start()
    {
        if (player != null)
        {
            controller = player.GetComponent<FirstPersonController>();
            lastPlayer = player.position;
        }

        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.minDistance = 1f;
        source.maxDistance = 10f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.playOnAwake = false;
    }

    private void Update()
    {
        if (player == null || tubes == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        Vector3 p = player.position;
        float speed = dt > 0f ? Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(lastPlayer.x, 0f, lastPlayer.z)) / dt : 0f;
        lastPlayer = p;
        Vector3 here = transform.position;
        float distance = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(here.x, here.z));
        bool sameFloor = Mathf.Abs(p.y - here.y) < 1.5f;
        bool felt = sameFloor && distance < feelRadius && speed > 0.4f;
        float target = felt ? 0.08f : 1f;
        if (felt && height > 0.6f)
        {
            Play(SoundLibrary.Instance.clawsInWall, 0.5f);
        }

        // Down in a quarter of a second, up again over three.
        height = Mathf.MoveTowards(height, target, dt * (target < height ? 4f : 0.33f));
        tubes.localScale = new Vector3(1f, height, 1f);

        biteCooldown -= dt;
        stillTime = sameFloor && distance < biteRadius ? stillTime + dt : 0f;
        if (stillTime > 0.6f && biteCooldown <= 0f && controller != null)
        {
            biteCooldown = 2.5f;
            controller.Jolt(4f, Random.Range(-6f, 6f));
            Play(SoundLibrary.Instance.smallCreatureSqueals, 0.8f);
        }
    }

    private void Play(AudioClip[] clips, float volume)
    {
        if (clips != null && clips.Length > 0 && !source.isPlaying)
        {
            source.pitch = Random.Range(1.1f, 1.4f);
            source.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
        }
    }
}
