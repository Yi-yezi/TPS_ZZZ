using UnityEngine;

/// <summary>
/// 受击盒 - 挂在角色身体上的 Trigger Collider，被 HitBox 检测
/// </summary>
[RequireComponent(typeof(Collider))]
public class HurtBox : MonoBehaviour
{
    [Tooltip("归属 GameObject（默认取 root）")]
    public GameObject Owner;

    private CombatEntity combatEntity;

    private void Awake()
    {
        if (Owner == null)
            Owner = transform.root.gameObject;

        combatEntity = Owner.GetComponent<CombatEntity>();
    }

    public void TakeHit(DamageInfo info)
    {
        combatEntity?.TakeDamage(info);
    }
}
