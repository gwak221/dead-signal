# 맵 배치 문제 목록 (조사 결과 - 아무것도 수정하지 않음)

생성 근거: 렌더러 AABB(축 정렬 상자) 근사 검사(가구 1298개 대상). **정밀 메시 검사가 아니므로** 회전된 가구/벽, 의도적으로 흩어 놓은 침대·휠체어 더미는 오탐일 수 있습니다. 좌표는 월드 좌표이고, 오브젝트 이름은 Hierarchy 이름(상위 2~3단계, 전부 `HospitalMap` 하위)입니다. 겹침 정도: 가구끼리는 "작은 쪽 부피 대비 겹침 비율"과 겹친 상자 크기(x,y,z m), 벽은 침투 깊이(m).

수정은 사용자가 직접 확인한 뒤 결정합니다. 스크린샷은 시작방 주변 5장만 있습니다(`Temp/Verification/`, git 제외) — 아래 목록은 좌표로 찾아가세요.

> **범위 정정 (사용자 지시)**: 기존 팀원 맵에는 의도적으로 막아 둔 방/출입문과 겹쳐 배치한 가구가 있으므로 A/B/D/E 항목은 **참고용 기록으로만 유지**하며 수정·추가 조사·스크린샷 대상이 아닙니다. 확인 대상은 아래 0번(오늘 추가/수정한 요소)뿐이고, 스크린샷은 `Temp/Verification/MapIssues/` 에 저장합니다.

## 0. 오늘 추가/수정한 모델·아이템 관련 (별도 분류)

| # | 대상 | 문제 | 게임플레이 영향 | 상태 |
|---|---|---|---|---|
| T1 | 스폰 후보 44개(filing 서랍 12, tea_niche, 약장 선반 등) | 서랍/선반 바닥 높이가 실제 메시보다 1~2cm 낮아 아이템이 바닥에 파묻힘 | 아이템 일부가 메시에 묻혀 보임 | 실제 메시 윗면으로 재배치 완료(점 위치만 이동) |
| T2 | FlashlightBattery_AA_cab4 / cabinet004_4 / cabinet005 / cabinet006 (약장 선반) | 선반 위 약 3.4cm 떠 있음 | 약간 떠 보임 | 선반 윗면(+4mm)으로 내림 |
| T3 | Syringe (시작방 tea_niche_002 (1) 상단 칸, 팀원 배치) @(-4.47,1.69,-0.84) | 정밀 검사에서 지지면 아래로 0.53m 떨어진 것으로 판정 | 스크린샷에서는 상단 칸 바닥에 정상적으로 놓여 있음(`Temp/Verification/start_open_Upper.png`) -> 검사 오탐 가능성 높음 | 팀원 배치, 변경 안 함 |
| T4 | 주사기(길이 0.49m) vs 약장 선반/tea_niche 후보 | 정밀 검사에서 선반/벽 메시와 겹침 판정 | 랜덤 주사기가 놓일 수 있는 지점이 4곳으로 제한됨 | 허용 종류에서 제외 |
| T5 | Syringe_cabinet004_5 (med_cabinet_004 (5) 상단 선반) @(53.77,1.23,42.26) | 정밀 검사에서 선반과 겹침 경고 | 눈으로 확인 필요 | 변경 안 함 |
| T6 | medkit_006 @(91.1,0.72,26.0) (약품실 box_stack 위), Syringe_room07 @(58.33,0.94,21.96) (stair2 북쪽 가장자리) | 열린 면 배치(메시 검사 대상 아님) | 접근 가능은 확인함(F 줍기 판정), 시각적 안착은 눈으로 확인 필요 | 변경 안 함 |
| T7 | anesthesia_002 @(51.1,0,40.3) (장식 처리함) | 개폐부 정면(-x/-z) 0.4m 앞에 bed_016 (27) 가 붙어 서랍 접근 불가(맵 배치 문제) | 마취기 서랍을 열 수 없음 -> 장식 처리 | 장식 처리(모델 유지) |
| T8 | anesthesia_002 (8) @(69.7,0.1,-23.4) (장식 처리함) | 쓰러져 누운 상태 + Wall_011 (1) 로 0.19m 침투 | 서랍이 일부만 열림 -> 장식 처리 | 장식 처리(모델 유지) |
| T9 | med_cabinet_001 (삭제함) | 침대와 겹침 | - | 씬에서 삭제(프리팹 유지) |


## A. 가구끼리 겹침 (54건)

작은 쪽 부피의 40% 이상이 겹친 쌍(바닥~1.6m 이내). 비율 1.0 = 작은 쪽이 거의 전부 큰 쪽 상자 안.

| # | 문제 오브젝트 (Hierarchy) | 겹치는 상대 오브젝트 | 월드 좌표 | 겹침 정도 | 유형 | 게임플레이 영향 | 의도적 배치 가능성 |
|---|---|---|---|---|---|---|---|
| A1 | `HospitalMap/Objects/bed/bed_016 (8)` | `HospitalMap/Objects/bench_a/bench_a_002 (7)` | (70.8, 1.2, 1.8) / (71.0, 0.5, 1.4) | 비율 1.00, 겹침 (1.94, 1.02, 0.90) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A2 | `HospitalMap/Objects/anesthesia/anesthesia_002 (3)` | `HospitalMap/Objects/ecg/ecg_003 (18)` | (48.2, 0.5, 46.6) / (47.8, 0.6, 46.6) | 비율 0.99, 겹침 (1.55, 1.25, 1.56) | 가구-가구 겹침 | 장비가 침대 안으로 파고들어 보일 수 있음 | 중간 (침대 옆 장비 배치 근사) |
| A3 | `HospitalMap/Objects/bed/bed_016 (1)` | `HospitalMap/Objects/ecg/ecg_003 (1)` | (37.6, 0.5, 9.0) / (37.2, 0.1, 8.0) | 비율 0.89, 겹침 (1.51, 0.93, 0.66) | 가구-가구 겹침 | 장비가 침대 안으로 파고들어 보일 수 있음 | 중간 (침대 옆 장비 배치 근사) |
| A4 | `HospitalMap/Objects/bed/bed_010 (2)` | `HospitalMap/Objects/ecg/ecg_002` | (113.1, 1.4, 29.4) / (113.2, 0.7, 29.6) | 비율 0.89, 겹침 (1.53, 1.35, 0.78) | 가구-가구 겹침 | 장비가 침대 안으로 파고들어 보일 수 있음 | 중간 (침대 옆 장비 배치 근사) |
| A5 | `HospitalMap/Objects/anesthesia/anesthesia_002 (7)` | `HospitalMap/Objects/ecg/ecg_003 (18)` | (47.8, 0.7, 45.9) / (47.8, 0.6, 46.6) | 비율 0.85, 겹침 (1.55, 1.19, 1.41) | 가구-가구 겹침 | 장비가 침대 안으로 파고들어 보일 수 있음 | 중간 (침대 옆 장비 배치 근사) |
| A6 | `HospitalMap/Objects/wheelchair/wheelchair_007 (4)` | `HospitalMap/Objects/wheelchair/wheelchair_007 (8)` | (13.4, 0.3, -1.6) / (13.8, 0.6, -1.4) | 비율 0.77, 겹침 (1.06, 0.84, 1.04) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A7 | `HospitalMap/Objects/bed/bed_010 (4)` | `HospitalMap/Objects/bed/bed_010 (5)` | (113.1, 1.5, 17.2) / (113.1, 2.2, 17.2) | 비율 0.75, 겹침 (2.57, 1.21, 2.50) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A8 | `HospitalMap/Objects/bed/bed_010 (1)` | `HospitalMap/Objects/bed/bed_010 (3)` | (113.1, 1.5, 27.9) / (113.1, 2.2, 27.9) | 비율 0.75, 겹침 (2.57, 1.21, 2.50) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A9 | `HospitalMap/Objects/bed/bed_010 (6)` | `HospitalMap/Objects/window_torn/window_torn_015` | (113.1, 1.4, 18.7) / (114.1, 2.2, 19.4) | 비율 0.71, 겹침 (1.11, 1.24, 0.20) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A10 | `HospitalMap/Objects/bench_a/bench_a_003` | `HospitalMap/Objects/defib/defib_001` | (10.7, 0.4, 36.4) / (11.7, 0.1, 36.3) | 비율 0.69, 겹침 (0.36, 0.53, 0.50) | 가구-가구 겹침 | 장비가 침대 안으로 파고들어 보일 수 있음 | 중간 (침대 옆 장비 배치 근사) |
| A11 | `HospitalMap/Objects/bed/bed_010 (7)` | `HospitalMap/Objects/bench_b/bench_b_005` | (113.1, 1.8, 15.9) / (113.5, 0.6, 15.7) | 비율 0.67, 겹침 (1.93, 0.62, 1.41) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A12 | `HospitalMap/Objects/bed/bed_016 (6)` | `HospitalMap/Objects/wheelchair/wheelchair_007 (5)` | (15.5, 0.5, 14.1) / (15.7, 0.9, 14.5) | 비율 0.65, 겹침 (1.20, 0.62, 1.04) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A13 | `HospitalMap/Objects/bed/bed_016 (32)` | `HospitalMap/Objects/bench_a/bench_a_002 (18)` | (37.1, 0.7, 46.8) / (38.3, 0.8, 47.1) | 비율 0.64, 겹침 (1.31, 1.81, 1.96) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A14 | `HospitalMap/Objects/cardboard_box/cardboard_box_010` | `HospitalMap/Objects/wheelchair/wheelchair_005 (5)` | (40.7, 0.2, -0.6) / (40.8, 0.5, -0.9) | 비율 0.62, 겹침 (0.56, 0.31, 0.32) | 가구-가구 겹침 | 상자가 휠체어에 걸쳐 보임; 개폐 가능한 상자였으면 열기 방해 | 중간 |
| A15 | `HospitalMap/Objects/bed/bed_016 (38)` | `HospitalMap/Objects/bench_a/bench_a_002 (11)` | (41.3, 0.3, 28.1) / (41.3, 0.6, 28.0) | 비율 0.62, 겹침 (2.04, 0.86, 2.20) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A16 | `HospitalMap/Objects/bed/bed_016 (14)` | `HospitalMap/Objects/ecg/ecg_003 (6)` | (14.0, 0.7, 21.2) / (13.4, 0.7, 20.7) | 비율 0.62, 겹침 (0.80, 1.65, 1.34) | 가구-가구 겹침 | 장비가 침대 안으로 파고들어 보일 수 있음 | 중간 (침대 옆 장비 배치 근사) |
| A17 | `HospitalMap/Objects/wheelchair/wheelchair_004 (1)` | `HospitalMap/Objects/wheelchair/wheelchair_004 (2)` | (61.5, 0.3, 48.3) / (62.0, 0.5, 48.6) | 비율 0.59, 겹침 (0.93, 1.16, 1.19) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A18 | `HospitalMap/Objects/bed/bed_016 (13)` | `HospitalMap/Objects/ecg/ecg_003 (5)` | (16.3, 0.5, 21.2) / (16.9, 0.9, 21.0) | 비율 0.59, 겹침 (0.89, 0.57, 1.49) | 가구-가구 겹침 | 장비가 침대 안으로 파고들어 보일 수 있음 | 중간 (침대 옆 장비 배치 근사) |
| A19 | `HospitalMap/Objects/anesthesia/anesthesia_002 (4)` | `HospitalMap/Objects/ecg/ecg_003 (19)` | (37.2, 0.7, 51.9) / (37.9, 0.7, 51.5) | 비율 0.58, 겹침 (1.16, 1.41, 1.12) | 가구-가구 겹침 | 장비가 침대 안으로 파고들어 보일 수 있음 | 중간 (침대 옆 장비 배치 근사) |
| A20 | `HospitalMap/Objects/bed/bed_010 (2)` | `HospitalMap/Objects/bed/bed_012` | (113.1, 1.4, 29.4) / (113.6, 0.6, 28.5) | 비율 0.56, 겹침 (1.68, 1.12, 1.59) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A21 | `HospitalMap/Objects/bed/bed_016 (48)` | `HospitalMap/Objects/bed/bed_016 (49)` | (64.0, 0.6, 47.8) / (64.7, 1.2, 48.1) | 비율 0.55, 겹침 (1.97, 0.86, 2.35) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A22 | `HospitalMap/Objects/bed/bed_010` | `HospitalMap/Objects/bed/bed_010 (1)` | (114.1, 1.4, 28.5) / (113.1, 1.5, 27.9) | 비율 0.55, 겹침 (1.10, 1.45, 1.76) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A23 | `HospitalMap/Objects/anesthesia/anesthesia_002 (5)` | `HospitalMap/Objects/bed/bed_016 (32)` | (36.5, 0.4, 47.4) / (37.1, 0.7, 46.8) | 비율 0.55, 겹침 (2.12, 1.80, 1.92) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A24 | `HospitalMap/Objects/anesthesia/anesthesia_002 (5)` | `HospitalMap/Objects/bed/bed_016 (30)` | (36.5, 0.4, 47.4) / (35.5, 0.5, 46.9) | 비율 0.54, 겹침 (1.19, 1.04, 1.99) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A25 | `HospitalMap/Objects/anesthesia/anesthesia_002 (3)` | `HospitalMap/Objects/ecg/ecg_003 (20)` | (48.2, 0.5, 46.6) / (49.5, 0.9, 46.9) | 비율 0.53, 겹침 (0.58, 0.85, 1.56) | 가구-가구 겹침 | 장비가 침대 안으로 파고들어 보일 수 있음 | 중간 (침대 옆 장비 배치 근사) |
| A26 | `HospitalMap/Objects/anesthesia/anesthesia_002 (3)` | `HospitalMap/Objects/anesthesia/anesthesia_002 (7)` | (48.2, 0.5, 46.6) / (47.8, 0.7, 45.9) | 비율 0.53, 겹침 (2.36, 1.18, 1.96) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A27 | `HospitalMap/Objects/bench_a/bench_a_002 (24)` | `HospitalMap/Objects/bench_a/bench_a_002 (25)` | (68.5, 0.5, -8.4) / (67.8, 0.5, -8.7) | 비율 0.52, 겹침 (1.15, 1.02, 1.19) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A28 | `HospitalMap/Objects/bench_a/bench_a_002 (2)` | `HospitalMap/Objects/wheelchair/wheelchair_005 (3)` | (50.2, 0.3, 13.4) / (49.9, 0.4, 12.9) | 비율 0.52, 겹침 (1.46, 1.20, 0.76) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A29 | `HospitalMap/Objects/bed/bed_016 (23)` | `HospitalMap/Objects/wheelchair/wheelchair_007 (10)` | (19.0, 0.3, 25.6) / (19.3, 0.3, 26.6) | 비율 0.52, 겹침 (1.61, 1.38, 0.92) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A30 | `HospitalMap/Objects/bed/bed_016 (12)` | `HospitalMap/Objects/ecg/ecg_003 (4)` | (19.5, 0.5, 21.2) / (18.7, 0.7, 20.7) | 비율 0.52, 겹침 (1.23, 1.04, 1.10) | 가구-가구 겹침 | 장비가 침대 안으로 파고들어 보일 수 있음 | 중간 (침대 옆 장비 배치 근사) |
| A31 | `HospitalMap/Objects/bed/bed_016 (36)` | `HospitalMap/Objects/bed/bed_016 (33)` | (36.7, 1.2, 45.1) / (37.5, 0.7, 44.2) | 비율 0.51, 겹침 (2.04, 1.04, 1.70) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A32 | `HospitalMap/Objects/bed/bed_016 (23)` | `HospitalMap/Objects/bed/bed_016 (25)` | (19.0, 0.3, 25.6) / (19.4, 0.6, 24.6) | 비율 0.51, 겹침 (2.39, 1.33, 1.51) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A33 | `HospitalMap/Objects/anesthesia/anesthesia_002 (3)` | `HospitalMap/Objects/bed/bed_016 (45)` | (48.2, 0.5, 46.6) / (48.7, 0.4, 47.6) | 비율 0.51, 겹침 (2.38, 1.38, 1.24) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A34 | `HospitalMap/Objects/wheelchair/wheelchair_007 (16)` | `HospitalMap/Objects/wheelchair/wheelchair_007 (17)` | (34.0, 0.3, 37.9) / (34.7, 0.3, 37.5) | 비율 0.49, 겹침 (0.71, 0.94, 0.87) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A35 | `HospitalMap/Objects/bed/bed_012` | `HospitalMap/Objects/ecg/ecg_002` | (113.6, 0.6, 28.5) / (113.2, 0.7, 29.6) | 비율 0.49, 겹침 (1.22, 1.25, 0.58) | 가구-가구 겹침 | 장비가 침대 안으로 파고들어 보일 수 있음 | 중간 (침대 옆 장비 배치 근사) |
| A36 | `HospitalMap/Objects/bed/bed_016 (23)` | `HospitalMap/Objects/wheelchair/wheelchair_007 (11)` | (19.0, 0.3, 25.6) / (17.6, 0.3, 26.2) | 비율 0.47, 겹침 (0.58, 0.94, 1.04) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A37 | `HospitalMap/Objects/bed/bed_014 (2)` | `HospitalMap/Objects/bed/bed_014 (3)` | (56.9, 1.1, 21.6) / (56.5, 0.7, 22.2) | 비율 0.47, 겹침 (1.72, 1.26, 2.14) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A38 | `HospitalMap/Objects/bed/bed_010` | `HospitalMap/Objects/bed/bed_010 (2)` | (114.1, 1.4, 28.5) / (113.1, 1.4, 29.4) | 비율 0.47, 겹침 (1.10, 1.52, 1.45) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A39 | `HospitalMap/Objects/bed/bed_016 (45)` | `HospitalMap/Objects/ecg/ecg_003 (20)` | (48.7, 0.4, 47.6) / (49.5, 0.9, 46.9) | 비율 0.45, 겹침 (0.86, 0.76, 0.99) | 가구-가구 겹침 | 장비가 침대 안으로 파고들어 보일 수 있음 | 중간 (침대 옆 장비 배치 근사) |
| A40 | `HospitalMap/Objects/bed/bed_016 (21)` | `HospitalMap/Objects/wheelchair/wheelchair_007 (13)` | (25.1, 0.5, 31.2) / (26.0, 0.3, 30.8) | 비율 0.44, 겹침 (1.11, 1.04, 1.48) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A41 | `HospitalMap/Objects/bed/bed_016 (16)` | `HospitalMap/Objects/cardboard_crushed/cardboard_crushed_011` | (10.6, 0.5, 31.3) / (10.5, 0.1, 32.6) | 비율 0.44, 겹침 (0.97, 0.12, 0.37) | 가구-가구 겹침 | 상자가 휠체어에 걸쳐 보임; 개폐 가능한 상자였으면 열기 방해 | 중간 |
| A42 | `HospitalMap/Objects/bed/bed_010` | `HospitalMap/Objects/bed/bed_010 (3)` | (114.1, 1.4, 28.5) / (113.1, 2.2, 27.9) | 비율 0.44, 겹침 (1.02, 1.14, 1.93) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A43 | `HospitalMap/Objects/bed/bed_010 (5)` | `HospitalMap/Objects/bed/bed_010 (7)` | (113.1, 2.2, 17.2) / (113.1, 1.8, 15.9) | 비율 0.43, 겹침 (2.57, 2.07, 1.06) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A44 | `HospitalMap/Objects/bed/bed_016 (75)` | `HospitalMap/Objects/bed/bed_016 (63)` | (86.0, 0.7, 41.1) / (86.7, 0.7, 40.2) | 비율 0.41, 겹침 (1.93, 1.70, 1.08) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A45 | `HospitalMap/Objects/bed/bed_016 (49)` | `HospitalMap/Objects/bed/bed_016 (50)` | (64.7, 1.2, 48.1) / (65.7, 1.4, 47.5) | 비율 0.41, 겹침 (1.35, 1.84, 2.05) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A46 | `HospitalMap/Objects/bed/bed_016 (37)` | `HospitalMap/Objects/bed/bed_016 (39)` | (43.5, 0.5, 28.9) / (42.6, 0.5, 29.9) | 비율 0.41, 겹침 (1.55, 1.26, 1.81) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A47 | `HospitalMap/Objects/bench_b/bench_b_003 (13)` | `HospitalMap/Objects/filing/filing_006 (1)` | (21.3, 0.4, 44.6) / (20.9, 0.7, 45.4) | 비율 0.40, 겹침 (0.69, 1.22, 0.65) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A48 | `HospitalMap/Objects/bed/bed_016 (38)` | `HospitalMap/Objects/wheelchair/wheelchair_007 (15)` | (41.3, 0.3, 28.1) / (41.2, 0.3, 26.7) | 비율 0.40, 겹침 (1.15, 0.80, 0.42) | 가구-가구 겹침 | 모델이 서로 관통해 보임 | 중간 |
| A49 | `HospitalMap/Objects/bed/bed_016 (29)` | `HospitalMap/Objects/bed/bed_016 (31)` | (36.3, 0.5, 45.0) / (35.8, 0.5, 43.6) | 비율 0.40, 겹침 (2.08, 1.04, 1.35) | 가구-가구 겹침 | 이동 경로·시야를 막거나 침대 위에 침대가 떠 보일 수 있음 | 높음 (같은 종류를 쌓아/겹쳐 둔 더미 추정) |
| A50 | `HospitalMap/Objects/tea_niche_broken/tea_niche_broken_002 (3)` | `HospitalMap/Objects/waste/waste_009` | (53.8, 0.4, 5.8) / (54.1, 0.3, 5.7) | 비율 1.00, 겹침 (1.46, 0.61, 1.26) | 가구-가구 겹침 (잔해 포함) | 낮음 (바닥 잔해) | 높음 (잔해/폐기물 장식) |
| A51 | `HospitalMap/Objects/tea_niche_broken/tea_niche_broken_002` | `HospitalMap/Objects/wheelchair/wheelchair_007 (15)` | (42.5, 0.8, 27.4) / (41.2, 0.3, 26.7) | 비율 0.54, 겹침 (0.67, 0.74, 1.04) | 가구-가구 겹침 (잔해 포함) | 낮음 (바닥 잔해) | 높음 (잔해/폐기물 장식) |
| A52 | `HospitalMap/Objects/bench_a/bench_a_002 (11)` | `HospitalMap/Objects/tea_niche_broken/tea_niche_broken_002` | (41.3, 0.6, 28.0) / (42.5, 0.8, 27.4) | 비율 0.49, 겹침 (1.16, 1.36, 1.93) | 가구-가구 겹침 (잔해 포함) | 낮음 (바닥 잔해) | 높음 (잔해/폐기물 장식) |
| A53 | `HospitalMap/Objects/bed/bed_016 (2)` | `HospitalMap/Objects/tea_niche_broken/tea_niche_broken_002 (2)` | (53.8, 0.5, 14.2) / (53.8, 0.4, 13.4) | 비율 0.45, 겹침 (2.32, 0.75, 1.14) | 가구-가구 겹침 (잔해 포함) | 낮음 (바닥 잔해) | 높음 (잔해/폐기물 장식) |
| A54 | `HospitalMap/Objects/bed/bed_016 (28)` | `HospitalMap/Objects/waste/waste_004` | (22.4, 0.5, 1.9) / (21.0, 0.3, 2.3) | 비율 0.42, 겹침 (0.65, 0.61, 1.29) | 가구-가구 겹침 (잔해 포함) | 낮음 (바닥 잔해) | 높음 (잔해/폐기물 장식) |

## B. 벽 관통 (34건)

가구 상자(5% 축소)가 벽 상자 안으로 0.12m 이상 들어간 경우. 최대 값이 0.20m 로 잘려 있어 실제 침투는 더 클 수 있습니다.

| # | 문제 오브젝트 (Hierarchy) | 겹치는 상대 오브젝트 | 월드 좌표 | 겹침 정도 | 유형 | 게임플레이 영향 | 의도적 배치 가능성 |
|---|---|---|---|---|---|---|---|
| B1 | `HospitalMap/Objects/tea_niche_broken/tea_niche_broken_002 (3)` | `HospitalMap/Walls/Room_10/Wall_079` | (53.8, 0.4, 5.8) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B2 | `HospitalMap/Objects/ecg/ecg_003 (2)` | `HospitalMap/Walls/Room_06/Wall_075` | (23.7, 0.7, 20.3) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B3 | `HospitalMap/Objects/bookcase/bookcase_003 (1)` | `HospitalMap/Walls/Room_07/WallFill_023` | (64.9, 0.2, 29.9) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B4 | `HospitalMap/Objects/bench_b/bench_b_005` | `HospitalMap/Walls/Hall_01/Wall_037` | (113.5, 0.6, 15.7) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B5 | `HospitalMap/Objects/bed/bed_016 (9)` | `HospitalMap/Walls/Room_09/Wall_084` | (70.3, 0.9, 3.3) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B6 | `HospitalMap/Objects/bed/bed_016 (54)` | `HospitalMap/Walls/4) (Room_10/WallDoor_04` | (48.6, 0.5, -7.4) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B7 | `HospitalMap/Objects/bed/bed_016 (49)` | `HospitalMap/Walls/Room_03/Wall_042` | (64.7, 1.2, 48.1) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B8 | `HospitalMap/Objects/bed/bed_016 (31)` | `HospitalMap/Walls/Room_02/Wall_059` | (35.8, 0.5, 43.6) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B9 | `HospitalMap/Objects/bed/bed_016 (30)` | `HospitalMap/Walls/Room_02/Wall_059` | (35.5, 0.5, 46.9) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B10 | `HospitalMap/Objects/bed/bed_010 (7)` | `HospitalMap/Walls/Hall_01/Wall_037` | (113.1, 1.8, 15.9) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B11 | `HospitalMap/Objects/bed/bed_010 (6)` | `HospitalMap/Walls/Hall_01/Wall_031` | (113.1, 1.4, 18.7) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B12 | `HospitalMap/Objects/bed/bed_010 (3)` | `HospitalMap/Walls/Hall_01/Wall_043` | (113.1, 2.2, 27.9) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B13 | `HospitalMap/Objects/bed/bed_010 (2)` | `HospitalMap/Walls/Hall_01/Wall_055` | (113.1, 1.4, 29.4) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B14 | `HospitalMap/Objects/bed/bed(2) (55)` | `HospitalMap/Walls/Room_13/Wall_000` | (5.3, 1.5, 8.7) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B15 | `HospitalMap/Objects/anesthesia/anesthesia_002` | `HospitalMap/Walls/Room_02/Wall_052` | (50.8, 1.1, 40.1) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B16 | `HospitalMap/Objects/anesthesia/anesthesia_002 (6)` | `HospitalMap/Walls/Room_02/Wall_059` | (35.6, 0.2, 41.1) | 침투 약 0.20m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B17 | `HospitalMap/Objects/anesthesia/anesthesia_002 (8)` | `HospitalMap/Walls/1) (Hall_01/Wall_011` | (69.7, 0.1, -23.4) | 침투 약 0.19m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B18 | `HospitalMap/Objects/ecg/ecg_004` | `HospitalMap/Walls/Room_10/Wall_083` | (42.7, 0.1, 4.2) | 침투 약 0.18m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B19 | `HospitalMap/Objects/bed/bed_016 (77)` | `HospitalMap/Walls/3) (Hall_01/Wall_007` | (45.3, 0.8, -10.8) | 침투 약 0.18m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B20 | `HospitalMap/Objects/bed/bed_011` | `HospitalMap/Walls/Hall_01/Wall_043` | (112.2, 0.6, 27.8) | 침투 약 0.18m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B21 | `HospitalMap/Objects/waste/waste_007` | `HospitalMap/Walls/Room_04/Wall_070` | (72.4, 0.3, 36.5) | 침투 약 0.17m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B22 | `HospitalMap/Objects/bench_a/bench_a_003` | `HospitalMap/Walls/1) (Hall_01/Wall_001` | (10.7, 0.4, 36.4) | 침투 약 0.17m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B23 | `HospitalMap/Objects/bench_a/bench_a_002 (32)` | `HospitalMap/Walls/Room_04/Wall_070` | (87.3, 0.5, 36.8) | 침투 약 0.16m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B24 | `HospitalMap/Objects/wheelchair/wheelchair_005 (5)` | `HospitalMap/Walls/Room_15/WallFill_006` | (40.8, 0.5, -0.9) | 침투 약 0.15m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B25 | `HospitalMap/Objects/builtin_cabinet_open/builtin_cabinet_open_003 (3)` | `HospitalMap/Walls/Room_07/WallFill_021` | (36.2, 0.4, 23.3) | 침투 약 0.15m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B26 | `HospitalMap/Objects/bed/bed_016 (20)` | `HospitalMap/Walls/Room_06/Wall_073` | (22.1, 0.5, 31.7) | 침투 약 0.15m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B27 | `HospitalMap/Objects/anesthesia/anesthesia_002 (2)` | `HospitalMap/Walls/Room_17/Wall_028` | (90.5, 1.1, -11.9) | 침투 약 0.15m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B28 | `HospitalMap/Objects/wheelchair/wheelchair_004 (2)` | `HospitalMap/Walls/Room_03/Wall_042` | (62.0, 0.5, 48.6) | 침투 약 0.13m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B29 | `HospitalMap/Objects/ecg/ecg_003 (7)` | `HospitalMap/Walls/Room_06/Wall_075` | (9.8, 0.7, 20.3) | 침투 약 0.13m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B30 | `HospitalMap/Objects/ecg/ecg_003 (3)` | `HospitalMap/Walls/Room_06/Wall_075` | (22.0, 0.7, 20.3) | 침투 약 0.13m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B31 | `HospitalMap/Objects/ecg/ecg_003 (13)` | `HospitalMap/Walls/Room_06/Wall_073` | (26.4, 0.1, 32.2) | 침투 약 0.13m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B32 | `HospitalMap/Objects/bench_a/bench_a_002 (3)` | `HospitalMap/Walls/Room_11/WallFill_014` | (29.9, 0.5, 12.8) | 침투 약 0.13m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B33 | `HospitalMap/Objects/bed/bed_016 (56)` | `HospitalMap/Walls/1) (Room_10/WallDoor_04` | (32.7, 0.5, -5.9) | 침투 약 0.12m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|
| B34 | `HospitalMap/Objects/bed/bed_016 (27)` | `HospitalMap/Walls/Room_02/Wall_071` | (48.8, 0.5, 40.2) | 침투 약 0.12m | 벽 관통(근사) | 벽에서 모델 일부가 튀어나오거나 벽 속으로 들어가 보임 | 낮음~중간 (벽에 붙여 놓은 가구의 상자 근사 오차 최대 0.2m 가능)|

## D. 출입문 통로 차단 (3건)

문 중심 기준 1.6x1.9x1.6m 통로 상자와 가구 상자가 0.15m 이상 겹친 경우.

| # | 문제 오브젝트 (Hierarchy) | 겹치는 상대 오브젝트 | 월드 좌표 | 겹침 정도 | 유형 | 게임플레이 영향 | 의도적 배치 가능성 |
|---|---|---|---|---|---|---|---|
| D1 | `HospitalMap/Walls/Room_10/WallDoor_04 (4)/DoorFrame.004` | `HospitalMap/Objects/bed/bed_016 (54)` | 문 (47.6, 0.0, -6.4) / 가구 (48.6, 0.5, -7.4) | 통로 상자와 겹침 (1.20, 0.98, 0.97) | 출입문 통로 차단 | 침대 1개가 문 통로(1.6m 폭)에 걸려 문이 막히거나 좁아짐 - **실제 동선 문제 가능성 높음** | 낮음~중간 (침대 정리 위치 불명) |
| D2 | `HospitalMap/Walls/Room_13/WallDoor_02 (1)/DoorFrame.002` | `HospitalMap/Objects/builtin_cabinet_open/builtin_cabinet_open_003 (1)` | 문 (2.5, 0.0, 9.6) / 가구 (3.2, 0.4, 9.7) | 통로 상자와 겹침 (0.67, 0.86, 1.60) | 출입문 통로 차단 | 붙박이장(builtin_cabinet_open_003 (1))이 문 앞 0.67m 점유 - 문 열림/통행 방해 가능 | 낮음~중간 (침대 정리 위치 불명) |
| D3 | `HospitalMap/Walls/Room_10/WallDoor_04 (1)/DoorFrame.004` | `HospitalMap/Objects/bed/bed_016 (56)` | 문 (32.8, 0.0, -6.4) / 가구 (32.7, 0.5, -5.9) | 통로 상자와 겹침 (1.60, 1.19, 0.85) | 출입문 통로 차단 | 침대 1개가 문 통로에 걸림 - **실제 동선 문제 가능성 높음** | 낮음~중간 (침대 정리 위치 불명) |

## E. 개폐부 장애물 (10건)

수납 가구 개폐부 정면 0.7m 이내 상자 영역에 다른 Collider 가 있는 경우(장식용 제외).

| # | 문제 오브젝트 (Hierarchy) | 겹치는 상대 오브젝트 | 월드 좌표 | 겹침 정도 | 유형 | 게임플레이 영향 | 의도적 배치 가능성 |
|---|---|---|---|---|---|---|---|
| E1 | `HospitalMap/Objects/tea_niche/tea_niche_001 (1)` | `Room_10/WallDoor_04 (5)` | (51.7, 1.2, -7.1) | 개폐부 정면 0.7m 안에 존재 | 개폐부 장애물 | tea_niche 앞을 문/커튼이 가로막음 - 열기 방해 가능 (후보에서 제외 검토) | 낮음~중간 |
| E2 | `HospitalMap/Objects/cardboard_box/cardboard_box_011` | `Room_12/Wall_097, Hall_01/WallCorner_078, Room_12/Wall_098` | (102.6, 0.2, 9.2) | 개폐부 정면 0.7m 안에 존재 | 개폐부 장애물 | 박스 열리는 방향이 벽 쪽 - 열기 어려움(박스는 내용물 없음, 중요도 낮음) | 높음 (벽에 붙여 놓은 박스) |
| E3 | `HospitalMap/Objects/cardboard_box/cardboard_box_008` | `Room_04/Wall_070` | (73.4, 0.2, 35.4) | 개폐부 정면 0.7m 안에 존재 | 개폐부 장애물 | 박스 열리는 방향이 벽 쪽 - 열기 어려움(박스는 내용물 없음, 중요도 낮음) | 높음 (벽에 붙여 놓은 박스) |
| E4 | `HospitalMap/Objects/cardboard_box/cardboard_box_012` | `Hall_01/Wall_001, Hall_01/WallCorner_001` | (7.4, 0.2, 36.8) | 개폐부 정면 0.7m 안에 존재 | 개폐부 장애물 | 박스 열리는 방향이 벽 쪽 - 열기 어려움(박스는 내용물 없음, 중요도 낮음) | 높음 (벽에 붙여 놓은 박스) |
| E5 | `HospitalMap/Objects/anesthesia/anesthesia_002 (1)` | `bed/bed_016 (26), cutton/Hospital_cutton_v1 (1)` | (50.7, 1.1, 53.0) | 개폐부 정면 0.7m 안에 존재 | 개폐부 장애물 | 서랍 앞에 침대/카트가 있어 서랍을 열 수 없을 수 있음 (현재 이 서랍엔 스폰 후보 없음/일부 있음 - 후보 audit 결과 안 쓰도록 검토 필요) | 낮음~중간 |
| E6 | `HospitalMap/Objects/anesthesia/anesthesia_002 (2)` | `operating_Room/operatingRoom_cart, operating_Room/chair_v1 (8)` | (90.5, 1.1, -11.9) | 개폐부 정면 0.7m 안에 존재 | 개폐부 장애물 | 서랍 앞에 침대/카트가 있어 서랍을 열 수 없을 수 있음 (현재 이 서랍엔 스폰 후보 없음/일부 있음 - 후보 audit 결과 안 쓰도록 검토 필요) | 낮음~중간 |
| E7 | `HospitalMap/Walls/Room_WasteStorage/cardboard_box` | `Hall_01/WallCorner_053, Hall_01/Wall_056` | (112.1, 0.2, 53.3) | 개폐부 정면 0.7m 안에 존재 | 개폐부 장애물 | 박스 열리는 방향이 벽 쪽 - 열기 어려움(박스는 내용물 없음, 중요도 낮음) | 높음 (벽에 붙여 놓은 박스) |
| E8 | `HospitalMap/Objects/cardboard_box/cardboard_box_005` | `Hall_01/Wall_037` | (121.1, 0.2, 15.6) | 개폐부 정면 0.7m 안에 존재 | 개폐부 장애물 | 박스 열리는 방향이 벽 쪽 - 열기 어려움(박스는 내용물 없음, 중요도 낮음) | 높음 (벽에 붙여 놓은 박스) |
| E9 | `HospitalMap/Objects/cardboard_box/cardboard_box_006` | `Room_13/Wall_077` | (13.1, 0.2, 6.4) | 개폐부 정면 0.7m 안에 존재 | 개폐부 장애물 | 박스 열리는 방향이 벽 쪽 - 열기 어려움(박스는 내용물 없음, 중요도 낮음) | 높음 (벽에 붙여 놓은 박스) |
| E10 | `HospitalMap/Objects/tea_niche/tea_niche_001` | `cutton/Hospital_cutton_v1` | (36.7, 1.2, 12.0) | 개폐부 정면 0.7m 안에 존재 | 개폐부 장애물 | tea_niche 앞을 문/커튼이 가로막음 - 열기 방해 가능 (후보에서 제외 검토) | 낮음~중간 |
