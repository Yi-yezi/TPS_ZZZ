using UnityEngine;
using UnityEngine.UI;

public class FeedbackFlashHUD : MonoBehaviour
    {
        [Header("References")] [Tooltip("Image component of the flash")]
        public Image FlashImage;

        [Tooltip("CanvasGroup to fade the damage flash, used when recieving damage end healing")]
        public CanvasGroup FlashCanvasGroup;

        [Tooltip("CanvasGroup to fade the critical health vignette")]
        public CanvasGroup VignetteCanvasGroup; // 当玩家处于危急状态时显示的渐变遮罩CanvasGroup

        [Header("Damage")] [Tooltip("Color of the damage flash")]
        public Color DamageFlashColor;

        [Tooltip("Duration of the damage flash")]
        public float DamageFlashDuration;

        [Tooltip("Max alpha of the damage flash")]
        public float DamageFlashMaxAlpha = 1f;

        [Header("Critical health")] [Tooltip("Max alpha of the critical vignette")]
        public float CriticaHealthVignetteMaxAlpha = .8f;

        [Tooltip("Frequency at which the vignette will pulse when at critical health")]
        public float PulsatingVignetteFrequency = 4f;

        [Tooltip("低于此血量比率时进入危急状态（0–1）")]
        public float CriticalHealthRatio = 0.3f;

        [Header("Heal")] [Tooltip("Color of the heal flash")]
        public Color HealFlashColor;

        [Tooltip("Duration of the heal flash")]
        public float HealFlashDuration;

        [Tooltip("Max alpha of the heal flash")]
        public float HealFlashMaxAlpha = 1f;

        bool m_FlashActive;
        float m_LastTimeFlashStarted = Mathf.NegativeInfinity;
        CombatEntity m_CombatEntity;
        float m_PreviousHp;
        GameFlowManager m_GameFlowManager;

        void Start()
        {
            CharacterBehaviour player = FindObjectOfType<CharacterBehaviour>();

            m_CombatEntity = player.GetComponent<CombatEntity>();
            m_PreviousHp = m_CombatEntity.CurrentHp;

            m_GameFlowManager = FindObjectOfType<GameFlowManager>();

            m_CombatEntity.OnDamaged += OnTakeDamage;
            m_CombatEntity.OnHpChanged += OnHpChanged;
        }

        void Update()
        {
            bool isCritical = m_CombatEntity.maxHp > 0f &&
                              (m_CombatEntity.CurrentHp / m_CombatEntity.maxHp) <= CriticalHealthRatio;
            if (isCritical) // 如果玩家处于危急状态
            {
                VignetteCanvasGroup.gameObject.SetActive(true);
                float vignetteAlpha =
                    (1 - (m_CombatEntity.CurrentHp / m_CombatEntity.maxHp /
                          CriticalHealthRatio)) * CriticaHealthVignetteMaxAlpha;

                if (m_GameFlowManager.GameIsEnding)
                    VignetteCanvasGroup.alpha = vignetteAlpha;
                else
                    VignetteCanvasGroup.alpha =
                        ((Mathf.Sin(Time.time * PulsatingVignetteFrequency) / 2) + 0.5f) * vignetteAlpha;
            }
            else
            {
                VignetteCanvasGroup.gameObject.SetActive(false);
            }


            if (m_FlashActive) // 如果正在进行闪烁效果，根据时间计算当前的闪烁强度，并更新CanvasGroup的alpha值
            {
                float normalizedTimeSinceDamage = (Time.time - m_LastTimeFlashStarted) / DamageFlashDuration;

                if (normalizedTimeSinceDamage < 1f) // 如果闪烁时间未结束，根据时间计算当前的闪烁强度
                {
                    float flashAmount = DamageFlashMaxAlpha * (1f - normalizedTimeSinceDamage);
                    FlashCanvasGroup.alpha = flashAmount;
                }
                else
                {
                    FlashCanvasGroup.gameObject.SetActive(false);
                    m_FlashActive = false;
                }
            }
        }

        void ResetFlash() // 重置闪烁效果，准备开始新的闪烁
        {
            m_LastTimeFlashStarted = Time.time;
            m_FlashActive = true;
            FlashCanvasGroup.alpha = 0f;
            FlashCanvasGroup.gameObject.SetActive(true);
        }

        void OnTakeDamage(DamageInfo info)
        {
            ResetFlash();
            FlashImage.color = DamageFlashColor;
        }

        void OnHpChanged(float current, float max)
        {
            if (current > m_PreviousHp)
            {
                ResetFlash();
                FlashImage.color = HealFlashColor;
            }
            m_PreviousHp = current;
        }
    }