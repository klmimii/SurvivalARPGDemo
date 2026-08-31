using System;

using DG.Tweening;
using TMPro;
using UnityEngine;

public class DamageNumberWidget : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation")]
    [SerializeField] private float duration = 0.8f;
    [SerializeField] private float risePixels = 70f;
    [SerializeField] private float horizontalDrift = 18f;

    private Sequence sequence;
    private Action<DamageNumberWidget> releaseCallback;

    public void Play(
            Vector2 localPosition,
            int value,
            Color color,
            Action<DamageNumberWidget> onFinished)
    {
        sequence?.Kill(false);

        float driftDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        Vector2 endPosition = localPosition + new Vector2(
            driftDirection * horizontalDrift,
            risePixels);

        releaseCallback = onFinished;
        valueText.text = value.ToString();
        valueText.color = color;
        canvasGroup.alpha = 1f;
        rectTransform.anchoredPosition = localPosition;
        rectTransform.localScale = Vector3.one * 0.72f;
        gameObject.SetActive(true);

        sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .Join(rectTransform.DOAnchorPos(endPosition, duration).SetEase(Ease.OutCubic))
            .Join(rectTransform.DOScale(1.12f, 0.14f).SetEase(Ease.OutBack))
            .Insert(Mathf.Max(0f, duration * 0.5f),
                canvasGroup.DOFade(0f, duration * 0.5f).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                sequence = null;
                Action<DamageNumberWidget> callback = releaseCallback;
                releaseCallback = null;
                callback?.Invoke(this);
            });
    }



    public void Deactivate()
    {
        sequence?.Kill(false);
        sequence = null;
        releaseCallback = null;
        valueText.text = string.Empty;
        canvasGroup.alpha = 0f;
        rectTransform.localScale = Vector3.one;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        sequence?.Kill(false);
        sequence = null;
    }

}