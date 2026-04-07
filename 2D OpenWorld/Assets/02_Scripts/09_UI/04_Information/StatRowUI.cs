using TMPro;
using UnityEngine;

public class StatRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text statText;
    [SerializeField] private string label;
    [SerializeField] private string value;

    public void SetLabel(string text) => label = text;
    public void SetValue(string text) => value = text;
    public void SetText() => statText.text = $"{label} {value}";
}
