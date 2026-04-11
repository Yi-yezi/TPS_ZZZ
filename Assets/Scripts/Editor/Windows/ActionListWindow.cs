using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SkillSystem.Editor
{
    /// <summary>
    /// 动作列表窗口 - 管理 ActionListSO 中的动作
    /// 选中动作时通知 ActionDetailWindow 显示详情
    /// </summary>
    public class ActionListWindow : EditorWindow
    {
        // 数据
        private ActionListSO _actionList;
        private ActionSO _selectedAction;

        // 目标 Animator（用于 Timeline 预览）
        private Animator _targetAnimator;

        // 搜索
        private string _searchQuery = "";
        private readonly List<ActionSO> _filteredActions = new();

        // 滚动
        private Vector2 _actionListScroll;

        public ActionListSO ActionList => _actionList;
        public ActionSO SelectedAction => _selectedAction;
        public Animator TargetAnimator => _targetAnimator;

        [MenuItem("ActionSystem/动作编辑器")]
        public static void OpenEditor()
        {
            ShowWindow();
            ActionDetailWindow.ShowWindow();
        }

        [MenuItem("ActionSystem/动作列表")]
        public static ActionListWindow ShowWindow()
        {
            var window = GetWindow<ActionListWindow>("动作列表");
            window.minSize = new Vector2(300, 400);
            return window;
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
        }

        public void SelectAction(ActionSO action)
        {
            if (_selectedAction == action) return;
            _selectedAction = action;

            // 通知详情窗口
            var detailWindow = ActionDetailWindow.FindInstance();
            if (detailWindow != null)
            {
                detailWindow.SetAction(_selectedAction, _actionList);
            }

            // 有目标 Animator 时自动打开/切换 Timeline 预览
            if (_selectedAction != null && _targetAnimator != null)
            {
                ActionPreviewUtility.OpenPreview(_selectedAction, _targetAnimator.gameObject);
            }

            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawActionList();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            _actionList = (ActionListSO)EditorGUILayout.ObjectField(
                _actionList, typeof(ActionListSO), false,
                GUILayout.Width(180));
            if (EditorGUI.EndChangeCheck())
            {
                SelectAction(null);
            }

            GUILayout.FlexibleSpace();

            if (_actionList != null)
            {
                GUILayout.Label($"{_actionList.actions.Count}", EditorStyles.miniLabel);
            }

            GUILayout.EndHorizontal();

            // 第二行：目标 Animator
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("目标:", EditorStyles.miniLabel, GUILayout.Width(32));
            _targetAnimator = (Animator)EditorGUILayout.ObjectField(
                _targetAnimator, typeof(Animator), true);
            GUILayout.EndHorizontal();
        }

        private void DrawActionList()
        {
            // 标题栏
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("动作列表", ActionEditorStyles.HeaderLabel);

            using (new EditorGUI.DisabledScope(_actionList == null))
            {
                if (GUILayout.Button("▼", EditorStyles.toolbarDropDown, GUILayout.Width(24)))
                {
                    ShowAddActionMenu();
                }
            }
            GUILayout.EndHorizontal();

            // 搜索栏
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            _searchQuery = EditorGUILayout.TextField(_searchQuery, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? EditorStyles.miniButton,
                    GUILayout.Width(18)))
            {
                _searchQuery = "";
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            // 列表
            _actionListScroll = GUILayout.BeginScrollView(_actionListScroll);

            if (_actionList != null)
            {
                FilterActions();
                foreach (var action in _filteredActions)
                {
                    DrawActionListItem(action);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("请在上方选择一个 ActionListSO", MessageType.Info);
            }

            GUILayout.EndScrollView();
        }

        private void FilterActions()
        {
            _filteredActions.Clear();
            if (_actionList == null) return;

            foreach (var action in _actionList.actions)
            {
                if (action == null) continue;
                if (string.IsNullOrEmpty(_searchQuery) ||
                    action.name.ToLower().Contains(_searchQuery.ToLower()))
                {
                    _filteredActions.Add(action);
                }
            }
        }

        private void DrawActionListItem(ActionSO action)
        {
            bool isSelected = action == _selectedAction;
            var style = new GUIStyle(ActionEditorStyles.ListItemNormal);
            ActionEditorStyles.BeginHighlight(style, isSelected);

            GUILayout.BeginHorizontal();

            string label = action.name;

            if (GUILayout.Button(label, style))
            {
                SelectAction(action);

                // 双击打开详情窗口
                if (Event.current.clickCount == 2)
                {
                    var detailWindow = ActionDetailWindow.ShowWindow();
                    detailWindow.SetAction(action, _actionList);
                }
            }

            // 删除按钮
            if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20), GUILayout.Height(18)))
            {
                if (EditorUtility.DisplayDialog("确认移除",
                        $"确定从列表移除动作 \"{action.name}\"？\n（资源文件不会被删除）", "移除", "取消"))
                {
                    if (_selectedAction == action) SelectAction(null);
                    _actionList.RemoveActionReference(action);
                    AssetDatabase.SaveAssets();
                    GUIUtility.ExitGUI();
                }
            }

            ActionEditorStyles.EndHighlight();

            // 右键菜单
            if (Event.current.type == EventType.ContextClick &&
                GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            {
                ShowActionContextMenu(action);
                Event.current.Use();
            }

            GUILayout.EndHorizontal();
        }

        #region Action Management

        private void ShowAddActionMenu()
        {
            var states = CollectLeafStates();
            if (states.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先在上方设置目标 Animator", "确定");
                return;
            }

            var menu = new GenericMenu();
            foreach (var (menuPath, state) in states)
            {
                string stateName = state.name; // 纯 State 名，不含 SubStateMachine 前缀
                bool exists = _actionList.actions.Any(a => a != null && a.name == stateName);
                if (exists)
                {
                    menu.AddDisabledItem(new GUIContent($"{menuPath} (已存在)"));
                }
                else
                {
                    var capturedStateName = stateName;
                    var capturedState = state;
                    menu.AddItem(new GUIContent(menuPath), false,
                        () => CreateActionFromState(capturedStateName, capturedState));
                }
            }
            menu.ShowAsContext();
        }

        private void CreateActionFromState(string stateName, AnimatorState state)
        {
            string prefix = string.IsNullOrEmpty(_actionList.namePrefix)
                ? "" : $"{_actionList.namePrefix}_";
            string defaultName = prefix + stateName;

            string path = EditorUtility.SaveFilePanelInProject(
                "新建动作", defaultName, "asset", "选择保存位置");
            if (string.IsNullOrEmpty(path)) return;

            var action = ScriptableObject.CreateInstance<ActionSO>();
            AssetDatabase.CreateAsset(action, path);

            // 重命名为 stateName（确保 action.name == state name）
            action.name = stateName;
            AssetDatabase.ImportAsset(path);

            // 创建 AnimationTrack 并添加对应动画 Clip
            var animClip = ExtractClipFromMotion(state.motion);
            var track = action.CreateTrack<AnimationTrack>(null, "Animation");
            if (animClip != null)
            {
                // 清除 AnimationClip 自带的事件，避免 "no receiver" 警告
                AnimationUtility.SetAnimationEvents(animClip, System.Array.Empty<AnimationEvent>());

                var timelineClip = track.CreateClip(animClip);
                timelineClip.start = 0;
                timelineClip.displayName = stateName;
            }

            EditorUtility.SetDirty(action);
            _actionList.AddExistingAction(action);
            AssetDatabase.SaveAssets();
            SelectAction(action);
        }

        private void ShowActionContextMenu(ActionSO action)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("在详情窗口打开"), false, () =>
            {
                SelectAction(action);
                var detailWindow = ActionDetailWindow.ShowWindow();
                detailWindow.SetAction(action, _actionList);
            });
            menu.AddItem(new GUIContent("在 Timeline 中打开"), false, () =>
            {
                SelectAction(action);
                ActionPreviewUtility.OpenPreview(action,
                    _targetAnimator != null ? _targetAnimator.gameObject : null);
            });
            menu.AddItem(new GUIContent("复制名称"), false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = action.name;
            });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("从列表移除"), false, () =>
            {
                if (EditorUtility.DisplayDialog("确认移除",
                        $"确定从列表移除动作 \"{action.name}\"？\n（资源文件不会被删除）", "移除", "取消"))
                {
                    if (_selectedAction == action) SelectAction(null);
                    _actionList.RemoveActionReference(action);
                    AssetDatabase.SaveAssets();
                }
            });
            menu.ShowAsContext();
        }

        #endregion

        #region Animator State Helpers

        private List<(string name, AnimatorState state)> CollectLeafStates()
        {
            var results = new List<(string, AnimatorState)>();

            if (_targetAnimator == null || _targetAnimator.runtimeAnimatorController == null)
                return results;

            var controller = _targetAnimator.runtimeAnimatorController as AnimatorController;
            if (controller == null) return results;

            foreach (var layer in controller.layers)
            {
                CollectStatesRecursive(layer.stateMachine, "", results);
            }

            return results;
        }

        private static void CollectStatesRecursive(AnimatorStateMachine sm, string prefix,
            List<(string name, AnimatorState state)> results)
        {
            foreach (var childState in sm.states)
            {
                // name = 纯 State 名（与 ActionNames 常量、Animator.CrossFade 参数一致）
                // prefix 只用于菜单中的分组路径显示
                string menuPath = prefix + childState.state.name;
                results.Add((menuPath, childState.state));
            }

            foreach (var sub in sm.stateMachines)
            {
                CollectStatesRecursive(sub.stateMachine,
                    prefix + sub.stateMachine.name + "/", results);
            }
        }

        /// <summary>
        /// 从 State 的 Motion 中提取 AnimationClip。BlendTree 取最长子 Clip。
        /// </summary>
        private static AnimationClip ExtractClipFromMotion(Motion motion)
        {
            if (motion is AnimationClip clip) return clip;
            if (motion is BlendTree tree) return FindLongestClipInBlendTree(tree);
            return null;
        }

        private static AnimationClip FindLongestClipInBlendTree(BlendTree tree)
        {
            AnimationClip longest = null;
            float maxLength = 0f;

            foreach (var child in tree.children)
            {
                AnimationClip candidate = null;
                if (child.motion is AnimationClip childClip)
                    candidate = childClip;
                else if (child.motion is BlendTree subTree)
                    candidate = FindLongestClipInBlendTree(subTree);

                if (candidate != null && candidate.length > maxLength)
                {
                    maxLength = candidate.length;
                    longest = candidate;
                }
            }

            return longest;
        }

        #endregion
    }
}
