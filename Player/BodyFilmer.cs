using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Test tool: films the player's body from outside while a recorded body clip plays (Editor/HandMocap/RecordedBody),
// from the front, the side and close to the face, and measures every frame: the feet against the ground and the left
// hand against the mouth, plus the first person view (the main camera) with where the hand sits on screen. Started from code in Play (MCP): AddComponent<BodyFilmer>().Run(folder, seconds, onStart).
// The first person head only casts shadows; while filming it is drawn so the outside cameras see the face.
public sealed class BodyFilmer : MonoBehaviour
{
    public bool Done { get; private set; }
    public string Summary { get; private set; } = "";

    public void Run(string folder, float seconds, System.Action onStart, int every = 3)
    {
        StartCoroutine(Film(folder, seconds, onStart, every));
    }

    private IEnumerator Film(string folder, float seconds, System.Action onStart, int every)
    {
        Directory.CreateDirectory(folder);
        PlayerBody body = FindAnyObjectByType<PlayerBody>();
        FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
        Animator animator = body.GetComponent<Animator>();
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal) ?? animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform[] feet =
        {
            animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot),
            animator.GetBoneTransform(HumanBodyBones.LeftToes), animator.GetBoneTransform(HumanBodyBones.RightToes),
        };
        // The mouth: 7.5 cm under the eye the camera rides, kept on the head.
        Vector3 mouthLocal = head.InverseTransformPoint(body.EyePoint - body.transform.up * 0.075f);

        Renderer[] shadowOnly = System.Array.FindAll(body.GetComponentsInChildren<Renderer>(), r => r.enabled && r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly);

        Camera[] cams = new Camera[3];
        string[] names = { "frente", "perfil", "perto" };
        RenderTexture rt = new RenderTexture(720, 960, 24);
        Texture2D read = new Texture2D(720, 960, TextureFormat.RGB24, false);
        for (int i = 0; i < 3; i++)
        {
            cams[i] = new GameObject("Filmer_" + names[i]).AddComponent<Camera>();
            cams[i].enabled = false;
            cams[i].targetTexture = rt;
            cams[i].fieldOfView = i == 2 ? 40f : 32f;
            cams[i].nearClipPlane = 0.05f;
        }

        Camera eye = Camera.main;
        RenderTexture eyeRt = new RenderTexture(1280, 720, 24);
        Texture2D eyeRead = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        float bestScreen = float.MaxValue;
        Vector3 bestView = Vector3.zero;

        StringBuilder csv = new StringBuilder("quadro;tempo;pe_mais_baixo_m;pe_mais_alto_m;mao_boca_m;mao_tela_x;mao_tela_y\n");
        float ground = player.transform.position.y;
        float minHand = float.MaxValue, lowFoot = float.MaxValue, highFoot = float.MinValue;
        onStart?.Invoke();
        float start = Time.time;
        int frame = 0;
        while (Time.time - start < seconds)
        {
            yield return null;
            Vector3 fwd = body.transform.forward, up = Vector3.up, right = body.transform.right;
            Vector3 chest = body.transform.position + up * 1.2f;
            Vector3 mouth = head.TransformPoint(mouthLocal);
            float handMouth = Vector3.Distance(hand.position, mouth);
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (Transform f in feet)
            {
                if (f != null)
                {
                    lo = Mathf.Min(lo, f.position.y - ground);
                    hi = Mathf.Max(hi, f.position.y - ground);
                }
            }
            minHand = Mathf.Min(minHand, handMouth);
            lowFoot = Mathf.Min(lowFoot, lo);
            highFoot = Mathf.Max(highFoot, lo);
            // Where the hand is on the first person screen (0 to 1; 0.5, 0.5 is the middle; z below 0 = behind the lens).
            Vector3 view = eye.WorldToViewportPoint(hand.position);
            if (view.z > 0f && handMouth < 0.5f)
            {
                float off = Mathf.Abs(view.x - 0.5f) + Mathf.Abs(view.y - 0.5f);
                if (off < bestScreen)
                {
                    bestScreen = off;
                    bestView = view;
                }
            }
            csv.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0};{1:F3};{2:F4};{3:F4};{4:F4};{5:F3};{6:F3}", frame, Time.time - start, lo, hi, handMouth, view.z > 0f ? view.x : -1f, view.z > 0f ? view.y : -1f));
            if (frame % every == 0)
            {
                cams[0].transform.SetPositionAndRotation(chest - up * 0.25f + fwd * 3.2f, Quaternion.LookRotation(-fwd, up));
                // From the body's left side (the cough hand), looking across it.
                cams[1].transform.SetPositionAndRotation(chest - up * 0.25f - right * 3.2f, Quaternion.LookRotation(right, up));
                Vector3 face = head.position;
                Vector3 closeAt = face + fwd * 0.75f - right * 0.35f - up * 0.1f;
                cams[2].transform.SetPositionAndRotation(closeAt, Quaternion.LookRotation(face - up * 0.12f - closeAt, up));
                // Drawn only for these renders (other scripts set the first person head back every frame).
                foreach (Renderer r in shadowOnly)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                for (int i = 0; i < 3; i++)
                {
                    cams[i].Render();
                    RenderTexture.active = rt;
                    read.ReadPixels(new Rect(0, 0, 720, 960), 0, 0);
                    read.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(folder, names[i] + "_" + frame.ToString("000") + ".png"), read.EncodeToPNG());
                }
                foreach (Renderer r in shadowOnly)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
                RenderTexture keep = eye.targetTexture;
                eye.targetTexture = eyeRt;
                eye.Render();
                eye.targetTexture = keep;
                RenderTexture.active = eyeRt;
                eyeRead.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                eyeRead.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(folder, "primeira_" + frame.ToString("000") + ".png"), eyeRead.EncodeToPNG());
            }
            frame++;
        }
        File.WriteAllText(Path.Combine(folder, "medidas.csv"), csv.ToString());
        foreach (Camera c in cams)
        {
            Destroy(c.gameObject);
        }
        rt.Release();
        eyeRt.Release();
        Summary = string.Format(CultureInfo.InvariantCulture, "{0} quadros; mao-boca minima {1:F3} m; pe mais baixo {2:F3} m, mais alto (do pe de apoio) {3:F3} m; mao na tela mais central ({4:F2}, {5:F2}){6}",
            frame, minHand, lowFoot, highFoot, bestView.x, bestView.y, bestScreen == float.MaxValue ? " (nunca na tela)" : "");
        Done = true;
    }
}
