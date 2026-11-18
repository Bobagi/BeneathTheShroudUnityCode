using System;
using UnityEngine;

// Moves bone chains with no animation clips: a loose tentacle, or every arm of the colossal octopus (rigged by
// the game-3d-assets skill, rig_tentacles.py: a chain of bones along each arm's centre line).
//   Alive: a wave runs from the base of each chain to its tip, bending each bone toward the creature's "up" side
//   (curl) and sideways (sway), more toward the tip; each chain has its own phase so the arms never move in step.
//   Values in GameTuning ("Tentáculos").
//   Shot (TentacleHitbox): it cannot be killed (GDD: the colossal ones are unbeatable). The arm that took the round
//   is thrown back, away from the bullet, from the struck bone outward, on a slow damped spring, and curls in toward
//   the body in a pain reflex that fades over about a second and a half; the other arms curl a little too (all of
//   them, hard, for a shot to the mantle). The dark fluid sprays from the wound and it roars (at most every 4 s).
// Bend axes are measured on the rest pose: each bone's direction to the next joint, crossed with the creature's
// up (for the octopus, the normal of the plane its arms lie in), so the same numbers bend every chain alike
// whatever roll the bones came with.
[DefaultExecutionOrder(20)]
public sealed class TentacleRig : MonoBehaviour
{
    [Serializable]
    public sealed class Chain
    {
        public string name;
        public Transform[] bones;
    }

    [SerializeField]
    private Chain[] chains = new Chain[0];

    [Tooltip("The creature's up in its own space: the side the arms curl toward (the octopus' plane normal).")]
    [SerializeField]
    private Vector3 localUp = Vector3.up;

    [SerializeField]
    private AudioSource voice;

    [SerializeField]
    private AudioClip[] hurtRoars = new AudioClip[0];

    [SerializeField]
    private Material fluidMaterial;

    [SerializeField]
    private Color fluid = new Color(0.12f, 0.05f, 0.07f);

    // A colossus: a slow, wide spring (a revolver round throws a 7 m arm, it does not snap it).
    private const float Stiffness = 30f;
    private const float Damping = 7f;
    private const float ReflexSeconds = 1.4f;
    private const float RoarCooldown = 4f;
    private const float FlinchShare = 0.25f;

    private Quaternion[][] rest;
    private Vector3[][] curlAxis;
    private Vector3[][] swayAxis;
    private float[] phase;
    // Per chain: spring angle and speed about the curl and sway axes, and the bone it starts from.
    private Vector2[] recoil;
    private Vector2[] recoilSpeed;
    private int[] recoilFrom;
    // Pain reflex per chain, 1 just after a hit, fading: the arm curls in toward the body.
    private float[] reflex;
    private float nextRoar;
    private int lastRoar = -1;
    private ParticleSystem spray;

    public int ChainCount => chains.Length;

    public void Configure(Chain[] list, Vector3 up, AudioSource source, AudioClip[] roars, Material fluidParticles)
    {
        chains = list;
        localUp = up;
        voice = source;
        hurtRoars = roars;
        fluidMaterial = fluidParticles;
    }

    private void Awake()
    {
        int n = chains.Length;
        rest = new Quaternion[n][];
        curlAxis = new Vector3[n][];
        swayAxis = new Vector3[n][];
        phase = new float[n];
        recoil = new Vector2[n];
        recoilSpeed = new Vector2[n];
        recoilFrom = new int[n];
        reflex = new float[n];
        Vector3 up = transform.TransformDirection(localUp).normalized;
        for (int c = 0; c < n; c++)
        {
            Transform[] bones = chains[c].bones;
            rest[c] = new Quaternion[bones.Length];
            curlAxis[c] = new Vector3[bones.Length];
            swayAxis[c] = new Vector3[bones.Length];
            phase[c] = c * 2.399f;
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                rest[c][i] = bone.localRotation;
                Vector3 along = i + 1 < bones.Length ? bones[i + 1].position - bone.position
                    : (i > 0 ? bone.position - bones[i - 1].position : bone.up);
                Vector3 curl = Vector3.Cross(along, up);
                if (curl.sqrMagnitude < 1e-8f)
                {
                    curl = Vector3.Cross(along, transform.forward);
                }

                curl.Normalize();
                Vector3 sway = Vector3.Cross(curl, along).normalized;
                curlAxis[c][i] = bone.InverseTransformDirection(curl);
                swayAxis[c][i] = bone.InverseTransformDirection(sway);
            }
        }
    }

    private void LateUpdate()
    {
        GameTuning tuning = GameTuning.Instance;
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        float time = Time.time * tuning.tentacleWaveSpeed;
        for (int c = 0; c < chains.Length; c++)
        {
            // Spring back from a hit.
            recoilSpeed[c] += (-Stiffness * recoil[c] - Damping * recoilSpeed[c]) * dt;
            recoil[c] += recoilSpeed[c] * dt;
            reflex[c] = Mathf.MoveTowards(reflex[c], 0f, dt / ReflexSeconds);
            float pain = Mathf.SmoothStep(0f, 1f, reflex[c]) * tuning.tentacleFlinch;

            Transform[] bones = chains[c].bones;
            int count = bones.Length;
            // The recoil angle is the whole arm's bend, shared by the bones it moves (not added to each).
            float share = 1f / Mathf.Max(1, count - recoilFrom[c]);
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 1f;
                float reach = t * t * 0.7f + t * 0.3f;
                float wave = Mathf.Sin(time - i * 0.55f + phase[c]);
                float sideWave = Mathf.Sin(time * 0.73f - i * 0.4f + phase[c] * 1.7f);
                float curl = (tuning.tentacleCurl + tuning.tentacleWave * wave + pain) * reach;
                float sway = tuning.tentacleWave * 0.6f * sideWave * reach;
                if (i >= recoilFrom[c])
                {
                    curl += recoil[c].x * share;
                    sway += recoil[c].y * share;
                }

                bones[i].localRotation = rest[c][i]
                    * Quaternion.AngleAxis(curl, curlAxis[c][i])
                    * Quaternion.AngleAxis(sway, swayAxis[c][i]);
            }
        }
    }

    // A bullet struck bone `bone` of chain `chain`; chain -1 is the mantle (the whole body flinches).
    public void Hit(int chain, int bone, Shot shot)
    {
        Vector3 push = shot.direction * shot.impulse * GameTuning.Instance.tentacleRecoil;
        float flinch = push.magnitude;
        if (chain >= 0 && chain < chains.Length)
        {
            Transform struck = chains[chain].bones[bone];
            // Kick the spring in the bone's own bend axes, the way the bullet pushes: turning about an axis a
            // moves the bone toward a x along, so each axis takes the push's share along that motion.
            Vector3 curlWorld = struck.TransformDirection(curlAxis[chain][bone]);
            Vector3 swayWorld = struck.TransformDirection(swayAxis[chain][bone]);
            Vector3 along = Vector3.Cross(swayWorld, curlWorld);
            Vector2 kick = new Vector2(Vector3.Dot(push, Vector3.Cross(curlWorld, along)), Vector3.Dot(push, Vector3.Cross(swayWorld, along)));
            recoilFrom[chain] = Mathf.Max(0, bone - 1);
            recoilSpeed[chain] += kick * 6f;
            reflex[chain] = 1f;
        }
        else
        {
            flinch /= FlinchShare;
        }

        // The other arms flinch: curled in toward the body's up side.
        for (int c = 0; c < chains.Length; c++)
        {
            if (c != chain)
            {
                recoilFrom[c] = 0;
                recoilSpeed[c] += new Vector2(flinch * 6f * FlinchShare, 0f);
                reflex[c] = Mathf.Max(reflex[c], chain < 0 ? 0.8f : 0.35f);
            }
        }

        Splash(shot);
        if (Time.time >= nextRoar && voice != null)
        {
            AudioClip roar = SoundLibrary.Pick(hurtRoars, ref lastRoar);
            if (roar != null)
            {
                nextRoar = Time.time + RoarCooldown;
                voice.pitch = UnityEngine.Random.Range(0.85f, 1f);
                voice.PlayOneShot(roar, 1f);
            }
        }
    }

    private void Splash(Shot shot)
    {
        int ignored = -1;
        AudioClip thud = SoundLibrary.Pick(SoundLibrary.Instance.bulletFlesh, ref ignored);
        if (thud != null)
        {
            AudioSource.PlayClipAtPoint(thud, shot.point, 0.9f);
        }

        if (spray == null)
        {
            spray = CreatureVitals.BuildSpray(fluid, fluidMaterial);
        }

        spray.transform.SetPositionAndRotation(shot.point, Quaternion.LookRotation(-shot.direction));
        spray.Emit(16);
        spray.transform.rotation = Quaternion.LookRotation(shot.direction);
        spray.Emit(20);
    }
}
