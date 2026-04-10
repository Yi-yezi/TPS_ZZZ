using System.Collections.Generic;
using UnityEngine;

namespace SkillSystem
{
    /// <summary>
    /// 动作列表 - 一个角色的所有动作
    /// 支持引用已有的独立 ActionSO 资源，也支持新建动作
    /// </summary>
    [CreateAssetMenu(fileName = "ActionList", menuName = "ActionSystem/ActionList")]
    public class ActionListSO : ScriptableObject
    {
        [Header("角色名称")]
        public string characterName;

        [Header("动作名称前缀（用于区分不同角色的文件）")]
        public string namePrefix;

        [Header("动作列表")]
        public List<ActionSO> actions = new();

        /// <summary>
        /// 添加已有的独立 ActionSO 资源到列表（不会复制，只是引用）
        /// </summary>
        public void AddExistingAction(ActionSO action)
        {
            if (action == null || actions.Contains(action)) return;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "添加已有动作");
#endif
            actions.Add(action);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 从列表移除动作引用（不删除资源文件）
        /// </summary>
        public void RemoveActionReference(ActionSO action)
        {
            if (action == null) return;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "移除动作引用");
#endif
            actions.Remove(action);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
