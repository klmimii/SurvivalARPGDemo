using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 静态类，不需要new实例化，也不保存任何内部状态，只是纯粹的工具箱/计算单元
/// </summary>
public static  class ConsumableItemService
{
    /// <summary>
    /// 尝试使用消耗品
    /// </summary>
    /// <param name="item">要使用的物品数据（配方、属性等）</param>
    /// <param name="userHealth">使用者的血量</param>
    /// <returns></returns>
    public static InventoryOperationResult TryUse(ItemDefinition item, Health userHealth)
    {
        //如果传入的物品为空或者物品类型不是消耗品，直接返回
        if(item==null||item.itemType!=ItemType.Consumale)
        {
            return InventoryOperationResult.Fail("该物品无法使用。");
        }
        //如果玩家血量为空或已经死亡了，直接返回
        if (userHealth == null||userHealth.IsDead)
        {
            return InventoryOperationResult.Fail("当前无法使用物品。");
        }
        //如果玩家当前血量大于玩家的最大血量，则返回玩家啊血量已满，防止玩家误操作浪费药水
        if(userHealth.CurrentHealth>=userHealth.MaxHealth)
        {
            return InventoryOperationResult.Fail("生命值已满。");
        }

        //尝试从背包中扣除一个物品
        InventoryOperationResult removeResult = GameBootstrap.InventoryService.TryRemoveItem(item, 1);
        
        if (!removeResult.Success)
        {
            return removeResult;//直接返回移除结果
        }

        //扣除成功才真正给角色回复生命值
        userHealth.Heal(item.healAmount);

        //返回成功结果及提示文
        return InventoryOperationResult.Succeed($"使用{item.displayName}，回复{item.healAmount}点生命。");
    }
}
