using System;

/// <summary>
/// 一个静态容器，保存“这台电脑拥有的网络玩家”。任何脚本想获取本机玩家时，不需要到处找引用，直接用LocalPlayerContext.Player就行
/// 每个游戏进程只会有一个 IsOwner 为 true 的 Player。
/// 后续 HUD、背包和建造 UI 都通过这里绑定本机玩家。
/// </summary>
public static class LocalPlayerContext
{
    public static NetworkPlayer Player { get; private set; }
    //玩家登记/移除时触发，触发此事件，其他脚本可以订阅这个事件，比如UI在玩家生成时自动显示血条，在玩家销毁时自动隐藏
    public static event Action<NetworkPlayer> Changed;

    /// <summary>
    /// 注册，NetworkPlayer在onNetworkSpawn中调用，把自己登记进去
    /// </summary>
    /// <param name="player"></param>
    public static void Register(NetworkPlayer player)
    {
        //如果传进来的player是空的或者已经和当前的player相同，直接返回
        if (player == null || Player == player)
        {
            return;
        }
        //把这个传进来的玩家设为当前玩家
        Player = player;
        //触发Changed事件，通知所有订阅者本机玩家已经变了
        Changed?.Invoke(Player);
    }

    /// <summary>
    /// 取消注册，NetworkPlayer在OnNetworkDespawn中调用，把自己移除
    /// </summary>
    /// <param name="player"></param>
    public static void Unregister(NetworkPlayer player)
    {
        //如果当前存储的Player和传入的不一致，直接返回
        if (Player != player)
        {
            return;
        }
        //将Player设为空
        Player = null;
        //触发Changed事件，通知所有订阅者本机玩家已经消失了。
        Changed?.Invoke(null);
    }
}