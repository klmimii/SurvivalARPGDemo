using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背包数据单元类（实例数据层）把ItemDefinition什么东西和Amount多少个包装在一起
/// </summary>
[Serializable]//这个特性可以让这个类被Unity的序列化引擎识别，在Inspector面板上可以直观看到并编辑ItemStack数据
//后续如果使用JsonUtility蒋蓓蓓数据保存为Json文件，这个类可以直接被正确解析转化
public class ItemStack 
{
    //Definition只有get，一个背包格子一旦创建出来比如红药水，他的物品类是不应该在运行期间被随意替换的，如果要把红药水替换成蓝药水，规范的做法应该是替换整个ItemStack或清空它
    //设为只读可以防止其他脚本误操作修改了stack.Definition=another或清空它
    public ItemDefinition Definition { get; private set; }

    //物品的数量会频繁发生变动（拾取加数量，使用减数量），因此必须支持动态修改，设置为private set然后通过内部专用方法SetAdd等修改
    public int Amount { get; private set; }

    //属性表达式，如果Definition是空或者物品数量小于等于0时，则IsEmpty是空，
    public bool IsEmpty => Definition == null || Amount <= 0;


    //默认创建一个空槽位，自动调用Clear()初始化为null和0
    public ItemStack()
    {
        Clear();
    }

    //带参构造函数 一步完成
    public ItemStack(ItemDefinition definition, int amount)
    {
        Set(definition, amount);
    }

    /// <summary>
    /// 覆盖赋值，比如添加物品到背包里，格子什么都没放，要在这个格子里放物品时就调用这个方法
    /// </summary>
    /// <param name="definition"></param>
    /// <param name="amount"></param>
    public void Set(ItemDefinition definition, int amount)
    {
        this.Definition = definition;
        this.Amount = amount;
    }

    /// <summary>
    /// 增量累加，当玩家已有该物品的格子叠加数量时，直接调用Add
    /// </summary>
    /// <param name="amount"></param>
    public void Add(int amount)
    {
        Amount += amount;
    }

    /// <summary>
    /// 扣减数量，一旦数量被扣光，自动触发Clear()将Definition置空
    /// </summary>
    /// <param name="amount"></param>
    public void Remove(int amount)
    {
        Amount -= amount;

        if(Amount<=0)
        {
            Clear();

        }
    }

    //为什么ItemStack需要Empty状态？
    //背包不是只存已有物品的列表，而是固定数量的格子，一个空格子的Definition是null，Amount是0，这让你后续能够实现拖拽，交换装备槽与仓库
    public void Clear()
    {
        Definition = null;
        Amount = 0;
    }
}
