using UnityEngine;

/// <summary>
/// 방 하나의 소리 성격. RoomAudioZone이 참조하고, 플레이어가 그 Zone에 들어가면 RoomAudioController가 적용한다.
///   - 잔향(reverb): 화장실은 울리고, 병실은 먹먹하고, 창고는 길게 울린다
///   - 방 전용 환경음 루프 (냉장 창고 웅웅 등, 비워 두면 없음)
///   - 방 안에서 가끔 나는 랜덤 소리 (비워 두면 없음)
///   - 전역 드론 볼륨 배율
/// 만들기: Project 창 우클릭 > Create > Dead Signal > Room Audio Profile
/// </summary>
[CreateAssetMenu(menuName = "Dead Signal/Room Audio Profile", fileName = "NewRoomAudioProfile")]
public class RoomAudioProfile : ScriptableObject
{
    [Header("Reverb (잔향)")]
    [Tooltip("미리 만든 잔향 성격. Custom이면 아래 custom 값을 그대로 쓴다")]
    public RoomReverbPreset reverbPreset = RoomReverbPreset.Room;
    [Tooltip("잔향 크기 조절 (mB). +면 더 울리고, -면 덜 울린다. 0 = 프리셋 그대로")]
    [Range(-3000f, 1000f)] public float reverbLevelOffset = 0f;
    public RoomReverb custom = RoomReverb.FromPreset(RoomReverbPreset.Room);

    [Header("Ambience (방 전용 환경음)")]
    [Tooltip("방에 있는 동안 반복 재생 (2D 루프 권장). 비워 두면 없음")]
    public SoundCue ambienceLoop;
    [Tooltip("전역 드론(AmbienceDrone) 볼륨 배율. 방 환경음이 드론을 덮게 하려면 낮춘다")]
    [Range(0f, 1.5f)] public float droneWeight = 1f;

    [Header("Random one-shots (방 안에서 가끔 나는 소리)")]
    [Tooltip("플레이어 주변 랜덤 위치에서 가끔 재생 (3D 권장). 비워 두면 없음")]
    public SoundCue randomOneShot;
    [Tooltip("다음 소리까지 대기 시간 범위(초)")]
    public Vector2 randomInterval = new Vector2(8f, 20f);
    [Tooltip("플레이어로부터 이 거리(m) 범위 안에서 소리가 난다")]
    public Vector2 randomDistance = new Vector2(2f, 6f);

    [TextArea(2, 4)] public string note;

    public RoomReverb GetReverb()
    {
        var r = reverbPreset == RoomReverbPreset.Custom ? custom : RoomReverb.FromPreset(reverbPreset);
        r.reverb = Mathf.Clamp(r.reverb + reverbLevelOffset, -10000f, 2000f);
        r.reflections = Mathf.Clamp(r.reflections + reverbLevelOffset, -10000f, 1000f);
        return r;
    }
}

public enum RoomReverbPreset
{
    Off, Generic, PaddedCell, Room, Bathroom, LivingRoom, StoneRoom, Auditorium,
    Hallway, StoneCorridor, Hangar, Cave, Custom
}

/// <summary>AudioReverbZone 파라미터 묶음 (FMOD 프리셋 값). 프리셋끼리 부드럽게 섞기 위해 숫자로 들고 있는다.</summary>
[System.Serializable]
public struct RoomReverb
{
    [Range(-10000, 0)] public float room;
    [Range(-10000, 0)] public float roomHF;
    [Range(0.1f, 20f)] public float decayTime;
    [Range(0.1f, 2f)] public float decayHFRatio;
    [Range(-10000, 1000)] public float reflections;
    [Range(0f, 0.3f)] public float reflectionsDelay;
    [Range(-10000, 2000)] public float reverb;
    [Range(0f, 0.1f)] public float reverbDelay;
    [Range(0, 100)] public float diffusion;
    [Range(0, 100)] public float density;

    static RoomReverb P(float room, float roomHF, float decay, float hfRatio, float refl, float reflDelay, float rev, float revDelay, float diff = 100f, float dens = 100f)
        => new RoomReverb { room = room, roomHF = roomHF, decayTime = decay, decayHFRatio = hfRatio, reflections = refl, reflectionsDelay = reflDelay, reverb = rev, reverbDelay = revDelay, diffusion = diff, density = dens };

    public static RoomReverb FromPreset(RoomReverbPreset p)
    {
        switch (p)
        {
            case RoomReverbPreset.Off:           return P(-10000, -10000, 1.00f, 1.00f, -10000, 0.000f, -10000, 0.000f, 0, 0);
            case RoomReverbPreset.Generic:       return P(-1000, -100, 1.49f, 0.83f, -2602, 0.007f, 200, 0.011f);
            case RoomReverbPreset.PaddedCell:    return P(-1000, -6000, 0.17f, 0.10f, -1204, 0.001f, 207, 0.002f);
            case RoomReverbPreset.Room:          return P(-1000, -454, 0.40f, 0.83f, -1646, 0.002f, 53, 0.003f);
            case RoomReverbPreset.Bathroom:      return P(-1000, -1200, 1.49f, 0.54f, -370, 0.007f, 1030, 0.011f, 100, 60);
            case RoomReverbPreset.LivingRoom:    return P(-1000, -6000, 0.50f, 0.10f, -1376, 0.003f, -1104, 0.004f);
            case RoomReverbPreset.StoneRoom:     return P(-1000, -300, 2.31f, 0.64f, -711, 0.012f, 83, 0.017f);
            case RoomReverbPreset.Auditorium:    return P(-1000, -476, 4.32f, 0.59f, -789, 0.020f, -289, 0.030f);
            case RoomReverbPreset.Hallway:       return P(-1000, -300, 1.49f, 0.59f, -2219, 0.007f, 441, 0.011f);
            case RoomReverbPreset.StoneCorridor: return P(-1000, -237, 2.70f, 0.79f, -1214, 0.013f, 395, 0.020f);
            case RoomReverbPreset.Hangar:        return P(-1000, -1000, 10.05f, 0.23f, -602, 0.020f, 198, 0.030f);
            case RoomReverbPreset.Cave:          return P(-1000, 0, 2.91f, 1.30f, -602, 0.015f, -302, 0.022f);
            default:                             return P(-1000, -454, 0.40f, 0.83f, -1646, 0.002f, 53, 0.003f);
        }
    }

    public static RoomReverb MoveTowards(RoomReverb a, RoomReverb b, float t)
    {
        // t: 0~1 비율. 지수 감쇠처럼 쓰기 위해 Lerp
        return new RoomReverb
        {
            room = Mathf.Lerp(a.room, b.room, t),
            roomHF = Mathf.Lerp(a.roomHF, b.roomHF, t),
            decayTime = Mathf.Lerp(a.decayTime, b.decayTime, t),
            decayHFRatio = Mathf.Lerp(a.decayHFRatio, b.decayHFRatio, t),
            reflections = Mathf.Lerp(a.reflections, b.reflections, t),
            reflectionsDelay = Mathf.Lerp(a.reflectionsDelay, b.reflectionsDelay, t),
            reverb = Mathf.Lerp(a.reverb, b.reverb, t),
            reverbDelay = Mathf.Lerp(a.reverbDelay, b.reverbDelay, t),
            diffusion = Mathf.Lerp(a.diffusion, b.diffusion, t),
            density = Mathf.Lerp(a.density, b.density, t),
        };
    }

    public void ApplyTo(AudioReverbZone z)
    {
        z.reverbPreset = AudioReverbPreset.User;
        z.room = Mathf.RoundToInt(room);
        z.roomHF = Mathf.RoundToInt(roomHF);
        z.roomLF = 0;
        z.decayTime = decayTime;
        z.decayHFRatio = decayHFRatio;
        z.reflections = Mathf.RoundToInt(reflections);
        z.reflectionsDelay = reflectionsDelay;
        z.reverb = Mathf.RoundToInt(reverb);
        z.reverbDelay = reverbDelay;
        z.HFReference = 5000f;
        z.LFReference = 250f;
        z.diffusion = diffusion;
        z.density = density;
    }
}
