using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SkillSystem.Editor
{
    /// <summary>
    /// VFXClip 自定义 Inspector + Scene 视图中的移动/旋转工具
    /// 选中 VFX clip 后可直接在 Scene 中拖拽调整偏移
    /// </summary>
    [CustomEditor(typeof(VFXClip))]
    public class VFXClipEditor : UnityEditor.Editor
    {
        private VFXClip _clip;
        private SerializedProperty _vfxPrefab;
        private SerializedProperty _parentPath;
        private SerializedProperty _attachToParent;
        private SerializedProperty _positionOffset;
        private SerializedProperty _rotationOffset;

        private void OnEnable()
        {
            _clip = (VFXClip)target;
            _vfxPrefab = serializedObject.FindProperty("vfxPrefab");
            _parentPath = serializedObject.FindProperty("parentPath");
            _attachToParent = serializedObject.FindProperty("attachToParent");
            _positionOffset = serializedObject.FindProperty("positionOffset");
            _rotationOffset = serializedObject.FindProperty("rotationOffset");
            SceneView.duringSceneGui += DrawSceneHandles;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneHandles;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                var script = MonoScript.FromScriptableObject(_clip);
                EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
            }

            EditorGUILayout.Space(2);

            EditorGUILayout.PropertyField(_vfxPrefab);

            EditorGUILayout.Space(4);

            // 参考点选择
            DrawParentPathField();
            EditorGUILayout.PropertyField(_attachToParent,
                new GUIContent("跟随挂载点", "勾选则特效跟随参考点移动，否则仅以其位置生成后固定"));

            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(_positionOffset);
            EditorGUILayout.PropertyField(_rotationOffset);

            serializedObject.ApplyModifiedProperties();

            if (VFXPlayable.ActiveInstances.TryGetValue(_clip, out var instance) && instance != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("可在 Scene 窗口中直接拖动调整位置和旋转", MessageType.Info);
            }
        }

        private void DrawParentPathField()
        {
            var animator = GetTargetAnimator();

            // 拖入 Transform 对象字段
            EditorGUILayout.BeginHorizontal();

            // 解析当前 parentPath 对应的 Transform（用于回显）
            Transform currentRef = null;
            if (animator != null && !string.IsNullOrEmpty(_parentPath.stringValue))
            {
                if (_parentPath.stringValue == ".")
                    currentRef = animator.transform;
                else
                {
                    currentRef = animator.transform.Find(_parentPath.stringValue);
                    if (currentRef == null)
                        currentRef = FindChildRecursive(animator.transform, _parentPath.stringValue);
                }
            }

            EditorGUI.BeginChangeCheck();
            var newRef = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("参考点", "拖入骨骼/子物体，或留空以角色根位置为基准"),
                currentRef, typeof(Transform), true);
            if (EditorGUI.EndChangeCheck())
            {
                if (newRef == null)
                {
                    _parentPath.stringValue = "";
                }
                else if (animator != null && (newRef == animator.transform))
                {
                    _parentPath.stringValue = ".";
                }
                else if (animator != null && newRef.IsChildOf(animator.transform))
                {
                    // 先尝试完整路径，如果名称唯一则用短名
                    string fullPath = GetFullPath(newRef, animator.transform);
                    var byName = FindChildRecursive(animator.transform, newRef.name);
                    _parentPath.stringValue = (byName == newRef) ? newRef.name : fullPath;
                }
                else
                {
                    // 不是目标 Animator 的子物体，仍存名称
                    _parentPath.stringValue = newRef.name;
                }
                serializedObject.ApplyModifiedProperties();
            }

            if (animator != null && GUILayout.Button("▼", GUILayout.Width(24)))
            {
                ShowBoneMenu(animator);
            }
            EditorGUILayout.EndHorizontal();

            // 显示路径 / 状态提示
            if (string.IsNullOrEmpty(_parentPath.stringValue))
            {
                EditorGUILayout.LabelField("  → 世界空间", EditorStyles.miniLabel);
            }
            else if (currentRef != null && animator != null)
            {
                EditorGUILayout.LabelField($"  → {GetFullPath(currentRef, animator.transform)}", EditorStyles.miniLabel);
            }
            else if (!string.IsNullOrEmpty(_parentPath.stringValue))
            {
                EditorGUILayout.HelpBox($"未找到 \"{_parentPath.stringValue}\"", MessageType.Warning);
            }
        }

        private void ShowBoneMenu(Animator animator)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("(世界空间)"), string.IsNullOrEmpty(_parentPath.stringValue), () =>
            {
                _parentPath.stringValue = "";
                serializedObject.ApplyModifiedProperties();
            });
            menu.AddItem(new GUIContent("(根物体)"), _parentPath.stringValue == ".", () =>
            {
                _parentPath.stringValue = ".";
                serializedObject.ApplyModifiedProperties();
            });
            menu.AddSeparator("");

            // 收集所有子 Transform
            var transforms = new List<Transform>();
            CollectTransforms(animator.transform, transforms);

            foreach (var t in transforms)
            {
                string path = GetFullPath(t, animator.transform);
                string displayPath = path.Replace("/", " → ");
                string boneName = t.name;

                menu.AddItem(new GUIContent(displayPath), _parentPath.stringValue == boneName || _parentPath.stringValue == path, () =>
                {
                    serializedObject.Update();
                    _parentPath.stringValue = boneName;
                    serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        private static void CollectTransforms(Transform root, List<Transform> results)
        {
            foreach (Transform child in root)
            {
                results.Add(child);
                CollectTransforms(child, results);
            }
        }

        private static string GetFullPath(Transform t, Transform root)
        {
            var parts = new List<string>();
            var current = t;
            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return string.Join("/", parts);
        }

        private static Animator GetTargetAnimator()
        {
            var listWindows = Resources.FindObjectsOfTypeAll<ActionListWindow>();
            if (listWindows.Length > 0 && listWindows[0].TargetAnimator != null)
                return listWindows[0].TargetAnimator;
            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var result = FindChildRecursive(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private void DrawSceneHandles(SceneView sceneView)
        {
            if (_clip == null) return;
            if (!VFXPlayable.ActiveInstances.TryGetValue(_clip, out var instance) || instance == null)
                return;

            // 查找参考点 Transform
            var refTransform = FindRefTransform();
            var refPos = refTransform != null ? refTransform.position
                       : (GetTargetAnimator() != null ? GetTargetAnimator().transform.position : Vector3.zero);
            var refRot = refTransform != null ? refTransform.rotation : Quaternion.identity;

            // 计算当前世界坐标
            Vector3 worldPos;
            Quaternion worldRot;
            if (_clip.attachToParent && refTransform != null)
            {
                // 挂载模式：偏移是本地空间
                worldPos = refTransform.TransformPoint(_clip.positionOffset);
                worldRot = refTransform.rotation * Quaternion.Euler(_clip.rotationOffset);
            }
            else
            {
                // 世界模式：偏移以参考点旋转变换
                worldPos = refPos + refRot * _clip.positionOffset;
                worldRot = refRot * Quaternion.Euler(_clip.rotationOffset);
            }

            EditorGUI.BeginChangeCheck();

            switch (Tools.current)
            {
                case Tool.Rotate:
                    worldRot = Handles.RotationHandle(worldRot, worldPos);
                    break;
                case Tool.Transform:
                    Handles.TransformHandle(ref worldPos, ref worldRot);
                    break;
                default:
                    worldPos = Handles.PositionHandle(worldPos, worldRot);
                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_clip, "调整 VFX 偏移");

                if (_clip.attachToParent && refTransform != null)
                {
                    _clip.positionOffset = refTransform.InverseTransformPoint(worldPos);
                    _clip.rotationOffset = (Quaternion.Inverse(refTransform.rotation) * worldRot).eulerAngles;
                }
                else
                {
                    _clip.positionOffset = Quaternion.Inverse(refRot) * (worldPos - refPos);
                    _clip.rotationOffset = (Quaternion.Inverse(refRot) * worldRot).eulerAngles;
                }

                EditorUtility.SetDirty(_clip);
            }

            // 标签
            string refLabel = string.IsNullOrEmpty(_clip.parentPath) ? "根" : _clip.parentPath;
            string modeLabel = _clip.attachToParent ? "跟随" : "固定";
            Handles.Label(worldPos + Vector3.up * 0.3f,
                $"VFX: {(_clip.vfxPrefab != null ? _clip.vfxPrefab.name : "(未设置)")} [{refLabel}, {modeLabel}]",
                EditorStyles.boldLabel);
        }

        private Transform FindRefTransform()
        {
            if (string.IsNullOrEmpty(_clip.parentPath)) return null;
            var animator = GetTargetAnimator();
            if (animator == null) return null;
            if (_clip.parentPath == ".") return animator.transform;
            var found = animator.transform.Find(_clip.parentPath);
            return found != null ? found : FindChildRecursive(animator.transform, _clip.parentPath);
        }
    }
}
