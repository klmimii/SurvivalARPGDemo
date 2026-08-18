using System;

/// <summary>
/// 保存“这台电脑拥有的网络玩家”。
/// 每个游戏进程只会有一个 IsOwner 为 true 的 Player。
/// 后续 HUD、背包和建造 UI 都通过这里绑定本机玩家。
/// </summary>
public static class LocalPlayerContext
{
    public static NetworkPlayer Player { get; private set; }

    public static event Action<NetworkPlayer> Changed;

    public static void Register(NetworkPlayer player)
    {
        if (player == null || Player == player)
        {
            return;
        }

        Player = player;
        Changed?.Invoke(Player);
    }

    public static void Unregister(NetworkPlayer player)
    {
        if (Player != player)
        {
            return;
        }

        Player = null;
        Changed?.Invoke(null);
    }
}