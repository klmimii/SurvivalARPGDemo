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
        view.CreateWorldClicked += OnCreateWorldClicked;
        view.CopyCodeClicked += OnCopyCodeClicked;
        view.JoinWorldClicked += OnJoinWorldClicked;
        view.LeaveWorldClicked += OnLeaveWorldClicked;

        sessionService.StatusChanged += view.SetStatus;
        sessionService.JoinCodeChanged += view.SetJoinCode;
        sessionService.BusyChanged += OnBusyChanged;
        sessionService.SessionStateChanged += OnSessionStateChanged;

        if (GameBootstrap.InputMode != null)
        {
            GameBootstrap.InputMode.SetMode(GameInputMode.UI);
        }

        // 连接页面需要操作按钮和输入框，因此显示鼠标。
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        view.RestoreButtons(
            sessionService.HasSession,
            sessionService.IsBusy);
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
        if (!sessionService.HasSession || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            SetMenuVisible(!menuVisible);
        }
    }

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
            GameBootstrap.InputMode.SetMode(
                visible
                    ? GameInputMode.UI
                    : GameInputMode.Gameplay);
        }
        else
        {
            Cursor.visible = visible;
            Cursor.lockState = visible
                ? CursorLockMode.None
                : CursorLockMode.Locked;
        }
    }

    private async void OnCreateWorldClicked()
    {
        await sessionService.CreateWorldAsync();
    }

    private void OnCopyCodeClicked()
    {
        view.CopyCurrentCodeToClipboard();
    }

    private async void OnJoinWorldClicked(string joinCode)
    {
        await sessionService.JoinWorldAsync(joinCode);
    }

    private async void OnLeaveWorldClicked()
    {
        await sessionService.LeaveWorldAsync();
    }

    private void OnBusyChanged(bool busy)
    {
        view.RestoreButtons(sessionService.HasSession, busy);
    }

    private void OnSessionStateChanged(bool active)
    {
        view.RestoreButtons(active, sessionService.IsBusy);

        // 连接成功后隐藏；离开世界后重新显示。
        SetMenuVisible(!active);
    }
}