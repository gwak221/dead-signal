# 스펙: 코어 오디오 매니저

작성일: 2026-11-23
근거 문서: [docs/intent/audio-manager.md](../intent/audio-manager.md)
대상 버전: Unity 6000.3.24f1, 씬 `Assets/Scenes/DeadSignal_Main.unity`

---

## 1. 설계 원칙

| 원칙 | 구현 수단 | 해결하는 문제 (intent "왜 지금") |
|---|---|---|
| 오디오 → 게임 코드 참조 금지 | 전용 어셈블리(`DeadSignal.Audio.asmdef`). 게임 코드(Assembly-CSharp)는 자동 참조로 오디오를 쓸 수 있지만, 오디오 어셈블리는 Assembly-CSharp를 **참조할 수 없음** → 컴파일러가 강제 | 적 코드 수정 시 오디오 컴파일 오류 |
| 소리 목록은 한 곳 | 매니저 프리팹 하나의 `sounds` 리스트가 유일한 등록처. 별도 Cue 에셋 없음 | 쓰지 않는 소리가 쌓여 목록 불명확 |
| 주체별 단일 재생 | `PlayExclusive` — 같은 주체(Transform)의 이전 소리를 끊고 새 소리 재생 | Enemy1 idle/hear/spot 겹침 |
| 코어는 닫고 확장은 연다 | 코어는 이벤트·읽기 전용 정보만 노출. room 필터 등은 별도 모듈이 구독 | room 필터 추가 중 매니저 붕괴 |
| 실패해도 게임은 멈추지 않는다 | 없는 이름·빈 클립·매니저 부재 → 경고 1회 후 무시, 예외 없음 | — |

---

## 2. 폴더 / 파일 구조

```
Assets/Audio/
├─ Clips/                         (유지)
├─ Mixer/MainMixer.mixer          (유지)
├─ CREDITS.md                     (유지)
├─ Resources/
│   └─ AudioManager.prefab        ← 유일한 소리 등록처
└─ Scripts/
    ├─ DeadSignal.Audio.asmdef
    ├─ AudioManager.cs            public  — 정적 API + MonoBehaviour 본체
    ├─ SoundEntry.cs              public  — 인스펙터 항목 1개
    ├─ SoundHandle.cs             public  — 재생 중 소리 핸들
    ├─ AudioVoice.cs              internal — AudioSource 1개 래퍼
    └─ Editor/
        ├─ DeadSignal.Audio.Editor.asmdef   (Editor 전용, DeadSignal.Audio 참조)
        └─ AudioManagerEditor.cs            검증 표시 + 미리듣기 버튼
```

### asmdef 설정
- `DeadSignal.Audio`: `rootNamespace = DeadSignal.Audio`, references 없음(UnityEngine 기본만), `autoReferenced = true`.
  - `autoReferenced = true`이므로 게임 코드는 별도 설정 없이 `DeadSignal.Audio.AudioManager`를 사용할 수 있다.
- `DeadSignal.Audio.Editor`: `includePlatforms = [Editor]`, references `[DeadSignal.Audio]`.

> 확장 모듈(room 필터 등)은 **자체 asmdef**(`DeadSignal.Audio.Rooms` 등)로 만들고 `DeadSignal.Audio`를 참조한다. 코어는 확장을 모른다.

---

## 3. 클래스 구조

```
          게임 코드 (Assembly-CSharp)
                │  AudioManager.Play("en1_spot", transform)
                ▼
┌──────────────────────── DeadSignal.Audio ────────────────────────┐
│ AudioManager (static API + MonoBehaviour, 싱글턴)                  │
│   - List<SoundEntry> sounds          ← 인스펙터                    │
│   - Dictionary<string, SoundEntry>   ← Awake에서 빌드              │
│   - AudioVoice[] voices (풀)                                       │
│   - Dictionary<Transform, AudioVoice> exclusive  ← 주체별 단일 재생 │
│   - event VoiceStarted / VoiceStopped ← 확장 모듈 연결점            │
│                                                                    │
│ SoundEntry  : 이름, 클립들, 볼륨/피치 폭, 3D 설정, 믹서 그룹, 루프   │
│ AudioVoice  : AudioSource + 따라갈 대상 + 주체 + 세대번호 + 페이드   │
│ SoundHandle : (voice index, generation) 값 타입                     │
└────────────────────────────────────────────────────────────────────┘
```

### 3.1 `SoundEntry` (`[Serializable] public class`)

| 필드 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `name` | `string` | `""` | **호출 키.** 직접 정한 이름. 첫 번째 string 필드라 인스펙터 리스트 요소 제목으로 표시됨 |
| `clips` | `AudioClip[]` | 빈 배열 | 여러 개면 무작위 선택 |
| `avoidRepeat` | `bool` | `true` | 클립 2개 이상일 때 직전 클립 연속 회피 |
| `volume` | `float [0,1]` | `1` | 기준 볼륨 |
| `volumeJitter` | `float [0,0.5]` | `0.05` | 재생마다 `volume ± jitter` (0~1로 클램프) |
| `pitch` | `float [0.1,3]` | `1` | 기준 피치 |
| `pitchJitter` | `float [0,0.5]` | `0.05` | 재생마다 `pitch ± jitter` |
| `group` | `AudioMixerGroup` | `null` | MainMixer 그룹. 비면 매니저의 `defaultGroup` |
| `spatial` | `bool` | `true` | true = 3D, false = 2D |
| `minDistance` | `float` | `1` | 3D일 때만 표시 |
| `maxDistance` | `float` | `20` | 3D일 때만 표시 |
| `rolloff` | `AudioRolloffMode` | `Linear` | 3D일 때만 표시 |
| `loop` | `bool` | `false` | 루프. 핸들로 직접 멈춰야 함 |
| `ignorePause` | `bool` | `false` | `AudioListener.pause` 중에도 재생 (메뉴/UI용) |
| `note` | `string` (TextArea) | `""` | 메모. 게임에 영향 없음 |

런타임 전용(직렬화 안 함): `[NonSerialized] int lastClipIndex = -1`

공개 메서드 (internal로 충분하나 확장 모듈 편의상 public):
- `bool HasClips { get; }`
- `AudioClip PickClip()` — null 클립 제외, `avoidRepeat` 적용
- `float RollVolume()`, `float RollPitch()`

### 3.2 `SoundHandle` (`public readonly struct`)

```csharp
public readonly struct SoundHandle
{
    public static readonly SoundHandle None;   // index = -1
    internal readonly int index;
    internal readonly int generation;

    public bool IsValid   { get; }  // None이 아님
    public bool IsPlaying { get; }  // 해당 voice가 아직 이 재생을 들고 있음
    public void Stop(float fadeOut = 0f);
    public void SetVolume(float volume01);  // entry 볼륨에 곱해지는 배율
}
```
- voice가 다른 재생에 재사용되면 `generation`이 달라져 이전 핸들은 자동 무효 → 남의 소리를 끄는 사고 방지.

### 3.3 `AudioVoice` (`internal sealed class`)
- 매니저 자식 GameObject `Voice_00` … 에 붙은 `AudioSource` 1개를 관리.
- 상태: `entry`, `follow (Transform)`, `owner (Transform, exclusive일 때)`, `generation`, `startTime`, `fade(목표, 속도)`, `volumeScale`.
- 매 프레임(매니저 `LateUpdate`에서 일괄):
  - `follow`가 있으면 위치 동기화. `follow`가 파괴되면 → 루프는 정지, 원샷은 마지막 위치에서 끝까지 재생.
  - 페이드 처리, 재생 끝났으면 반납.

### 3.4 `AudioManager` (`public sealed class : MonoBehaviour`)

**직렬화 필드 (인스펙터)**

| 필드 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `mixer` | `AudioMixer` | MainMixer | 참조 표시용 + 그룹 검증 |
| `defaultGroup` | `AudioMixerGroup` | World | 항목 그룹이 비었을 때 |
| `voiceCount` | `int [8,64]` | `32` | 동시 재생 상한 |
| `sounds` | `List<SoundEntry>` | **빈 목록** | 소리 등록처 |

**생성 / 수명**
- `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`에서 `Resources.Load<AudioManager>("AudioManager")` → 인스턴스화 → `DontDestroyOnLoad`.
  - 씬에 배치하지 않으므로 씬 파일 충돌·Missing Script가 생기지 않는다.
  - 매니저는 항상 하나. 씬에 따로 배치된 것이 있어도 **먼저 생긴(Resources에서 만든) 매니저가 유지**되고 나머지는 `Destroy`. (씬 로드 전에 생성되므로 씬 배치본이 먼저일 수 없다.)
- `Awake`: `sounds` → `Dictionary<string, SoundEntry>` (대소문자 구분, 앞뒤 공백 trim). 중복 이름은 **첫 항목 사용 + 경고**.
- `Awake`: voice 풀 생성 (`playOnAwake = false`).

---

## 4. 공개 API

모두 `static`. 매니저가 없거나 이름이 없으면 `SoundHandle.None` 반환, 예외 없음.

```csharp
namespace DeadSignal.Audio
{
    public sealed class AudioManager : MonoBehaviour
    {
        // ── 재생 ─────────────────────────────────────────────
        // 2D 재생 (UI, BGM). entry.spatial이 true여도 리스너 위치에서 재생.
        public static SoundHandle Play(string name);

        // 고정 위치 3D 재생 (문 소리, 아이템 줍기)
        public static SoundHandle Play(string name, Vector3 position);

        // 대상을 따라가는 3D 재생 (발소리, 숨소리)
        public static SoundHandle Play(string name, Transform target);

        // 주체별 단일 재생: target의 이전 exclusive 소리를 끊고 재생 (적 행동 소리)
        public static SoundHandle PlayExclusive(string name, Transform target, float fadeOutPrevious = 0.05f);

        // ── 정지 ─────────────────────────────────────────────
        public static void Stop(SoundHandle handle, float fadeOut = 0f);  // = handle.Stop()
        public static void StopExclusive(Transform target, float fadeOut = 0f);
        public static void StopAll(float fadeOut = 0f);

        // ── 조회 ─────────────────────────────────────────────
        public static bool Has(string name);
        public static bool IsExclusivePlaying(Transform target);

        // ── 확장 연결점 (코어 수정 없이 room 필터 등을 붙이는 용도) ──
        public static event Action<VoiceInfo> VoiceStarted;
        public static event Action<VoiceInfo> VoiceStopped;
        public static IReadOnlyList<VoiceInfo> ActiveVoices { get; }
    }

    // 확장 모듈에 노출하는 읽기 전용 정보
    public readonly struct VoiceInfo
    {
        public SoundHandle Handle { get; }
        public string Name { get; }
        public AudioSource Source { get; }  // 확장이 필터 컴포넌트를 붙일 수 있음
        public Vector3 Position { get; }
        public Transform Owner { get; }     // exclusive일 때만, 아니면 null
    }
}
```

### 4.1 동작 규칙

**`Play` 공통 흐름**
1. 이름 조회 실패 → `LogWarning` (같은 이름은 세션당 1회) → `None`.
2. `PickClip()`이 null → 조용히 `None` (클립 미할당은 정상 상태로 간주, 인스펙터에 경고 표시만).
3. 빈 voice 확보 → 없으면 **voice 훔치기**: 루프·exclusive가 아닌 원샷 중 가장 오래된 것. 그래도 없으면 가장 오래된 원샷 아무거나. 루프만 가득하면 재생 포기 + 경고.
4. AudioSource 설정: clip, volume, pitch, group, spatialBlend(`spatial ? 1 : 0`), min/max/rolloff, loop, `ignoreListenerPause`.
5. `generation++`, `VoiceStarted` 발생, 핸들 반환.

**`PlayExclusive` 추가 규칙** (intent 핵심 기능 4)
- 주체 키 = `target` Transform. 적 하나의 행동 소리는 **항상 같은 Transform**(적 루트 `transform`)으로 호출한다.
- 이전 exclusive voice가 재생 중이면 `fadeOutPrevious`(기본 0.05초, 클릭 노이즈 방지) 동안 줄이고 끊은 뒤 새 소리 시작. 페이드 중인 voice는 새 소리와 잠깐 겹치지만 즉시 exclusive 슬롯에서 빠진다.
- **같은 이름이 이미 재생 중이면?** → 그래도 재시작(대체). idle 루프 같은 경우는 호출 쪽에서 `IsExclusivePlaying`으로 판단.
- 발소리 등 행동 소리와 동시에 나야 하는 소리는 `Play(name, target)`(비독점)으로 호출한다.
- `target` 파괴 시 해당 exclusive 슬롯 자동 정리.

**피치/볼륨**: 최종 볼륨 = `RollVolume() × handle 배율`. `Time.timeScale`은 피치에 반영하지 않음(일시정지는 `AudioListener.pause`로 처리).

**씬 전환**: 매니저는 유지. 씬 언로드 시 `follow`/`owner`가 파괴된 voice는 위 규칙대로 정리. BGM 루프는 호출 쪽이 핸들로 관리.

---

## 5. 인스펙터 형태

기본 리스트 UI(재정렬·추가·삭제)를 그대로 쓰고, `AudioManagerEditor`는 **검증 표시와 미리듣기만** 추가한다. 리스트를 직접 그리는 복잡한 에디터는 만들지 않는다(에디터 툴이 망가지는 일을 반복하지 않기 위해).

```
┌ AudioManager (Prefab: Assets/Audio/Resources/AudioManager.prefab) ───────┐
│ Mixer           [MainMixer          ◉]                                   │
│ Default Group   [World              ◉]                                   │
│ Voice Count     [====●=========] 32                                      │
│                                                                          │
│ ⚠ 중복 이름: "en1_spot" (항목 4, 9)                                       │
│ ⚠ 클립 없음: "door_locked"                                                │
│ 등록 12개 · 클립 23개                       [검색: en1        ]           │
│                                                                          │
│ Sounds                                                       12          │
│ ▼ en1_idle                                                               │
│     Name           [en1_idle                       ]                     │
│     Clips          ▸ 1  (en1_idle_01)        ← 클립 드래그&드롭          │
│     Avoid Repeat   [✓]                                                   │
│     Volume         [=======●==] 0.8   Jitter [=●========] 0.05           │
│     Pitch          [====●=====] 1.0   Jitter [=●========] 0.05           │
│     Group          [Enemy              ◉]                                │
│     Spatial        [✓]   Min 1   Max 18   Rolloff [Linear ▾]             │
│     Loop [ ]   Ignore Pause [ ]                                          │
│     Note           [평상시 그르렁                    ]                    │
│     [▶ 미리듣기]                                                          │
│ ▶ en1_hear                                                               │
│ ▶ en1_spot                                                               │
│ ▶ en1_step_walk                                                          │
│                                                       [ + ] [ - ]        │
└──────────────────────────────────────────────────────────────────────────┘
```

**에디터가 하는 일 (전부)**
1. 상단 경고 박스: 빈 이름, 중복 이름, 클립 없음, 그룹이 MainMixer 소속이 아님.
2. 요약 줄: 등록 수 · 총 클립 수.
3. 검색 필드: 이름/노트 부분 일치 항목만 펼쳐 보이기(필터링, 데이터 변경 없음).
4. 항목별 `▶ 미리듣기`: 에디터에서 `PickClip` 결과를 볼륨·피치 적용해 재생.
5. `spatial`이 꺼져 있으면 3D 필드 숨김 (`SoundEntry`용 PropertyDrawer).

**하지 않는 일**: 클립 파일명으로 자동 항목 생성(intent에서 금지), 이름 상수 코드 생성, 씬 스캔/일괄 설정.

### 이름 규칙 (권장, 강제 아님)
`분류_대상_행동` 소문자 스네이크. 예: `en1_idle`, `en1_spot`, `en1_step_walk`, `door_open`, `ui_click`, `bgm_chase`.
클립 파일명 접두어와 맞추면 찾기 쉽지만, 이름은 항상 사람이 직접 입력한다.

---

## 6. 사용 예

```csharp
using DeadSignal.Audio;

// 적 행동 소리 — 겹치지 않음
AudioManager.PlayExclusive("en1_spot", transform);
AudioManager.PlayExclusive("en1_hear", transform);   // spot을 끊고 hear 재생

// 발소리 — 행동 소리와 동시에 남
AudioManager.Play("en1_step_walk", transform);

// 고정 위치
AudioManager.Play("door_open", door.position);

// UI / BGM
AudioManager.Play("ui_click");
SoundHandle chase = AudioManager.Play("bgm_chase");  // loop 항목
chase.Stop(1.5f);
```

---

## 7. 확장 모듈 연결 방법 (room 필터 예시, 구현은 범위 밖)

```
DeadSignal.Audio.Rooms.asmdef  → references: DeadSignal.Audio
  RoomAudioFilter : MonoBehaviour
    OnEnable  : AudioManager.VoiceStarted += v => 필요시 v.Source에 AudioLowPassFilter 추가
    LateUpdate: foreach v in AudioManager.ActiveVoices → 리스너/소스 방 비교 → cutoff 조절
```
코어에 필요한 것은 이미 4장의 이벤트와 `ActiveVoices`뿐이며, 코어 코드 변경은 없다.
코어는 voice를 재사용할 때 **확장이 붙인 컴포넌트를 제거하지 않는다** — 확장이 `VoiceStarted`에서 상태를 다시 설정할 책임을 진다.

---

## 8. 오류 처리 요약

| 상황 | 동작 |
|---|---|
| 매니저 프리팹 없음 (Resources 로드 실패) | 첫 호출 시 `LogError` 1회, 이후 모든 호출 no-op |
| 이름 없음 | 이름별 `LogWarning` 1회, `None` |
| 클립 없음 | 조용히 `None` (인스펙터 경고로 확인) |
| voice 부족 | 원샷 훔치기 → 실패 시 경고 1회 |
| target/owner 파괴 | 루프 정지, 원샷은 끝까지, exclusive 슬롯 정리 |
| 무효 핸들로 Stop | 무시 |

---

## 9. 마이그레이션 (스펙 승인 후 작업 순서)

1. GitHub `main` 최신 내용을 `feature/audio`로 가져오기.
2. 삭제: `Assets/Audio/Scripts/*`(기존 16개 스크립트), `Assets/Audio/Cues`, `Assets/Audio/RoomProfiles`, `Assets/Audio/Prefabs`, `Assets/Audio/Editor`.
3. 외부 참조 정리:
   - `Assets/Openables/Scripts/OpenableSound.cs` — `SoundCue` 필드 → `string openSound / closeSound` + `AudioManager.Play(name, transform.position)`.
   - `Assets/HospitalMap/Editor/SceneMergeTool.cs` — 오디오 컴포넌트 이름 목록(41–50행)과 `AudioSetupTool` 호출(430–434행) 제거.
   - 씬/프리팹 Missing Script 제거.
4. 코어 작성 (2장 파일 구조) → `AudioManager.prefab` 생성, `sounds` 빈 목록.
5. Enemy1부터 소리 등록 및 호출 연결 (`en1_idle`, `en1_hear`, `en1_spot`은 `PlayExclusive`, 발소리는 `Play`).
6. 나머지 소리는 실제로 쓰는 것만 하나씩 등록. 등록 대상과 순서는 [audio-list.md](audio-list.md)를 따른다.

> 기존 시스템은 오디오가 게임 이벤트(`DoorPair.AnyMoved`, `GameProgress.Changed` 등)를 구독하는 구조였다. 새 구조에서는 **게임 코드가 호출**한다. 따라서 5~6단계에서 각 게임 스크립트에 `AudioManager.Play` 한 줄씩이 추가된다.

---

## 10. 성공 기준 검증

| intent 성공 기준 | 검증 방법 |
|---|---|
| Enemy1 idle/hear/spot이 겹치지 않음 | 플레이 모드에서 spot 직후 hear 유도 → `AudioManager.ActiveVoices` 중 owner가 해당 적인 voice가 항상 ≤1 (페이드 0.05초 구간 제외) |
| 적 코드 수정해도 오디오 컴파일 안 깨짐 | `DeadSignal.Audio`가 Assembly-CSharp를 참조할 수 없으므로 구조적으로 보장. 확인: 적 스크립트에 임의 컴파일 오류를 넣어도 콘솔 오류의 출처가 Assembly-CSharp뿐 |
| 인스펙터만 보면 사용 중인 소리 전체가 보임 | 등록처가 `AudioManager.prefab` 하나뿐. 코드에서 `AudioManager.Play` 외 재생 경로 없음(`AudioSource.Play` 직접 호출 금지를 팀 규칙으로) |

---

## 11. 결정 기록 (검토 시 확인할 점)

- **주체 키를 `Transform`으로** — 별도 ID/채널 개념 없이 가장 단순. 대신 적 스크립트가 자식 Transform으로 호출하면 다른 주체가 된다 → "적 루트 `transform`으로 호출" 규칙 필요.
- **채널(행동/발소리 등) 개념 없음** — 비독점 `Play`로 충분하다고 판단. 한 적에 독점 그룹이 둘 이상 필요해지면 `PlayExclusive(name, target, channel)` 오버로드 추가로 확장 가능(기존 호출 호환).
- **Resources 자동 생성** — 씬 배치보다 충돌이 적다. 단점: 프리팹 위치가 `Resources` 폴더로 고정됨.
- **볼륨 설정 API(옵션 메뉴 슬라이더) 미포함** — MainMixer에 노출된 파라미터가 현재 없음(`m_ExposedParameters: []`). 필요해지면 그룹 볼륨을 노출하고 `SetGroupVolume(group, 0~1)` 추가.
- **쿨다운(minInterval) 미포함** — 머신건 방지는 변형 재생 + exclusive로 해결. 같은 소리 연타 문제가 실제로 생기면 `SoundEntry.cooldown` 추가.
