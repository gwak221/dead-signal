using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class ScreenFitUI : MonoBehaviour
{
    public RectTransform designRoot;
    public Vector2 designSize = new Vector2(1280, 720);
    void OnEnable() { Fit(); }
    void LateUpdate() { Fit(); }
    public void Fit()
    {
        if (designRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
        var safeRoot = (RectTransform)transform;
        var safe = Screen.safeArea;
        safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        designRoot.anchorMin = designRoot.anchorMax = designRoot.pivot = new Vector2(.5f,.5f);
        designRoot.anchoredPosition = Vector2.zero;
        designRoot.sizeDelta = designSize;
        float scale = Mathf.Min(safeRoot.rect.width / designSize.x, safeRoot.rect.height / designSize.y);
        designRoot.localScale = Vector3.one * Mathf.Max(.001f, scale);
    }
}
