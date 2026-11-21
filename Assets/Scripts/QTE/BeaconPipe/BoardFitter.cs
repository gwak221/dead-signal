using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class BoardFitter : MonoBehaviour
{
    public Image background;
    public RectTransform board;
    [Range(10, 20)] public int margin = 16;

    // 배경 그림에서 테두리 안쪽. 위에서 잰 픽셀.
    const float FrameLeft = 34f;
    const float FrameRight = 899f;
    const float FrameTop = 27f;
    const float FrameBottom = 796f;

    bool fitting;

    void OnEnable()
    {
        Fit();
    }

    void OnRectTransformDimensionsChange()
    {
        Fit();
    }

    void OnValidate()
    {
        Fit();
    }

    public void Fit()
    {
        if (fitting || background == null || background.sprite == null || board == null)
            return;

        Rect bg = background.rectTransform.rect;
        Rect sprite = background.sprite.rect;
        if (bg.width < 1f || bg.height < 1f || sprite.width < 1f || sprite.height < 1f)
            return;

        float pad = Mathf.Clamp(margin, 10, 20);
        float left = FrameLeft + pad;
        float right = FrameRight - pad;
        float top = FrameTop + pad;
        float bottom = FrameBottom - pad;
        if (right <= left || bottom <= top)
            return;

        fitting = true;

        float draw = Mathf.Min(bg.width / sprite.width, bg.height / sprite.height);
        float boxW = (right - left + 1f) * draw;
        float boxH = (bottom - top + 1f) * draw;

        float boxCenterX = (left + right) * 0.5f;
        float boxCenterFromTop = (top + bottom) * 0.5f;
        float boxCenterY = sprite.height - boxCenterFromTop;
        Vector2 offset = new Vector2(
            (boxCenterX - sprite.width * 0.5f) * draw,
            (boxCenterY - sprite.height * 0.5f) * draw);

        float boardW = board.rect.width > 1f ? board.rect.width : board.sizeDelta.x;
        float boardH = board.rect.height > 1f ? board.rect.height : board.sizeDelta.y;
        float fit = Mathf.Min(boxW / boardW, boxH / boardH);

        if ((board.anchoredPosition - offset).sqrMagnitude > 0.01f)
            board.anchoredPosition = offset;

        Vector3 scale = new Vector3(fit, fit, 1f);
        if ((board.localScale - scale).sqrMagnitude > 0.000001f)
            board.localScale = scale;

        fitting = false;
    }
}
