using TMPro;
using UnityEngine;

// A text that shows Loc.T(key) and changes with the language. Put it on any TextMeshPro text and fill the key
// (a line of Settings/Resources/Localization/ui.txt). Upper case on screen is a flag, so the table keeps
// normal case.
[RequireComponent(typeof(TMP_Text))]
public sealed class LocalizedText : MonoBehaviour
{
    [SerializeField]
    private string key;

    [SerializeField]
    private bool upperCase;

    private TMP_Text text;

    public string Key
    {
        get => key;
        set
        {
            key = value;
            Refresh();
        }
    }

    private void OnEnable()
    {
        Loc.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Loc.Changed -= Refresh;
    }

    public void Refresh()
    {
        if (text == null)
        {
            text = GetComponent<TMP_Text>();
        }

        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        string value = Loc.T(key);
        text.text = upperCase ? value.ToUpperInvariant() : value;
    }
}
