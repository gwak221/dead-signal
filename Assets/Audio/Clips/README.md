# 사운드 클립 넣는 법

각 폴더에 `<cue이름>_01.wav` 임시 파일이 들어 있다. 모든 Cue(`Assets/Audio/Cues`)에 이미 연결되어 있다.

- 짧은 효과음: 작은 "삑" 소리 (폴더마다 음 높이가 다르다. 어떤 소리가 어디서 나는지 테스트용)
- 반복 재생(BGM, 환경음, 비콘 험, 숨소리): 2초 무음

## 교체하기

1. **같은 이름, 같은 확장자(.wav)로 덮어쓰기** (탐색기에서 파일만 바꾸기): 연결이 그대로 유지된다. 추가 작업 없음.
2. **확장자가 다르거나(.mp3, .ogg) 여러 개를 넣을 때** (`door_open_01.ogg`, `door_open_02.ogg` ...):
   임시 `.wav`를 지우고 새 파일을 넣은 뒤 메뉴 `Tools > Dead Signal > Audio > 4. Auto-Assign Clips From Folders` 실행.
3. 반복 재생 사운드는 Import Settings에서 Load Type을 Streaming으로 두는 것을 권장 (BGM).

파일을 넣으면 `Assets/Audio/CREDITS.md` 에 출처와 라이선스를 한 줄 적는다.
