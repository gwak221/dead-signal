using UnityEngine;

[System.Flags]
public enum SpawnAllow { None = 0, FlashlightBattery = 1, Medkit = 2, Syringe = 4 }

/// <summary>
/// 랜덤 아이템 스폰 후보 지점. 위치 = 아이템이 놓일 '받침면' 위의 한 점(y 가 받침면 높이), Y 회전 = 아이템 방향.
/// 부모 Transform 은 이 지점이 속한 개폐부(서랍/문) 또는 가구라서, 아이템이 거기에 붙으면 서랍과 함께 움직이고 LootQteGate 가 그 개폐부에 QTE 를 붙인다.
/// 씬에서 직접 추가/이동할 수 있다: 빈 오브젝트에 이 컴포넌트를 붙이고 받침면 위에 두면 된다 (Gizmo 로 위치/허용 종류 표시).
/// </summary>
public class ItemSpawnPoint : MonoBehaviour
{
    public SpawnAllow allow = SpawnAllow.FlashlightBattery;
    [Tooltip("분포 균형용 구역 이름")] public string zone;
    [Tooltip("수납장 밖(바닥/책상/카트 위) 지점. 전체 랜덤 아이템 중 일부만 배정된다.")] public bool openSurface;
    [Tooltip("고정 아이템이 놓인 지점 등, 랜덤 배정에서 제외")] public bool reserved;
    [Tooltip("약장(DoorPair) 안이면 그 약장. AA 의 cabinetDoor 로 설정된다.")] public DoorPair cabinetDoor;

    public bool Allows(SpawnAllow kind) => (allow & kind) != 0;

    void OnDrawGizmos()
    {
        Gizmos.color = reserved ? new Color(0.6f, 0.6f, 0.6f, 0.8f)
                     : Allows(SpawnAllow.Syringe) ? new Color(0.3f, 1f, 0.9f, 0.9f)
                     : Allows(SpawnAllow.Medkit) ? new Color(1f, 0.4f, 0.3f, 0.9f)
                     : new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.04f, new Vector3(0.12f, 0.08f, 0.12f));
        Gizmos.DrawLine(transform.position, transform.position + Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward * 0.15f);
    }
}
