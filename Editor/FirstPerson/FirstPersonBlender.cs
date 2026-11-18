using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// The first person clips edited in Blender (Gustavo, 10/10: "fazer no Blender é mais fácil"; it is how studios do it:
// the arms are animated in a DCC and the engine plays the exported clips).
//   Horror > Animação > Primeira pessoa: abrir no Blender
//     writes SourceAssets/Blender/FirstPerson/clips.json (every clip of the rig, frame by frame, the rest pose, the eye
//     and the gun), builds SourceAssets/Blender/FirstPerson/FirstPersonArms.blend with Blender in the background
//     (SourceAssets/Tools/first_person_blender.py: the player's skeleton and skin, the revolver in the right hand,
//     a camera at the eye, one Action per clip) and opens it.
//   Horror > Animação > Primeira pessoa: importar os clipes do Blender
//     exports one FBX per Action with Blender in the background into Art/Animations/FirstPerson/Blender/, then copies
//     the arm bones' curves of each one into the game's .anim of the same name (events and the gun's own tracks stay),
//     and rebuilds the Animator. A clip that does not exist in the game yet becomes a new .anim in Tomadas/.
// Checked by a round trip (Export, Import without editing): bones back within 0.1 degree and 1 mm.
public static class FirstPersonBlender
{
    public const string BlenderExe = @"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe";
    public static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../SourceAssets/Blender/FirstPerson"));
    public static readonly string Script = Path.GetFullPath(Path.Combine(Application.dataPath, "../SourceAssets/Tools/first_person_blender.py"));
    public static readonly string JsonPath = Path.Combine(Folder, "clips.json");
    public static readonly string BlendPath = Path.Combine(Folder, "FirstPersonArms.blend");
    public const string FbxFolder = FirstPersonRig.Folder + "/Blender";
    private const float Fps = 30f;

    [System.Serializable]
    private class BonePose
    {
        public string name;
        public string parent;
        public float[] position;
        public float[] rotation;
    }

    [System.Serializable]
    private class ClipData
    {
        public string name;
        public string path;
        public float fps;
        public int frames;
        public bool loop;
        // frames x bones, flattened: position (3) then rotation (4) per bone, in the rig root's space.
        public float[] values;
        public string[] bones;
    }

    [System.Serializable]
    private class Export
    {
        public string bodyFbx;
        public string gunFbx;
        public float[] gunInHand;
        public float gunScale;
        // The gun's own bones (Cylinder, Hammer, ...) in the right hand's space, so Blender can fit the imported gun
        // on them whatever its importer does with the file's root.
        public BonePose[] gunBones;
        public float[] eyePosition;
        public float[] eyeRotation;
        public float eyeFov;
        public BonePose[] rest;
        public ClipData[] clips;
    }

    [MenuItem("Horror/Animação/Primeira pessoa: abrir no Blender")]
    public static void OpenInBlender()
    {
        if (!File.Exists(BlendPath))
        {
            RebuildBlend();
        }
        else if (!File.Exists(BlenderExe))
        {
            Debug.LogError("[FirstPerson] Blender não encontrado em " + BlenderExe);
            return;
        }

        if (File.Exists(BlendPath))
        {
            Process.Start(BlenderExe, "\"" + BlendPath + "\"");
            Debug.Log("[FirstPerson] Blender aberto com " + BlendPath + " (o arquivo é o seu: só \"refazer da Unity\" o sobrescreve)");
        }
    }

    // Throws the .blend away and makes it again from the game's clips: what was edited in Blender and not yet
    // imported is lost, so the menu asks.
    [MenuItem("Horror/Animação/Primeira pessoa: refazer o .blend a partir da Unity (perde edições não importadas)")]
    public static void RebuildBlendMenu()
    {
        if (File.Exists(BlendPath) && !EditorUtility.DisplayDialog("Refazer o .blend",
            "Isto refaz " + BlendPath + " a partir dos clipes do jogo. O que foi editado no Blender e ainda não importado se perde. Continuar?", "Refazer", "Cancelar"))
        {
            return;
        }

        RebuildBlend();
    }

    public static bool RebuildBlend()
    {
        WriteJson();
        return RunBlender("build", JsonPath, BlendPath);
    }

    // A dated copy of the .blend in backup/ (what was edited there is never thrown away by a rebuild).
    public static string BackupBlend(string tag)
    {
        if (!File.Exists(BlendPath))
        {
            return null;
        }

        string folder = Path.Combine(Folder, "backup");
        Directory.CreateDirectory(folder);
        string copy = Path.Combine(folder, "FirstPersonArms_" + System.DateTime.Now.ToString("yyyy-MM-dd_HHmm") + "_" + tag + ".blend");
        File.Copy(BlendPath, copy, true);
        Debug.Log("[FirstPerson] .blend copiado para " + copy);
        return copy;
    }

    // Whether the .blend already has an Action for each of these names (read from the file by Blender, a few seconds).
    public static bool BlendHasActions(IEnumerable<string> names)
    {
        if (!File.Exists(BlendPath) || !File.Exists(BlenderExe))
        {
            return false;
        }

        string list = Path.Combine(Folder, "actions.txt");
        if (File.Exists(list))
        {
            File.Delete(list);
        }

        RunBlender("list", BlendPath, list);
        if (!File.Exists(list))
        {
            return false;
        }

        HashSet<string> have = new HashSet<string>(File.ReadAllLines(list).Select(l => l.Trim()));
        return names.All(have.Contains);
    }

    [MenuItem("Horror/Animação/Primeira pessoa: importar os clipes do Blender")]
    public static void ImportFromBlender()
    {
        ImportFromBlender(true);
    }

    // ---------------------------------------------------------------- export

    public static void WriteJson()
    {
        Directory.CreateDirectory(Folder);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirstPersonRig.PrefabPath);
        Transform eye = new GameObject("Eye").transform;
        GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        rig.transform.SetParent(eye, false);
        try
        {
            Transform root = rig.transform;
            List<Transform> bones = AllBones(root);
            Export data = new Export
            {
                bodyFbx = Path.GetFullPath(FirstPersonRig.ModelPath),
                eyePosition = V(root.InverseTransformPoint(eye.position)),
                eyeRotation = Q(Quaternion.Inverse(root.rotation) * eye.rotation),
                eyeFov = 68f,
                rest = bones.Select(b => new BonePose
                {
                    name = b.name,
                    parent = b.parent == root ? "" : b.parent.name,
                    position = V(root.InverseTransformPoint(b.position)),
                    rotation = Q(Quaternion.Inverse(root.rotation) * b.rotation),
                }).ToArray(),
            };

            // The revolver as the scene holds it (the model under the socket): its pose in the right hand's space.
            Revolver revolver = Object.FindAnyObjectByType<Revolver>(FindObjectsInactive.Include);
            FirstPersonArms sceneArms = revolver != null ? revolver.GetComponentInParent<FirstPersonArms>() : null;
            if (revolver != null && sceneArms != null && revolver.transform.childCount > 0)
            {
                Transform model = revolver.transform.GetChild(0);
                Transform hand = sceneArms.Bone("hand_r");
                data.gunFbx = Path.GetFullPath(Python357Setup.ModelPath);
                data.gunInHand = V(hand.InverseTransformPoint(model.position)).Concat(Q(Quaternion.Inverse(hand.rotation) * model.rotation)).ToArray();
                data.gunScale = model.lossyScale.x;
                data.gunBones = model.GetComponentsInChildren<Transform>(true)
                    .Where(t => t != model && t.GetComponent<Renderer>() == null && t.GetComponent<Light>() == null && t.GetComponent<ParticleSystem>() == null)
                    .Select(t => new BonePose { name = t.name, parent = "", position = V(hand.InverseTransformPoint(t.position)), rotation = Q(Quaternion.Inverse(hand.rotation) * t.rotation) })
                    .ToArray();
            }

            List<ClipData> clips = new List<ClipData>();
            foreach (string path in ClipPaths())
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    continue;
                }

                int frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * Fps) + 1);
                ClipData c = new ClipData { name = clip.name, path = path, fps = Fps, frames = frames, loop = clip.isLooping, bones = bones.Select(b => b.name).ToArray() };
                c.values = new float[frames * bones.Count * 7];
                for (int f = 0; f < frames; f++)
                {
                    clip.SampleAnimation(rig, Mathf.Min(f / Fps, clip.length));
                    for (int b = 0; b < bones.Count; b++)
                    {
                        int at = (f * bones.Count + b) * 7;
                        Vector3 p = root.InverseTransformPoint(bones[b].position);
                        Quaternion q = Quaternion.Inverse(root.rotation) * bones[b].rotation;
                        c.values[at] = p.x; c.values[at + 1] = p.y; c.values[at + 2] = p.z;
                        c.values[at + 3] = q.x; c.values[at + 4] = q.y; c.values[at + 5] = q.z; c.values[at + 6] = q.w;
                    }
                }

                clips.Add(c);
            }

            data.clips = clips.ToArray();
            File.WriteAllText(JsonPath, JsonUtility.ToJson(data));
            Debug.Log("[FirstPerson] " + JsonPath + ": " + clips.Count + " clipes, " + bones.Count + " ossos" + (data.gunFbx != null ? ", revólver" : ""));
        }
        finally
        {
            Object.DestroyImmediate(eye.gameObject);
        }
    }

    // A clip the game did not have yet: by its name's family (Revolver_Fire goes with the revolver's, a free name is a take).
    private static string NewClipPath(string name)
    {
        if (FirstPersonRig.FinalStates.Contains(name))
        {
            return FirstPersonRig.FinalFolder + "/" + name + ".anim";
        }

        string folder = name.StartsWith("Revolver_") ? FirstPersonRig.Folder + "/Revolver"
            : name.StartsWith("Torch_") ? FirstPersonRig.Folder + "/Lanterna"
            : ProjectPaths.FirstPersonTakes;
        return folder + "/" + name + ".anim";
    }

    private static IEnumerable<string> ClipPaths()
    {
        List<string> paths = new List<string>
        {
            FirstPersonRig.IdleClip, FirstPersonRig.TorchHoldClip, FirstPersonRig.TorchFocusClip, FirstPersonRig.RevolverHoldClip,
            FirstPersonRig.RevolverAimClip, FirstPersonRig.RevolverReloadClip, FirstPersonRig.RevolverSprintClip, FirstPersonRig.RevolverFireClip,
            FirstPersonRig.CoughClip,
        };
        if (Directory.Exists(FirstPersonRig.FinalFolder))
        {
            paths.AddRange(Directory.GetFiles(FirstPersonRig.FinalFolder, "*.anim").Select(f => f.Replace('\\', '/')).OrderBy(f => f));
        }

        if (Directory.Exists(ProjectPaths.FirstPersonTakes))
        {
            paths.AddRange(Directory.GetFiles(ProjectPaths.FirstPersonTakes, "*.anim").Select(f => f.Replace('\\', '/')).OrderBy(f => f));
        }

        return paths;
    }

    // The rig's arm bones, parents before children (clavicles first).
    private static List<Transform> AllBones(Transform root)
    {
        HashSet<string> names = new HashSet<string>(FirstPersonRig.BoneNames(false).Concat(FirstPersonRig.BoneNames(true)));
        return root.GetComponentsInChildren<Transform>(true).Where(t => names.Contains(t.name)).ToList();
    }

    private static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
    private static float[] Q(Quaternion q) => new[] { q.x, q.y, q.z, q.w };

    private static bool RunBlender(string mode, string a, string b)
    {
        if (!File.Exists(BlenderExe))
        {
            Debug.LogError("[FirstPerson] Blender não encontrado em " + BlenderExe);
            return false;
        }

        ProcessStartInfo info = new ProcessStartInfo(BlenderExe, "-b --factory-startup --python \"" + Script + "\" -- " + mode + " \"" + a + "\" \"" + b + "\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using (Process p = Process.Start(info))
        {
            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            p.WaitForExit();
            string report = string.Join("\n", output.Split('\n').Where(l => l.StartsWith("[first_person]") || l.Contains("Error") || l.Contains("Traceback")));
            if (p.ExitCode != 0)
            {
                Debug.LogError("[FirstPerson] Blender (" + mode + ") saiu com " + p.ExitCode + ":\n" + report + "\n" + error);
                return false;
            }

            Debug.Log("[FirstPerson] Blender (" + mode + "):\n" + report);
            return true;
        }
    }

    // ---------------------------------------------------------------- import

    // Returns a report. check: compare each FBX with the game's .anim before writing (the round trip test).
    public static string ImportFromBlender(bool write)
    {
        if (!File.Exists(BlendPath))
        {
            Debug.LogError("[FirstPerson] Falta " + BlendPath + " (abra no Blender primeiro).");
            return "no blend";
        }

        string fbxFolder = Path.GetFullPath(FbxFolder);
        Directory.CreateDirectory(fbxFolder);
        if (!RunBlender("export", BlendPath, fbxFolder))
        {
            return "blender failed";
        }

        AssetDatabase.Refresh();
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirstPersonRig.PrefabPath);
        Transform eye = new GameObject("Eye").transform;
        GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        rig.transform.SetParent(eye, false);
        try
        {
            foreach (string file in Directory.GetFiles(fbxFolder, "*.fbx").OrderBy(f => f))
            {
                string fbx = "Assets" + file.Replace('\\', '/').Substring(Application.dataPath.Length);
                ConfigureImporter(fbx);
                AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                if (source == null)
                {
                    report.AppendLine(fbx + ": sem clipe");
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(file);
                string animPath = ClipPaths().FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name) ?? NewClipPath(name);
                AnimationClip target = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
                AnimationClip remapped = Remap(source, rig.transform);
                string check = target != null ? Compare(target, remapped, rig) : "novo";
                // Untouched in Blender (the round trip's own noise measured on 17/10: 0 on the poses, 0.36 degrees and
                // 1.4 mm on the cough keyed on every frame): the game's clip stays as it is, so an import never regresses a
                // clip that was changed only in Unity.
                bool unchanged = target != null && LastWorstAngle < 0.5f && LastWorstMove < 0.002f && Mathf.Abs(target.length - remapped.length) < 0.02f;
                report.AppendLine(name + ": " + check + (write ? (unchanged ? " -> sem mudança, mantido" : " -> gravado em " + animPath) : ""));
                if (write && !unchanged)
                {
                    Write(remapped, target, animPath);
                }
            }

            if (write)
            {
                FirstPersonRig.BuildController();
            }
        }
        finally
        {
            Object.DestroyImmediate(eye.gameObject);
        }

        Debug.Log("[FirstPerson] importação do Blender:\n" + report);
        return report.ToString();
    }

    private static void ConfigureImporter(string fbx)
    {
        ModelImporter importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }

        if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            changed = true;
        }

        if (importer.animationCompression != ModelImporterAnimationCompression.Off)
        {
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            changed = true;
        }

        if (importer.resampleCurves)
        {
            importer.resampleCurves = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    // The FBX clip's bone curves on the rig's paths: the path is cut at the clavicle (the rig root stands for the
    // spine the clavicles hang from), positions are kept only for the upper arms and scaled to the rig's units, all
    // other bones (the spine, the legs) are dropped.
    private static AnimationClip Remap(AnimationClip source, Transform rig)
    {
        AnimationClip clip = new AnimationClip { frameRate = source.frameRate, name = source.name };
        float scale = 1f;
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
        {
            int at = binding.path.IndexOf("clavicle_", System.StringComparison.Ordinal);
            if (at < 0 || binding.type != typeof(Transform))
            {
                continue;
            }

            string path = binding.path.Substring(at);
            string leaf = path.Substring(path.LastIndexOf('/') + 1);
            // Only the rig's own bones: the Blender file's helper chains (IKC_*, SOCK_*) hang off the clavicles too.
            if (!FirstPersonRig.BoneNames(false).Contains(leaf) && !FirstPersonRig.BoneNames(true).Contains(leaf))
            {
                continue;
            }

            bool position = binding.propertyName.StartsWith("m_LocalPosition");
            if (position && !leaf.StartsWith("upperarm"))
            {
                continue;
            }

            if (!position && !binding.propertyName.StartsWith("m_LocalRotation") && !binding.propertyName.StartsWith("localEulerAngles"))
            {
                continue;
            }

            AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
            if (position)
            {
                if (scale == 1f)
                {
                    scale = PositionScale(source, rig, path);
                }

                Keyframe[] keys = curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i].value *= scale;
                    keys[i].inTangent *= scale;
                    keys[i].outTangent *= scale;
                }

                curve = new AnimationCurve(keys);
            }

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), binding.propertyName), curve);
        }

        clip.EnsureQuaternionContinuity();
        return clip;
    }

    // Blender's units against the rig's: the upper arm's rest distance from its clavicle (17.4 cm on this body).
    private static float PositionScale(AnimationClip source, Transform rig, string upperPath)
    {
        Transform upper = rig.Find(upperPath);
        if (upper == null)
        {
            return 1f;
        }

        string sourcePath = AnimationUtility.GetCurveBindings(source).First(b => b.path.EndsWith(upperPath)).path;
        Vector3 at = Vector3.zero;
        foreach (string axis in new[] { "x", "y", "z" })
        {
            AnimationCurve c = AnimationUtility.GetEditorCurve(source, EditorCurveBinding.FloatCurve(sourcePath, typeof(Transform), "m_LocalPosition." + axis));
            if (c == null)
            {
                return 1f;
            }

            at[axis == "x" ? 0 : axis == "y" ? 1 : 2] = c.Evaluate(0f);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirstPersonRig.PrefabPath);
        Transform rest = prefab.transform.Find(upperPath);
        // The clip's first frame may hold the shoulder a little off its rest place (the shift of a weapon pose), so
        // the ratio only picks the unit: metres, centimetres or hundredths.
        float ratio = at.magnitude > 1e-6f ? rest.localPosition.magnitude / at.magnitude : 1f;
        float best = 1f;
        foreach (float candidate in new[] { 0.01f, 1f, 100f })
        {
            if (Mathf.Abs(Mathf.Log10(ratio / candidate)) < Mathf.Abs(Mathf.Log10(ratio / best)))
            {
                best = candidate;
            }
        }

        return best;
    }

    // The worst bone over the clip: degrees of turn and millimetres of place, sampled every frame on the rig.
    private static float LastWorstAngle, LastWorstMove;

    private static string Compare(AnimationClip a, AnimationClip b, GameObject rig)
    {
        List<Transform> bones = AllBones(rig.transform);
        float worstAngle = 0f, worstMove = 0f;
        string worstBone = "";
        float length = Mathf.Max(a.length, b.length);
        int frames = Mathf.RoundToInt(length * Fps) + 1;
        Quaternion[] ra = new Quaternion[bones.Count];
        Vector3[] pa = new Vector3[bones.Count];
        for (int f = 0; f < frames; f++)
        {
            float t = Mathf.Min(f / Fps, length);
            a.SampleAnimation(rig, Mathf.Min(t, a.length));
            for (int i = 0; i < bones.Count; i++)
            {
                ra[i] = bones[i].rotation;
                pa[i] = bones[i].position;
            }

            b.SampleAnimation(rig, Mathf.Min(t, b.length));
            for (int i = 0; i < bones.Count; i++)
            {
                float angle = Quaternion.Angle(ra[i], bones[i].rotation);
                float move = Vector3.Distance(pa[i], bones[i].position);
                if (angle > worstAngle)
                {
                    worstAngle = angle;
                    worstBone = bones[i].name + " a " + t.ToString("F2") + " s";
                }

                worstMove = Mathf.Max(worstMove, move);
            }
        }

        LastWorstAngle = worstAngle;
        LastWorstMove = worstMove;
        return "pior osso " + worstAngle.ToString("F2") + " graus (" + worstBone + "), " + (worstMove * 1000f).ToString("F1") + " mm; " + a.length.ToString("F2") + " s antes, " + b.length.ToString("F2") + " s do Blender";
    }

    // The .anim keeps everything that is not an arm bone (events, the gun's crane, the speedloader) and takes the
    // bones from Blender.
    private static void Write(AnimationClip remapped, AnimationClip target, string animPath)
    {
        AnimationClip clip = new AnimationClip { frameRate = remapped.frameRate, name = Path.GetFileNameWithoutExtension(animPath) };
        if (target != null)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(target))
            {
                if (!binding.path.StartsWith("clavicle_") || binding.type != typeof(Transform))
                {
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(target, binding));
                }
            }

            AnimationUtility.SetAnimationEvents(clip, AnimationUtility.GetAnimationEvents(target));
            AnimationUtility.SetAnimationClipSettings(clip, AnimationUtility.GetAnimationClipSettings(target));
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(remapped))
        {
            AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(remapped, binding));
        }

        clip.EnsureQuaternionContinuity();
        ClipRecorder.Save(clip, animPath);
    }
}
