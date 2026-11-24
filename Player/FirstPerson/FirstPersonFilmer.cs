using System.Collections;
using System.IO;
using UnityEngine;

// Test tool: films every first person animation state the way the game plays it, into a folder of PNGs, to judge
// them by looking (and to measure that the camera stays still while the feet adapt to the floor). Started from code
// in Play (MCP): new GameObject().AddComponent<FirstPersonFilmer>().Run(folder). Each shot is the player's view
// (fp_<name>), a close view of the hands from the right (cr_<name>) and the outside view of the body (out_<name>)
// when the test scene has one (OutsideView).
public sealed class FirstPersonFilmer : MonoBehaviour
{
    public bool Done { get; private set; }
    public string Log { get; private set; } = "";

    private string folder;
    private Camera eye;
    private Camera side;
    private RenderTexture rt;
    private Texture2D tex;
    private OutsideView outside;

    public void Run(string outputFolder)
    {
        folder = outputFolder;
        Directory.CreateDirectory(folder);
        StartCoroutine(Film());
    }

    // Only the run with the revolver: a burst of frames across the step cycle (the arms pumping with the gun).
    public void RunSprint(string outputFolder)
    {
        folder = outputFolder;
        Directory.CreateDirectory(folder);
        StartCoroutine(FilmSprint());
    }

    private IEnumerator FilmSprint()
    {
        Application.runInBackground = true;
        Time.captureFramerate = 30;
        FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
        Inventory inventory = FindAnyObjectByType<Inventory>();
        outside = FindAnyObjectByType<OutsideView>();
        eye = Camera.main;
        side = new GameObject("FilmCamera").AddComponent<Camera>();
        side.CopyFrom(eye);
        side.enabled = false;
        side.fieldOfView = 32f;
        side.nearClipPlane = 0.03f;
        rt = new RenderTexture(1280, 720, 24);
        tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        // Facing the open side of the test floor (the wall stands at z 6).
        controller.Teleport(new Vector3(0f, 0f, -1.5f), -90f);
        inventory.Equip(Inventory.Hand.Revolver, true);
        yield return Frames(30);
        controller.TestSprint = true;
        yield return Frames(60);
        for (int i = 0; i < 8; i++)
        {
            Shot("sprint_" + i);
            yield return Frames(4);
        }

        controller.TestSprint = false;
        yield return Frames(30);
        Shot("sprint_stopped");
        Time.captureFramerate = 0;
        Done = true;
        Log += "sprint filmed; saved to " + folder + "\n";
    }

    private IEnumerator Film()
    {
        Application.runInBackground = true;
        Time.captureFramerate = 30;
        FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
        PlayerBody body = FindAnyObjectByType<PlayerBody>();
        Inventory inventory = FindAnyObjectByType<Inventory>();
        Revolver revolver = FindAnyObjectByType<Revolver>(FindObjectsInactive.Include);
        TorchHold torch = FindAnyObjectByType<TorchHold>(FindObjectsInactive.Include);
        RecordedHandsTest take = FindAnyObjectByType<RecordedHandsTest>();
        outside = FindAnyObjectByType<OutsideView>();
        eye = Camera.main;
        side = new GameObject("FilmCamera").AddComponent<Camera>();
        side.CopyFrom(eye);
        side.enabled = false;
        side.fieldOfView = 32f;
        side.nearClipPlane = 0.03f;
        rt = new RenderTexture(1280, 720, 24);
        tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        controller.LookAt(eye.transform.position + Vector3.ProjectOnPlane(eye.transform.forward, Vector3.up).normalized * 10f);
        yield return Frames(30);

        if (revolver != null && inventory != null)
        {
            inventory.Equip(Inventory.Hand.Revolver, true);
            yield return Frames(60);
            Shot("revolver_hold");
            revolver.HoldAim = true;
            yield return Frames(30);
            Shot("revolver_aim");
            revolver.HoldAim = false;
            yield return Frames(25);
            revolver.SetAmmo(2, 4, 60);
            revolver.Reload();
            int frame = 0;
            while ((revolver.IsReloading || frame < 10) && frame < 150)
            {
                if (frame % 10 == 0)
                {
                    Shot("reload_" + (frame / 30f).ToString("0.00").Replace(',', '.'));
                }

                yield return null;
                frame++;
            }

            Log += "reload filmed over " + frame + " frames\n";
            yield return Frames(20);
            Shot("revolver_after_reload");
        }

        if (inventory != null)
        {
            inventory.Equip(Inventory.Hand.Torch, true);
            yield return Frames(45);
            Shot("torch_hold");
            if (torch != null)
            {
                torch.HoldFocus = true;
                yield return Frames(30);
                Shot("torch_focus");
                torch.HoldFocus = false;
                yield return Frames(20);
            }
        }

        if (body != null)
        {
            body.Cough();
            for (int f = 0; f < 90; f += 15)
            {
                yield return Frames(15);
                Shot("cough_" + (f / 30f + 0.5f).ToString("0.00").Replace(',', '.'));
            }

            yield return Frames(30);
        }

        if (take != null)
        {
            take.Toggle();
            yield return Frames(60);
            Shot("take_2s");
            yield return Frames(120);
            Shot("take_6s");
            yield return Frames(120);
            Shot("take_10s");
            take.Toggle();
            yield return Frames(30);
        }

        // Looking down at the body with the torch, and up.
        if (inventory != null)
        {
            inventory.Equip(Inventory.Hand.Torch, true);
            yield return Frames(20);
            controller.LookAt(eye.transform.position + eye.transform.forward * 2f - Vector3.up * 3f);
            yield return Frames(30);
            Shot("torch_look_down");
            controller.LookAt(eye.transform.position + eye.transform.forward * 2f + Vector3.up * 2f);
            yield return Frames(30);
            Shot("torch_look_up");
            controller.LookAt(eye.transform.position + Vector3.ProjectOnPlane(eye.transform.forward, Vector3.up).normalized * 10f);
            yield return Frames(20);
        }

        // The feet on a step: the camera must not move while the hips drop so both feet reach the floor.
        GameObject step = GameObject.Find("Step");
        if (step != null && body != null)
        {
            Vector3 edge = step.transform.position + new Vector3(step.transform.localScale.x * 0.5f + 0.12f, -step.transform.position.y + 0.0f, 0f);
            edge.y = 0f;
            controller.Teleport(edge, -90f);
            yield return Frames(45);
            float minEye = float.MaxValue, maxEye = float.MinValue, minBody = float.MaxValue, maxBody = float.MinValue;
            for (int f = 0; f < 60; f++)
            {
                float e = eye.transform.position.y;
                float b = body.transform.position.y;
                minEye = Mathf.Min(minEye, e);
                maxEye = Mathf.Max(maxEye, e);
                minBody = Mathf.Min(minBody, b);
                maxBody = Mathf.Max(maxBody, b);
                yield return null;
            }

            Shot("feet_on_step");
            Log += "feet on step: eye y " + minEye.ToString("F4") + " to " + maxEye.ToString("F4") + ", body root y " + minBody.ToString("F4") + " to " + maxBody.ToString("F4")
                + ", player y " + controller.transform.position.y.ToString("F4") + "\n";
        }

        Time.captureFramerate = 0;
        Done = true;
        Log += "saved to " + folder + "\n";
    }

    private static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return null;
        }
    }

    private void Shot(string name)
    {
        Save(eye, Path.Combine(folder, "fp_" + name + ".png"));
        Transform e = eye.transform;
        // The hands from the right, close.
        Vector3 chest = e.position - e.up * 0.2f + e.forward * 0.25f;
        side.transform.position = chest + e.right * 0.7f + e.forward * 0.15f + e.up * 0.1f;
        side.transform.rotation = Quaternion.LookRotation(chest - side.transform.position, Vector3.up);
        Save(side, Path.Combine(folder, "cr_" + name + ".png"));
        if (outside != null && outside.Target != null)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = outside.Target;
            Texture2D o = new Texture2D(outside.Target.width, outside.Target.height, TextureFormat.RGB24, false);
            o.ReadPixels(new Rect(0, 0, outside.Target.width, outside.Target.height), 0, 0);
            o.Apply(false);
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(folder, "out_" + name + ".png"), o.EncodeToPNG());
            Destroy(o);
        }
    }

    private void Save(Camera camera, string path)
    {
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = rt;
        camera.Render();
        camera.targetTexture = null;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply(false);
        RenderTexture.active = previous;
        File.WriteAllBytes(path, tex.EncodeToPNG());
    }
}
