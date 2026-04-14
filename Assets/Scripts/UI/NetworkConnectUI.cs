using UnityEngine;

/// <summary>
/// 简易网络连接 UI。
/// 挂载到场景中任意 GameObject，运行后按 F1 连接服务器。
/// 连接成功后 UI 自动隐藏。
/// </summary>
public class NetworkConnectUI : MonoBehaviour
{
    private bool _showUI = true;
    private string _host = "127.0.0.1";
    private string _port = "7777";
    private string _name = "";
    private string _status = "未连接";

    void Start()
    {
        _name = System.Environment.MachineName;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            _showUI = !_showUI;
    }

    void OnGUI()
    {
        if (!_showUI) return;

        GUILayout.BeginArea(new Rect(10, 10, 280, 240), GUI.skin.box);
        GUILayout.Label("== 网络连接 (F1 切换) ==");

        GUILayout.BeginHorizontal();
        GUILayout.Label("服务器:", GUILayout.Width(50));
        _host = GUILayout.TextField(_host, GUILayout.Width(120));
        GUILayout.Label(":", GUILayout.Width(10));
        _port = GUILayout.TextField(_port, GUILayout.Width(60));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("昵称:", GUILayout.Width(50));
        _name = GUILayout.TextField(_name, GUILayout.Width(190));
        GUILayout.EndHorizontal();

        var nm = NetworkManager.Instance;
        bool connected = nm != null && nm.IsConnected;

        GUI.enabled = !connected;
        if (GUILayout.Button(connected ? "已连接" : "连接服务器"))
        {
            if (nm != null && int.TryParse(_port, out int port))
            {
                nm.serverHost = _host;
                nm.serverPort = port;
                nm.playerName = _name;
                nm.Connect();
                _status = "连接中...";
            }
        }
        GUI.enabled = true;

        // 断开连接按钮
        GUI.enabled = connected;
        if (GUILayout.Button("断开连接"))
        {
            nm.Disconnect();
            _status = "已断开";
        }
        GUI.enabled = true;

        if (connected)
            _status = $"已连接 (ID: {nm.LocalPlayerId})";

        GUILayout.Label(_status);
        GUILayout.EndArea();
    }
}
