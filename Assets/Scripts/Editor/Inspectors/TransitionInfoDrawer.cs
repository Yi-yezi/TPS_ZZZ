using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SkillSystem.Editor
{
    /// <summary>
    /// TransitionInfo 及其子类的自定义 PropertyDrawer
    /// 将 targetActionName 从文本输入改为从 ActionListSO 中选择下拉菜单
    /// </summary>
    [CustomPropertyDrawer(typeof(TransitionInfo), true)]
    public class TransitionInfoDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // targetActionName - 用下拉菜单
                var targetProp = property.FindPropertyRelative("targetActionName");
                var lineRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                DrawActionNamePopup(lineRect, targetProp, "目标动作");
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // 遍历其余属性（fadeDuration, command, phase, signalName 等）
                var iter = property.Copy();
                var endProp = property.Copy();
                endProp.Next(false); // 指向下一个同级属性

                if (iter.Next(true)) // 进入子属性
                {
                    do
                    {
                        if (SerializedProperty.EqualContents(iter, endProp)) break;
                        if (iter.name == "targetActionName") continue; // 已经画过了

                        lineRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                        EditorGUI.PropertyField(lineRect, iter, true);
                        y += EditorGUI.GetPropertyHeight(iter, true) + EditorGUIUtility.standardVerticalSpacing;
                    } while (iter.Next(false));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // targetActionName 行
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // 其余属性
            var iter = property.Copy();
            var endProp = property.Copy();
            endProp.Next(false);

            if (iter.Next(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iter, endProp)) break;
                    if (iter.name == "targetActionName") continue;
                    height += EditorGUI.GetPropertyHeight(iter, true) + EditorGUIUtility.standardVerticalSpacing;
                } while (iter.Next(false));
            }

            return height;
        }

        private static void DrawActionNamePopup(Rect rect, SerializedProperty targetProp, string label)
        {
            var actionNames = GetActionNames();

            if (actionNames.Count == 0)
            {
                // 没有找到 ActionListSO，回退到文本输入
                EditorGUI.PropertyField(rect, targetProp, new GUIContent(label));
                return;
            }

            // 构建选项列表："(无)" + 所有动作名
            var options = new List<string> { "(无)" };
            options.AddRange(actionNames);

            string currentValue = targetProp.stringValue ?? "";
            int selectedIndex = 0;
            if (!string.IsNullOrEmpty(currentValue))
            {
                int foundIndex = actionNames.IndexOf(currentValue);
                if (foundIndex >= 0)
                    selectedIndex = foundIndex + 1; // +1 因为有 "(无)"
                else
                    selectedIndex = -1; // 当前值不在列表中
            }

            // 如果当前值不在列表中，额外显示它
            if (selectedIndex < 0)
            {
                options.Insert(1, $"? {currentValue}");
                selectedIndex = 1;
            }

            var displayOptions = options.Select(o => new GUIContent(o)).ToArray();

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(rect, new GUIContent(label), selectedIndex, displayOptions);
            if (EditorGUI.EndChangeCheck())
            {
                if (newIndex == 0)
                    targetProp.stringValue = "";
                else
                {
                    string chosen = options[newIndex];
                    // 去掉 "? " 前缀（如果是未识别的旧值）
                    if (chosen.StartsWith("? "))
                        chosen = chosen[2..];
                    targetProp.stringValue = chosen;
                }
            }
        }

        private static List<string> GetActionNames()
        {
            // 优先从 ActionListWindow 获取当前 ActionListSO
            var listWindows = Resources.FindObjectsOfTypeAll<ActionListWindow>();
            if (listWindows.Length > 0 && listWindows[0].ActionList != null)
            {
                return listWindows[0].ActionList.actions
                    .Where(a => a != null)
                    .Select(a => a.name)
                    .ToList();
            }

            // 回退：搜索项目中所有 ActionListSO
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
}
