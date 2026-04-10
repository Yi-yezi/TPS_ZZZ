using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace SkillSystem.Editor
{
    /// <summary>
    /// CommandTransitionClip 自定义 Inspector
    /// 在 Timeline 窗口中选中 Clip 时显示，修改属性后同步轨道名和 Clip 显示名
    /// </summary>
    [CustomEditor(typeof(CommandTransitionClip))]
    public class CommandTransitionClipEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("commandTransition"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("inputBufferDuration"),
                new GUIContent("输入缓冲"));
            bool changed = EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();

            if (changed)
                SyncDisplayNames((CommandTransitionClip)target);
        }

        /// <summary>
        /// 同步 Clip displayName 和所属轨道名称
        /// </summary>
        private static void SyncDisplayNames(CommandTransitionClip clipAsset)
        {
            var displayName = clipAsset.GetDisplayName();

            // 找到包含此 Clip 的 ActionSO 和 Track
            var guids = AssetDatabase.FindAssets("t:ActionSO");
            foreach (var guid in guids)
            {
                var action = AssetDatabase.LoadAssetAtPath<ActionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (action == null) continue;

                foreach (var track in action.GetOutputTracks())
                {
                    if (track is not CommandTransitionTrack ctTrack) continue;
                    foreach (var clip in ctTrack.GetClips())
                    {
                        if (clip.asset != clipAsset) continue;

                        clip.displayName = displayName;
                        ctTrack.name = displayName;
                        EditorUtility.SetDirty(action);
                        return;
                    }
                }
            }
        }
    }
}
