# 통합 노트 (integration-on-main-1124)

기준: origin/main a09b319 + 우리 고유분(8d09f62) + mapwork-taema 9de20f9 의 맵 변경.

## 추후 작업 / 검증 항목
- **공간별 룸 존·리버브·차폐 오디오**: main 의 AudioManager 교체로 이번 통합에서 제외. 신규 AudioManager(`VoiceStarted/VoiceStopped` 확장 연결점)와 호환되는 확장 모듈로 재구현 필요.
- **원장실 출입구**: 씬에서 오브젝트 이름 미확인. Unity 에서 위치 확인 후 출입/잠금/NavMesh 동선 별도 검증.
- **Spawn_01**: 맵 담당자가 문(WallDoor_04 (4)) 앞에 `bed_016 (54)` 를 놓고 한쪽 문짝을 없애 방이 막혀, (47.3,0,-12) → (43.3,0.1,-12) 로 옮김. 의도 확인 필요.
- **조명 Baked/Realtime**: 우리 씬 값(Fluo 등 188개 Realtime)을 복구함. URP 는 비-Baked Area Light 경고를 냄(렌더 영향 없음 예상). 필요 시 일괄 Baked 로 통일 검토.
- 별도 진행: 재시작 버그, 빈 수납장 QTE 버그, box1.fbx 적용, 아이템 수량 조정.

## 후속 작업 목록 (체크포인트 이후)
1. 재시작 시 UI 및 QTE 초기화 버그
2. 빈 수납장 SkillCheck QTE 발생 버그
3. Monster 2 등장 직후 PathPartial 문제 (F11 직후 Hound→Player 경로가 PathPartial, 4번째 비콘 이후엔 PathComplete)
4. 원장실 출입구 및 이동 동선 (오브젝트 이름 미확인)
5. 비콘 Pipe QTE 실제 검증 (이번엔 입력 방식을 몰라 미실행)
6. AA 배터리 모델 크기 및 깨진 라벨 (`AA_Label` 글자가 모델 위에서 깨져 보임)
7. box1.fbx 약장 모델 적용
8. 아이템 배치 및 수량 조정
9. 전체 조명 및 Area Light 경고 (Fluo 등 188개 Realtime Area Light, URP 경고)
10. 오디오 실제 재생 검증

## 안정화 단계 (재시작 버그 / 수납장 QTE) 이후 후속 작업 목록
(이번 단계에서는 수정하지 않음)
- 아이템 드랍 수량 증가
- 원장실 출입 동선 확보: Monster 2 등장 컷씬에서 몬스터가 나오는 장소가 원장실이며 향후 최종 스테이지로 사용 예정. 침대 폭발 이후에도 플레이어가 진입할 수 있어야 함
- Monster 2 등장 직후 PathPartial 문제
- 허공에 떠 있는 배터리 문제
- R키 재장전 시 구버전 배터리 모델이 나오는 문제
- box1.fbx 약장 모델 적용
- 구급상자 들고 있는 자세 및 사용 연출 개선
- 씬 조명 및 Lux 값 조정
- Monster 2 인게임 사운드 및 컷씬 사운드 누락
- Monster 2 컷씬과 인게임 모델의 외형 차이
- 비콘 Pipe QTE 검증
- 신규 맵 및 배터리 모델 최종 시각 검증

## Monster 2 / 원장실 작업 기록
- 원장실 = Monster 2 등장 컷씬의 방(약 x112~123, z15~20). 침대 비산 후 정착 침대가 입구/내부를 막던 문제를
  `Monster2ReleaseSequence.ClearReservedZone` 으로 해결 (정착 직후 암전 중, 확보 구역에 걸친 침대만 구역 밖 빈 바닥으로 이동. 삭제/Collider 비활성화 없음).
  확보 구역 크기는 인스펙터(`reservedHalfWidth/Behind/BeyondExit/Clearance`)에서 조정. 최종 스테이지에서 방 전체를 쓰려면 값을 키우면 된다.
- `VerifyHoundPath` 는 경로가 Partial 이어도 침대 탓으로 단정하지 않는다(잠긴 튜토리얼 문 등 다른 원인이면 침대 장애물 유지).
- Monster2CutsceneCamera 에 UniversalAdditionalCameraData(renderPostProcessing=true) 추가: 컷씬만 후처리 없이 밝고 뿌옇게 보이던 문제.

## Monster 2 사운드 조사 결과 (수정하지 않음) — 오디오 담당 요청 사항
- 파일: `Assets/Audio/Clips/Enemy2/en2_{idle,chase,spot,charge_ready,charge,charge_hit,step_walk}_01.wav` 7개 존재하지만
  **전부 바이트가 같은 6.6KB 플레이스홀더**다 (md5 동일). 실제 음원 교체 필요.
- AudioManager 등록: `AudioManager.prefab` 47개 항목에 `en2_*` 가 **하나도 없다** → 등록 누락.
- 호출 연결: Monster_2 프리팹에는 `MonsterVoice`(또는 동등 컴포넌트)가 없고 `MonsterHoundAI` 는 AudioManager 를 부르지 않는다.
  Enemy1 은 `MonsterVoice` 로 연결돼 있다 → 이벤트 연결 누락. 발소리는 애니메이션 이벤트 `OnFootstep(이름)` 방식이라 Monster_2 클립에도 이벤트가 필요.
- 컷씬: `Monster2ReleaseSequence` 에 포효/침대 파괴/돌진/침대 낙하 사운드 트리거가 전혀 없다 → 트리거 자체가 없음.
- 추격 BGM 은 `MusicSounds` 가 `MonsterHoundAI.IsChasingPlayer` 를 세므로 동작한다.
- 요청: (1) en2_* 실제 음원 제작/교체 (2) AudioManager 에 이름 등록 (3) Hound 에 MonsterVoice 류 호출 연결 + 발소리 이벤트 (4) 컷씬 단계별 사운드(포효, 침대 파괴, 돌진, 착지) 트리거 추가.
