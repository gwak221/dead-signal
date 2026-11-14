using UnityEngine;

/// <summary>
/// Openables 프리팹 루트 표시. 교체 도구(Tools/Openables/Replace In Open Scenes)가
/// 씬의 원본 모델 인스턴스(이름이 replacesModel 인 FBX/프리팹)를 이 프리팹으로 바꿀 때 쓴다.
/// 원본 에셋을 직접 참조하지 않으므로 폴더째 다른 프로젝트로 옮겨도 의존성이 생기지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class OpenableFurniture : MonoBehaviour
{
    [Tooltip("교체 대상 원본 모델 에셋 이름 (확장자 제외). 예: filing → .../filing.fbx 인스턴스를 교체")]
    public string replacesModel;

    [Tooltip("원본 모델 루트의 기본 로컬 회전(오일러). 원본 FBX가 루트에 축 변환 회전을 갖고 있으면 그 값. " +
             "교체 시 newRotation = oldRotation * Inverse(이 값) 으로 같은 자세를 유지한다. 교체 도구가 원본에서 직접 읽을 수 있으면 그 값을 우선한다.")]
    public Vector3 sourceRootEuler = new Vector3(90f, 0f, 0f);

    OpenablePart[] parts;

    public OpenablePart[] Parts => parts != null && parts.Length > 0 ? parts : (parts = GetComponentsInChildren<OpenablePart>(true));

    [ContextMenu("Open All")]
    public void OpenAll() { foreach (var p in Parts) p.SetOpen(true, !Application.isPlaying); }

    [ContextMenu("Close All")]
    public void CloseAll() { foreach (var p in Parts) p.SetOpen(false, !Application.isPlaying); }
}
