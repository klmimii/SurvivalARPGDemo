using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本地背包服务实现类，实现了IInventoryService规定的规范，当有人向往背包塞东西的时候
/// 她负责做安全检查传进来的物品是不是空的，数量是不是小于0，检查通过后，它再把东西交给InventoryModel写入账本
/// </summary>
public class LocalInventoryService : IInventoryService
{
    private readonly InventoryModel inventoryModel;
    //控制反转与依赖注入，没有在内部自己new InventoryModel而是通过构造函数把外部已有的InventoryModel实例传进来保存
    //保证全局只能共享容易个InventoryModel数据源，无论玩家是在商店购买杀怪掉落还是任务奖励，只要调用的服务都持有同一个InventoryModel实例，数据就会统一更新

    public LocalInventoryService(InventoryModel inventoryModel)
    {
        //依赖注入，调用者在创建我的时候，把已有的inventoryModel传给我
        this.inventoryModel = inventoryModel;
    }

    /// <summary>
    /// 添加物品方法
    /// </summary>
    /// <param name="definition"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public InventoryOperationResult TryAddItem(ItemDefinition definition, int amount)
    {
        //业务参数拦截，检查传入的物品配置是否为空数量是否合法
        if(definition ==null||amount<=0)
        {
            return InventoryOperationResult.Fail("物品数据无效。");
        }

        //尝试添加，直接调用Model层的TryAdd
        if(!inventoryModel.TryAdd(definition,amount))
        {
            //如果Model层返回false（说明背包空间不足装不下）
            return InventoryOperationResult.Fail("背包空间不足");
        }

        //调用全局入口中的事件总线实例中的时间发布方法，传递具体的物品定义数据对象和增加的数量，内部执行ItemAdded?.Invoke(definition,amount),所有订阅这个事件的系统同时得到通知。
        GameBootstrap.Events.PublishItemAdded(definition, amount);

        //成功，返回带提示文本的成功结果
        return InventoryOperationResult.Succeed($"获得{definition.displayName}x{amount}");
    }

    /// <summary>
    /// 尝试移除物品方法
    /// </summary>
    /// <param name="definition"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public InventoryOperationResult TryRemoveItem(ItemDefinition definition, int amount)
    {
        if(!inventoryModel.TryRemove(definition,amount))
        {
            return InventoryOperationResult.Fail("物品数量不足。");
        }

        return InventoryOperationResult.Succeed();
    }


    public int CountItem(ItemDefinition definition)
    {
        return inventoryModel.CountItem(definition);
    }
}
