using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

//配置单个任务的奖励的物品和数量
[Serializable]
public class QuestReward
{
    public ItemDefinition item;//引用的物品数据定义对象
    [Min(1)] public int amount = 1;//给与该物品的数量
}