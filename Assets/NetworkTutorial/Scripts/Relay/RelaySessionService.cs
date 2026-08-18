using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// 只负责Unity服务、Session和Relay，不直接操作UI。
/// WithRelayNetwork会通过SDK自动启动NGO Host或Client。
/// </summary>
public sealed class RelaySessionService : MonoBehaviour
{
    public ISession ActiveSession { get; private set; }
    public bool IsBusy { get; private set; }
    public bool HasSession => ActiveSession != null;

    public event Action<string> StatusChanged;
    public event Action<string> JoinCodeChanged;
    public event Action<bool> BusyChanged;
    public event Action<bool> SessionStateChanged;

    public async Task InitializeAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            ReportStatus("正在初始化Unity Gaming Services...");
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            ReportStatus("正在匿名登录...");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        ReportStatus(
            "网络服务已就绪，PlayerId=" +
            AuthenticationService.Instance.PlayerId);
    }

    public async Task CreateWorldAsync()
    {
        if (IsBusy)
        {
            ReportStatus("正在处理上一个网络操作，请稍候");
            return;
        }

        if (ActiveSession != null)
        {
            ReportStatus("已经在一个世界中，请先离开");
            return;
        }

        SetBusy(true);

        try
        {
            await InitializeAsync();
            ReportStatus("正在创建Relay世界...");

            SessionOptions options = new SessionOptions
            {
                MaxPlayers = 2,
                IsPrivate = true,
                Name = "SurvivalARPGDemo"
            }.WithRelayNetwork();

            // WithRelayNetwork会配置Relay并自动启动NGO Host。
            ActiveSession = await MultiplayerService.Instance
                .CreateSessionAsync(options);

            SubscribeSessionEvents(ActiveSession);

            JoinCodeChanged?.Invoke(ActiveSession.Code);
            SessionStateChanged?.Invoke(true);
            ReportStatus("世界创建成功，等待另一名玩家加入");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            ActiveSession = null;
            SessionStateChanged?.Invoke(false);
            ReportStatus("创建失败：" + exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async Task JoinWorldAsync(string joinCode)
    {
        if (IsBusy)
        {
            ReportStatus("正在处理上一个网络操作，请稍候");
            return;
        }

        if (ActiveSession != null)
        {
            ReportStatus("已经在一个世界中，请先离开");
            return;
        }

        string normalizedCode = joinCode == null
            ? string.Empty
            : joinCode.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            ReportStatus("请先输入Host显示的加入码");
            return;
        }

        SetBusy(true);

        try
        {
            await InitializeAsync();
            ReportStatus("正在通过加入码连接Relay世界...");

            // SDK会查找Session、配置Relay并自动启动NGO Client。
            ActiveSession = await MultiplayerService.Instance
                .JoinSessionByCodeAsync(normalizedCode);

            SubscribeSessionEvents(ActiveSession);

            SessionStateChanged?.Invoke(true);
            ReportStatus("加入世界成功");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            ActiveSession = null;
            SessionStateChanged?.Invoke(false);
            ReportStatus("加入失败：" + exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async Task LeaveWorldAsync()
    {
        if (IsBusy)
        {
            ReportStatus("正在处理上一个网络操作，请稍候");
            return;
        }

        if (ActiveSession == null)
        {
            ReportStatus("当前没有加入任何世界");
            return;
        }

        SetBusy(true);

        ISession leavingSession = ActiveSession;

        try
        {
            ReportStatus("正在离开世界...");
            UnsubscribeSessionEvents(leavingSession);

            // LeaveAsync会退出Session并关闭关联的Relay/NGO连接。
            await leavingSession.LeaveAsync();

            ReportStatus("已离开世界");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            ReportStatus("离开时发生错误：" + exception.Message);
        }
        finally
        {
            ActiveSession = null;
            JoinCodeChanged?.Invoke(string.Empty);
            SessionStateChanged?.Invoke(false);
            SetBusy(false);
        }
    }

    private void SubscribeSessionEvents(ISession session)
    {
        if (session == null) return;

        session.PlayerJoined += OnPlayerJoined;
        session.PlayerHasLeft += OnPlayerLeft;
        session.RemovedFromSession += OnRemovedFromSession;
    }

    private void UnsubscribeSessionEvents(ISession session)
    {
        if (session == null) return;

        session.PlayerJoined -= OnPlayerJoined;
        session.PlayerHasLeft -= OnPlayerLeft;
        session.RemovedFromSession -= OnRemovedFromSession;
    }

    private void OnPlayerJoined(string playerId)
    {
        ReportStatus("有玩家加入世界：" + playerId);
    }

    private void OnPlayerLeft(string playerId)
    {
        ReportStatus("有玩家离开世界：" + playerId);
    }

    private void OnRemovedFromSession()
    {
        ISession oldSession = ActiveSession;
        UnsubscribeSessionEvents(oldSession);
        ActiveSession = null;
        JoinCodeChanged?.Invoke(string.Empty);
        SessionStateChanged?.Invoke(false);
        ReportStatus("已被移出世界或房主连接已结束");
    }

    private void SetBusy(bool value)
    {
        IsBusy = value;
        BusyChanged?.Invoke(value);
    }

    private void ReportStatus(string message)
    {
        StatusChanged?.Invoke(message);
        Debug.Log("[RelaySessionService] " + message, this);
    }
}