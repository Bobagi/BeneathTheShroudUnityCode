using System;
using UnityEngine;

// Interchangeable looks of the player's body on one skeleton (reskins): each skin is a set of renderers skinned
// to the same bones of the MetaHuman, so the animator, the IK, the torch grip and the camera at the eyes do not
// change; only the drawn meshes do. One skin is drawn at a time. Renderers that are shadow-only in first
// person (the head) keep their shadow mode, they are just switched on and off with their skin.
public sealed class BodySkins : MonoBehaviour
{
    [Serializable]
    public sealed class Skin
    {
        public string name;
        public Renderer[] renderers;
        // Bulky clothes (the suit's chest pack, mask and hood) stop the head bowing all the way
        // (GameTuning.bulkyHeadPitch).
        public bool bulky;
    }

    [SerializeField]
    private Skin[] skins = new Skin[0];

    [SerializeField]
    private int current;

    public int Count => skins.Length;
    public int Current => current;
    public string CurrentName => skins.Length > 0 ? skins[current].name : string.Empty;
    public bool CurrentIsBulky => skins.Length > 0 && skins[current].bulky;

    public event Action<BodySkins> Changed;

    private void Awake()
    {
        Apply(current);
    }

    // Called by the scene builders; the skin drawn at start is chosen per scene.
    public void Configure(Skin[] list, int start)
    {
        skins = list;
        Apply(start);
    }

    public void Apply(int index)
    {
        if (skins.Length == 0)
        {
            return;
        }

        current = Mathf.Clamp(index, 0, skins.Length - 1);
        // A renderer can sit in more than one skin (the body's head is in the bare and the clothed ones): every
        // skin's renderers go off first, then the current skin's come on.
        for (int i = 0; i < skins.Length; i++)
        {
            foreach (Renderer r in skins[i].renderers)
            {
                if (r != null)
                {
                    r.enabled = false;
                }
            }
        }
        foreach (Renderer r in skins[current].renderers)
        {
            if (r != null)
            {
                r.enabled = true;
            }
        }

        Changed?.Invoke(this);
    }

    public void Apply(string skinName)
    {
        for (int i = 0; i < skins.Length; i++)
        {
            if (skins[i].name == skinName)
            {
                Apply(i);
                return;
            }
        }
    }

    public void Next()
    {
        if (skins.Length > 0)
        {
            Apply((current + 1) % skins.Length);
        }
    }
}
