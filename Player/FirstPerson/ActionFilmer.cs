using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Test tool (13/10): films the first person actions as VIDEO, every frame at a fixed 30 fps (Time.captureFramerate),
// so a whole action (aim in, shots, reload, sprint, the torch) can be watched and measured afterwards. Started in Play
// from code (MCP): new GameObject().AddComponent<ActionFilmer>().Run(folder, "all"). Writes, per take:
//   <take>/fp_0000.jpg ...   the player's view
//   <take>/cr_0000.jpg ...   the hands close from the right, every frame
//   <take>.csv               per frame: the arms' state, where the gun's grip and muzzle and both wrists are in the
//                            eye's space (cm), the bore's angle to the eye's forward, and the ViewModel's own offset.
// SourceAssets/Tools/film_assemble.py turns the folders into mp4s and contact sheets.
[DefaultExecutionOrder(10000)]
public sealed class ActionFilmer : MonoBehaviour
{
    public bool Done { get; private set; }
    public string Log { get; private set; } = "";

    private const int Width = 960;
    private const int Height = 540;

    private string folder;
    private string take;
    private int frame;
    private StringBuilder csv;
    private Camera eye;
    private Camera side;
    private RenderTexture rt;
    private Texture2D tex;
    private FirstPersonController controller;
    private FirstPersonArms arms;
    private Revolver revolver;
    private TorchHold torch;
    private Inventory inventory;
    private bool recording;
    private bool pending;

    private void LateUpdate()
    {
        if (pending)
        {
            pending = false;
            Record();
        }
    }

    public void Run(string outputFolder, string which)
    {
        folder = outputFolder;
        Directory.CreateDirectory(folder);
        StartCoroutine(Film(which ?? "all"));
    }

    private IEnumerator Film(string which)
    {
        Application.runInBackground = true;
        Time.captureFramerate = 30;
        controller = FindAnyObjectByType<FirstPersonController>();
        arms = FindAnyObjectByType<FirstPersonArms>();
        revolver = FindAnyObjectByType<Revolver>(FindObjectsInactive.Include);
        torch = FindAnyObjectByType<TorchHold>(FindObjectsInactive.Include);
        inventory = FindAnyObjectByType<Inventory>();
        eye = Camera.main;
        side = new GameObject("FilmSide").AddComponent<Camera>();
        side.CopyFrom(eye);
        side.enabled = false;
        side.fieldOfView = 34f;
        side.nearClipPlane = 0.03f;
        rt = new RenderTexture(Width, Height, 24);
        tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);

        // At the firing line, facing the middle target.
        controller.Teleport(new Vector3(0f, 0f, -1.2f), 0f);
        yield return Frames(10);
        controller.LookAt(new Vector3(0f, 1.55f, 5.9f));
        yield return Frames(10);

        // which: "all", or any of aim, hip, reload, sprint, draw, torch, torchsprint (or "revolver" for the first five).
        bool all = which == "all";
        bool gun = all || which.Contains("revolver");
        System.Func<string, bool> want = n => gun || which.Split(',', ' ').Contains(n);
        if (want("aim") || want("hip") || want("reload") || want("sprint") || want("draw"))
        {
            inventory.Equip(Inventory.Hand.Revolver, true);
            yield return Frames(30);
            revolver.SetAmmo(6, 0, 60);
        }

        if (want("aim"))
        {
            // Hold, then aim in, three aimed shots, out.
            Begin("revolver_aim_fire");
            yield return Frames(30);
            revolver.HoldAim = true;
            yield return Frames(30);
            for (int i = 0; i < 3; i++)
            {
                revolver.Fire();
                yield return Frames(18);
            }

            yield return Frames(12);
            revolver.HoldAim = false;
            yield return Frames(30);
            End();
        }

        if (want("hip"))
        {
            // Hip fire: two shots without aiming.
            Begin("revolver_hip_fire");
            yield return Frames(15);
            for (int i = 0; i < 2; i++)
            {
                revolver.Fire();
                yield return Frames(20);
            }

            yield return Frames(20);
            End();
        }

        if (want("reload"))
        {
            // Reload with 5 fired.
            revolver.SetAmmo(1, 5, 60);
            Begin("revolver_reload");
            yield return Frames(5);
            revolver.Reload();
            int guard = 0;
            yield return Frames(3);
            while (revolver.IsReloading && guard < 240)
            {
                guard++;
                yield return Frames(1);
            }

            yield return Frames(25);
            End();
        }

        if (want("sprint"))
        {
            // Sprint forward and stop (toward the targets, stopping short of the wall).
            controller.Teleport(new Vector3(-3.6f, 0f, -6f), 0f);
            yield return Frames(10);
            controller.LookAt(new Vector3(-3.6f, 1.5f, 5f));
            yield return Frames(10);
            Begin("revolver_sprint");
            yield return Frames(10);
            controller.TestSprint = true;
            yield return Frames(70);
            controller.TestSprint = false;
            yield return Frames(30);
            End();
            controller.Teleport(new Vector3(0f, 0f, -1.2f), 0f);
            yield return Frames(5);
            controller.LookAt(new Vector3(0f, 1.55f, 5.9f));
            yield return Frames(5);
        }

        if (want("draw"))
        {
            // Draw and holster: torch -> revolver.
            Begin("revolver_draw");
            inventory.Equip(Inventory.Hand.Torch, true);
            yield return Frames(25);
            inventory.Equip(Inventory.Hand.Revolver, true);
            yield return Frames(35);
            End();
        }

        if (all || which.Split(',', ' ').Contains("torch"))
        {
            inventory.Equip(Inventory.Hand.Torch, true);
            yield return Frames(30);
            Begin("torch_focus");
            yield return Frames(30);
            torch.HoldFocus = true;
            yield return Frames(45);
            torch.HoldFocus = false;
            yield return Frames(30);
            End();
        }

        if (all || which.Split(',', ' ').Contains("torchsprint"))
        {
            inventory.Equip(Inventory.Hand.Torch, true);
            controller.Teleport(new Vector3(-3.6f, 0f, -6f), 0f);
            yield return Frames(10);
            controller.LookAt(new Vector3(-3.6f, 1.5f, 5f));
            yield return Frames(10);
            Begin("torch_sprint");
            yield return Frames(10);
            controller.TestSprint = true;
            yield return Frames(70);
            controller.TestSprint = false;
            yield return Frames(30);
            End();
            controller.Teleport(new Vector3(0f, 0f, -1.2f), 0f);
        }

        Time.captureFramerate = 0;
        Destroy(side.gameObject);
        Done = true;
        Log += "saved to " + folder + "\n";
    }

    private void Begin(string name)
    {
        take = name;
        frame = 0;
        Directory.CreateDirectory(Path.Combine(folder, take));
        csv = new StringBuilder("frame,state,aim,gunX,gunY,gunZ,muzX,muzY,muzZ,boreDeg,rX,rY,rZ,lX,lY,lZ,vmX,vmY,vmZ\n");
        recording = true;
    }

    private void End()
    {
        if (pending)
        {
            pending = false;
            Record();
        }

        recording = false;
        File.WriteAllText(Path.Combine(folder, take + ".csv"), csv.ToString());
        Log += take + ": " + frame + " frames\n";
    }

    private IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            // Recorded in this object's LateUpdate, after every other (the camera's own animation, CameraTilt, is
            // applied in LateUpdate and the controller resets the look in the next Update).
            yield return null;
            pending = recording;
        }
    }

    private void Record()
    {
        string n = frame.ToString("0000");
        Save(eye, Path.Combine(folder, take, "fp_" + n + ".jpg"));
        Transform e = eye.transform;
        Vector3 chest = e.position - e.up * 0.15f + e.forward * 0.3f;
        side.transform.position = chest + e.right * 0.75f + e.up * 0.1f + e.forward * 0.05f;
        side.transform.rotation = Quaternion.LookRotation(chest - side.transform.position, e.up);
        Save(side, Path.Combine(folder, take, "cr_" + n + ".jpg"));
        // And from the left, a little ahead (the left hand's side: support grip, speedloader).
        side.transform.position = chest - e.right * 0.6f + e.up * 0.2f + e.forward * 0.35f;
        side.transform.rotation = Quaternion.LookRotation(chest + e.forward * 0.05f - side.transform.position, e.up);
        Save(side, Path.Combine(folder, take, "cl_" + n + ".jpg"));

        CultureInfo ci = CultureInfo.InvariantCulture;
        string state = "?";
        if (arms != null)
        {
            AnimatorStateInfo s = arms.Animator.GetCurrentAnimatorStateInfo(0);
            foreach (string name in new[] { "Empty", "Torch_Hold", "Torch_Focus", "Revolver_Hold", "Revolver_Aim", "Revolver_Reload", "Revolver_Sprint", "Revolver_Fire" })
            {
                if (s.IsName(name))
                {
                    state = name;
                }
            }

            if (arms.Animator.IsInTransition(0))
            {
                state += ">";
            }
        }

        Vector3 gun = Vector3.zero, muz = Vector3.zero;
        float bore = 0f;
        Transform held = arms != null && arms.Current == FirstPersonArms.Hands.Torch && torch != null ? torch.transform
            : revolver != null ? revolver.transform : null;
        if (held != null)
        {
            gun = e.InverseTransformPoint(held.position) * 100f;
            Transform tip = revolver != null && held == revolver.transform ? revolver.Muzzle : null;
            muz = tip != null ? e.InverseTransformPoint(tip.position) * 100f : gun;
            Vector3 forward = tip != null ? tip.forward : held.forward;
            bore = Vector3.Angle(forward, e.forward);
        }

        Vector3 r = Vector3.zero, l = Vector3.zero;
        if (arms != null)
        {
            Transform hr = arms.Bone("hand_r"), hl = arms.Bone("hand_l");
            r = hr != null ? e.InverseTransformPoint(hr.position) * 100f : r;
            l = hl != null ? e.InverseTransformPoint(hl.position) * 100f : l;
        }

        Vector3 vm = arms != null ? arms.transform.parent.localPosition * 100f : Vector3.zero;
        csv.Append(frame).Append(',').Append(state).Append(',').Append((revolver != null ? revolver.Aim : 0f).ToString("F2", ci));
        foreach (Vector3 v in new[] { gun, muz })
        {
            csv.Append(',').Append(v.x.ToString("F2", ci)).Append(',').Append(v.y.ToString("F2", ci)).Append(',').Append(v.z.ToString("F2", ci));
        }

        csv.Append(',').Append(bore.ToString("F2", ci));
        foreach (Vector3 v in new[] { r, l, vm })
        {
            csv.Append(',').Append(v.x.ToString("F2", ci)).Append(',').Append(v.y.ToString("F2", ci)).Append(',').Append(v.z.ToString("F2", ci));
        }

        csv.Append('\n');
        frame++;
    }

    private void Save(Camera camera, string path)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = camera.targetTexture;
        camera.targetTexture = rt;
        camera.Render();
        camera.targetTexture = target;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply(false);
        RenderTexture.active = previous;
        File.WriteAllBytes(path, tex.EncodeToJPG(88));
    }
}
