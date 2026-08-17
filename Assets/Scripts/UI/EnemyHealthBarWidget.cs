using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBarWidget : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image fillImage;

    private int lastCurrent = -1;
    private int lastMax = -1;

    public void Activate()
    {
        lastCurrent = -1;
        lastMax = -1;
        canvasGroup.alpha = 1f;
        gameObject.SetActive(true);
    }

    public void SetScreenVisible(bool visible)
    {
        canvasGroup.alpha = visible ? 1f : 0f;
    }

    public void SetLocalPosition(Vector2 localPosition)
    {
        rectTransform.anchoredPosition = localPosition;
    }

    public void Refresh(int current, int max)
    {
        if (current == lastCurrent && max == lastMax)
        {
            return;
        }

        lastCurrent = current;
        lastMax = max;

        fillImage.fillAmount = max > 0
            ? Mathf.Clamp01((float)current / max)
            : 0f;
    }

    public void Deactivate()
    {
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}