using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// 只负责Unity服务、Session和Relay，不直接操作UI。
/// WithRelayNetwork会通过SDK自动启动NGO Host或Client。
/// 写成sealed类禁止被继承，防御性变成。
/// </summary>
public sealed class RelaySessionService : MonoBehaviour
{
    //ISession是Unity Multiplayer Service SDK中定义的一个接口。他不关心这个房间具体是怎么实现的，但他承诺了以下功能
    //session.code加入码；Players当前在房间里的玩家的ID列表；MaxPlayers房间最大容量等等
    //ActiveSession就是当前玩家所在的房间。Host创建时得到它，Client加入时也得到它，两者拿到的是同一个房间的不同视角。
    public ISession ActiveSession { get; private set; }
    public bool IsBusy { get; private set; }
    public bool HasSession => ActiveSession != null;

    //这些事件给UI脚本订阅
    public event Action<string> StatusChanged;//状态文字变化
    public event Action<string> JoinCodeChanged;//加入码变化
    public event Action<bool> BusyChanged;//是否在处理网络操作
    public event Action<bool> SessionStateChanged;//是否在房间中

    /// <summary>
    /// 统一初始化入口
    /// async标记是一个异步方法，async void表示发射后不管的异步方法，调用方不饿能等待他完成，无法知道他什么时候结束容易捕获异常。async Task表示可以等待完成的异步方法，调用方能等待他完成，调用方可以写await CreateWorldAsync()
    /// </summary>
    /// <returns></returns>
    public async Task InitializeAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            ReportStatus("正在初始化Unity Gaming Services...");
            //await表示在这里等待Unity Gaming Services初始化完成，但等待期间不阻塞主线程。
            await UnityServices.InitializeAsync();//跟Unity云端握手，验证SDK版本
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            ReportStatus("正在匿名登录...");
            //AuthenticationService是Unity Gaming Services的给当前玩家分配的一个身份标识符，让Unity的云服务器知道是谁再发起请求
            await AuthenticationService.Instance.SignInAnonymouslyAsync();//向服务器请求一个匿名身份
        }

        ReportStatus("网络服务已就绪，PlayerId=" + AuthenticationService.Instance.PlayerId);
    }

    /// <summary>
    /// Host创建房间
    /// </summary>
    /// <returns></returns>
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
            //调用初始化入口方法
            await InitializeAsync();
            ReportStatus("正在创建Relay世界...");

            //会话设置，这里向Relay服务申请一个专用的中继服务地址。自动在你的电脑上调用NetworkManager.StartHost()启动NGO主机
            SessionOptions options = new SessionOptions
            {
                MaxPlayers = 2,//房间最大玩家数量
                IsPrivate = true,//是否私密房间
                Name = "SurvivalARPGDemo"//房间名称
            }.WithRelayNetwork();//Unity.Services.Multiplayer命名空间中，是Unity官方为SessionOptions类专门添加的一个方法。作用是为这个SessionOptions配置Relay相关的网络参数，并返回一个SessionOptions实例

            //这个方法Unity会根据传入的options参数在Unity的会话服务中创建一个新的房间记录。因为配置了WithRelayNetwork，还会自动向Relay服务申请一个中继节点，为后续网络传输准备好通道
            //并将当前玩家设为主机。
            ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);//向Relay服务器申请一个中继节点，分配端口

            //订阅会话事件
            SubscribeSessionEvents(ActiveSession);

            //广播加入码改变事件
            JoinCodeChanged?.Invoke(ActiveSession.Code);
            SessionStateChanged?.Invoke(true);//广播会话状态改变事件
            ReportStatus("世界创建成功，等待另一名玩家加入");
        }
        //当CreateSessionAsync网络请求失败时，用Exception作为捕获类型，可以保证无论发生什么类型的错误，程序都不会崩溃，进入catch处理
        catch (Exception exception)//Exception时C#中所有异常的基类，当程序运行时发生了以为外情况，系统会创建一个Exception对象，里面包含了错误的详细信息
        {
            Debug.LogException(exception, this);
            ActiveSession = null;
            SessionStateChanged?.Invoke(false);
            ReportStatus("创建失败：" + exception.Message);
        }
        finally//finally中的代码，无论try块中是否发生异常，都会被执行
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// 客户端加入房间
    /// </summary>
    /// <param name="joinCode"></param>
    /// <returns>加入码</returns>
    public async Task JoinWorldAsync(string joinCode)
    {
        //防止用户重复点击加入按钮
        if (IsBusy)
        {
            ReportStatus("正在处理上一个网络操作，请稍候");
            return;
        }
        //防止玩家已经在房间里，又试图加入另一个房间
        if (ActiveSession != null)
        {
            ReportStatus("已经在一个世界中，请先离开");
            return;
        }
        //标准化处理加入码，Trim去掉空格，ToUpperInvatiant统一转成大写。
        string normalizedCode = joinCode == null ? string.Empty : joinCode.Trim().ToUpperInvariant();
        //如果用户每输入任何内容，提示请先输入加入码
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            ReportStatus("请先输入Host显示的加入码");
            return;
        }

        //开始加入流程
        //首先锁定UI，让加入变灰，放UI重复点击
        SetBusy(true);

        try
        {
            await InitializeAsync();//确保云服务已经初始化
            ReportStatus("正在通过加入码连接Relay世界...");

            // SDK会查找Session、配置Relay并自动启动NGO Client。向Unity云端发送我要用这个加入码找房间的请求。如果找到且未满员，服务器会返回该房间的Isession对象，并自动配置Relay连接
            ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(normalizedCode);

            //监听其他玩家的进出事件，实时更新UI
            SubscribeSessionEvents(ActiveSession);

            //告诉UI已经进入房间，UI可以切换界面
            SessionStateChanged?.Invoke(true);
            ReportStatus("加入世界成功");
        }
        catch (Exception exception)//失败时进入catch
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

    /// <summary>
    /// 离开世界时
    /// </summary>
    /// <returns></returns>
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
            //广播会话状态改变事件
            SessionStateChanged?.Invoke(false);
            SetBusy(false);
        }
    }

    /// <summary>
    /// 订阅会话事件
    /// </summary>
    /// <param name="session"></param>
    private void SubscribeSessionEvents(ISession session)
    {
        if (session == null) return;
        //ISession中定义了一系列事件
        session.PlayerJoined += OnPlayerJoined;//其他玩家加入事件
        session.PlayerHasLeft += OnPlayerLeft;//其他玩家主动离开或掉线时触发
        session.RemovedFromSession += OnRemovedFromSession;//当前玩家被移除房间时出啊发
    }

    /// <summary>
    /// 取消订阅会话事件
    /// </summary>
    /// <param name="session"></param>
    private void UnsubscribeSessionEvents(ISession session)
    {
        if (session == null) return;

        session.PlayerJoined -= OnPlayerJoined;
        session.PlayerHasLeft -= OnPlayerLeft;
        session.RemovedFromSession -= OnRemovedFromSession;
    }

    /// <summary>
    /// 打印玩家进入世界的信息
    /// </summary>
    /// <param name="playerId"></param>
    private void OnPlayerJoined(string playerId)
    {
        ReportStatus("有玩家加入世界：" + playerId);
    }

    /// <summary>
    /// 打印玩家离开世界的信息
    /// </summary>
    /// <param name="playerId"></param>
    private void OnPlayerLeft(string playerId)
    {
        ReportStatus("有玩家离开世界：" + playerId);
    }

    /// <summary>
    /// 玩家被动离开世界时调用
    /// </summary>
    private void OnRemovedFromSession()
    {
        ISession oldSession = ActiveSession;//把当前正在使用的房间对象暂存到一个局部变量里，防御性变成，防止万一有另一个线程或事件回调意外的修改了Active Session的值
        UnsubscribeSessionEvents(oldSession);//取消订阅这个房间上的所有事件监听
        ActiveSession = null;//清除类成员变量对房间对象的引用
        JoinCodeChanged?.Invoke(string.Empty);//通知所有订阅了JoinCodeChanged事件的UI元素，加入码已经被清空了，把显示框里面的内容擦掉
        SessionStateChanged?.Invoke(false);//通知所有订阅了会话状态事件的会话状态已经失效了。
        ReportStatus("已被移出世界或房主连接已结束");
    }

    /// <summary>
    /// 修改网络操作忙碌状态并通知所有订阅者
    /// </summary>
    /// <param name="value"></param>
    private void SetBusy(bool value)
    {
        //当前是否在执行网络操作设为传进来的值
        IsBusy = value;
        BusyChanged?.Invoke(value);
    }

    /// <summary>
    /// 广播状态改变事件并在控制台打印信息
    /// </summary>
    /// <param name="message"></param>
    private void ReportStatus(string message)
    {
        StatusChanged?.Invoke(message);
        Debug.Log("[RelaySessionService] " + message, this);
    }
}