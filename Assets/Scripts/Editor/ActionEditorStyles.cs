using UnityEditor;
using UnityEngine;

namespace SkillSystem.Editor
{
    /// <summary>
    /// 编辑器通用样式
    /// </summary>
    public static class ActionEditorStyles
    {
        private static GUIStyle _headerLabel;
        public static GUIStyle HeaderLabel
        {
            get
            {
                if (_headerLabel == null)
                {
                    _headerLabel = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 13,
                        alignment = TextAnchor.MiddleLeft,
                    };
                }
                return _headerLabel;
            }
        }

        private static GUIStyle _listItemNormal;
        public static GUIStyle ListItemNormal
        {
            get
            {
                if (_listItemNormal == null)
                {
                    _listItemNormal = new GUIStyle(GUI.skin.button)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        richText = true,
                    };
                }
                return _listItemNormal;
            }
        }

        public static readonly Color PanelBackground = new(0.22f, 0.22f, 0.22f, 1f);
        public static readonly Color SelectedColor = new(0.17f, 0.36f, 0.53f, 1f);
        public static readonly Color TrackBackground = new(0.28f, 0.28f, 0.28f, 1f);
        public static readonly Color TimelineClipColor = new(0.3f, 0.6f, 0.3f, 0.8f);
        public static readonly Color TransitionArrowColor = new(0.9f, 0.6f, 0.2f, 1f);
        public static readonly Color GridLineColor = new(0.35f, 0.35f, 0.35f, 0.5f);

        public static void BeginHighlight(GUIStyle style, bool condition)
        {
            if (condition)
            {
                GUI.backgroundColor = new Color(0.3f, 0.6f, 1f, 1f);
                style.fontStyle = FontStyle.Bold;
            }
        }

        public static void EndHighlight()
        {
            GUI.backgroundColor = Color.white;
        }
    }
}
