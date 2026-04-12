using UnityEngine;

public class GameFlowManager : MonoBehaviour
    {
        [Header("Parameters")] [Tooltip("Duration of the fade-to-black at the end of the game")]
        public float EndSceneLoadDelay = 3f;

        [Tooltip("The canvas group of the fade-to-black screen")]
        public CanvasGroup EndGameFadeCanvasGroup;

        public bool GameIsEnding { get; private set; }

        float m_TimeQuit;

        CombatEntity m_PlayerCombat;

        void Awake()
        {
            m_PlayerCombat = FindObjectOfType<CharacterBehaviour>().GetComponent<CombatEntity>();
            m_PlayerCombat.OnDied += OnPlayerDeath;
        }

        void Update()
        {
            if (GameIsEnding)
            {
                float timeRatio = 1 - (m_TimeQuit - Time.time) / EndSceneLoadDelay;
                EndGameFadeCanvasGroup.alpha = timeRatio;

                if (Time.time >= m_TimeQuit)
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
            }
        }

        void OnPlayerDeath() => EndGame();

        void EndGame()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GameIsEnding = true;
            EndGameFadeCanvasGroup.gameObject.SetActive(true);
            m_TimeQuit = Time.time + EndSceneLoadDelay;
        }

        void OnDestroy()
        {
            if (m_PlayerCombat != null)
                m_PlayerCombat.OnDied -= OnPlayerDeath;
        }
    }