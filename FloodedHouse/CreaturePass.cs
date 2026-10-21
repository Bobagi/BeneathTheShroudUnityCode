using System.Collections;
using UnityEngine;

// Placeholder for the creature's first appearance: a long dark body that crosses the corridor once,
// fast, cutting through the torch beam, and is gone. No AI. The segments trail the head like a
// chain so the body snakes; a low, slowed-down groan and a wash of bubbles sell the size.
public sealed class CreaturePass : MonoBehaviour
{
    [SerializeField]
    private Transform[] segments;

    [SerializeField]
    private Vector3 startPoint;

    [SerializeField]
    private Vector3 endPoint;

    [SerializeField]
    private float duration = 2.6f;

    [SerializeField]
    private float segmentSpacing = 0.55f;

    [SerializeField]
    private float undulation = 0.35f;

    [SerializeField]
    private AudioSource voice;

    [SerializeField]
    private AudioClip groan;

    [SerializeField]
    private DreadDirector dread;

    public bool HasPlayed { get; private set; }

    private bool passing;

    private void Awake()
    {
        SetVisible(false);
    }

    // For passes that repeat (the shapes at the windows after the blackout).
    public void PlayAgain()
    {
        if (!passing)
        {
            HasPlayed = false;
            Play();
        }
    }

    public void Play()
    {
        if (HasPlayed)
        {
            return;
        }

        HasPlayed = true;
        StartCoroutine(Pass());
    }

    private IEnumerator Pass()
    {
        Vector3 direction = (endPoint - startPoint).normalized;
        Vector3 side = Vector3.Cross(direction, Vector3.up).normalized;
        int count = segments.Length;
        Vector3[] trail = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            trail[i] = startPoint - direction * segmentSpacing * i;
        }

        passing = true;
        SetVisible(true);
        if (voice != null && groan != null)
        {
            voice.transform.position = Vector3.Lerp(startPoint, endPoint, 0.5f);
            voice.pitch = 0.55f;
            voice.PlayOneShot(groan, 1f);
            voice.PlayOneShot(ProceduralAudio.Bubbles(), 0.8f);
        }

        if (dread != null)
        {
            dread.Provoke(1f);
        }

        float length = segmentSpacing * count;
        float total = Vector3.Distance(startPoint, endPoint) + length;
        for (float travelled = 0f; travelled < total; travelled += total / duration * Time.deltaTime)
        {
            Vector3 head = startPoint + direction * travelled + side * Mathf.Sin(travelled * 1.7f) * undulation;
            trail[0] = head;
            for (int i = 1; i < count; i++)
            {
                Vector3 offset = trail[i] - trail[i - 1];
                trail[i] = trail[i - 1] + offset.normalized * segmentSpacing;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 ahead = i == 0 ? direction : (trail[i - 1] - trail[i]).normalized;
                segments[i].SetPositionAndRotation(trail[i], Quaternion.LookRotation(ahead, Vector3.up));
            }

            yield return null;
        }

        SetVisible(false);
        passing = false;
    }

    private void SetVisible(bool visible)
    {
        foreach (Transform segment in segments)
        {
            segment.gameObject.SetActive(visible);
        }
    }
}
