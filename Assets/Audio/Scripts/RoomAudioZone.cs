using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방 영역(박스). 플레이어(AudioListener)가 이 박스 안에 있으면 profile의 소리가 적용된다.
/// 콜라이더를 쓰지 않는다 (트리거가 다른 레이캐스트를 막는 일이 없도록, 순수 위치 계산만).
/// 박스는 이 오브젝트의 위치/회전 기준으로 size 크기. Scene 뷰에서 핸들로 크기를 조절할 수 있다.
/// 겹치면 priority가 높은 쪽, 같으면 작은 박스가 이긴다 (큰 방 안의 작은 방 처리).
/// </summary>
[ExecuteAlways]
public class RoomAudioZone : MonoBehaviour
{
    public RoomAudioProfile profile;
    [Tooltip("로컬 기준 박스 크기 (m). 중심은 이 오브젝트 위치")]
    public Vector3 size = new Vector3(10f, 3.2f, 10f);
    [Tooltip("겹칠 때 높은 쪽이 이긴다")]
    public int priority = 0;
    public Color gizmoColor = new Color(0.2f, 0.8f, 1f, 1f);

    static readonly List<RoomAudioZone> active = new List<RoomAudioZone>();
    public static IReadOnlyList<RoomAudioZone> Active => active;

    public float Volume => Mathf.Abs(size.x * size.y * size.z * transform.lossyScale.x * transform.lossyScale.y * transform.lossyScale.z);

    void OnEnable() { if (!active.Contains(this)) active.Add(this); }
    void OnDisable() { active.Remove(this); }

    public bool Contains(Vector3 worldPoint)
    {
        Vector3 p = transform.InverseTransformPoint(worldPoint);
        Vector3 h = size * 0.5f;
        return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
    }

    /// <summary>point를 포함하는 Zone 중 우선순위가 가장 높은 것. 없으면 null.</summary>
    public static RoomAudioZone Find(Vector3 worldPoint)
    {
        RoomAudioZone best = null;
        foreach (var z in active)
        {
            if (z == null || z.profile == null || !z.Contains(worldPoint)) continue;
            if (best == null || z.priority > best.priority || (z.priority == best.priority && z.Volume < best.Volume)) best = z;
        }
        return best;
    }

    void OnDrawGizmos() => Draw(0.06f, 0.6f);
    void OnDrawGizmosSelected() => Draw(0.15f, 1f);

    void Draw(float fillA, float lineA)
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        var c = gizmoColor;
        Gizmos.color = new Color(c.r, c.g, c.b, fillA);
        Gizmos.DrawCube(Vector3.zero, size);
        Gizmos.color = new Color(c.r, c.g, c.b, lineA);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
