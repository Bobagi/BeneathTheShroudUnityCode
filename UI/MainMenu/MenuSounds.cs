using UnityEngine;

// The menu's own small sounds (Gustavo 10/10: Kenney's click_004 for the ordinary entries, moving, choosing and
// going back; a sound of its own for starting or loading a game). One 2D source on the menu; buttons call the
// static methods.
public sealed class MenuSounds : MonoBehaviour
{
    [SerializeField]
    private AudioSource source;

    [SerializeField]
    private AudioClip move;

    [SerializeField]
    private AudioClip confirm;

    [SerializeField]
    private AudioClip back;

    [Tooltip("Starting a new game or loading a save (falls back to Confirm when empty).")]
    [SerializeField]
    private AudioClip start;

    [SerializeField]
    [Range(0f, 1f)]
    private float startVolume = 0.8f;

    [SerializeField]
    [Range(0f, 1f)]
    private float moveVolume = 0.18f;

    [SerializeField]
    [Range(0f, 1f)]
    private float confirmVolume = 0.45f;

    private static MenuSounds instance;
    private static int silentUntilFrame;

    private void Awake()
    {
        instance = this;
    }

    // Selection set by code (a page opening) makes no tick.
    public static void SilenceThisFrame()
    {
        silentUntilFrame = Time.frameCount + 1;
    }

    public static void Move()
    {
        if (instance != null && Time.frameCount > silentUntilFrame)
        {
            instance.Play(instance.move, instance.moveVolume, Random.Range(0.94f, 1.06f));
        }
    }

    public static void Confirm()
    {
        if (instance != null)
        {
            instance.Play(instance.confirm, instance.confirmVolume, Random.Range(0.97f, 1.03f));
        }
    }

    public static void StartGame()
    {
        if (instance != null)
        {
            instance.Play(instance.start != null ? instance.start : instance.confirm, instance.start != null ? instance.startVolume : instance.confirmVolume, 1f);
        }
    }

    public static void Back()
    {
        if (instance != null)
        {
            instance.Play(instance.back != null ? instance.back : instance.confirm, instance.confirmVolume * 0.8f, 0.9f);
        }
    }

    private void Play(AudioClip clip, float volume, float pitch)
    {
        if (source == null || clip == null)
        {
            return;
        }

        source.pitch = pitch;
        source.PlayOneShot(clip, volume);
    }
}
