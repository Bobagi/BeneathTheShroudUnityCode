using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One option in the main menu (SettingsCatalog id): its name on the left, its value on the right between two
// arrows. Left/right on the keyboard or gamepad change it while the row is selected, the arrows change it with the
// mouse, and Enter or a click on the row steps it forward. Selected, the row lights like a MenuButton.
[RequireComponent(typeof(Button))]
public sealed class SettingRow : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IMoveHandler
{
    [SerializeField]
    private string settingId;

    [SerializeField]
    private TMP_Text label;

    [SerializeField]
    private TMP_Text value;

    [SerializeField]
    private Button previous;

    [SerializeField]
    private Button next;

    [SerializeField]
    private Color idle = new Color(0.78f, 0.74f, 0.66f, 0.82f);

    [SerializeField]
    private Color active = new Color(1f, 0.8f, 0.52f, 1f);

    private SettingsCatalog.Setting setting;
    private bool selected;
    private float lit;

    private void Awake()
    {
        setting = SettingsCatalog.Find(settingId);
        GetComponent<Button>().onClick.AddListener(() => Step(1));
        if (previous != null)
        {
            previous.onClick.AddListener(() => Step(-1));
        }

        if (next != null)
        {
            next.onClick.AddListener(() => Step(1));
        }
    }

    private void OnEnable()
    {
        Loc.Changed += Refresh;
        GameSettings.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Loc.Changed -= Refresh;
        GameSettings.Changed -= Refresh;
    }

    public void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Left)
        {
            Step(-1);
            eventData.Use();
        }
        else if (eventData.moveDir == MoveDirection.Right)
        {
            Step(1);
            eventData.Use();
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        MenuSounds.Move();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    private void Step(int direction)
    {
        if (setting == null)
        {
            return;
        }

        setting.step(direction);
        MenuSounds.Move();
        Refresh();
    }

    private void Refresh()
    {
        if (setting == null)
        {
            setting = SettingsCatalog.Find(settingId);
        }

        if (setting == null)
        {
            if (value != null)
            {
                value.text = "?";
            }

            return;
        }

        if (label != null)
        {
            label.text = Loc.T(setting.labelKey).ToUpperInvariant();
        }

        if (value != null)
        {
            value.text = setting.value();
        }
    }

    private void Update()
    {
        lit = Mathf.MoveTowards(lit, selected ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        float e = Mathf.SmoothStep(0f, 1f, lit);
        if (label != null)
        {
            label.color = Color.Lerp(idle, active, e);
        }
    }
}
