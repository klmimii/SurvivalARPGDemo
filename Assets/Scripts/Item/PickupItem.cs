using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickupItem : MonoBehaviour,IInteractable,IPoolable
{
    [SerializeField]
    //支持场景预设（静态）如果是地图上手动摆放的箱子，可以直接在Inspector面板中把ItemDefinition拖进去，并设置初始数量
    private ItemDefinition itemDefinition;
    [SerializeField]
    private int amount = 1;

    //支持怪物掉落（动态）如果怪物死亡时触发掉落，代码可以通过Instantiate生成掉落物预制体后，调用Setup动态注入数据
    public void Setup(ItemDefinition definition,int itemAmount)
    {
        itemDefinition = definition;
        amount = itemAmount;
    }
    public string GetPromptText()
    {
        //如果哪个掉落物忘了配置模板，UI依然能安全输出备用文本
        if(itemDefinition==null)
        {
            return "拾取物品";
        }

        return $"按E拾取{itemDefinition.displayName}x{amount}";
    }

    //按下E键触发TryInteract，脚本调用全局服务尝试存入背包
    public bool TryInteract(GameObject interactor)
    {
        //交互后尝试将物品添加进全局背包。通过全局单例/服务入口拿到IInventoryService
        //返回结果包括Successhe Message
        InventoryOperationResult result = GameBootstrap.InventoryService.TryAddItem(itemDefinition, amount);

        //如果存入失败
        if(!result.Success)
        {
            Debug.Log(result.Message);//控制台输出失败原因，例如背包空间不足
            return false;//交互失败 掉落物继续留在地上
        }

        //添加成功，打印成功信息
        Debug.Log(result.Message);
        GameBootstrap.Pool.Despawn(this);
        return true;

    }

    public void OnSpawned()
    {
        // Setup 会设置本次掉落的具体物品；这里可重置视觉、旋转、粒子等。
    }

    public void OnDespawned()
    {
        itemDefinition = null;
        amount = 0;
    }
}
