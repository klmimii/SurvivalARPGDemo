using Unity.Netcode.Components;

/// <summary>
/// 测试阶段使用“拥有者权威”的NetworkTransform。
/// 每个玩家在自己的电脑上移动自己，再把位置同步给其他端。
/// </summary>
public sealed class OwnerNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        // false = 不是服务器权威，而是拥有这个对象的客户端负责位置。
        return false;
    }
}