using System.Collections.Generic;
using UnityEngine;

// What the player carries (keys, the lamp, oil, matches, the hook, the case) and the archive of what was
// read or heard (recordings, clippings, photos, notes, official notices), which the diary (Tab) lists so nothing has to be
// found twice. Items are plain ids: a door asks HasItem("chave_desumidificador").
public sealed class PlayerItems : MonoBehaviour
{
    public enum EntryKind { Recording, Clipping, Photo, Note, Notice }

    public sealed class Entry
    {
        public string id;
        public string title;
        public EntryKind kind;
        public string body;
        public string signature;
        public Texture photo;
        public Recorder recorder;
    }

    private readonly HashSet<string> items = new HashSet<string>();
    private readonly List<Entry> archive = new List<Entry>();

    public IReadOnlyList<Entry> Archive => archive;

    // What is carried, for the save.
    public IEnumerable<string> Owned => items;

    public event System.Action<string> ItemAdded;
    public event System.Action ArchiveChanged;

    public bool HasItem(string id)
    {
        return !string.IsNullOrEmpty(id) && items.Contains(id);
    }

    public void Add(string id)
    {
        if (!string.IsNullOrEmpty(id) && items.Add(id))
        {
            ItemAdded?.Invoke(id);
        }
    }

    // Loading a checkpoint: carried again without the story reacting to it as a new find.
    public void AddSilently(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            items.Add(id);
        }
    }

    public void Remove(string id)
    {
        items.Remove(id);
    }

    public void Remember(Entry entry)
    {
        for (int i = 0; i < archive.Count; i++)
        {
            if (archive[i].id == entry.id)
            {
                return;
            }
        }

        archive.Add(entry);
        ArchiveChanged?.Invoke();
    }
}
