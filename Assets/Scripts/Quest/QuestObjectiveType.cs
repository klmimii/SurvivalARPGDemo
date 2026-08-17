using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 任务目标/完成条件的不同类型
/// </summary>
public enum QuestObjectiveType
{
    ObtainItem,//获得物品/收集资源
    KillEnemy,//击杀普通敌人
    CraftRecipe,//合成/配方制作
    BuildBuilding,//放置/建造建筑
    KillBoss//击杀特定Boss
}
