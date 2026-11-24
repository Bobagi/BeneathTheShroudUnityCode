using System.Collections.Generic;
using UnityEngine;

// A creature the torch can blind (Gustavo, 07/10: "iluminar os olhos das criaturas, fazendo elas tomarem um stun").
public interface ITorchStunnable
{
    // The creature's body: it has to face the light (dot of its forward with the way to the lamp).
    Transform Root { get; }

    // Where its eyes are (the head bone).
    Transform Eyes { get; }

    // Awake, not dead, not already stunned or in its after-stun immunity.
    bool Stunnable { get; }

    // Alive and a danger right now (the player's fear reads it: Immersion).
    bool Threatening { get; }

    void Stun(float seconds);
}

// The torch held up and aimed (right mouse, TorchHold.Focus) into a creature's eyes for GameTuning.torchStunExposure
// seconds stuns it for torchStunSeconds: the eyes inside the focused cone (torchStunAngle), within torchStunRange,
// in plain sight of the lamp (nothing between) and the creature facing the lamp. Looking away or losing the aim lets
// the exposure drain at the same rate. Creatures register themselves (OnEnable/OnDisable); no Find, no allocation.
public static class TorchStun
{
    private static readonly List<ITorchStunnable> Targets = new List<ITorchStunnable>();
    private static readonly Dictionary<ITorchStunnable, float> Exposure = new Dictionary<ITorchStunnable, float>();
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    public static void Register(ITorchStunnable target)
    {
        if (!Targets.Contains(target))
        {
            Targets.Add(target);
            Exposure[target] = 0f;
        }
    }

    public static void Unregister(ITorchStunnable target)
    {
        Targets.Remove(target);
        Exposure.Remove(target);
    }

    // How close the most exposed creature is to being stunned (0 to 1), for feedback.
    public static float Strongest { get; private set; }

    // A creature was just stunned by the torch (achievements listen).
    public static event System.Action AnyStunned;

    // The registered creatures (also what the player is afraid of: Immersion).
    public static int Count => Targets.Count;

    public static ITorchStunnable Get(int index)
    {
        return Targets[index];
    }

    // Called every frame by the torch. lit: the beam is on and held up (focus).
    public static void Shine(Transform lamp, bool lit, Transform playerRoot, float dt)
    {
        GameTuning tuning = GameTuning.Instance;
        float need = Mathf.Max(0.05f, tuning.torchStunExposure);
        float strongest = 0f;
        for (int i = 0; i < Targets.Count; i++)
        {
            ITorchStunnable target = Targets[i];
            float exposure = Exposure[target];
            bool inLight = lit && target.Stunnable && InBeam(lamp, target, playerRoot, tuning);
            exposure = Mathf.Max(0f, exposure + (inLight ? dt : -dt));
            if (exposure >= need)
            {
                target.Stun(tuning.torchStunSeconds);
                exposure = 0f;
                AnyStunned?.Invoke();
            }

            Exposure[target] = exposure;
            strongest = Mathf.Max(strongest, exposure / need);
        }

        Strongest = strongest;
    }

    private static bool InBeam(Transform lamp, ITorchStunnable target, Transform playerRoot, GameTuning tuning)
    {
        Transform eyes = target.Eyes;
        if (eyes == null)
        {
            return false;
        }

        Vector3 to = eyes.position - lamp.position;
        float distance = to.magnitude;
        if (distance > tuning.torchStunRange || distance < 0.05f)
        {
            return false;
        }

        if (Vector3.Angle(lamp.forward, to) > tuning.torchStunAngle)
        {
            return false;
        }

        Vector3 facing = target.Root.forward;
        facing.y = 0f;
        Vector3 back = -to;
        back.y = 0f;
        if (Vector3.Dot(facing.normalized, back.normalized) < 0.25f)
        {
            return false;
        }

        // Nothing but the creature itself (or the player) between the lamp and the eyes.
        int count = Physics.RaycastNonAlloc(lamp.position, to / distance, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = Hits[i].transform;
            if (!hit.IsChildOf(target.Root) && (playerRoot == null || !hit.IsChildOf(playerRoot)))
            {
                return false;
            }
        }

        return true;
    }
}
