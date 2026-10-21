using UnityEngine;

// Health and the body's answer to bullets, shared by creatures that can be killed. Each shot: damage scaled by
// the body region hit, a kick to the struck bone (HitReactor), the wet thud of the round going in, a spray of
// the creature's dark fluid out of the wound, and a cry. At zero health the body goes limp (Ragdoll) carrying
// its own motion plus the bullet's push. Hurt/Died tell the creature's brain, which picks the animation.
public sealed class CreatureVitals : MonoBehaviour
{
    [SerializeField]
    private float maxHealth = 100f;

    [SerializeField]
    private Ragdoll ragdoll;

    [SerializeField]
    private HitReactor reactor;

    [SerializeField]
    private AudioSource voice;

    [Tooltip("The creature's fluid: dark, nearly black red, with a faint green cast (sea and rot).")]
    [SerializeField]
    private Color fluid = new Color(0.16f, 0.05f, 0.04f);

    [Tooltip("Soft round particle material (Art/VFX/Combat/WoundFluid); the colour above tints it.")]
    [SerializeField]
    private Material fluidMaterial;

    private float health;
    private int lastCry = -1;
    private ParticleSystem spray;

    public float Health => health;
    public bool IsDead { get; private set; }

    // Region hit, the shot, and whether it killed.
    public event System.Action<CreatureHitbox, Shot, bool> Hurt;

    // Any creature killed by a shot, with the hitbox of the killing round (achievements and stats listen).
    public static event System.Action<CreatureVitals, CreatureHitbox> AnyKilled;

    public void Configure(Ragdoll body, HitReactor hitReactor, AudioSource source, Material fluidParticles)
    {
        fluidMaterial = fluidParticles;
        ragdoll = body;
        reactor = hitReactor;
        voice = source;
    }

    private void Awake()
    {
        health = maxHealth;
    }

    public void TakeShot(CreatureHitbox hitbox, Shot shot)
    {
        Splash(shot);
        SoundLibrary sounds = SoundLibrary.Instance;
        int ignored = -1;
        AudioClip thud = SoundLibrary.Pick(sounds.bulletFlesh, ref ignored);
        if (thud != null)
        {
            AudioSource.PlayClipAtPoint(thud, shot.point, 0.9f);
        }

        if (IsDead)
        {
            ragdoll.Push(hitbox.body, shot.point, shot.direction * shot.impulse);
            return;
        }

        health -= shot.damage * hitbox.DamageScale;
        bool fatal = health <= 0f;
        if (fatal)
        {
            IsDead = true;
            reactor.Clear();
            // A slight lift: a round in the chest knocks the body back and off its feet, not into the floor.
            Vector3 push = (shot.direction + Vector3.up * 0.15f).normalized * shot.impulse;
            ragdoll.GoLimp(hitbox.body, shot.point, push);
            Cry(sounds.smallCreatureDeaths, 1f);
        }
        else
        {
            reactor.React(hitbox.transform, shot.point, shot.direction, shot.impulse);
            Cry(sounds.smallCreatureSqueals, 0.85f);
        }

        Hurt?.Invoke(hitbox, shot, fatal);
        if (fatal)
        {
            AnyKilled?.Invoke(this, hitbox);
        }
    }

    private void Cry(AudioClip[] set, float volume)
    {
        AudioClip clip = SoundLibrary.Pick(set, ref lastCry);
        if (clip != null && voice != null)
        {
            voice.pitch = Random.Range(0.85f, 1.05f);
            voice.PlayOneShot(clip, volume);
        }
    }

    // A short burst at the wound: thick drops thrown back toward the shooter and out the far side.
    private void Splash(Shot shot)
    {
        if (spray == null)
        {
            spray = BuildSpray(fluid, fluidMaterial);
        }

        spray.transform.SetPositionAndRotation(shot.point, Quaternion.LookRotation(-shot.direction));
        spray.Emit(14);
        spray.transform.rotation = Quaternion.LookRotation(shot.direction);
        spray.Emit(22);
    }

    // Also used by the tentacled creatures (TentacleRig).
    internal static ParticleSystem BuildSpray(Color color, Material material)
    {
        GameObject go = new GameObject("WoundSpray");
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.03f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, color * 1.6f);
        main.gravityModifier = 1.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.01f;
        ParticleSystem.CollisionModule collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.dampen = 0.9f;
        collision.bounce = 0.05f;
        collision.lifetimeLoss = 0.4f;
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.6f));
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }
}
