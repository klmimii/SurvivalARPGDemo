using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveGameData
{
    public int saveVersion = 1;//存档版本号
    public Vector3 playerPosition;//玩家角色状态，位置角度血量
    public Vector3 playerRotation;
    public int playerHealth;
    public List<InventorySlotSaveData> inventorySlots = new List<InventorySlotSaveData>();//背包系统数据（所有各自的物品与数量）
    public List<SaveableData> worldStates = new List<SaveableData>();//通用世界/机关宝箱状态数据
    public List<BuildingSaveData> buildings = new List<BuildingSaveData>();//地图上所有的已被建造的建筑数据
    public List<QuestSaveData> quests = new List<QuestSaveData>();//任务数据
}
