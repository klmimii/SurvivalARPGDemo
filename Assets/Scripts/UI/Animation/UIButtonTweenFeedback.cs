using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Selectable))]
public sealed class UIButtonTweenFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField, Range(1f, 1.15f)] private float hoverScale = 1.045f;
    [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.94f;
    [SerializeField, Min(0.01f)] private float duration = 0.1f;

    private Selectable selectable;
    private Vector3 baseScale;
    private bool pointerInside;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        transform.DOKill(false);
        transform.localScale = baseScale;
        pointerInside = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        TweenTo(hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        TweenTo(1f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (CanAnimate())
        {
            TweenTo(pressedScale);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        TweenTo(pointerInside ? hoverScale : 1f);
    }

    private void TweenTo(float multiplier)
    {
        if (!CanAnimate())
        {
            transform.localScale = baseScale;
            return;
        }

        transform.DOKill(false);
        transform.DOScale(baseScale * multiplier, duration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private bool CanAnimate()
    {
        return isActiveAndEnabled &&
            selectable != null &&
            selectable.IsInteractable();
    }
}