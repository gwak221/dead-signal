# 통합 노트 (integration-on-main-1124) — 최신 상태

세션이 초기화돼도 이 문서만 보고 이어서 작업할 수 있도록 현재 상태를 한곳에 정리한다. (마지막 갱신: 알파테스트 전날, EXE 빌드 전)

## 0. 기준 / 브랜치 / 커밋
- 작업 브랜치: `integration-on-main-1124` (origin/main a09b319 에서 분기). **push / merge 금지 상태** (upstream 미설정).
- 백업(보존): `integration-team-updates-1123` = 8d09f62 (원격에도 있음). 백업 파일: `C:\Users\haeah\Downloads\dead-signal-backup-1124\`
- 기준: 게임플레이 = origin/main a09b319, 최신 맵 = origin/mapwork-taema 9de20f9, 우리 고유 기능 = 8d09f62
- 공식 씬: `Assets/Scenes/DeadSignal_Main.unity` (텍스트 YAML, GUID `3827ac68…` 유지) / NavMesh 에셋 GUID `c3bd4705…` 유지
- 커밋 이력 (위가 최신)
  - `1831cc7` feat: improve item distribution, flashlight balance and medkit interactions
  - `0ba719b` fix: unify AA battery models and update medical cabinets
  - `e9d6aac` fix: secure director room access and improve Monster 2 sequence
  - `5940c44` fix: restore runtime systems on restart and correct cabinet QTE gates
  - `7113868` checkpoint: integrate latest hospital map and gameplay systems
- **미커밋 변경 (이 문서 갱신 시점)**: 조명 재조정 2회분 + filing 배터리 획득 수정 + bench 장식 처리 + 문서.
  파일: `PlayerInteraction.cs`, `DeadSignal_Main.unity`, `HospitalMapProfile.asset`, `Poster1~3.mat`, `wp_led_cool_dim.mat`, `Openables/Materials/wp_led_warm.mat`,
  FBX meta 4개(`chandelier/downlight/linear_pendant/smoke_detector.fbx.meta`), 신규 `Assets/Materials/wp_led_{cool,warm}_soft.mat`, `wp_red_led_soft.mat`.

## 1. 구현된 것 (통합 결과)
- **맵**: main 의 b06c463 맵 + mapwork 9de20f9 변경(xrayRoom 그룹, tile_chunks, 침대/휠체어/문짝 이동, DoorLeaf_L.004 삭제, bed_016 (77)). NavMesh 재베이크.
- **오디오**: main 의 AudioManager 채택(구 AudioSystem 과 혼용 안 함). 방별 룸 존/리버브/차폐는 제외(아래 후속 작업).
- **재시작 버그**: 씬 재로드 시 `AfterSceneLoad` 부트스트랩이 다시 실행되지 않던 문제. `HeartRateEcgView`, `PlayerCaughtSequence`, `LootQteBootstrap` 에 `sceneLoaded` 재부착 추가, `LootQteGate` 정적 레지스트리는 `sceneUnloaded` 에서 비움.
- **수납장 QTE**: 게이트를 가구 루트가 아니라 '아이템이 든 개폐부'(서랍/하단 찬장 쌍/약장)에만 붙임. linked 개폐부는 한 번의 QTE 로 함께 풀림.
- **원장실 / Monster 2**: `Monster2ReleaseSequence.ClearReservedZone` — 침대 정착 직후(암전 중) 방 내부·입구·출구 앞 복도에 걸친 침대를 구역 밖 빈 바닥으로 이동(삭제/Collider 비활성화 없음).
  구역 크기는 인스펙터 `reservedHalfWidth/Behind/BeyondExit/Clearance`. `VerifyHoundPath` 는 침대 때문일 때만 장애물을 해제.
  컷씬 카메라(`Monster2CutsceneCamera`)에 URP 카메라 데이터(후처리 켜짐) 추가 → 인게임과 같은 후처리.
- **AA 배터리**: 획득용/재장전용(`AABatteryViewModel`) 모두 `AAbattery.fbx` 모델 + Unlit 재질(`Assets/Prefabs/Materials/`), `AA_Label` 금색.
- **약장**: `box1.fbx` 적용(Door_PlusX 베이크 메시만 갱신, 변환 역산). **투명화 버그는 여전히 발생(후속 작업)**.
- **구급상자**: 손에 드는 자세(`PlayerInventory.MedkitHoldCamPos/Tilt`)와 사용 모션(`PlayerInteraction.AnimateMedkit`) 개선 — 사용자 검증 완료(수정 금지).
- **손전등**: 소모 `drainPercentPerSecond = 2/3` (1.5초당 1%, 연속 약 150초), 스턴 35%, 0%에서만 재장전 — 재장전 연출 검증 완료(수정 금지).
- **filing 배터리 획득 버그**: 열린 서랍의 속이 찬 BoxCollider 가 시야 검사를 막던 문제. `PlayerInteraction.IsOwnOpenContainer` 로 '아이템이 든 열린 서랍 자신'의 콜라이더만 통과 — 사용자 검증 완료(수정 금지).
- **장식용 처리(DecorativeOpenable)**: `filing_005 (1)`, `filing_006 (1)`(둘 다 Z 311° 기울어진 장식용, 개폐/안내/QTE 없음, 내부 아이템 없음, 모델·콜라이더 유지). 기존: `Room_WasteStorage/filing`, `med_cabinet_005 (1)/(2)` 등. `filing_006`, `filing_006 (2)` 는 정상적으로 열리는 가구(내부 AA 유지). (`bench_b_003 (12)` 에 잘못 붙였던 표시는 제거해 원복.)

## 2. 아이템 최종 수량 / 배치
- 비콘용 배터리 `BatteryPickup` **5** (battery_002/003/004/004 (1)/004 (2)) — **절대 수정 금지** (위치/회전/모델/참조)
- 손전등 AA `FlashlightBatteryPickup` **14**
  - 튜토리얼: `…_cab4` = `med_cabinet_004` **맨 위 선반**(이동/삭제 금지, `cabinetDoor` 유지)
  - filing 서랍: `filing003`(filing_003 서랍3), `filing004`(filing_004 서랍3), `filing006`(filing_006 서랍2), `filing006_2`(filing_006 (2) 서랍3)
  - 벽 약장: `cabinet004_4`(med_cabinet_004 (4)), `cabinet005`, `cabinet006`
  - 상판/바닥: `lobby_floor`(로비 바닥), `xray_desk`, `waste_cart1/2`(폐기물 창고 wire_cart), `southEast_cart`(wagon_v2), `wardSouth_desk`(check_in)
- 구급상자 `MedkitPickup` **7**: (115,6) tea_niche(1), (37,12) tea_niche_001, (-5,-1) 시작방, (52,-7) tea_niche_001 (1), (10,37), (107,36) filing_003 서랍2, (90,-13) 수술실 카트
- 아이템이 없는 방: 상담실, 화장실, Room12, Room07, 원장실(최종 스테이지 예정)
- 참고: `filing_006 (1)` 은 사용자 확인으로 장식용 처리 완료(비워 둠).

## 3. 조명 (현재 값 / 되돌리는 법)
- **핵심 사실**: Rectangle(Area) 라이트 246개(`Fluo` 187, `Moon` 59)는 Realtime 이라 URP 가 렌더하지 않는다 → 조명 기여 0, 경고 188개의 원인. **삭제/Baked 변환 안 함**(형광등 메시의 발광만 보임).
  실제로 빛을 내는 것: Aux 포인트 73~81개, 비상등 12, 튜토리얼 스포트 4~5, Ceil 3.
- RenderSettings(씬): ambient Sky (0.036,0.046,0.062) / Equator (0.050,0.062,0.072) / Ground 0.014, fog 0.05, fog 색 (0.020,0.028,0.031)
- Global Volume `HospitalMapProfile`: postExposure 0.25, Bloom 강도 0.4 / 임계값 1.3 (원래 0.7/1.0)
- Aux 포인트(공간별): 시작방 5.5, 병실 4.5, 수술실 9(청백색), 엑스레이실 4.5(청록), 원장실 7.5(붉은색), 폐기물/기타 5, 범위 7~10.
  **벽에서 1.2m 이내였던 Aux 25개는 벽에서 최대 1.5m 로 이동, 시작방 외 벽 근접 23개는 ×0.6.** (시작방 포스터 벽면 과노출 원인: `Aux_002`/`Aux_006` 이 벽에서 0.55m, 강도 12)
- 비상등 12(그림자는 시작방·수술실 4개만 유지, 강도 12), 게시판 스포트 0.6, 주황 Ceil ×0.8
- 발광 재질(FBX 내장 재질을 외부 mat 로 복제 후 Material Remap): `wp_led_cool_soft`(다운라이트/선형 조명 115개, 최대 1.2), `wp_led_warm_soft`(1.4), `wp_red_led_soft`(2.8), `wp_led_cool_dim`(1.4), `wp_led_warm.mat` ×0.4, `Poster1~3` 0.30
  → 원복하려면 해당 FBX 임포터의 Material Remap 해제 + 값 복원. 기존 조명 프리셋(`HorrorLightingPreset`, `DeadSignal_OriginalLighting`)은 그대로.
- 방별 밝기/분위기는 사용자가 플레이하며 계속 피드백 중. 수술실·엑스레이실은 정면 벽이 매우 어두울 수 있음.

## 4. 후속 작업 목록
1. **box1.fbx 약장 투명화 버그** — 여전히 발생(사용자 확인). 문 메시(Door_PlusX)만 교체했고 box.009 평면은 베이크에 포함된 적 없음. 원인 재조사 필요(재질/렌더 큐/컬링/문 뒷면 법선 등).
2. **주사기 아이템 통합** — 팀원이 거의 완성. 효과: 사용 후 일정 시간 달려도 심박수가 상승하지 않음. **작업물 전달 대기 중, 임의 구현 금지.** 받으면 `PlayerInventory`(슬롯), `HeartRateController`, 사용 모션과 통합.
3. **Monster 2 사운드** — 이번 알파에서는 제외 결정. (조사 결과: `Enemy2/en2_*.wav` 7개는 md5 동일한 6.6KB 플레이스홀더, AudioManager 항목에 `en2_*` 없음, Hound 에 `MonsterVoice` 류 없음, 컷씬 사운드 트리거 없음)
4. **방별 룸 오디오 존/리버브/차폐 복구** — main 의 AudioManager 교체로 삭제됨(구 시스템은 8d09f62). `AudioManager.VoiceStarted/Stopped` 확장점으로 `DeadSignal.Audio` 확장 모듈을 새로 작성하는 방식으로 가능(AudioReverbZone + 방 환경음 크로스페이드 + AudioSource 로우패스). Zone 은 씬 독립 루트로.
5. 비콘 Pipe QTE 실제 플레이 검증(F11/F9 는 `DebugComplete` 로 진행 상태만 채움).
6. 원장실 최종 스테이지 구현(현재 접근성만 확보). 수술실/엑스레이실/원장실 조명 세부 조정.
7. 맵 `BatteryPickup`(구버전 `battery.fbx`)과 AA 모델 통일 여부 결정(요구 범위 밖이라 보류).
8. Area Light 188 경고 정리 방침 결정(삭제/Baked 금지 상태). 필요하면 Aux 포인트 추가(성능 확인 필요).
9. 구급상자는 Lit 재질이라 어두운 곳에서 어둡게 보임(조명 수정과 함께 검토).

## 5. 알파 빌드(EXE) 준비 메모 — 아직 빌드하지 않음, 빌드 설정 수정하지 않음
- 개발용 단축키는 모두 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 안 → **Release 빌드에는 컴파일되지 않음. 배포용은 Development Build 를 끄고 빌드할 것.**
  F6~F8 `PlayerBodyStatus.cs`(109~121행), F9 `ExitCutsceneController.cs`(64~79), F10/F12 `PlayerCaughtSequence.cs`(184~192), F11 `Monster2ReleaseSequence.cs`(Update), T `SkillCheckManager.cs`(136, `debugKeyT` 기본 false), `Beacon.DebugComplete()`.
- 에디터 전용(빌드 자동 제외): `Assets/HospitalMap/Editor/*`(씬 병합 도구 등), `Assets/Scripts/Environment/Editor/*`, `Assets/Audio/Editor/*`.
- 구버전 UI: 씬 `HeartRateUI` 의 `HeartIcon/HeartRate_Text` 는 `HeartRateEcgView` 가 런타임에 숨김. `Debug.Log` 45곳은 보호되지 않음.
- Build Settings: `DeadSignal_Main.unity` 하나만 등록(GUID 유지).
- 빌드 전 확인: 콘솔 오류 0, Missing Script 0, NavMesh/조명 확인.

## 6. 작업 도구 / 유의사항 (세션 복구용)
- Unity 연동: `unity command ...` (Editor 포트 7800). Play Mode 에서 입력 시뮬레이션이 필요하면 `Application.runInBackground = true` 와 `InputSystem.settings.backgroundBehavior = IgnoreFocus` 설정 후 **가상 Keyboard 디바이스에 `QueueStateEvent`** 사용(기존 키보드는 포커스 없으면 무시됨). 재컴파일하면 이 설정이 초기화됨.
- 분석/검증용 스크립트는 `AgentScripts/` (`.gitignore` 제외, 커밋 대상 아님). 스크린샷은 `Temp/Verification/`.
- 씬 수정 시 프리팹 인스턴스의 필드 변경은 `SerializedObject` + `PrefabUtility.RecordPrefabInstancePropertyModifications` 로 저장해야 한다(직접 대입은 저장되지 않는 경우가 있었음).
- 맵 그룹(HospitalMap 하위) 로컬 좌표: 월드 = (x, -z, y) 형태(로컬 z 가 월드 y 의 음수).
- 원칙: `HospitalMap.unity` 원본 수정 금지, 비콘 배터리 5개 수정 금지, 승인 없는 commit/push/merge 금지.
