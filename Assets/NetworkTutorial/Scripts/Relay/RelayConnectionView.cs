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
        //绑定每个按钮的监听事件
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
        //？？的意思是如果左边的值不为空就用左边的值，否则用右边的值
        currentHostJoinCode = joinCode ?? string.Empty;
        //如果加入码不为空
        bool hasCode = !string.IsNullOrWhiteSpace(currentHostJoinCode);
        //那么使用GUIUtility中的静态属性systemCopyBuffer，将字符串复制到系统剪贴板。自动复制一次加入码
        if (hasCode)
        {
            GUIUtility.systemCopyBuffer = currentHostJoinCode;
        }
        //更新加入码区域文本
        hostJoinCodeText.text = hasCode ? "加入码：" + currentHostJoinCode : "加入码：尚未创建";
        //复制按钮根据是否有hasCode不能点击或能点击
        copyCodeButton.interactable = hasCode;
    }

    /// <summary>
    /// 设置状态文本文字
    /// </summary>
    /// <param name="message"></param>
    public void SetStatus(string message)
    {
        statusText.text = message ?? string.Empty;
    }

    /// <summary>
    /// 设置如果在处理网络操作，按钮都不可点击
    /// </summary>
    /// <param name="busy"></param>
    public void SetBusy(bool busy)
    {
        if (busy)
        {
            createWorldButton.interactable = false;
            joinWorldButton.interactable = false;
            leaveWorldButton.interactable = false;
        }
    }

    /// <summary>
    /// 根据设置会话状态设置按钮，如果在房间只有离开房间可以点击，否则只有离开房间不能点击
    /// </summary>
    /// <param name="active"></param>
    public void SetSessionActive(bool active)
    {
        createWorldButton.interactable = !active;
        joinWorldButton.interactable = !active;
        joinCodeInput.interactable = !active;
        leaveWorldButton.interactable = active;
    }

    /// <summary>
    /// 恢复按钮
    /// </summary>
    /// <param name="sessionActive">当前玩家是否在房间中</param>
    /// <param name="busy">当前是否正在执行网络操作（创建/加入/离开）</param>
    public void RestoreButtons(bool sessionActive, bool busy)
    {
        //根据是否正在处理网络操作，控制所有按钮是否可点击
        if (busy)
        {
            SetBusy(true);
            return;
        }
        //根据是否在房间中，控制创建加入离开按钮组
        SetSessionActive(sessionActive);
        //复制按钮根据当前加入码区域是否有文字决定
        copyCodeButton.interactable = !string.IsNullOrWhiteSpace(currentHostJoinCode);
    }

    /// <summary>
    /// 复制当前代码到剪贴板方法
    /// </summary>
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