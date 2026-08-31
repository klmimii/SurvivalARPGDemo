using System.Collections;
using System.Collections.Generic;

using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// UI飘字提示框
/// </summary>
public class ToastView : MonoBehaviour
{
    [SerializeField]
    private TMP_Text toastText;//界面上要显示的文字内容
    [SerializeField]
    private float showDuration = 1.5f;//提示框停在屏幕上的时间

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 basePosition;
    private Sequence sequence;

    private void Awake()
    {
        rectTransform = toastText.rectTransform;
        canvasGroup = toastText.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = toastText.gameObject.AddComponent<CanvasGroup>();
        }

        basePosition = rectTransform.anchoredPosition;
        canvasGroup.alpha = 0f;
        toastText.enabled = false;
    }

    public void Show(string message)
    {
        sequence?.Kill(false);

        toastText.text = message;
        toastText.enabled = true;
        canvasGroup.alpha = 0f;
        rectTransform.anchoredPosition = basePosition - new Vector2(0f, 18f);
        rectTransform.localScale = Vector3.one * 0.9f;

        sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .Join(canvasGroup.DOFade(1f, 0.16f))
            .Join(rectTransform.DOAnchorPos(basePosition, 0.22f).SetEase(Ease.OutCubic))
            .Join(rectTransform.DOScale(1f, 0.22f).SetEase(Ease.OutBack))
            .AppendInterval(showDuration)
            .Append(canvasGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad))
            .Join(rectTransform.DOAnchorPos(basePosition + new Vector2(0f, 16f), 0.2f))
            .OnComplete(() =>
            {
                toastText.enabled = false;
                rectTransform.anchoredPosition = basePosition;
                rectTransform.localScale = Vector3.one;
                sequence = null;
            });
    }

    private void OnDisable()
    {
        sequence?.Kill(false);
        sequence = null;
        if (toastText != null)
        {
            toastText.enabled = false;
        }
    }




}
