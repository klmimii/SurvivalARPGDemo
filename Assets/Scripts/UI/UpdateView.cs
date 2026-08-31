using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class UpdateView : MonoBehaviour
{
    private UIPanelTween panelTween;
    public bool IsVisible => panelTween != null
        ? panelTween.IsVisible
        : gameObject.activeInHierarchy;


    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Slider progressSlider;

    public void SetStatus(string text)
    {
        statusText.text = text;
    }

    public void SetProgress(float value)
    {
        progressSlider.value = Mathf.Clamp01(value);
        progressText.text = $"{progressSlider.value * 100f:F0}%";
    }

    public void Hide()
    {
        UIPanelTween.GetOrAdd(gameObject).Hide();
    }
    public void Show()
    {
        panelTween = UIPanelTween.GetOrAdd(gameObject);
        panelTween.Show();
    }
}