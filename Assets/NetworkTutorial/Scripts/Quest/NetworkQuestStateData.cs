using System;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// 网络只同步稳定 ID、目标下标、进度和状态，不直接传 ScriptableObject。
/// </summary>
public struct NetworkQuestStateData :
    INetworkSerializable,
    IEquatable<NetworkQuestStateData>
{
    public FixedString64Bytes QuestId;
    public int ObjectiveIndex;
    public int Progress;
    public int Status;

    public NetworkQuestStateData(
        string questId,
        int objectiveIndex,
        int progress,
        QuestStatus status)
    {
        QuestId = new FixedString64Bytes(questId);
        ObjectiveIndex = objectiveIndex;
        Progress = progress;
        Status = (int)status;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref QuestId);
        serializer.SerializeValue(ref ObjectiveIndex);
        serializer.SerializeValue(ref Progress);
        serializer.SerializeValue(ref Status);
    }

    public bool Equals(NetworkQuestStateData other)
    {
        return QuestId.Equals(other.QuestId) &&
            ObjectiveIndex == other.ObjectiveIndex &&
            Progress == other.Progress &&
            Status == other.Status;
    }
}