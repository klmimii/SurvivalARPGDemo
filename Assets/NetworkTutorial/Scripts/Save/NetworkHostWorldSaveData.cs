using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class NetworkHostWorldSaveData
{
    public int saveVersion = 1;
    public string savedUtc;

    public Vector3 hostPosition;
    public Vector3 hostRotation;
    public int hostHealth;

    public List<InventorySlotSaveData> hostInventory = new();
    public List<QuestSaveData> hostQuests = new();
    public List<NetworkBuildingSaveData> buildings = new();
    public List<NetworkGatherableSaveData> gatherables = new();
}