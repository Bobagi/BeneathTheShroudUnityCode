using UnityEngine;

// Where the spores are too thick to walk into without the mask: a solid stop (so nobody dies of it by
// accident before having any protection) that says why when touched, and coughs. Lifted for good once the
// player has the mask.
public sealed class SporeBarrier : MonoBehaviour
{
    [SerializeField]
    private Collider wall;

    [SerializeField]
    private Respirator respirator;

    [SerializeField]
    private DiveHud hud;

    [SerializeField]
    private Transform player;

    [SerializeField]
    private float warnDistance = 0.8f;

    private float nextWarning;

    private void Update()
    {
        if (respirator.HasMask)
        {
            wall.enabled = false;
            enabled = false;
            return;
        }

        Vector3 closest = wall.ClosestPoint(player.position + Vector3.up);
        if (Vector3.Distance(closest, player.position + Vector3.up) < warnDistance && Time.time > nextWarning)
        {
            nextWarning = Time.time + 6f;
            hud.ShowMessage(Loc.T("msg.spores_wall"), 4f);
            AudioSource.PlayClipAtPoint(SoundLibrary.Instance.Cough(), player.position + Vector3.up * 1.5f, 0.6f);
        }
    }
}
