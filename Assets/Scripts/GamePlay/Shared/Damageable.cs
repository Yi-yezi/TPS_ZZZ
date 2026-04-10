using System;
using UnityEngine;

namespace GamePlay.Shared
{
    /// <summary>
    /// 可受伤组件，处理伤害计算与伤害反馈。
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Damageable : MonoBehaviour
    {
        [Header("防御配置")]
        [SerializeField] private float damageMultiplier = 1f;
        [SerializeField] private bool invulnerable = false;

        [Header("无敌时间")]
        [SerializeField] private bool useInvulnerabilityFrames = false;
        [SerializeField] private float invulnerabilityDuration = 0.5f;

        private Health health;
        private float invulnerabilityTimer = 0f;

        /// <summary>伤害倍率。</summary>
        public float DamageMultiplier
        {
            get => damageMultiplier;
            set => damageMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>是否无敌。</summary>
        public bool IsInvulnerable => invulnerable || (useInvulnerabilityFrames && invulnerabilityTimer > 0f);

        /// <summary>受伤事件（伤害值，攻击者）。</summary>
        public event Action<int, GameObject> OnDamageReceived;

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (invulnerabilityTimer > 0f)
            {
                invulnerabilityTimer -= Time.deltaTime;
            }
        }

        /// <summary>
        /// 应用伤害。
        /// </summary>
        /// <param name="baseDamage">基础伤害。</param>
        /// <param name="attacker">攻击者。</param>
        public void ApplyDamage(int baseDamage, GameObject attacker = null)
        {
            if (IsInvulnerable || health == null || health.IsDead)
                return;

            int finalDamage = Mathf.RoundToInt(baseDamage * damageMultiplier);
            finalDamage = Mathf.Max(0, finalDamage);

            health.TakeDamage(finalDamage);
            OnDamageReceived?.Invoke(finalDamage, attacker);

            if (useInvulnerabilityFrames)
            {
                invulnerabilityTimer = invulnerabilityDuration;
            }
        }

        /// <summary>
        /// 设置无敌状态。
        /// </summary>
        public void SetInvulnerable(bool value)
        {
            invulnerable = value;
        }

        /// <summary>
        /// 触发无敌帧。
        /// </summary>
        public void TriggerInvulnerabilityFrames(float duration = -1f)
        {
            if (!useInvulnerabilityFrames)
                return;

            invulnerabilityTimer = duration < 0f ? invulnerabilityDuration : duration;
        }
    }
}
