
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBarWidget : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image fillImage;

    private int lastCurrent = -1;

    private Tween fillTween;
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

        bool isFirstRefresh = lastCurrent < 0 || lastMax < 0;
        lastCurrent = current;
        lastMax = max;

        float targetFill = max > 0
            ? Mathf.Clamp01((float)current / max)
            : 0f;

        fillTween?.Kill(false);
        if (isFirstRefresh)
        {
            fillImage.fillAmount = targetFill;
            return;
        }

        fillTween = fillImage.DOFillAmount(targetFill, 0.18f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    public void Deactivate()
    {
        fillTween?.Kill(false);
        fillTween = null;
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        fillTween?.Kill(false);
        fillTween = null;
    }

}