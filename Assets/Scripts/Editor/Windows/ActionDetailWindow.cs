using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace SkillSystem.Editor
{
    /// <summary>
    /// 动作详情窗口 - 显示选中动作的 Inspector + 转移关系图
    /// 提供 Timeline 打开按钮，自动绑定 Animator
    /// </summary>
    public class ActionDetailWindow : EditorWindow
    {
        private ActionSO _action;
        private ActionListSO _actionList;
        private UnityEditor.Editor _actionEditor;

        // 布局
        private float _transitionPanelHeight = 250f;
        private bool _resizingTransitionPanel;
        private const float RESIZE_HANDLE_WIDTH = 4f;
        private const float TOOLBAR_HEIGHT = 22f;
        private const float MIN_PANEL_SIZE = 150f;

        // 滚动
        private Vector2 _detailScroll;
        private Vector2 _transitionScroll;

        [MenuItem("ActionSystem/动作详情")]
        public static ActionDetailWindow ShowWindow()
        {
            var window = GetWindow<ActionDetailWindow>("动作详情");
            window.minSize = new Vector2(400, 400);
            return window;
        }

        public static ActionDetailWindow FindInstance()
        {
            var windows = Resources.FindObjectsOfTypeAll<ActionDetailWindow>();
            return windows.Length > 0 ? windows[0] : null;
        }

        public void SetAction(ActionSO action, ActionListSO actionList = null)
        {
            _action = action;
            if (actionList != null) _actionList = actionList;
            ClearEditor();
            if (_action != null)
                _actionEditor = UnityEditor.Editor.CreateEditor(_action);
            Repaint();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            ClearEditor();
        }

        private void ClearEditor()
        {
            if (_actionEditor != null)
            {
                DestroyImmediate(_actionEditor);
                _actionEditor = null;
            }
        }

        private void OnGUI()
        {
            HandleResizing();

            var contentRect = new Rect(0, 0, position.width, position.height);

            // 上方：详情面板
            float detailHeight = contentRect.height - _transitionPanelHeight - RESIZE_HANDLE_WIDTH;
            var detailRect = new Rect(0, 0, contentRect.width, detailHeight);
            DrawDetailPanel(detailRect);

            // 分割线
            DrawHorizontalResizeHandle(0, detailRect.yMax, contentRect.width);

            // 下方：转移关系图
            var transitionRect = new Rect(0, detailRect.yMax + RESIZE_HANDLE_WIDTH,
                contentRect.width, _transitionPanelHeight);
            DrawTransitionPanel(transitionRect);
        }

        #region Detail Panel

        private void DrawDetailPanel(Rect rect)
        {
            GUILayout.BeginArea(rect);
            EditorGUI.DrawRect(new Rect(0, 0, rect.width, rect.height),
                new Color(0.2f, 0.2f, 0.2f, 1f));

            // 工具栏
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (_action != null)
                GUILayout.Label(_action.name, ActionEditorStyles.HeaderLabel);
            else
                GUILayout.Label("动作详情", ActionEditorStyles.HeaderLabel);

            using (new EditorGUI.DisabledScope(_action == null))
            {
                if (GUILayout.Button("在 Timeline 中打开", EditorStyles.toolbarButton, GUILayout.Width(110)))
                {
                    // 从列表窗口获取目标 Animator
                    var listWindow = GetListWindow();
                    var targetGo = listWindow?.TargetAnimator != null
                        ? listWindow.TargetAnimator.gameObject
                        : null;
                    ActionPreviewUtility.OpenPreview(_action, targetGo);
                }
            }

            GUILayout.EndHorizontal();

            // Inspector 内容
            if (_action != null && _actionEditor != null)
            {
                _detailScroll = GUILayout.BeginScrollView(_detailScroll);
                _actionEditor.OnInspectorGUI();
                GUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("请从动作列表窗口选择一个动作，或双击动作打开", MessageType.Info);
            }

            GUILayout.EndArea();
        }

        private static ActionListWindow GetListWindow()
        {
            var windows = Resources.FindObjectsOfTypeAll<ActionListWindow>();
            return windows.Length > 0 ? windows[0] : null;
        }

        #endregion

        #region Transition Panel

        private void DrawTransitionPanel(Rect rect)
        {
            GUILayout.BeginArea(rect);
            EditorGUI.DrawRect(new Rect(0, 0, rect.width, rect.height), ActionEditorStyles.PanelBackground);

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("转移关系图", ActionEditorStyles.HeaderLabel);
            GUILayout.EndHorizontal();

            if (_action != null)
            {
                _transitionScroll = GUILayout.BeginScrollView(_transitionScroll);
                DrawTransitionGraph(new Rect(0, 0, rect.width, rect.height - TOOLBAR_HEIGHT));
                GUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("选择一个动作以查看其转移关系", MessageType.Info);
            }

            GUILayout.EndArea();
        }

        private void DrawTransitionGraph(Rect rect)
        {
            if (_action == null) return;

            float y = 4f;
            float lineHeight = EditorGUIUtility.singleLineHeight + 2f;

            // 完成转移
            if (!string.IsNullOrEmpty(_action.finishTransition?.targetActionName))
            {
                var info = _action.finishTransition;
                DrawTransitionEntry(ref y, lineHeight,
                    $"[完成] → {info.targetActionName}",
                    $"过渡: {info.fadeDuration:F2}s",
                    new Color(0.4f, 0.7f, 1f));
            }

            // 指令转移
            if (_action.commandTransitions != null)
            {
                foreach (var entry in _action.commandTransitions)
                {
                    var ct = entry?.transition;
                    if (ct == null) continue;
                    DrawTransitionEntry(ref y, lineHeight,
                        $"[指令] {ct.command}.{ct.phase} → {ct.targetActionName}",
                        $"时间窗口: {entry.startTime:F2}s ~ {entry.startTime + entry.duration:F2}s, 缓冲: {entry.inputBufferDuration:F2}s",
                        ActionEditorStyles.TransitionArrowColor);
                }
            }

            // 信号转移
            if (_action.signalTransitions != null)
            {
                foreach (var st in _action.signalTransitions)
                {
                    if (st == null) continue;
                    DrawTransitionEntry(ref y, lineHeight,
                        $"[信号] \"{st.signalName}\" → {st.targetActionName}",
                        $"过渡: {st.fadeDuration:F2}s",
                        new Color(0.6f, 0.9f, 0.6f));
                }
            }

            // 入度
            DrawIncomingTransitions(ref y, lineHeight);

            if (y <= 4f)
            {
                EditorGUILayout.LabelField("当前动作没有转移关系", EditorStyles.centeredGreyMiniLabel);
            }

            GUILayoutUtility.GetRect(1, Mathf.Max(y, lineHeight * 2));
        }

        private void DrawTransitionEntry(ref float y, float lineHeight, string title, string detail, Color color)
        {
            var iconRect = new Rect(8, y, 8, lineHeight);
            EditorGUI.DrawRect(iconRect, color);

            GUI.Label(new Rect(22, y, 400, lineHeight), title, EditorStyles.label);
            y += lineHeight;

            GUI.Label(new Rect(30, y, 400, lineHeight), detail, EditorStyles.miniLabel);
            y += lineHeight + 4f;
        }

        private void DrawIncomingTransitions(ref float y, float lineHeight)
        {
            if (_actionList == null || _action == null) return;

            string currentName = _action.name;
            bool hasIncoming = false;

            foreach (var action in _actionList.actions)
            {
                if (action == null || action == _action) continue;

                if (action.finishTransition?.targetActionName == currentName)
                {
                    DrawIncomingHeader(ref y, lineHeight, ref hasIncoming);
                    DrawTransitionEntry(ref y, lineHeight,
                        $"{action.name} [完成] → 此动作", "",
                        new Color(0.5f, 0.5f, 0.8f));
                }

                if (action.commandTransitions != null)
                {
                    foreach (var entry in action.commandTransitions)
                    {
                        var ct = entry?.transition;
                        if (ct?.targetActionName != currentName) continue;
                        DrawIncomingHeader(ref y, lineHeight, ref hasIncoming);
                        DrawTransitionEntry(ref y, lineHeight,
                            $"{action.name} [{ct.command}.{ct.phase}] → 此动作", "",
                            new Color(0.8f, 0.7f, 0.4f));
                    }
                }

                if (action.signalTransitions != null)
                {
                    foreach (var st in action.signalTransitions)
                    {
                        if (st?.targetActionName != currentName) continue;
                        DrawIncomingHeader(ref y, lineHeight, ref hasIncoming);
                        DrawTransitionEntry(ref y, lineHeight,
                            $"{action.name} [\"{st.signalName}\"] → 此动作", "",
                            new Color(0.6f, 0.8f, 0.6f));
                    }
                }
            }
        }

        private void DrawIncomingHeader(ref float y, float lineHeight, ref bool hasIncoming)
        {
            if (hasIncoming) return;
            y += 8f;
            EditorGUI.DrawRect(new Rect(8, y, 200, 1), Color.gray);
            y += 6f;
            GUI.Label(new Rect(8, y, 200, lineHeight), "▼ 来源转移", EditorStyles.boldLabel);
            y += lineHeight + 2f;
            hasIncoming = true;
        }

        #endregion

        #region Resize

        private void DrawHorizontalResizeHandle(float x, float y, float width)
        {
            var handleRect = new Rect(x, y, width, RESIZE_HANDLE_WIDTH);
            EditorGUI.DrawRect(handleRect, new Color(0.15f, 0.15f, 0.15f, 1f));
            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeVertical);
        }

        private void HandleResizing()
        {
            var e = Event.current;
            float hHandleY = position.height - _transitionPanelHeight - RESIZE_HANDLE_WIDTH;
            var hHandleRect = new Rect(0, hHandleY, position.width, RESIZE_HANDLE_WIDTH);

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && hHandleRect.Contains(e.mousePosition):
                    _resizingTransitionPanel = true;
                    e.Use();
                    break;

                case EventType.MouseDrag when _resizingTransitionPanel:
                    _transitionPanelHeight = Mathf.Clamp(position.height - e.mousePosition.y,
                        MIN_PANEL_SIZE, position.height - MIN_PANEL_SIZE);
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseUp:
                    _resizingTransitionPanel = false;
                    break;
            }
        }

        #endregion
    }
}
