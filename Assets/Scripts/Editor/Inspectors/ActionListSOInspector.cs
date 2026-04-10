using UnityEditor;
using UnityEngine;

namespace SkillSystem.Editor
{
    /// <summary>
    /// ActionListSO 自定义 Inspector
    /// 支持新建动作和添加已有动作
    /// </summary>
    [CustomEditor(typeof(ActionListSO))]
    public class ActionListSOInspector : UnityEditor.Editor
    {
        private ActionSO _dragDropAction;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("characterName"), new GUIContent("角色名称"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("namePrefix"), new GUIContent("名称前缀"));

            EditorGUILayout.Space(8);

            var actionsProp = serializedObject.FindProperty("actions");
            EditorGUILayout.PropertyField(actionsProp, new GUIContent($"动作列表 ({actionsProp.arraySize})"), true);

            EditorGUILayout.Space(4);

            // 添加已有动作的拖拽区域
            EditorGUILayout.LabelField("添加已有动作", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            _dragDropAction = (ActionSO)EditorGUILayout.ObjectField(
                "拖拽动作到此处", _dragDropAction, typeof(ActionSO), false);
            if (EditorGUI.EndChangeCheck() && _dragDropAction != null)
            {
                var list = (ActionListSO)target;
                if (!list.actions.Contains(_dragDropAction))
                {
                    list.AddExistingAction(_dragDropAction);
                    EditorUtility.SetDirty(list);
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "该动作已在列表中", "确定");
                }
                _dragDropAction = null;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("在编辑器中打开", GUILayout.Width(120)))
            {
                ActionListWindow.OpenEditor();
            }

            if (GUILayout.Button("+ 新建动作", GUILayout.Width(100)))
            {
                // 在项目中创建独立的 ActionSO 资源
                string path = EditorUtility.SaveFilePanelInProject(
                    "新建动作", "NewAction", "asset",
                    "选择保存位置");
                if (!string.IsNullOrEmpty(path))
                {
                    var action = CreateInstance<ActionSO>();
                    AssetDatabase.CreateAsset(action, path);
                    AssetDatabase.SaveAssets();

                    var list = (ActionListSO)target;
                    list.AddExistingAction(action);
                    EditorUtility.SetDirty(list);
                }
            }

            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
