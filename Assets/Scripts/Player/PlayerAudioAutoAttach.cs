using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 소리 컴포넌트(PlayerSounds, PlayerBreathing)를 게임 시작·씬 로드 때 자동으로 붙인다.
/// 씬에 직접 붙여 둘 필요가 없어서, 씬 파일을 고치지 않고도 오디오가 동작한다 (씬 병합 충돌 줄이기).
/// 이미 붙어 있으면 그대로 둔다.
/// </summary>
static class PlayerAudioAutoAttach
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachAll();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachAll();

    static void AttachAll()
    {
        var players = Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var p in players)
        {
            var go = p.gameObject;
            if (go.GetComponent<PlayerSounds>() == null) go.AddComponent<PlayerSounds>();
            if (go.GetComponent<PlayerBreathing>() == null) go.AddComponent<PlayerBreathing>();
        }
    }
}
