using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventorySlotSaveData
{
    public string itemId;//物品的唯一ID
    public int amount;//该格子里堆叠的物品数量
}
