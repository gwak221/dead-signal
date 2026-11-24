# 통합 노트 (integration-on-main-1124) — 최신 상태

세션이 초기화돼도 이 문서만 보고 이어서 작업할 수 있도록 현재 상태를 한곳에 정리한다. (마지막 갱신: 알파테스트 전날, EXE 빌드 전)

## 0. 기준 / 브랜치 / 커밋
- 작업 브랜치: `integration-on-main-1124` (origin/main a09b319 에서 분기). **push / merge 금지 상태** (upstream 미설정).
- 백업(보존): `integration-team-updates-1123` = 8d09f62 (원격에도 있음). 백업 파일: `C:\Users\haeah\Downloads\dead-signal-backup-1124\`
- 기준: 게임플레이 = origin/main a09b319, 최신 맵 = origin/mapwork-taema 9de20f9, 우리 고유 기능 = 8d09f62
- 공식 씬: `Assets/Scenes/DeadSignal_Main.unity` (텍스트 YAML, GUID `3827ac68…` 유지) / NavMesh 에셋 GUID `c3bd4705…` 유지
- 커밋 이력 (위가 최신)
  - `0139ec2` fix: refine hospital lighting and item interactions
  - `1831cc7` feat: improve item distribution, flashlight balance and medkit interactions
  - `0ba719b` fix: unify AA battery models and update medical cabinets
  - `e9d6aac` fix: secure director room access and improve Monster 2 sequence
  - `5940c44` fix: restore runtime systems on restart and correct cabinet QTE gates
  - `7113868` checkpoint: integrate latest hospital map and gameplay systems
- **미커밋 변경 (이 문서 갱신 시점)**: 팀원 주사기 통합(8a86076 선별 적용) + 주사기 4개 추가 배치 + G키 아이템 버리기 + 문서.
  신규 `Assets/Syringe/`, `HeldItemOutline.cs`, `ItemUseScreenEffect.cs`(+meta), 수정 `HeartRateController`, `FirstPersonController`, `PlayerInventory`, `PlayerInteraction`, `MedkitPickup`, `RecoveredItemLog`, `AimOutlineHull.shader`, `LootQteGate`, `DeadSignal_Main.unity`.

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

### 1-b. 주사기 (팀원 PR #20 / 커밋 8a86076 선별 통합)
- 통합 방식: `git diff b7a8df1 8a86076`(씬 제외)를 `git apply --3way` 로 적용 → 우리 `PlayerInventory`/`PlayerInteraction` 수정(구급상자 자세/모션, 서랍 획득, 아래 버리기)은 보존. 씬은 통째로 가져오지 않고 `ItemUseScreenEffect` 루트 오브젝트(팀원 값)와 Syringe 프리팹 인스턴스만 이식.
- **실제 효과(정정)**: F 로 획득 → 슬롯 선택 후 E 로 사용(주입 애니메이션 약 0.65초) → **15초 동안 심박수에 의한 달리기 제한(Sprint Lock)만 해제**. **심박수(BPM) 자체는 기존대로 상승하고 몬스터 탐지 배율도 그대로.** 효과 중 화면 가장자리에 청록색 효과(`ItemUseScreenEffect.Sustain`), 중복 사용 시 지속시간 갱신, 사용 후 아이템 소비. 효과 종료 시 BPM 이 높으면 바로 다시 달리기 잠김.
  (이전 문서/대화의 "심박수 상승 억제" 설명은 틀렸음. `HeartRateController.ApplySprintUnlock`, `IsSprintUnlocked`.)
- 재시작: `HeartRateController` 가 씬과 함께 새로 만들어져 효과가 초기화됨(검증 완료).
- 주사기 5개: `Syringe`(팀원 배치, 시작방 `tea_niche_002 (1)` 상단 칸 안 → 기존 QTE 규칙 적용), `Syringe_lobby`(로비 `drawer(2)` 상판), `Syringe_pharmacy`(약제실 `med_cart` 상판), `Syringe_room07`(Room07 재활 플랫폼 `stair2` 상판), `Syringe_cabinet001`(`med_cabinet_001` 맨 위 선반, QTE).

### 1-c. G키 아이템 버리기
- 기존 `EjectSlotItemToWorld`(부상 시 슬롯 잠금용, 물리로 굴림)는 그대로 두고, `PlayerInventory.TryDropSelected` 를 새로 추가. G 키는 기존에 쓰이지 않음(충돌 없음).
- 대상: 2/3번 자유 슬롯의 **비콘 배터리, 구급상자, 주사기**. 손전등(1번)과 AA 슬롯(4번)은 불가.
- 처리: 삭제/복제 없이 같은 오브젝트를 플레이어 앞 **안전한 바닥**에 내려놓음(원래 눕힌 자세와 `worldScale`, 콜라이더, 획득 컴포넌트 복원). 후보 위치마다 바닥(발 높이 ±0.3, 위로 향한 면) + NavMesh 위 + 카메라 시야가 안 막힘 + 아이템 크기 박스가 다른 콜라이더와 안 겹침을 검사. 앞 → 옆 → 뒤 순으로 시도하고 모두 실패하면 `NO SPACE TO DROP` 으로 취소(손에 그대로).
- `LootQteGate.Unregister` 로 이전 수납장 게이트에서 분리 → 닫힌 수납장 때문에 버린 아이템을 못 줍는 일 방지.
- 불가 상태: 사용 중(구급상자/주사기), QTE(SkillCheck/Pipe), 숨기, 포획, 게임 종료, 메뉴, 컷씬(Exit/Monster2), 조작 잠금.
- UI: 버릴 수 있는 아이템을 들면 `[G] DROP` 안내(구급상자/주사기 프롬프트 뒤에 붙음, 비콘 배터리는 단독 표시). 인벤토리가 가득 차면 `INVENTORY FULL - SELECT AN ITEM, PRESS [G] TO DROP`.
- 검증: 비콘 배터리 획득 → G → 바닥에 복원 → F 재획득, 비콘 배터리 총 5개 유지.

- 비콘용 배터리 `BatteryPickup` **5** (battery_002/003/004/004 (1)/004 (2)) — **절대 수정 금지** (위치/회전/모델/참조)
- 손전등 AA `FlashlightBatteryPickup` **14**
  - 튜토리얼: `…_cab4` = `med_cabinet_004` **맨 위 선반**(이동/삭제 금지, `cabinetDoor` 유지)
  - filing 서랍: `filing003`(filing_003 서랍3), `filing004`(filing_004 서랍3), `filing006`(filing_006 서랍2), `filing006_2`(filing_006 (2) 서랍3)
  - 벽 약장: `cabinet004_4`(med_cabinet_004 (4)), `cabinet005`, `cabinet006`
  - 상판/바닥: `lobby_floor`(로비 바닥), `xray_desk`, `waste_cart1/2`(폐기물 창고 wire_cart), `southEast_cart`(wagon_v2), `wardSouth_desk`(check_in)
- 주사기 `SyringePickup` **5** (위 1-b)
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
2. ~~주사기 아이템 통합~~ — 통합 완료(위 1-b). 남은 것: 주사기 보유 중 시각(손에 든 모습)·밸런스(효과 15초, 개수 5)는 사용자 플레이 피드백 대기.
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

---

## 추가 수정 (4b2fd07 이후, 미커밋)

1. **anesthesia 정지 상태 미작동**: `PlayerInteraction.HasClearLine` 이 서랍 후보를 '가구 몸통 MeshCollider 에 가려짐'으로 제외했다(서랍 Collider 가 몸통에 일부 묻혀 있어 정지 시 십자선이 몸통 앞면에 먼저 맞음). 같은 가구의 몸통 면이고 대상 표면이 0.3m 이내면 가려지지 않은 것으로 처리(`IsOwnFurnitureFace`). 정지 상태 일괄 측정(개폐부 110개): 실패 12 → 7 (tea_niche 0, anesthesia 6→3). 남은 7은 anesthesia(1) 서랍 3개, cardboard 박스 덮개 4개 — 몸통이 서랍보다 앞에 있는 지오메트리 문제로 미해결.
2. **약장**: AA 4개를 선반 앞쪽으로 약 0.11m 이동(앞면까지 약 0.19m). `med_cabinet_001` 씬에서 삭제(프리팹/모델 그대로, 내부 아이템 없었음) — 주사기는 `med_cabinet_004 (5)` 상단 선반으로 이전.
3. **약품실(DoorLeaf_L.007 → Room_05, 약 77~96, 24~39)**: `medkit_006` 을 입구 쪽 `box_stack` 위(91.1, 0.72, 26.0)로 이전. 구급상자 총 7개 유지.
4. **랜덤 스폰**: `ItemSpawnPoint`(후보 지점, Gizmo/Inspector 로 추가·이동·허용 종류 설정) + `RandomItemSpawner`(Awake 에서 씬 로드마다 재배치, 같은 플레이 중 고정). 지점 총 40개(고정 6 제외 34개): 기존 아이템 자리 20 + 서랍(filing) 12 + tea_niche 2. 랜덤 대상: AA 13, 구급상자 5(002/003/007/009/010), 주사기 2(Syringe_lobby, Syringe_cabinet004_5). 고정: AA cab4, 구급상자 006(약품실)/008(시작방), 주사기 3(시작방/약제 카트/Room07). 한 개폐부에 1개, 구역(24m 격자)당 상한 4, 열린 면 최대 3, 후보 부족 시 원래 자리 → 수량 보존. QTE 는 배치 후 `LootQteBootstrap` 이 실제 위치로 부착. 재시작 3회 확인: 위치 매번 변경, 수량(비콘5/AA14/구급7/주사기5) 유지.
   - 한계: 후보가 filing 방 두 곳(약 8,38 / 106,35)에 몰려 있어 해당 구역에 아이템이 5~7개 배치될 수 있음. electrical/anesthesia/cardboard/fire 등에는 후보 지점이 아직 없음 → 씬에서 `ItemSpawnPoint` 를 추가하면 자동 반영(후속).
5. **Monster 2**(`MonsterHoundAI`): 시야 상실 후 추격 유지 `chaseLoseSightGrace` 1.2 → 8초(Monster 1 은 5초), 시야각 75 → 100°. 시야 밖이면 플레이어 실시간 위치 대신 **마지막 발견 위치**로 이동, 도착 후 제자리에서 둘러봄. 충돌 후 정지(Recovery) 뒤에도 숨지 않았다면 마지막 위치로 계속 추적. 숨기 시 기존대로 즉시 해제. Monster 1 무변경. (씬 인스턴스 값에 직렬화돼 있어 씬에도 반영)
6. **조작키**: CONTROLS 목록에 `G — Drop Item` 추가(메인/ESC 공용).
7. **R키 재시작·오디오**: 원인 = `AudioManager` 가 DontDestroyOnLoad 라 씬 재로드 시 재생 중이던 사망 BGM 이 남음. `AudioManager` 가 씬(Single) 로드 시 `StopAll(0)` 수행(환경음/음악 스크립트는 IsPlaying 을 보고 다시 시작). 사망 BGM 재생 후 재시작 → 활성 음 목록에서 사라짐, AudioManager 1개 확인. `GameProgress` 재시작은 1회만(인스턴스 플래그), 결말 화면에서는 손전등 R 재장전 차단.
8. **구급상자 사용 모션**: 모든 원격 브랜치/에셋에서 구급상자 모델(medkit.fbx) 외의 애니메이션/컨트롤러는 발견되지 않음 → 현재 구현 유지.


---

## 추가 수정 2 (미커밋)

1. **랜덤 스폰 후보 확대**: `ItemSpawnPoint` 85개(고정 6 제외 79개, 수납장 안 70). 구성: 기존 아이템 자리 26(고정 6 포함) + filing 서랍 12 + tea_niche 15 + 약장(DoorPair) 선반 14(0.71/0.93m) + anesthesia 서랍 6 + cardboard 박스 8 + 소화전함 2. 제외: electrical 패널 21개(몸통이 벽에 묻혀 내부가 벽 Collider 와 겹침), 일부 소화전함/tea_niche(NavMesh 접근 불가), 일반 문, 장식용 가구. 지점은 가구 메시의 위쪽 면(서랍 바닥/선반/박스 바닥)에서 산출하고 겹침·NavMesh 검사 통과분만 사용. 약장 지점은 기존 AA(cab4)의 앞쪽 오프셋과 같은 기준.
2. **구역 상한 미작동 원인**: 구역을 24m 격자로 나눠 같은 방 가구가 서로 다른/같은 칸으로 갈렸고, 후보가 filing 방 두 곳에 몰려 상한 적용 후보가 소진되면 상한을 풀고 계속 배치했다. → 구역 = 후보 지점끼리 6m 이내로 이어진 무리(22개, `clusterRadius`), 구역당 최대 2개(`maxPerZone`)로 변경. 재시작 3회: 구역당 최대 2, 15~17개 구역에 분산, 수량(비콘5/AA14/구급7/주사기5)과 고정 11/랜덤 20 유지.
3. **개폐 불가 가구**: `IsOwnFurnitureFace` 허용 깊이 0.3 → 0.9m(같은 가구 몸통 면일 때만). 정지 상태 측정(장식용 제외 106개): 실패 1(anesthesia_002 최상단 서랍, 측정 시선 기준). 이전 보고의 cardboard 4개 실패는 장식용(DecorativeOpenable) 박스가 측정에 섞인 것이었고 실제 문제 아님.
4. **재시작 경로 통일**: `GameProgress.ReloadScene()` 한 곳에서 timeScale/AudioListener 복구 + 씬 재로드. R 재시작과 메뉴 Quit to Title 이 같이 사용.
5. **Pipe QTE**: 시작점(좌상단)에서 오른쪽과 아래쪽 모두 전력 출력(`startFeedDirections={1,2}`), 어느 쪽으로든 끝점에 닿으면 성공. 해결 경로는 판마다 두 방향 중 하나에서 생성(두 입구 모두 막지 않음). 300판 검증: 전부 해결 가능, 시작 시 이미 해결 0, 경로 오른쪽 147/아래 153. 퍼즐 이미지/회전/난이도/취소 무변경(시작 이미지는 그대로).
6. 이전 요청 확인: med_cabinet_001 삭제됨(내부 아이템 없음, 주사기는 med_cabinet_004 (5) 상단), 약장 AA 앞쪽 이동 적용, 약품실 구급상자 medkit_006 = box_stack 위 (91.1, 0.72, 26.0), CONTROLS 에 `G — Drop Item` 있음.


---

## 추가 수정 3 (미커밋)

1. **cardboard / 소화전함 완전 제외**: 해당 후보 지점 10개 삭제, 모든 `cardboard_box*`·`fire_cabinet*` 에 `DecorativeOpenable` 적용(개폐/안내/외곽선/QTE 없음, 모델·Collider 유지). `anesthesia_002`(51,40)도 정면(-x/-z)에 침대 `bed_016 (27)` 이 0.4m 앞에 붙어 서랍 접근이 불가능해 장식용 처리하고 후보 2개 삭제.
2. **최종 후보 지점 73개**(고정 6 제외 67, 수납장 안 58). 구성: 기존 자리 26 / filing 12 / tea_niche 15 / 약장 선반 14 / anesthesia 4. 재시작 3회 검증: 수량 비콘5·AA14·구급7·주사기5, 고정 6(아이템 기준; 고정 11 = 비콘 5 + 고정 6), 랜덤 20, 구역당 최대 2, 구역 13개 사용, 열린 면 1개, 장식용 가구 내부 아이템 0, 약품실 구급상자/시작방 아이템 유지.
3. **가림 허용 판정 보강**(`IsOwnFurnitureFace`): 허용 깊이 1.2m + '개폐부 정면 쪽 시점일 때만'(서랍=열리는 방향, 문=몸통 중심→문 방향). 뒤/옆에서 몸통을 뚫고 여는 경우를 줄임. 한계: 얇은 가구(filing/tea 일부)는 뒤쪽에서도 열리는 경우가 일부 남음.
4. 남은 문제: `anesthesia_002 (8)`(69,-23)는 쓰러진 상태라 서랍 일부가 열리지 않음(장식 처리 안 함 — 아이템 후보에도 없음). electrical 패널은 후보 제외 유지.


---

## 추가 수정 4 (미커밋)

1. **장식용 추가**: 쓰러진 `anesthesia_002 (8)` 와 `elec_panel*` 22개(씬 내 전부)에 `DecorativeOpenable` 적용(개폐/안내/외곽선/QTE 없음, 모델·Collider 유지). 이 가구들에 걸린 스폰 후보는 없음(후보 73개 그대로).
2. **뒤쪽 개폐 차단**: `PlayerInteraction.FrontOk` — 서랍(Slide)은 열리는 방향, 문(Hinge)은 '가구 몸통 Collider 중심 → 문 중심' 방향을 앞으로 보고, 시점이 그 앞쪽 반공간(옆/약간 비스듬 포함)일 때만 대상이 된다. 직접 조준(direct)과 보조 SphereCast 모두에 적용(이전엔 몸통에 가려질 때만 검사). 가림 허용 깊이(1.2m)는 늘리지 않음. 측정: 정면 접근 정지 상태 실패 0, 뒤쪽 접근 개폐 2/19(이전 대부분 열림). 정면 측정의 일부 '실패'는 쌓인 서랍에서 위/아래 이웃 서랍이 조준되는 측정 방식 문제.
3. **Syringe_room07**: 기존 위치(58.4, 0.94, 22.6)는 벽(WallFill_022)에 가려져 접근 가능한 바닥에서 집을 수 없었음. 같은 방에 수납장이 없어(가장 가까운 것 20m 밖) 같은 발판(stair2 (2)) 북쪽 가장자리 (58.33, 0.94, 21.96) 로 이동 — 북쪽 바닥(58.2, 19.8)에서 거리 2.3m, F 줍기 판정 성공 확인. 고정 아이템 유지.
4. 수량/비율 무변경: 비콘5 / AA14 / 구급7 / 주사기5, 고정 11 / 랜덤 20.

---

## 추가 수정 5 (미커밋, 148891c 이후)

1. **cardboard / 소화전함 개폐 복구**: 이전에 내가 일괄 부여했던 `DecorativeOpenable` 15개 제거(원래 장식이던 cardboard_box_002 / cardboard_box_006 (1) 2개는 유지). 상호작용 안내/조준 외곽선 복구, 내용물이 없어 QTE 없이 열림. 스폰 후보에서는 계속 제외. electrical 패널 22개, anesthesia_002 (8), anesthesia_002 (51,40) 장식 처리는 그대로.
2. **가구 루트당 아이템 1개**(`RandomItemSpawner`): 가구 루트(OpenableFurniture / 약장 DoorPair) 단위로 최대 1개, 고정 아이템이 든 가구(시작방 포함)에는 랜덤 아이템을 넣지 않음. 후보가 없으면 가구당 상한을 무시하지 않고 원래 자리에 둔다(예전의 'home 지점으로 폴백' 제거). 시작방 tea_niche_002 (1) 의 고정 구급상자+주사기 2개는 예외(시작방). 구역당 2개는 후보가 부족한 마지막 몇 개에서 3개까지 허용될 수 있음(검증 시 최대 3).
3. **구급상자 7 → 4**: 삭제 medkit_003/007/010. 남음: medkit_008(시작방 tea_niche 고정), medkit_006(약품실 고정), medkit_002/009(랜덤 2). 수량을 강제로 맞추는 코드는 없음(랜덤 대상은 씬의 리스트 `randomMedkits` 에 있는 것만). 총 28개 = 비콘 5 + AA 14 + 구급 4 + 주사기 5, 고정 11(비콘5+AA1+구급2+주사기3) / 랜덤 17(AA 13, 구급 2, 주사기 2). *(이전 문서의 '랜덤 20'은 구급상자 감소로 17 이 됨)*
4. **스폰 후보 정밀 검사**(`AgentScripts/Aud.cs`, 커밋 대상 아님): 아이템 OBB 와 가구 메시 삼각형 SAT 검사 + 윗면 높이 재산출. 결과: 서랍/선반 바닥보다 아래로 1~2cm 파묻히던 후보 44개 재배치, 종류별 겹침 후보 허용 종류 축소(43개), 모든 종류가 불가한 후보 7개 삭제. 최종 후보 66개(고정 6 제외 60). 허용 종류: AA 60 / 구급상자 5 / 주사기 4. 약장 AA 4개는 선반 위 약 3.4cm 떠 있던 것을 선반 윗면으로 내림. '앞쪽에서 잘 보이는가' 검사는 신뢰도가 낮아(고정 위치까지 전부 실패) 결과에 반영하지 않음.
5. **비콘 배터리 원본 비교**(씬 YAML): battery_002/003/004/004 (1)은 a09b319(main), 7113868, 현재 모두 동일(위치/회전/부모/활성/컴포넌트). battery_004 (2)만 main 과 다름: main (12.064, -12.961, 0.012) → 현재 (11.105, -14.061, -0.79). 이 값은 팀원 최신 맵 `origin/mapwork-taema` 와 정확히 같고(맵 이식 때 채택), 7113868 이후 변경 없음. 5개 모두 수납장 밖 고정(부모 battery/Objects), RandomItemSpawner 대상(AA/구급상자/주사기 리스트)에 없음. 수정하지 않음 — 확인 후 결정 필요.
6. **맵/가구 겹침 목록**: `docs/map-placement-issues.md` (수정 없음, 사용자 확인용).


---

## 추가 수정 6 (미커밋)

1. **랜덤 스폰 배정 단순화**(`RandomItemSpawner`): 백트래킹/최대 흐름 폐기(백트래킹이 에디터를 멈췄음). 구급상자 -> 주사기 -> AA 순으로, 각자 유효한 후보 중 무작위 선택. 유효 조건: 허용 종류, 지점 중복 금지, 가구 루트당 1개, 고정 아이템 가구/제외 가구 금지, 같은 서랍/문 1개, 구역당 2개(시도가 절반 넘게 실패하면 3개까지), 수납장 밖 3개(후반 시도에서 6개까지). 실패하면 처음부터 다시, 최대 `maxAttempts`(40)회로 고정 -> 무한 탐색 없음. 끝내 실패하면 사전 검증된 `safeItems/safePoints`(컨텍스트 메뉴 `Bake Safe Plan` 으로 저장, 현재 17개 저장됨)를 사용한다. 기존 위치에 방치하는 폴백 없음.
2. **발견했던 버그**: 부모 Transform 이 같으면 같은 서랍으로 취급해(가구 없이 `ItemSpawnPoints` 루트에 매달린 열린 면 후보가 전부 하나로 묶임) 계획이 불가능해졌음 -> 부모가 실제 `OpenablePart` 일 때만 서랍 단위 제한(`PartOf`).
3. **시작방 약장 제외**: `med_cabinet_004 (1)`(-4.6,0,1.8, 시작방 `cab4` 옆) 은 원래 아이템이 없었고, 이 약장의 후보 지점 2개가 AA 를 받을 수 있었음 -> 후보 삭제 + `excludedRoots` 에 등록(영구 제외). 개폐/외곽선/안내 유지, 내용물 없어 QTE 없음. 튜토리얼 AA(cab4, med_cabinet_004)는 유지. cardboard/소화전함은 이름 규칙으로 항상 제외(개폐 가능, 항상 빈 상태).
4. **확인**: 시작 + 재시작 2회 -> 수량 비콘5/AA14/구급4/주사기5 = 28, 고정 6(+비콘5), 랜덤 17, 구역당 최대 2, 가구 루트 중복 없음, 지점 중복 없음, 시도 1~9회 안에 성공, `med_cabinet_004 (1)` 안 아이템 0, 콘솔 오류 0. 후보 지점 64개(고정 6 제외 58).
5. **오늘 추가/수정 요소 확인(스크린샷 `Temp/Verification/MapIssues/`)**: AA_cab4_med_cabinet_004, AA_cabinet004_4/005/006, Syringe_cabinet004_5, medkit_006_pharmacy, Syringe_room07_platform. medkit_006 은 상자 더미 위에 정상 안착(F 안내 표시), Syringe_room07 은 플랫폼 위에서 F 로 집을 수 있음. 약장 내부 샷은 약장 안이 매우 어두워 AA/주사기 모양만 보이고 메시 겹침은 육안 판정이 어렵다(정밀 검사에서는 AA 4개 선반 위 3.4cm 떠 있던 것을 내렸고, Syringe_cabinet004_5 는 겹침 경고가 있었으므로 직접 확인 필요).
6. **맵 검토 범위**: 기존 팀원 맵 배치(의도적 차단/겹침)는 조사·수정 대상 아님. `docs/map-placement-issues.md` A/B/D/E 는 참고용.


---

## 알파 빌드 (v0.1.0-alpha, 미커밋)

1. **약장 AA 시인성**: 약장(DoorPair) AA/후보 지점 20개를 문 쪽으로 5cm 이동(문 중심면 기준 깊이 0.19 -> 0.14m). 정면 방향은 '몸통 중심 -> 문 중심'(`GetDoorCenter`)으로 약장마다 계산, 모든 약장에서 같은 기준(좌우 중앙, 선반 윗면 +4mm). 고정 AA 4개와 후보 지점(안전 배치는 지점 참조라 자동 반영) 모두 적용. `med_cabinet_004 (1)` 제외 유지.
2. **디버그 코드 점검**: F6~F8(PlayerBodyStatus), F9(ExitCutsceneController), F10/F12(PlayerCaughtSequence), F11(Monster2ReleaseSequence), T(SkillCheckManager) 는 모두 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 안 -> Release 빌드에는 코드가 없음(추가 변경 불필요). `Beacon.DebugComplete` 는 위 조건부 코드에서만 호출됨. 그 외 치트/디버그 UI(OnGUI) 없음.
3. **화면 설정**: PlayerSettings - 제품명 `DEAD SIGNAL`, 버전 `0.1.0-alpha`, 기본 1920x1080, Fullscreen Window, Resizable Window 켬, Unity 기본 Alt+Enter 끔(자체 `WindowModeToggle` 이 Alt+Enter 로 전체화면/창 모드 전환), runInBackground 끔. Canvas Scaler: 모든 ScaleWithScreenSize 캔버스를 Expand 로(QTECanvas, BeaconPipePuzzle 프리팹, GameProgress/PlayerHide/PlayerCaught/Blood/Poster 오버레이) - 16:9 외 비율에서도 UI 전체가 보임. 메뉴 캔버스는 기존의 비율별 match 전환 유지.
4. **빌드**: Windows x86-64, Development/Script Debugging/Autoconnect Profiler 모두 OFF, 씬 `DeadSignal_Main` 1개. 결과 Succeeded(오류 0, 경고 251, 246MB, 2분 3초). 경로 `C:/Users/haeah/Downloads/DEAD_SIGNAL_Alpha_v0.1/DEAD_SIGNAL.exe` (저장소 밖). `*_BurstDebugInformation_DoNotShip` 폴더는 배포에서 제외. ZIP: `C:/Users/haeah/Downloads/DEAD_SIGNAL_Alpha_v0.1.zip`(README.txt 포함). 빌드가 URP 설정 에셋(`PC_RPAsset`, `UniversalRenderPipelineGlobalSettings`, `DefaultVolumeProfile`)의 셰이더 prefilter 값과 `UnityConnectSettings` 를 자동 갱신함(빌드 부산물, 의도한 변경 아님 - 커밋 시 검토/되돌릴지 결정).
5. **EXE 확인**: 실행 후 응답 유지, `Player.log` 에 오류/예외/Missing 0건, 창 크기 = 바탕화면 해상도(전체화면 창). 창 포커스를 얻지 못해 입력으로 게임 진행/Alt+Enter 를 시험하지 못함 -> 수동 확인 필요.
