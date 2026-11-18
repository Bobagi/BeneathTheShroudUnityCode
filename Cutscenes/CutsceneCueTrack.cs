using UnityEngine.Timeline;

// The cutscene's cue track: its clips are CutsceneCue (captions, fades, lightning, animation, movement), and it is
// bound to the scene's CutsceneDirector, which draws what the cues ask for.
[TrackColor(0.85f, 0.45f, 0.15f)]
[TrackBindingType(typeof(CutsceneDirector))]
[TrackClipType(typeof(CutsceneCue))]
public sealed class CutsceneCueTrack : TrackAsset
{
}
