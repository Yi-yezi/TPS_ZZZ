using UnityEngine;
using UnityEngine.UI;

public class WorldspaceHealthBar : MonoBehaviour
{
    [Tooltip("要跟踪的 CombatEntity 组件")]
    public CombatEntity CombatEntity;

    [Tooltip("显示血量的 Image（Fill 模式）")]
    public Image HealthBarImage;

    [Tooltip("血条 Pivot（用于朝向相机）")]
    public Transform HealthBarPivot;

    [Tooltip("满血时是否隐藏血条")]
    public bool HideFullHealthBar = true;

    void Update()
    {
        if (CombatEntity == null) return;

        float ratio = CombatEntity.maxHp > 0f
            ? CombatEntity.CurrentHp / CombatEntity.maxHp
            : 0f;

        HealthBarImage.fillAmount = ratio;

        // 血条始终面向相机
        if (HealthBarPivot != null && Camera.main != null)
            HealthBarPivot.LookAt(Camera.main.transform.position);

        // 满血时隐藏
        if (HideFullHealthBar && HealthBarPivot != null)
            HealthBarPivot.gameObject.SetActive(ratio < 1f);
    }
}
