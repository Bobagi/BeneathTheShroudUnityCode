using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;

// The shots of the arrival (Cutscenes/Arrival.playable, edited in the Timeline window afterwards):
//    0 - 12  aerial: over the storm toward the island, the boat's lamp below; "Daysavo apresenta"
//   12 - 20  low on the water: the boat comes past, spray, wake and lamp
//   20 - 25  Erick at the tiller, lit by lightning, looking up at the house
//   25 - 33  over his shoulder: the dark mansion on the hill; a tentacle rises beside the boat and
//            sinks again, and he does not see it
//   33 - 40  from the pier: the boat comes alongside and stops
//   40 - 47  behind him, walking off the pier (cut: he has tied up and climbed out)
//   47 - 55  low on the path, the house above: lightning right overhead, and the title comes out of the flash
//   54 - end  as he walks the last metres the camera glides into his eyes (third person to first, 2.5 s blend);
//            he steps onto the porch, stops at arm's length, his hand goes to the handle (IK) and grips it;
//            lightning; the door creaks as the screen goes black
// No subtitles, no words about the story (Gustavo 03/10): only the cards, sound and acting. Music: arrival_theme
// (Audio/Music, CC0), cut so its swell peaks at the title. Card technique: Docs/Cinema/titulos.md.
public static partial class ArrivalCutsceneBuilder
{
    private const float BoatArrives = 38f;
    private const float BoatEasing = 0.35f;
    private const float WalkSpeed = 1.35f;
    // The end at the door: he stops on the porch, the camera goes into his eyes, his hand goes to the handle.
    private const float PorchArrives = 58.6f;
    private const float FirstPersonAt = 54f;
    private const float CameraBlend = 2.5f;
    private const float ReachAt = 59.2f;
    private const float End = 64f;

    private static PlayableDirector director;
    private static TimelineAsset timeline;
    private static CutsceneDirector cutscene;

    private static void Timeline(Transform boat, Transform hull, Animator erickBoat, Animator erickLand, GameObject tentacle)
    {
        // Camera with the Cinemachine brain and the post-processing.
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(root, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 16000f;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        CinemachineBrain brain = cameraObject.AddComponent<CinemachineBrain>();

        GameObject directorObject = new GameObject("Cutscene");
        directorObject.transform.SetParent(root, false);
        director = directorObject.AddComponent<PlayableDirector>();
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.Hold;
        cutscene = directorObject.AddComponent<CutsceneDirector>();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectPaths.Fonts + "/SpecialElite SDF.asset");
        SetField(cutscene, "timeline", director);
        SetField(cutscene, "storm", Object.FindAnyObjectByType<LightningStorm>());
        SetField(cutscene, "font", font);
        SetField(cutscene, "titleFont", font);

        string path = Folder + "/Arrival.playable";
        timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
        if (timeline == null)
        {
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, path);
        }

        foreach (TrackAsset track in timeline.GetRootTracks().ToArray())
        {
            timeline.DeleteTrack(track);
        }

        director.playableAsset = timeline;

        // Courses.
        CutscenePath course = PathObject("BoatCourse", new[]
        {
            new Vector3(-80f, 0f, -150f), new Vector3(-55f, 0f, -126f), new Vector3(-26f, 0f, -103f), new Vector3(6f, 0f, -80f),
            new Vector3(5f, 0f, -66f), new Vector3(1.9f, 0f, -60.8f), new Vector3(1.9f, 0f, -58.6f),
        });
        CutscenePath up = Object.FindObjectsByType<CutscenePath>().First(p => p.name == "UpPath");
        float upLength = up.Length;
        CutscenePath walkPier = SubPath("WalkPier", up, 0.4f, 0.4f + 9.4f);
        CutscenePath walkHouse = SubPath("WalkHouse", up, upLength - 13.7f, upLength - 0.3f);
        erickLand.transform.position = walkPier.Evaluate(0f);
        erickLand.gameObject.SetActive(false);

        Transform head = erickBoat.GetBoneTransform(HumanBodyBones.Head).Find("ErickHeadOnBoat");
        Transform landHead = erickLand.GetBoneTransform(HumanBodyBones.Head);
        Transform window = GameObject.Find("WindowTarget").transform;
        Transform house = GameObject.Find("HouseTarget").transform;
        Transform island = GameObject.Find("AerialAim").transform;
        Transform door = GameObject.Find("DoorTarget").transform;

        // Cameras.
        Transform cams = Group("Cameras");
        // 40 m behind the boat and 10 m up, aiming at the island: the hill on the horizon, and the boat's lamp about
        // 14 degrees under the centre, in the lower third (aimed at the boat itself, the frame was all black sea).
        CinemachineCamera aerial = Cam("CM_Aerial", cams, new Vector3(-108f, 10f, -178f), island, null, Vector3.zero, 44f, false);
        CutscenePath aerialPath = PathObject("AerialPath", new[] { new Vector3(-108f, 10f, -178f), new Vector3(-93f, 9.5f, -163f), new Vector3(-77f, 9f, -147f) });

        Vector3 b19 = BoatAt(course, 16.5f);
        Vector3 fwd19 = course.Tangent(Ease(16.5f / BoatArrives, BoatEasing));
        Vector3 right19 = Vector3.Cross(Vector3.up, fwd19).normalized;
        CinemachineCamera water = Cam("CM_WaterLow", cams, b19 + right19 * 5.5f + Vector3.up * 0.9f, hull, null, Vector3.zero, 44f, true);

        CinemachineCamera face = Cam("CM_Face", cams, Vector3.zero, head, hull, new Vector3(0.5f, 0.98f, -0.3f), 40f, true);
        CinemachineCamera shoulder = Cam("CM_OverShoulder", cams, Vector3.zero, window, hull, new Vector3(-0.5f, 1.5f, -2.5f), 44f, true);
        CinemachineCamera pier = Cam("CM_Pier", cams, new Vector3(-0.6f, PierDeck + 1.3f, -52.5f), hull, null, Vector3.zero, 50f, true);
        CinemachineCamera walk = Cam("CM_WalkStart", cams, Vector3.zero, landHead, erickLand.transform, new Vector3(0.55f, 1.75f, -2.5f), 46f, true);
        // Behind him as he climbs the last of the path, aimed at the door: his silhouette against the porch light
        // (aimed at the middle of the house he sat 17 degrees under the centre, lost in the dark slope; 1.5 m to his
        // side he stood against the dark wall: straight behind and below his eyes, his head covers the lamp).
        CinemachineCamera wide = Cam("CM_HouseWide", cams, Vector3.zero, door, erickLand.transform, new Vector3(0f, 1.4f, -4.5f), 44f, true);
        CinemachineCamera doorCam = Cam("CM_Door", cams, Vector3.zero, door, erickLand.transform, new Vector3(0.4f, 1.65f, -1.9f), 44f, true);
        // First person: in his eyes, aimed at the handle, a wider lens like the game's.
        Transform grip = GameObject.Find("DoorGrip").transform;
        Transform eyes = erickLand.GetBoneTransform(HumanBodyBones.Head).Find("EyePoint");
        CinemachineCamera firstPerson = Cam("CM_FirstPerson", cams, Vector3.zero, GameObject.Find("DoorLook").transform, eyes, Vector3.zero, 70f, true);
        firstPerson.GetComponent<CinemachineFollow>().TrackerSettings.PositionDamping = Vector3.zero;
        // World up: bound to the head bone the camera took its roll from the bone's own axes (35 degrees off).
        firstPerson.GetComponent<CinemachineFollow>().TrackerSettings.BindingMode = BindingMode.WorldSpace;
        firstPerson.GetComponent<CinemachineRotationComposer>().Damping = new Vector2(0.9f, 0.9f);
        firstPerson.GetComponent<CinemachineBasicMultiChannelPerlin>().AmplitudeGain = 0.35f;
        foreach (CinemachineCamera c in new[] { walk, wide, doorCam })
        {
            c.GetComponent<CinemachineFollow>().TrackerSettings.BindingMode = BindingMode.LockToTargetWithWorldUp;
        }

        // Camera cuts.
        CinemachineTrack cuts = timeline.CreateTrack<CinemachineTrack>(null, "Câmeras");
        director.SetGenericBinding(cuts, brain);
        Shot(cuts, aerial, 0f, 12f, "Aérea");
        Shot(cuts, water, 12f, 20f, "Rente à água");
        Shot(cuts, face, 20f, 25f, "Erick no leme");
        Shot(cuts, shoulder, 25f, 33f, "Por cima do ombro");
        Shot(cuts, pier, 33f, 40f, "Do píer");
        Shot(cuts, walk, 40f, 47f, "Saindo do píer");
        // From behind him climbing the path, the camera goes into his eyes while he walks the last metres (Gustavo
        // 03/10: the change to first person happens as he comes to the door, not after he stops): the shots
        // overlap by CameraBlend and Cinemachine blends them, eased. CM_Door stays in the scene, unused.
        Shot(cuts, wide, 47f, FirstPersonAt + CameraBlend, "A casa no alto");
        TimelineClip intoEyes = Shot(cuts, firstPerson, FirstPersonAt, End, "Nos olhos dele");
        intoEyes.blendInDuration = CameraBlend;

        // Who is where.
        ActivationTrack seated = timeline.CreateTrack<ActivationTrack>(null, "Erick no barco");
        director.SetGenericBinding(seated, erickBoat.gameObject);
        Clip(seated.CreateDefaultClip(), 0f, 40f);
        ActivationTrack standing = timeline.CreateTrack<ActivationTrack>(null, "Erick em terra");
        director.SetGenericBinding(standing, erickLand.gameObject);
        Clip(standing.CreateDefaultClip(), 40f, End);

        // Captions and fades.
        CutsceneCueTrack cards = Cues("Letreiros");
        Fade(cards, 0f, 3f, 1f, 0f);
        Caption(cards, 3.5f, 10f, CutsceneCue.CaptionStyle.Presents, "Daysavo apresenta");
        // Out of the close lightning at 48.4 s.
        Caption(cards, 48.55f, 55.5f, CutsceneCue.CaptionStyle.Title, "BENEATH THE SHROUD");
        Fade(cards, 62f, 63.4f, 0f, 1f);

        CutsceneCueTrack sky = Cues("Raios");
        Lightning(sky, 6.2f, false);
        Lightning(sky, 21.2f, false);
        Lightning(sky, 48.4f, true);
        // As his hand closes on the handle: the flash shows the door, the thunder comes over the black.
        Lightning(sky, 61.2f, false);

        CutsceneCueTrack acting = Cues("Animação do Erick");
        Animate(acting, 40f, erickLand, "Walk", 0.1f);
        Animate(acting, PorchArrives - 0.25f, erickLand, "Idle", 0.45f);
        // His head goes to shadow only when the blend is most of the way into his eyes.
        // To the end: a short clip is skipped when the timeline jumps (scrubbing, or starting late) and never fires.
        Cue(acting, FirstPersonAt + CameraBlend * 0.55f, End, CutsceneCue.Kind.FirstPerson, "Primeira pessoa").animator.exposedName = Exposed(erickLand);
        CutsceneCue reach = Cue(acting, ReachAt, End, CutsceneCue.Kind.Reach, "Mão na maçaneta");
        reach.reachShare = 1.5f / (End - ReachAt);
        reach.animator.exposedName = Exposed(erickLand);
        reach.target.exposedName = Exposed(grip);

        Move(Cues("Câmera aérea"), 0f, 12f, aerial.transform, aerialPath, 0f, false, false);
        Move(Cues("Barco"), 0f, BoatArrives, boat, course, BoatEasing, true, false);
        CutsceneCueTrack erickMoves = Cues("Erick andando");
        Move(erickMoves, 40f, 40f + walkPier.Length / WalkSpeed, erickLand.transform, walkPier, 0.15f, true, true);
        // (Kept a second before the porch stop, whatever the path measures.)
        float houseEnd = Mathf.Min(47f + walkHouse.Length / WalkSpeed, PorchArrives - 1.2f);
        Move(erickMoves, 47f, houseEnd, erickLand.transform, walkHouse, 0.2f, true, true);
        // Up onto the porch to arm's length from the handle, slowing to a stop.
        CutscenePath porch = PathObject("WalkPorch", new[] { walkHouse.Evaluate(1f), StandPoint });
        Move(erickMoves, houseEnd, PorchArrives, erickLand.transform, porch, 0.6f, true, true);

        // The tentacle: up beside the boat, ahead and to starboard, while the camera looks over his shoulder.
        // Placed in that shot's frame: from where the camera will be at 29 s, 9 m along its view to the window
        // and 2.8 m right of the centre (the boat is still under way, so it slides past on the right).
        Vector3 b29 = BoatAt(course, 29f);
        Vector3 fwd30 = course.Tangent(Ease(29f / BoatArrives, BoatEasing));
        Vector3 eye = b29 + Quaternion.LookRotation(fwd30) * new Vector3(-0.5f, 1.5f, -2.5f);
        Vector3 view = window.position - eye;
        view.y = 0f;
        view.Normalize();
        Vector3 spot = eye + view * 9f + Vector3.Cross(Vector3.up, view) * 2.8f;
        tentacle.transform.position = new Vector3(spot.x, -4.4f, spot.z);
        tentacle.transform.rotation = Quaternion.LookRotation(Vector3.up, -fwd30);
        CutscenePath rise = PathObject("TentacleRise", new[] { new Vector3(spot.x, -4.4f, spot.z), new Vector3(spot.x, -0.5f, spot.z) });
        CutscenePath sink = PathObject("TentacleSink", new[] { new Vector3(spot.x, -0.5f, spot.z), new Vector3(spot.x, -4.6f, spot.z) });
        CutsceneCueTrack creature = Cues("Tentáculo");
        Move(creature, 27.6f, 29.4f, tentacle.transform, rise, 0.6f, false, false);
        Move(creature, 30.6f, 32.6f, tentacle.transform, sink, 0.5f, false, false);

        // Music, 2D, under the storm's own sound.
        AudioClip theme = AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Music/arrival_theme.wav");
        if (theme != null)
        {
            AudioSource musicSource = directorObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.volume = 0.6f;
            AudioTrack music = timeline.CreateTrack<AudioTrack>(null, "Música");
            director.SetGenericBinding(music, musicSource);
            TimelineClip clip = music.CreateClip(theme);
            Clip(clip, 0f, Mathf.Min(End, theme.length));
        }

        // The door gives a little under his hand as the screen goes black.
        AudioClip creak = AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Doors/door_creak_1.wav");
        if (creak != null)
        {
            AudioSource doorSource = directorObject.AddComponent<AudioSource>();
            doorSource.playOnAwake = false;
            doorSource.spatialBlend = 0f;
            doorSource.volume = 0.8f;
            AudioTrack doorTrack = timeline.CreateTrack<AudioTrack>(null, "Porta");
            director.SetGenericBinding(doorTrack, doorSource);
            Clip(doorTrack.CreateClip(creak), 62.1f, Mathf.Min(End, 62.1f + creak.length));
        }

        // Where he looks: ahead over the bow from the boat (Gustavo 03/10), at the door on the way up.
        Transform ahead = new GameObject("BoatAhead").transform;
        ahead.SetParent(hull, false);
        ahead.localPosition = new Vector3(0f, 1.3f, 25f);
        LookAt(erickBoat, ahead);
        LookAt(erickLand, door);

        timeline.fixedDuration = End;
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
    }

    private static void LookAt(Animator actor, Transform target)
    {
        ActorLook look = actor.GetComponent<ActorLook>();
        if (look != null)
        {
            SetField(look, "target", target);
        }
    }

    // ---- shot and cue helpers -------------------------------------------------------------------------------------

    // A virtual camera: either standing at `position`, or carried by `follow` at `offset` in its space (locked to it,
    // so it rocks with the boat), aiming at `lookAt`. Hand-held shake on the closer shots.
    private static CinemachineCamera Cam(string name, Transform parent, Vector3 position, Transform lookAt, Transform follow, Vector3 offset, float fov, bool handheld)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        CinemachineCamera cam = go.AddComponent<CinemachineCamera>();
        LensSettings lens = cam.Lens;
        lens.FieldOfView = fov;
        lens.NearClipPlane = 0.08f;
        // Out to the mainland on the horizon (15 km).
        lens.FarClipPlane = 16000f;
        cam.Lens = lens;
        cam.LookAt = lookAt;
        if (lookAt != null)
        {
            CinemachineRotationComposer composer = go.AddComponent<CinemachineRotationComposer>();
            composer.Damping = new Vector2(0.6f, 0.6f);
        }

        if (follow != null)
        {
            cam.Follow = follow;
            CinemachineFollow f = go.AddComponent<CinemachineFollow>();
            f.FollowOffset = offset;
            f.TrackerSettings.BindingMode = BindingMode.LockToTarget;
            f.TrackerSettings.PositionDamping = new Vector3(0.3f, 0.3f, 0.3f);
            go.transform.position = follow.TransformPoint(offset);
        }

        if (handheld)
        {
            CinemachineBasicMultiChannelPerlin noise = go.AddComponent<CinemachineBasicMultiChannelPerlin>();
            noise.NoiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>(AssetDatabase.FindAssets("Handheld_normal_mild t:NoiseSettings").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault());
            noise.AmplitudeGain = 0.7f;
            noise.FrequencyGain = 0.8f;
        }

        return cam;
    }

    private static TimelineClip Shot(CinemachineTrack track, CinemachineCamera cam, float start, float end, string label)
    {
        TimelineClip clip = track.CreateClip<CinemachineShot>();
        Clip(clip, start, end);
        clip.displayName = label;
        CinemachineShot shot = (CinemachineShot)clip.asset;
        shot.DisplayName = label;
        shot.VirtualCamera.exposedName = GUID.Generate().ToString();
        director.SetReferenceValue(shot.VirtualCamera.exposedName, cam);
        return clip;
    }

    // A name under which the director resolves `value` for an ExposedReference.
    private static string Exposed(Object value)
    {
        string name = GUID.Generate().ToString();
        director.SetReferenceValue(name, value);
        return name;
    }

    private static CutsceneCueTrack Cues(string name)
    {
        CutsceneCueTrack track = timeline.CreateTrack<CutsceneCueTrack>(null, name);
        director.SetGenericBinding(track, cutscene);
        return track;
    }

    private static CutsceneCue Cue(CutsceneCueTrack track, float start, float end, CutsceneCue.Kind kind, string label)
    {
        TimelineClip clip = track.CreateClip<CutsceneCue>();
        Clip(clip, start, end);
        clip.displayName = label;
        CutsceneCue cue = (CutsceneCue)clip.asset;
        cue.kind = kind;
        return cue;
    }

    private static void Caption(CutsceneCueTrack track, float start, float end, CutsceneCue.CaptionStyle style, string text)
    {
        CutsceneCue cue = Cue(track, start, end, CutsceneCue.Kind.Caption, text);
        cue.style = style;
        cue.text = text;
    }

    private static void Fade(CutsceneCueTrack track, float start, float end, float from, float to)
    {
        CutsceneCue cue = Cue(track, start, end, CutsceneCue.Kind.Fade, to > from ? "Escurece" : "Clareia");
        cue.from = from;
        cue.to = to;
    }

    private static void Lightning(CutsceneCueTrack track, float at, bool close)
    {
        CutsceneCue cue = Cue(track, at, at + 0.4f, CutsceneCue.Kind.Lightning, close ? "Raio perto" : "Raio");
        cue.close = close;
    }

    private static void Animate(CutsceneCueTrack track, float at, Animator animator, string state, float crossfade)
    {
        CutsceneCue cue = Cue(track, at, at + 0.4f, CutsceneCue.Kind.Animate, state);
        cue.state = state;
        cue.crossfade = crossfade;
        cue.animator.exposedName = GUID.Generate().ToString();
        director.SetReferenceValue(cue.animator.exposedName, animator);
    }

    private static void Move(CutsceneCueTrack track, float start, float end, Transform target, CutscenePath path, float easing, bool faceAlong, bool followGround)
    {
        CutsceneCue cue = Cue(track, start, end, CutsceneCue.Kind.Move, target.name + " por " + path.name);
        cue.easing = easing;
        cue.faceAlong = faceAlong;
        cue.followGround = followGround;
        cue.target.exposedName = GUID.Generate().ToString();
        cue.path.exposedName = GUID.Generate().ToString();
        director.SetReferenceValue(cue.target.exposedName, target);
        director.SetReferenceValue(cue.path.exposedName, path);
    }

    private static void Clip(TimelineClip clip, float start, float end)
    {
        clip.start = start;
        clip.duration = end - start;
    }

    // The same easing the Move cue applies, to know where the boat is at a given second of the timeline.
    private static float Ease(float t, float easing)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Lerp(t, t * t * (3f - 2f * t), easing);
    }

    private static Vector3 BoatAt(CutscenePath course, float seconds)
    {
        return course.Evaluate(Ease(seconds / BoatArrives, BoatEasing));
    }

    // A stretch of a path, from `from` to `to` metres along it, as a path of its own.
    private static CutscenePath SubPath(string name, CutscenePath path, float from, float to)
    {
        float length = path.Length;
        List<Vector3> points = new List<Vector3>();
        for (float d = from; d < to; d += 1.5f)
        {
            points.Add(path.Evaluate(d / length));
        }

        points.Add(path.Evaluate(to / length));
        return PathObject(name, points.ToArray());
    }
}
