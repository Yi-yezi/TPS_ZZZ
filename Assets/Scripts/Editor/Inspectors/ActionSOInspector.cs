using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SkillSystem.Editor
{
    /// <summary>
    /// ActionSO 自定义 Inspector
    /// 由于继承了 TimelineAsset，轨道编辑交给 Unity Timeline 窗口
    /// Inspector 只负责显示战斗参数和转移配置
    /// </summary>
    [CustomEditor(typeof(ActionSO))]
    public class ActionSOInspector : UnityEditor.Editor
    {
        private ActionSO _target;

        // 折叠状态
        private bool _foldAnim = true;
        private bool _foldCombat = true;
        private bool _foldFinishTransition = true;
        private bool _foldCommandTransitions = true;
        private bool _foldSignalTransitions = true;

        private void OnEnable()
        {
            _target = (ActionSO)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 名称编辑
            var nameProp = serializedObject.FindProperty("m_Name");
            EditorGUILayout.PropertyField(nameProp, new GUIContent("动作名称"));

            EditorGUILayout.Space(4);

            // 在 Timeline 中打开按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("在 Timeline 中打开（预览）", GUILayout.Height(28)))
            {
                ActionPreviewUtility.OpenPreview(_target);
            }

            using (new EditorGUI.DisabledScope(!ActionPreviewUtility.IsPreviewActive))
            {
                if (GUILayout.Button("关闭预览", GUILayout.Height(28), GUILayout.Width(70)))
                {
                    ActionPreviewUtility.CleanupPreview();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (ActionPreviewUtility.IsPreviewActive)
            {
                EditorGUILayout.HelpBox($"预览对象已创建，关闭 Timeline 窗口或点击\"关闭预览\"会自动清理", MessageType.Info);
            }

            EditorGUILayout.Space(4);

            // 动画状态（只读显示，来自 AnimatorStateTrack）
            _foldAnim = EditorGUILayout.Foldout(_foldAnim, "动画配置", true, EditorStyles.foldoutHeader);
            if (_foldAnim)
            {
                EditorGUI.indentLevel++;
                DrawAnimatorStateInfo(_target);
                EditorGUILayout.Space(2);
                DrawTimelineSummary(_target);
                EditorGUI.indentLevel--;
            }

            // 战斗参数
            _foldCombat = EditorGUILayout.Foldout(_foldCombat, "战斗参数", true, EditorStyles.foldoutHeader);
            if (_foldCombat)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("damage"), new GUIContent("伤害"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cooldown"), new GUIContent("冷却时间"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("attackDistance"), new GUIContent("攻击距离"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("attackOffset"), new GUIContent("攻击偏移"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("hitboxNames"), new GUIContent("命中判定名"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("parryNames"), new GUIContent("弹反判定名"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);
            DrawSeparator("状态转移");

            // 完成转移
            _foldFinishTransition = EditorGUILayout.Foldout(_foldFinishTransition,
                "完成转移 (动作结束后)", true, EditorStyles.foldoutHeader);
            if (_foldFinishTransition)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("finishTransition"), true);
                DrawActionNamePopup(serializedObject.FindProperty("inheritTransitionActionName"), "继承转移自");
                EditorGUI.indentLevel--;
            }

            // 指令转移（每条 CommandTransitionTrack 对应一条转移）
            var ctClips = _target.GetCommandTransitionClips().ToList();
            _foldCommandTransitions = EditorGUILayout.Foldout(_foldCommandTransitions,
                $"指令转移 ({ctClips.Count})", true, EditorStyles.foldoutHeader);
            if (_foldCommandTransitions)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < ctClips.Count; i++)
                {
                    var (timelineClip, ctAsset, ctTrack) = ctClips[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    EditorGUILayout.LabelField(
                        $"[{timelineClip.start:F2}s ~ {timelineClip.start + timelineClip.duration:F2}s]",
                        EditorStyles.boldLabel);

                    var clipSO = new SerializedObject(ctAsset);
                    clipSO.Update();
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(clipSO.FindProperty("commandTransition"), true);
                    EditorGUILayout.PropertyField(clipSO.FindProperty("inputBufferDuration"),
                        new GUIContent("输入缓冲"));
                    if (EditorGUI.EndChangeCheck())
                    {
                        clipSO.ApplyModifiedProperties();
                        var displayName = ctAsset.GetDisplayName();
                        timelineClip.displayName = displayName;
                        ctTrack.name = displayName;
                        EditorUtility.SetDirty(_target);
                    }
                    else
                    {
                        clipSO.ApplyModifiedProperties();
                    }

                    if (GUILayout.Button("删除", GUILayout.Width(50)))
                    {
                        _target.DeleteTrack(ctTrack);
                        EditorUtility.SetDirty(_target);
                        AssetDatabase.SaveAssets();
                        TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
                        GUIUtility.ExitGUI();
                    }

                    EditorGUILayout.EndVertical();
                }

                if (GUILayout.Button("添加指令转移"))
                {
                    var track = _target.CreateTrack<CommandTransitionTrack>(null, "指令转移");
                    track.CreateClip<CommandTransitionClip>();
                    EditorUtility.SetDirty(_target);
                    AssetDatabase.SaveAssets();
                    TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
                }

                EditorGUI.indentLevel--;
            }

            // 信号转移
            _foldSignalTransitions = EditorGUILayout.Foldout(_foldSignalTransitions,
                $"信号转移 ({_target.signalTransitions?.Count ?? 0})", true, EditorStyles.foldoutHeader);
            if (_foldSignalTransitions)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("signalTransitions"),
                    new GUIContent("信号转移"), true);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawSeparator(string label)
        {
            EditorGUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 9, rect.width, 1),
                new Color(0.5f, 0.5f, 0.5f, 0.5f));
            var labelRect = new Rect(rect.x + 8, rect.y, 100, 20);

            var bgRect = new Rect(labelRect.x - 4, labelRect.y, labelRect.width + 8, labelRect.height);
            EditorGUI.DrawRect(bgRect, new Color(0.22f, 0.22f, 0.22f, 1f));

            GUI.Label(labelRect, label, EditorStyles.boldLabel);
            EditorGUILayout.Space(2);
        }

        private static void DrawActionNamePopup(SerializedProperty prop, string label)
        {
            var actionNames = GetActionNames();
            if (actionNames.Count == 0)
            {
                EditorGUILayout.PropertyField(prop, new GUIContent(label));
                return;
            }

            var options = new List<string> { "(无)" };
            options.AddRange(actionNames);

            string current = prop.stringValue ?? "";
            int selected = 0;
            if (!string.IsNullOrEmpty(current))
            {
                int idx = actionNames.IndexOf(current);
                selected = idx >= 0 ? idx + 1 : -1;
            }

            if (selected < 0)
            {
                options.Insert(1, $"? {current}");
                selected = 1;
            }

            EditorGUI.BeginChangeCheck();
            int newIdx = EditorGUILayout.Popup(label, selected, options.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                if (newIdx == 0)
                    prop.stringValue = "";
                else
                {
                    string chosen = options[newIdx];
                    if (chosen.StartsWith("? "))
                        chosen = chosen[2..];
                    prop.stringValue = chosen;
                }
            }
        }

        private static void DrawAnimatorStateInfo(ActionSO action)
        {
            EditorGUILayout.LabelField("Animator State", action.name, EditorStyles.boldLabel);

            var animTrack = action.GetOutputTracks().OfType<AnimationTrack>().FirstOrDefault();
            if (animTrack == null)
            {
                EditorGUILayout.HelpBox("尚未添加 Animation 轨道，建议通过\"从 State 创建\"自动生成", MessageType.Warning);
                return;
            }

            var firstClip = animTrack.GetClips().FirstOrDefault();
            if (firstClip?.asset is AnimationPlayableAsset playableAsset && playableAsset.clip != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("动画 Clip", playableAsset.clip,
                        typeof(AnimationClip), false);
                }
                EditorGUILayout.LabelField("动画时长", $"{playableAsset.clip.length:F2}s", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.HelpBox("Animation 轨道上还没有动画 Clip", MessageType.Info);
            }
        }

        private static void DrawTimelineSummary(ActionSO action)
        {
            var runtimeData = ActionUnpacker.Unpack(action);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"Timeline 轨道数: {runtimeData.TrackCount}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"特效 {runtimeData.VfxEvents.Count} | 音效 {runtimeData.SfxEvents.Count} | 打击感 {runtimeData.HitFeelEvents.Count}",
                EditorStyles.miniLabel);

            if (runtimeData.TrackCount == 0)
            {
                EditorGUILayout.HelpBox("当前动作还没有任何 Timeline 轨道", MessageType.Info);
                return;
            }

            foreach (var trackSummary in runtimeData.TrackSummaries)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{trackSummary.TrackType}: {trackSummary.TrackName}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Clip 数量: {trackSummary.ClipCount}", EditorStyles.miniLabel);
                if (trackSummary.ClipCount > 0)
                {
                    EditorGUILayout.LabelField(
                        $"时间范围: {trackSummary.StartTime:F2}s ~ {trackSummary.EndTime:F2}s",
                        EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
            }
        }

        private static List<string> GetActionNames()
        {
            var listWindows = Resources.FindObjectsOfTypeAll<ActionListWindow>();
            if (listWindows.Length > 0 && listWindows[0].ActionList != null)
            {
                return listWindows[0].ActionList.actions
                    .Where(a => a != null)
                    .Select(a => a.name)
                    .ToList();
            }

            var guids = AssetDatabase.FindAssets("t:ActionListSO");
            var allNames = new HashSet<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var actionList = AssetDatabase.LoadAssetAtPath<ActionListSO>(path);
                if (actionList == null) continue;
                foreach (var action in actionList.actions)
                {
                    if (action != null)
                        allNames.Add(action.name);
                }
            }
            return allNames.ToList();
        }

    }

    /// <summary>
    /// 动作预览工具 - 管理临时预览对象的生命周期
    /// 支持使用指定的场景对象（带 Animator）或自动创建临时对象
    /// 在 Timeline 关闭、进入 PlayMode、切换场景时自动清理临时对象
    /// </summary>
    public static class ActionPreviewUtility
    {
        private const string PreviewName = "[ActionPreview]";
        private static GameObject _previewGo;
        private static bool _registered;
        private static bool _usingExternalTarget;
        private static GameObject _externalTarget;
        private static ActionSO _currentAction;
        private static int _lastTrackCount;

        public static bool IsPreviewActive => _previewGo != null || _usingExternalTarget;

        /// <summary>
        /// 打开 Timeline 预览。如果提供了 targetObject，直接使用它（需有 Animator）；
        /// 否则创建临时预览对象。
        /// </summary>
        public static void OpenPreview(ActionSO action, GameObject targetObject = null)
        {
            _currentAction = action;

            if (targetObject != null)
            {
                // 使用指定的场景对象
                var director = targetObject.GetComponent<PlayableDirector>();
                if (director == null)
                    director = targetObject.AddComponent<PlayableDirector>();

                director.playableAsset = action;

                // 自动将所有 AnimationTrack 绑定到目标对象的 Animator
                var animator = targetObject.GetComponent<Animator>();
                if (animator != null)
                {
                    foreach (var track in action.GetOutputTracks())
                    {
                        if (track is AnimationTrack)
                            director.SetGenericBinding(track, animator);
                    }
                }

                _usingExternalTarget = true;
                _externalTarget = targetObject;
                _lastTrackCount = action.GetOutputTracks().Count();

                Selection.activeGameObject = targetObject;
                EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");

                RegisterCleanupCallbacks();
                return;
            }

            // 无目标对象，创建临时预览对象
            if (_previewGo == null)
            {
                _previewGo = new GameObject(PreviewName)
                {
                    hideFlags = HideFlags.DontSave
                };
                _previewGo.AddComponent<Animator>();
                _previewGo.AddComponent<UnityEngine.Playables.PlayableDirector>();
            }

            var previewDirector = _previewGo.GetComponent<UnityEngine.Playables.PlayableDirector>();
            previewDirector.playableAsset = action;
            _lastTrackCount = action.GetOutputTracks().Count();

            _usingExternalTarget = false;
            _externalTarget = null;
            Selection.activeGameObject = _previewGo;
            EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");

            RegisterCleanupCallbacks();
        }

        public static void CleanupPreview()
        {
            if (_previewGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_previewGo);
                _previewGo = null;
            }

            _usingExternalTarget = false;
            _externalTarget = null;
            _currentAction = null;
            _lastTrackCount = 0;
            UnregisterCleanupCallbacks();
        }

        private static void RegisterCleanupCallbacks()
        {
            if (_registered) return;
            _registered = true;

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorApplication.update += CheckTimelineWindow;
        }

        private static void UnregisterCleanupCallbacks()
        {
            if (!_registered) return;
            _registered = false;

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            EditorApplication.update -= CheckTimelineWindow;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                CleanupPreview();
        }

        private static void OnSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene)
        {
            CleanupPreview();
        }

        private static void CheckTimelineWindow()
        {
            if (_previewGo == null && !_usingExternalTarget)
            {
                UnregisterCleanupCallbacks();
                return;
            }

            // Timeline 窗口关闭时自动清理
            if (!HasOpenTimelineWindow())
            {
                CleanupPreview();
                return;
            }

            // 自动绑定新添加的 AnimationTrack 到 Animator
            AutoBindNewTracks();
        }

        private static void AutoBindNewTracks()
        {
            if (_currentAction == null) return;

            var targetGo = _usingExternalTarget ? _externalTarget : _previewGo;
            if (targetGo == null) return;

            var director = targetGo.GetComponent<PlayableDirector>();
            var animator = targetGo.GetComponent<Animator>();
            if (director == null || animator == null) return;

            int currentCount = _currentAction.GetOutputTracks().Count();
            if (currentCount == _lastTrackCount) return;

            _lastTrackCount = currentCount;

            foreach (var track in _currentAction.GetOutputTracks())
            {
                if (track is AnimationTrack && director.GetGenericBinding(track) == null)
                    director.SetGenericBinding(track, animator);
            }
        }

        private static bool HasOpenTimelineWindow()
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var w in windows)
            {
                if (w.GetType().FullName == "UnityEditor.Timeline.TimelineWindow")
                    return true;
            }
            return false;
        }
    }
}
