using UnityEngine;

// Carries the GameTuning values of Dayse's growths (16/11) to their materials: the eggs' inner glow and how much of
// the embryo shows through the shell, the glow of the blue and of the yellow fungus. Listens to GameTuning.Changed,
// so the sliders act at once, in Play Mode and out of it. One of these sits on the house's flora group; the builder
// hands it the materials.
[ExecuteAlways]
public sealed class TunedGrowths : MonoBehaviour
{
    private static readonly int Glow = Shader.PropertyToID("_Glow");
    private static readonly int Opacity = Shader.PropertyToID("_Opacity");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    [SerializeField]
    private Material[] eggShells = new Material[0];

    [SerializeField]
    private Material[] blueFungus = new Material[0];

    [SerializeField]
    private Material[] yellowFungus = new Material[0];

    [Tooltip("Colour of the blue fungus's light (the texture's bright spots are multiplied by it and by the tuned glow).")]
    [SerializeField]
    private Color blueTint = new Color(0.35f, 0.75f, 1f);

    [SerializeField]
    private Color yellowTint = new Color(0.9f, 1f, 0.35f);

    public void Configure(Material[] eggs, Material[] blue, Material[] yellow)
    {
        eggShells = eggs;
        blueFungus = blue;
        yellowFungus = yellow;
        Apply();
    }

    private void OnEnable()
    {
        GameTuning.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameTuning.Changed -= Apply;
    }

    private void Apply()
    {
        GameTuning tuning = GameTuning.Instance;
        if (tuning == null)
        {
            return;
        }

        foreach (Material m in eggShells)
        {
            if (m != null)
            {
                m.SetFloat(Glow, tuning.eggGlow);
                m.SetFloat(Opacity, tuning.eggOpacity);
            }
        }

        foreach (Material m in blueFungus)
        {
            if (m != null)
            {
                m.SetColor(EmissionColor, blueTint * tuning.blueFungusGlow);
            }
        }

        foreach (Material m in yellowFungus)
        {
            if (m != null)
            {
                m.SetColor(EmissionColor, yellowTint * tuning.yellowFungusGlow);
            }
        }
    }
}
