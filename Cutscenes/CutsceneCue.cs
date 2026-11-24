using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// One clip of a cutscene's cue track (CutsceneCueTrack, bound to the scene's CutsceneDirector). What it does
// depends on its kind, and it lasts as long as the clip:
//   Caption    a text card (the "presents" line, the game's title, or a subtitle at the bottom), fading in and
//              out at the clip's ends and moving slowly while it shows (CutsceneDirector.SetCaption).
//   Fade       the screen to black (from -> to over the clip).
//   Lightning  a strike at the clip's start (close = thunder at once and loud).
//   Animate    the actor's Animator crossfades to a state at the clip's start.
//   Move       the target is carried along a CutscenePath over the clip (eased), facing its way, optionally kept
//              on the ground under it (walking up the island).
//   Reach      the actor's right hand goes to `target` (ActorReach) over the first `reachShare` of the clip and
//              holds; the head turns to it too (ActorLook). Lets go at the clip's end.
//   FirstPerson the actor's head turns to shadow only (ActorFirstPerson), for the camera that goes into his eyes.
// Captions, fades and moves also run while scrubbing the Timeline window in the editor; lightning and animator
// changes only in Play.
[Serializable]
public sealed class CutsceneCue : PlayableAsset, ITimelineClipAsset
{
    public enum Kind { Caption, Fade, Lightning, Animate, Move, Reach, FirstPerson }

    public enum CaptionStyle { Presents, Title, Subtitle }

    public Kind kind = Kind.Caption;

    [Header("Caption")]
    public CaptionStyle style = CaptionStyle.Subtitle;
    [TextArea]
    public string text = "";

    [Header("Fade (0 clear, 1 black)")]
    public float from;
    public float to = 1f;

    [Header("Lightning")]
    public bool close;

    [Header("Animate")]
    public ExposedReference<Animator> animator;
    public string state = "";
    public float crossfade = 0.3f;

    [Header("Move")]
    public ExposedReference<Transform> target;
    public ExposedReference<CutscenePath> path;
    [Tooltip("Ease in and out (0 = constant speed, 1 = smooth start and stop).")]
    [Range(0f, 1f)]
    public float easing = 0.5f;
    public bool faceAlong = true;
    public bool followGround;

    [Header("Reach (animator = the actor, target = the grip)")]
    [Range(0.05f, 1f)]
    public float reachShare = 0.6f;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<CutsceneCueBehaviour> playable = ScriptPlayable<CutsceneCueBehaviour>.Create(graph);
        CutsceneCueBehaviour b = playable.GetBehaviour();
        IExposedPropertyTable resolver = graph.GetResolver();
        b.cue = this;
        b.animator = animator.Resolve(resolver);
        b.target = target.Resolve(resolver);
        b.path = path.Resolve(resolver);
        b.reach = b.animator != null ? b.animator.GetComponent<ActorReach>() : null;
        return playable;
    }
}

public sealed class CutsceneCueBehaviour : PlayableBehaviour
{
    public CutsceneCue cue;
    public Animator animator;
    public Transform target;
    public CutscenePath path;
    public ActorReach reach;

    private bool started;
    private static readonly RaycastHit[] Hits = new RaycastHit[8];

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        started = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        CutsceneDirector director = playerData as CutsceneDirector;
        double duration = playable.GetDuration();
        float t = duration > 0 ? Mathf.Clamp01((float)(playable.GetTime() / duration)) : 1f;
        if (!started)
        {
            started = true;
            Begin(director);
        }

        switch (cue.kind)
        {
            case CutsceneCue.Kind.Caption:
                if (director != null)
                {
                    // Cinema cards fade slowly (1.6 s in, the title 2.2 s); subtitles quickly. Notes: Docs/Cinema/titulos.md.
                    float edgeIn = cue.style == CutsceneCue.CaptionStyle.Subtitle ? 0.35f : cue.style == CutsceneCue.CaptionStyle.Title ? 2.2f : 1.6f;
                    float edgeOut = cue.style == CutsceneCue.CaptionStyle.Subtitle ? 0.35f : 1.4f;
                    float seconds = (float)playable.GetTime();
                    float left = (float)duration - seconds;
                    float alpha = Mathf.Clamp01(Mathf.Min(seconds / edgeIn, left / edgeOut));
                    director.SetCaption(cue.style, cue.text, alpha * alpha * (3f - 2f * alpha), t);
                }

                break;
            case CutsceneCue.Kind.Fade:
                if (director != null)
                {
                    director.SetBlack(Mathf.Lerp(cue.from, cue.to, t));
                }

                break;
            case CutsceneCue.Kind.Move:
                Move(t);
                break;
            case CutsceneCue.Kind.Reach:
                if (reach != null)
                {
                    float r = Mathf.Clamp01(t / Mathf.Max(0.01f, cue.reachShare));
                    reach.Grip = target;
                    reach.Weight = r * r * (3f - 2f * r);
                }

                break;
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (cue != null && cue.kind == CutsceneCue.Kind.Caption && info.output.GetUserData() is CutsceneDirector director)
        {
            director.SetCaption(cue.style, cue.text, 0f, 1f);
        }

        // The cutscene ends holding the handle; only scrubbing back lets go.
        if (cue != null && cue.kind == CutsceneCue.Kind.Reach && reach != null && playable.GetTime() <= 0.0)
        {
            reach.Weight = 0f;
        }
    }

    private void Begin(CutsceneDirector director)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        switch (cue.kind)
        {
            case CutsceneCue.Kind.Lightning:
                if (director != null)
                {
                    director.Lightning(cue.close);
                }

                break;
            case CutsceneCue.Kind.Animate:
                if (animator != null && !string.IsNullOrEmpty(cue.state))
                {
                    animator.CrossFadeInFixedTime(cue.state, cue.crossfade, 0, 0f);
                }

                break;
            case CutsceneCue.Kind.Reach:
                if (animator != null && target != null && animator.TryGetComponent(out ActorLook look))
                {
                    look.Target = target;
                }

                break;
            case CutsceneCue.Kind.FirstPerson:
                if (animator != null && animator.TryGetComponent(out ActorFirstPerson firstPerson))
                {
                    firstPerson.Set(true);
                }

                break;
        }
    }

    private void Move(float t)
    {
        if (target == null || path == null)
        {
            return;
        }

        float eased = Mathf.Lerp(t, t * t * (3f - 2f * t), cue.easing);
        Vector3 position = path.Evaluate(eased);
        if (cue.followGround)
        {
            position.y = Ground(position);
        }

        target.position = position;
        if (cue.faceAlong)
        {
            Vector3 along = path.Tangent(eased);
            along.y = 0f;
            if (along.sqrMagnitude > 1e-6f)
            {
                target.rotation = Quaternion.LookRotation(along.normalized, Vector3.up);
            }
        }
    }

    // The highest surface under the point, ignoring the mover itself (the island, a plank of the pier).
    private float Ground(Vector3 at)
    {
        int count = Physics.RaycastNonAlloc(at + Vector3.up * 3f, Vector3.down, Hits, 8f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            if (!Hits[i].transform.IsChildOf(target) && Hits[i].point.y > best)
            {
                best = Hits[i].point.y;
            }
        }

        return float.IsNegativeInfinity(best) ? at.y : best;
    }
}
