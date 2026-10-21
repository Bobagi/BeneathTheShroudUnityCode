using UnityEngine;

// Everything outside the house (rocks, mangrove, sea, rain) and the sky lights (moon, lightning, window
// fills) only matter on the ground floor. Once the eye is under the basement water line (the lab, the
// sunken bedroom, the air pocket) none of it can be seen, yet the moon and lightning shadow cascades
// would keep drawing it every frame. So below that line the exterior is switched off and the sky lights
// with it. Camera frustum culling alone does not cover shadow passes; this does.
public sealed class SceneryCulling : MonoBehaviour
{
    [SerializeField]
    private Transform eye;

    [SerializeField]
    private GameObject[] exterior;

    [SerializeField]
    private Light[] skyLights;

    [Tooltip("World height under which nothing outside can be seen (just under the basement water line).")]
    [SerializeField]
    private float hideBelowY = -2.9f;

    [Tooltip("Band around the line so hovering at the surface does not flip it every frame.")]
    [SerializeField]
    private float hysteresis = 0.2f;

    private bool hidden;

    public bool ExteriorHidden => hidden;

    private void LateUpdate()
    {
        if (eye == null)
        {
            return;
        }

        float y = eye.position.y;
        if (!hidden && y < hideBelowY - hysteresis)
        {
            Apply(true);
        }
        else if (hidden && y > hideBelowY + hysteresis)
        {
            Apply(false);
        }
    }

    private void Apply(bool hide)
    {
        hidden = hide;
        foreach (GameObject root in exterior)
        {
            if (root != null)
            {
                root.SetActive(!hide);
            }
        }

        foreach (Light sky in skyLights)
        {
            if (sky != null)
            {
                sky.enabled = !hide;
            }
        }
    }
}
