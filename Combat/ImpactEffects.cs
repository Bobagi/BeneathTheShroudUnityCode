using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// What a bullet leaves behind (Gustavo, 07/10: "efeitos de sangue, decals, fumaça... o mais legal possível"). Built
// and wired by Editor/CombatEffectsSetup; textures CC0 (CREDITS.md):
//   Wall, floor, crate: a bullet hole (URP decal) turned at random, a puff of smoke that hangs and drifts up.
//   Creature: a dark mist out of both sides of the wound; what goes through spatters the surface behind it (a stain,
//   and on a wall sometimes a run of drips under it); drops land on the floor below; a kill leaves a pool spreading
//   under the body.
//   On the creature itself (Gustavo, 07/10: "decals de sangue nos inimigos nas áreas de impacto, esguicho de sangue"):
//   a spatter decal at the wound, parented to the struck bone so it rides the walk, the fall and the ragdoll; a spurt
//   of stretched droplets out of the wound that fall under gravity and leave small stains where they land (particle
//   collision); and a hit to the head or body keeps pumping for a moment (two or three weaker spurts, a pulse).
// Every decal comes from a fixed pool (oldest reused), so a long fight costs nothing new; nothing runs per frame
// except a pool that is still spreading.
public sealed class ImpactEffects : MonoBehaviour
{
    [SerializeField]
    private Material bulletHole;

    [SerializeField]
    private Material[] bloodStains = new Material[0];

    [SerializeField]
    private Material[] bloodDrips = new Material[0];

    [Tooltip("Fields of small drops (the floor under a wound, where the spurt lands).")]
    [SerializeField]
    private Material[] dropStains = new Material[0];

    [Tooltip("Soft particle material for the smoke puff off a wall (white puff, tinted grey).")]
    [SerializeField]
    private Material puffMaterial;

    [Tooltip("Soft particle material for the blood mist (tinted by the colour below).")]
    [SerializeField]
    private Material mistMaterial;

    [Tooltip("Small high-velocity spatter decals for the wound on the creature's body (BloodSplat_*).")]
    [SerializeField]
    private Material[] bodySplats = new Material[0];

    [Tooltip("Stretched-particle material for the spurt's droplets (blood_droplet, lit).")]
    [SerializeField]
    private Material dropletMaterial;

    [SerializeField]
    private Color bloodColor = new Color(0.2f, 0.05f, 0.045f, 0.85f);

    [SerializeField]
    private int bodyCount = 40;

    [Tooltip("Size of the spatter at a wound (metres).")]
    [SerializeField]
    private Vector2 bodySplatSize = new Vector2(0.18f, 0.3f);

    [SerializeField]
    private int holeCount = 48;

    [SerializeField]
    private int bloodCount = 32;

    [Tooltip("Bullet hole size (metres).")]
    [SerializeField]
    private float holeSize = 0.075f;

    private DecalProjector[] holes;
    private DecalProjector[] blood;
    private DecalProjector[] body;
    private int nextHole;
    private int nextBlood;
    private int nextBody;
    private ParticleSystem spurt;
    private float nextDropStain;
    private readonly System.Collections.Generic.List<ParticleCollisionEvent> landed = new System.Collections.Generic.List<ParticleCollisionEvent>(32);
    private ParticleSystem puff;
    private ParticleSystem mist;
    private Transform root;
    private AudioSource[] voices;
    private int nextVoice;
    private int lastImpact = -1;
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    private void Awake()
    {
        // In the world, not under the camera the gun rides on.
        root = new GameObject("ImpactEffects_World").transform;
        Root = root;
        holes = Pool("BulletHole", holeCount);
        blood = Pool("Blood", bloodCount);
        body = Pool("BodyBlood", bodyCount);
        spurt = Spurt();
        puff = Particles("ImpactPuff", puffMaterial, new Color(0.62f, 0.6f, 0.57f, 0.55f), 0.8f, 2.2f, 0.06f, 0.2f, -0.04f);
        mist = Particles("BloodMist", mistMaterial, bloodColor, 0.35f, 0.8f, 0.08f, 0.26f, 0.25f);
        voices = new AudioSource[4];
        for (int i = 0; i < voices.Length; i++)
        {
            AudioSource voice = new GameObject("ImpactSound").AddComponent<AudioSource>();
            voice.transform.SetParent(root, false);
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            voice.minDistance = 1.5f;
            voice.maxDistance = 30f;
            voice.rolloffMode = AudioRolloffMode.Logarithmic;
            voices[i] = voice;
        }
    }

    private void OnDestroy()
    {
        if (root != null)
        {
            Destroy(root.gameObject);
        }
    }

    // A bullet into anything that is not a creature.
    public void Bullet(RaycastHit hit, Vector3 direction)
    {
        // A hole on things that do not move (a hole left behind by a pushed crate would float in the air).
        if (hit.rigidbody == null && bulletHole != null)
        {
            DecalProjector hole = Take(holes, ref nextHole);
            float size = holeSize * Random.Range(0.85f, 1.2f);
            Place(hole, bulletHole, hit.point, hit.normal, new Vector3(size, size, 0.06f), Random.Range(0f, 360f));
        }

        Burst(puff, hit.point + hit.normal * 0.03f, Vector3.Lerp(hit.normal, -direction, 0.3f), Random.Range(3, 6), 0.35f);

        // The crack of the hit, where it hit.
        AudioClip clip = SoundLibrary.Pick(SoundLibrary.Instance.bulletImpacts, ref lastImpact);
        if (clip != null)
        {
            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.transform.position = hit.point;
            voice.pitch = Random.Range(0.9f, 1.1f);
            voice.PlayOneShot(clip, 0.8f);
        }
    }

    // A bullet into a creature: point and direction of the shot, the normal at the wound, the creature's root (its own
    // colliders are skipped when looking for what is behind and under it), and whether the shot killed it.
    // bone: the struck collider's transform (the spatter is parented to it); pumping: a hit to the head or body.
    public void Blood(Vector3 point, Vector3 direction, Vector3 normal, Transform creature, bool kill, Transform bone, bool pumping)
    {
        Blood(point, direction, normal, creature, kill);
        if (bone == null)
        {
            return;
        }

        // The wound: a spatter on the skin at the hit, riding the bone.
        if (bodySplats.Length > 0)
        {
            DecalProjector splat = Take(body, ref nextBody);
            float size = Random.Range(bodySplatSize.x, bodySplatSize.y);
            Place(splat, bodySplats[Random.Range(0, bodySplats.Length)], point, normal, new Vector3(size, size, 0.16f), Random.Range(0f, 360f));
            splat.transform.SetParent(bone, true);
        }

        // Out of the wound: back toward the shooter and through.
        Spray(point, Vector3.Lerp(normal, Vector3.up, 0.25f), Random.Range(14, 22), 3.2f);
        Spray(point + direction * 0.08f, Vector3.Lerp(direction, Vector3.up, 0.15f), Random.Range(10, 16), 4.5f);
        if (pumping && !kill)
        {
            StartCoroutine(Pump(bone, bone.InverseTransformPoint(point), bone.InverseTransformDirection(normal)));
        }
    }

    // A wound that keeps pumping: two or three weaker spurts, about one a heartbeat, from the same spot on the bone.
    private IEnumerator Pump(Transform bone, Vector3 localPoint, Vector3 localNormal)
    {
        int beats = Random.Range(2, 4);
        for (int i = 0; i < beats; i++)
        {
            yield return new WaitForSeconds(Random.Range(0.32f, 0.45f));
            if (bone == null)
            {
                yield break;
            }

            float weaker = 1f - i * 0.28f;
            Spray(bone.TransformPoint(localPoint), Vector3.Lerp(bone.TransformDirection(localNormal), Vector3.up, 0.2f), Mathf.RoundToInt(Random.Range(7, 11) * weaker), 2.4f * weaker);
        }
    }

    private void Spray(Vector3 point, Vector3 direction, int count, float speed)
    {
        if (spurt == null)
        {
            return;
        }

        spurt.transform.SetPositionAndRotation(point, Quaternion.LookRotation(direction.sqrMagnitude > 1e-6f ? direction : Vector3.up));
        ParticleSystem.MainModule main = spurt.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
        spurt.Emit(count);
    }

    // Droplets that land leave small stains (at most a few a second, from the shared blood pool).
    private void OnSpurtLanded(GameObject other)
    {
        int count = spurt.GetCollisionEvents(other, landed);
        for (int i = 0; i < count; i++)
        {
            if (Time.time < nextDropStain || other.GetComponentInParent<IBulletTarget>() != null)
            {
                continue;
            }

            nextDropStain = Time.time + 0.06f;
            Stain(landed[i].intersection, landed[i].normal, Random.Range(0.1f, 0.2f), dropStains);
        }
    }

    public void Blood(Vector3 point, Vector3 direction, Vector3 normal, Transform creature, bool kill)
    {
        Burst(mist, point, normal, Random.Range(4, 7), 0.6f);
        Burst(mist, point + direction * 0.1f, direction, Random.Range(5, 9), 1.4f);

        // Through and onto what is behind: closer surfaces take a tighter, darker splash.
        if (Cast(point, direction, 3f, creature, out RaycastHit behind))
        {
            float size = Mathf.Lerp(0.35f, 0.9f, behind.distance / 3f);
            Stain(behind.point, behind.normal, size);
            if (Mathf.Abs(behind.normal.y) < 0.5f && bloodDrips.Length > 0 && Random.value < 0.6f)
            {
                // Running down the wall from the splash.
                float drip = size * Random.Range(0.8f, 1.3f);
                Vector3 below = behind.point + Vector3.down * drip * 0.45f;
                DecalProjector run = Take(blood, ref nextBlood);
                Place(run, bloodDrips[Random.Range(0, bloodDrips.Length)], below, behind.normal, new Vector3(size * 0.9f, drip, 0.12f), 0f);
            }
        }

        // Drops on the floor under the wound.
        if (Cast(point, Vector3.down, 3f, creature, out RaycastHit floor))
        {
            // Scattered along the floor, never off it (a jitter in height put the decal box above or below it).
            Stain(floor.point + Vector3.ProjectOnPlane(Random.insideUnitSphere * 0.15f, floor.normal), floor.normal, Random.Range(0.3f, 0.5f), dropStains);
        }

        if (kill && creature != null)
        {
            StartCoroutine(Pool(creature));
        }
    }

    private void Stain(Vector3 point, Vector3 normal, float size, Material[] set = null)
    {
        set = set != null && set.Length > 0 ? set : bloodStains;
        if (set.Length == 0)
        {
            return;
        }

        DecalProjector stain = Take(blood, ref nextBlood);
        Place(stain, set[Random.Range(0, set.Length)], point, normal, new Vector3(size, size, 0.12f), Random.Range(0f, 360f));
    }

    // The pool under a dead body: starts small where the chest comes to rest and spreads for a few seconds.
    private IEnumerator Pool(Transform creature)
    {
        yield return new WaitForSeconds(1.2f);
        if (creature == null || bloodStains.Length == 0)
        {
            yield break;
        }

        Animator animator = creature.GetComponentInChildren<Animator>();
        Transform chest = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Chest) : creature;
        if (!Cast(chest.position + Vector3.up * 0.3f, Vector3.down, 2.5f, creature, out RaycastHit floor))
        {
            yield break;
        }

        DecalProjector pool = Take(blood, ref nextBlood);
        Material material = bloodStains[Random.Range(0, bloodStains.Length)];
        float angle = Random.Range(0f, 360f);
        const float Seconds = 5f;
        for (float t = 0f; t < Seconds && pool != null; t += Time.deltaTime)
        {
            float size = Mathf.Lerp(0.3f, 1.3f, 1f - (1f - t / Seconds) * (1f - t / Seconds));
            Place(pool, material, floor.point, floor.normal, new Vector3(size, size, 0.15f), angle);
            yield return null;
        }
    }

    private bool Cast(Vector3 from, Vector3 direction, float distance, Transform skip, out RaycastHit best)
    {
        best = default;
        float nearest = float.MaxValue;
        int count = Physics.RaycastNonAlloc(from, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform t = Hits[i].transform;
            if (Hits[i].distance < nearest && Hits[i].rigidbody == null && (skip == null || !t.IsChildOf(skip)) && t.GetComponentInParent<FirstPersonController>() == null)
            {
                nearest = Hits[i].distance;
                best = Hits[i];
            }
        }

        return nearest < float.MaxValue;
    }

    private static void Place(DecalProjector decal, Material material, Vector3 point, Vector3 normal, Vector3 size, float roll)
    {
        // Back in the world (a body decal may have been riding a bone, maybe of a creature now gone).
        if (decal.transform.parent != Root)
        {
            decal.transform.SetParent(Root, true);
        }

        // The projector looks into the surface (its +Z) with its box centred on it (centred in front of it, the surface
        // sat on the far edge of the box and was cut away: no hole showed, 07/10).
        decal.transform.SetPositionAndRotation(point, Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, roll));
        decal.material = material;
        decal.size = size;
        decal.fadeFactor = 1f;
        decal.enabled = true;
    }

    private static DecalProjector Take(DecalProjector[] pool, ref int next)
    {
        DecalProjector decal = pool[next];
        next = (next + 1) % pool.Length;
        return decal;
    }

    private static Transform Root;

    // The spurt: stretched droplets (long along their speed), heavy, short-lived, colliding with the world.
    private ParticleSystem Spurt()
    {
        GameObject go = new GameObject("BloodSpurt");
        go.transform.SetParent(root, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.022f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 1f), new Color(0.75f, 0.7f, 0.7f, 1f));
        main.gravityModifier = 1.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 0.015f;
        ParticleSystem.CollisionModule collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.lifetimeLoss = 1f;
        collision.bounce = 0f;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.sendCollisionMessages = true;
        collision.maxCollisionShapes = 64;
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f));
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = dropletMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.045f;
        renderer.lengthScale = 1.2f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<SpurtLanding>().Owner = this;
        return ps;
    }

    // Forwards the spurt's particle collisions (Unity sends them to the particle system's own GameObject).
    private sealed class SpurtLanding : MonoBehaviour
    {
        public ImpactEffects Owner;

        private void OnParticleCollision(GameObject other)
        {
            if (Owner != null)
            {
                Owner.OnSpurtLanded(other);
            }
        }
    }

    private DecalProjector[] Pool(string name, int count)
    {
        DecalProjector[] pool = new DecalProjector[Mathf.Max(1, count)];
        for (int i = 0; i < pool.Length; i++)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root, false);
            DecalProjector decal = go.AddComponent<DecalProjector>();
            decal.pivot = Vector3.zero;
            decal.drawDistance = 40f;
            decal.startAngleFade = 180f;
            decal.endAngleFade = 180f;
            decal.enabled = false;
            pool[i] = decal;
        }

        return pool;
    }

    private static void Burst(ParticleSystem ps, Vector3 point, Vector3 direction, int count, float speed)
    {
        if (ps == null)
        {
            return;
        }

        ps.transform.SetPositionAndRotation(point, Quaternion.LookRotation(direction.sqrMagnitude > 1e-6f ? direction : Vector3.up));
        ParticleSystem.MainModule main = ps.main;
        main.startSpeedMultiplier = speed;
        ps.Emit(count);
    }

    // Soft, lit, slow particles (smoke or mist): size grows, alpha falls.
    private ParticleSystem Particles(string name, Material material, Color color, float minLife, float maxLife, float minSize, float maxSize, float gravity)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.02f;
        ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 3f;
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.8f));
        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }
}
