using System;
using UnityEngine;

namespace GamePlay.Shared
{
    /// <summary>
    /// 生命值组件，管理实体的生命值与死亡状态。
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Header("生命值配置")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int currentHealth;
        [SerializeField] private bool isDead = false;

        /// <summary>最大生命值。</summary>
        public int MaxHealth => maxHealth;
        
        /// <summary>当前生命值。</summary>
        public int CurrentHealth => currentHealth;
        
        /// <summary>是否死亡。</summary>
        public bool IsDead => isDead;
        
        /// <summary>生命值百分比（0-1）。</summary>
        public float HealthPercent => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

        /// <summary>生命值变化事件（当前值，最大值）。</summary>
        public event Action<int, int> OnHealthChanged;
        
        /// <summary>死亡事件。</summary>
        public event Action OnDeath;
        
        /// <summary>复活事件。</summary>
        public event Action OnRevive;

        private void Awake()
        {
            currentHealth = maxHealth;
            isDead = false;
        }

        /// <summary>
        /// 设置最大生命值并完全恢复。
        /// </summary>
        public void SetMaxHealth(int value, bool fillHealth = true)
        {
            maxHealth = Mathf.Max(1, value);
            if (fillHealth)
            {
                currentHealth = maxHealth;
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }

        /// <summary>
        /// 造成伤害。
        /// </summary>
        public void TakeDamage(int damage)
        {
            if (isDead || damage <= 0)
                return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0 && !isDead)
            {
                Die();
            }
        }

        /// <summary>
        /// 恢复生命值。
        /// </summary>
        public void Heal(int amount)
        {
            if (isDead || amount <= 0)
                return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// 执行死亡。
        /// </summary>
        private void Die()
        {
            isDead = true;
            OnDeath?.Invoke();
        }

        /// <summary>
        /// 复活并恢复生命值。
        /// </summary>
        public void Revive(int healthAmount = -1)
        {
            if (!isDead)
                return;

            isDead = false;
            currentHealth = healthAmount < 0 ? maxHealth : Mathf.Min(healthAmount, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnRevive?.Invoke();
        }

        /// <summary>
        /// 立即击杀。
        /// </summary>
        public void Kill()
        {
            if (isDead)
                return;

            currentHealth = 0;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            Die();
        }
    }
}
