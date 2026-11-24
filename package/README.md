# Monster_1 어둠 시인성 & 핏줄 발광 업데이트 패키지 (`Monster_1_Update.unitypackage`)

> **대상 패키지:** `Monster_1_Update.unitypackage`  
> **목적:** 어두운 방·복도 및 ACES 포스트프로세싱 환경에서도 **Monster_1**의 전신 실루엣, 피부 질감, 골격, 발톱, 안면 촉수가 선명하게 보이도록 개선하며, 붉은 핏줄과 심장의 발광 포인트를 함께 적용합니다.  
> **호환성:** 기존 프로젝트(변경 전 게임)의 `Monster_1` 머티리얼 GUID를 그대로 유지하므로, 씬(`DeadSignal_Main.unity`)·프리팹(`Monster.prefab`)·FBX(`Monster_1.fbx`)나 다른 스크립트를 덮어쓰지 않고 임포트 즉시 반영됩니다. *(URP 프로젝트 기준)*

---

## 1. 팀원 프로젝트 적용 방법

1. Unity 에디터에서 변경 전 프로젝트를 엽니다.
2. 상단 메뉴 **`Assets > Import Package > Custom Package...`** 를 선택합니다.
3. **`Monster_1_Update.unitypackage`** 를 선택한 뒤, 모든 항목(총 10개 파일)이 체크된 상태에서 **`Import`** 를 클릭합니다.
4. 임포트가 끝나면 별도 연결 작업 없이 씬/프리팹의 `Monster_1` 외형이 즉시 업데이트됩니다.

*(CLI 사용 시)*
```bash
unity assets import _ExtractedChanges/Monster_1_Update.unitypackage
```

---

## 2. 추가된 파일 (4개)

원본 게임에는 없던 전용 셰이더 3종과 피부 핏줄 발광 마스크 텍스처 1종이 추가되었습니다.

| 구분 | 경로 | GUID | 설명 |
| --- | --- | --- | --- |
| Shader | `Assets/Shaders/MonsterLit.shader` | `91df08ca917994405a5dbcd9bb549f61` | URP Lit 기반 커스텀 몬스터 셰이더 (`DeadSignal/MonsterLit`). `_AlbedoLift`, `_AmbientFill`, `_EnvSheen`, `_RimIntensity`, `_RimPower`, `_RimColor` 프로퍼티 지원 |
| HLSL Include | `Assets/Shaders/MonsterLitInput.hlsl` | `786d32604f98a43998fd76835e712c82` | `InitializeStandardLitSurfaceData`에서 `_AlbedoLift > 0`일 때 `_BaseColor` 곱셈 전 텍스처 선형 알베도의 암부를 비선형 감마 곡선으로 끌어올려 어두운 피부 질감 및 추격 시 붉은색 변색(`MonsterChase`)이 모두 유지되도록 처리 |
| HLSL Include | `Assets/Shaders/MonsterLitForwardPass.hlsl` | `c0178db22de924882bdb45c13d17a2db` | 자체 발광(`surfaceData.emission`)을 제외한 실제 주변 조도(`litOnly`) 기준으로 `sceneLit`/`darkMask`를 계산하고, 카메라 기준 3/4 상단 키 라이트·윤기(`_EnvSheen`)·하부까지 이어지는 림 라이트(`rimMask` 하한 0.55)로 어둠 속 입체 실루엣 형성 |
| Texture | `Assets/Monster_1_Textures/M1_Skin_VeinEmission.png` | `048b0a3eb4905454e9d6fb70c1bb89df` | 피부 표면의 붉은 핏줄을 발광시키기 위한 Emission 마스크 텍스처 (2048×2048) |

---

## 3. 수정된 파일 (6개)

`Assets/Monster_1_Materials/` 내 6개 머티리얼이 기존 `Universal Render Pipeline/Lit`에서 `DeadSignal/MonsterLit`으로 전환되었으며, 부위별 시인성과 발광 값이 조정되었습니다. (모든 `.mat` 파일의 **GUID는 원본과 동일**합니다.)

| 머티리얼 경로 (GUID) | 변경 전 (원본) | 변경 후 |
| --- | --- | --- |
| `Assets/Monster_1_Materials/Mat_Monster_Skin.mat`<br>(`df37bccc6eba74430b532d9ac30fac43`) | - 셰이더: URP Lit<br>- `_BumpScale`: `0.9`<br>- `_EmissionMap`: 없음<br>- `_EmissionColor`: `(0, 0, 0)` | - 셰이더: `DeadSignal/MonsterLit`<br>- `_BumpScale`: `1.9`<br>- `_EmissionMap`: `M1_Skin_VeinEmission.png` (`_EMISSION` 활성)<br>- `_EmissionColor`: `(1.6, 0, 0)`<br>- `_AlbedoLift`: `0.68`, `_AmbientFill`: `1.72`, `_EnvSheen`: `0.65`<br>- `_RimIntensity`: `0.56`, `_RimPower`: `3.1`, `_RimColor`: `(0.76, 0.84, 0.84)` |
| `Assets/Monster_1_Materials/Mat_Monster_Bone.mat`<br>(`e2c10944bbaaa401f9b644edba5b4c51`) | - 셰이더: URP Lit | - 셰이더: `DeadSignal/MonsterLit`<br>- `_AlbedoLift`: `0.25`, `_AmbientFill`: `1.65`, `_EnvSheen`: `0.3`<br>- `_RimIntensity`: `0.42`, `_RimPower`: `3`, `_RimColor`: `(0.76, 0.84, 0.84)` |
| `Assets/Monster_1_Materials/Mat_Monster_Claw.mat`<br>(`9b94afe26311e441db016807d703d808`) | - 셰이더: URP Lit | - 셰이더: `DeadSignal/MonsterLit`<br>- `_AlbedoLift`: `0.58`, `_AmbientFill`: `1.75`, `_EnvSheen`: `0.45`<br>- `_RimIntensity`: `0.55`, `_RimPower`: `2.9`, `_RimColor`: `(0.76, 0.84, 0.84)` |
| `Assets/Monster_1_Materials/Mat_Monster_Heart.mat`<br>(`fb5de69673a644ec9af3fdf251770a23`) | - 셰이더: URP Lit<br>- `_EmissionColor`: `(0.45, 0.03, 0.03)` | - 셰이더: `DeadSignal/MonsterLit`<br>- `_EmissionColor`: `(8.6, 0, 0)`<br>- `_AlbedoLift`: `0`, `_AmbientFill`: `1.15`, `_EnvSheen`: `0.1`<br>- `_RimIntensity`: `0.2`, `_RimPower`: `3.4`, `_RimColor`: `(0.74, 0.78, 0.8)` |
| `Assets/Monster_1_Materials/Mat_Monster_Tendril.mat`<br>(`65d27917c7c494b6588a42675636f48b`) | - 셰이더: URP Lit | - 셰이더: `DeadSignal/MonsterLit`<br>- `_AlbedoLift`: `0.3`, `_AmbientFill`: `1.6`, `_EnvSheen`: `0.3`<br>- `_RimIntensity`: `0.48`, `_RimPower`: `2.9`, `_RimColor`: `(0.76, 0.84, 0.84)` |
| `Assets/Monster_1_Materials/Mat_Monster_Veins.mat`<br>(`e3c6b0796ee574d6c826c153c4de7f88`) | - 셰이더: URP Lit<br>- `_EmissionMap`: 없음<br>- `_EmissionColor`: `(0, 0, 0)` | - 셰이더: `DeadSignal/MonsterLit`<br>- `_EmissionMap`: `M1_Veins_BaseColor.png` (`_EMISSION` 활성)<br>- `_EmissionColor`: `(9.2, 0, 0)`<br>- `_AlbedoLift`: `0`, `_AmbientFill`: `1.45`, `_EnvSheen`: `0.1`<br>- `_RimIntensity`: `0.22`, `_RimPower`: `3.4`, `_RimColor`: `(0.74, 0.78, 0.8)` |

---

## 4. 패키지에 의도적으로 제외된 항목

팀원의 기존 작업물과 충돌하지 않도록 아래 항목은 패키지에서 제외했습니다.

- **변경되지 않은 Monster_1 원본 에셋**: `Monster_1.fbx`, `Assets/Prefabs/Monster.prefab`, 기존 `M1_*` 텍스처(`BaseColor`, `Normal`, `MetallicSmoothness` 등), 애니메이션 컨트롤러/클립
- **씬 파일**: `Assets/Scenes/DeadSignal_Main.unity`
- **Monster_1 외 작업물**: `Monster_2` 관련 머티리얼/텍스처/스크립트 및 `MonsterMovementAnimator.cs`, `MonsterSpawnPoints.cs`, `PlayerCaughtSequence.cs`, `PlayerBloodDeathEffect.cs` 등 C# 스크립트

---

## 5. 커스터마이징 팁 (Inspector에서 미세 조정 시)

어두운 방에서의 밝기나 외곽선 느낌을 더 조절하고 싶다면 `Assets/Monster_1_Materials/Mat_Monster_Skin.mat` 인스펙터에서 아래 값을 조절하면 됩니다.

- **`_AlbedoLift`**: 피부 기본 텍스처의 암부 밝기 보정(손전등/조명 반응 및 피부 톤 깊이 조절)
- **`_AmbientFill`**: 어두운 방에서 비추는 3/4 방향 가상 키 라이트 강도
- **`_EnvSheen`**: 어둠 속 피부 주름/표면 윤기 반사 강도
- **`_RimIntensity` / `_RimPower` / `_RimColor`**: 외곽 실루엣 테두리 빛 강도, 두께 집중도, 색상
