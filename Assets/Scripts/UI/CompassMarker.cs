using UnityEngine;
using UnityEngine.UI;

public class CompassMarker : MonoBehaviour
{
    [Tooltip("标记图片")] public Image MainImage;

    [Tooltip("标记的 CanvasGroup")]
    public CanvasGroup CanvasGroup;

    [Header("敌人标记")] [Tooltip("默认颜色")]
    public Color DefaultColor = Color.red;

    [Tooltip("警觉状态颜色")]
    public Color AltColor = Color.yellow;

    [Header("方向标记")] [Tooltip("是否为方向标记（N/S/E/W）")]
    public bool IsDirection;

    [Tooltip("方向文字")]
    public TMPro.TextMeshProUGUI TextContent;

    public void Initialize(CompassElement compassElement, string textDirection)
    {
        if (IsDirection && TextContent)
        {
            TextContent.text = textDirection;
        }
        else
        {
            // 非方向标记（敌人）—— 默认用 DefaultColor
            if (MainImage != null)
                MainImage.color = DefaultColor;
        }
    }
}