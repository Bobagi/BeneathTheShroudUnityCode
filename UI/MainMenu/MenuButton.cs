using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A main menu entry: plain spaced capitals that warm up to the lantern's colour when selected and open their letter
// spacing a little, with a short flicker as they light, like a wick catching (no underline: Gustavo 10/10). The
// mouse selects by hovering, so mouse, keyboard and gamepad share one highlight. Text from Loc through the label's LocalizedText.
[RequireComponent(typeof(Button))]
public sealed class MenuButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [SerializeField]
    private TMP_Text label;

    [SerializeField]
    private Color idle = new Color(0.78f, 0.74f, 0.66f, 0.82f);

    [SerializeField]
    private Color active = new Color(1f, 0.8f, 0.52f, 1f);

    [SerializeField]
    private Color disabled = new Color(0.5f, 0.48f, 0.44f, 0.45f);

    [SerializeField]
    private float spacingGrow = 5f;

    [SerializeField]
    private bool backButton;

    [Tooltip("Starts or loads a game: plays the start sound instead of the ordinary click.")]
    [SerializeField]
    private bool startsGame;

    private Button button;
    private float baseSpacing;
    private float lit;
    private bool selected;
    private float selectedAt;

    public Button Button => button != null ? button : (button = GetComponent<Button>());

    public TMP_Text Label => label;

    private void Awake()
    {
        button = GetComponent<Button>();
        baseSpacing = label != null ? label.characterSpacing : 0f;
        Button.onClick.AddListener(() =>
        {
            if (startsGame)
            {
                MenuSounds.StartGame();
            }
            else if (backButton)
            {
                MenuSounds.Back();
            }
            else
            {
                MenuSounds.Confirm();
            }
        });
    }

    private void OnEnable()
    {
        selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        lit = selected ? 1f : 0f;
        Apply();
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        selectedAt = Time.unscaledTime;
        MenuSounds.Move();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Button.interactable && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    private void Update()
    {
        lit = Mathf.MoveTowards(lit, selected && Button.interactable ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        Apply();
    }

    private void Apply()
    {
        float e = Mathf.SmoothStep(0f, 1f, lit);
        // The first 0.25 s after lighting: two quick dips, a flame steadying.
        float age = Time.unscaledTime - selectedAt;
        float flicker = selected && age < 0.25f ? 1f - 0.35f * Mathf.Abs(Mathf.Sin(age * 38f)) : 1f;
        if (label != null)
        {
            Color c = Button.interactable ? Color.Lerp(idle, active, e * flicker) : disabled;
            label.color = c;
            label.characterSpacing = baseSpacing + spacingGrow * e;
        }
    }
}
