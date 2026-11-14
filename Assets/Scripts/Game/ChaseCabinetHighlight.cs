using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 몬스터가 플레이어를 실제로 감지해 추격 중일 때만 tall_cabinet(HideSpot)을 '누르면 숨을 수 있는 곳'으로 보이게 한다.
/// 3단계 affordance:
///   먼 거리  : 모서리 라인 + 본체 Emission (강한 외곽 Highlight)
///   중거리   : 문/손잡이 쪽 집중 Pulse 헤일로 + Cabinet 옆 World Space 안내 "[E] 숨기"
///   근거리   : 기존 PlayerInteraction 하단 프롬프트 "[E] 숨기" (이때 월드 안내는 자동으로 숨어 겹치지 않음)
///  - MonsterChase.IsChasing / OnChaseStarted / OnChaseEnded 만 읽는다 (MonsterChase는 이 클래스를 모른다).
///  - 플레이어가 숨어 있으면(PlayerHide.IsPlayerHiding) 모두 꺼진다.
///  - 깊이 테스트만 사용(벽 뒤 X-ray 없음), 월드 안내는 카메라→안내 Linecast 로 벽 차단 시 숨김. 새 Light 없음.
/// </summary>
public class ChaseCabinetHighlight : MonoBehaviour
{
    [Tooltip("DeadSignal/ChaseEdge 셰이더를 쓰는 머티리얼 (원본은 수정하지 않고 인스턴스를 만든다).")]
    public Material lineMaterial;
    [ColorUsage(false, true)] public Color color = new Color(1f, 0.78f, 0.45f);
    [Tooltip("모서리 라인 밝기 (HDR).")]
    [Range(0.2f, 8f)] public float intensity = 4.0f;
    [Tooltip("캐비넷 본체 자체 Emission 강도 (이 캐비넷만 빛남. 주변은 밝히지 않음).")]
    [Range(0f, 2f)] public float bodyEmission = 0.12f;
    [Tooltip("캐비넷 표면에서 바깥으로 띄우는 거리 (m).")]
    [Range(0f, 0.1f)] public float padding = 0.02f;
    [Range(0.005f, 0.15f)] public float baseWidth = 0.045f;
    [Tooltip("카메라 거리 1m 당 선 굵기 증가율 / 최대 배율.")]
    public float widthPerMeter = 0.25f;
    public float maxWidthScale = 9f;
    public float fadeInSeconds = 0.35f;
    public float fadeOutSeconds = 0.5f;
    [Range(0f, 0.4f)] public float pulse = 0.15f;

    [Header("문/손잡이 집중 Pulse")]
    [Tooltip("손잡이 높이 (바닥 기준 m).")]
    public float handleHeight = 1.1f;
    public float handleHaloSize = 1.1f;
    [Range(0f, 1f)] public float handleHaloAlpha = 0.95f;
    public float handlePulseSpeed = 5f;

    [Header("World Space 안내 [E] 숨기")]
    public string hintText = HideSpot.PromptHide;
    [Tooltip("이 거리(m) 이내이고 벽에 가리지 않을 때만 표시.")]
    public float hintShowDistance = 5f;
    public float hintHeight = 2.7f;
    [Tooltip("글자 세계 크기 (m, 대략 글자 높이).")]
    public float hintSize = 0.55f;
    public Color hintColor = new Color(1f, 0.86f, 0.35f);

    class Cabinet
    {
        public HideSpot spot;
        public Transform root;
        public readonly List<LineRenderer> lines = new List<LineRenderer>();
        public Material[] bodyMats;
        public SpriteRenderer handle;
        public TextMeshPro hint;
        public Vector3 front;
        public Vector3 handlePos, hintPos;
        public float hintAlpha;
    }

    readonly List<MonsterChase> monsters = new List<MonsterChase>();
    readonly List<Cabinet> cabinets = new List<Cabinet>();
    Material mat;
    float fade;
    float rescanTimer;
    bool bodyLit;
    PlayerInteraction interaction;
    static Sprite softSprite;

    static readonly int FadeId = Shader.PropertyToID("_Fade");

    void Start()
    {
        if (lineMaterial == null) { enabled = false; return; }
        mat = new Material(lineMaterial);
        mat.SetColor("_Color", color * intensity);
        mat.SetFloat(FadeId, 0f);

        foreach (var spot in FindObjectsByType<HideSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Build(spot);

        RescanMonsters();
    }

    void Build(HideSpot spot)
    {
        var src = spot.GetComponentInChildren<MeshRenderer>(true);
        var filter = src != null ? src.GetComponent<MeshFilter>() : null;
        if (filter == null || filter.sharedMesh == null) return;

        var c = new Cabinet { spot = spot, root = src.transform, front = spot.FrontDirection };
        c.bodyMats = src.materials; // 인스턴스 (공유 FBX 머티리얼 보호)
        foreach (var m in c.bodyMats) if (m.HasProperty("_EmissionColor")) m.EnableKeyword("_EMISSION");

        // ── 모서리 라인 (Bounds 12 edges) ──
        Bounds b = filter.sharedMesh.bounds;
        Vector3 s = src.transform.lossyScale;
        Vector3 pad = new Vector3(padding / Mathf.Max(0.0001f, Mathf.Abs(s.x)), padding / Mathf.Max(0.0001f, Mathf.Abs(s.y)), padding / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
        Vector3 min = b.min - pad, max = b.max + pad;
        var root = new GameObject("ChaseOutline").transform;
        root.SetParent(src.transform, false);
        Vector3[] bot = { new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z) };
        Vector3[] top = { new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z) };
        c.lines.Add(MakeLine(root, "Bottom", bot, true));
        c.lines.Add(MakeLine(root, "Top", top, true));
        for (int i = 0; i < 4; i++)
            c.lines.Add(MakeLine(root, "Edge" + i, new[] { bot[i], top[i] }, false));

        // ── 앞면(문) 위치 계산: 월드 Bounds 를 앞 방향으로 투영 ──
        Bounds wb = src.bounds;
        Vector3 f = c.front;
        float halfDepth = Mathf.Abs(f.x) * wb.extents.x + Mathf.Abs(f.z) * wb.extents.z;
        Vector3 faceCenter = new Vector3(wb.center.x, wb.min.y, wb.center.z) + f * halfDepth;
        c.handlePos = faceCenter + f * 0.06f + Vector3.up * handleHeight;
        c.hintPos = faceCenter + f * 0.3f + Vector3.up * hintHeight;

        // 손잡이 집중 Pulse 헤일로 (스프라이트, 깊이 테스트)
        var hg = new GameObject("ChaseHandleGlow");
        hg.transform.SetParent(src.transform, true);
        hg.transform.position = c.handlePos;
        var sr = hg.AddComponent<SpriteRenderer>();
        sr.sprite = SoftSprite();
        sr.sortingOrder = 5;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        sr.enabled = false;
        c.handle = sr;

        // World Space 안내 [E] 숨기 (3D TMP, 한글 폴백 폰트 사용)
        var hgo = new GameObject("HideHint", typeof(RectTransform));
        hgo.transform.SetParent(src.transform, true);
        hgo.transform.position = c.hintPos;
        var t = hgo.AddComponent<TextMeshPro>();
        t.text = hintText;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = false;
        t.fontSize = 10f;
        t.color = new Color(hintColor.r, hintColor.g, hintColor.b, 0f);
        t.outlineWidth = 0.5f;                               // 밝은 캐비넷 위에서도 읽히도록 어두운 외곽선
        t.outlineColor = new Color32(10, 6, 0, 255);
        t.rectTransform.sizeDelta = new Vector2(4f, 1f);
        hgo.transform.localScale = Vector3.one * hintSize; // fontSize 10 × scale → 월드 글자 높이 ≈ hintSize(m)
        t.gameObject.SetActive(false);
        c.hint = t;

        cabinets.Add(c);
    }

    LineRenderer MakeLine(Transform parent, string name, Vector3[] pts, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = loop;
        lr.positionCount = pts.Length;
        lr.SetPositions(pts);
        lr.sharedMaterial = mat;
        lr.alignment = LineAlignment.View;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        lr.widthMultiplier = baseWidth;
        lr.enabled = false;
        return lr;
    }

    // 상태는 MonsterChase 의 읽기 전용 값(IsChasing)과 이벤트만 사용한다. 리스폰/교체에 대비해 가끔 다시 찾는다.
    void RescanMonsters()
    {
        foreach (var m in monsters) if (m != null) { m.OnChaseStarted -= OnChanged; m.OnChaseEnded -= OnChanged; }
        monsters.Clear();
        foreach (var m in FindObjectsByType<MonsterChase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            monsters.Add(m);
            m.OnChaseStarted += OnChanged;
            m.OnChaseEnded += OnChanged;
        }
    }

    // 이벤트는 상태 변경 훅. 실제 표시 여부는 Update 에서 IsChasing 으로 평가한다.
    void OnChanged(MonsterChase _) { }

    void OnDestroy()
    {
        foreach (var m in monsters) if (m != null) { m.OnChaseStarted -= OnChanged; m.OnChaseEnded -= OnChanged; }
        if (mat != null) Destroy(mat);
        foreach (var c in cabinets) if (c.bodyMats != null) foreach (var m in c.bodyMats) if (m != null) Destroy(m);
    }

    bool WantHighlight()
    {
        if (PlayerHide.IsPlayerHiding) return false;
        foreach (var m in monsters)
            if (m != null && m.IsChasing) return true;
        return false;
    }

    void Update()
    {
        if (mat == null) return;

        rescanTimer -= Time.unscaledDeltaTime;
        if (rescanTimer <= 0f)
        {
            rescanTimer = 3f;
            if (monsters.Count == 0 || monsters.Exists(m => m == null)) RescanMonsters();
        }

        bool want = WantHighlight();
        float speed = want ? 1f / Mathf.Max(0.01f, fadeInSeconds) : 1f / Mathf.Max(0.01f, fadeOutSeconds);
        fade = Mathf.MoveTowards(fade, want ? 1f : 0f, speed * Time.unscaledDeltaTime);
        bool visible = fade > 0.001f;

        var cam = Camera.main;
        float p = 1f - pulse * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));

        // 하단 프롬프트가 '숨기' 면 월드 안내는 숨긴다 (근거리 단계로 넘어감)
        bool bottomIsHide = false;
        if (interaction == null) interaction = FindFirstObjectByType<PlayerInteraction>();
        if (interaction != null && interaction.interactionText != null && interaction.interactionText.gameObject.activeSelf)
            bottomIsHide = interaction.interactionText.text == HideSpot.PromptHide;

        foreach (var c in cabinets)
        {
            if (c.root == null) continue;

            float scale = 1f;
            if (visible && cam != null)
            {
                float d = Vector3.Distance(cam.transform.position, c.root.position);
                scale = Mathf.Min(1f + d * widthPerMeter, maxWidthScale);
            }
            foreach (var lr in c.lines)
            {
                if (lr.enabled != visible) lr.enabled = visible;
                if (visible) lr.widthMultiplier = baseWidth * scale;
            }

            // 손잡이 집중 Pulse (조금 더 빠른 박동)
            if (c.handle != null)
            {
                if (c.handle.enabled != visible) c.handle.enabled = visible;
                if (visible && cam != null)
                {
                    float hp = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * handlePulseSpeed);
                    c.handle.transform.rotation = cam.transform.rotation;
                    float dist = Vector3.Distance(cam.transform.position, c.handlePos);
                    c.handle.transform.localScale = Vector3.one * handleHaloSize * Mathf.Min(1f + dist * 0.05f, 2f) * (0.8f + 0.4f * hp);
                    c.handle.color = new Color(1f, 0.82f, 0.45f, handleHaloAlpha * fade * (0.35f + 0.65f * hp));
                }
            }

            UpdateHint(c, cam, want, bottomIsHide);
        }

        if (visible) mat.SetFloat(FadeId, fade * p);
        if (visible || bodyLit)
        {
            Color e = color * (bodyEmission * fade * p);
            foreach (var c in cabinets)
                if (c.bodyMats != null)
                    foreach (var m in c.bodyMats)
                        if (m != null && m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", visible ? e : Color.black);
            bodyLit = visible;
        }
    }

    void UpdateHint(Cabinet c, Camera cam, bool chasing, bool bottomIsHide)
    {
        if (c.hint == null) return;
        bool show = false;
        if (chasing && cam != null && !bottomIsHide && !PlayerHide.IsPlayerHiding)
        {
            Vector3 camPos = cam.transform.position;
            if (Vector3.Distance(camPos, c.root.position) <= hintShowDistance)
                show = HasLineOfSight(camPos, c);
        }

        c.hintAlpha = Mathf.MoveTowards(c.hintAlpha, show ? 1f : 0f, Time.unscaledDeltaTime * (show ? 5f : 8f));
        bool on = c.hintAlpha > 0.01f;
        if (c.hint.gameObject.activeSelf != on) c.hint.gameObject.SetActive(on);
        if (!on) return;

        float bob = 0.04f * Mathf.Sin(Time.unscaledTime * 3f);
        c.hint.transform.position = c.hintPos + Vector3.up * bob;
        c.hint.transform.rotation = cam.transform.rotation; // 빌보드
        c.hint.color = new Color(hintColor.r, hintColor.g, hintColor.b, c.hintAlpha);
    }

    // 카메라 → 캐비넷(안내 위치와 앞면) 사이가 벽/문/소품에 막히지 않았는지 (자기 자신/플레이어는 무시)
    bool HasLineOfSight(Vector3 camPos, Cabinet c)
    {
        return Clear(camPos, c.hintPos, c) || Clear(camPos, c.handlePos, c);
    }

    bool Clear(Vector3 from, Vector3 to, Cabinet c)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;
        var hits = Physics.RaycastAll(from, dir / dist, dist, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            var t = h.collider.transform;
            if (t.IsChildOf(c.spot.transform) || t.IsChildOf(c.root)) continue;
            if (interaction != null && t.IsChildOf(interaction.transform)) continue;
            return false;
        }
        return true;
    }

    static Sprite SoftSprite()
    {
        if (softSprite != null) return softSprite;
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                float a = Mathf.Clamp01(1f - d); a = a * a * a;
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        softSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return softSprite;
    }
}
