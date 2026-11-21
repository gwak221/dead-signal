using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 중앙 조준점으로 보고 있는 상호작용 대상(E 환경 상호작용 / F 줍기)에 얇은 외곽선을 그리는 공용 하이라이트.
/// - 대상은 PlayerInteraction 이 매 프레임 SetTarget 으로 알려 준다. 조준을 벗어나면 null 이 들어와 즉시 꺼진다.
/// - 원본 Material 은 절대 바꾸지 않는다: Graphics.DrawMesh 로 별도 외곽선 메시를 그리고(역방향 헐, 스텐실로 실루엣 안쪽 제외),
///   여러 Renderer 로 이뤄진 대상도 합쳐진 하나의 덩어리로 표시한다.
/// - URP(Unlit 패스) 전용 셰이더, 읽기 불가 메시에서도 동작. 에디터/빌드 동일.
/// - 색: E 환경 상호작용 = 흰빛, F 줍기 = 따뜻한 노랑(Pickup 이 조준되면 Pickup 만 표시해 겹친 가구보다 우선).
/// </summary>
public class AimOutline : MonoBehaviour
{
    static AimOutline instance;

    static readonly Color EnvColor = new Color(0.85f, 0.95f, 1f, 1f);
    static readonly Color PickupColor = new Color(1f, 0.82f, 0.25f, 1f);
    static readonly int IdColor = Shader.PropertyToID("_Color");
    static readonly int IdCenter = Shader.PropertyToID("_Center");
    static readonly int IdExtents = Shader.PropertyToID("_Extents");
    static readonly int IdPixelWidth = Shader.PropertyToID("_PixelWidth");

    [Tooltip("외곽선 두께 (화면 픽셀)")]
    public float pixelWidth = 2.0f;
    const int MaxRenderers = 48;

    Material stampMat, hullMat;
    MaterialPropertyBlock mpb;
    Transform target;
    Transform[] targetExtras;
    bool targetIsPickup;
    readonly List<MeshRenderer> parts = new List<MeshRenderer>();
    readonly List<MeshFilter> filters = new List<MeshFilter>();

    /// <summary>현재 외곽선이 표시 중인 대상 루트 (없으면 null). 검증/디버그용.</summary>
    public static Transform CurrentTarget => instance != null ? instance.target : null;
    public static bool CurrentIsPickup => instance != null && instance.targetIsPickup;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    static AimOutline Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("AimOutline");
                instance = go.AddComponent<AimOutline>();
            }
            return instance;
        }
    }

    /// <summary>외곽선 대상 지정. root 가 null 이면 즉시 끈다.</summary>
    public static void SetTarget(Transform root, bool isPickup, Transform[] extras = null)
    {
        if (root == null)
        {
            if (instance != null) instance.Clear();
            return;
        }
        Instance.Apply(root, isPickup, extras);
    }

    void Awake()
    {
        // 셰이더가 빌드에 포함되도록 Resources 의 Material 에서 가져온다.
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
        targetExtras = null;
        parts.Clear();
        filters.Clear();
    }

    static bool SameExtras(Transform[] a, Transform[] b)
    {
        int na = a == null ? 0 : a.Length, nb = b == null ? 0 : b.Length;
        if (na != nb) return false;
        for (int i = 0; i < na; i++) if (a[i] != b[i]) return false;
        return true;
    }

    void Apply(Transform root, bool isPickup, Transform[] extras)
    {
        if (root == target && isPickup == targetIsPickup && SameExtras(extras, targetExtras)) return;
        Clear();
        target = root;
        targetExtras = extras;
        targetIsPickup = isPickup;
        Collect(root, isPickup);
        if (extras != null)
            foreach (var e in extras) if (e != null && e != root) Collect(e, isPickup);
    }

    void Collect(Transform root, bool isPickup)
    {
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (parts.Count >= MaxRenderers) break;
            if (!r.enabled) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            // 가구 안에 들어 있는 다른 줍기 아이템(배터리/Medkit 등)은 가구 외곽선에 포함하지 않는다.
            if (!isPickup && (r.GetComponentInParent<IPickupable>() != null || r.GetComponentInParent<BatteryPickup>() != null)) continue;
            parts.Add(r);
            filters.Add(mf);
        }
    }

    void LateUpdate()
    {
        if (target == null || parts.Count == 0 || stampMat == null || hullMat == null) return;

        // 합쳐진 월드 Bounds (문/서랍이 움직여도 매 프레임 갱신)
        Bounds b = default; bool any = false;
        for (int i = 0; i < parts.Count; i++)
        {
            var r = parts[i];
            if (r == null || !r.gameObject.activeInHierarchy) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return;

        mpb.SetColor(IdColor, targetIsPickup ? PickupColor : EnvColor);
        mpb.SetVector(IdCenter, b.center);
        mpb.SetVector(IdExtents, b.extents);
        mpb.SetFloat(IdPixelWidth, pixelWidth);

        for (int i = 0; i < parts.Count; i++)
        {
            var r = parts[i];
            if (r == null || !r.gameObject.activeInHierarchy || !r.enabled) continue;
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
