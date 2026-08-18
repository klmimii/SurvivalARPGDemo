using System;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// 可以被 NGO 序列化的背包格。
/// 网络上传稳定 itemId 和数量，不发送 ItemDefinition 引用。
/// </summary>
public struct NetworkItemStackData :
    INetworkSerializable,
    IEquatable<NetworkItemStackData>
{
    public FixedString64Bytes ItemId;
    public int Amount;

    public NetworkItemStackData(string itemId, int amount)
    {
        ItemId = new FixedString64Bytes(itemId ?? string.Empty);
        Amount = amount;
    }

    public bool IsEmpty => ItemId.Length == 0 || Amount <= 0;

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ItemId);
        serializer.SerializeValue(ref Amount);
    }

    public bool Equals(NetworkItemStackData other)
    {
        return ItemId.Equals(other.ItemId) &&
               Amount == other.Amount;
    }
}