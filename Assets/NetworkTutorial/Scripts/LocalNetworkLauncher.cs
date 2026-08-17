using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 本地测试专用启动界面。
/// Unity Editor点Host，Windows exe窗口点Client。
/// </summary>
public sealed class LocalNetworkLauncher : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button shutdownButton;

    [Header("Text")]
    [SerializeField] private TMP_Text statusText;

    private NetworkManager Manager => NetworkManager.Singleton;

    private void Start()
    {
        hostButton.onClick.AddListener(StartHost);
        clientButton.onClick.AddListener(StartClient);
        shutdownButton.onClick.AddListener(Shutdown);

        if (Manager == null)
        {
            SetStatus("错误：场景中没有NetworkManager");
            SetButtons(false);
            return;
        }

        Manager.OnClientConnectedCallback += OnClientConnected;
        Manager.OnClientDisconnectCallback += OnClientDisconnected;

        SetStatus("请选择Host或Client。主窗口先点Host。");
        RefreshButtons();
    }

    private void OnDestroy()
    {
        if (hostButton != null)
            hostButton.onClick.RemoveListener(StartHost);
        if (clientButton != null)
            clientButton.onClick.RemoveListener(StartClient);
        if (shutdownButton != null)
            shutdownButton.onClick.RemoveListener(Shutdown);

        if (Manager != null)
        {
            Manager.OnClientConnectedCallback -= OnClientConnected;
            Manager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void StartHost()
    {
        if (Manager == null || Manager.IsListening)
            return;

        bool started = Manager.StartHost();
        SetStatus(started
            ? "Host已启动，等待Client加入..."
            : "Host启动失败，请查看Console");
        RefreshButtons();
    }

    private void StartClient()
    {
        if (Manager == null || Manager.IsListening)
            return;

        bool started = Manager.StartClient();
        SetStatus(started
            ? "Client正在连接127.0.0.1:7777..."
            : "Client启动失败，请查看Console");
        RefreshButtons();
    }

    private void Shutdown()
    {
        if (Manager == null || !Manager.IsListening)
            return;

        Manager.Shutdown();
        SetStatus("连接已关闭，可以重新选择Host或Client");
        RefreshButtons();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (Manager.IsHost)
        {
            SetStatus(
                $"Host运行中，客户端{clientId}已连接，当前人数：" +
                Manager.ConnectedClientsIds.Count);
        }
        else if (clientId == Manager.LocalClientId)
        {
            SetStatus($"Client连接成功，本机ClientId={clientId}");
        }

        RefreshButtons();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (Manager != null && clientId == Manager.LocalClientId)
        {
            SetStatus("本机已断开连接");
        }
        else
        {
            SetStatus($"客户端{clientId}已离开");
        }

        RefreshButtons();
    }

    private void RefreshButtons()
    {
        bool isListening = Manager != null && Manager.IsListening;
        hostButton.interactable = !isListening;
        clientButton.interactable = !isListening;
        shutdownButton.interactable = isListening;
    }

    private void SetButtons(bool interactable)
    {
        hostButton.interactable = interactable;
        clientButton.interactable = interactable;
        shutdownButton.interactable = interactable;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log("[LocalNetworkLauncher] " + message, this);
    }
}