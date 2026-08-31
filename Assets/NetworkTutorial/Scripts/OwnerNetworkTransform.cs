using Unity.Netcode.Components;

/// <summary>
/// NetworkTransform是NGO提供的一个组件，他自动同步游戏物体的位置、旋转、缩放到客户端。其他客户端看到这个物体在移动
/// 这个脚本继承NetworkTransform，他重写了OnIsServerAuthoritative()方法，固定返回false
/// 
/// </summary>
public sealed class OwnerNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        // false = 不是服务器权威，而是拥有这个对象的客户端负责位置。也就是客户端权威，因为需要考虑手感优先，移动要立即响应，不能等服务器回包
        return false;
    }
}