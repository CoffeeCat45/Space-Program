using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drag a Screen Space Overlay panel by a header Graphic. Keeps a sliver on-screen.
/// </summary>
[DisallowMultipleComponent]
public sealed class UiOverlayDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    const float EdgeMargin = 40f;

    RectTransform window;
    RectTransform canvasRect;
    Vector2 grabOffset;

    public void Bind(RectTransform windowRect, Canvas canvas)
    {
        window = windowRect;
        canvasRect = canvas != null ? canvas.transform as RectTransform : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanDrag())
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointer
        ))
        {
            return;
        }

        grabOffset = window.anchoredPosition - pointer;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!CanDrag())
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointer
        ))
        {
            return;
        }

        window.anchoredPosition = pointer + grabOffset;
        ClampToCanvas();
    }

    bool CanDrag()
    {
        return window != null && canvasRect != null;
    }

    void ClampToCanvas()
    {
        Vector2 pos = window.anchoredPosition;
        Vector2 size = window.rect.size;
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;
        float canvasW = canvasRect.rect.width;
        float canvasH = canvasRect.rect.height;
        pos.x = Mathf.Clamp(
            pos.x,
            -canvasW * 0.5f + EdgeMargin - halfW,
            canvasW * 0.5f - EdgeMargin + halfW
        );
        pos.y = Mathf.Clamp(
            pos.y,
            -canvasH * 0.5f + EdgeMargin - halfH,
            canvasH * 0.5f - EdgeMargin + halfH
        );
        window.anchoredPosition = pos;
    }
}
