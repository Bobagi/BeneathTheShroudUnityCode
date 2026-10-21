using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

// Test tool (not used in the demo): plays every clip of the Animator in real time, the way the game does, and at
// set moments of each clip (after the whole frame: animation, LateUpdate fixes) renders the creature from the side
// and measures the lowest point of its deformed skin above the ground. Writes <folder>/<clip>_<k>.png and
// <folder>/ground.txt, then done.txt. Forcing poses with Animator.Play + Update(0) inside one call gave poses that
// changed from run to run (02/10), so validation is done on real frames only.
[RequireComponent(typeof(Animator))]
public sealed class AnimationFilmstrip : MonoBehaviour
{
    public string folder = "Docs/Screenshots/Drowned/film";
    public float[] moments = { 0.02f, 0.2f, 0.4f, 0.6f, 0.8f, 0.97f };
    public Vector3 stage = new Vector3(6f, 0f, -7f);

    private void Start()
    {
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        Directory.CreateDirectory(folder);
        if (File.Exists(Path.Combine(folder, "done.txt")))
        {
            File.Delete(Path.Combine(folder, "done.txt"));
        }

        Animator animator = GetComponent<Animator>();
        AnimationShowcase showcase = GetComponent<AnimationShowcase>();
        if (showcase != null)
        {
            showcase.enabled = false;
        }

        Vector3 home = transform.position;
        transform.position = stage;
        SkinnedMeshRenderer[] skins = GetComponentsInChildren<SkinnedMeshRenderer>();
        Mesh baked = new Mesh();
        Camera cam = new GameObject("FilmstripCamera").AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.15f, 0.16f, 0.18f);
        cam.enabled = false;
        RenderTexture rt = new RenderTexture(360, 400, 24);
        cam.targetTexture = rt;
        Texture2D tex = new Texture2D(360, 400, TextureFormat.RGB24, false);
        StringBuilder report = new StringBuilder();
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            animator.Play(clip.name, 0, 0f);
            yield return null;
            report.Append(clip.name.PadRight(18));
            int shot = 0;
            float start = Time.time;
            while (shot < moments.Length && Time.time - start < clip.length * 1.5f + 1f)
            {
                yield return new WaitForEndOfFrame();
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                float t = info.normalizedTime % 1f;
                if (!info.IsName(clip.name) || (clip.isLooping ? t < moments[shot] && info.normalizedTime < 1f : info.normalizedTime < moments[shot]))
                {
                    continue;
                }

                float lowest = float.MaxValue;
                string part = "";
                foreach (SkinnedMeshRenderer skin in skins)
                {
                    skin.BakeMesh(baked, true);
                    Matrix4x4 m = skin.transform.localToWorldMatrix;
                    Vector3[] vs = baked.vertices;
                    BoneWeight[] weights = skin.sharedMesh.boneWeights;
                    for (int i = 0; i < vs.Length; i++)
                    {
                        float y = m.MultiplyPoint3x4(vs[i]).y;
                        if (y < lowest)
                        {
                            lowest = y;
                            part = skin.bones[weights[i].boneIndex0].name.Replace("mixamorig:", "");
                        }
                    }
                }

                float depth = lowest - transform.position.y;
                report.Append(" ").Append(depth.ToString("+0.00;-0.00"));
                if (depth < -0.05f)
                {
                    report.Append("(").Append(part).Append(")");
                }
                Vector3 hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                cam.transform.position = new Vector3(hips.x, transform.position.y + 0.35f, hips.z) + transform.right * 3.8f;
                cam.transform.LookAt(new Vector3(hips.x, transform.position.y + 0.7f, hips.z));
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 360, 400), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(folder, clip.name + "_" + shot + ".png"), tex.EncodeToPNG());
                shot++;
            }

            report.AppendLine();
        }

        File.WriteAllText(Path.Combine(folder, "ground.txt"), report.ToString());
        File.WriteAllText(Path.Combine(folder, "done.txt"), "ok");
        Destroy(cam.gameObject);
        rt.Release();
        transform.position = home;
        if (showcase != null)
        {
            showcase.enabled = true;
        }
    }
}
