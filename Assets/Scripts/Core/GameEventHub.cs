using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 中央事件总线
/// </summary>
public class GameEventHub
{
    public event Action<ItemDefinition, int> ItemAdded;//当获得物品时触发，像订阅者传递ItemDefinition物品定义和int数量
    public event Action<string> EnemyKilled;//当击杀普通敌人时触发，传递string敌人的ID或名称
    public event Action<RecipeDefinition> RecipeCrafted;//合成配方时触发，传递RecipeDefinition配方对象
    public event Action<BuildingDefinition> BuildingPlaced;//建造建筑物时触发，传递Building Definition建筑对象
    public event Action<string> BossKilled;//击杀Boss时，传递string（Boss ID）

    public void PublishItemAdded(ItemDefinition item, int amount)//向外广播事件获得物品时
    {
        ItemAdded?.Invoke(item, amount);
    }

    public void PublishEnemyKilled(string enemyId)
    {
        EnemyKilled?.Invoke(enemyId);
    }

    public void PublishRecipeCrafted(RecipeDefinition recipe)
    {
        RecipeCrafted?.Invoke(recipe);
    }

    public void PublishBuildingPlaced(BuildingDefinition building)
    {
        BuildingPlaced?.Invoke(building);
    }

    public void PublishBossKilled(string bossId)
    {
        BossKilled?.Invoke(bossId);
    }
}
