using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 攻击判定盒 - 挂在角色武器/肢体上的 Trigger Collider
/// </summary>
[RequireComponent(typeof(Collider))]
public class HitBox : MonoBehaviour
{
    [Tooltip("判定盒名称，与 ActionSO.hitboxNames 对应")]
    public string hitBoxName;

    private float damage;
    private GameObject owner;
    private readonly HashSet<GameObject> hitTargets = new();

    /// <summary>
    /// 激活判定盒，设置伤害值和归属者
    /// </summary>
    public void Activate(float dmg, GameObject ownerObj)
    {
        damage = dmg;
        owner = ownerObj;
        hitTargets.Clear();
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 关闭判定盒
    /// </summary>
    public void Deactivate()
    {
        gameObject.SetActive(false);
        hitTargets.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        var hurtBox = other.GetComponent<HurtBox>();
        if (hurtBox == null) return;
        if (hurtBox.Owner == owner) return;           // 不打自己
        if (hitTargets.Contains(hurtBox.Owner)) return; // 同一次攻击不重复命中

        hitTargets.Add(hurtBox.Owner);

        var info = new DamageInfo
        {
            Damage = damage,
            Attacker = owner,
            HitPoint = other.ClosestPoint(transform.position),
            HitDirection = (other.transform.position - transform.position).normalized,
        };

        hurtBox.TakeHit(info);
    }
}
