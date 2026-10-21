using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Sets the weapon socket from target positions of the gun's bones in the hand's space (centimetres, hand_r local),
// as written by SourceAssets/Tools/revolver_grip_pack.py from Blender: the rigid transform that carries the gun's
// bones (in the gun's own space) onto the targets is the socket's local pose. Solved from three well-spread bones
// (an orthonormal frame on each side), checked on all of them.
public static class GunSocketFromTargets
{
    [System.Serializable]
    private class Targets
    {
        public List<string> names = new List<string>();
        public List<Vector3> positions = new List<Vector3>();
        public float gunScale = 1f;
    }

    public static string Apply(string jsonPath, bool write)
    {
        // The JSON is {"bones": {name: [x,y,z]}, "gunScale": s}: parsed by hand (JsonUtility has no dictionaries).
        string text = System.IO.File.ReadAllText(jsonPath);
        Targets t = new Targets();
        int bonesAt = text.IndexOf("\"bones\"");
        int end = text.IndexOf("}", bonesAt);
        string body = text.Substring(bonesAt, end - bonesAt);
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(body, "\"([^\"]+)\"\\s*:\\s*\\[\\s*([-0-9.eE+]+)\\s*,\\s*([-0-9.eE+]+)\\s*,\\s*([-0-9.eE+]+)\\s*\\]"))
        {
            t.names.Add(m.Groups[1].Value);
            t.positions.Add(new Vector3(float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture)));
        }

        System.Text.RegularExpressions.Match sm = System.Text.RegularExpressions.Regex.Match(text, "\"gunScale\"\\s*:\\s*([-0-9.eE+]+)");
        if (sm.Success)
        {
            t.gunScale = float.Parse(sm.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        FirstPersonArms arms = Object.FindAnyObjectByType<FirstPersonArms>(FindObjectsInactive.Include);
        Transform socket = arms != null ? arms.transform.Find(FirstPersonRig.SocketPath(FirstPersonArms.WeaponSocketName)) : null;
        Transform gunRoot = socket != null && socket.childCount > 0 ? socket.GetChild(0) : null;
        if (gunRoot == null || t.names.Count < 3)
        {
            Debug.LogError("[GunSocket] falta o revólver sob o socket na cena, ou alvos de menos (" + t.names.Count + ").");
            return null;
        }

        // The gun's bones in the socket's space (what the socket's children are), from the scene instance: the bone
        // transforms relative to the socket, scaled to the hand's units (the socket cancels the hand's centimetres).
        // Only the rig's bones: the prefab also has mesh objects named like bones (Bullet1 the renderer next to
        // Bullet1 the bone), which sit at the root and ruined the fit (81 mm, 10/10).
        Transform[] all = gunRoot.GetComponentsInChildren<Transform>(true).Where(x => x != gunRoot && x.GetComponent<Renderer>() == null).ToArray();
        List<Vector3> src = new List<Vector3>();
        List<Vector3> dst = new List<Vector3>();
        List<string> used = new List<string>();
        Matrix4x4 socketInv = socket.worldToLocalMatrix;
        for (int i = 0; i < t.names.Count; i++)
        {
            // Blender names the gun's root bone after the object ("Revolver"); Unity's object of that name is the
            // prefab root, not the bone, so it is skipped (the other 19 bones are plenty).
            Transform b = all.FirstOrDefault(x => x.name == t.names[i] && x != gunRoot);
            if (b == null || t.names[i] == gunRoot.name)
            {
                continue;
            }

            // Position in the socket's local space (socket units), then into hand units: the socket's local scale
            // (103.6) maps socket units to hand units.
            Vector3 inSocket = socketInv.MultiplyPoint3x4(b.position);
            src.Add(Vector3.Scale(inSocket, socket.localScale));
            dst.Add(t.positions[i]);
            used.Add(t.names[i]);
        }

        if (src.Count < 3)
        {
            Debug.LogError("[GunSocket] só " + src.Count + " ossos em comum.");
            return null;
        }

        // Rigid transform src -> dst from the three most spread points, then checked on all.
        int ia = 0, ib = 0, ic = 0;
        float bestSpread = -1f;
        for (int i = 0; i < src.Count; i++)
        for (int j = i + 1; j < src.Count; j++)
        for (int k = j + 1; k < src.Count; k++)
        {
            float area = Vector3.Cross(src[j] - src[i], src[k] - src[i]).magnitude;
            if (area > bestSpread)
            {
                bestSpread = area;
                ia = i; ib = j; ic = k;
            }
        }

        Quaternion rs = Frame(src[ia], src[ib], src[ic]);
        Quaternion rd = Frame(dst[ia], dst[ib], dst[ic]);
        Quaternion rot = rd * Quaternion.Inverse(rs);
        Vector3 cs = (src[ia] + src[ib] + src[ic]) / 3f;
        Vector3 cd = (dst[ia] + dst[ib] + dst[ic]) / 3f;
        Vector3 trans = cd - rot * cs;
        float worst = 0f;
        for (int i = 0; i < src.Count; i++)
        {
            worst = Mathf.Max(worst, Vector3.Distance(rot * src[i] + trans, dst[i]));
        }

        // src are socket-space points scaled to hand units, so the transform that carries them onto the hand-space
        // targets IS the socket's local pose.
        Vector3 newPos = trans;
        Quaternion newRot = rot;
        string report = "[GunSocket] " + src.Count + " ossos (" + used[ia] + ", " + used[ib] + ", " + used[ic] + " para o referencial), pior desvio " + (worst * 10f).ToString("F1") + " mm; socket " + socket.localPosition.ToString("F2") + " -> " + newPos.ToString("F2") + " cm, rot " + socket.localEulerAngles.ToString("F1") + " -> " + newRot.eulerAngles.ToString("F1");
        Debug.Log(report);
        if (write)
        {
            FirstPersonRig.SetSocket(FirstPersonArms.WeaponSocketName, newPos, newRot);
            // The scene's rig is a prefab instance with per-property overrides on the socket (an earlier manual
            // edit): a saved prefab only reaches the properties without one, so the overrides are dropped first.
            if (PrefabUtility.IsPartOfPrefabInstance(socket))
            {
                PrefabUtility.RevertObjectOverride(socket, InteractionMode.AutomatedAction);
            }

            socket.localPosition = newPos;
            socket.localRotation = newRot;
            Debug.Log("[GunSocket] cena agora " + socket.localPosition.ToString("F2") + " " + socket.localEulerAngles.ToString("F1"));
        }

        return report;
    }

    private static Quaternion Frame(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 x = (b - a).normalized;
        Vector3 z = Vector3.Cross(x, c - a).normalized;
        Vector3 y = Vector3.Cross(z, x);
        return Quaternion.LookRotation(z, y);
    }
}
