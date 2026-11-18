using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Inspection renders of the generated house, outside Play, with flat working light and no fog (the scene is a dark
// night: for geometry one needs to see the form): a top-down view of each room from just under its ceiling (the
// ceiling slab is above the camera, so the furniture and the walls' tops show), and eye-height views from a corner
// of each room. Written as PNGs. HouseShots.Rooms(folder) / HouseShots.Shot(folder, name, eye, target, fov).
public static class HouseShots
{

    private struct Room
    {
        public string name;
        public Rect rect;
        public float floor, ceiling;
    }

    private static Room[] AllRooms()
    {
        const float GC = LunaManor.GroundCeiling, UF = LunaManor.UpperFloor, UC = LunaManor.UpperCeiling;
        return new[]
        {
            new Room { name = "hall", rect = LunaManor.Hall, floor = 0f, ceiling = GC },
            new Room { name = "jantar", rect = LunaManor.Jantar, floor = 0f, ceiling = GC },
            new Room { name = "estar", rect = LunaManor.Estar, floor = 0f, ceiling = GC },
            new Room { name = "cozinha", rect = LunaManor.Cozinha, floor = 0f, ceiling = GC },
            new Room { name = "despensa", rect = LunaManor.Despensa, floor = 0f, ceiling = GC },
            new Room { name = "loucas", rect = LunaManor.Loucas, floor = 0f, ceiling = GC },
            new Room { name = "patio", rect = LunaManor.Courtyard, floor = 0f, ceiling = 20f },
            new Room { name = "rotunda", rect = LunaManor.Rotunda, floor = 0f, ceiling = GC },
            new Room { name = "energia", rect = LunaManor.Caixa, floor = 0f, ceiling = GC },
            new Room { name = "lavanderia", rect = LunaManor.Lavanderia, floor = 0f, ceiling = GC },
            new Room { name = "biblioteca", rect = LunaManor.Biblioteca, floor = 0f, ceiling = GC },
            new Room { name = "passagem", rect = LunaManor.Passagem, floor = 0f, ceiling = GC },
            new Room { name = "bunker", rect = LunaManor.Bunker, floor = 0f, ceiling = GC },
            new Room { name = "deposito", rect = LunaManor.Deposito, floor = 0f, ceiling = GC },
            new Room { name = "garagem", rect = LunaManor.Garagem, floor = 0f, ceiling = GC },
            new Room { name = "galeria_oeste", rect = LunaManor.GalleryWest, floor = UF, ceiling = UC },
            new Room { name = "galeria_norte", rect = LunaManor.GalleryNorth, floor = UF, ceiling = UC },
            new Room { name = "galeria_leste", rect = LunaManor.GalleryEast, floor = UF, ceiling = UC },
            new Room { name = "solteiro1", rect = LunaManor.Solteiro1, floor = UF, ceiling = UC },
            new Room { name = "solteiro2", rect = LunaManor.Solteiro2, floor = UF, ceiling = UC },
            new Room { name = "gerador1", rect = LunaManor.Gerador1, floor = UF, ceiling = UC },
            new Room { name = "banheiro1", rect = LunaManor.Banheiro1, floor = UF, ceiling = UC },
            new Room { name = "casal1", rect = LunaManor.Casal1, floor = UF, ceiling = UC },
            new Room { name = "sacada", rect = LunaManor.SacadaPassage, floor = UF, ceiling = UC },
            new Room { name = "casal2", rect = LunaManor.Casal2, floor = UF, ceiling = UC },
            new Room { name = "banheiro2", rect = LunaManor.Banheiro2, floor = UF, ceiling = UC },
            new Room { name = "gerador2", rect = LunaManor.Gerador2, floor = UF, ceiling = UC },
            new Room { name = "solteiro3", rect = LunaManor.Solteiro3, floor = UF, ceiling = UC },
            new Room { name = "quarto_alex", rect = LunaManor.QuartoAlex, floor = UF, ceiling = UC },
        };
    }

    private static Vector3 M(float x, float y, float z) => FloodedHouseBuilder.PlanToWorld(new Vector3(x, y, z));

    public static string Rooms(string folder, bool corners = true)
    {
        Directory.CreateDirectory(folder);
        List<string> done = new List<string>();
        using (new WorkLight())
        {
            Camera cam = MakeCamera();
            // A warm-up render: the first frame after objects were created can miss them (GPU Resident Drawer).
            Render(cam, Path.Combine(folder, "_warmup.png"), 256, 256);
            foreach (Room r in AllRooms())
            {
                Rect rc = r.rect;
                Vector3 centre = M(rc.center.x, r.ceiling - 0.12f, rc.center.y);
                cam.transform.position = centre;
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(rc.width, rc.height) * 0.5f * FloodedHouseBuilder.PlanScale + 0.3f;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 12f;
                Render(cam, Path.Combine(folder, r.name + "_top.png"), 1024, 1024);
                done.Add(r.name);
                if (!corners)
                {
                    continue;
                }

                // Two eye-height views across the room, from opposite corners (0.4 m in from the corner).
                cam.orthographic = false;
                cam.fieldOfView = 80f;
                cam.farClipPlane = 60f;
                Vector3 a = M(rc.xMin + 0.45f, r.floor + 1.6f, rc.yMin + 0.45f), b = M(rc.xMax - 0.45f, r.floor + 1.6f, rc.yMax - 0.45f);
                cam.transform.position = a;
                cam.transform.rotation = Quaternion.LookRotation(new Vector3(b.x, r.floor + 1.2f, b.z) - a);
                Render(cam, Path.Combine(folder, r.name + "_sw.png"), 1024, 640);
                cam.transform.position = b;
                cam.transform.rotation = Quaternion.LookRotation(new Vector3(a.x, r.floor + 1.2f, a.z) - b);
                Render(cam, Path.Combine(folder, r.name + "_ne.png"), 1024, 640);
            }

            Object.DestroyImmediate(cam.gameObject);
        }

        return string.Join(",", done);
    }

    // One perspective shot in local plan coordinates (eye and target), with the working light.
    public static string Shot(string path, Vector3 eyeLocal, Vector3 targetLocal, float fov = 60f, int w = 1280, int h = 800)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (new WorkLight())
        {
            Camera cam = MakeCamera();
            cam.fieldOfView = fov;
            Vector3 eye = M(eyeLocal.x, eyeLocal.y, eyeLocal.z);
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation(M(targetLocal.x, targetLocal.y, targetLocal.z) - eye);
            Render(cam, Path.Combine(Path.GetDirectoryName(path), "_warmup.png"), 256, 256);
            Render(cam, path, w, h);
            Object.DestroyImmediate(cam.gameObject);
        }

        return path;
    }

    private static Camera MakeCamera()
    {
        GameObject go = new GameObject("HouseShotsCamera");
        go.hideFlags = HideFlags.DontSave;
        Camera cam = go.AddComponent<Camera>();
        // A real game camera with URP data: a hidden ad-hoc camera rendered only the particles and emissive quads
        // (the GPU Resident Drawer skipped it), measured 22/10.
        cam.cameraType = CameraType.Game;
        go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
        cam.allowHDR = true;
        return cam;
    }

    private static void Render(Camera cam, string path, int w, int h)
    {
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    // Flat light for inspection: fog off, bright ambient, a sun from above, every Volume off; all restored on dispose.
    private sealed class WorkLight : System.IDisposable
    {
        private readonly bool fog;
        private readonly AmbientMode ambientMode;
        private readonly Color ambient;
        private readonly float ambientIntensity;
        private readonly List<Volume> volumes = new List<Volume>();
        private readonly GameObject sun;

        public WorkLight()
        {
            fog = RenderSettings.fog;
            ambientMode = RenderSettings.ambientMode;
            ambient = RenderSettings.ambientLight;
            ambientIntensity = RenderSettings.ambientIntensity;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
            foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
            {
                if (v.enabled)
                {
                    v.enabled = false;
                    volumes.Add(v);
                }
            }

            sun = new GameObject("HouseShotsSun");
            sun.hideFlags = HideFlags.HideAndDontSave;
            Light l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            l.color = new Color(1f, 0.97f, 0.92f);
            l.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(62f, 35f, 0f);
        }

        public void Dispose()
        {
            RenderSettings.fog = fog;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientIntensity = ambientIntensity;
            foreach (Volume v in volumes)
            {
                v.enabled = true;
            }

            Object.DestroyImmediate(sun);
        }
    }
}
