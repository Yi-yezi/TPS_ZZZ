using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SkillSystem.Editor
{
    /// <summary>
    /// 转移关系图视图 - 以节点图形式可视化动作间的转移关系
    /// </summary>
    public class TransitionGraphView : EditorWindow
    {
        private ActionListSO _actionList;
        private ActionSO _focusedAction;

        private Vector2 _scrollPos;
        private float _zoom = 1f;

        // 节点布局
        private readonly Dictionary<ActionSO, Rect> _nodeRects = new();
        private bool _layoutDirty = true;

        // 拖拽
        private ActionSO _draggingNode;
        private Vector2 _dragOffset;

        // 节点样式参数
        private const float NODE_WIDTH = 140f;
        private const float NODE_HEIGHT = 40f;
        private const float NODE_SPACING_X = 200f;
        private const float NODE_SPACING_Y = 80f;
        private const float PADDING = 50f;
        private const float PORT_MARGIN = 16f; // 端口距节点边缘的最小留白

        [MenuItem("ActionSystem/转移关系图")]
        public static TransitionGraphView ShowWindow()
        {
            var window = GetWindow<TransitionGraphView>("转移关系图");
            window.minSize = new Vector2(600, 400);
            return window;
        }

        public void SetActionList(ActionListSO list, ActionSO focused = null)
        {
            _actionList = list;
            _focusedAction = focused;
            _layoutDirty = true;
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_actionList == null)
            {
                EditorGUILayout.HelpBox("请选择一个 ActionListSO", MessageType.Info);
                return;
            }

            if (_layoutDirty)
            {
                AutoLayout();
                _layoutDirty = false;
            }

            HandleEvents();
            DrawGraph();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            _actionList = (ActionListSO)EditorGUILayout.ObjectField(
                _actionList, typeof(ActionListSO), false, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck())
            {
                _layoutDirty = true;
                _focusedAction = null;
            }

            if (GUILayout.Button("自动布局", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _layoutDirty = true;
            }

            GUILayout.Label($"缩放: {_zoom:F1}x", EditorStyles.miniLabel, GUILayout.Width(60));

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void AutoLayout()
        {
            _nodeRects.Clear();
            if (_actionList == null) return;

            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(_actionList.actions.Count)));
            int i = 0;

            foreach (var action in _actionList.actions)
            {
                if (action == null) continue;

                int row = i / cols;
                int col = i % cols;

                float x = PADDING + col * NODE_SPACING_X;
                float y = PADDING + row * NODE_SPACING_Y;

                _nodeRects[action] = new Rect(x, y, NODE_WIDTH, NODE_HEIGHT);
                i++;
            }
        }

        private void DrawGraph()
        {
            var graphRect = new Rect(0, 20, position.width, position.height - 20);
            EditorGUI.DrawRect(graphRect, new Color(0.18f, 0.18f, 0.18f, 1f));

            // 绘制网格
            DrawGrid(graphRect);

            // 开始裁剪
            GUI.BeginClip(graphRect);

            var offset = -_scrollPos;

            // 第1遍：收集所有连线
            var connections = new List<(ActionSO source, string targetName, Color color, bool dashed, string label)>();
            foreach (var (action, rect) in _nodeRects)
            {
                if (action == null) continue;
                CollectConnections(action, connections);
            }

            // 统计每个节点的出线数和入线数，分配端口位置
            var outIndex = new Dictionary<ActionSO, int>();   // 当前已分配的出端口序号
            var outTotal = new Dictionary<ActionSO, int>();   // 总出线数
            var inIndex = new Dictionary<string, int>();      // 当前已分配的入端口序号
            var inTotal = new Dictionary<string, int>();      // 总入线数

            foreach (var conn in connections)
            {
                outTotal[conn.source] = outTotal.GetValueOrDefault(conn.source, 0) + 1;
                inTotal[conn.targetName] = inTotal.GetValueOrDefault(conn.targetName, 0) + 1;
            }

            // 逐条绘制，每条线获得独立的出端口和入端口
            foreach (var conn in connections)
            {
                int oi = outIndex.GetValueOrDefault(conn.source, 0);
                outIndex[conn.source] = oi + 1;
                int oTotal = outTotal[conn.source];

                int ii = inIndex.GetValueOrDefault(conn.targetName, 0);
                inIndex[conn.targetName] = ii + 1;
                int iTotal = inTotal[conn.targetName];

                DrawConnection(conn.source, conn.targetName, offset, conn.color,
                    oi, oTotal, ii, iTotal, conn.dashed, conn.label);
            }

            // 第2遍：绘制节点
            foreach (var (action, rect) in _nodeRects)
            {
                if (action == null) continue;
                DrawNode(action, new Rect(rect.position + offset, rect.size));
            }

            GUI.EndClip();
        }

        private void DrawGrid(Rect rect)
        {
            float gridSize = 50f * _zoom;
            float startX = -_scrollPos.x % gridSize;
            float startY = -_scrollPos.y % gridSize + rect.y;

            Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            for (float x = startX; x < rect.width; x += gridSize)
            {
                Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.yMax));
            }
            for (float y = startY; y < rect.yMax; y += gridSize)
            {
                Handles.DrawLine(new Vector3(0, y), new Vector3(rect.width, y));
            }
            Handles.color = Color.white;
        }

        private void DrawNode(ActionSO action, Rect rect)
        {
            bool isFocused = action == _focusedAction;

            // 节点背景
            Color bgColor = isFocused
                ? new Color(0.2f, 0.45f, 0.7f, 1f)
                : new Color(0.3f, 0.3f, 0.3f, 1f);
            EditorGUI.DrawRect(rect, bgColor);

            // 边框
            Color borderColor = isFocused ? new Color(0.4f, 0.7f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            DrawRectBorder(rect, borderColor);

            // 标题
            var titleStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = isFocused ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = Color.white },
                fontSize = 11,
                clipping = TextClipping.Clip,
            };

            GUI.Label(rect, action.name, titleStyle);

            // 点击选择
            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                _focusedAction = action;
                Selection.activeObject = action;
                Event.current.Use();
                Repaint();
            }
        }

        private void CollectConnections(ActionSO action,
            List<(ActionSO source, string targetName, Color color, bool dashed, string label)> connections)
        {
            // 完成转移
            if (!string.IsNullOrEmpty(action.finishTransition?.targetActionName))
            {
                connections.Add((action, action.finishTransition.targetActionName,
                    new Color(0.4f, 0.7f, 1f, 0.8f), false, "完成"));
            }

            // 指令转移
            if (action.commandTransitions != null)
            {
                foreach (var entry in action.commandTransitions)
                {
                    var ct = entry?.transition;
                    if (ct == null || string.IsNullOrEmpty(ct.targetActionName)) continue;
                    string label = $"指令:{ct.command}({ct.phase})";
                    connections.Add((action, ct.targetActionName,
                        ActionEditorStyles.TransitionArrowColor, false, label));
                }
            }

            // 信号转移
            if (action.signalTransitions != null)
            {
                foreach (var st in action.signalTransitions)
                {
                    if (st == null || string.IsNullOrEmpty(st.targetActionName)) continue;
                    string label = $"信号:{st.signalName}";
                    connections.Add((action, st.targetActionName,
                        new Color(0.5f, 0.9f, 0.5f, 0.8f), false, label));
                }
            }
        }

        /// <summary>
        /// 计算节点边缘上第 index 个端口（共 total 个）的 X 坐标，均匀分布在 [left+margin, right-margin] 区间
        /// </summary>
        private static float GetPortX(Rect nodeRect, int index, int total)
        {
            float left = nodeRect.x + PORT_MARGIN;
            float right = nodeRect.xMax - PORT_MARGIN;
            if (total <= 1) return (left + right) * 0.5f;
            return Mathf.Lerp(left, right, (float)index / (total - 1));
        }

        private void DrawConnection(ActionSO sourceAction, string targetName, Vector2 offset,
            Color color, int outIdx, int outTotal, int inIdx, int inTotal,
            bool dashed = false, string label = null)
        {
            // 查找目标节点
            ActionSO targetAction = null;
            foreach (var (action, rect) in _nodeRects)
            {
                if (action != null && action.name == targetName)
                {
                    targetAction = action;
                    break;
                }
            }

            if (targetAction == null || !_nodeRects.ContainsKey(targetAction)) return;
            if (!_nodeRects.ContainsKey(sourceAction)) return;

            var sourceRect = _nodeRects[sourceAction];
            var targetRect = _nodeRects[targetAction];

            bool targetAbove = targetRect.center.y < sourceRect.center.y;

            // 出端口：目标在下方从底边出，在上方从顶边出
            float startX = GetPortX(sourceRect, outIdx, outTotal) + offset.x;
            float startY = (targetAbove ? sourceRect.y : sourceRect.yMax) + offset.y;
            var startPos = new Vector2(startX, startY);

            // 入端口：目标在下方从顶边入，在上方从底边入
            float endX = GetPortX(targetRect, inIdx, inTotal) + offset.x;
            float endY = (targetAbove ? targetRect.yMax : targetRect.y) + offset.y;
            var endPos = new Vector2(endX, endY);

            // 贝塞尔曲线
            float tangent = Mathf.Abs(endPos.y - startPos.y) * 0.5f + 20f;
            Vector2 startTangent, endTangent;
            if (targetAbove)
            {
                startTangent = startPos - Vector2.up * tangent;
                endTangent = endPos + Vector2.up * tangent;
            }
            else
            {
                startTangent = startPos + Vector2.up * tangent;
                endTangent = endPos - Vector2.up * tangent;
            }

            Handles.DrawBezier(startPos, endPos, startTangent, endTangent, color, null, 2f);

            // 箭头
            Vector2 dir = (endPos - endTangent).normalized;
            Vector2 arrowLeft = endPos - dir * 8 + new Vector2(-dir.y, dir.x) * 5;
            Vector2 arrowRight = endPos - dir * 8 + new Vector2(dir.y, -dir.x) * 5;

            Handles.color = color;
            Handles.DrawAAConvexPolygon(endPos, arrowLeft, arrowRight);
            Handles.color = Color.white;

            // 绘制连线标签
            if (!string.IsNullOrEmpty(label))
            {
                var mid = GetBezierPoint(0.5f, startPos, startTangent, endTangent, endPos);
                var labelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = color },
                    fontSize = 10,
                    alignment = TextAnchor.MiddleCenter,
                };
                var labelContent = new GUIContent(label);
                var labelSize = labelStyle.CalcSize(labelContent);
                var labelRect = new Rect(mid.x - labelSize.x * 0.5f, mid.y - labelSize.y * 0.5f,
                    labelSize.x, labelSize.y);
                EditorGUI.DrawRect(labelRect, new Color(0.15f, 0.15f, 0.15f, 0.85f));
                GUI.Label(labelRect, labelContent, labelStyle);
            }
        }

        private static Vector2 GetBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        private static void DrawRectBorder(Rect rect, Color color, float thickness = 1f)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void HandleEvents()
        {
            var e = Event.current;

            switch (e.type)
            {
                case EventType.ScrollWheel:
                    _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.05f, 0.3f, 2f);
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseDrag when e.button == 2:
                    _scrollPos -= e.delta;
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseDown when e.button == 0:
                    // 检查是否点击了节点（用于拖拽）
                    var graphOffset = -_scrollPos;
                    foreach (var (action, rect) in _nodeRects)
                    {
                        var screenRect = new Rect(rect.position + graphOffset, rect.size);
                        if (screenRect.Contains(e.mousePosition))
                        {
                            _draggingNode = action;
                            _dragOffset = e.mousePosition - rect.position - graphOffset;
                            break;
                        }
                    }
                    break;

                case EventType.MouseDrag when e.button == 0 && _draggingNode != null:
                    if (_nodeRects.ContainsKey(_draggingNode))
                    {
                        var r = _nodeRects[_draggingNode];
                        r.position = e.mousePosition + _scrollPos - _dragOffset;
                        _nodeRects[_draggingNode] = r;
                    }
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseUp when e.button == 0:
                    _draggingNode = null;
                    break;
            }
        }
    }
}
