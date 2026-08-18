using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 只管理UGUI控件，不直接访问UGS或NetworkManager。
/// </summary>
public sealed class RelayConnectionView : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button createWorldButton;
    [SerializeField] private Button copyCodeButton;
    [SerializeField] private Button joinWorldButton;
    [SerializeField] private Button leaveWorldButton;

    [Header("Input And Text")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text hostJoinCodeText;
    [SerializeField] private TMP_Text statusText;

    public event Action CreateWorldClicked;
    public event Action CopyCodeClicked;
    public event Action<string> JoinWorldClicked;
    public event Action LeaveWorldClicked;

    private string currentHostJoinCode = string.Empty;

    private void Awake()
    {
        createWorldButton.onClick.AddListener(OnCreateClicked);
        copyCodeButton.onClick.AddListener(OnCopyClicked);
        joinWorldButton.onClick.AddListener(OnJoinClicked);
        leaveWorldButton.onClick.AddListener(OnLeaveClicked);

        SetJoinCode(string.Empty);
        SetStatus("请选择创建世界或输入加入码");
        SetSessionActive(false);
        SetBusy(false);
    }

    private void OnDestroy()
    {
        createWorldButton.onClick.RemoveListener(OnCreateClicked);
        copyCodeButton.onClick.RemoveListener(OnCopyClicked);
        joinWorldButton.onClick.RemoveListener(OnJoinClicked);
        leaveWorldButton.onClick.RemoveListener(OnLeaveClicked);
    }

    public void SetJoinCode(string joinCode)
    {
        currentHostJoinCode = joinCode ?? string.Empty;

        bool hasCode = !string.IsNullOrWhiteSpace(currentHostJoinCode);

        if (hasCode)
        {
            GUIUtility.systemCopyBuffer = currentHostJoinCode;
        }

        hostJoinCodeText.text = hasCode
            ? "加入码：" + currentHostJoinCode
            : "加入码：尚未创建";
        copyCodeButton.interactable = hasCode;
    }

    public void SetStatus(string message)
    {
        statusText.text = message ?? string.Empty;
    }

    public void SetBusy(bool busy)
    {
        if (busy)
        {
            createWorldButton.interactable = false;
            joinWorldButton.interactable = false;
            leaveWorldButton.interactable = false;
        }
    }

    public void SetSessionActive(bool active)
    {
        createWorldButton.interactable = !active;
        joinWorldButton.interactable = !active;
        joinCodeInput.interactable = !active;
        leaveWorldButton.interactable = active;
    }

    public void RestoreButtons(bool sessionActive, bool busy)
    {
        if (busy)
        {
            SetBusy(true);
            return;
        }

        SetSessionActive(sessionActive);
        copyCodeButton.interactable =
            !string.IsNullOrWhiteSpace(currentHostJoinCode);
    }

    public void CopyCurrentCodeToClipboard()
    {
        if (string.IsNullOrWhiteSpace(currentHostJoinCode))
        {
            SetStatus("当前没有可复制的加入码");
            return;
        }

        GUIUtility.systemCopyBuffer = currentHostJoinCode;
        SetStatus("加入码已复制到剪贴板");
    }

    private void OnCreateClicked()
    {
        CreateWorldClicked?.Invoke();
    }

    private void OnCopyClicked()
    {
        CopyCodeClicked?.Invoke();
    }

    private void OnJoinClicked()
    {
        JoinWorldClicked?.Invoke(joinCodeInput.text);
    }

    private void OnLeaveClicked()
    {
        LeaveWorldClicked?.Invoke();
    }
}