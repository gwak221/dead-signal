using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace DeadSignal.Audio.Editor
{
    /// <summary>
    /// AudioManager 인스펙터. 기본 리스트 UI에 검증 경고, 요약, 검색만 더한다.
    /// 클립 이름으로 항목을 자동 생성하지 않는다 (항목 이름은 사람이 직접 정한다).
    /// </summary>
    [CustomEditor(typeof(AudioManager))]
    public sealed class AudioManagerEditor : UnityEditor.Editor
    {
        string search = "";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var mixer = serializedObject.FindProperty("mixer");
            var defaultGroup = serializedObject.FindProperty("defaultGroup");
            var voiceCount = serializedObject.FindProperty("voiceCount");
            var sounds = serializedObject.FindProperty("sounds");

            EditorGUILayout.PropertyField(mixer);
            EditorGUILayout.PropertyField(defaultGroup);
            EditorGUILayout.PropertyField(voiceCount);
            EditorGUILayout.Space();

            DrawWarnings(sounds, mixer.objectReferenceValue as AudioMixer, defaultGroup.objectReferenceValue as AudioMixerGroup);

            int totalClips = 0;
            for (int i = 0; i < sounds.arraySize; i++)
                totalClips += SoundEntryDrawer.CountClips(sounds.GetArrayElementAtIndex(i).FindPropertyRelative("clips"));

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"등록 {sounds.arraySize}개 · 클립 {totalClips}개", GUILayout.Width(160));
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            }

            if (string.IsNullOrWhiteSpace(search))
            {
                EditorGUILayout.PropertyField(sounds, true);
            }
            else
            {
                // 검색 중에는 일치하는 항목만 보여준다 (데이터는 바꾸지 않음)
                string q = search.Trim();
                int shown = 0;
                for (int i = 0; i < sounds.arraySize; i++)
                {
                    var e = sounds.GetArrayElementAtIndex(i);
                    string n = e.FindPropertyRelative("name").stringValue ?? "";
                    string note = e.FindPropertyRelative("note").stringValue ?? "";
                    if (n.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        note.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    EditorGUILayout.PropertyField(e, true);
                    shown++;
                }
                if (shown == 0) EditorGUILayout.HelpBox($"\"{q}\"와 일치하는 항목이 없습니다.", MessageType.None);
            }

            serializedObject.ApplyModifiedProperties();
        }

        static void DrawWarnings(SerializedProperty sounds, AudioMixer mixer, AudioMixerGroup defaultGroup)
        {
            var empty = new List<int>();
            var noClips = new List<string>();
            var wrongGroup = new List<string>();
            var seen = new Dictionary<string, int>();
            var dupes = new List<string>();

            for (int i = 0; i < sounds.arraySize; i++)
            {
                var e = sounds.GetArrayElementAtIndex(i);
                string n = (e.FindPropertyRelative("name").stringValue ?? "").Trim();
                if (n.Length == 0) { empty.Add(i); continue; }
                if (seen.TryGetValue(n, out int first)) dupes.Add($"\"{n}\" (항목 {first}, {i})");
                else seen[n] = i;
                if (SoundEntryDrawer.CountClips(e.FindPropertyRelative("clips")) == 0) noClips.Add(n);
                var g = e.FindPropertyRelative("group").objectReferenceValue as AudioMixerGroup;
                if (mixer != null && g != null && g.audioMixer != mixer) wrongGroup.Add(n);
            }

            if (empty.Count > 0) EditorGUILayout.HelpBox("이름 없는 항목: " + string.Join(", ", empty), MessageType.Warning);
            if (dupes.Count > 0) EditorGUILayout.HelpBox("중복 이름 (첫 항목만 사용): " + string.Join(", ", dupes), MessageType.Warning);
            if (noClips.Count > 0) EditorGUILayout.HelpBox("클립 없음: " + string.Join(", ", noClips), MessageType.Warning);
            if (wrongGroup.Count > 0) EditorGUILayout.HelpBox("Mixer에 속하지 않은 그룹: " + string.Join(", ", wrongGroup), MessageType.Warning);
            if (defaultGroup == null) EditorGUILayout.HelpBox("Default Group이 비어 있습니다. 그룹 없는 항목은 믹서를 거치지 않습니다.", MessageType.Info);
        }
    }
}
