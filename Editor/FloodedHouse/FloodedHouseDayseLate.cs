using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// What Dayse put in the Drive on 24 and 25/11 (WhatsApp of 25/11; Docs/Notas/modelos-dayse-meshy-2026-11.md):
//   "O homem na parede": her own Blender model, a chrome wall with a face pressed through it, a nose and one big
//   eye. Three pieces (SourceAssets/Tools/wall_man_split.py): the wall (mirror polish set here, as she asked), the
//   nose, and the eyeball on a pivot. Her animation of the eye is a clip (WallMan_LookAround.anim, made from her
//   keys) played by an Animator; WallManEye makes it stare at the player when they come near. Stood against a free
//   wall of the passage to Alex's lab (the bunker); a proposal, the place is Gustavo's to choose.
//   The tactical boat (Meshy): a closed patrol boat, so it cannot be Erick's open boat of the arrival ("não sei se
//   ele vai servir pq ele é fechado"): aground off the north shore by the end of the road, beyond the rail.
//   The two trees (Meshy, "flooded surreal" and "abyssal alien"): on the ground the island gained on 24/11, the
//   mangrove-rooted one near the banks, the black one with blue bulbs along the road (glowing through the blue
//   fungus's slider, GameTuning.blueFungusGlow).
// Built with the flora (BuildFlora) and on its own into the open scene by the menu below.
public static partial class FloodedHouseBuilder
{
    private const string WallManAnimations = ProjectPaths.Art + "/Animations/WallMan";
    private const string WallManClipPath = WallManAnimations + "/WallMan_LookAround.anim";
    private const string WallManControllerPath = WallManAnimations + "/WallMan.controller";
    private const string DayseLateGroup = "DayseLate";

    [System.Serializable]
    private sealed class WallManLook
    {
        public float fps = 24f;
        public float nose_side = -1f;
        public List<WallManFrame> frames = new List<WallManFrame>();
    }

    [System.Serializable]
    private sealed class WallManFrame
    {
        public float t;
        public float side;
        public float up;
    }

    [MenuItem("Horror/Flooded House/Modelos da Dayse de 25-11 (homem na parede, barco, árvores)")]
    public static void BuildDayseLateInScene()
    {
        GameObject flora = GameObject.Find("Flora");
        if (flora == null)
        {
            Debug.LogWarning("[FloodedHouse] No 'Flora' group in the open scene: open Flooded_House first.");
            return;
        }

        Random.State saved = Random.state;
        Random.InitState(2511);
        FirstPersonController player = Object.FindAnyObjectByType<FirstPersonController>();
        int before = blueFungusMaterials.Count;
        DayseLate(flora.transform, player != null ? player.transform : null);
        Random.state = saved;

        // The tree's glow rides the blue fungus's slider: hand its material to the scene's TunedGrowths without
        // touching the ones the full build gave it.
        TunedGrowths tuned = flora.GetComponent<TunedGrowths>();
        if (tuned != null)
        {
            SerializedObject so = new SerializedObject(tuned);
            SerializedProperty list = so.FindProperty("blueFungus");
            foreach (Material m in blueFungusMaterials.Skip(before))
            {
                bool there = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    there |= list.GetArrayElementAtIndex(i).objectReferenceValue == m;
                }

                if (!there)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = m;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(flora.scene);
    }

    private static void DayseLate(Transform flora, Transform player)
    {
        Transform old = flora.Find(DayseLateGroup);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        Transform g = Group(DayseLateGroup, flora);
        WallManScene(g, player);
        ShoreBoat(g);
        LateTrees(g);
    }

    // ---------------------------------------------------------------- the man in the wall

    private static bool WallManReady() => HasModel("WallManWall") && HasModel("WallManNose") && HasModel("WallManEye");

    // In the passage to Alex's lab (2.5 m wide: the player has to walk past the eye, a metre and a half from it),
    // else against the first free stretch of wall of the lab, of the store, of the laundry. The lab and the store
    // had no free wall on 25/11, and in the laundry a slab leaning by the south wall hid the eye.
    private static void WallManScene(Transform parent, Transform player)
    {
        if (!WallManReady())
        {
            return;
        }

        // Dayse, 26/11 (she walked the whole demo and never found it in the passage): "é pra ele ficar na varanda. Do
        // tamanho de uma parede mesmo. A parte da parede é substituída pelo homem na parede". The varanda is the
        // courtyard: the panel grows to the gallery's full height (3.4 m) and is sunk into the wall, so the face comes
        // out of the wall itself.
        const float wallScale = (LunaManor.GroundCeiling - 0.04f) / 2.6f;
        // The west gallery's wall north of the kitchen door (the only door in it, at z 12.6): eight metres of plain
        // wall, seen across the lawn between the pillars. FreePanelSpot refuses the courtyard's walls (their skirting
        // and cornice are not "flat"), so the spot is given and only the wall's face is measured.
        Vector3 courtOut = Vector3.right;
        Vector3 probe = M(LunaManor.Courtyard.xMin + 1.2f, 1.6f, 17.3f);
        if (WallFace(probe, -courtOut, 3f, out Vector3 courtFace))
        {
            Vector3 courtFoot = new Vector3(courtFace.x, 0f, courtFace.z);
            WallMan(parent, courtFoot - courtOut * 0.22f, courtOut, player, wallScale);
            Debug.Log("[FloodedHouse] wall man: varanda at " + courtFoot.ToString("F2") + " facing " + courtOut);
            return;
        }

        foreach ((string name, Rect room) in new[] { ("passagem", LunaManor.Passagem), ("bunker", LunaManor.Bunker), ("depósito", LunaManor.Deposito), ("lavanderia", LunaManor.Lavanderia) })
        {
            if (FreePanelSpot(room, 2.7f, 2.65f, 1.15f, out Vector3 foot, out Vector3 outward))
            {
                WallMan(parent, foot, outward, player);
                Debug.Log("[FloodedHouse] wall man: " + name + " at " + foot.ToString("F2") + " facing " + outward);
                return;
            }
        }

        Debug.LogWarning("[FloodedHouse] wall man: no free wall 2.7 m wide with 1.15 m clear in front of it.");
    }

    // A stretch of wall of `room`, `width` wide and `height` tall from the floor, flat (no door, window or slab) and
    // with `depth` metres of clear room in front: the place nearest the middle of its wall, the longest walls first.
    private static bool FreePanelSpot(Rect room, float width, float height, float depth, out Vector3 foot, out Vector3 outward)
    {
        Physics.SyncTransforms();
        (Vector2 from, Vector2 to, Vector3 outward)[] walls =
        {
            (new Vector2(room.xMin, room.yMin), new Vector2(room.xMin, room.yMax), Vector3.right),
            (new Vector2(room.xMax, room.yMin), new Vector2(room.xMax, room.yMax), Vector3.left),
            (new Vector2(room.xMin, room.yMin), new Vector2(room.xMax, room.yMin), Vector3.forward),
            (new Vector2(room.xMin, room.yMax), new Vector2(room.xMax, room.yMax), Vector3.back),
        };
        float half = width * 0.5f;
        foreach ((Vector2 from, Vector2 to, Vector3 o) in walls.OrderByDescending(w => (w.to - w.from).magnitude))
        {
            Vector3 a = M(from, 0f), b = M(to, 0f);
            Vector3 along = (b - a).normalized;
            float length = (b - a).magnitude;
            Vector3 best = Vector3.zero;
            float bestOff = float.MaxValue;
            for (float d = half + 0.3f; d <= length - half - 0.3f; d += 0.2f)
            {
                Vector3 centre = a + along * d;
                if (!WallFace(centre + Vector3.up * 1.3f + o * 1.4f, -o, 2.2f, out Vector3 face))
                {
                    continue;
                }

                // The floor is the lowest thing under the spot: the first hit can be a cot or a crate standing there
                // (the panel stood 0.69 m up on one, 25/11), which the clearance box below then refuses.
                RaycastHit[] under = Physics.RaycastAll(face + o * 0.5f + Vector3.up * 0.3f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
                if (under.Length == 0)
                {
                    continue;
                }

                RaycastHit floor = under.OrderBy(h => h.point.y).First();

                bool flat = true;
                foreach (float h in new[] { 0.35f, 1.3f, height - 0.25f })
                {
                    foreach (float s in new[] { -half + 0.1f, 0f, half - 0.1f })
                    {
                        Vector3 from3 = new Vector3(face.x, floor.point.y + h, face.z) + along * s + o * 1.4f;
                        flat &= WallFace(from3, -o, 2.2f, out Vector3 c) && Mathf.Abs(Vector3.Dot(c - face, o)) < 0.04f;
                    }
                }

                Vector3 boxCentre = new Vector3(face.x, floor.point.y + 0.15f + (height - 0.15f) * 0.5f, face.z) + o * (0.06f + depth * 0.5f);
                Vector3 halfBox = Abs(along) * half + Vector3.up * ((height - 0.15f) * 0.5f) + Abs(o) * (depth * 0.5f);
                if (flat && !Physics.CheckBox(boxCentre, halfBox, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(d - length * 0.5f) < bestOff)
                {
                    bestOff = Mathf.Abs(d - length * 0.5f);
                    best = new Vector3(face.x, floor.point.y, face.z);
                }
            }

            if (bestOff < float.MaxValue)
            {
                foot = best;
                outward = o;
                return true;
            }
        }

        foot = Vector3.zero;
        outward = Vector3.forward;
        return false;
    }

    // The three pieces share one pivot (the middle of the foot of the wall's back face). Root: +Z out of the wall.
    internal static GameObject WallMan(Transform parent, Vector3 foot, Vector3 outward, Transform viewer, float scale = 1f)
    {
        GameObject man = new GameObject("WallMan");
        man.transform.SetParent(parent, false);
        man.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        GameObject wall = ModelProp("WallManWall", man.transform, Vector3.zero, Quaternion.identity, false, false);
        GameObject nose = ModelProp("WallManNose", man.transform, Vector3.zero, Quaternion.identity, false, false);
        GameObject eye = ModelProp("WallManEye", man.transform, Vector3.zero, Quaternion.identity, false, false);

        // Which way the file's "out of the wall" came through the import: the wall's thin horizontal axis, on the
        // side the nose sticks out of. Turned to the root's +Z.
        Bounds wb = RendererBounds(wall), nb = RendererBounds(nose);
        Vector3 normal = wb.size.x < wb.size.z ? Vector3.right : Vector3.forward;
        if (Vector3.Dot(nb.center - wb.center, normal) < 0f)
        {
            normal = -normal;
        }

        Quaternion fix = Quaternion.Inverse(Quaternion.LookRotation(normal, Vector3.up));
        foreach (GameObject piece in new[] { wall, nose, eye })
        {
            piece.transform.SetPositionAndRotation(fix * piece.transform.position, fix * piece.transform.rotation);
        }

        Bounds eb = RendererBounds(eye);
        nb = RendererBounds(nose);
        Transform pivot = new GameObject("EyePivot").transform;
        pivot.SetParent(man.transform, false);
        pivot.localPosition = eb.center;
        eye.transform.SetParent(pivot, true);

        // The mirror: Dayse's steel is brushed in the bake; the polish is set here (25/11, "precisa colocar direto na unity").
        Material chrome = wall.GetComponent<Renderer>().sharedMaterial;
        chrome.SetFloat("_Smoothness", 0.96f);
        chrome.SetFloat("_Metallic", 1f);
        chrome.SetColor("_BaseColor", new Color(0.9f, 0.92f, 0.93f));
        EditorUtility.SetDirty(chrome);
        // A wet eyeball; the nose keeps the skin's dull sheen.
        Material ball = eye.GetComponent<Renderer>().sharedMaterial;
        ball.SetFloat("_Smoothness", 0.9f);
        EditorUtility.SetDirty(ball);

        FitCollider(wall);
        FitCollider(nose);
        SphereCollider socket = pivot.gameObject.AddComponent<SphereCollider>();
        socket.radius = Mathf.Max(eb.extents.x, eb.extents.y);

        Animator animator = man.AddComponent<Animator>();
        animator.runtimeAnimatorController = WallManController(Mathf.Sign(nb.center.x - eb.center.x));
        animator.cullingMode = AnimatorCullingMode.CullCompletely;
        man.AddComponent<WallManEye>().Configure(pivot, Camera.main != null ? Camera.main.transform : null);

        man.transform.SetPositionAndRotation(foot, Quaternion.LookRotation(outward, Vector3.up));
        man.transform.localScale = Vector3.one * scale;
        return man;
    }

    // Dayse's keys (wall_man_split.py wrote the gaze of every frame as two angles) as a clip on the pivot, looping.
    // Made once: after that the .anim is the source (edit it in the Animation window); delete it to remake it.
    private static AnimatorController WallManController(float noseSign)
    {
        if (!AssetDatabase.IsValidFolder(WallManAnimations))
        {
            AssetDatabase.CreateFolder(ProjectPaths.Art + "/Animations", "WallMan");
        }

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WallManClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = "WallMan_LookAround", frameRate = 24f };
            string json = System.IO.Path.Combine(Application.dataPath, "../SourceAssets/BlenderExports/Props/WallMan/WallMan_look.json");
            WallManLook look = System.IO.File.Exists(json) ? JsonUtility.FromJson<WallManLook>(System.IO.File.ReadAllText(json)) : new WallManLook();
            clip.frameRate = look.fps;
            AnimationCurve yaw = new AnimationCurve(), pitch = new AnimationCurve(), roll = new AnimationCurve();
            foreach (WallManFrame f in look.frames)
            {
                // "side" grows away from the nose in her file when nose_side is -1; the root's +X is where the import put it.
                yaw.AddKey(f.t, f.side * look.nose_side * noseSign);
                pitch.AddKey(f.t, -f.up);
            }

            if (look.frames.Count > 1)
            {
                // Back to the first pose, so the loop has no jump.
                float end = look.frames[look.frames.Count - 1].t + 0.8f;
                yaw.AddKey(end, yaw.keys[0].value);
                pitch.AddKey(end, pitch.keys[0].value);
                roll.AddKey(0f, 0f);
                roll.AddKey(end, 0f);
            }

            clip.SetCurve("EyePivot", typeof(Transform), "localEulerAnglesRaw.x", pitch);
            clip.SetCurve("EyePivot", typeof(Transform), "localEulerAnglesRaw.y", yaw);
            clip.SetCurve("EyePivot", typeof(Transform), "localEulerAnglesRaw.z", roll);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, WallManClipPath);
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(WallManControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(WallManControllerPath);
            controller.layers[0].stateMachine.AddState("LookAround");
        }

        AnimatorState state = controller.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == "LookAround");
        if (state != null && state.motion != clip)
        {
            state.motion = clip;
            EditorUtility.SetDirty(controller);
        }

        return controller;
    }

    // ---------------------------------------------------------------- the boat aground and the trees

    // Lying along the north shore east of where the road meets the sea, heeled on the bank, beyond the rail: seen
    // from the shore in the lightning, never reached (no collider, no shadow, as the shore's rocks).
    private static void ShoreBoat(Transform parent)
    {
        if (!HasModel("TacticalBoat"))
        {
            return;
        }

        SeaWaves sea = Object.FindAnyObjectByType<SeaWaves>();
        float water = sea != null ? sea.transform.position.y : -2.6f;
        Vector3 at = Shore(6.2f, 46.45f, water - 0.75f);
        GameObject boat = ModelProp("TacticalBoat", parent, at, Quaternion.identity, false, true);
        Bounds b = RendererBounds(boat);
        // Along the shore (the world's x there), the bow to the east, 7 degrees over on its side towards the land.
        bool alongX = b.size.x > b.size.z;
        boat.transform.rotation = Quaternion.AngleAxis(-7f, Vector3.right) * Quaternion.Euler(0f, alongX ? 0f : 90f, 0f) * boat.transform.rotation;
        boat.transform.position += Vector3.up * (at.y - RendererBounds(boat).min.y);
        boat.name = "TacticalBoatAground";
        NoCollidersNoShadows(boat, false);
    }

    // (model, old-shore x and z as Shore() takes them, yaw, scale): the mangrove-rooted tree near the banks, the black
    // one with the blue bulbs by the road to the sea and inland. Each stands on the island's ground (found by a ray:
    // a place over water or over the rail is skipped), the scrub that grew on the same spot removed.
    private static void LateTrees(Transform parent)
    {
        (string model, float x, float z, float yaw, float scale)[] spots =
        {
            ("AbyssalTree", 5.6f, 35f, 20f, 1f), ("AbyssalTree", -4.6f, 39.5f, 200f, 0.9f), ("AbyssalTree", -13f, 33f, 95f, 1.08f), ("AbyssalTree", 16f, 37f, 300f, 0.95f),
            ("FloodedTree", 7.5f, 42.3f, 140f, 1f), ("FloodedTree", -7f, 42.6f, 30f, 1.1f), ("FloodedTree", -16f, 40f, 250f, 0.92f), ("FloodedTree", 19f, 41f, 75f, 1.05f),
            ("FloodedTree", -27f, 20f, 180f, 1f), ("FloodedTree", 30f, 14f, 10f, 0.95f),
        };
        GameObject scrub = GameObject.Find("IslandScrub");
        int placed = 0;
        foreach ((string model, float x, float z, float yaw, float scale) in spots)
        {
            if (!HasModel(model))
            {
                continue;
            }

            Vector3 at = Shore(x, z, 0f);
            if (!Physics.Raycast(at + Vector3.up * 20f, Vector3.down, out RaycastHit ground, 40f, ~0, QueryTriggerInteraction.Ignore) || !ground.collider.name.StartsWith("Ground"))
            {
                Debug.LogWarning("[FloodedHouse] late tree " + model + " at shore " + x + ", " + z + ": no island ground there, skipped.");
                continue;
            }

            if (scrub != null)
            {
                foreach (Transform child in scrub.transform.Cast<Transform>().ToArray())
                {
                    Vector3 d = child.position - at;
                    d.y = 0f;
                    if (child.name != "IslandGrass" && d.magnitude < 3f)
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }
            }

            // The roots a hand into the mud.
            at.y = ground.point.y - 0.12f;
            GameObject tree = ModelProp(model, parent, at, Quaternion.Euler(0f, yaw, 0f), false, false);
            tree.transform.localScale *= scale;
            tree.transform.position += Vector3.up * (at.y - RendererBounds(tree).min.y);
            if (model == "AbyssalTree")
            {
                Material glow = DayseFungusMaterial(model, true);
                glow.SetFloat("_Smoothness", 0.45f);
                foreach (Renderer r in tree.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterial = glow;
                }
            }

            NoCollidersNoShadows(tree, false);
            // Only the trunk stops the player: the box of the whole crown would wall off 6 m of ground.
            Bounds tb = RendererBounds(tree);
            GameObject trunk = new GameObject("Trunk");
            trunk.transform.SetParent(parent, false);
            trunk.transform.position = new Vector3(at.x, tb.center.y, at.z);
            CapsuleCollider capsule = trunk.AddComponent<CapsuleCollider>();
            capsule.radius = 0.75f * scale;
            capsule.height = tb.size.y;
            trunk.transform.SetParent(tree.transform, true);
            placed++;
        }

        Debug.Log("[FloodedHouse] late trees: " + placed + " of " + spots.Length);
    }
}
