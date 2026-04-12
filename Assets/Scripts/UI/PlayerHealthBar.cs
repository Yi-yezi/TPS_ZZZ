using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBar : MonoBehaviour
    {
        [Tooltip("Image component dispplaying current health")]
        public Image HealthFillImage;

        CombatEntity m_CombatEntity;

        void Start()
        {
            CharacterBehaviour player = FindObjectOfType<CharacterBehaviour>();

            m_CombatEntity = player.GetComponent<CombatEntity>();
        }

        void Update()
        {
            // update health bar value
            HealthFillImage.fillAmount = m_CombatEntity.CurrentHp / m_CombatEntity.maxHp;
        }
    }