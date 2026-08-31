using UnityEngine;
using UnityEngine.InputSystem;
/// <summary>
/// MVP中的Presenter：连接View事件与RelaySessionService。
/// </summary>
public sealed class RelayConnectionPresenter : MonoBehaviour
{
    [SerializeField] private RelayConnectionView view;
    [SerializeField] private RelaySessionService sessionService;

    [SerializeField] private CanvasGroup connectionCanvasGroup;

    private bool menuVisible = true;

    private void Start()
    {
        //订阅view中的点击事件
        view.CreateWorldClicked += OnCreateWorldClicked;
        view.CopyCodeClicked += OnCopyCodeClicked;
        view.JoinWorldClicked += OnJoinWorldClicked;
        view.LeaveWorldClicked += OnLeaveWorldClicked;
        //订阅sessionService中的状态文字改变/加入码改变等事件/是否在处理网络操作/会话状态
        sessionService.StatusChanged += view.SetStatus;
        sessionService.JoinCodeChanged += view.SetJoinCode;
        sessionService.BusyChanged += OnBusyChanged;
        sessionService.SessionStateChanged += OnSessionStateChanged;

        if (GameBootstrap.InputMode != null)
        {
            //将输入模式设为UI
            GameBootstrap.InputMode.SetMode(GameInputMode.UI);
        }

        // 连接页面需要操作按钮和输入框，因此显示鼠标。
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        view.RestoreButtons(sessionService.HasSession,sessionService.IsBusy);
    }

    private void OnDestroy()
    {
        if (view != null)
        {
            view.CreateWorldClicked -= OnCreateWorldClicked;
            view.CopyCodeClicked -= OnCopyCodeClicked;
            view.JoinWorldClicked -= OnJoinWorldClicked;
            view.LeaveWorldClicked -= OnLeaveWorldClicked;
        }

        if (sessionService != null)
        {
            sessionService.StatusChanged -= view.SetStatus;
            sessionService.JoinCodeChanged -= view.SetJoinCode;
            sessionService.BusyChanged -= OnBusyChanged;
            sessionService.SessionStateChanged -= OnSessionStateChanged;
        }
    }

    private void Update()
    {
        //当玩家不在任何房间中或者Unity的InputSystem检测到没有键盘。只有在房间里才需要切换菜单
        if (!sessionService.HasSession || Keyboard.current == null)
        {
            return;
        }
        //通过UnityInputSystem获取键盘上的f1键，.wadPressedThisFrame这个属性在这一帧刚刚按下时返回true，但只持续一阵
        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            //调用设置菜单显隐方法，实现开关切换
            SetMenuVisible(!menuVisible);
        }
    }

    /// <summary>
    /// 设置菜单显隐
    /// </summary>
    /// <param name="visible"></param>
    private void SetMenuVisible(bool visible)
    {
        menuVisible = visible;

        if (connectionCanvasGroup != null)
        {
            connectionCanvasGroup.alpha = visible ? 1f : 0f;
            connectionCanvasGroup.interactable = visible;
            connectionCanvasGroup.blocksRaycasts = visible;
        }

        if (GameBootstrap.InputMode != null)
        {
            GameBootstrap.InputMode.SetMode(visible? GameInputMode.UI : GameInputMode.Gameplay);
        }
        else
        {
            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }

    /// <summary>
    /// 当点击创建世界按钮时调用sessionService的异步创建世界方法
    /// 因为事件处理器的签名必须匹配委托类型，UnityAction或Button.onClick.AddListener需要的签名是void返回类型，不能返回Task
    /// </summary>
    private async void OnCreateWorldClicked()
    {
        //调用异步创建世界方法
        //因为CreateWorldAsync方法是async Task所以才能在这里用await
        //因为在方法内部已经用了try-catch捕获了异常，所以异常不会往外抛
        await sessionService.CreateWorldAsync();
    }

    /// <summary>
    /// 点击复制按钮时，调用View的复制方法
    /// </summary>
    private void OnCopyCodeClicked()
    {
        view.CopyCurrentCodeToClipboard();
    }

    /// <summary>
    /// 当点击加入世界按钮时，调用异步加入世界方法，传入加入码
    /// </summary>
    /// <param name="joinCode"></param>
    private async void OnJoinWorldClicked(string joinCode)
    {
        await sessionService.JoinWorldAsync(joinCode);
    }

    /// <summary>
    /// 点击离开世界按钮时做什么
    /// </summary>
    private async void OnLeaveWorldClicked()
    {
        await sessionService.LeaveWorldAsync();
    }

    /// <summary>
    /// 当繁忙状态改变时做什么
    /// </summary>
    /// <param name="busy"></param>
    private void OnBusyChanged(bool busy)
    {
        view.RestoreButtons(sessionService.HasSession, busy);
    }

    /// <summary>
    /// 当会话状态改变时做什么，如果bool为true，则恢复按钮状态并将界面隐藏，false则锁定按钮状态并显示界面
    /// </summary>
    /// <param name="active"></param>
    private void OnSessionStateChanged(bool active)
    {
        view.RestoreButtons(active, sessionService.IsBusy);

        // 连接成功后隐藏；离开世界后重新显示。
        SetMenuVisible(!active);
    }
}