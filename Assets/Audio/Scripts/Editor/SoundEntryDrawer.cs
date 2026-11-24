using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace DeadSignal.Audio.Editor
{
    /// <summary>SoundEntry 한 항목: 제목을 이름으로, 2D면 3D 필드 숨김, 맨 아래 미리듣기 버튼.</summary>
    [CustomPropertyDrawer(typeof(SoundEntry))]
    public sealed class SoundEntryDrawer : PropertyDrawer
    {
        static readonly string[] SpatialOnly = { "minDistance", "maxDistance", "rolloff" };
        static float Line => EditorGUIUtility.singleLineHeight;
        static float Gap => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = Line;
            if (!property.isExpanded) return h;
            bool spatial = property.FindPropertyRelative("spatial").boolValue;
            foreach (var child in Children(property))
            {
                if (!spatial && IsSpatialOnly(child)) continue;
                h += EditorGUI.GetPropertyHeight(child, true) + Gap;
            }
            return h + Line + Gap * 2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            string n = property.FindPropertyRelative("name").stringValue;
            var clips = property.FindPropertyRelative("clips");
            int clipCount = CountClips(clips);
            string title = string.IsNullOrWhiteSpace(n) ? "(이름 없음)" : n;
            string suffix = clipCount == 0 ? "   ⚠ 클립 없음" : $"   ({clipCount})";

            var row = new Rect(position.x, position.y, position.width, Line);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, title + suffix, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                bool spatial = property.FindPropertyRelative("spatial").boolValue;
                float y = row.yMax + Gap;
                foreach (var child in Children(property))
                {
                    if (!spatial && IsSpatialOnly(child)) continue;
                    float ch = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(new Rect(position.x, y, position.width, ch), child, true);
                    y += ch + Gap;
                }
                EditorGUI.indentLevel--;

                var btn = EditorGUI.IndentedRect(new Rect(position.x, y + Gap, 120f, Line));
                btn.width = 110f;
                using (new EditorGUI.DisabledScope(clipCount == 0))
                {
                    if (GUI.Button(btn, "▶ 미리듣기")) AudioPreview.Play(property);
                }
                var stop = new Rect(btn.xMax + 4f, btn.y, 60f, Line);
                if (GUI.Button(stop, "■ 정지")) AudioPreview.Stop();
            }

            EditorGUI.EndProperty();
        }

        static bool IsSpatialOnly(SerializedProperty p)
        {
            foreach (var s in SpatialOnly) if (p.name == s) return true;
            return false;
        }

        internal static int CountClips(SerializedProperty clips)
        {
            int c = 0;
            for (int i = 0; i < clips.arraySize; i++)
                if (clips.GetArrayElementAtIndex(i).objectReferenceValue != null) c++;
            return c;
        }

        static System.Collections.Generic.IEnumerable<SerializedProperty> Children(SerializedProperty property)
        {
            var it = property.Copy();
            var end = property.GetEndProperty();
            bool enter = true;
            while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
            {
                enter = false;
                yield return it.Copy();
            }
        }
    }

    /// <summary>에디터에서 항목 설정(볼륨·피치·그룹)을 적용해 클립을 들어본다.</summary>
    internal static class AudioPreview
    {
        static AudioSource source;

        public static void Play(SerializedProperty entry)
        {
            var clips = entry.FindPropertyRelative("clips");
            var valid = new System.Collections.Generic.List<AudioClip>();
            for (int i = 0; i < clips.arraySize; i++)
                if (clips.GetArrayElementAtIndex(i).objectReferenceValue is AudioClip c) valid.Add(c);
            if (valid.Count == 0) return;

            float vol = entry.FindPropertyRelative("volume").floatValue;
            float volJ = entry.FindPropertyRelative("volumeJitter").floatValue;
            float pitch = entry.FindPropertyRelative("pitch").floatValue;
            float pitchJ = entry.FindPropertyRelative("pitchJitter").floatValue;
            var group = entry.FindPropertyRelative("group").objectReferenceValue as AudioMixerGroup;

            Ensure();
            source.Stop();
            source.clip = valid[Random.Range(0, valid.Count)];
            source.volume = Mathf.Clamp01(vol + Random.Range(-volJ, volJ));
            source.pitch = Mathf.Max(0.01f, pitch + Random.Range(-pitchJ, pitchJ));
            source.outputAudioMixerGroup = group;
            source.Play();
        }

        public static void Stop()
        {
            if (source != null) source.Stop();
        }

        static void Ensure()
        {
            if (source != null) return;
            var go = EditorUtility.CreateGameObjectWithHideFlags("AudioPreview", HideFlags.HideAndDontSave, typeof(AudioSource));
            source = go.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
    }
}
