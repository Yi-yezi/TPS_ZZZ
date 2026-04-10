using System;
using UnityEngine;

/// <summary>
/// 战斗实体 - 血量管理
/// </summary>
public class CombatEntity : MonoBehaviour
{
    [Header("血量")]
    public float maxHp = 100f;

    [SerializeField]
    private float currentHp;

    public float CurrentHp => currentHp;
    public bool IsDead => currentHp <= 0f;

    public event Action<DamageInfo> OnDamaged;
    public event Action OnDied;

    private void Awake()
    {
        currentHp = maxHp;
    }

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead) return;

        currentHp = Mathf.Max(currentHp - info.Damage, 0f);
        OnDamaged?.Invoke(info);

        if (currentHp <= 0f)
        {
            OnDied?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        currentHp = Mathf.Min(currentHp + amount, maxHp);
    }

    public void ResetHp()
    {
        currentHp = maxHp;
    }
}
