using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 尝试添加物品接口。外部模块如掉落物拾取脚本，任务奖励发放系统商店购买系统需要依赖IInventoryService
/// 操作规范接口，不写具体逻辑，只规定任何想成为背包管理员的类，都必须提供一个TryAddItem TryRemoveItem CountItem（物品，数量）的方法，并告诉我成功了没有
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// 添加物品，不再是简单返回bool，而是返回结构化类型InventoryOperationResult，如果失败会返回失败原因
    /// </summary>
    /// <param name="definition">添加什么物品</param>
    /// <param name="amount">物品数量</param>
    /// <returns></returns>
    InventoryOperationResult TryAddItem(ItemDefinition definition, int amount);
    /// <summary>
    /// 移除物品
    /// </summary>
    /// <param name="definition">移除什么物品</param>
    /// <param name="amount">物品数量</param>
    /// <returns></returns>
    InventoryOperationResult TryRemoveItem(ItemDefinition definition, int amount);
    /// <summary>
    /// 暴露给外部查询数量的标准接口。外部系统在做判定时，，如NPC判断玩家身上是否有5个任务道具可以直接调用
    /// </summary>
    /// <param name="definition"></param>
    /// <returns></returns>
    int CountItem(ItemDefinition definition);
}
