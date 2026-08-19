using System;

[Serializable]
public sealed class NetworkGatherableSaveData
{
    public string sceneSaveId;
    public bool available;
    public float remainingRefreshSeconds;
}