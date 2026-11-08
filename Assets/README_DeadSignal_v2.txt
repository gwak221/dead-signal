Dead Signal 수정본 v2

이 버전은 금고 내부/메모/비밀번호 문/비밀번호 UI를 Play 시점에 임시 생성하지 않습니다.
Unity에서 Dead_signal 씬을 열면 Editor 스크립트가 한 번만 실제 씬 오브젝트로 생성하고 자동 저장합니다.
따라서 Play를 누르기 전에도 Hierarchy/Scene에서 확인하고 위치, 크기, 색상 등을 직접 수정할 수 있습니다.

주요 오브젝트
- Safe > SafeInterior : 금고 내부 벽/선반/1234 메모
- PasswordDoor : 비밀번호 문/문틀
- Canvas > PasswordPanel : 비밀번호 UI (평소 비활성화)

기능
- 금고 E 열기/닫기
- 최초 금고 사용 후 화면에 들고 있던 Key 모델 숨김 (hasKey 보유 상태는 유지)
- 금고 내부 메모 1234
- PasswordDoor E -> 비밀번호 UI
- ESC -> 비밀번호 UI 닫기
- 1234 -> 문 열림, 이후 E로 열기/닫기
