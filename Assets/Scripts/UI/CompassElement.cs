using UnityEngine;

public class CompassElement : MonoBehaviour
{
    [Tooltip("罗盘上对应的标记预制体")]
    public CompassMarker CompassMarkerPrefab;

    [Tooltip("方向标记的文字（如 N/S/E/W）")]
    public string TextDirection;

    Compass m_Compass;

    void Awake()
    {
        m_Compass = FindObjectOfType<Compass>();
        if (m_Compass == null)
        {
            Debug.LogWarning($"[CompassElement] Compass not found in scene, disabling {name}");
            enabled = false;
            return;
        }

        var markerInstance = Instantiate(CompassMarkerPrefab);
        markerInstance.Initialize(this, TextDirection);
        m_Compass.RegisterCompassElement(transform, markerInstance);
    }

    void OnDestroy()
    {
        if (m_Compass != null)
            m_Compass.UnregisterCompassElement(transform);
    }
}