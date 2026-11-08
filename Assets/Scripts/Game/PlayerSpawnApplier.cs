using UnityEngine;

/// <summary>
/// 게임 시작 시 Player를 'PlayerSpawn' Transform의 Position/Rotation 그대로 배치한다.
/// 충돌 검사/주변 탐색/보정은 하지 않는다. (스폰 위치는 맵 제작자가 PlayerSpawn을 직접 배치)
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlayerSpawnApplier : MonoBehaviour
{
    [Tooltip("비어 있으면 씬의 'PlayerSpawn' 오브젝트를 이름으로 찾는다.")]
    public Transform spawnPoint;

    void Awake()
    {
        if (spawnPoint == null)
        {
            var go = GameObject.Find("PlayerSpawn");
            if (go != null) spawnPoint = go.transform;
        }
        if (spawnPoint == null)
        {
            Debug.LogWarning("PlayerSpawnApplier: 'PlayerSpawn' 오브젝트를 찾지 못했습니다. Player의 씬 위치를 그대로 사용합니다.");
            return;
        }

        var cc = GetComponent<CharacterController>();
        bool wasEnabled = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        if (cc != null) cc.enabled = wasEnabled;
    }
}
