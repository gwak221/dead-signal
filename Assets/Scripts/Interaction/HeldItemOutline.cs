using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 슬롯에서 아이템을 꺼냈을 때 손에 든 모델에 외곽선을 그리고 fadeSeconds 에 걸쳐 천천히 지운다.
/// 주사기처럼 작은 아이템도 "지금 무엇을 들었는지" 알아볼 수 있게 하기 위한 것. 모든 아이템(손전등 포함) 공통.
/// AimOutline 과 같은 셰이더/머티리얼(Resources/AimOutlineStamp, AimOutlineHull)을 쓰되 별도 인스턴스라 조준 외곽선과 동시에 보인다.
/// 원본 Material 은 바꾸지 않는다 (Graphics.DrawMesh).
/// </summary>
public class HeldItemOutline : MonoBehaviour
{
    static HeldItemOutline instance;

    static readonly Color OutlineColor = new Color(1f, 0.82f, 0.25f, 1f);   // AimOutline 줍기 색과 같은 따뜻한 노랑
    static readonly int IdColor = Shader.PropertyToID("_Color");
    static readonly int IdCenter = Shader.PropertyToID("_Center");
    static readonly int IdExtents = Shader.PropertyToID("_Extents");
    static readonly int IdPixelWidth = Shader.PropertyToID("_PixelWidth");
    const int MaxRenderers = 48;

    [Tooltip("외곽선 두께 (화면 픽셀)")]
    public float pixelWidth = 2.5f;
    [Tooltip("꺼낸 순간부터 완전히 사라질 때까지(초)")]
    public float fadeSeconds = 2f;

    Material stampMat, hullMat;
    MaterialPropertyBlock mpb;
    GameObject target;
    float startTime;
    readonly List<MeshRenderer> parts = new List<MeshRenderer>();
    readonly List<MeshFilter> filters = new List<MeshFilter>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    /// <summary>손에 든 아이템 루트에 외곽선을 켠다 (fadeSeconds 동안 서서히 사라짐). null 이면 끈다.</summary>
    public static void Flash(GameObject item)
    {
        if (item == null) { if (instance != null) instance.Clear(); return; }
        if (instance == null) instance = new GameObject("HeldItemOutline").AddComponent<HeldItemOutline>();
        instance.Begin(item);
    }

    void Awake()
    {
        var stamp = Resources.Load<Material>("AimOutlineStamp");
        var hull = Resources.Load<Material>("AimOutlineHull");
        if (stamp != null) stampMat = new Material(stamp);
        if (hull != null) hullMat = new Material(hull);
        mpb = new MaterialPropertyBlock();
    }

    void OnDestroy()
    {
        if (stampMat != null) Destroy(stampMat);
        if (hullMat != null) Destroy(hullMat);
        if (instance == this) instance = null;
    }

    void Clear()
    {
        target = null;
        parts.Clear();
        filters.Clear();
    }

    void Begin(GameObject item)
    {
        Clear();
        target = item;
        startTime = Time.time;
        foreach (var r in item.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (parts.Count >= MaxRenderers) break;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            parts.Add(r);
            filters.Add(mf);
        }
    }

    void LateUpdate()
    {
        if (target == null || stampMat == null || hullMat == null) return;

        float t = (Time.time - startTime) / Mathf.Max(0.01f, fadeSeconds);
        // 다 사라졌거나 손에서 내려놓았으면(비활성화/소비) 끝
        if (t >= 1f || !target.activeInHierarchy) { Clear(); return; }
        float alpha = 1f - Mathf.SmoothStep(0f, 1f, t);

        Bounds b = default; bool any = false;
        for (int i = 0; i < parts.Count; i++)
        {
            var r = parts[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return;

        Color c = OutlineColor; c.a = alpha;
        mpb.SetColor(IdColor, c);
        mpb.SetVector(IdCenter, b.center);
        mpb.SetVector(IdExtents, b.extents);
        mpb.SetFloat(IdPixelWidth, pixelWidth);

        for (int i = 0; i < parts.Count; i++)
        {
            var r = parts[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            Mesh mesh = filters[i].sharedMesh;
            if (mesh == null) continue;
            Matrix4x4 m = r.transform.localToWorldMatrix;
            int layer = r.gameObject.layer;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                Graphics.DrawMesh(mesh, m, stampMat, layer, null, s, null, false, false, false);
                Graphics.DrawMesh(mesh, m, hullMat, layer, null, s, mpb, false, false, false);
            }
        }
    }
}
