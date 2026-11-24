# 오디오 등록 목록

작성일: 2026-11-23 · 수정: 2026-11-24 (`find_cilp` 클립 반영)
관련: [audio-manager.md](audio-manager.md) (코어 스펙), [../intent/audio-manager.md](../intent/audio-manager.md)

새 `AudioManager.prefab`의 `sounds` 목록에 넣을 소리와, **각 소리가 언제 나고 언제 멈추는지**를 정한다.
기존 Cue 에셋은 옮기지 않고 이 문서를 기준으로 **새로 만든다.**

## 진행 상황 (2026-11-24)
- **1장 Enemy1 + 가구 (8개)**: `en1_idle`, `en1_chase`, `en1_hear`, `en1_spot`, `en1_step_walk`, `en1_step_run`, `box_open`, `box_close`
  - Enemy1 호출: `Assets/Script/MonsterVoice.cs` (Monster 프리팹 루트에 부착)
  - 발소리 이벤트 `OnFootstep`: Monster_Walk 0.70s / 1.45s, Monster_Run 0.10s / 0.80s (발 뼈 높이 샘플링으로 정함)
  - 가구: `OpenableSound`가 `box_open` / `box_close`를 이름으로 호출
- **2장 플레이어 (14개)**: `plr_step_walk`, `plr_step_run`, `plr_breath_idle`, `plr_breath_tense`, `plr_heartbeat`, `plr_interact`, `plr_slot_select`, `plr_slot_deselect`, `item_pickup_battery`, `item_pickup`, `item_docu`, `plr_hide_enter`, `plr_hide_exit`, `plr_caught_bite`
  - 호출: `Assets/Scripts/Player/PlayerSounds.cs`, 숨소리 `Assets/Scripts/Player/PlayerBreathing.cs` (`PlayerAudioAutoAttach`가 실행 시 `FirstPersonController` 오브젝트에 자동 부착 — 씬 수정 불필요)
  - 열쇠 줍기는 `item_pickup_key`가 등록될 때까지 `item_pickup`으로 대체 재생
  - 타이틀/일시정지 중 인벤토리 지급(게임 시작 시 손전등)은 무음 — 기존엔 배터리·열쇠만 소리를 냈으므로 생기지 않던 문제
  - `item_docu`는 등록만 됨. 문서 아이템이 생기면 `PlayerSounds.HandleItemAdded`에 종류를 추가
- **3장 손전등 (4개)**: `fl_on`, `fl_off`, `fl_charge`, `fl_flash` — `PlayerSounds`가 `FlashlightController` 이벤트를 받아 재생
  - 충전 소리(1.66초)가 충전 시간(0.6초)보다 길어서, 섬광 발사 또는 충전 게이지가 0으로 돌아가면 0.08초 페이드로 끊는다
- **4장 문·퍼즐 (4개)**: `door_open`, `door_close`, `door_locked`, `esc_door_open` (`box_open`/`box_close`는 1장에서 등록)
  - 호출: `Assets/Scripts/Game/WorldSounds.cs` — 게임 시작 때 스스로 생겨서(`[WorldSounds]`, 씬 배치 불필요) `DoorPair` / `PasswordDoor` / `SafeDoor`의 정적 이벤트와 `EscapeDoorController.onAllLampsActive`를 받는다
  - 적이 문을 열면 `door_open_monster`가 등록될 때까지 `door_open`으로 대체. 금고 여는 소리 `pz_safe_open`은 등록 전까지 무음
  - 잠김 소리는 같은 문에서 0.4초 안에 다시 나지 않는다 (연타 방지, 기존 동작과 같음)
  - 탈출 문 소리는 램프가 모두 켜지고 `openDelay`(1초) 뒤, 문이 움직이기 시작할 때 2D로 재생
  - QTE 성공/실패 `qte_success`, `qte_fail`: 이제 **아이템이 든 상자**의 잠금 QTE(`LootQteGate` → `SkillCheckManager`)에서 난다. 원래 비콘용(`bcn_qte_*`)이던 것을 이름을 바꿔 옮겼다
- **5장 비콘 (4개)**: `bcn_hum_off`, `bcn_hum_on`, `bcn_battery_insert`, `bcn_activate` — 호출은 `WorldSounds`
  - 비콘 QTE는 파이프 연결 미니게임(`BeaconPipeQte`)으로 바뀌었다. 미니게임에는 실패가 없고(성공 / ESC 취소만), 미니게임 소리는 클립을 찾는 중이라 비워 둔다 → 대기 목록 `pz_pipe_rotate`, `pz_pipe_connect`
  - 비콘마다 작동음 루프 하나: `IsActivated`를 보고 off/on을 고른다. 바뀔 때 0.6초 페이드아웃 + 1.5초 페이드인 (기존 동작과 같음). 비콘 오브젝트가 꺼지면 소리도 끈다
  - 소리 위치: 비콘 피벗이 바닥이라 모델 중심(약 1.47m)에 실행 중에만 쓰는 점(`[AudioPoint]`)을 만든다. 예전 "Audio" 자식이 있으면 그걸 쓴다
  - 작동음 클립 2개는 Load Type = Streaming으로 바꿈 (62초·81초)
- **6장 음악·진행 (5개)**: `bgm_menu`, `bgm_chase`, `sfx_chase_escaped`, `bgm_gameover`, `bgm_escape` — 호출: `Assets/Scripts/Game/MusicSounds.cs` (게임 시작 때 스스로 생김, 씬 배치 불필요)
  - 메뉴: 타이틀에 있는 동안 루프(`ignorePause`), 1.5초 페이드인 / 게임 시작 시 1.2초 페이드아웃
  - 추격: `MonsterChase.IsChasing` + `MonsterHoundAI.IsChasingPlayer` 수를 센다. 0.6초 페이드인, 쫓는 적이 0이 되고 1초 뒤 0.8초 페이드아웃 + 따돌림 소리 (기존 2.5초는 따돌림 소리가 추격 음악에 묻혀서 줄임). 추격 시작/끝은 콘솔에 `[MusicSounds]`로 남는다 (에디터·개발 빌드만). 잡히거나 게임이 끝나면 따돌림 없이 0.15초에 끈다 (기존 동작과 같음). `MusicSounds.IsChaseMusicOn`으로 상태를 볼 수 있다 (7장 드론 줄이기용)
  - 결말: `GameProgress.IsEnded`가 되면 1회, 탈출이면 `bgm_escape`, 아니면 `bgm_gameover`
  - BGM 클립 4개 Load Type = Streaming
  - 코어 수정: 스트리밍 클립을 읽는 중이거나 에디터 일시정지 중에는 `isPlaying`이 false여도 재생이 끝난 것으로 보지 않는다 (`AudioVoice.IsStarting`). 고치기 전에는 루프 BGM이 시작하자마자 정리될 수 있었다
- **7장 환경음 (4개)**: `amb_drone`, `amb_random`, `amb_fluorescent`, `amb_emergency` — 호출: `Assets/Scripts/Game/AmbienceSounds.cs` (게임 시작 때 스스로 생김)
  - 드론: 게임 중(타이틀 아님) 2D 루프, 추격 음악 중에는 35%로 서서히 줄임
  - 랜덤: 리스너 주변 18m 안 랜덤 위치, 12~30초 간격 (클립 9개)
  - 조명: `Fluo`(190개) 9m 간격, `Emergency`(10개) 6m 간격으로 고른 뒤, **들리는 거리 안의 가까운 6개만** 재생 (기존은 처음 24개/16개를 계속 재생 → 새 구조에선 재생 슬롯 32개가 루프로 가득 차서 바꿈)
  - 숨으면 리스너에 로우패스(1400Hz)를 걸어 모든 소리를 먹먹하게 (기존 동작). `muffleWhenHiding`으로 끌 수 있다
  - 드론·형광등 클립 Load Type = Streaming
- **8장 UI (2개)**: `ui_hover`, `ui_click` — 호출: `Assets/Scripts/UI/UISounds.cs` (게임 시작 때 스스로 생김)
  - `GameMenuManager`가 실행 중에 만드는 버튼을 1초마다 찾아 `UIButtonSound`를 붙인다 (마우스 올림 / 왼쪽 클릭, 비활성 버튼 제외). 다른 버튼에도 `UIButtonSound`를 직접 붙여 쓸 수 있다
  - ESC로 일시정지 메뉴가 열릴 때 `ui_click`. 두 항목 모두 `ignorePause`
- **1~8장 등록 완료 (47개).** 남은 것은 [대기 목록](#대기-목록--클립-생기면-등록) (클립이 생기면 등록)

## 출처
- **클립**: `Assets/Audio/find_cilp/` (오디오 담당이 고른 개선 클립 112개). 아래 "클립" 열은 이 폴더의 파일 이름이다.
- **트리거("언제")**: 소리가 나는 상황을 정한 별도 문서는 없었다. 기존 연결 스크립트(`Assets/Audio/Scripts`의 `MonsterAudio`, `PlayerAudio`, `WorldAudio`, `BeaconAudio`, `ChaseMusicController`, `MenuAudio`, `BreathingAudio`, `FootstepEmitter`, `RandomVocalizer`)의 실제 동작을 옮겨 적었다.

## 규칙
- **클립이 실제로 있을 때만 등록한다.** 클립이 없거나 임시 파일이면 [대기 목록](#대기-목록--클립-생기면-등록)에 트리거만 적어 둔다.
- 같은 계열 클립(`_01`, `_02` …)은 **전부** 한 항목에 넣는다 (변형 재생).
- 항목 이름은 클립 파일 이름과 별개로 직접 정한다 (`enemy01_walk_*` 파일 → `en1_step_walk` 항목).
- **방식**: `Ex` = `PlayExclusive(name, 루트 transform)` · `3D` = `Play(name, target 또는 위치)` · `2D` = `Play(name)`
- **루프** `L` = 핸들로 시작·정지. **멈춤** 열이 비어 있으면 원샷(끝까지 재생).
- **호출 위치** = 새 구조에서 `AudioManager.Play…` 한 줄이 들어갈 게임 코드 지점.
- **클립 상태**: `새` = 기존과 다른 새 파일 · `동일` = 기존 `Clips`와 같은 파일 · 판단이 필요했던 항목은 [find_cilp 대조 결정](#find_cilp-대조-결정-2026-11-24) 참고
- **확인** 열: 오디오 담당이 트리거를 검토하고 `O`로 표시.

---

## 1. Enemy1 — 첫 버전 성공 기준

| 이름 | 클립 (find_cilp) | 상태 | 언제 | 멈춤 | 방식 | 그룹 | 호출 위치 | 확인 |
|---|---|---|---|---|---|---|---|---|
| `en1_idle` | enemy01_Idle_01~07 (1.7~7.6초) | 새 ×7 | 추격 중이 아닐 때 **4~10초 랜덤 간격**마다 | 다른 Ex 소리가 대체 | Ex | Enemy | 적 AI 평상시 상태 (타이머는 적 코드) | O |
| `en1_chase` | enemy01_chase_01~02 | 기존 클립 이름 변경 ×2 | 추격 중 **1.5~4초 랜덤 간격**마다 | 다른 Ex 소리가 대체 | Ex | Enemy | 적 AI 추격 상태 (타이머는 적 코드) | O |
| `en1_hear` | enemy01_hear_01~02 | 동일 | 적이 **소리를 감지**했을 때 | 다른 Ex 소리가 대체 | Ex | Enemy | 소리 감지 로직 (기존엔 호출된 적 없음) | O |
| `en1_spot` | enemy01_spot_01 | 동일 | 적이 **플레이어를 발견해 추격 시작**할 때. 직전 spot 후 **6초 안이면 생략** | 다른 Ex 소리가 대체 | Ex | Enemy | `MonsterChase.OnChaseStarted` (쿨다운은 적 코드) | O |
| `en1_step_walk` | enemy01_walk_01~07 | 새 ×7 | 평상시 이동 중 **한 걸음마다** | | 3D | Enemy | 걷기 애니메이션 이벤트 | O |
| `en1_step_run` | enemy01_run_01~07 | 새 ×7 | **추격 중** 이동할 때 한 걸음마다 | | 3D | Enemy | 뛰기 애니메이션 이벤트 | O |

**공통 음소거 조건 (기존 동작)**: 기절 중에는 발소리·울음 모두 멈춤 / 플레이어가 잡히는 연출 중에는 울음 멈춤.

**결정 (2026-11-24)**
- **spot 6초 쿨다운**과 **idle/chase 반복 간격**(4~10초, 1.5~4초)은 **적 코드**가 가진다. 코어에는 쿨다운·타이머가 없다.
- **적 발소리는 애니메이션 이벤트**로 낸다. 걷기/뛰기 애니메이션의 발이 닿는 프레임에 이벤트를 넣고, 이벤트 함수가 `AudioManager.Play("en1_step_walk" 또는 "en1_step_run", transform)`을 호출한다.
  - 걷기/뛰기 구분은 재생 중인 애니메이션이 정한다 (걷기 클립 이벤트 → walk, 뛰기 클립 이벤트 → run).
  - 기절 중 음소거는 기절 애니메이션에 발소리 이벤트가 없으므로 자연히 해결된다.
  - 현재 구조 (`Assets/Animations/Monster/Monster_AnimatorController`): Walk·Run은 블렌드 트리가 아니라 **별도 상태**이고, `Monster_Walk.anim` / `Monster_Run.anim`은 편집 가능한 `.anim` 파일이다. 이벤트는 아직 0개 → 발이 닿는 프레임마다 추가해야 한다.
  - 이벤트 함수는 **Animator와 같은 GameObject**의 컴포넌트에 있어야 한다 (예: 적 쪽 `MonsterFootstep.OnFootstep()`).
  - Walk↔Run 전환 중 크로스페이드 구간에는 두 클립 이벤트가 모두 올 수 있다 → 가중치가 큰 쪽만 받도록 거른다 (`AnimationEvent.animatorClipInfo.weight` ≥ 0.5).

## 2. 플레이어

| 이름 | 클립 (find_cilp) | 상태 | 언제 | 멈춤 | 방식 | 그룹 | 호출 위치 | 확인 |
|---|---|---|---|---|---|---|---|---|
| `plr_step_walk` | player_walk_01~10 | 새 ×10 | 걷는 중 **분당 90걸음** 박자로 | | 3D | Player | 이동 코드 | O |
| `plr_step_run` | player_run_01~10 | 새 ×10 | 달리는 중(`IsSprinting`) **분당 150걸음** 박자로 | | 3D | Player | 〃 | O |
| `plr_breath_idle` | player_breath_idle_01~06 (각 ~0.9초) | 새 ×6 | **일반 숨 구간**에서 숨 한 번마다. 간격은 [숨소리 규칙](#숨소리-규칙) | | 2D | Player | 숨소리 스크립트 (게임 쪽) | O |
| `plr_breath_tense` | player_breath_tense_01~06 (각 ~0.75초) | 새 ×6 | **거친 숨 구간**에서 숨 한 번마다. 간격은 [숨소리 규칙](#숨소리-규칙) | | 2D | Player | 〃 | O |
| `plr_heartbeat` | player_heartbeat_01~08 | 새 ×8 | 심장 박동 1회마다(`HeartRateController.OnBeat`). **95 BPM 미만은 안 들림**, BPM 높을수록 크게(0.15→1.0) | | 2D | Player | `HeartRateController` | O |
| `plr_interact` | player_interact_01 | 새 | 상호작용 키로 무언가를 사용했을 때. **같은 프레임에 아이템을 주웠으면 생략** | | 2D | Player | `PlayerInteraction.OnInteracted` | O |
| `plr_slot_select` | player_slot_select_01 | 새 | 핫바 슬롯 선택 | | 2D | UI | `PlayerInventory.OnSlotToggled(true)` | O |
| `plr_slot_deselect` | player_slot_deselect_01 | 새 | 핫바 슬롯 선택 해제 | | 2D | UI | `PlayerInventory.OnSlotToggled(false)` | O |
| `item_pickup_battery` | item_pickup_battery_01 | 동일 | 배터리를 인벤토리에 넣었을 때 | | 2D | Player | `PlayerInventory.OnItemAdded(Battery)` | O |
| `item_pickup` | item_pickup_01 | 동일 | 그 밖의 아이템(구급상자 등)을 주웠을 때 | | 2D | Player | `PlayerInventory.OnItemAdded(기타)` | O |
| `item_docu` | item_docu_01~02 | 새 ×2 | **문서·쪽지 아이템**을 주울 때. *아이템은 아직 없음 — 미리 등록* | | 2D | Player | 문서 아이템 줍기 (추가 예정, `OnItemAdded(문서)` 등) | O |
| `plr_hide_enter` | player_hide_enter_01 | 새 | 숨는 장소에 들어갈 때 | | 2D | Player | `PlayerHide.OnHidingChanged(true)` | O |
| `plr_hide_exit` | player_hide_exit_01 | 새 | 숨는 장소에서 나올 때 | | 2D | Player | `PlayerHide.OnHidingChanged(false)` | O |
| `plr_caught_bite` | player_caught_bite_01 | 동일 | 잡힘 연출에서 **물리는 순간** | | 2D | Player | `PlayerCaughtSequence.onBiteImpact` | O |

**공통 음소거 조건 (기존 동작)**: 발소리는 잡힘 연출 중 / 숨은 상태 / 조작 비활성 중에 안 남. 숨소리는 잡힘 연출 중에 안 남.

### 숨소리 규칙
기존의 "긴 루프 2개를 크로스페이드"하는 방식을 버리고, **숨 한 번 = 원샷 한 번**을 반복한다. 심박 범위는 60~160 BPM(`HeartRateController`)이다.

**1) 구간 — 어느 세트를 쓰는가**

| 구간 | 들어가는 조건 | 나오는 조건 | 사용 항목 |
|---|---|---|---|
| 일반 숨 | 시작 상태 / 거친 숨에서 **110 BPM 이하** | **120 BPM 이상** | `plr_breath_idle` |
| 거친 숨 | **120 BPM 이상** | **110 BPM 이하** | `plr_breath_tense` |

- 들어가는 값과 나오는 값을 다르게 둔다(120/110). 경계에서 숨소리가 왔다 갔다 하지 않게 하려는 것이고, 기존 동작과 같은 값이다.
- 구간이 바뀌어도 지금 나는 숨은 끊지 않고, **다음 숨부터** 새 세트를 쓴다.

**2) 빠르기: 구간마다 간격 범위를 따로 둔다**

일반 숨과 거친 숨이 각자 간격의 최대·최소를 가진다. 구간의 BPM 범위 안에서 심박이 높을수록 최소 쪽으로 간다. 두 범위가 겹치지 않으므로 구간이 바뀌면 빠르기도 확 달라져서, 상태가 바뀐 것을 귀로 알 수 있다.

| 구간 | BPM 범위 | 간격 (최대 → 최소) |
|---|---|---|
| 일반 숨 | 60(최저) → 120 | 4.0초 → 2.6초 |
| 거친 숨 | 110 → 160(최대) | 1.6초 → 0.9초 |

| BPM | 간격 | 분당 호흡 | 구간 |
|---|---|---|---|
| 60 (최저) | 4.0초 | 15회 | 일반 |
| 70 (안정) | 3.8초 | 16회 | 일반 |
| 110 | 2.8초 | 21회 | 일반 (거친 숨에서 돌아올 때) |
| 120 | 1.5초 | 41회 | 거친 (들어갈 때) |
| 140 | 1.2초 | 51회 | 거친 |
| 160 (최대, 달리기 불가) | 0.9초 | 67회 | 거친: 가장 짧게 몰아쉼 |

- 간격은 숨 하나가 **시작될 때** 그 순간의 BPM으로 계산한다.
- **겹침 방지:** 직전 숨이 아직 나는 중이면, 그 숨이 끝나고 0.1초(`minGap`) 쉰 뒤 다음 숨을 낸다. 거친 숨 클립은 0.73~0.80초인데 무작위 폭(-10%)과 피치(-4%)가 겹치면 0.9초 간격보다 길어질 수 있기 때문이다.
- 간격에 ±10% 무작위 폭을 줘서 기계적으로 들리지 않게 한다.
- 이 규칙은 **게임 쪽 숨소리 스크립트**(`PlayerBreathing`)가 맡는다. 코어는 `AudioManager.Play("plr_breath_idle")` 호출만 받는다.
- 조정 손잡이: 구간별 간격 최대·최소 4개, 구간 경계 `120/110`.


## 3. 손전등

| 이름 | 클립 (find_cilp) | 상태 | 언제 | 멈춤 | 방식 | 그룹 | 호출 위치 | 확인 |
|---|---|---|---|---|---|---|---|---|
| `fl_on` | fl_on_01 | 동일 | 손전등 켤 때 | | 2D | Player | `FlashlightController` 켜기 | O |
| `fl_off` | fl_off_01 (0.09초) | 새 | 손전등 끌 때 | | 2D | Player | `FlashlightController` 끄기 | O |
| `fl_charge` | fl_charge_01 | 동일 | 섬광(눈뽕) **충전 시작**. 발사하거나 충전이 풀리면 짧게 페이드로 끊음 | | 2D | Player | 섬광 충전 | O |
| `fl_flash` | fl_flash_01 | 동일 | 섬광 **발사**로 적이 기절할 때 | | 2D | Player | 섬광 발사 | O |

## 4. 문 · 가구 · 퍼즐

| 이름 | 클립 (find_cilp) | 상태 | 언제 | 멈춤 | 방식 | 그룹 | 호출 위치 | 확인 |
|---|---|---|---|---|---|---|---|---|
| `door_open` | door_open_01 | 동일 | 일반 문(`DoorPair`)·비밀번호 문이 **열리기 시작**할 때, 문 중심 위치 | | 3D | World | `DoorPair` / `PasswordDoor` | O |
| `door_close` | door_close_01 | 동일 | 일반 문·비밀번호 문·금고가 **닫히기 시작**할 때 | | 3D | World | `DoorPair` / `PasswordDoor` / `SafeDoor` | O |
| `door_locked` | door_locked_01 | 새 | 잠긴 문·금고를 열려고 했을 때 | | 3D | World | `DoorPair.AnyLocked` / `SafeDoor.AnyLocked` | O |
| `box_open` | box_open_01 | 새 | 가구(7종) 파츠가 열리기 시작할 때. 여러 파츠가 동시에 움직이면 **한 번만** (0.08초 안 중복 무시) | | 3D | World | `OpenableSound` | O |
| `box_close` | box_close_01 | 새 | 가구 파츠가 닫히기 시작할 때 (같은 중복 규칙) | | 3D | World | `OpenableSound` | O |
| `esc_door_open` | esc_door_open (7.9초) | 동일 (기존 `bcn_all_done_01`과 같은 파일) | 램프가 모두 켜져 **탈출 문이 열리기 시작**할 때. 맵 어디에 있든 들리도록 **2D** | | 2D | World | `EscapeDoorController.onAllLampsActive` | O |
| `qte_success` | qte_success_01 | 새 | **아이템이 든 상자**의 잠금 QTE(원형 스킬체크) 성공 | | 2D | UI | `SkillCheckManager.OnSuccess` (`LootQteGate`) | |
| `qte_fail` | qte_fail_01 | 새 | 아이템 상자 QTE 실패. 잡혀서 QTE가 취소될 때는 안 남 | | 2D | UI | `SkillCheckManager.OnFail` | |

## 5. 비콘

| 이름 | 클립 (find_cilp) | 상태 | 언제 | 멈춤 | 방식 | 그룹 | 호출 위치 | 확인 |
|---|---|---|---|---|---|---|---|---|
| `bcn_hum_off` | bcn_hum_off_01 (62.6초) | 동일 | 비콘이 **비활성** 상태인 동안 계속 | 활성화되면 | 3D L | World | `Beacon` 상태 | |
| `bcn_hum_on` | bcn_hum_on_01 (81.3초) | 동일 | 비콘이 **활성** 상태인 동안 계속 | (게임 끝까지) | 3D L | World | 〃 | |
| `bcn_battery_insert` | bcn_battery_insert_01 | 동일 | 비콘에 배터리를 꽂을 때 | | 3D | World | `Beacon.AnyActivated` | |
| `bcn_activate` | bcn_activate_01 | 동일 | 배터리 장착 소리 **0.35초 뒤** | | 3D | World | 〃 (지연 호출) | |

## 6. 음악 · 진행

| 이름 | 클립 (find_cilp) | 상태 | 언제 | 멈춤 | 방식 | 그룹 | 호출 위치 | 확인 |
|---|---|---|---|---|---|---|---|---|
| `bgm_menu` | bgm_menu_01 (61초) | 동일 | 타이틀 화면에 있는 동안 (`ignorePause`) | 게임 시작 시 페이드 아웃 | 2D L | BGM | `GameMenuManager.InTitle` | O |
| `bgm_chase` | bgm_chase_01 (41.7초) | 새 | 쫓는 적 수가 **0 → 1 이상**이 될 때. 적이 여러 마리여도 **하나만** | 쫓는 적이 0이 되면 | 2D L | BGM | 추격 상태 집계 | O |
| `sfx_chase_escaped` | sfx_chase_escaped_01 (2.9초) | 새 | 추격이 끝났는데 **잡히지 않았을 때** (따돌림) | | 2D | BGM | 〃 | O |
| `bgm_gameover` | bgm_gameover_01 (41.7초) | 새 | 게임 오버 (1회) | | 2D | BGM | `GameProgress.Changed` | O |
| `bgm_escape` | bgm_escape_01 (19.1초) | 새 | 탈출 성공 (1회) | | 2D | BGM | `GameProgress.Changed` (`HasEscaped`) | |

## 7. 환경음

| 이름 | 클립 (find_cilp) | 상태 | 언제 | 멈춤 | 방식 | 그룹 | 호출 위치 | 확인 |
|---|---|---|---|---|---|---|---|---|
| `amb_drone` | amb_drone_01 (54.9초) | 동일 | 게임 중 항상. **추격 중에는 볼륨 35%로** 줄임 | | 2D L | Ambience | 환경음 스크립트 (신규, 게임 쪽) | O |
| `amb_random` | amb_random_01~09 | 동일 ×9 | 플레이어 주변 **랜덤 위치**에서 랜덤 간격으로 | | 3D | Ambience | 〃 | O |
| `amb_fluorescent` | amb_fluorescent_01 (52.4초) | 동일 | 이름이 `Fluo`로 시작하는 조명 위치에서 계속 (9m 간격, 최대 24개) | | 3D L | Ambience | 〃 | O |
| `amb_emergency` | amb_emergency_01 (10.2초) | 동일 | 이름이 `Emergency`로 시작하는 조명 위치에서 계속 (6m 간격, 최대 16개) | | 3D L | Ambience | 〃 | O |


## 8. UI

| 이름 | 클립 (find_cilp) | 상태 | 언제 | 멈춤 | 방식 | 그룹 | 호출 위치 | 확인 |
|---|---|---|---|---|---|---|---|---|
| `ui_hover` | ui_hover_01 (0.03초) | 새 | 버튼에 마우스 올림. 비활성 버튼은 제외 (`ignorePause`) | | 2D | UI | 메뉴 버튼 | O |
| `ui_click` | ui_click_01 | 새 | 버튼 클릭 / **ESC로 일시정지 메뉴가 열릴 때** (`ignorePause`) | | 2D | UI | 메뉴 버튼, 일시정지 | O |

---

## find_cilp 대조 결정 (2026-11-24)

| 항목 | 결정 |
|---|---|
| `en1_chase` | find_cilp에 없던 것 중 **이것만** 기존 클립을 쓴다. `Clips/Enemy1/en1_chase_01~02` → `find_cilp/enemy01_chase_01~02`로 이름을 맞춰 복사함 |
| `item_pickup_key`, `pz_keypad_press` | 기존 클립 쓰지 않음 → 대기 목록 |
| `esc_door_open` | find_cilp의 `esc_door_open.wav`를 탈출 문 소리로 사용, **2D** 재생 |
| `bcn_all_done` | 위 파일을 탈출 문이 가져갔으므로 → 대기 목록 |
| `plr_breath_*` | 원샷 반복 + 심박 구간으로 세트 전환 + 심박에 따라 가빠짐 → [숨소리 규칙](#숨소리-규칙) |
| `item_docu` | 문서·쪽지 아이템이 나중에 생길 예정 → **미리 등록** |

## 가져오기 설정 (find_cilp → Unity)

- `find_cilp`에는 아직 `.meta` 파일이 없다 → Unity가 아직 임포트하지 않았다.
- **3D로 재생할 클립은 Force To Mono 켜기.** 발소리·문·적 소리 대부분이 스테레오라, 그대로 두면 거리 감쇠는 되지만 방향감이 약해진다.
- **긴 루프(BGM, 비콘 험, 드론, 형광등)는 Load Type = Streaming.** 비콘 험은 96kHz 24bit라 원본만 80MB가 넘는다.
- 샘플레이트가 44.1k~192k로 섞여 있다 → Sample Rate Setting을 **Override 48000Hz**로 맞추면 용량이 줄고 음질 차이는 거의 없다.

## 대기 목록 — 클립 생기면 등록

트리거만 정해 두고, 실제 클립이 들어오면 위 표로 옮겨 등록한다.

| 이름 (예정) | 언제 | 방식 | 그룹 | 상태 |
|---|---|---|---|---|
| `en1_attack` | Enemy1이 공격할 때 (`MonsterChase.OnAttack`) | Ex | Enemy | 클립 없음 |
| `en1_stun` | Enemy1이 기절할 때 (`MonsterStun.Stunned`) | Ex | Enemy | 클립 없음 |
| `en2_idle` | Enemy2 평상시 랜덤 간격 | Ex | Enemy | 클립 없음 (기존은 임시) |
| `en2_chase` | Enemy2 추격 중 랜덤 간격 | Ex | Enemy | 〃 |
| `en2_spot` | Enemy2 추격 시작 | Ex | Enemy | 〃 |
| `en2_charge_ready` | Enemy2 돌진 준비 | Ex | Enemy | 〃 |
| `en2_charge` | Enemy2 돌진 시작 | Ex | Enemy | 〃 |
| `en2_charge_hit` | Enemy2 돌진이 무언가에 부딪힘 | 3D | Enemy | 〃 |
| `en2_step_walk` | Enemy2 이동 중 한 걸음마다 | 3D | Enemy | 〃 |
| `item_pickup_key` | 열쇠를 인벤토리에 넣었을 때 (`PlayerInventory.OnItemAdded(Key)`) | 2D | Player | find_cilp에 없음 |
| `pz_keypad_press` | 비밀번호 UI에서 숫자 키 하나 입력 (`PasswordUIController.OnTyped`) | 2D | UI | find_cilp에 없음 |
| `bcn_all_done` | **마지막 비콘**이 켜져 탈출 안내가 나올 때, 1회 (`GameProgress.Changed`) | 2D | World | 클립을 `esc_door_open`이 사용 |
| `plr_hurt` | 신체 부위 페널티가 새로 생겼을 때 (`PlayerBodyStatus.OnPenaltyChanged(active)`) | 2D | Player | 클립 없음 |
| `plr_drop` | 아이템을 버릴 때 | 3D | Player | 클립 없음 |
| `door_open_monster` | 적이 문을 열 때 (`DoorPair`에서 `byPlayer == false`) — 없으면 `door_open` 사용 | 3D | World | 클립 없음 |
| `fl_reload` | 손전등 배터리 교체 | 2D | Player | 클립 없음 |
| `fl_empty` | 배터리 없이 켜려고 할 때 "빈 딸깍" | 2D | Player | 클립 없음 |
| `pz_keypad_ok` | 비밀번호 정답 (`OnSubmitted(true)`) | 2D | UI | 클립 없음 |
| `pz_keypad_ng` | 비밀번호 오답 (`OnSubmitted(false)`) | 2D | UI | 클립 없음 |
| `pz_safe_open` | 금고가 열릴 때 | 3D | World | 클립 없음 |
| `pz_pipe_rotate` | 비콘 파이프 미니게임에서 파이프를 클릭해 돌릴 때 (`PipeTile.RotatePipe`) | 2D | UI | 클립 찾는 중 (비워 둠) |
| `pz_pipe_connect` | 파이프가 끝까지 연결된 순간 (`PuzzleManager.Solved`). 창이 닫힌 뒤 배터리 장착 → 활성화 소리가 이어진다 | 2D | UI | 클립 찾는 중 (비워 둠) |

## 등록하지 않음

| 기존 Cue | 이유 |
|---|---|
| `room_cold_hum`, `room_or_hum`, `room_waste_drone` | room 기능 = 범위 밖. 확장 모듈에서 다시 정한다 |

## 클립 폴더 정리 (마이그레이션 때)

- 최종 위치는 intent대로 `Assets/Audio/Clips`. `find_cilp`의 파일을 분류 폴더(`Enemy1/`, `Player/` …)로 옮기고, 위 표에서 쓰지 않는 기존 클립은 삭제한다.
- 기존 `Clips/sfx_chase_escaped_01.wav`, `Clips/BGM/sfx_chase_escaped_01.wav` 중복 문제는 find_cilp의 새 파일로 대체되어 **둘 다 삭제 대상**.
- `Clips/README.md`는 내용이 오래됐고 삭제될 툴을 안내하므로 새 등록 방법으로 다시 쓴다.
- `CREDITS.md`가 비어 있다 → find_cilp 파일의 출처·라이선스를 적어야 한다 (특히 새 파일).
