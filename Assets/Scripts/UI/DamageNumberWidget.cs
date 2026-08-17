using System;
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

    private Vector2 startPosition;
    private float elapsed;
    private float driftDirection;
    private Action<DamageNumberWidget> releaseCallback;

    public void Play(
        Vector2 localPosition,
        int value,
        Color color,
        Action<DamageNumberWidget> onFinished)
    {
        startPosition = localPosition;
        elapsed = 0f;
        driftDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        releaseCallback = onFinished;

        valueText.text = value.ToString();
        valueText.color = color;
        canvasGroup.alpha = 1f;
        rectTransform.anchoredPosition = startPosition;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float normalized = duration > 0f
            ? Mathf.Clamp01(elapsed / duration)
            : 1f;

        float easedRise = 1f - (1f - normalized) * (1f - normalized);

        rectTransform.anchoredPosition = startPosition + new Vector2(
            driftDirection * horizontalDrift * normalized,
            risePixels * easedRise);

        canvasGroup.alpha = 1f - normalized;

        if (normalized >= 1f)
        {
            Action<DamageNumberWidget> callback = releaseCallback;
            releaseCallback = null;
            callback?.Invoke(this);
        }
    }

    public void Deactivate()
    {
        releaseCallback = null;
        valueText.text = string.Empty;
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}