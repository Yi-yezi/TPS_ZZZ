using Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class InGameMenuManager : MonoBehaviour
{
    [Tooltip("要切换显示/隐藏的菜单 Canvas")]
    public Canvas MenuCanvas;

    [Tooltip("关闭按钮（可选）")]
    public Button CloseButton;

    CharacterInput m_Input;
    CinemachineInputProvider m_CinemachineInput;

    void Start()
    {
        if (MenuCanvas != null)
            MenuCanvas.gameObject.SetActive(false);

        if (CloseButton != null)
            CloseButton.onClick.AddListener(CloseMenu);

        m_Input = new CharacterInput();
        m_Input.Enable();

        m_CinemachineInput = FindObjectOfType<CinemachineInputProvider>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (m_Input.Player.Tab.WasPressedThisFrame())
            ToggleMenu();
    }

    void OnDestroy()
    {
        m_Input?.Dispose();
    }

    public void ToggleMenu()
    {
        if (MenuCanvas == null) return;

        bool active = !MenuCanvas.gameObject.activeSelf;
        MenuCanvas.gameObject.SetActive(active);

        // 暂停/恢复游戏
        Time.timeScale = active ? 0f : 1f;

        // 菜单打开时显示鼠标，关闭时锁定
        Cursor.lockState = active ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = active;

        // 暂停时禁用相机输入，防止相机乱转
        if (m_CinemachineInput != null)
            m_CinemachineInput.enabled = !active;
    }

    public void CloseMenu()
    {
        if (MenuCanvas == null) return;

        MenuCanvas.gameObject.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (m_CinemachineInput != null)
            m_CinemachineInput.enabled = true;
    }
}
