using System;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UIPanelTween : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float showDuration = 0.24f;
    [SerializeField, Min(0.01f)] private float hideDuration = 0.16f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InCubic;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Motion")]
    [SerializeField] private Vector2 hiddenOffset = new Vector2(0f, -24f);
    [SerializeField, Range(0.7f, 1f)] private float hiddenScale = 0.94f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 visiblePosition;
    private Vector3 visibleScale;
    private Sequence sequence;
    private bool initialized;
    private bool isVisible;

    public bool IsVisible => isVisible;

    private void Awake()
    {
        EnsureInitialized();
        isVisible = gameObject.activeSelf;
    }

    private void OnDisable()
    {
        sequence?.Kill(false);
        sequence = null;
        isVisible = false;
    }

    public void Show()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        EnsureInitialized();
        KillCurrentSequence();

        isVisible = true;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        rectTransform.anchoredPosition = visiblePosition + hiddenOffset;
        rectTransform.localScale = visibleScale * hiddenScale;

        sequence = DOTween.Sequence()
            .SetUpdate(useUnscaledTime)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .Join(canvasGroup.DOFade(1f, showDuration).SetEase(Ease.OutQuad))
            .Join(rectTransform.DOAnchorPos(visiblePosition, showDuration).SetEase(showEase))
            .Join(rectTransform.DOScale(visibleScale, showDuration).SetEase(showEase))
            .OnComplete(() =>
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                sequence = null;
            });
    }

    public void Hide(Action onComplete = null)
    {
        EnsureInitialized();
        isVisible = false;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (!gameObject.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }

        KillCurrentSequence();

        sequence = DOTween.Sequence()
            .SetUpdate(useUnscaledTime)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .Join(canvasGroup.DOFade(0f, hideDuration).SetEase(Ease.InQuad))
            .Join(rectTransform.DOAnchorPos(visiblePosition + hiddenOffset, hideDuration).SetEase(hideEase))
            .Join(rectTransform.DOScale(visibleScale * hiddenScale, hideDuration).SetEase(hideEase))
            .OnComplete(() =>
            {
                sequence = null;
                gameObject.SetActive(false);
                onComplete?.Invoke();
            });
    }

    public void HideImmediate()
    {
        EnsureInitialized();
        KillCurrentSequence();
        isVisible = false;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        rectTransform.anchoredPosition = visiblePosition;
        rectTransform.localScale = visibleScale;
        gameObject.SetActive(false);
    }

    public static UIPanelTween GetOrAdd(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        UIPanelTween transition = target.GetComponent<UIPanelTween>();
        return transition != null
            ? transition
            : target.AddComponent<UIPanelTween>();
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        rectTransform = transform as RectTransform;
        if (rectTransform == null)
        {
            Debug.LogError("UIPanelTween 必须挂在带 RectTransform 的 UI 对象上。", this);
            enabled = false;
            return;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        visiblePosition = rectTransform.anchoredPosition;
        visibleScale = rectTransform.localScale;
        initialized = true;
    }

    private void KillCurrentSequence()
    {
        if (sequence == null)
        {
            return;
        }

        sequence.Kill(false);
        sequence = null;
    }
}
