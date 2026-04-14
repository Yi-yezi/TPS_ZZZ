using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 罗盘 UI。
/// 在屏幕顶部显示一个水平罗盘条，标记敌人和方向（N/S/E/W）的相对方位。
/// 外部通过 RegisterCompassElement / RegisterEnemy 注册跟踪目标，
/// Update 中根据玩家朝向求角度差，映射到罗盘条的水平位置。
/// </summary>
public class Compass : MonoBehaviour
{
    [Tooltip("罗盘条的 RectTransform，标记会被定位在其宽度范围内")]
    public RectTransform CompasRect;

    [Tooltip("可见角度范围（度），左右各占一半")]
    public float VisibilityAngle = 180f;

    [Tooltip("高度差对标记垂直偏移的放大倍数")]
    public float HeightDifferenceMultiplier = 2f;

    [Tooltip("远处敌人标记的最小缩放比例")]
    public float MinScale = 0.5f;

    [Tooltip("超过此距离后标记缩至最小")]
    public float DistanceMinScale = 50f;

    [Tooltip("标记垂直偏移的边距比例（防止超出罗盘条）")]
    public float CompasMarginRatio = 0.8f;

    [Tooltip("方向标记预制体（N/S/E/W）")]
    public GameObject MarkerDirectionPrefab;

    [Tooltip("敌人标记预制体（需有 CompassMarker 组件）")]
    public CompassMarker EnemyMarkerPrefab;

    public static Compass Instance { get; private set; }

    Transform m_PlayerTransform;
    Dictionary<Transform, CompassMarker> m_ElementsDictionary = new Dictionary<Transform, CompassMarker>();

    float m_WidthMultiplier;
    float m_HeightOffset;

    void Awake()
    {
        Instance = this;

        var player = FindObjectOfType<CharacterBehaviour>();
        if (player != null)
            m_PlayerTransform = player.transform;

        m_WidthMultiplier = CompasRect.rect.width / VisibilityAngle;
        m_HeightOffset = -CompasRect.rect.height / 2;
    }

    void Update()
    {
        if (m_PlayerTransform == null) return;

        foreach (var element in m_ElementsDictionary)
        {
            float distanceRatio = 1;
            float heightDifference = 0;
            float angle;

            if (element.Value.IsDirection)
            {
                angle = Vector3.SignedAngle(m_PlayerTransform.forward,
                    element.Key.transform.localPosition.normalized, Vector3.up);
            }
            else
            {
                Vector3 targetDir = (element.Key.transform.position - m_PlayerTransform.position).normalized;
                targetDir = Vector3.ProjectOnPlane(targetDir, Vector3.up);
                Vector3 playerForward = Vector3.ProjectOnPlane(m_PlayerTransform.forward, Vector3.up);
                angle = Vector3.SignedAngle(playerForward, targetDir, Vector3.up);

                Vector3 directionVector = element.Key.transform.position - m_PlayerTransform.position;

                heightDifference = (directionVector.y) * HeightDifferenceMultiplier;
                heightDifference = Mathf.Clamp(heightDifference, -CompasRect.rect.height / 2 * CompasMarginRatio,
                    CompasRect.rect.height / 2 * CompasMarginRatio);

                distanceRatio = directionVector.magnitude / DistanceMinScale;
                distanceRatio = Mathf.Clamp01(distanceRatio);
            }

            if (angle > -VisibilityAngle / 2 && angle < VisibilityAngle / 2)
            {
                element.Value.CanvasGroup.alpha = 1;
                element.Value.CanvasGroup.transform.localPosition = new Vector2(m_WidthMultiplier * angle,
                    heightDifference + m_HeightOffset);
                element.Value.CanvasGroup.transform.localScale =
                    Vector3.one * Mathf.Lerp(1, MinScale, distanceRatio);
            }
            else
            {
                element.Value.CanvasGroup.alpha = 0;
            }
        }
    }

    /// <summary>注册一个罗盘元素（由 CompassElement 在 Awake 中调用）。</summary>
    public void RegisterCompassElement(Transform element, CompassMarker marker)
    {
        marker.transform.SetParent(CompasRect);
        m_ElementsDictionary.Add(element, marker);
    }

    /// <summary>注销一个罗盘元素并销毁其标记 UI。</summary>
    public void UnregisterCompassElement(Transform element)
    {
        if (m_ElementsDictionary.TryGetValue(element, out CompassMarker marker) && marker.CanvasGroup != null)
            Destroy(marker.CanvasGroup.gameObject);
        m_ElementsDictionary.Remove(element);
    }

    /// <summary>动态注册一个敌人到罗盘（用 EnemyMarkerPrefab 生成标记）。</summary>
    public void RegisterEnemy(Transform enemy)
    {
        if (EnemyMarkerPrefab == null || enemy == null || m_ElementsDictionary.ContainsKey(enemy)) return;
        var marker = Instantiate(EnemyMarkerPrefab);
        marker.Initialize(null, null);
        RegisterCompassElement(enemy, marker);
    }
}